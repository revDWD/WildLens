using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// The reserve beyond the Meadow Loop (0.7). Four destinations, each chosen from the terrain for a reason and each
    /// with a purpose, a structure, and resident wildlife:
    ///   Ridge Overlook   highest prominent ground: a deck with the widest view in the reserve; hawks
    ///   Cattail Pond     the lowest flat basin: open water, reeds, a jetty; deer come to drink
    ///   Old-Growth Grove the densest woodland: giant trees around a quiet clearing; squirrels and a fox
    ///   Meadow Blind     the most open flat ground: a wildflower meadow and a wooden hide; deer, rabbits
    /// A reserve track (footpath and bike track) runs from the loop to each, routed around steep ground, with a
    /// fingerpost where it leaves the loop. Scenery is extended along the tracks and around the destinations.
    ///
    /// Two passes: <see cref="PlanReserve"/> before scenery (choose sites, shape terrain, route tracks, set forest
    /// and meadow biases, extend the scatter area) and <see cref="BuildDestinations"/> after it (structures, clearing,
    /// hotspots, signs).
    /// </summary>
    public partial class VegetationBuilder
    {
        private class Site
        {
            public DestinationKind kind;
            public string title, note;
            public string[] residents;
            public Vector3 centre;     // pond centre, ridge top, grove centre, meadow centre
            public float radius;       // influence radius for forest bias and clearing
            public float water;        // pond radius
            public float waterLevel;
            public Vector3 arrival;    // where the track ends
            public Vector3 facing;     // for structures: view or approach direction
            public Vector3 structure;  // where the main structure stands (blind)
            public ReserveTrack track;
            public int number;
        }

        private readonly List<Site> _sites = new List<Site>();
        private readonly List<ReserveTrack> _tracks = new List<ReserveTrack>();
        private readonly List<(Vector3 p, float r)> _keepOut = new List<(Vector3, float)>();
        private float _populationScale = 1f;

        public int DestinationsPlaced => _sites.Count;
        public float TrackLength { get { float l = 0f; foreach (var t in _tracks) l += t.Length; return l; } }

        /// <summary>Distance to the trail loop or the nearest reserve track.</summary>
        private float PathDistance(Vector3 p)
        {
            // Widths differ (0.9 hierarchy): measure to each path's edge, offset so the loop-width rules hold for all.
            float half = _s.trailWidth * 0.5f;
            float d = _trail.DistanceToTrail(p);
            foreach (var t in _tracks) d = Mathf.Min(d, t.DistanceTo(p) - (t.Width * 0.5f - half));
            return Mathf.Min(d, PadDistance(p)); // pull-offs paint and clear like path
        }

        /// <summary>Nothing is scattered inside these circles (kiosk, bike parking, destination structures).</summary>
        public void AddKeepOut(Vector3 p, float radius) => _keepOut.Add((p, radius));

        private bool InKeepOut(Vector3 p)
        {
            foreach (var k in _keepOut) if (TerrainUtil.FlatDistance(p, k.p) < k.r) return true;
            return false;
        }

        /// <summary>Forest density offset near destinations: dense in the grove, open at the meadow, pond and ridge top.</summary>
        private float ReserveForestBias(Vector3 p)
        {
            float bias = 0f;
            foreach (var s in _sites)
            {
                float d = TerrainUtil.FlatDistance(p, s.centre);
                float near = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.radius * 0.6f, s.radius * 1.25f, d));
                switch (s.kind)
                {
                    case DestinationKind.OldGrowth: bias += near * 0.45f; break;
                    case DestinationKind.ObservationBlind: bias -= near * 0.8f; break;
                    case DestinationKind.Wetland: bias -= near * 0.8f; break;
                    case DestinationKind.RidgeOverlook: bias -= near * 0.6f; break;
                }
            }
            // Riparian woodland: denser trees along the creek (the channel itself is kept clear by IsFree).
            float creek = CreekDistance(p);
            if (creek < 32f) bias += 0.3f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 32f, creek)));
            return bias;
        }

        // ================================================================== plan

        public void PlanReserve(Transform parent, bool shapeTerrain, bool trackMeshes)
        {
            ChooseSites();
            if (_sites.Count == 0)
            {
                Debug.LogWarning("[WildLens] No room for reserve destinations on this terrain (it may be too small or too steep). " +
                                 "Only the Meadow Loop was built.");
                return;
            }
            if (shapeTerrain) ShapeTerrain();
            foreach (var s in _sites) PlaceArrival(s);
            PlanCreek(parent, shapeTerrain);   // 0.8: the reserve's backbone, before tracks so they cross it cleanly
            if (shapeTerrain) FlattenBlindPad();
            RouteTracks(parent, trackMeshes);

            // Number destinations nearest-first along their tracks.
            _sites.Sort((a, b) => a.track.Length.CompareTo(b.track.Length));
            for (int i = 0; i < _sites.Count; i++) _sites[i].number = i + 1;
            PlanParking(shapeTerrain);
            PlanRoads(parent, trackMeshes, shapeTerrain); // the Reserve Road ring through every pull-off (0.9; graded 1.0)

            // Meadows at the blind and around the pond get meadow grass, flowers and the meadow ground tint.
            foreach (var s in _sites)
            {
                if (s.kind == DestinationKind.ObservationBlind) _meadows.Add(new Meadow { center = s.centre, radius = s.radius });
                if (s.kind == DestinationKind.Wetland) _meadows.Add(new Meadow { center = s.centre, radius = s.water + 10f });
            }

            // Scatter scenery over the whole reserve, keeping the original density.
            float oldArea = (_max.x - _min.x) * (_max.y - _min.y);
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            foreach (var s in _sites)
            {
                _min = Vector2.Min(_min, new Vector2(s.centre.x - s.radius - 60f, s.centre.z - s.radius - 60f));
                _max = Vector2.Max(_max, new Vector2(s.centre.x + s.radius + 60f, s.centre.z + s.radius + 60f));
            }
            _min = Vector2.Max(_min, new Vector2(tPos.x + 2f, tPos.z + 2f));
            _max = Vector2.Min(_max, new Vector2(tPos.x + size.x - 2f, tPos.z + size.z - 2f));
            float newArea = (_max.x - _min.x) * (_max.y - _min.y);
            // Most of the new area is far from any path; it gets scenery at a lower density (the budget grows
            // with the reserve, but not in proportion, to protect frame rate).
            _populationScale = Mathf.Clamp(1f + (newArea / Mathf.Max(1f, oldArea) - 1f) * 0.5f, 1f, 2.2f);
        }

        private int Scaled(int count) => Mathf.RoundToInt(count * _populationScale);

        // ------------------------------------------------------------------ site selection

        private float BaseForest(Vector3 p) =>
            Mathf.PerlinNoise(_noiseOffset + p.x * 0.014f, _noiseOffset + p.z * 0.014f) * 0.75f +
            Mathf.PerlinNoise(_noiseOffset + p.x * 0.05f, _noiseOffset - p.z * 0.05f) * 0.25f;

        private static float RingMean(Vector3 c, float r, int n = 12)
        {
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                sum += TerrainUtil.SampleHeight(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
            }
            return sum / n;
        }

        private static float RingMin(Vector3 c, float r, int n = 16)
        {
            float m = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                m = Mathf.Min(m, TerrainUtil.SampleHeight(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r));
            }
            return m;
        }

        private static float MaxSlope(Vector3 c, float r)
        {
            float m = TerrainUtil.SlopeDegrees(c);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                m = Mathf.Max(m, TerrainUtil.SlopeDegrees(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r));
            }
            return m;
        }

        private void ChooseSites()
        {
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            const float inset = 45f, step = 10f;
            var candidates = new List<Vector3>();
            for (float x = tPos.x + inset; x <= tPos.x + size.x - inset; x += step)
            for (float z = tPos.z + inset; z <= tPos.z + size.z - inset; z += step)
            {
                Vector3 p = TerrainUtil.Ground(new Vector3(x, 0f, z));
                float reach = TerrainUtil.FlatDistance(p, _loop.center);
                if (LoopRadius(p) < 1.5f || reach > _s.reserveReach) continue;
                if (_trail.DistanceToTrail(p) < 50f) continue;
                candidates.Add(p);
            }
            if (candidates.Count == 0) return;

            Pick(candidates, DestinationKind.RidgeOverlook, 40f, p =>
            {
                if (MaxSlope(p, 6f) > 18f) return float.NaN;
                return TerrainUtil.SampleHeight(p) - RingMean(p, 45f) + TerrainUtil.SampleHeight(p) * 0.05f;
            });
            Pick(candidates, DestinationKind.Wetland, 26f, p =>
            {
                if (MaxSlope(p, 14f) > 9f) return float.NaN;
                return (RingMean(p, 26f) - TerrainUtil.SampleHeight(p)) * 2f - TerrainUtil.SampleHeight(p) * 0.02f;
            });
            Pick(candidates, DestinationKind.OldGrowth, 34f, p => MaxSlope(p, 10f) > 24f ? float.NaN : BaseForest(p));
            Pick(candidates, DestinationKind.ObservationBlind, 30f, p => MaxSlope(p, 15f) > 9f ? float.NaN : 1f - BaseForest(p));
        }

        private void Pick(List<Vector3> candidates, DestinationKind kind, float radius, System.Func<Vector3, float> score)
        {
            var raw = new List<(Vector3 p, float s)>();
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in candidates)
            {
                bool clear = true;
                foreach (var s in _sites)
                    if (TerrainUtil.FlatDistance(p, s.centre) < s.radius + radius + 50f) { clear = false; break; }
                if (!clear) continue;
                float v = score(p);
                if (float.IsNaN(v)) continue;
                raw.Add((p, v));
                lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v);
            }
            if (raw.Count == 0) return;

            float best = float.MinValue;
            Vector3 chosen = raw[0].p;
            foreach (var (p, v) in raw)
            {
                float s = (v - lo) / Mathf.Max(0.0001f, hi - lo);
                s += 0.2f * TerrainUtil.FlatDistance(p, _loop.center) / _s.reserveReach; // farther is more of a trip
                // Spread destinations around the compass so every direction has somewhere to go.
                float bearing = Bearing(p);
                foreach (var other in _sites)
                {
                    float gap = Mathf.Abs(Mathf.DeltaAngle(bearing, Bearing(other.centre)));
                    if (gap < 60f) s -= (60f - gap) / 60f;
                }
                if (s > best) { best = s; chosen = p; }
            }

            var site = new Site { kind = kind, centre = chosen, radius = radius };
            switch (kind)
            {
                case DestinationKind.RidgeOverlook:
                    site.title = "Ridge Overlook";
                    site.note = "The highest ground in the reserve. Red-tailed hawks hunt the slopes below.";
                    site.residents = new[] { "red_tailed_hawk", "red_tailed_hawk", "coyote" };
                    break;
                case DestinationKind.Wetland:
                    site.title = "Cattail Pond";
                    site.note = "Still water in the low ground. A heron fishes the shallows at dawn; deer drink at dusk.";
                    site.residents = new[] { "white_tailed_deer", "great_blue_heron", "red_winged_blackbird", "raccoon" };
                    site.water = 14f;
                    break;
                case DestinationKind.OldGrowth:
                    site.title = "Old-Growth Grove";
                    site.note = "The oldest trees in the reserve. A barred owl roosts here by day; a fox dens among the roots.";
                    site.residents = new[] { "eastern_gray_squirrel", "red_fox", "barred_owl", "blue_jay" };
                    break;
                case DestinationKind.ObservationBlind:
                    site.title = "Wildflower Meadow Blind";
                    site.note = "A wooden hide on the meadow's edge. Step inside and you all but disappear.";
                    site.residents = new[] { "white_tailed_deer", "eastern_cottontail", "northern_cardinal", "wild_turkey" };
                    break;
            }
            _sites.Add(site);
        }

        private float Bearing(Vector3 p)
        {
            Vector3 d = p - _loop.center;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ terrain shaping

        /// <summary>
        /// A real basin for the pond and a real hill for the overlook when the ground doesn't already provide one.
        /// Targets are computed from rings outside the shaped area, so rebuilding gives the same result rather than
        /// digging deeper each time.
        /// </summary>
        private void ShapeTerrain()
        {
            var data = _terrain.terrainData;
            if (_s.backupTerrainData && !IsOwnTerrain()) BackupTerrainData(data);
            Undo.RegisterCompleteObjectUndo(data, "Shape WildLens reserve terrain");

            foreach (var s in _sites)
            {
                if (s.kind == DestinationKind.Wetland)
                {
                    float R = s.water;
                    s.waterLevel = RingMin(s.centre, R * 1.35f + 16f) - 0.35f;
                    // A terrain at its lowest representable height can't be dug: raise the water and build the banks up instead.
                    float floor = _terrain.transform.position.y + 0.05f;
                    if (s.waterLevel - 1.6f < floor) s.waterLevel = floor + 1.6f;
                    float level = s.waterLevel;
                    var site = s;
                    // 0.8: an irregular shoreline (coves and a point) instead of a perfect circle.
                    ShapeAt(s.centre, R * 1.35f + 9f, (p, d, h) =>
                    {
                        float r = PondRadius(site, p);
                        if (d < r) return Mathf.Min(h, level - 0.15f - 1.3f * (1f - (d / r) * (d / r)));
                        // Banks: a gentle shore rising from just above the water, blended into the land.
                        float bank = level + 0.12f + (d - r) * 0.09f;
                        float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r + 3f, r + 9f, d));
                        return Mathf.Lerp(bank, h, w);
                    });
                }
                else if (s.kind == DestinationKind.RidgeOverlook)
                {
                    float top = TerrainUtil.SampleHeight(s.centre), around = RingMean(s.centre, 60f);
                    if (top - around >= 5f) continue; // already a proper rise
                    float R = 48f, height = 7f, baseH = around;
                    Shape(s.centre, R, (d, h) =>
                    {
                        float k = Mathf.Cos(Mathf.Clamp01(d / R) * Mathf.PI) * 0.5f + 0.5f; // 1 at the top → 0 at the edge
                        return Mathf.Max(h, baseH + height * k * k * (3f - 2f * k));
                    });
                }
            }
        }

        private bool IsOwnTerrain()
        {
            string path = AssetDatabase.GetAssetPath(_terrain.terrainData);
            return _terrain.name.Contains("WildLens") || (!string.IsNullOrEmpty(path) && path.StartsWith(GeneratedAssets.Folder));
        }

        /// <summary>Pond radius in the direction of <paramref name="p"/>: coves and points from angular noise.</summary>
        private float PondRadius(Site s, Vector3 p)
        {
            float a = Mathf.Atan2(p.z - s.centre.z, p.x - s.centre.x);
            float c = Mathf.Cos(a), sn = Mathf.Sin(a), o = _noiseOffset * 0.37f;
            float k = (Mathf.PerlinNoise(o + c * 1.1f, o + sn * 1.1f) - 0.5f) * 0.6f + (Mathf.PerlinNoise(o + 9f + c * 2.6f, o + sn * 2.6f) - 0.5f) * 0.22f;
            return s.water * (1f + k);
        }

        private void Shape(Vector3 centre, float radius, System.Func<float, float, float> f) => ShapeAt(centre, radius, (p, d, h) => f(d, h));

        private void ShapeAt(Vector3 centre, float radius, System.Func<Vector3, float, float, float> f)
        {
            var data = _terrain.terrainData;
            Vector3 tPos = _terrain.transform.position, size = data.size;
            int res = data.heightmapResolution;
            float cell = size.x / (res - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((centre.x - radius - tPos.x) / cell), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((centre.z - radius - tPos.z) / (size.z / (res - 1))), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((centre.x + radius - tPos.x) / cell), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((centre.z + radius - tPos.z) / (size.z / (res - 1))), 0, res - 1);
            int w = x1 - x0 + 1, hgt = z1 - z0 + 1;
            if (w <= 1 || hgt <= 1) return;
            var heights = data.GetHeights(x0, z0, w, hgt);
            for (int z = 0; z < hgt; z++)
            for (int x = 0; x < w; x++)
            {
                Vector3 p = new Vector3(tPos.x + (x0 + x) * cell, 0f, tPos.z + (z0 + z) * (size.z / (res - 1)));
                float d = TerrainUtil.FlatDistance(p, centre);
                if (d > radius) continue;
                float h = tPos.y + heights[z, x] * size.y;
                heights[z, x] = Mathf.Clamp01((f(p, d, h) - tPos.y) / size.y);
            }
            data.SetHeights(x0, z0, heights);
        }

        // ------------------------------------------------------------------ tracks

        private void PlaceArrival(Site s)
        {
            Vector3 toLoop = TerrainUtil.Flat(_loop.center - s.centre).normalized;
            switch (s.kind)
            {
                case DestinationKind.Wetland:
                    s.facing = -toLoop; // the jetty points out over the water from the loop side
                    s.arrival = TerrainUtil.Ground(s.centre + toLoop * (s.water + 10f));
                    break;
                case DestinationKind.RidgeOverlook:
                    s.facing = BestView(s.centre);
                    s.arrival = TerrainUtil.Ground(s.centre - s.facing * 9f);
                    break;
                case DestinationKind.OldGrowth:
                    s.facing = -toLoop;
                    s.arrival = s.centre;
                    break;
                case DestinationKind.ObservationBlind:
                    // The hide sits inside the meadow's edge nearest the loop, facing in, close enough that animals in
                    // the meadow are near (0.8); the track arrives from behind it, out of the animals' sight.
                    s.facing = -toLoop;
                    s.structure = TerrainUtil.Ground(s.centre - s.facing * (s.radius * 0.5f));
                    s.arrival = TerrainUtil.Ground(s.structure - s.facing * 10f);
                    break;
            }
        }

        /// <summary>A level pad for the blind so its floor sits flat and its door at ground level.</summary>
        private void FlattenBlindPad()
        {
            foreach (var s in _sites)
            {
                if (s.kind != DestinationKind.ObservationBlind) continue;
                float hc = TerrainUtil.SampleHeight(s.structure);
                Shape(s.structure, 6.5f, (d, h) => Mathf.Lerp(hc, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.4f, 6.5f, d))));
                s.structure = TerrainUtil.Ground(s.structure);
                // Keep trees, rocks and terrain grass out of the hide and off its duckboard path.
                AddKeepOut(s.structure, 3.4f);
                for (float d = 0f; d <= 1f; d += 0.1f) AddKeepOut(Vector3.Lerp(s.arrival, s.structure, d), 1.1f);
            }
        }

        /// <summary>Direction from <paramref name="p"/> with the lowest, most open ground ahead: the widest view.</summary>
        private Vector3 BestView(Vector3 p)
        {
            float h0 = TerrainUtil.SampleHeight(p), best = float.MaxValue;
            Vector3 dir = TerrainUtil.Flat(p - _loop.center).normalized;
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float sum = 0f;
                for (float r = 60f; r <= 180f; r += 30f) sum += TerrainUtil.SampleHeight(p + d * r) - h0;
                if (TerrainUtil.FindTerrainAt(p + d * 180f) == null) sum += 1000f; // looking off the edge of the world
                // 0.9: an overlook is a golden-hour destination, so favour views toward the setting sun (west).
                DayCycle.SunPosition(17.5f, out float sunAz, out _);
                float toSunset = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, sunAz));
                sum -= Mathf.Cos(toSunset * Mathf.Deg2Rad) * 25f;
                if (sum < best) { best = sum; dir = d; }
            }
            return dir;
        }

        private void RouteTracks(Transform parent, bool meshes)
        {
            var dirt = meshes ? GeneratedAssets.Material("TrackDirt", Color.white, 0.05f,
                GeneratedAssets.LoadOrCreate("T_TrailDirt", () => GeneratedAssets.NoiseTexture(128,
                    new Color(0.42f, 0.34f, 0.24f), new Color(0.55f, 0.46f, 0.33f), 6f, 3, 0.04f, new Color(0.7f, 0.66f, 0.58f)))) : null;
            foreach (var s in _sites)
            {
                _trail.DistanceToTrail(s.arrival, out float along);
                Vector3 start = _trail.GetPoint(along);
                var path = FindPath(start, s.arrival);
                if (path == null) path = new List<Vector3> { start, s.arrival };
                path = Resample(Chaikin(Chaikin(Chaikin(path))), 3f);
                for (int i = 0; i < path.Count; i++) path[i] = TerrainUtil.Ground(path[i]);

                var go = new GameObject("Track: " + s.title);
                go.transform.SetParent(parent, false);
                var track = go.AddComponent<ReserveTrack>();
                track.title = s.title;
                track.SetPath(path, 1.5f); // a footpath: walking first (0.9 hierarchy)
                track.kind = TrackKind.Footpath;
                _tracks.Add(track);
                s.track = track;

                if (meshes)
                {
                    var mesh = GeneratedAssets.Save(ProceduralMeshes.TrailRibbon(path, 1.5f, 0.6f, closed: false), "Mesh_Track_" + s.kind);
                    var surface = new GameObject("Track Surface");
                    surface.transform.SetParent(go.transform, false);
                    surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = surface.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = dirt;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        /// <summary>A* on a 5 m grid: prefers gentle ground, avoids steep slopes, water and other destinations.</summary>
        private List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            const float cell = 5f;
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            int w = Mathf.FloorToInt(size.x / cell), h = Mathf.FloorToInt(size.z / cell);
            Vector2Int Node(Vector3 p) => new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((p.x - tPos.x) / cell), 1, w - 2),
                                                         Mathf.Clamp(Mathf.RoundToInt((p.z - tPos.z) / cell), 1, h - 2));
            Vector3 World(Vector2Int n) => new Vector3(tPos.x + n.x * cell, 0f, tPos.z + n.y * cell);

            var slopeCache = new Dictionary<Vector2Int, float>();
            float Penalty(Vector2Int n)
            {
                if (slopeCache.TryGetValue(n, out float v)) return v;
                Vector3 p = World(n);
                float slope = TerrainUtil.SlopeDegrees(p);
                v = 1f + (slope / 12f) * (slope / 12f);
                if (slope > 26f) v += 40f;
                foreach (var s in _sites)
                {
                    if (s.water > 0f && TerrainUtil.FlatDistance(p, s.centre) < s.water + 4f) v += 200f;
                    if (s.kind == DestinationKind.ObservationBlind && TerrainUtil.FlatDistance(p, s.centre) < s.radius * 0.8f) v += 6f; // keep out of the meadow
                }
                if (LoopRadius(p) < 0.92f) v += 3f; // don't cut across the loop's inner meadow
                if (CreekDistance(p) < 6f) v += 12f; // cross the creek once, squarely (a footbridge goes there)
                slopeCache[n] = v;
                return v;
            }

            Vector2Int a = Node(from), b = Node(to);
            var open = new SortedSet<(float f, int id)>();
            var g = new Dictionary<Vector2Int, float> { [a] = 0f };
            var came = new Dictionary<Vector2Int, Vector2Int>();
            int Id(Vector2Int n) => n.y * w + n.x;
            Vector2Int FromId(int id) => new Vector2Int(id % w, id / w);
            open.Add((Vector2Int.Distance(a, b), Id(a)));
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
                               new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) };
            int guard = 0;
            while (open.Count > 0 && guard++ < 200000)
            {
                var top = open.Min;
                open.Remove(top);
                var cur = FromId(top.id);
                if (cur == b)
                {
                    var path = new List<Vector3> { to };
                    while (came.TryGetValue(cur, out var prev)) { path.Add(World(cur)); cur = prev; }
                    path.Add(from);
                    path.Reverse();
                    return path;
                }
                float gc = g[cur];
                foreach (var d in dirs)
                {
                    var n = cur + d;
                    if (n.x < 1 || n.y < 1 || n.x > w - 2 || n.y > h - 2) continue;
                    float step = (d.x != 0 && d.y != 0 ? 1.414f : 1f) * (Penalty(n) + Penalty(cur)) * 0.5f;
                    float ng = gc + step;
                    if (g.TryGetValue(n, out float old) && old <= ng) continue;
                    if (g.ContainsKey(n)) open.Remove((old + Vector2Int.Distance(n, b), Id(n)));
                    g[n] = ng;
                    came[n] = cur;
                    open.Add((ng + Vector2Int.Distance(n, b), Id(n)));
                }
            }
            return null;
        }

        private static List<Vector3> Chaikin(List<Vector3> p)
        {
            if (p.Count < 3) return p;
            var r = new List<Vector3> { p[0] };
            for (int i = 0; i < p.Count - 1; i++)
            {
                r.Add(Vector3.Lerp(p[i], p[i + 1], 0.25f));
                r.Add(Vector3.Lerp(p[i], p[i + 1], 0.75f));
            }
            r.Add(p[p.Count - 1]);
            return r;
        }

        private static List<Vector3> Resample(List<Vector3> p, float spacing)
        {
            var r = new List<Vector3> { p[0] };
            float carry = 0f;
            for (int i = 1; i < p.Count; i++)
            {
                Vector3 a = p[i - 1], b = p[i];
                float len = TerrainUtil.FlatDistance(a, b), d = spacing - carry;
                while (d < len) { r.Add(Vector3.Lerp(a, b, d / len)); d += spacing; }
                carry = len - (d - spacing);
            }
            if (TerrainUtil.FlatDistance(r[r.Count - 1], p[p.Count - 1]) > 0.5f) r.Add(p[p.Count - 1]);
            return r;
        }

        // ================================================================== build

        public void BuildDestinations(Transform parent)
        {
            if (_sites.Count == 0) return;
            var wood = GeneratedAssets.Material("Wood", new Color(0.52f, 0.39f, 0.26f));
            var woodDark = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
            foreach (var s in _sites)
            {
                var group = new GameObject($"{s.number}. {s.title}").transform;
                group.SetParent(parent, false);
                group.position = s.centre;

                var dest = group.gameObject.AddComponent<ReserveDestination>();
                dest.number = s.number;
                dest.title = s.title;
                dest.fieldNote = s.note;
                dest.kind = s.kind;
                dest.residentSpecies = s.residents;
                dest.waterRadius = s.water;
                dest.bestTimes = BestTimes(s.kind);
                dest.wildlifeRadius = s.kind == DestinationKind.Wetland ? s.water + 12f : s.radius * 0.7f;
                dest.arrivalRadius = s.kind == DestinationKind.Wetland ? s.water + 14f : 20f;

                switch (s.kind)
                {
                    case DestinationKind.RidgeOverlook: BuildOverlook(group, s, wood, woodDark); break;
                    case DestinationKind.Wetland: BuildPond(group, s, wood, woodDark, dest); break;
                    case DestinationKind.OldGrowth: BuildGrove(group, s, wood, woodDark); break;
                    case DestinationKind.ObservationBlind: BuildBlind(group, s, wood, woodDark, dest); break;
                }
                Fingerpost(group, s, wood, woodDark);
            }
        }

        /// <summary>When each destination is at its best (0.9): shown on the trailhead board and in its signs.</summary>
        private static string BestTimes(DestinationKind kind)
        {
            switch (kind)
            {
                case DestinationKind.RidgeOverlook: return "golden hour · hawks all day";
                case DestinationKind.Wetland: return "dawn mist · deer at dusk";
                case DestinationKind.OldGrowth: return "midday shade · squirrels morning & evening";
                case DestinationKind.ObservationBlind: return "early morning & late afternoon";
                default: return "";
            }
        }

        private void Clear(Vector3 centre, float radius)
        {
            bool Inside(Vector3 p) => TerrainUtil.FlatDistance(p, centre) < radius;
            RemoveWhere(_treeObjects, Inside);
            RemoveWhere(_bushObjects, Inside);
            var doomed = new List<GameObject>();
            foreach (var f in SceneQuery.FindAll<EnvironmentFeature>())
                if (f.kind != FeatureKind.Tree && Inside(f.transform.position) && f.transform.root.name == WildLensSliceBuilder.RootName)
                    doomed.Add(f.gameObject);
            foreach (var d in doomed) Object.DestroyImmediate(d);
        }

        /// <summary>
        /// The reserve's sign standard (0.9): dark stained boards with cream routed lettering and a thin green rule,
        /// the same everywhere, so the place reads as one curated reserve rather than scattered props.
        /// </summary>
        /// <remarks>1.2: big bold title, pictograms and at most one short line (<see cref="SignKit.Sign"/>), readable from
        /// across a clearing. Destination signs carry the destination's number, as on the trailhead map.</remarks>
        private static void Plank(Transform parent, string title, string line, SignIcon[] icons, int number, Vector3 pos, Quaternion rot, Material post) =>
            SignKit.Sign(parent, pos, rot, title, line, icons, number, post);

        /// <summary>A fingerpost where the track leaves the loop: "→ Cattail Pond 0.4 km".</summary>
        private void Fingerpost(Transform group, Site s, Material wood, Material post)
        {
            var pts = s.track.Points;
            if (pts.Count < 4) return;
            Vector3 start = pts[0], dir = TerrainUtil.Flat(pts[3] - pts[0]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 pos = TerrainUtil.Ground(start + dir * 2.5f + side * 2.3f);
            var sign = PropBuilder.Group("Fingerpost: " + s.title, group, pos, Quaternion.LookRotation(side));
            // 1.2: a finger sized to its words, the destination's number disc at the root, lettering on both faces.
            float km = s.track.Length / 1000f;
            string text = $"{s.title.ToUpperInvariant()}  {km:0.0} km";
            float size = SignKit.Fit(text, 0.016f, 1.9f, true);
            float len = SignKit.TextWidth(text, size, true) + 0.5f;
            PropBuilder.Box("Post", sign, new Vector3(0f, 0.85f, 0f), new Vector3(0.11f, 1.7f, 0.11f), post);
            PropBuilder.Box("Finger", sign, new Vector3(-len * 0.5f, 1.45f, 0f), new Vector3(len, 0.3f, 0.05f), PropBuilder.SignBoard);
            PropBuilder.Box("Point", sign, new Vector3(-len, 1.45f, 0f), new Vector3(0.21f, 0.21f, 0.05f), PropBuilder.SignBoard); // arrow tip
            sign.Find("Point").localRotation = Quaternion.Euler(0f, 0f, 45f);
            foreach (float face in new[] { 1f, -1f })
            {
                var side2 = PropBuilder.Group("Face", sign, sign.position, sign.rotation * Quaternion.Euler(0f, face > 0f ? 0f : 180f, 0f));
                // Each face reads root-to-tip: the front has the root on its right, the back on its left.
                float root = face > 0f ? -0.2f : 0.2f, dirX = face > 0f ? -1f : 1f;
                SignKit.Icon(side2, new Vector3(root, 1.45f, 0.03f), SignIcon.Disc, 0.22f, SignKit.Green);
                SignKit.Label(side2, new Vector3(root, 1.45f, 0.033f), s.number.ToString(), 0.019f, SignKit.Cream, TextAnchor.MiddleCenter, true);
                SignKit.Label(side2, new Vector3(root + dirX * 0.15f, 1.45f, 0.03f), text, size, SignKit.Cream,
                    face > 0f ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, true);
            }
            Clear(pos, 1.5f);
        }

        // ------------------------------------------------------------------ Ridge Overlook

        private void BuildOverlook(Transform group, Site s, Material wood, Material post)
        {
            Clear(s.centre, 14f);
            Vector3 f = s.facing;
            float ground = float.MinValue;
            for (int i = 0; i < 9; i++)
                ground = Mathf.Max(ground, TerrainUtil.SampleHeight(s.centre + Quaternion.Euler(0f, i * 40f, 0f) * Vector3.forward * (i == 8 ? 0f : 2f)));
            Vector3 pos = new Vector3(s.centre.x, ground, s.centre.z);
            var deck = PropBuilder.Group("Overlook Deck", group, pos, Quaternion.LookRotation(f));
            float y = 0.32f;
            Planks(deck, new Vector3(0f, y, 0f), new Vector3(4f, 0.12f, 3f), wood, post);
            PropBuilder.Box("Step", deck, new Vector3(0f, y * 0.45f, -1.75f), new Vector3(1.4f, y * 0.9f, 0.5f), post);
            foreach (var c in new[] { new Vector3(-1.9f, 0f, -1.4f), new Vector3(1.9f, 0f, -1.4f), new Vector3(-1.9f, 0f, 1.4f), new Vector3(1.9f, 0f, 1.4f) })
                PropBuilder.Box("Post", deck, c + new Vector3(0f, 0.5f, 0f), new Vector3(0.13f, 1.6f, 0.13f), post);
            PropBuilder.Box("Rail Front", deck, new Vector3(0f, y + 0.95f, 1.45f), new Vector3(4f, 0.09f, 0.09f), post);
            PropBuilder.Box("Rail L", deck, new Vector3(-1.95f, y + 0.95f, 0f), new Vector3(0.09f, 0.09f, 3f), post);
            PropBuilder.Box("Rail R", deck, new Vector3(1.95f, y + 0.95f, 0f), new Vector3(0.09f, 0.09f, 3f), post);
            PropBuilder.Box("Bench Seat", deck, new Vector3(0f, y + 0.45f, -0.6f), new Vector3(2f, 0.08f, 0.42f), wood);
            PropBuilder.Box("Bench Leg L", deck, new Vector3(-0.85f, y + 0.22f, -0.6f), new Vector3(0.09f, 0.45f, 0.4f), post);
            PropBuilder.Box("Bench Leg R", deck, new Vector3(0.85f, y + 0.22f, -0.6f), new Vector3(0.09f, 0.45f, 0.4f), post);
            PropBuilder.AddCollider(deck, new Vector3(0f, y, 0f), new Vector3(4f, 0.12f, 3f));
            PropBuilder.AddCollider(deck, new Vector3(0f, y * 0.45f, -1.75f), new Vector3(1.4f, y * 0.9f, 0.5f));
            foreach (float x in new[] { -1.95f, 1.95f })
                PropBuilder.AddCollider(deck, new Vector3(x, y + 0.6f, 0f), new Vector3(0.1f, 1.2f, 3f));
            PropBuilder.AddCollider(deck, new Vector3(0f, y + 0.6f, 1.45f), new Vector3(4f, 1.2f, 0.1f));

            Plank(group, "RIDGE OVERLOOK", "hawks ride the wind", SignKit.TimeIcons(BestTimes(s.kind)), s.number,
                TerrainUtil.Ground(pos - f * 4.2f + Vector3.Cross(Vector3.up, f) * 2.6f), Quaternion.LookRotation(-f), post);
            ClearCone(pos, f, 35f, 160f);
            AddViewpoint(group, "Ridge Overlook", pos + Vector3.up * (y + 1.65f), TerrainUtil.Ground(pos + f * 120f) + Vector3.up * 3f);

            // A dead snag on the shoulder of the ridge: the hawk's lookout as much as yours.
            var deadwood = GeneratedAssets.Material("DeadWood", new Color(0.45f, 0.42f, 0.38f));
            Vector3 snag = TerrainUtil.Ground(pos + Quaternion.Euler(0f, 70f, 0f) * f * 16f);
            Snag(group, snag, deadwood);
            AddHotspot(group, "Perch Hotspot", HotspotType.Perch, snag, 6f);
            Anchor(group, ScheduleRole.Ridge, pos + f * 25f, 30f); // hawks soar out over the view in daylight

            // Signature (1.0): a viewing telescope at the rail and a stone cairn marking the summit.
            var brass = GeneratedAssets.Material("Brass", new Color(0.72f, 0.56f, 0.28f), 0.75f);
            var scope = PropBuilder.Group("Viewing Telescope", deck, deck.TransformPoint(new Vector3(1.2f, y, 1.15f)), deck.rotation);
            PrimitiveMeshes.CreatePart("Pedestal", PrimitiveType.Cylinder, scope, new Vector3(0f, 0.55f, 0f), new Vector3(0.09f, 0.55f, 0.09f), post);
            PrimitiveMeshes.CreatePart("Body", PrimitiveType.Cylinder, scope, new Vector3(0f, 1.2f, 0.05f), new Vector3(0.14f, 0.22f, 0.14f), brass, new Vector3(80f, 0f, 0f));
            PrimitiveMeshes.CreatePart("Eyepiece", PrimitiveType.Cylinder, scope, new Vector3(0f, 1.17f, -0.2f), new Vector3(0.07f, 0.05f, 0.07f), brass, new Vector3(80f, 0f, 0f));
            var cairnStone = GeneratedAssets.Material("CairnStone", new Color(0.5f, 0.49f, 0.46f), 0.2f);
            Vector3 cairn = TerrainUtil.Ground(pos - f * 3.5f - Vector3.Cross(Vector3.up, f) * 4.5f);
            float cy = 0f;
            for (int i = 0; i < 5; i++)
            {
                float sz = 0.75f - i * 0.13f;
                MeshPart("Cairn Stone", group, Pick(_rocks), cairnStone, cairn + Vector3.up * (cy + sz * 0.3f), new Vector3(sz, sz * 0.6f, sz * 0.9f), Quaternion.Euler(0f, R(0f, 360f), 0f));
                cy += sz * 0.45f;
            }
            AddKeepOut(cairn, 1.5f);
            AddKeepOut(pos, 5f);

            // 0.8 identity: an exposed, rocky crest with the wind in your ears.
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = TerrainUtil.Ground(pos + Quaternion.Euler(0f, 110f + i * 28f + R(-10f, 10f), 0f) * f * R(7f, 15f));
                if (InKeepOut(p) || PathDistance(p) < 3f) continue;
                if (_art && i < 2 && _artBoulders.Count > 0) PlaceArt(_artBoulders, group, p, R(0.8f, 1.2f), 0.15f, 8f);
                else if (_art && _artRocks.Count > 0) PlaceArt(_artRocks, group, p, R(0.8f, 1.6f), 0.15f, 12f);
            }
            // 1.2: bedrock breaks the surface on the ridge's flanks (to the sides, so the view stays open).
            for (int i = 0; i < 2 && _artOutcrops.Count > 0; i++)
            {
                Vector3 p = TerrainUtil.Ground(pos + Quaternion.Euler(0f, (i == 0 ? -1f : 1f) * R(75f, 105f), 0f) * f * R(11f, 17f));
                if (InKeepOut(p) || PathDistance(p) < 5f || !IsFree(p, 5f)) continue;
                PlaceOutcrop(group, p, R(0.6f, 0.85f));
            }
            var wind = new GameObject("Ridge Wind");
            wind.transform.SetParent(group, false);
            wind.transform.position = pos + Vector3.up * 3f;
            wind.AddComponent<AudioSource>();
            var amb = wind.AddComponent<AmbientSound>();
            amb.kind = AmbientKind.Wind;
            amb.volume = 0.5f;
            amb.audibleRadius = 70f;
            InterpretivePanel(group, TerrainUtil.Ground(pos - f * 4.4f - Vector3.Cross(Vector3.up, f) * 2.6f), -f, SignIcon.Bird, "RIDING THE WIND",
                "Hawks soar on the rising air.\nWatch the dead snag.", wood, post);
        }

        // ------------------------------------------------------------------ Cattail Pond

        private void BuildPond(Transform group, Site s, Material wood, Material post, ReserveDestination dest)
        {
            float maxR = s.water * 1.35f;
            Clear(s.centre, maxR + 5f);
            float level = s.waterLevel != 0f ? s.waterLevel : TerrainUtil.SampleHeight(s.centre) + 0.1f;
            // 1.2: still, dark water that mirrors the banks, with faint ripples drifting in the breeze.
            var waterMat = WaterMaterial("PondWater", new Color(0.06f, 0.085f, 0.07f), 0.88f, new Vector2(18f, 18f), 0.25f);
            var disc = GeneratedAssets.LoadOrCreate("Mesh_PondDisc", () => ProceduralMeshes.Disc(48));
            var water = new GameObject("Water");
            water.transform.SetParent(group, false);
            water.transform.position = new Vector3(s.centre.x, level, s.centre.z);
            // The disc reaches under every bank; the irregular shoreline is the terrain rising above it.
            water.transform.localScale = new Vector3(maxR + 3f, 1f, maxR + 3f);
            water.AddComponent<MeshFilter>().sharedMesh = disc;
            var mr = water.AddComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var drift = water.AddComponent<WaterFlow>();
            drift.speed = new Vector2(0.004f, 0.006f);
            drift.wobble = 1f;

            // Reflections of the banks and trees, not just the sky: a realtime probe rendered once on load.
            var probeGo = new GameObject("Pond Reflections");
            probeGo.transform.SetParent(group, false);
            probeGo.transform.position = new Vector3(s.centre.x, level + 2f, s.centre.z);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(maxR * 2f + 30f, 24f, maxR * 2f + 30f);
            probe.boxProjection = true;
            probe.resolution = 256;
            probe.importance = 2;

            Vector3 f = s.facing;
            Vector3 side = Vector3.Cross(Vector3.up, f);
            // Reeds: thick in the shallow coves, thin along the steep side; lily pads on the calm water.
            var clumps = new List<Vector3>();
            for (int i = 0; i < 190; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 probeAt = s.centre + dir * s.water;
                float r = PondRadius(s, probeAt);
                Vector3 c = s.centre + dir * (r + R(-1.6f, 1.0f));
                if (Vector3.Dot(dir, -f) > 0.93f) continue;            // keep the jetty's water open
                float ground = TerrainUtil.SampleHeight(c);
                if (ground > level + 0.5f) continue;                   // reeds only at the waterline
                if (r < s.water && R() < 0.5f) continue;                // fewer on the narrow (steep) side
                clumps.Add(c);
            }
            PlantPondReeds(group, clumps, level);
            var pad = GeneratedAssets.Material("LilyPad", new Color(0.24f, 0.4f, 0.16f), 0.5f);
            var flower = GeneratedAssets.Material("LilyFlower", new Color(0.97f, 0.94f, 0.88f), 0.3f);
            var discMesh = disc;
            for (int i = 0; i < 38; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = PondRadius(s, s.centre + dir * s.water) * R(0.45f, 0.85f);
                Vector3 c = s.centre + dir * r;
                if (Vector3.Dot(dir, -f) > 0.9f) continue;
                float size = R(0.18f, 0.32f);
                MeshPart("Lily Pad", group, discMesh, pad, new Vector3(c.x, level + 0.012f, c.z), new Vector3(size, 1f, size), Quaternion.Euler(0f, R(0f, 360f), 0f));
                if (R() < 0.15f) MeshPart("Lily Flower", group, PrimitiveMeshes.Get(PrimitiveType.Sphere), flower, new Vector3(c.x, level + 0.05f, c.z), new Vector3(0.09f, 0.05f, 0.09f), Quaternion.identity);
            }

            // Jetty from the loop-side shore out over the water.
            Vector3 shore = s.centre - f * (PondRadius(s, s.centre - f * s.water) + 1.5f);
            var jetty = PropBuilder.Group("Jetty", group, new Vector3(shore.x, level, shore.z), Quaternion.LookRotation(f));
            float deckY = 0.42f, len = 7f;
            Planks(jetty, new Vector3(0f, deckY, len * 0.5f - 1f), new Vector3(1.5f, 0.1f, len), wood, post);
            for (float z = -0.5f; z < len - 1f; z += 2f)
                foreach (float x in new[] { -0.7f, 0.7f })
                    PropBuilder.Box("Pile", jetty, new Vector3(x, -0.2f, z), new Vector3(0.12f, 1.3f, 0.12f), post);
            PropBuilder.Box("Rail L", jetty, new Vector3(-0.72f, deckY + 0.85f, len * 0.5f), new Vector3(0.07f, 0.07f, len - 2f), post);
            PropBuilder.Box("Rail R", jetty, new Vector3(0.72f, deckY + 0.85f, len * 0.5f), new Vector3(0.07f, 0.07f, len - 2f), post);
            PropBuilder.Box("Rail End", jetty, new Vector3(0f, deckY + 0.85f, len - 1.5f), new Vector3(1.5f, 0.07f, 0.07f), post);
            PropBuilder.AddCollider(jetty, new Vector3(0f, deckY, len * 0.5f - 1f), new Vector3(1.5f, 0.1f, len));
            PropBuilder.AddCollider(jetty, new Vector3(-0.75f, deckY + 0.6f, len * 0.5f), new Vector3(0.08f, 1.2f, len - 2f));
            PropBuilder.AddCollider(jetty, new Vector3(0.75f, deckY + 0.6f, len * 0.5f), new Vector3(0.08f, 1.2f, len - 2f));
            PropBuilder.AddCollider(jetty, new Vector3(0f, deckY + 0.6f, len - 1.5f), new Vector3(1.5f, 1.2f, 0.08f));

            Plank(group, "CATTAIL POND", "deer drink at dusk", SignKit.TimeIcons(BestTimes(s.kind)), s.number,
                TerrainUtil.Ground(shore - f * 3.5f + side * 2.2f), Quaternion.LookRotation(-f), post);
            InterpretivePanel(group, TerrainUtil.Ground(shore - f * 3.2f - side * 2.4f), -f, SignIcon.Water, "LIFE AT THE WATER'S EDGE",
                "Herons fish the shallows.\nDeer come down at dusk.", wood, post);
            AddViewpoint(group, "Cattail Pond", new Vector3(shore.x, level + deckY + 1.65f, shore.z) + f * (len - 2f), s.centre + f * s.water + Vector3.up);
            Vector3 farShore = TerrainUtil.Ground(s.centre + f * (PondRadius(s, s.centre + f * s.water) + 6f));
            AddHotspot(group, "Pond Edge Hotspot", HotspotType.Meadow, farShore, 10f);
            AddHotspot(group, "Pond Brush Hotspot", HotspotType.BrushPile, TerrainUtil.Ground(s.centre + side * (s.water * 1.2f + 5f)), 7f);
            dest.wildlifeCentre = farShore; // across the water from the jetty
            Anchor(group, ScheduleRole.Water, farShore, 12f);
            AddHotspot(group, "Pond Shallows", HotspotType.Water, TerrainUtil.Ground(s.centre + side * PondRadius(s, s.centre + side * s.water)), 8f);
            AddHotspot(group, "Pond Shallows", HotspotType.Water, farShore, 8f);
            // Signature (1.0): the rangers' canoe pulled up on the bank by the jetty, a paddle across it.
            Vector3 canoePos = TerrainUtil.Ground(shore + side * 3.2f - f * 0.8f);
            var canoe = PropBuilder.Group("Ranger Canoe", group, canoePos, Quaternion.LookRotation(Quaternion.Euler(0f, 25f, 0f) * f));
            PrimitiveMeshes.CreatePart("Hull", PrimitiveType.Capsule, canoe, new Vector3(0f, 0.12f, 0f), new Vector3(0.82f, 2.1f, 0.42f),
                GeneratedAssets.Material("CanoeHull", new Color(0.22f, 0.36f, 0.24f), 0.5f), new Vector3(90f, 0f, 0f));
            PrimitiveMeshes.CreatePart("Gunwale", PrimitiveType.Cube, canoe, new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.03f, 3.6f), wood);
            PrimitiveMeshes.CreatePart("Paddle", PrimitiveType.Cube, canoe, new Vector3(0.1f, 0.34f, 0.3f), new Vector3(0.06f, 0.03f, 1.4f), post, new Vector3(0f, 35f, 0f));
            AddKeepOut(canoePos, 2.6f);
            Mist(group, new Vector3(s.centre.x, level + 0.6f, s.centre.z), new Vector3(maxR * 2f + 6f, 1.4f, maxR * 2f + 6f), 1f);
            AddKeepOut(shore, 4f);
        }

        private void PlantPondReeds(Transform group, List<Vector3> clumps, float level)
        {
            var stems = new List<CombineInstance>();
            var heads = new List<CombineInstance>();
            Mesh capsule = PrimitiveMeshes.Get(PrimitiveType.Capsule);
            foreach (var c in clumps)
            {
                float ground = Mathf.Max(level - 0.3f, TerrainUtil.SampleHeight(c));
                int n = _rng.Next(6, 12);
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = new Vector3(c.x + R(-0.55f, 0.55f), ground, c.z + R(-0.55f, 0.55f));
                    float h = R(1.1f, 1.9f);
                    var rot = Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-8f, 8f));
                    stems.Add(new CombineInstance { mesh = _cylinder, transform = Matrix4x4.TRS(p + rot * Vector3.up * (h * 0.5f), rot, new Vector3(0.022f, h * 0.5f, 0.022f)) });
                    if (R() < 0.35f)
                        heads.Add(new CombineInstance { mesh = capsule, transform = Matrix4x4.TRS(p + rot * Vector3.up * (h * 0.82f), rot, new Vector3(0.05f, 0.11f, 0.05f)) });
                }
            }
            CombinedStatic(group, "Pond Reeds", stems, new Color(0.38f, 0.44f, 0.2f));
            CombinedStatic(group, "Pond Cattails", heads, new Color(0.3f, 0.19f, 0.1f));
        }

        // ------------------------------------------------------------------ Old-Growth Grove

        private void BuildGrove(Transform group, Site s, Material wood, Material post)
        {
            Clear(s.centre, 8f);
            Vector3 f = s.facing;
            // Three giants around the clearing.
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = TerrainUtil.Ground(s.centre + Quaternion.Euler(0f, i * 120f + R(-20f, 20f), 0f) * f * R(10f, 13f));
                Clear(p, 4f);
                var giant = GiantTree(group, p);
                bool kitGiant = IsKit(giant);
                giant.name = "Old-Growth Giant";
                float k = kitGiant ? R(1.1f, 1.3f) : R(1.7f, 2f); // a kit giant is already modelled big
                giant.transform.localScale *= k;
                var feature = giant.GetComponent<EnvironmentFeature>();
                if (feature != null) { feature.radius *= k; feature.height *= k; }
                MakeStaticExceptSway(giant);
                _treeObjects.Add(giant);
                Occupy(p);
                AddHotspot(group, "Mature Tree Hotspot", HotspotType.MatureTree, p, 12f);
            }
            // Signature (1.0): the Grandmother Oak, the biggest giant, roped off with a plaque.
            GameObject eldest = null;
            foreach (Transform t in group) if (t.name == "Old-Growth Giant" && (eldest == null || t.localScale.x > eldest.transform.localScale.x)) eldest = t.gameObject;
            if (eldest != null)
            {
                eldest.name = "Grandmother Oak";
                eldest.transform.localScale *= 1.2f;
                var ef = eldest.GetComponent<EnvironmentFeature>();
                if (ef != null) { ef.radius *= 1.2f; ef.height *= 1.2f; }
                Vector3 c = eldest.transform.position;
                var rope = GeneratedAssets.Material("Rope", new Color(0.62f, 0.53f, 0.38f), 0.1f);
                const int posts = 9; const float ringR = 3.4f;
                var ring = new GameObject("Rope Ring").transform;
                ring.SetParent(group, false);
                ring.position = c;
                Vector3 prev = Vector3.zero;
                for (int i = 0; i <= posts; i++)
                {
                    float a2 = i / (float)posts * Mathf.PI * 2f;
                    Vector3 pt = TerrainUtil.Ground(c + new Vector3(Mathf.Cos(a2), 0f, Mathf.Sin(a2)) * ringR);
                    if (i < posts) PrimitiveMeshes.CreatePart("Post", PrimitiveType.Cylinder, ring, ring.InverseTransformPoint(pt) + Vector3.up * 0.4f, new Vector3(0.09f, 0.4f, 0.09f), post);
                    if (i > 0)
                    {
                        Vector3 mid = (prev + pt) * 0.5f + Vector3.up * 0.62f;
                        var seg = PrimitiveMeshes.CreatePart("Rope", PrimitiveType.Cylinder, ring, ring.InverseTransformPoint(mid), new Vector3(0.025f, Vector3.Distance(prev, pt) * 0.5f, 0.025f), rope);
                        seg.transform.rotation = Quaternion.FromToRotation(Vector3.up, (pt - prev).normalized);
                    }
                    prev = pt;
                }
                Vector3 toClearing = TerrainUtil.Flat(s.centre - c).normalized;
                var plaque = PropBuilder.Group("Plaque", group, TerrainUtil.Ground(c + toClearing * (ringR + 0.7f)), Quaternion.LookRotation(toClearing));
                PropBuilder.Box("Post", plaque, new Vector3(0f, 0.4f, 0f), new Vector3(0.1f, 0.8f, 0.1f), post);
                var plate = PropBuilder.Group("Plate", plaque, plaque.TransformPoint(new Vector3(0f, 0.86f, 0.03f)), plaque.rotation * Quaternion.Euler(-30f, 0f, 0f));
                PropBuilder.Box("Board", plate, Vector3.zero, new Vector3(0.95f, 0.42f, 0.04f), PropBuilder.SignBoard);
                SignKit.Label(plate, new Vector3(0f, 0.07f, 0.022f), "GRANDMOTHER OAK", SignKit.Fit("GRANDMOTHER OAK", 0.016f, 0.85f, true), SignKit.Cream, TextAnchor.MiddleCenter, true);
                SignKit.Label(plate, new Vector3(0f, -0.1f, 0.022f), "about 280 years old", 0.011f, SignKit.Cream);
                Occupy(c);
                AddKeepOut(c, ringR + 0.5f);
            }

            // 0.8: a closed canopy around the clearing. Shade, big trunks, dense stand, mostly broadleaf.
            int planted = 0;
            for (int attempt = 0; attempt < 600 && planted < 72; attempt++)
            {
                float a = R(0f, Mathf.PI * 2f), r = Mathf.Lerp(9f, 40f, Mathf.Sqrt(R())); // 0.9: twice as dense: real shade
                Vector3 p = TerrainUtil.Ground(s.centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                if (PathDistance(p) < 3.5f || TerrainUtil.SlopeDegrees(p) > 30f || !IsFree(p, 2.6f)) continue;
                var tree = R() < 0.75f ? Deciduous(group, p) : Conifer(group, p);
                float k = R(1.15f, 1.5f);
                tree.transform.localScale *= k;
                var feature = tree.GetComponent<EnvironmentFeature>();
                if (feature != null) { feature.radius *= k; feature.height *= k; }
                MakeStaticExceptSway(tree);
                AddDistanceCull(tree, 0.012f);
                _treeObjects.Add(tree);
                Occupy(p);
                planted++;
            }
            // Moss-covered rocks, fallen timber, stumps and a carpet of ferns: an old forest floor.
            for (int i = 0; i < 26; i++)
            {
                float a = R(0f, Mathf.PI * 2f), r = R(5f, 34f);
                Vector3 p = TerrainUtil.Ground(s.centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                if (PathDistance(p) < 2.5f || InKeepOut(p)) continue;
                if (i < 7 && _art && _artRocks.Count > 0) PlaceArt(_artRocks, group, p, R(0.7f, 1.4f), 0.15f, 10f);
                else if (i < 10 && _art && _artLogs.Count > 0 && IsFree(p, 3f)) PlaceArt(_artLogs, group, p, R(0.9f, 1.2f), 0.15f, 3f);
                else if (i < 12 && _art && _artStumps.Count > 0) PlaceArt(_artStumps, group, p, R(0.9f, 1.3f), 0.05f, 4f);
                else if (_art && _artFerns.Count > 0)
                    for (int k = 0; k < 4; k++)
                        PlaceArt(_artFerns, group, TerrainUtil.Ground(p + new Vector3(R(-1.5f, 1.5f), 0f, R(-1.5f, 1.5f))), R(0.8f, 1.3f), 0.02f, 6f);
            }
            // Bird boxes on two big trunks: someone looks after this place.
            var box = GeneratedAssets.Material("NestBox", new Color(0.46f, 0.36f, 0.25f));
            int boxes = 0;
            foreach (Transform t in group)
            {
                if (boxes >= 2 || !t.name.StartsWith("Old-Growth Giant")) continue;
                Vector3 toward = TerrainUtil.Flat(s.centre - t.position).normalized;
                var nb = PropBuilder.Group("Nest Box", group, t.position + Vector3.up * 3.2f + toward * 0.45f, Quaternion.LookRotation(toward));
                PropBuilder.Box("Box", nb, Vector3.zero, new Vector3(0.22f, 0.32f, 0.2f), box);
                PropBuilder.Box("Roof", nb, new Vector3(0f, 0.18f, 0.02f), new Vector3(0.28f, 0.03f, 0.26f), post);
                PrimitiveMeshes.CreatePart("Hole", PrimitiveType.Cylinder, nb, new Vector3(0f, 0.05f, 0.101f), new Vector3(0.05f, 0.005f, 0.05f),
                    GeneratedAssets.Material("NestHole", new Color(0.05f, 0.04f, 0.03f)), new Vector3(90f, 0f, 0f));
                boxes++;
            }
            // A bench in the clearing, facing the biggest trunk.
            var bench = PropBuilder.Group("Bench", group, TerrainUtil.Ground(s.centre - f * 2f), Quaternion.LookRotation(f));
            PropBuilder.Box("Seat", bench, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.45f), wood);
            PropBuilder.Box("Back", bench, new Vector3(0f, 0.8f, -0.22f), new Vector3(1.8f, 0.34f, 0.06f), wood);
            PropBuilder.Box("LegL", bench, new Vector3(-0.75f, 0.22f, 0f), new Vector3(0.09f, 0.45f, 0.42f), post);
            PropBuilder.Box("LegR", bench, new Vector3(0.75f, 0.22f, 0f), new Vector3(0.09f, 0.45f, 0.42f), post);
            PropBuilder.AddCollider(bench, new Vector3(0f, 0.45f, -0.02f), new Vector3(1.8f, 0.9f, 0.5f));
            Plank(group, "OLD-GROWTH GROVE", "cool shade at midday", SignKit.TimeIcons(BestTimes(s.kind)), s.number, TerrainUtil.Ground(s.centre - f * 5f + Vector3.Cross(Vector3.up, f) * 3f),
                Quaternion.LookRotation(-f), post);
            InterpretivePanel(group, TerrainUtil.Ground(s.centre - f * 5f - Vector3.Cross(Vector3.up, f) * 3f), -f, SignIcon.Tree, "OLD GROWTH",
                "Trees older than the reserve.\nOwls roost here by day.", wood, post);
            AddViewpoint(group, "Old-Growth Grove", TerrainUtil.Ground(s.centre - f * 2f) + Vector3.up * 1.2f, TerrainUtil.Ground(s.centre + f * 12f) + Vector3.up * 6f);
            AddKeepOut(s.centre, 6f);
        }

        // ------------------------------------------------------------------ Meadow Blind

        /// <summary>
        /// 0.8 rebuild. A roomy hide (4 × 3 m inside) on a levelled pad with a plank floor, a 1.2 m doorway in the back
        /// wall reached by a duckboard path from the track, and a long viewing window that works standing (eye 1.65 m)
        /// and crouched (eye 0.95 m). Brush screens its sides. The meadow hotspot sits 14 m in front, so animals are
        /// close. Inside, the HUD says you're hidden and a ranger's log hangs on the wall.
        /// </summary>
        private void BuildBlind(Transform group, Site s, Material wood, Material post, ReserveDestination dest)
        {
            Vector3 f = s.facing; // into the meadow
            Vector3 side = Vector3.Cross(Vector3.up, f);
            Vector3 pos = s.structure != Vector3.zero ? s.structure : TerrainUtil.Ground(s.centre - f * (s.radius * 0.5f));
            group.position = pos; // arrival is at the hide, not in the middle of the meadow
            Clear(pos, 6f);
            var hide = PropBuilder.Group("Observation Blind", group, pos, Quaternion.LookRotation(f));
            const float W = 4.2f, D = 3.2f, H = 2.35f, t = 0.1f;
            const float sill = 0.85f, head = 1.8f, door = 1.2f;
            // Front: a long window from crouched to standing eye height, framed by posts.
            Wall(hide, "Front Low", new Vector3(0f, sill * 0.5f, D * 0.5f), new Vector3(W, sill, t), wood);
            Wall(hide, "Front High", new Vector3(0f, (head + H) * 0.5f, D * 0.5f), new Vector3(W, H - head, t), wood);
            foreach (float x in new[] { -W * 0.5f + 0.3f, 0f, W * 0.5f - 0.3f })
                PropBuilder.Box("Window Post", hide, new Vector3(x, (sill + head) * 0.5f, D * 0.5f), new Vector3(0.08f, head - sill, 0.08f), post);
            PropBuilder.Box("Shelf", hide, new Vector3(0f, sill + 0.02f, D * 0.5f - 0.17f), new Vector3(W - 0.3f, 0.04f, 0.3f), post);
            Wall(hide, "Side L", new Vector3(-W * 0.5f, H * 0.5f, 0f), new Vector3(t, H, D), wood);
            Wall(hide, "Side R", new Vector3(W * 0.5f, H * 0.5f, 0f), new Vector3(t, H, D), wood);
            // Back wall with a centred 1.2 m doorway (≥ the capsule's 0.8 m with room to spare; see AGENTS.md checklist).
            float seg = (W - door) * 0.5f;
            Wall(hide, "Back L", new Vector3(-(door + seg) * 0.5f, H * 0.5f, -D * 0.5f), new Vector3(seg, H, t), wood);
            Wall(hide, "Back R", new Vector3((door + seg) * 0.5f, H * 0.5f, -D * 0.5f), new Vector3(seg, H, t), wood);
            Wall(hide, "Lintel", new Vector3(0f, H - 0.12f, -D * 0.5f), new Vector3(door, 0.24f, t), wood);
            Wall(hide, "Roof", new Vector3(0f, H + 0.06f, 0.1f), new Vector3(W + 0.5f, 0.1f, D + 0.7f), post);
            // Plank floor: grass doesn't poke through, footing is level.
            Planks(hide, new Vector3(0f, 0.03f, 0f), new Vector3(W - 0.1f, 0.06f, D - 0.1f), post, wood);
            PropBuilder.AddCollider(hide, new Vector3(0f, 0.03f, 0f), new Vector3(W - 0.1f, 0.06f, D - 0.1f));
            PropBuilder.Box("Bench", hide, new Vector3(0f, 0.45f, D * 0.5f - 0.75f), new Vector3(W - 0.8f, 0.07f, 0.35f), wood);
            // Ranger's log on the inside of the left wall, readable from the bench.
            var log = PropBuilder.Group("Ranger Log", hide, hide.TransformPoint(new Vector3(-W * 0.5f + 0.07f, 1.45f, -0.2f)), hide.rotation * Quaternion.Euler(0f, 90f, 0f));
            PropBuilder.Box("Board", log, Vector3.zero, new Vector3(0.95f, 0.7f, 0.02f), GeneratedAssets.Material("PanelFace", new Color(0.88f, 0.84f, 0.7f), 0.2f));
            SignKit.Label(log, new Vector3(0f, 0.25f, 0.012f), "RANGER'S LOG", 0.011f, SignKit.Ink, TextAnchor.MiddleCenter, true);
            const string entries = "Mon  doe and fawn, dawn\nTue  cottontails, brush line\nThu  fox crossing at dusk\nSat  turkeys by the feeder\nSit still. They come to you.";
            SignKit.Label(log, new Vector3(0f, -0.06f, 0.012f), entries, SignKit.Fit(entries, 0.0075f, 0.85f, false), SignKit.Ink);
            var blind = hide.gameObject.AddComponent<ObservationBlind>();
            blind.interiorCentre = new Vector3(0f, 1f, 0f);
            blind.interiorSize = new Vector3(W - 0.2f, 2f, D - 0.2f);

            // Duckboards from the track's end to the door.
            Vector3 doorWorld = hide.TransformPoint(new Vector3(0f, 0f, -D * 0.5f - 0.3f));
            Vector3 start = s.arrival;
            float pathLen = TerrainUtil.FlatDistance(start, doorWorld);
            Vector3 along = TerrainUtil.Flat(doorWorld - start).normalized;
            for (float d = 0.6f; d < pathLen; d += 1.25f)
            {
                Vector3 p = TerrainUtil.Ground(start + along * d);
                var board = PropBuilder.Group("Duckboard", group, p + Vector3.up * 0.03f, Quaternion.LookRotation(along));
                PropBuilder.Box("Slats", board, Vector3.zero, new Vector3(0.9f, 0.05f, 1.15f), wood);
            }
            // Brush screens the hide's sides so it reads as part of the meadow edge.
            for (int i = 0; i < 6; i++)
            {
                float sgn = i % 2 == 0 ? -1f : 1f;
                Vector3 p = TerrainUtil.Ground(pos + side * sgn * R(W * 0.5f + 1.2f, W * 0.5f + 3.5f) + f * R(-1.5f, 1.2f));
                if (_art && _artShrubs.Count > 0) PlaceArt(_artShrubs, group, p, R(0.9f, 1.3f), 0.02f, 3f);
            }

            Plank(group, "MEADOW BLIND", "enter quietly", SignKit.TimeIcons(BestTimes(s.kind)), s.number, TerrainUtil.Ground(s.arrival + side * 2.2f), Quaternion.LookRotation(-along), post);
            InterpretivePanel(group, TerrainUtil.Ground(s.arrival - side * 2.4f), -along, SignIcon.Binoculars, "A MEADOW FOR EVERYONE",
                "Sit still inside the blind.\nThe meadow comes to you.", wood, post);
            AddViewpoint(group, "Meadow Blind", pos + Vector3.up * 1.65f + f * (D * 0.5f - 0.15f), s.centre + Vector3.up * 0.8f);
            ClearCone(pos, f, 45f, s.radius * 1.8f);
            // Signature (1.0): a bird feeder on a pole in front of the window: life close to the glass.
            Vector3 feeder = TerrainUtil.Ground(pos + f * 6.5f + side * 0.8f);
            var fd = PropBuilder.Group("Bird Feeder", group, feeder, Quaternion.LookRotation(-f));
            PrimitiveMeshes.CreatePart("Pole", PrimitiveType.Cylinder, fd, new Vector3(0f, 0.85f, 0f), new Vector3(0.05f, 0.85f, 0.05f), post);
            PropBuilder.Box("Tray", fd, new Vector3(0f, 1.62f, 0f), new Vector3(0.42f, 0.04f, 0.42f), wood);
            PropBuilder.Box("Roof L", fd, new Vector3(-0.12f, 1.92f, 0f), new Vector3(0.3f, 0.03f, 0.5f), post);
            PropBuilder.Box("Roof R", fd, new Vector3(0.12f, 1.92f, 0f), new Vector3(0.3f, 0.03f, 0.5f), post);
            fd.Find("Roof L").localRotation = Quaternion.Euler(0f, 0f, 35f);
            fd.Find("Roof R").localRotation = Quaternion.Euler(0f, 0f, -35f);
            PrimitiveMeshes.CreatePart("Post", PrimitiveType.Cylinder, fd, new Vector3(0f, 1.77f, 0f), new Vector3(0.025f, 0.13f, 0.025f), post);
            AddKeepOut(feeder, 1f);

            Vector3 meadow = TerrainUtil.Ground(pos + f * 14f);
            AddHotspot(group, "Meadow Hotspot", HotspotType.Meadow, meadow, 12f);
            AddHotspot(group, "Brush Hotspot", HotspotType.BrushPile, TerrainUtil.Ground(meadow + side * 13f), 7f);
            dest.wildlifeCentre = meadow;
            dest.wildlifeRadius = 12f;
            AddKeepOut(pos, 4f);
        }

        /// <summary>Visible deck boards (1.0): a row of planks with small gaps instead of one flat box. Visual only.</summary>
        private static void Planks(Transform parent, Vector3 centre, Vector3 size, Material a, Material b)
        {
            const float board = 0.16f, gap = 0.014f;
            int count = Mathf.Max(1, Mathf.FloorToInt(size.z / (board + gap)));
            float start = centre.z - size.z * 0.5f + board * 0.5f;
            for (int i = 0; i < count; i++)
                PropBuilder.Box("Plank", parent, new Vector3(centre.x, centre.y + (i % 3 == 0 ? 0.004f : 0f), start + i * (board + gap)),
                    new Vector3(size.x * (i % 5 == 2 ? 0.985f : 1f), size.y, board), i % 4 == 1 ? b : a);
        }

        private static void Wall(Transform parent, string name, Vector3 centre, Vector3 size, Material mat)
        {
            PropBuilder.Box(name, parent, centre, size, mat);
            PropBuilder.AddCollider(parent, centre, size);
        }
    }
}
