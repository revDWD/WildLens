using UnityEngine;
using WildLens.Core;
using WildLens.Data;

namespace WildLens.Wildlife
{
    /// <summary>
    /// Brain + flight model for soaring birds (Red-tailed Hawk).
    ///
    /// Observable states (<see cref="AnimalAgent.Activity"/>):
    ///   Flying     circling on drifting thermals, or gliding to a new one
    ///   Landing    final approach to a perch: slows, flares and flaps
    ///   Perched    sits on a tall tree or boulder, scanning; watches the player when Aware/Alert
    ///   TakingOff  powerful flaps to climb away: when its perch time is up, or when awareness says Fleeing
    ///
    /// Awareness only matters while perched; in the air the bird is <see cref="WildlifeAwareness.Sheltered"/>.
    /// Velocity is steered with a limited turn rate and the body banks into turns, so paths are smooth arcs.
    /// </summary>
    public class FlyingAnimal : AnimalAgent
    {
        private enum Mode { Circling, Transit, Approach, Perched, TakingOff }

        [SerializeField] private float minAltitude = 20f;
        [SerializeField] private float maxAltitude = 38f;
        [SerializeField] private float perchSearchRadius = 70f;
        [SerializeField] private float landingDistance = 10f;

        private Mode _mode;
        private float _timer;
        private Vector3 _vel;

        private Vector3 _circleCenter;
        private float _circleRadius;
        private float _circleAngle;
        private float _circleDir;
        private float _altitude;

        private Vector3 _transitTarget;
        private Vector3 _perchPoint;
        private float _perchYaw;
        private float _perchShiftTimer;
        private float _bank;
        private float _flare;
        private bool _fledPerch;

        public bool IsFlapping { get; private set; }
        public bool IsPerched => _mode == Mode.Perched;
        /// <summary>Roll angle in degrees about the body's forward axis (negative = right wing down).</summary>
        public float Bank => _bank;
        /// <summary>0..1 landing flare (body pitched up, wings forward).</summary>
        public float Flare => _flare;

        private float Cruise => Species.walkSpeed * Temperament;

        public override void Initialize(SpeciesDefinition species, Vector3 home, WildlifeWorld world, int seed)
        {
            base.Initialize(species, home, world, seed);
            _altitude = Range(minAltitude, maxAltitude);
            _circleDir = Chance(0.5f) ? 1f : -1f;
            BeginCircle(home, Range(0f, Mathf.PI * 2f));
            transform.position = CirclePoint(_circleAngle);
            _vel = CircleTangent(_circleAngle) * Cruise;
            transform.rotation = Quaternion.LookRotation(_vel);
            if (Awareness != null) Awareness.StateChanged += OnAwarenessChanged;
        }

        private void OnDestroy()
        {
            if (Awareness != null) Awareness.StateChanged -= OnAwarenessChanged;
        }

        private void OnAwarenessChanged(AwarenessState previous, AwarenessState next)
        {
            if (_mode != Mode.Perched) return;
            if (next == AwarenessState.Fleeing) TakeOff(true);
        }

        protected override void Tick(float dt)
        {
            _timer -= dt;
            IsFlapping = false;
            AttentionPoint = null;
            if (Awareness != null) Awareness.Sheltered = _mode != Mode.Perched;

            switch (_mode)
            {
                case Mode.Circling: TickCircling(dt); break;
                case Mode.Transit: TickTransit(dt); break;
                case Mode.Approach: TickApproach(dt); break;
                case Mode.Perched: TickPerched(dt); return;
                case Mode.TakingOff: TickTakeOff(dt); break;
            }

            if (_mode != Mode.Approach) _flare = Mathf.MoveTowards(_flare, 0f, dt * 2f);
            Integrate(dt);
        }

        // ------------------------------------------------------------------ flying

        private void BeginCircle(Vector3 center, float angle)
        {
            _mode = Mode.Circling;
            _circleCenter = TerrainUtil.Flat(center);
            _circleRadius = Range(16f, 34f);
            _circleAngle = angle;
            _timer = Range(DurationFor(AnimalActivity.Flying, new Vector2(20f, 40f)));
            SetActivity(AnimalActivity.Flying);
        }

        private Vector3 CirclePoint(float angle)
        {
            Vector3 p = _circleCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _circleRadius;
            p.y = TerrainUtil.SampleHeight(_circleCenter) + _altitude + Noise(0.07f, 3f) * 4f;
            return p;
        }

