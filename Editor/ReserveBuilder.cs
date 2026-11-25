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
            float d = _trail.DistanceToTrail(p);
            foreach (var t in _tracks) d = Mathf.Min(d, t.DistanceTo(p));
            return d;
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
            RouteTracks(parent, trackMeshes);

            // Number destinations nearest-first along their tracks.
            _sites.Sort((a, b) => a.track.Length.CompareTo(b.track.Length));
            for (int i = 0; i < _sites.Count; i++) _sites[i].number = i + 1;

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
                    site.residents = new[] { "red_tailed_hawk" };
                    break;
                case DestinationKind.Wetland:
                    site.title = "Cattail Pond";
                    site.note = "Still water in the low ground. Deer come down to drink at the reed edge.";
                    site.residents = new[] { "white_tailed_deer" };
                    site.water = 14f;
                    break;
                case DestinationKind.OldGrowth:
                    site.title = "Old-Growth Grove";
                    site.note = "The oldest trees in the reserve. Squirrels everywhere; a fox dens among the roots.";
                    site.residents = new[] { "eastern_gray_squirrel", "red_fox" };
                    break;
                case DestinationKind.ObservationBlind:
                    site.title = "Wildflower Meadow Blind";
                    site.note = "A wooden hide on the meadow's edge. Step inside and you all but disappear.";
                    site.residents = new[] { "white_tailed_deer", "eastern_cottontail" };
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
                    s.waterLevel = RingMin(s.centre, R + 16f) - 0.35f;
                    // A terrain at its lowest representable height can't be dug: raise the water and build the banks up instead.
                    float floor = _terrain.transform.position.y + 0.05f;
                    if (s.waterLevel - 1.6f < floor) s.waterLevel = floor + 1.6f;
                    float level = s.waterLevel;
                    Shape(s.centre, R + 9f, (d, h) =>
                    {
                        if (d < R) return Mathf.Min(h, level - 0.15f - 1.3f * (1f - (d / R) * (d / R)));
                        // Banks: a gentle shore rising from just above the water, blended into the land.
                        float bank = level + 0.12f + (d - R) * 0.09f;
                        float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(R + 3f, R + 9f, d));
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

        private void Shape(Vector3 centre, float radius, System.Func<float, float, float> f)
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
                heights[z, x] = Mathf.Clamp01((f(d, h) - tPos.y) / size.y);
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
                    // The hide sits on the meadow edge nearest the loop, facing in; the track arrives from behind it.
                    s.facing = -toLoop;
                    s.arrival = TerrainUtil.Ground(s.centre + toLoop * (s.radius * 0.75f + 9f));
                    break;
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
                track.SetPath(path, 2.4f);
                _tracks.Add(track);
                s.track = track;

                if (meshes)
                {
                    var mesh = GeneratedAssets.Save(ProceduralMeshes.TrailRibbon(path, 2.2f, 0.6f, closed: false), "Mesh_Track_" + s.kind);
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

        private static void Plank(Transform parent, string text, Vector3 pos, Quaternion rot, Material wood, Material post, float width = 1.3f)
        {
            var sign = PropBuilder.Group("Sign", parent, pos, rot);
            PropBuilder.Box("Post", sign, new Vector3(0f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.1f), post);
            PropBuilder.Box("Board", sign, new Vector3(0f, 1.15f, 0.06f), new Vector3(width, 0.42f, 0.05f), wood);
            PropBuilder.Label(sign, new Vector3(0f, 1.15f, 0.09f), text, 0.011f);
        }

        /// <summary>A fingerpost where the track leaves the loop: "→ Cattail Pond 0.4 km".</summary>
        private void Fingerpost(Transform group, Site s, Material wood, Material post)
        {
            var pts = s.track.Points;
            if (pts.Count < 4) return;
            Vector3 start = pts[0], dir = TerrainUtil.Flat(pts[3] - pts[0]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 pos = TerrainUtil.Ground(start + dir * 2.5f + side * 2.3f);
            var sign = PropBuilder.Group("Fingerpost: " + s.title, group, pos, Quaternion.LookRotation(side));
            PropBuilder.Box("Post", sign, new Vector3(0f, 0.85f, 0f), new Vector3(0.11f, 1.7f, 0.11f), post);
            PropBuilder.Box("Finger", sign, new Vector3(-0.55f, 1.45f, 0f), new Vector3(1.2f, 0.24f, 0.05f), wood);
            PropBuilder.Box("Point", sign, new Vector3(-1.2f, 1.45f, 0f), new Vector3(0.17f, 0.17f, 0.05f), wood); // arrow tip
            sign.Find("Point").localRotation = Quaternion.Euler(0f, 0f, 45f);
            float km = s.track.Length / 1000f;
            string text = $"{s.number}  {s.title.ToUpperInvariant()}  {km:0.0} km";
            PropBuilder.Label(sign, new Vector3(-0.55f, 1.45f, 0.03f), text, 0.0085f);
            PropBuilder.Label(sign, new Vector3(-0.55f, 1.45f, -0.03f), text, 0.0085f);
            sign.GetChild(sign.childCount - 1).localRotation = Quaternion.identity; // second label reads from the back
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
            PropBuilder.Box("Deck", deck, new Vector3(0f, y, 0f), new Vector3(4f, 0.12f, 3f), wood);
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

            Plank(group, "RIDGE OVERLOOK\nHawks hunt the slopes below", TerrainUtil.Ground(pos - f * 4.2f + Vector3.Cross(Vector3.up, f) * 2.6f),
                Quaternion.LookRotation(-f), wood, post);
            ClearCone(pos, f, 35f, 160f);
            AddViewpoint(group, "Ridge Overlook", pos + Vector3.up * (y + 1.65f), TerrainUtil.Ground(pos + f * 120f) + Vector3.up * 3f);

            // A dead snag on the shoulder of the ridge: the hawk's lookout as much as yours.
            var deadwood = GeneratedAssets.Material("DeadWood", new Color(0.45f, 0.42f, 0.38f));
            Vector3 snag = TerrainUtil.Ground(pos + Quaternion.Euler(0f, 70f, 0f) * f * 16f);
            Snag(group, snag, deadwood);
            AddHotspot(group, "Perch Hotspot", HotspotType.Perch, snag, 6f);
            AddKeepOut(pos, 5f);
        }

        // ------------------------------------------------------------------ Cattail Pond

        private void BuildPond(Transform group, Site s, Material wood, Material post, ReserveDestination dest)
        {
            Clear(s.centre, s.water + 6f);
            float level = s.waterLevel != 0f ? s.waterLevel : TerrainUtil.SampleHeight(s.centre) + 0.1f;
            var waterMat = GeneratedAssets.Material("PondWater", new Color(0.1f, 0.17f, 0.17f), 0.96f);
            var disc = GeneratedAssets.LoadOrCreate("Mesh_PondDisc", () => ProceduralMeshes.Disc(48));
            var water = new GameObject("Water");
            water.transform.SetParent(group, false);
            water.transform.position = new Vector3(s.centre.x, level, s.centre.z);
            water.transform.localScale = new Vector3(s.water + 3f, 1f, s.water + 3f);
            water.AddComponent<MeshFilter>().sharedMesh = disc;
            var mr = water.AddComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            BuildReeds(group, s.centre, s.water, level);

            // Jetty from the loop-side shore out over the water.
            Vector3 f = s.facing;
            Vector3 shore = s.centre - f * (s.water + 1.5f);
            var jetty = PropBuilder.Group("Jetty", group, new Vector3(shore.x, level, shore.z), Quaternion.LookRotation(f));
            float deckY = 0.42f, len = 7f;
            PropBuilder.Box("Deck", jetty, new Vector3(0f, deckY, len * 0.5f - 1f), new Vector3(1.5f, 0.1f, len), wood);
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

            Vector3 side = Vector3.Cross(Vector3.up, f);
            Plank(group, "CATTAIL POND\nDeer drink here at dusk", TerrainUtil.Ground(shore - f * 3.5f + side * 2.2f), Quaternion.LookRotation(-f), wood, post);
            AddViewpoint(group, "Cattail Pond", new Vector3(shore.x, level + deckY + 1.65f, shore.z) + f * (len - 2f), s.centre + f * s.water + Vector3.up);
            AddHotspot(group, "Pond Edge Hotspot", HotspotType.Meadow, s.centre + f * (s.water + 6f), 10f);
            dest.wildlifeCentre = TerrainUtil.Ground(s.centre + f * (s.water + 6f)); // the far shore, across the water from the jetty
            AddKeepOut(shore, 4f);
        }

        private void BuildReeds(Transform group, Vector3 centre, float radius, float level)
        {
            var stems = new List<CombineInstance>();
            var heads = new List<CombineInstance>();
            Mesh cyl = _cylinder, capsule = PrimitiveMeshes.Get(PrimitiveType.Capsule);
            for (int clump = 0; clump < 70; clump++)
            {
                float a = R(0f, Mathf.PI * 2f);
                float d = radius + R(-1.8f, 1.2f);
                Vector3 c = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
                float ground = Mathf.Max(level - 0.3f, TerrainUtil.SampleHeight(c));
                if (ground > level + 0.6f) continue; // reeds only at the waterline
                int n = _rng.Next(5, 10);
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = new Vector3(c.x + R(-0.5f, 0.5f), ground, c.z + R(-0.5f, 0.5f));
                    float h = R(1.1f, 1.9f);
                    var rot = Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-8f, 8f));
                    stems.Add(new CombineInstance { mesh = cyl, transform = Matrix4x4.TRS(p + rot * Vector3.up * (h * 0.5f), rot, new Vector3(0.022f, h * 0.5f, 0.022f)) });
                    if (R() < 0.35f)
                        heads.Add(new CombineInstance { mesh = capsule, transform = Matrix4x4.TRS(p + rot * Vector3.up * (h * 0.82f), rot, new Vector3(0.05f, 0.11f, 0.05f)) });
                }
            }
            void Combined(string name, List<CombineInstance> parts, Color colour)
            {
                if (parts.Count == 0) return;
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(parts.ToArray(), true, true);
                mesh = GeneratedAssets.Save(mesh, "Mesh_" + name);
                var go = new GameObject(name);
                go.transform.SetParent(group, false);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = GeneratedAssets.Material(name, colour);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            Combined("Reeds", stems, new Color(0.38f, 0.44f, 0.2f));
            Combined("Cattails", heads, new Color(0.3f, 0.19f, 0.1f));
        }

        // ------------------------------------------------------------------ Old-Growth Grove

        private void BuildGrove(Transform group, Site s, Material wood, Material post)
        {
            Clear(s.centre, 9f);
            Vector3 f = s.facing;
            // Three giants around the clearing.
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = TerrainUtil.Ground(s.centre + Quaternion.Euler(0f, i * 120f + R(-20f, 20f), 0f) * f * R(11f, 14f));
                Clear(p, 4f);
                var giant = Deciduous(group, p);
                giant.name = "Old-Growth Giant";
                float k = R(1.6f, 1.9f);
                giant.transform.localScale *= k;
                var feature = giant.GetComponent<EnvironmentFeature>();
                if (feature != null) { feature.radius *= k; feature.height *= k; }
                MakeStaticExceptSway(giant);
                _treeObjects.Add(giant);
                AddHotspot(group, "Mature Tree Hotspot", HotspotType.MatureTree, p, 12f);
            }
            // A bench in the clearing, facing the biggest trunk, and a fallen log.
            var bench = PropBuilder.Group("Bench", group, TerrainUtil.Ground(s.centre - f * 2f), Quaternion.LookRotation(f));
            PropBuilder.Box("Seat", bench, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.45f), wood);
            PropBuilder.Box("Back", bench, new Vector3(0f, 0.8f, -0.22f), new Vector3(1.8f, 0.34f, 0.06f), wood);
            PropBuilder.Box("LegL", bench, new Vector3(-0.75f, 0.22f, 0f), new Vector3(0.09f, 0.45f, 0.42f), post);
            PropBuilder.Box("LegR", bench, new Vector3(0.75f, 0.22f, 0f), new Vector3(0.09f, 0.45f, 0.42f), post);
            PropBuilder.AddCollider(bench, new Vector3(0f, 0.45f, -0.02f), new Vector3(1.8f, 0.9f, 0.5f));
            Plank(group, "OLD-GROWTH GROVE\nQuiet please: squirrels at work", TerrainUtil.Ground(s.centre - f * 5f + Vector3.Cross(Vector3.up, f) * 3f),
                Quaternion.LookRotation(-f), wood, post);
            AddViewpoint(group, "Old-Growth Grove", TerrainUtil.Ground(s.centre - f * 2f) + Vector3.up * 1.2f, TerrainUtil.Ground(s.centre + f * 12f) + Vector3.up * 6f);
            AddKeepOut(s.centre, 6f);
        }

        // ------------------------------------------------------------------ Meadow Blind

        private void BuildBlind(Transform group, Site s, Material wood, Material post, ReserveDestination dest)
        {
            Vector3 f = s.facing; // into the meadow
            Vector3 pos = TerrainUtil.Ground(s.centre - f * (s.radius * 0.75f));
            group.position = pos; // arrival is at the hide, not in the middle of the meadow
            Clear(pos, 5f);
            var hide = PropBuilder.Group("Observation Blind", group, pos, Quaternion.LookRotation(f));
            const float W = 3.4f, D = 2.4f, H = 2.25f, t = 0.09f;
            const float slotLow = 1.32f, slotHigh = 1.78f;
            // Front wall with a long viewing slot at standing eye height.
            Wall(hide, "Front Low", new Vector3(0f, slotLow * 0.5f, D * 0.5f), new Vector3(W, slotLow, t), wood);
            Wall(hide, "Front High", new Vector3(0f, (slotHigh + H) * 0.5f, D * 0.5f), new Vector3(W, H - slotHigh, t), wood);
            Wall(hide, "Shelf", new Vector3(0f, slotLow - 0.02f, D * 0.5f - 0.18f), new Vector3(W - 0.3f, 0.05f, 0.3f), post);
            Wall(hide, "Side L", new Vector3(-W * 0.5f, H * 0.5f, 0f), new Vector3(t, H, D), wood);
            Wall(hide, "Side R", new Vector3(W * 0.5f, H * 0.5f, 0f), new Vector3(t, H, D), wood);
            // Back wall with a doorway on the track side.
            Wall(hide, "Back L", new Vector3(-W * 0.5f + 0.75f, H * 0.5f, -D * 0.5f), new Vector3(1.5f, H, t), wood);
            Wall(hide, "Back R", new Vector3(W * 0.5f - 0.6f, H * 0.5f, -D * 0.5f), new Vector3(1.2f, H, t), wood);
            Wall(hide, "Lintel", new Vector3(0.15f, H - 0.15f, -D * 0.5f), new Vector3(0.75f, 0.3f, t), wood);
            Wall(hide, "Roof", new Vector3(0f, H + 0.06f, 0f), new Vector3(W + 0.4f, 0.1f, D + 0.5f), post);
            PropBuilder.Box("Bench", hide, new Vector3(0f, 0.45f, D * 0.5f - 0.55f), new Vector3(W - 0.6f, 0.07f, 0.35f), wood);
            var blind = hide.gameObject.AddComponent<ObservationBlind>();
            blind.interiorCentre = new Vector3(0f, 1f, 0f);
            blind.interiorSize = new Vector3(W - 0.2f, 2f, D - 0.2f);

            Plank(group, "MEADOW BLIND\nEnter quietly. Inside, wildlife can't see you", TerrainUtil.Ground(pos - f * (D + 2.5f) + Vector3.Cross(Vector3.up, f) * 2f),
                Quaternion.LookRotation(-f), wood, post, 1.6f);
            AddViewpoint(group, "Meadow Blind", pos + Vector3.up * 1.6f + f * (D * 0.5f - 0.1f), s.centre + Vector3.up * 0.8f);
            ClearCone(pos, f, 40f, s.radius * 1.8f);
            AddHotspot(group, "Meadow Hotspot", HotspotType.Meadow, s.centre, s.radius * 0.7f);
            AddHotspot(group, "Brush Hotspot", HotspotType.BrushPile, TerrainUtil.Ground(s.centre + Vector3.Cross(Vector3.up, f) * s.radius * 0.6f), 8f);
            dest.wildlifeCentre = s.centre;
            AddKeepOut(pos, 4f);
        }

        private static void Wall(Transform parent, string name, Vector3 centre, Vector3 size, Material mat)
        {
            PropBuilder.Box(name, parent, centre, size, mat);
            PropBuilder.AddCollider(parent, centre, size);
        }
    }
}
