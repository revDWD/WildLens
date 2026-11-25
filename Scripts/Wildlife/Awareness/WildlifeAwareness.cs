using System;
using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;

namespace WildLens.Wildlife
{
    public enum AwarenessState { Calm, Aware, Alert, Fleeing }

    /// <summary>
    /// Per-animal perception of the player: Calm → Aware → Alert → Fleeing.
    ///
    /// The animal accumulates <see cref="Suspicion"/> (0–1) while the player is inside its awareness radius.
    /// The rate depends on:
    ///   • proximity    — rises steeply as the player closes from the awareness radius toward the flee radius,
    ///   • player noise — standing still ≪ creeping in camera mode &lt; walking ≪ sprinting (<see cref="PlayerPresence.Noise"/>),
    ///   • line of sight — trees, bushes and rocks between animal and player cut it by ~65%,
    ///   • memory       — an animal that fled recently is jumpier for a while.
    /// Keeping still lets suspicion fade, so a patient photographer can wait an animal out.
    ///
    /// States (with hysteresis so they don't flicker):
    ///   Aware   suspicion ≥ 0.25 inside the awareness radius — glances at the player, keeps doing what it was doing.
    ///   Alert   suspicion ≥ 0.6 inside the alert radius — stops and watches. Suspicion now grows at
    ///           1/alertPatience of the normal rate, and if the player keeps approaching for longer than the
    ///           patience it flees (deer watch for a long time, rabbits barely at all).
    ///   Fleeing suspicion reaches 1, the player enters the flee radius while moving, or a sprinting player startles it.
    ///
    /// This component only decides; brains (GroundAnimal, FlyingAnimal) subscribe to <see cref="StateChanged"/>
    /// and choose how to act, then call <see cref="EndFlee"/> when they reach safety.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class WildlifeAwareness : MonoBehaviour
    {
        public const float AwareThreshold = 0.25f;
        public const float AlertThreshold = 0.6f;
        private const float AwareExit = 0.12f;
        private const float AlertExit = 0.45f;

        private static readonly List<WildlifeAwareness> ActiveList = new List<WildlifeAwareness>();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private SpeciesDefinition _species;
        private Transform _eye;
        private float _losTimer;
        private bool _hasLineOfSight = true;
        private float _lastDistance = -1f;
        private float _approachRate;
        private float _alertTime;
        private float _spookMemory;
        private float _fleeTimeout;

        public AwarenessState State { get; private set; } = AwarenessState.Calm;
        /// <summary>0 = oblivious, 1 = about to flee.</summary>
        public float Suspicion { get; private set; }
        public float ThreatDistance { get; private set; } = float.MaxValue;
        public Vector3 ThreatPosition => PlayerPresence.Position;
        public bool HasLineOfSight => _hasLineOfSight;

        /// <summary>
        /// Set by the brain while the animal is somewhere it feels safe (squirrel on a trunk, bird in the air):
        /// suspicion builds slowly and only a very close approach makes it flee.
        /// </summary>
        public bool Sheltered { get; set; }

        /// <summary>(previous, current)</summary>
        public event Action<AwarenessState, AwarenessState> StateChanged;

        public void Initialize(SpeciesDefinition species, Transform eye)
        {
            _species = species;
            _eye = eye;
            _losTimer = UnityEngine.Random.Range(0f, 0.3f);
        }

        private void OnEnable() => ActiveList.Add(this);
        private void OnDisable() => ActiveList.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveList.Clear();

        /// <summary>
        /// A sudden sound (e.g. the camera shutter) adds suspicion to nearby animals, falling off with distance.
        /// </summary>
        public static void EmitSound(Vector3 position, float radius, float strength)
        {
            foreach (var a in ActiveList)
            {
                if (a._species == null || a.State == AwarenessState.Fleeing) continue;
                float d = Vector3.Distance(a.transform.position, position);
                if (d > radius) continue;
                a.Suspicion = Mathf.Clamp01(a.Suspicion + strength * (1f - d / radius) * (a.Sheltered ? 0.4f : 1f));
            }
        }

        /// <summary>Forces flight (also used by brains for scripted scares).</summary>
        public void Flee()
        {
            Suspicion = 1f;
            _fleeTimeout = 12f;
            SetState(AwarenessState.Fleeing);
        }

        /// <summary>Called by the brain once it has reached safety.</summary>
        public void EndFlee()
        {
            if (State != AwarenessState.Fleeing) return;
            Suspicion = 0.5f;
            _spookMemory = 30f;
            SetState(AwarenessState.Aware);
        }

        private Vector3 EyePosition => _eye != null ? _eye.position : transform.position + Vector3.up * 0.5f;

        private void Update()
        {
            if (_species == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _spookMemory = Mathf.Max(0f, _spookMemory - dt);

            if (State == AwarenessState.Fleeing)
            {
                _fleeTimeout -= dt;
                if (_fleeTimeout <= 0f) EndFlee();
                return;
            }

            if (!PlayerPresence.Exists)
            {
                Suspicion = Mathf.MoveTowards(Suspicion, 0f, _species.suspicionDecay * dt);
                ThreatDistance = float.MaxValue;
                Evaluate(float.MaxValue);
                return;
            }

            Vector3 eye = EyePosition;
            Vector3 head = PlayerPresence.HeadPosition;
            float d = Vector3.Distance(eye, head);
            ThreatDistance = d;
            if (_lastDistance >= 0f)
                _approachRate = Mathf.Lerp(_approachRate, (_lastDistance - d) / dt, 1f - Mathf.Exp(-4f * dt));
            _lastDistance = d;

            _losTimer -= dt;
            if (_losTimer <= 0f)
            {
                _losTimer = 0.3f + UnityEngine.Random.value * 0.1f;
                _hasLineOfSight = CheckLineOfSight(eye, head);
            }

            float noise = PlayerPresence.Noise;
            float aware = _species.awarenessRadius;
            float alert = _species.alertRadius;
            float flee = _species.fleeRadius * (Sheltered ? 0.4f : 1f);

            // Startle: a sprinting player crashing through nearby cover.
            if (!Sheltered && PlayerPresence.IsSprinting && d < alert * _species.startleSensitivity) { Flee(); return; }
            // Too close: flee unless the animal is oblivious and the player is perfectly still.
            if (d < flee && (noise >= 0.3f || Suspicion > AwareThreshold)) { Flee(); return; }

            if (d < aware)
            {
                float proximity = Mathf.InverseLerp(aware, flee, d);
                float sight = _hasLineOfSight ? 1f : 0.35f;
                float shelter = Sheltered ? 0.4f : 1f;
                float memory = _spookMemory > 0f ? 1.6f : 1f;
                float gain = _species.suspicionGain * noise * sight * shelter * memory * (0.25f + proximity * proximity * 1.75f);
                // Once watching, patient species escalate slowly: a deer stares for a while, a rabbit barely at all.
                if (State == AwarenessState.Alert) gain /= Mathf.Max(1f, _species.alertPatience);
                float stillDecay = noise < 0.3f ? _species.suspicionDecay * 0.6f : 0f;
                Suspicion += (gain - stillDecay) * dt;
            }
            else
            {
                Suspicion -= _species.suspicionDecay * dt;
            }
            // Escalation goes through Alert: a distant animal can be wary but only one that is
            // watching from inside its alert radius can talk itself into fleeing.
            float ceiling = State == AwarenessState.Alert ? 1f : d > alert ? AlertThreshold - 0.01f : 0.99f;
            Suspicion = Mathf.Clamp(Suspicion, 0f, ceiling);

            if (State == AwarenessState.Alert)
            {
                _alertTime += dt;
                // Watched for long enough and the observer is still coming closer: decide to leave.
                if (_alertTime > _species.alertPatience && _approachRate > 0.25f) { Flee(); return; }
            }
            if (Suspicion >= 1f) { Flee(); return; }

            Evaluate(d);
        }

        private void Evaluate(float d)
        {
            var next = State;
            switch (State)
            {
                case AwarenessState.Calm:
                    if (Suspicion >= AwareThreshold && d < _species.awarenessRadius) next = AwarenessState.Aware;
                    break;
                case AwarenessState.Aware:
                    if (Suspicion < AwareExit || d > _species.awarenessRadius * 1.1f) next = AwarenessState.Calm;
                    break;
                case AwarenessState.Alert:
                    if (Suspicion < AlertExit || d > _species.alertRadius * 1.2f) next = AwarenessState.Aware;
                    break;
            }
            if (next == AwarenessState.Aware && Suspicion >= AlertThreshold && d < _species.alertRadius)
                next = AwarenessState.Alert;
            SetState(next);
        }

        private void SetState(AwarenessState next)
        {
            if (next == State) return;
            var previous = State;
            State = next;
            if (next == AwarenessState.Alert) _alertTime = 0f;
            StateChanged?.Invoke(previous, next);
        }

        private bool CheckLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.01f) return true;
            int count = Physics.RaycastNonAlloc(from, dir / len, Hits, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float nearest = float.MaxValue;
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                var col = Hits[i].collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (PlayerPresence.Root != null && col.transform.IsChildOf(PlayerPresence.Root)) continue;
                if (col.GetComponentInParent<AnimalAgent>() != null) continue; // other animals don't hide the player
                if (Hits[i].distance < nearest) { nearest = Hits[i].distance; blocked = true; }
            }
            return !blocked;
        }

        private void OnDrawGizmosSelected()
        {
            var s = _species;
            if (s == null) return;
            Vector3 p = transform.position;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f); Gizmos.DrawWireSphere(p, s.awarenessRadius);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.7f); Gizmos.DrawWireSphere(p, s.alertRadius);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f); Gizmos.DrawWireSphere(p, s.fleeRadius);
        }
    }
}
