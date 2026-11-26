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
    /// Players learn these patterns the way naturalists do; the trailhead board and signs hint at them.
    /// </summary>
    public class WildlifeSchedule : MonoBehaviour
    {
        [Tooltip("Animals only move to places within this distance of their own territory.")]
        public float maxShift = 320f;

        public static WildlifeSchedule Instance { get; private set; }

        private WildlifeSpawner _spawner;
        private DayPeriod _applied = (DayPeriod)(-1);

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            var clock = DayCycle.Instance;
            if (clock == null) return;
            if (_spawner == null) _spawner = SceneQuery.FindOne<WildlifeSpawner>();
            if (_spawner == null || _spawner.Spawned.Count == 0) return;
            if (clock.Period == _applied) return;
            _applied = clock.Period;
            foreach (var a in _spawner.Spawned) if (a != null) Apply(a, _applied);
        }

        private enum Place { Home, Meadow, MeadowEdge, Brush, Woodland, Water, Ridge, Perch } // same order as RhythmPlace

        private static Place Where(string species, DayPeriod p)
        {
            switch (species)
            {
                case "white_tailed_deer":
                    return p == DayPeriod.Dusk ? Place.Water : p == DayPeriod.Midday ? Place.Woodland : p == DayPeriod.Night ? Place.Woodland : Place.Meadow;
                case "eastern_cottontail":
                    return p == DayPeriod.Dawn || p == DayPeriod.Dusk ? Place.MeadowEdge : Place.Brush;
                case "red_fox":
                    return p == DayPeriod.Dawn || p == DayPeriod.Dusk ? Place.MeadowEdge : p == DayPeriod.Midday ? Place.Woodland : Place.Home;
                case "red_tailed_hawk":
                    return p == DayPeriod.Morning || p == DayPeriod.Midday || p == DayPeriod.GoldenHour ? Place.Ridge : Place.Perch;
                default:
                    return Place.Home;
            }
        }

        /// <summary>Behaviour weighting for a species at a time of day (multiplies the species' own weights).</summary>
        public static float Bias(string species, DayPeriod p, AnimalActivity a)
        {
            bool quiet = p == DayPeriod.Midday || p == DayPeriod.Night;
            bool active = p == DayPeriod.Dawn || p == DayPeriod.Dusk || p == DayPeriod.GoldenHour;
            switch (a)
            {
                case AnimalActivity.Resting: return quiet ? 4f : active ? 0.4f : 1f;
                case AnimalActivity.Sitting: return quiet && species == "eastern_cottontail" ? 2.5f : 1f;
                case AnimalActivity.Grazing:
                case AnimalActivity.Foraging: return active ? 1.6f : quiet ? 0.6f : 1f;
                case AnimalActivity.Perched: return species == "red_tailed_hawk" && !quiet ? 1.8f : 1f;
                case AnimalActivity.Climbing: return species == "eastern_gray_squirrel" && !quiet ? 1.5f : quiet ? 0.5f : 1f;
                default: return 1f;
            }
        }

        private void Apply(AnimalAgent a, DayPeriod p)
        {
            if (a.Species == null) return;
            string id = a.Species.speciesId;
            a.ActivityBias = act => Bias(id, p, act);
            Vector3 origin = a.OriginHome;
            Vector3 target = origin;
            // Data first (1.0): species with a DailyRhythm need no code here.
            var rhythm = a.Species.rhythm;
            Place place = rhythm != null && rhythm.enabled ? (Place)(int)rhythm.For((int)p) : Where(id, p);
            switch (place)
            {
                case Place.Meadow: target = Nearest(origin, HotspotType.Meadow, null, false) ?? origin; break;
                case Place.MeadowEdge: target = Nearest(origin, HotspotType.Meadow, null, true) ?? origin; break;
                case Place.Brush: target = Nearest(origin, HotspotType.BrushPile, null, false) ?? origin; break;
                case Place.Perch: target = Nearest(origin, HotspotType.Perch, null, false) ?? origin; break;
                case Place.Woodland: target = Nearest(origin, HotspotType.None, ScheduleRole.Woodland, false) ?? origin; break;
                case Place.Water: target = Nearest(origin, HotspotType.None, ScheduleRole.Water, false) ?? origin; break;
                case Place.Ridge: target = Nearest(origin, HotspotType.None, ScheduleRole.Ridge, false, 450f) ?? origin; break;
            }
            // Residents of a place mostly stay put; others commute. A small jitter keeps a group from stacking up.
            a.SetHome(TerrainUtil.Ground(target + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f))));
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