        private Vector3 CircleTangent(float angle) =>
            new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _circleDir;

        private void TickCircling(float dt)
        {
            _circleAngle += _circleDir * (Cruise / _circleRadius) * dt;
            // Thermals drift with the wind.
            _circleCenter += new Vector3(Noise(0.05f, 1f), 0f, Noise(0.05f, 2f)) * (0.8f * dt);

            Vector3 desired = CirclePoint(_circleAngle + _circleDir * 0.35f);
            Steer(desired, Cruise, 0.9f, dt);
            IsFlapping = desired.y - transform.position.y > 3f || Noise(0.15f, 7f) > 0.8f;

            if (_timer <= 0f) ChooseNextFlight();
        }

        private void ChooseNextFlight()
        {
            var option = Species.PickBehavior(Rng);
            if (option != null && option.activity == AnimalActivity.Perched && TryStartPerch()) return;

            if (Chance(0.4f))
            {
                // Glide to a new thermal within the territory.
                Vector2 r = Random.insideUnitCircle * Species.wanderRadius;
                _transitTarget = HomePosition + new Vector3(r.x, 0f, r.y);
                _mode = Mode.Transit;
                _timer = 40f;
                SetActivity(AnimalActivity.Flying);
                return;
            }

            _altitude = Mathf.Clamp(_altitude + Range(-6f, 6f), minAltitude, maxAltitude);
            BeginCircle(_circleCenter, _circleAngle);
        }

        private void TickTransit(float dt)
        {
            Vector3 target = _transitTarget;
            target.y = TerrainUtil.SampleHeight(target) + _altitude;
            Steer(target, Cruise * 1.2f, 0.6f, dt);
            if (TerrainUtil.FlatDistance(transform.position, target) < 15f || _timer <= 0f)
            {
                Vector3 pos = transform.position;
                BeginCircle(target, Mathf.Atan2(pos.z - target.z, pos.x - target.x));
            }
        }

        // ------------------------------------------------------------------ landing & perching

        private bool TryStartPerch()
        {
            if (World == null || !World.TryGetRandomPerch(transform.position, perchSearchRadius, out var perch)) return false;
            // Won't choose a perch right next to the observer.
            if (TryGetPlayer(out Vector3 player, out _) && TerrainUtil.FlatDistance(player, perch.basePosition) < Species.alertRadius)
                return false;

            _perchPoint = perch.Top + Vector3.up * 0.02f;
            _mode = Mode.Approach;
            _timer = 30f;
            SetActivity(AnimalActivity.Flying);
            return true;
        }

        private void TickApproach(float dt)
        {
            Vector3 to = _perchPoint - transform.position;
            float dist = to.magnitude;
            bool landing = dist < landingDistance;
            if (landing) SetActivity(AnimalActivity.Landing);

            float speed = Mathf.Lerp(2f, Cruise, Mathf.Clamp01(dist / 30f));
            Steer(_perchPoint, speed, dist < 15f ? 3f : 1.2f, dt);
            IsFlapping = landing; // braking flaps
            _flare = Mathf.MoveTowards(_flare, landing ? 1f - Mathf.Clamp01(dist / landingDistance) * 0.5f : 0f, dt * 2f);

            if (dist < 0.6f)
            {
                transform.position = _perchPoint;
                Vector3 flatVel = TerrainUtil.Flat(_vel);
                _perchYaw = flatVel.sqrMagnitude > 0.01f ? Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
                _vel = Vector3.zero;
                Velocity = Vector3.zero;
                _bank = 0f;
                _flare = 0f;
                _mode = Mode.Perched;
                _timer = Range(DurationFor(AnimalActivity.Perched, new Vector2(12f, 25f)));
                _perchShiftTimer = Range(3f, 7f);
                SetActivity(AnimalActivity.Perched);
            }
            else if (_timer <= 0f)
            {
                BeginCircle(transform.position, 0f);
            }
        }

        private void TickPerched(float dt)
        {
            Velocity = Vector3.zero;
            var state = Awareness != null ? Awareness.State : AwarenessState.Calm;
            bool watching = state == AwarenessState.Aware || state == AwarenessState.Alert;
            if (watching) AttentionPoint = Awareness.ThreatPosition;

            _perchShiftTimer -= dt;
            if (_perchShiftTimer <= 0f)
            {
                _perchShiftTimer = Range(4f, 9f);
                // An alert bird turns to face the observer; a calm one shuffles about.
                if (state == AwarenessState.Alert)
                {
                    Vector3 to = TerrainUtil.Flat(Awareness.ThreatPosition - transform.position);
                    if (to.sqrMagnitude > 0.01f) _perchYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + Range(-30f, 30f);
                }
                else _perchYaw += Range(-70f, 70f);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _perchYaw, 0f), 1f - Mathf.Exp(-3f * dt));

