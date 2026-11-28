using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>
    /// A covered volume (1.10): the blind's room, the strip under the Reserve Board's eaves, the Rock Shelter's alcove. An oriented box
    /// in this object's LOCAL space (so it follows whatever group the builder parented it under), registered in a static list
    /// (no scene searches at runtime). The rain asks it two things: "is the listener under cover?" (<see cref="ListenerCover"/>, which the
    /// rain sound reads and anything else may) and "is this raindrop inside a roofed volume?" (<see cref="KillRain"/>, which removes it).
    /// It changes no weather and nothing else; with no shelters, or no rain, every method returns at once.
    /// </summary>
    public class RainShelter : MonoBehaviour
    {
        [Tooltip("Shown in the F3 overlay, e.g. 'Rock Shelter'.")]
        public string placeName = "Shelter";
        [Tooltip("The covered box in this object's local space: centre and full size. Roof-to-floor, as the geometry has it.")]
        public Vector3 centre = new Vector3(0f, 1f, 0f);
        public Vector3 size = new Vector3(2f, 2f, 2f);
        [Tooltip("Metres outside the box over which cover fades from 1 to 0 (stepping out is gradual).")]
        public float feather = 0.6f;

        private Matrix4x4 _worldToLocal;   // cached when enabled: shelters are static scenery
        private Vector3 _worldCentre;
        private float _boundRadius;

        // ------------------------------------------------------------------ registry

        private static readonly List<RainShelter> All = new List<RainShelter>();
        private static readonly List<RainShelter> Near = new List<RainShelter>(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear(); Near.Clear();
            ListenerCover = 0f; ListenerPlace = null; _target = 0f; _timer = 0f; _targetPlace = null;
        }

        private void OnEnable()
        {
            Refresh();
            if (!All.Contains(this)) All.Add(this); // never twice, however often the scene is rebuilt or reloaded
        }

        private void OnDisable() { All.Remove(this); }

        /// <summary>Re-reads the transform (call if a shelter is ever moved at runtime; builders never do).</summary>
        public void Refresh()
        {
            _worldToLocal = transform.worldToLocalMatrix;
            _worldCentre = transform.TransformPoint(centre);
            _boundRadius = (Vector3.Scale(size, transform.lossyScale) * 0.5f).magnitude + feather;
        }

        // ------------------------------------------------------------------ queries

        /// <summary>This box's cover at a world position: 1 inside, falling to 0 over <see cref="feather"/> metres outside its sides.</summary>
        public float CoverAt(Vector3 world)
        {
            Vector3 p = _worldToLocal.MultiplyPoint3x4(world) - centre;
            Vector3 h = size * 0.5f;
            if (p.y > h.y || p.y < -h.y) return 0f; // above the roof or below the floor: no cover (nobody is there)
            float dx = Mathf.Abs(p.x) - h.x, dz = Mathf.Abs(p.z) - h.z;
            float d = Mathf.Max(0f, Mathf.Max(dx, dz));
            if (d <= 0f) return 1f;
            return feather <= 0.001f ? 0f : Mathf.Clamp01(1f - d / feather);
        }

        /// <summary>True when the world point is inside the box proper (no feather): where a raindrop would be falling through the roof.</summary>
        public bool Contains(Vector3 world)
        {
            Vector3 p = _worldToLocal.MultiplyPoint3x4(world) - centre;
            Vector3 h = size * 0.5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        /// <summary>0..1: how much of the open sky a world position is hidden from by a roofed place (1 under cover).</summary>
        public static float Cover(Vector3 worldPos) => Cover(worldPos, out _);

        public static float Cover(Vector3 worldPos, out string place)
        {
            place = null;
            float best = 0f;
            for (int i = 0; i < All.Count; i++)
            {
                var s = All[i];
                if (s == null) continue;
                float c = s.CoverAt(worldPos);
                if (c > best) { best = c; place = s.placeName; }
            }
            return best;
        }

        public static int Count => All.Count;

        // ------------------------------------------------------------------ the listener (the player's camera)

        /// <summary>The camera's cover, eased over about half a second (0 in the open, 1 fully under a roof). Read by the rain sound; free for others to read.</summary>
        public static float ListenerCover { get; private set; }
        /// <summary>The place the listener is under, or null (for the F3 overlay).</summary>
        public static string ListenerPlace { get; private set; }
        public static bool Covered => ListenerCover > 0.02f;

        private static float _target, _timer;
        private static string _targetPlace;

        /// <summary>Called once a frame by <see cref="WeatherCycle"/>; samples the real cover five times a second and eases towards it.</summary>
        public static void Tick(Vector3 listener, float dt)
        {
            if (All.Count == 0)
            {
                if (ListenerCover != 0f) { ListenerCover = 0f; ListenerPlace = null; }
                return;
            }
            _timer -= dt;
            if (_timer <= 0f) { _timer = 0.2f; _target = Cover(listener, out _targetPlace); }
            float k = 1f - Mathf.Exp(-dt * 6f); // ~0.5 s to settle
            ListenerCover = Mathf.Abs(_target - ListenerCover) < 0.002f ? _target : Mathf.Lerp(ListenerCover, _target, k);
            ListenerPlace = ListenerCover > 0.02f ? _targetPlace : null;
        }

        // ------------------------------------------------------------------ the rain

        /// <summary>
        /// Removes raindrops that have fallen into a roofed volume: the rain stops at the roof and still falls outside the opening.
        /// Costs nothing unless a shelter is within <paramref name="radius"/> of <paramref name="rainCentre"/> (then one copy of the live
        /// particles through the caller's preallocated buffer and a box test per drop per nearby shelter; no allocation).
        /// </summary>
        public static void KillRain(ParticleSystem rain, Vector3 rainCentre, float radius, ParticleSystem.Particle[] buffer)
        {
            if (rain == null || buffer == null || All.Count == 0) return;
            Near.Clear();
            for (int i = 0; i < All.Count; i++)
            {
                var s = All[i];
                if (s == null) continue;
                if ((s._worldCentre - rainCentre).sqrMagnitude <= (radius + s._boundRadius) * (radius + s._boundRadius)) Near.Add(s);
            }
            if (Near.Count == 0) return;
            int n = rain.GetParticles(buffer);
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = buffer[i].position;
                for (int j = 0; j < Near.Count; j++)
                {
                    if (!Near[j].Contains(pos)) continue;
                    buffer[i].remainingLifetime = 0f;
                    any = true;
                    break;
                }
            }
            if (any) rain.SetParticles(buffer, n);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireCube(centre, size);
        }
    }
}
