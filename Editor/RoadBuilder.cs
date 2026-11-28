using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Trail hierarchy (0.9). Riding showed that every path was sized and shaped for walking. Now:
    ///   Reserve Road (primary)   3.4 m gravel, smooth and gently graded, a ring from the trailhead through every
    ///                            destination's pull-off and back. The bike's full (still gentle) speed is for here.
    ///   Footpaths (secondary)    the Meadow Loop and the narrow paths from the loop into each destination: walking
    ///                            first; the bike may use them, slowly.
    /// The road stays outside the loop (the loop is the reserve's intimate core) and never enters a destination: you
    /// park at the pull-off and walk in.
    /// </summary>
    public partial class VegetationBuilder
    {
        private readonly List<Vector3> _destinationPads = new List<Vector3>();
        public float RoadLength { get { float l = 0f; foreach (var t in _tracks) if (t.kind == TrackKind.Road) l += t.Length; return l; } }

        /// <summary>Distance beyond the edge of the nearest road, offset so the trail-width painting rules apply.</summary>
        private float RoadDistance(Vector3 p)
        {
            float half = _s.trailWidth * 0.5f, d = float.MaxValue;
            foreach (var t in _tracks) if (t.kind == TrackKind.Road) d = Mathf.Min(d, t.DistanceTo(p) - (t.Width * 0.5f - half));
            return d;
        }

        private bool HasRoads { get { foreach (var t in _tracks) if (t.kind == TrackKind.Road) return true; return false; } }

        public const float RoadWidth = 4.2f; // 1.0: room to ride at an easy pace and pass a walker

        public void PlanRoads(Transform parent, bool meshes, bool grade = true)
        {
            if (_destinationPads.Count == 0) return;
            Vector3 trailhead = _trail.GetPoint(12f);
            Vector3 start = PropBuilder.BikeParking != Vector3.zero ? PropBuilder.BikeParking
                : TerrainUtil.Ground(trailhead + TerrainUtil.Flat(trailhead - _loop.center).normalized * 3f);
            var stops = new List<Vector3>(_destinationPads);
            float startBearing = Bearing(start);
            stops.Sort((a, b) => Mathf.Repeat(Bearing(a) - startBearing, 360f).CompareTo(Mathf.Repeat(Bearing(b) - startBearing, 360f)));
            var ring = new List<Vector3> { start };
            ring.AddRange(stops);
            ring.Add(start);

            var gravel = meshes ? GeneratedAssets.Material("RoadGravel", new Color(0.62f, 0.57f, 0.5f), 0.05f) : null;
            for (int i = 1; i < ring.Count; i++)
            {
                var path = RoadPath(ring[i - 1], ring[i]);
                if (path == null) continue;
                path = Resample(Chaikin(Chaikin(Chaikin(path))), 4f);
                Relax(path, 10);                 // 1.0: long, even curves instead of grid wiggles
                path = Resample(path, 3f);
                for (int k = 0; k < path.Count; k++) path[k] = TerrainUtil.Ground(path[k]);
                var go = new GameObject($"Reserve Road {i}");
                go.transform.SetParent(parent, false);
                var road = go.AddComponent<ReserveTrack>();
                road.title = "Reserve Road";
                road.kind = TrackKind.Road;
                road.SetPath(path, RoadWidth);
                _tracks.Add(road);
                if (meshes)
                {
                    var mesh = GeneratedAssets.Save(ProceduralMeshes.TrailRibbon(path, RoadWidth, 0.8f, closed: false), "Mesh_Road_" + i);
                    var surface = new GameObject("Road Surface");
                    surface.transform.SetParent(go.transform, false);
                    surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = surface.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = gravel;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            if (grade) GradeRoads();

            // Where the road leaves the trailhead: what it is and how to use it.
            var first = _tracks.Find(t => t.kind == TrackKind.Road);
            if (first != null && first.Points.Count > 6)
            {
                var wood = GeneratedAssets.Material("Wood", new Color(0.52f, 0.39f, 0.26f));
                var post = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
                Vector3 a = first.Points[3], dir = TerrainUtil.Flat(first.Points[5] - first.Points[1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                Vector3 pos = TerrainUtil.Ground(a + side * 3f);
                Plank(parent, "RESERVE ROAD", "ride slowly · all destinations", new[] { SignIcon.Bike }, 0, pos, Quaternion.LookRotation(-dir), post);
                AddKeepOut(pos, 1.2f);
            }
        }

        /// <summary>Laplacian smoothing that keeps both ends fixed: removes kinks while following the same route.</summary>
        private static void Relax(List<Vector3> p, int iterations)
        {
            for (int it = 0; it < iterations; it++)
                for (int i = 1; i < p.Count - 1; i++)
                    p[i] = Vector3.Lerp(p[i], (p[i - 1] + p[i + 1]) * 0.5f, 0.5f);
        }

        /// <summary>
        /// Cut and fill (1.0): the road bed follows a smoothed version of the ground along it, so riding is calm instead
        /// of bucking over every bump. Flat across the road, blending into the land over ~3 m either side. Skipped at
        /// creek crossings (the bridge spans the channel). Converges on rebuild rather than drifting.
        /// </summary>
        private void GradeRoads()
        {
            var data = _terrain.terrainData;
            Vector3 tPos = _terrain.transform.position, size = data.size;
            int res = data.heightmapResolution;
            float cx = size.x / (res - 1), cz = size.z / (res - 1);
            float half = RoadWidth * 0.5f, reach = half + 3.2f;
            var best = new Dictionary<int, (float d, float h)>();
            foreach (var road in _tracks)
            {
                if (road.kind != TrackKind.Road) continue;
                var pts = road.Points;
                int n = pts.Count;
                var raw = new float[n];
                for (int i = 0; i < n; i++) raw[i] = TerrainUtil.SampleHeight(pts[i]);
                var profile = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float sum = 0f; int cnt = 0;
                    for (int k = Mathf.Max(0, i - 5); k <= Mathf.Min(n - 1, i + 5); k++) { sum += raw[k]; cnt++; } // ±15 m
                    profile[i] = sum / cnt;
                }
                for (int i = 1; i < n; i++)
                {
                    Vector3 a = pts[i - 1], b = pts[i];
                    int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach - tPos.x) / cx), 0, res - 1);
                    int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach - tPos.x) / cx), 0, res - 1);
                    int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z, b.z) - reach - tPos.z) / cz), 0, res - 1);
                    int z1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z, b.z) + reach - tPos.z) / cz), 0, res - 1);
                    Vector2 a2 = new Vector2(a.x, a.z), ab = new Vector2(b.x, b.z) - a2;
                    for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 q = new Vector2(tPos.x + x * cx, tPos.z + z * cz);
                        float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(q - a2, ab) / ab.sqrMagnitude) : 0f;
                        float d = Vector2.Distance(q, a2 + ab * t);
                        if (d > reach) continue;
                        int key = z * res + x;
                        if (best.TryGetValue(key, out var cur) && cur.d <= d) continue;
                        best[key] = (d, Mathf.Lerp(profile[i - 1], profile[i], t));
                    }
                }
            }
            if (best.Count == 0) return;
            if (_s.backupTerrainData && !IsOwnTerrain()) BackupTerrainData(data);
            var heights = data.GetHeights(0, 0, res, res);
            foreach (var kv in best)
            {
                int x = kv.Key % res, z = kv.Key / res;
                var p = new Vector3(tPos.x + x * cx, 0f, tPos.z + z * cz);
                if (CreekDistance(p) < 7f) continue;
                float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half + 0.6f, reach, kv.Value.d));
                float h = tPos.y + heights[z, x] * size.y;
                heights[z, x] = Mathf.Clamp01((Mathf.Lerp(h, kv.Value.h, w) - tPos.y) / size.y);
            }
            data.SetHeights(0, 0, heights);
            foreach (var road in _tracks)
            {
                if (road.kind != TrackKind.Road) continue;
                var pts = new List<Vector3>(road.Points);
                for (int i = 0; i < pts.Count; i++) pts[i] = TerrainUtil.Ground(pts[i]);
                road.SetPath(pts, road.Width);
            }
        }

        /// <summary>Low edge posts every ~60 m, alternating sides: the road reads at a glance, even at dusk.</summary>
        private void BuildRoadMarkers(Transform parent)
        {
            var white = GeneratedAssets.Material("RoadPost", new Color(0.86f, 0.84f, 0.78f), 0.3f);
            var band = PropBuilder.SignTrim;
            int side = 1;
            foreach (var road in _tracks)
            {
                if (road.kind != TrackKind.Road) continue;
                var pts = road.Points;
                float acc = 0f;
                for (int i = 1; i < pts.Count - 1; i++)
                {
                    acc += Vector3.Distance(pts[i - 1], pts[i]);
                    if (acc < 60f) continue;
                    acc = 0f;
                    Vector3 dir = TerrainUtil.Flat(pts[i + 1] - pts[i - 1]).normalized, s = Vector3.Cross(Vector3.up, dir) * side;
                    side = -side;
                    Vector3 p = TerrainUtil.Ground(pts[i] + s * (RoadWidth * 0.5f + 0.6f));
                    if (CreekDistance(p) < 6f || PadDistance(p) < 2f || InKeepOut(p)) continue;
                    var g = PropBuilder.Group("Road Post", parent, p, Quaternion.LookRotation(dir));
                    PropBuilder.Box("Post", g, new Vector3(0f, 0.45f, 0f), new Vector3(0.1f, 0.9f, 0.1f), white);
                    PropBuilder.Box("Band", g, new Vector3(0f, 0.75f, 0f), new Vector3(0.105f, 0.08f, 0.105f), band);
                }
            }
        }

        /// <summary>A* for the road: strongly prefers gentle grades, stays outside the loop, crosses the creek once.</summary>
        private List<Vector3> RoadPath(Vector3 from, Vector3 to)
        {
            const float cell = 5f;
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            int w = Mathf.FloorToInt(size.x / cell), h = Mathf.FloorToInt(size.z / cell);
            Vector2Int Node(Vector3 p) => new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((p.x - tPos.x) / cell), 2, w - 3),
                                                         Mathf.Clamp(Mathf.RoundToInt((p.z - tPos.z) / cell), 2, h - 3));
            Vector3 World(Vector2Int n) => new Vector3(tPos.x + n.x * cell, 0f, tPos.z + n.y * cell);
            var cache = new Dictionary<Vector2Int, float>();
            float Cost(Vector2Int n)
            {
                if (cache.TryGetValue(n, out float v)) return v;
                Vector3 p = World(n);
                float slope = TerrainUtil.SlopeDegrees(p);
                v = 1f + (slope / 9f) * (slope / 9f);
                if (slope > 20f) v += 60f;
                if (LoopRadius(p) < 1.08f) v += 8f;
                if (CreekDistance(p) < 6f) v += 20f;
                foreach (var s in _sites)
                {
                    float d = TerrainUtil.FlatDistance(p, s.centre);
                    if (s.kind == DestinationKind.RidgeOverlook && d < 20f) v += 40f;
                    if (s.kind == DestinationKind.ObservationBlind && d < s.radius) v += 40f;
                    if (s.kind == DestinationKind.Wetland && d < s.water * 1.35f + 6f) v += 200f;
                    if (s.kind == DestinationKind.OldGrowth && d < 14f) v += 30f;
                }
                cache[n] = v;
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
            while (open.Count > 0 && guard++ < 250000)
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
                    if (n.x < 2 || n.y < 2 || n.x > w - 3 || n.y > h - 3) continue;
                    float ng = gc + (d.x != 0 && d.y != 0 ? 1.414f : 1f) * (Cost(n) + Cost(cur)) * 0.5f;
                    if (g.TryGetValue(n, out float old) && old <= ng) continue;
                    if (g.ContainsKey(n)) open.Remove((old + Vector2Int.Distance(n, b), Id(n)));
                    g[n] = ng;
                    came[n] = cur;
                    open.Add((ng + Vector2Int.Distance(n, b), Id(n)));
                }
            }
            return null;
        }
    }
}