            if (_timer <= 0f) TakeOff(false);
        }

        private void TakeOff(bool disturbed)
        {
            Vector3 forward = transform.forward;
            if (disturbed && Awareness != null)
            {
                // Launch away from the threat.
                Vector3 away = TerrainUtil.Flat(transform.position - Awareness.ThreatPosition);
                if (away.sqrMagnitude > 0.01f) forward = away.normalized;
            }
            _mode = Mode.TakingOff;
            _timer = 12f;
            _fledPerch = disturbed;
            _vel = forward * 3f + Vector3.up * 2.5f;
            transform.rotation = Quaternion.LookRotation(_vel);
            SetActivity(AnimalActivity.TakingOff);
        }

        private void TickTakeOff(float dt)
        {
            Vector3 fwd = TerrainUtil.Flat(_vel);
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            Vector3 target = transform.position + fwd.normalized * 20f + Vector3.up * 12f;
            Steer(target, Cruise, 1.5f, dt);
            IsFlapping = true;

            float height = transform.position.y - TerrainUtil.SampleHeight(transform.position);
            if (height > _altitude * 0.7f || _timer <= 0f)
            {
                if (_fledPerch && Awareness != null) Awareness.EndFlee();
                // Join a circle that passes through the current position.
                Vector3 right = Vector3.Cross(Vector3.up, fwd.normalized) * _circleDir;
                float radius = Range(16f, 34f);
                Vector3 center = transform.position + right * radius;
                BeginCircle(center, Mathf.Atan2(transform.position.z - center.z, transform.position.x - center.x));
                _circleRadius = radius;
            }
        }

        // ------------------------------------------------------------------ flight model

        private void Steer(Vector3 target, float speed, float turnRateRadians, float dt)
        {
            Vector3 desired = target - transform.position;
            if (desired.sqrMagnitude < 0.0001f) return;
            desired = desired.normalized * speed;
            if (_vel.sqrMagnitude < 0.01f) _vel = desired.normalized * 0.5f;
            _vel = Vector3.RotateTowards(_vel, desired, turnRateRadians * dt, 4f * dt);
        }

        private void Integrate(float dt)
        {
            Vector3 prevFlat = TerrainUtil.Flat(transform.forward);
            Vector3 pos = transform.position + _vel * dt;

            if (_mode != Mode.Approach)
            {
                float minY = TerrainUtil.SampleHeight(pos) + 3f;
                if (pos.y < minY) { pos.y = minY; if (_vel.y < 0f) _vel.y = 0f; }
            }
            transform.position = pos;
            Velocity = _vel;

            if (_vel.sqrMagnitude > 0.01f)
            {
                Vector3 newFlat = TerrainUtil.Flat(_vel);
                float yawRate = Vector3.SignedAngle(prevFlat, newFlat, Vector3.up) / dt;
                float targetBank = Mathf.Clamp(-yawRate * 0.9f, -35f, 35f) * (1f - _flare);
                _bank = Mathf.Lerp(_bank, targetBank, 1f - Mathf.Exp(-3f * dt));
                // Look along the flat heading while flaring so the pitch-up reads clearly.
                Vector3 lookDir = _flare > 0.01f && newFlat.sqrMagnitude > 0.0001f ? Vector3.Slerp(_vel.normalized, newFlat.normalized, _flare) : _vel.normalized;
                Quaternion look = Quaternion.LookRotation(lookDir, Vector3.up);
                Quaternion target = look * Quaternion.Euler(-30f * _flare, 0f, _bank);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-6f * dt));
            }

            if (IsFlapping)
            {
                float rate = _mode == Mode.TakingOff ? 1.5f : 1f; // hard flaps on take-off
                GaitPhase = Mathf.Repeat(GaitPhase + dt * Species.gaitFrequency * rate * 2f * Mathf.PI, Mathf.PI * 2f);
            }
        }

        private Vector2 DurationFor(AnimalActivity activity, Vector2 fallback)
        {
            if (Species.behaviors != null)
                foreach (var b in Species.behaviors)
                    if (b.activity == activity) return b.duration;
            return fallback;
        }
    }
}
