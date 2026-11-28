using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>
    /// Global wind: a prevailing direction with slow gusts that travel across the landscape. Drives every
    /// <see cref="FoliageSway"/> near the camera in one pass (cheaper than per-object Updates) and mirrors its
    /// strength onto a Unity WindZone (for SpeedTree / Tree Creator prefabs). Terrain grass waves with the
    /// TerrainData waving settings the builder configures (not changed at runtime: TerrainData is an asset, and
    /// edits made in Play mode would persist).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class WildLensWind : MonoBehaviour
    {
        [SerializeField] private Vector3 direction = new Vector3(1f, 0f, 0.35f);
        [SerializeField, Range(0f, 2f)] private float strength = 1f;
        [Tooltip("How much gusts add on top of the base breeze (0-1).")]
        [SerializeField, Range(0f, 1f)] private float gustiness = 0.6f;
        [Tooltip("Gust front speed across the landscape (m/s).")]
        [SerializeField] private float gustSpeed = 6f;
        [Tooltip("Foliage further than this from the camera is not animated.")]
        [SerializeField] private float activeDistance = 90f;

        private static readonly List<FoliageSway> Registered = new List<FoliageSway>();
        private WindZone _zone;

        public static WildLensWind Instance { get; private set; }
        public Vector3 Direction => direction.sqrMagnitude > 0f ? direction.normalized : Vector3.right;
        public float Strength => strength * WeatherMultiplier;
        /// <summary>Set by <see cref="WeatherCycle"/> (1.4): stronger wind under cloud and in rain.</summary>
        public float WeatherMultiplier { get; set; } = 1f;

        internal static void Register(FoliageSway s) => Registered.Add(s);
        internal static void Unregister(FoliageSway s) => Registered.Remove(s);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Registered.Clear();

        private void Awake()
        {
            Instance = this;
            _zone = GetComponent<WindZone>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Gust factor 0..1 at a world position and time: low-frequency noise advected along the wind.</summary>
        public float Gust(Vector3 p, float t)
        {
            Vector3 d = Direction;
            float along = Vector3.Dot(p, d) - t * gustSpeed;
            float across = Vector3.Dot(p, Vector3.Cross(Vector3.up, d));
            return Mathf.PerlinNoise(along * 0.025f, across * 0.02f + t * 0.03f);
        }

        private void LateUpdate()
        {
            float t = Time.time;
            var cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float maxSqr = activeDistance * activeDistance;
            Vector3 dir = Direction;

            float gustHere = Gust(camPos, t);
            if (_zone != null)
            {
                _zone.windMain = Strength * (0.4f + 0.6f * gustHere);
                _zone.windTurbulence = 0.3f * Strength;
            }

            for (int i = 0; i < Registered.Count; i++)
            {
                var s = Registered[i];
                Vector3 p = s.transform.position;
                if ((p - camPos).sqrMagnitude > maxSqr) continue;
                float gust = (1f - gustiness) + gustiness * Gust(p, t);
                s.Apply(t, gust * Strength, dir);
            }
        }
    }
}
