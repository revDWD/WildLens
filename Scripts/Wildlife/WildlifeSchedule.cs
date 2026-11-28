using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.Wildlife
{
    /// <summary>
    /// Daily rhythms by placement, not by new AI (0.9). When the <see cref="DayCycle"/> period changes, each animal's
    /// home range moves to the kind of place its species uses at that time, chosen near its own territory, and it
    /// walks (or flies) there by itself. Behaviour weights shift too (deer bed down at midday).
    ///
    ///   Deer       dawn & golden hour: meadows · morning: meadows · midday: woodland cover (resting) · dusk: water
    ///   Cottontail dawn & dusk: out on the meadow edge · otherwise: brushy cover (sitting more at midday)
    ///   Fox        dawn & dusk: hunting meadow edges · midday: woodland (resting) · otherwise: home
    ///   Hawk       morning → golden hour: soaring over the ridge, using its perch · dawn & dusk: snags by meadows
    ///   Squirrel   stays in its trees; busiest in the morning and late afternoon, quiet at midday
    ///
    /// 1.9, natural events: the weather moves them too, by data (<see cref="WeatherHabits"/> on each species, decided by
    /// <see cref="WeatherHabitRules"/>): songbirds sit tight in brush while it rains and bathe at the water's edge after it, deer
    /// shelter and then come out to feed in the first sun, the hawk soars only in fair weather, the owl calls it a night in the rain,
    /// crows gather at the trailhead at dusk. Nothing is triggered by the player and nothing is spawned: the same hour and the same
    /// forecast always give the same pattern. Weather-triggered moves are staggered by a stable per-group delay so a flock
    /// does not set off on the same frame; a change of day period still moves everyone at once, as before.
    ///
    /// Players learn these patterns the way naturalists do; the trailhead board and signs hint at them.
    /// </summary>
    public class WildlifeSchedule : MonoBehaviour
    {
        [Tooltip("Animals only move to places within this distance of their own territory.")]
        public float maxShift = 320f;

        [Tooltip("Longest delay (seconds) before a group answers a change of weather; each group and animal has its own stable share of it.")]
        public float weatherStagger = 14f;

        public static WildlifeSchedule Instance { get; private set; }

        private sealed class AnimalState
        {
            public int appliedKey = int.MinValue;
            public int appliedPeriod = -1;
            public int pendingKey = int.MinValue;
            public float due;
        }

        private WildlifeSpawner _spawner;
        private readonly Dictionary<AnimalAgent, AnimalState> _states = new Dictionary<AnimalAgent, AnimalState>();
        private float _nextPoll;

        /// <summary>Raining hard enough to send animals to shelter (1.4).</summary>
        public static bool Raining => WeatherCycle.Instance != null && WeatherCycle.Instance.IsRaining;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Behaviour weighting for a species at a time of day (multiplies the species' own weights). Kept for callers from before 1.9.</summary>
        public static float Bias(string species, DayPeriod p, AnimalActivity a) =>
            WeatherHabitRules.Bias(species, new HabitDecision { period = (int)p }, a);

        private static HabitWeather Snapshot(DayCycle clock)
        {
            var w = new HabitWeather { fair = true, hoursSinceRain = 1e9f, hour = clock.Hour };
            var weather = WeatherCycle.Instance;
            if (weather == null) return w;
            w.raining = weather.IsRaining;
            w.fair = weather.IsFair;
            w.overcast = weather.OvercastSky;
            w.sunAfterRain = weather.SunAfterRain;
            w.hoursSinceRain = weather.HoursSinceRain;
            return w;
        }

        private void Update()
        {
            var clock = DayCycle.Instance;
            if (clock == null) return;
            if (_spawner == null) _spawner = SceneQuery.FindOne<WildlifeSpawner>();
            if (_spawner == null || _spawner.Spawned.Count == 0) return;
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + 0.25f;

            var weather = Snapshot(clock);
            int period = (int)clock.Period;
            foreach (var a in _spawner.Spawned)
            {
                if (a == null || a.Species == null) continue;
                if (!_states.TryGetValue(a, out var st)) _states[a] = st = new AnimalState();
                var species = a.Species;
                var d = WeatherHabitRules.Decide(species.speciesId, species.rhythm, species.weatherHabits, period, weather);
                int key = d.Key;
                if (key == st.appliedKey) { st.pendingKey = int.MinValue; continue; }

                // The first placement and a change of day period are immediate (as before 1.9). A change of weather alone is
                // answered after a stable delay that differs per group and, a little, per animal.
                bool immediate = st.appliedKey == int.MinValue || st.appliedPeriod != period;
                if (!immediate)
                {
                    if (st.pendingKey != key) { st.pendingKey = key; st.due = Time.time + Delay(a); }
                    if (Time.time < st.due) continue;
                }
                st.appliedKey = key;
                st.appliedPeriod = period;
                st.pendingKey = int.MinValue;
                Apply(a, d);
            }
        }

        /// <summary>A stable delay in seconds: the same for a group (its territory and species), a little different for each animal.</summary>
        private float Delay(AnimalAgent a)
        {
            Vector3 o = a.OriginHome;
            string id = a.Species.speciesId;
            float group = WeatherHabitRules.Hash01(id, Mathf.FloorToInt(o.x / 12f), Mathf.FloorToInt(o.z / 12f));
            float self = WeatherHabitRules.Hash01(id, Mathf.RoundToInt(o.x * 10f), Mathf.RoundToInt(o.z * 10f) + 7);
            return Mathf.Max(0f, weatherStagger) * group + self * 3f;
        }

        private void Apply(AnimalAgent a, HabitDecision d)
        {
            string id = a.Species.speciesId;
            a.ActivityBias = act => WeatherHabitRules.Bias(id, d, act);
            Vector3 origin = a.OriginHome;
            Vector3 target = origin;
            switch (d.place)
            {
                case RhythmPlace.Meadow: target = Nearest(origin, HotspotType.Meadow, null, false) ?? origin; break;
                case RhythmPlace.MeadowEdge: target = Nearest(origin, HotspotType.Meadow, null, true) ?? origin; break;
                case RhythmPlace.Brush: target = Nearest(origin, HotspotType.BrushPile, null, false) ?? origin; break;
                case RhythmPlace.Perch: target = Nearest(origin, HotspotType.Perch, null, false) ?? origin; break;
                case RhythmPlace.Woodland: target = Nearest(origin, HotspotType.None, ScheduleRole.Woodland, false) ?? origin; break;
                case RhythmPlace.Water: target = Nearest(origin, HotspotType.None, ScheduleRole.Water, false) ?? origin; break;
                case RhythmPlace.Ridge: target = Nearest(origin, HotspotType.None, ScheduleRole.Ridge, false, 450f) ?? origin; break;
                case RhythmPlace.Roost: target = Roost(a.Species, origin); break;
            }
            // Residents of a place mostly stay put; others commute. A small stable jitter keeps a group from stacking up.
            float jx = (WeatherHabitRules.Hash01(id, Mathf.RoundToInt(origin.x * 10f), Mathf.RoundToInt(origin.z * 10f) + 1) * 2f - 1f) * 3f;
            float jz = (WeatherHabitRules.Hash01(id, Mathf.RoundToInt(origin.x * 10f), Mathf.RoundToInt(origin.z * 10f) + 2) * 2f - 1f) * 3f;
            a.SetHome(TerrainUtil.Ground(target + new Vector3(jx, 0f, jz)));
        }

        /// <summary>The place a species gathers at: one of its own residences (the one labelled in its habits, else the first); its own territory if it has none.</summary>
        private static Vector3 Roost(SpeciesDefinition species, Vector3 fallback)
        {
            string label = species.weatherHabits != null ? species.weatherHabits.roostLabel : "";
            Vector3? first = null;
            foreach (var res in WildlifeResidence.All)
            {
                if (res == null || res.species == null || System.Array.IndexOf(res.species, species.speciesId) < 0) continue;
                if (!first.HasValue) first = res.transform.position;
                if (!string.IsNullOrEmpty(label) && string.Equals(res.label, label, System.StringComparison.OrdinalIgnoreCase)) return res.transform.position;
            }
            return first ?? fallback;
        }

        private Vector3? Nearest(Vector3 origin, HotspotType type, ScheduleRole? role, bool edge, float limit = -1f)
        {
            if (limit < 0f) limit = maxShift;
            Vector3? best = null;
            float bestD = limit;
            if (role.HasValue)
            {
                foreach (var anchor in ScheduleAnchor.All)
                {
                    if (anchor.role != role.Value) continue;
                    float d = TerrainUtil.FlatDistance(origin, anchor.transform.position);
                    if (d < bestD) { bestD = d; best = anchor.transform.position; }
                }
                return best;
            }
            var world = WildlifeWorld.Instance;
            if (world == null) return null;
            foreach (var h in world.Hotspots)
            {
                if (h.type != type) continue;
                float d = TerrainUtil.FlatDistance(origin, h.position);
                if (d >= bestD) continue;
                bestD = d;
                // The edge of a meadow nearest the animal's cover: out in the open, a dash from safety.
                best = edge ? h.position + TerrainUtil.Flat(origin - h.position).normalized * h.radius * 0.8f : h.position;
            }
            return best;
        }
    }
}
