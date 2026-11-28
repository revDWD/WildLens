using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Reserve geography (1.9; part of <see cref="VegetationBuilder"/>): Hemlock Ravine, a steep, shaded side valley cut by a
    /// small tributary (Hemlock Run) that joins Willow Creek, with a stepped cascade at its head and one footpath in; and the
    /// terrain work for four more hidden places (Editor/HiddenPlacesBuilder.cs).
    ///
    /// Why it runs late (after every scenery pass that draws from the shared random sequence, before small life and painting):
    /// the reserve that already exists for a seed must not move. Every earlier pass decides what to place from the terrain
    /// (slopes, paths, the creek); carving first would change those decisions and, through the shared random sequence, every tree
    /// after the first one affected. So the ground is cut here, and what already stood where the ground moved is settled onto
    /// the new surface (posts, rocks and anchors follow the ground; vegetation in the new channel, the new footpath, the pond's
    /// water or inside the crag is lifted out). The ravine's own scenery then comes from a private random stream. Terrain paint,
    /// terrain grass, small life and the grounding pass all run afterwards, on the final ground.
    ///
    /// Where it goes (prototyped and measured in Python before writing this; see the 1.9 report): a reverse Dijkstra from Willow
    /// Creek, costed the way water runs, gives every cell its way down to the creek. Heads on ground at least 8 m above the creek,
    /// 40 m from paths, are routed and scored; the run below the cascade keeps a gentle, even gradient from the mouth (a
    /// knickpoint ravine: it cuts deeper the further upstream it goes), and the cascade is the drop from the upper channel to it,
    /// in two or three steps over ledges into a pool. The best of up to ten routes is kept if it is dramatic enough (cascade plus
    /// depth); otherwise there is no ravine and one Console line says why. The carve never touches a path, road, destination,
    /// keep-out or hidden place (checked against its full width), and only cuts (min), so rebuilding gives the same ravine.
    /// </summary>
    public partial class VegetationBuilder
    {
        // ------------------------------------------------------------------ tuning (the prototype's numbers)
        private const float RunSpacing = 2f;
        private const float RavineFloor = 3.4f;   // floor half-width: channel plus a bench for the footpath
        private const float WallPower = 1.4f;     // wall profile: steepest at the rim
        private const float WallRun = 1.35f;      // horizontal metres of wall per metre of depth
        private const float WallMax = 17f;
        private const float BankSample = 20f;     // banks read outside the carve, so a rebuild never digs deeper
        private const float RimExtend = 8f;
        private const float DrainCell = 5f;

        private bool _trackMeshes = true;
        private readonly List<Vector3> _run = new List<Vector3>();
        private float[] _runBed, _runWater, _runBankL, _runBankR, _runRise;
        private int _runMouth = -1;
        private RunCascade _cascade;
        private readonly List<RunCrossing> _runCrossings = new List<RunCrossing>();
        private ReserveTrack _ravinePath;
        private int _ravineViewIndex = -1;
        private Vector3 _ravineView;
        private readonly List<(Vector3 p, float r)> _newHidden = new List<(Vector3, float)>();
        private int _settledMoved, _settledRemoved;
        private float _ravineDepth;

        private class RunCascade
        {
            public float height, foot, poolWater;
            public int[] lips, pool;
        }

        private struct RunCrossing
        {
            public int index;
            public Vector3 at, dir;
            public float width;
        }

        // ================================================================== entry

        /// <summary>
        /// 1.9: the ravine, its footpath and four hidden places. Call after every scenery pass that uses the shared random
        /// sequence (trees, rocks, logs, bushes, hotspots, viewpoints, destinations, ecosystem, discoveries, undergrowth) and
        /// before small life, terrain paint and grass.
        /// </summary>
        public void BuildGeography(Transform reserve)
        {
            if (reserve == null || _sites.Count == 0 || (!_s.hemlockRavine && !_s.moreHiddenPlaces)) return;
            var shared = _rng;
            _rng = new System.Random(_s.seed * 7919 + 1901); // a private stream: nothing built before or after moves
            try { BuildGeographyNow(reserve); }
            catch (System.Exception e) { Debug.LogError("[WildLens] Reserve geography stopped part-way (rebuild to retry): " + e); }
            finally { _rng = shared; }
        }

        private void BuildGeographyNow(Transform reserve)
        {
            var notes = new List<string>();
            var data = _terrain.terrainData;
            int res = data.heightmapResolution;
            bool shape = _s.shapeTerrainAtDestinations;
            if (shape)
            {
                if (_s.backupTerrainData && !IsOwnTerrain()) BackupTerrainData(data);
                Undo.RegisterCompleteObjectUndo(data, "Shape WildLens reserve geography");
            }
            var before = data.GetHeights(0, 0, res, res);

            bool ravine = false;
            if (!_s.hemlockRavine) { }
            else if (!shape) notes.Add("no Hemlock Ravine ('Shape terrain at destinations' is off)");
            else if (_creek.Count < 40 || _creekBed == null) notes.Add("no Hemlock Ravine (no Willow Creek to join)");
            else ravine = PlanRavine(notes);
            if (ravine) CarveRavine();

            Vector3? overlook = null;
            if (ravine && _cascade != null && _s.moreHiddenPlaces) overlook = PlanCascadeOverlook(notes);
            if (ravine) PlanRavinePath(reserve, notes);

            HiddenPondPlan pond = null;
            ShelterPlan shelter = null;
            Vector3? cabin = null;
            if (_s.moreHiddenPlaces)
            {
                if (shape) { pond = PlanHiddenPond(notes); shelter = PlanRockShelter(notes); }
                else notes.Add("no Hidden Pond or Rock Shelter ('Shape terrain at destinations' is off)");
                cabin = PlanCabin(notes);
            }

            var after = data.GetHeights(0, 0, res, res);
            SettleScenery(reserve, before, after, pond, shelter);
            if (_run.Count > 1) BuildWaterField(_run); // later passes (paint, grass, small life) see the run as water

            if (ravine) BuildRavine(reserve);
            var hidden = reserve.Find("Hidden Places");
            if (hidden == null)
            {
                hidden = new GameObject("Hidden Places").transform;
                hidden.SetParent(reserve, false);
            }
            if (overlook.HasValue) BuildCascadeOverlook(hidden, overlook.Value);
            if (pond != null) BuildHiddenPond(hidden, pond);
            if (shelter != null) BuildRockShelter(hidden, shelter, notes);
            if (cabin.HasValue) BuildCabinRuins(hidden, cabin.Value);

            string ravineText = ravine
                ? $"Hemlock Ravine {RunLength():0} m (cascade {(_cascade != null ? _cascade.height : 0f):0.0} m in {(_cascade != null ? _cascade.lips.Length : 0)} drops, up to {_ravineDepth:0.0} m deep, footpath {(_ravinePath != null ? _ravinePath.Length : 0f):0} m{(_runCrossings.Count > 0 ? $", {_runCrossings.Count} footbridge(s) over the run" : "")})"
                : "no ravine";
            Debug.Log($"[WildLens] Reserve: {ravineText}, hidden places {DiscoveriesPlaced} of 7; settled {_settledMoved} objects onto the new ground, removed {_settledRemoved}." +
                      (notes.Count > 0 ? " Notes: " + string.Join("; ", notes) + "." : ""));
        }

        private float RunLength()
        {
            float l = 0f;
            for (int i = 1; i < _run.Count; i++) l += TerrainUtil.FlatDistance(_run[i - 1], _run[i]);
            return l;
        }

        // ================================================================== shared queries

        /// <summary>Distance to the run's centre line, with the segment, position along it and side (+1 = its right).</summary>
        private float RunDistance(Vector3 p, out int segment, out float t, out float side)
        {
            segment = 0; t = 0f; side = 1f;
            float best = float.MaxValue;
            for (int i = 1; i < _run.Count; i++)
            {
                Vector3 a = _run[i - 1], b = _run[i];
                if (Mathf.Abs(p.x - a.x) > 40f && Mathf.Abs(p.x - b.x) > 40f) continue;
                if (Mathf.Abs(p.z - a.z) > 40f && Mathf.Abs(p.z - b.z) > 40f) continue;
                Vector2 q = new Vector2(p.x, p.z), a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z), ab = b2 - a2;
                float tt = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(q - a2, ab) / ab.sqrMagnitude) : 0f;
                float d = Vector2.Distance(q, a2 + ab * tt);
                if (d < best)
                {
                    best = d; segment = i - 1; t = tt;
                    // Unity's side = Cross(up, dir) = (dir.z, -dir.x): positive dot means the run's right.
                    side = (q.x - a2.x) * ab.y - (q.y - a2.y) * ab.x > 0f ? 1f : -1f;
                }
            }
            return best;
        }

        private float RunDistance(Vector3 p) => _run.Count < 2 ? float.MaxValue : RunDistance(p, out _, out _, out _);

        /// <summary>Nearest water: Willow Creek or Hemlock Run.</summary>
        private float WaterDistance(Vector3 p) => Mathf.Min(CreekDistance(p), RunDistance(p));

        private bool NearHiddenPlace(Vector3 p, float extra)
        {
            foreach (var d in _discoveryPoints) if (TerrainUtil.FlatDistance(p, d.p) < d.r + extra) return true;
            foreach (var d in _newHidden) if (TerrainUtil.FlatDistance(p, d.p) < d.r + extra) return true;
            return false;
        }

        private bool NearSite(Vector3 p, float extra)
        {
            foreach (var s in _sites) if (TerrainUtil.FlatDistance(p, s.centre) < s.radius + extra + s.water * 0.35f) return true;
            return false;
        }

        private bool InsideWorld(Vector3 p, float inset = 45f)
        {
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            return p.x >= tPos.x + inset && p.x <= tPos.x + size.x - inset && p.z >= tPos.z + inset && p.z <= tPos.z + size.z - inset;
        }

        private static Vector3 Side(IReadOnlyList<Vector3> pts, int i, out Vector3 dir)
        {
            int n = pts.Count;
            dir = TerrainUtil.Flat(pts[Mathf.Min(n - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
            return Vector3.Cross(Vector3.up, dir);
        }

        /// <summary>A small binary heap (Unity 2021 has no PriorityQueue); stale entries are skipped by the caller.</summary>
        private class MinHeap
        {
            private readonly List<(float k, int v)> _a = new List<(float, int)>();
            public int Count => _a.Count;
            public void Push(float k, int v)
            {
                _a.Add((k, v));
                int i = _a.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (_a[p].k <= _a[i].k) break;
                    (_a[p], _a[i]) = (_a[i], _a[p]);
                    i = p;
                }
            }
            public (float k, int v) Pop()
            {
                var top = _a[0];
                int last = _a.Count - 1;
                _a[0] = _a[last];
                _a.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < _a.Count && _a[l].k < _a[m].k) m = l;
                    if (r < _a.Count && _a[r].k < _a[m].k) m = r;
                    if (m == i) break;
                    (_a[m], _a[i]) = (_a[i], _a[m]);
                    i = m;
                }
                return top;
            }
        }

        // ================================================================== ravine: where

        /// <summary>The drainage field: every 5 m cell's cheapest way down to Willow Creek, costed the way water runs.</summary>
        private class Drain
        {
            public int n;
            public Vector3 origin;
            public float[] dist, height, slope;
            public int[] to, mouth;
            public Vector3 World(int id) => origin + new Vector3((id % n) * DrainCell, 0f, (id / n) * DrainCell);
        }

        private Drain BuildDrainage()
        {
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            int n = Mathf.FloorToInt(size.x / DrainCell);
            var d = new Drain { n = n, origin = new Vector3(tPos.x, 0f, tPos.z) };
            int count = n * n;
            d.dist = new float[count]; d.height = new float[count]; d.slope = new float[count];
            d.to = new int[count]; d.mouth = new int[count];
            var blocked = new bool[count];
            var extra = new float[count];
            var seg = new int[count];
            var near = new bool[count];

            // Mouths: not near the falls, the ford, a bridge or a hidden place, and not in the first or last stretch.
            int cn = _creek.Count;
            var okIndex = new bool[cn];
            var bridges = new List<Vector3>();
            void Bridges(IReadOnlyList<Vector3> path, bool closed)
            {
                int m = closed ? path.Count : path.Count - 1;
                for (int a = 0; a < m; a++)
                    for (int k = 1; k < cn; k++)
                        if (SegmentsIntersect(path[a], path[(a + 1) % path.Count], _creek[k - 1], _creek[k], out Vector3 hit)) bridges.Add(hit);
            }
            Bridges(_trail.Points, true);
            foreach (var t in _tracks) Bridges(t.Points, false);
            for (int i = 0; i < cn; i++)
            {
                bool ok = i >= cn * 0.05f && i <= cn * 0.97f;
                if (_fallsIndex >= 0 && Mathf.Abs(i - _fallsIndex) * 2.5f < 45f) ok = false;
                if (_hasFord && TerrainUtil.FlatDistance(_creek[i], _fordPoint) < 50f) ok = false;
                foreach (var b in bridges) if (TerrainUtil.FlatDistance(_creek[i], b) < 25f) ok = false;
                foreach (var h in _discoveryPoints) if (TerrainUtil.FlatDistance(_creek[i], h.p) < 35f) ok = false;
                okIndex[i] = ok;
            }

            var heap = new MinHeap();
            for (int id = 0; id < count; id++)
            {
                Vector3 p = d.World(id);
                int x = id % n, z = id / n;
                d.height[id] = TerrainUtil.SampleHeight(p);
                d.slope[id] = TerrainUtil.SlopeDegrees(p);
                d.dist[id] = float.MaxValue; d.to[id] = -1; d.mouth[id] = -1;
                if (x < 2 || z < 2 || x > n - 3 || z > n - 3) { blocked[id] = true; continue; }
                bool b = (HasRoads && RoadDistance(p) < 8f) || InKeepOut(p) || LoopRadius(p) < 1.15f || NearSite(p, 10f) || NearHiddenPlace(p, 25f);
                blocked[id] = b;
                if (b) continue;
                float e = (d.slope[id] / 14f) * (d.slope[id] / 14f);
                float fp = PathDistance(p); // footpaths may be crossed (a footbridge), at a price
                if (fp < 8f) e += 25f;
                if (fp < 30f) e += 3f;
                float cd = CreekDistanceExact(p, out int s, out _);
                if (cd < 14f && cd >= 4f) e += 15f; // into the creek, not alongside it
                extra[id] = e;
                seg[id] = s;
                near[id] = cd < 3.6f;
            }
            for (int id = 0; id < count; id++)
            {
                if (blocked[id] || !near[id] || !okIndex[seg[id]]) continue;
                d.dist[id] = 0f; d.mouth[id] = seg[id];
                heap.Push(0f, id);
            }
            int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
            while (heap.Count > 0)
            {
                var (k, v) = heap.Pop();
                if (k > d.dist[v]) continue;
                int vx = v % n, vz = v / n;
                for (int q = 0; q < 8; q++)
                {
                    int ux = vx + dx[q], uz = vz + dz[q];
                    if (ux < 2 || uz < 2 || ux > n - 3 || uz > n - 3) continue;
                    int u = uz * n + ux;
                    if (blocked[u]) continue;
                    float len = q >= 4 ? 1.414f : 1f;
                    // Water runs u -> v: cheap downhill, expensive uphill.
                    float step = len * (1f + extra[v]) + Mathf.Max(0f, d.height[v] - d.height[u]) * 2.5f;
                    float nd = k + step;
                    if (nd >= d.dist[u]) continue;
                    d.dist[u] = nd; d.to[u] = v; d.mouth[u] = d.mouth[v];
                    heap.Push(nd, u);
                }
            }
            return d;
        }

        private static List<Vector3> DrainRoute(Drain d, int id)
        {
            var pts = new List<Vector3>();
            for (int guard = 0; guard < 400 && id >= 0; guard++)
            {
                pts.Add(d.World(id));
                id = d.to[id];
            }
            return pts;
        }

        private bool PlanRavine(List<string> notes)
        {
            var d = BuildDrainage();
            int n = d.n;
            var cands = new List<(float score, int id)>();
            for (int id = 0; id < n * n; id++)
            {
                if (d.dist[id] == float.MaxValue || d.mouth[id] < 0 || d.slope[id] > 25f) continue;
                Vector3 p = d.World(id);
                if (TerrainUtil.FlatDistance(p, _loop.center) > _s.reserveReach + 40f || !InsideWorld(p)) continue;
                float rel = d.height[id] - _creekWater[d.mouth[id]];
                if (rel < 8f) continue;
                p.y = d.height[id];
                if (PathDistance(p) < 40f || CreekDistance(p) < 45f || LoopRadius(p) < 1.25f) continue;
                if (NearSite(p, 30f) || NearHiddenPlace(p, 50f)) continue;
                float len = DrainRoute(d, id).Count * DrainCell;
                if (len < 100f || len > 420f) continue;
                cands.Add((Mathf.Min(rel, 22f) - 0.02f * Mathf.Abs(len - 190f) + Forest(p) * 3f, id));
            }
            cands.Sort((a, b) => b.score.CompareTo(a.score));

            var heads = new List<Vector3>();
            float bestDrama = float.MinValue;
            RavineCandidate best = null;
            int tried = 0;
            var rejects = new Dictionary<string, int>();
            foreach (var c in cands)
            {
                Vector3 p = d.World(c.id);
                bool close = false;
                foreach (var h in heads) if (TerrainUtil.FlatDistance(p, h) < 60f) close = true;
                if (close) continue;
                heads.Add(p);
                if (++tried > 10) break;
                var cand = TryRavine(d, c.id, out string why);
                if (cand == null) { rejects[why] = rejects.TryGetValue(why, out int k) ? k + 1 : 1; continue; }
                if (cand.drama > bestDrama) { bestDrama = cand.drama; best = cand; }
            }
            if (best == null || best.drama < 9f)
            {
                var why = new List<string>();
                foreach (var kv in rejects) why.Add($"{kv.Value}× {kv.Key}");
                notes.Add($"no Hemlock Ravine: the land doesn't fall far enough toward Willow Creek away from paths " +
                          $"({cands.Count} heads, {Mathf.Min(tried, 10)} routed{(best != null ? $", best only {best.drama:0.0} of 9 for drama" : "")}" +
                          $"{(why.Count > 0 ? "; " + string.Join(", ", why) : "")})");
                return false;
            }
            _run.Clear(); _run.AddRange(best.run);
            _runBed = best.bed; _runWater = best.water; _runBankL = best.bankL; _runBankR = best.bankR; _runRise = best.rise;
            _runMouth = best.mouth; _cascade = best.cascade;
            _runCrossings.Clear(); _runCrossings.AddRange(best.crossings);
            return true;
        }

        private class RavineCandidate
        {
            public List<Vector3> run;
            public float[] bed, water, bankL, bankR, rise;
            public int mouth;
            public RunCascade cascade;
            public List<RunCrossing> crossings;
            public float drama;
        }

        private RavineCandidate TryRavine(Drain d, int head, out string why)
        {
            why = null;
            var path = DrainRoute(d, head);
            int mouth = d.mouth[head];
            if (path.Count < 6 || mouth < 0) { why = "too short"; return null; }
            path[path.Count - 1] = new Vector3(_creek[mouth].x, 0f, _creek[mouth].z); // end on the creek's centre line
            path = Resample(Chaikin(Chaikin(path)), 4f);
            // A gentle meander on the private stream.
            float total = 0f;
            var along = new float[path.Count];
            for (int i = 1; i < path.Count; i++) along[i] = total += TerrainUtil.FlatDistance(path[i - 1], path[i]);
            float phase = R(0f, 6.28f), wavelength = R(22f, 30f);
            var wavy = new List<Vector3>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 side = Side(path, i, out _);
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Min(along[i], total - along[i]) / 20f);
                wavy.Add(path[i] + side * Mathf.Sin(along[i] / wavelength * Mathf.PI * 2f + phase) * 1.6f * fade);
            }
            var run = Resample(Chaikin(wavy), RunSpacing);
            int n = run.Count;
            if (n < 40) { why = "too short"; return null; }
            for (int i = 0; i < n; i++) run[i] = TerrainUtil.Ground(run[i]);

            // Banks 20 m out (outside the carve), the ground under the line, distance to the mouth.
            var bankL = new float[n]; var bankR = new float[n]; var low = new float[n]; var toMouth = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 side = Side(run, i, out _);
                bankR[i] = TerrainUtil.SampleHeight(run[i] + side * BankSample);
                bankL[i] = TerrainUtil.SampleHeight(run[i] - side * BankSample);
                low[i] = Mathf.Min(Mathf.Min(bankL[i], bankR[i]), run[i].y + 1.5f);
            }
            for (int i = n - 2; i >= 0; i--) toMouth[i] = toMouth[i + 1] + TerrainUtil.FlatDistance(run[i], run[i + 1]);
            float mouthBed = _creekBed[mouth] + 0.05f;

            // The upper channel: a shallow notch in the upland, never rising downstream.
            var up = new float[n];
            float prev = float.MaxValue;
            for (int i = 0; i < n; i++) { up[i] = Mathf.Min(low[i] - (0.8f + 0.6f * Mathf.Min(1f, i / 4f)), prev); prev = up[i]; }

            // The knickpoint: where the cascade drops from the upper channel to the run, and the run's gradient from the mouth.
            float bestScore = float.MinValue, bestG = 0f, bestH = 0f;
            int bestK = -1;
            var depths = new List<float>();
            for (int K = 4; K < (int)(n * 0.45f); K++)
            foreach (float hc in new[] { 3f, 4f, 5f, 6f, 7f })
            {
                float g = (up[K] - hc - mouthBed) / Mathf.Max(1f, toMouth[K]);
                if (g < 0.01f || g > 0.08f) continue;
                depths.Clear();
                float pen = 0f, over = 0f;
                for (int i = K + 8; i < n - 8; i++)
                {
                    float dd = low[i] - (mouthBed + g * toMouth[i]);
                    depths.Add(dd);
                    pen += Mathf.Max(0f, 1.6f - dd);
                    over += Mathf.Max(0f, dd - 10f);
                }
                if (depths.Count < 10) continue;
                depths.Sort();
                float median = depths[depths.Count / 2];
                float score = -(pen / depths.Count) * 6f - (over / depths.Count) * 3f - Mathf.Abs(median - 6f) * 0.6f + hc * 0.35f
                              - Mathf.Abs(K / (float)n - 0.2f) * 4f - Mathf.Max(0f, g - 0.05f) * 60f;
                if (score > bestScore) { bestScore = score; bestK = K; bestH = hc; bestG = g; }
            }
            if (bestK < 0) { why = "no knickpoint fits"; return null; }

            var bed = new float[n];
            for (int i = 0; i < n; i++)
            {
                float run2 = mouthBed + bestG * toMouth[i];
                bed[i] = i < bestK ? up[i] : Mathf.Min(run2, up[i]);
                bed[i] = Mathf.Min(bed[i], low[i] - 1.4f); // always a channel
                if (i > 0) bed[i] = Mathf.Min(bed[i], bed[i - 1]);
                bed[i] = Mathf.Max(bed[i], mouthBed);
            }
            var rise = new float[n];
            for (int i = 0; i < n; i++) rise[i] = 0.45f;
            var water = new float[n];
            for (int i = 0; i < n; i++) water[i] = bed[i] + 0.22f;
            var cascade = PlanRunCascade(bed, water, rise, bestK, up[bestK] - (mouthBed + bestG * toMouth[bestK]));

            var cand = new RavineCandidate { run = run, bed = bed, water = water, bankL = bankL, bankR = bankR, rise = rise, mouth = mouth, cascade = cascade };
            cand.crossings = FindRunCrossings(run);
            if (!ValidateRavine(cand, out why)) return null;

            // Drama: the cascade, the depth of the upper run, not too much shallow gully, no more crossings than needed.
            int a = cascade != null ? cascade.pool[cascade.pool.Length - 1] + 1 : 4;
            var runDepth = new List<float>();
            for (int i = a; i < n - 8; i++) runDepth.Add(Mathf.Min(bankL[i], bankR[i]) - bed[i]);
            if (runDepth.Count < 8) { why = "too short"; return null; }
            int upperCount = Mathf.Max(4, (int)(runDepth.Count * 0.6f));
            var upper = runDepth.GetRange(0, Mathf.Min(upperCount, runDepth.Count));
            upper.Sort();
            float maxDepth = 0f, shallow = 0f;
            foreach (float dd in runDepth) { maxDepth = Mathf.Max(maxDepth, dd); if (dd < 2f) shallow++; }
            cand.drama = (cascade != null ? cascade.height : 0f) + upper[upper.Count / 2] + 0.4f * maxDepth - 4f * shallow / runDepth.Count - 3f * cand.crossings.Count;
            return cand;
        }

        /// <summary>The knickpoint becomes two or three drops over ledges into a plunge pool (the bed is only lowered).</summary>
        private static RunCascade PlanRunCascade(float[] bed, float[] water, float[] rise, int K, float drop)
        {
            int n = bed.Length;
            if (drop < 2.4f) return null;
            float H = Mathf.Min(drop, 8f);
            int steps = H >= 4.5f ? 3 : 2;
            float[] frac = steps == 3 ? new[] { 0.42f, 0.33f, 0.25f } : new[] { 0.58f, 0.42f };
            int L = Mathf.Max(2, K - 3 * steps); // the drops end at the knickpoint
            if (L + 3 * (steps - 1) + 4 >= n - 1) return null;
            var lips = new int[steps];
            for (int k = 0; k < steps; k++) lips[k] = L + 3 * k;
            float top = bed[L], acc = 0f;
            for (int k = 0; k < steps; k++)
            {
                acc += H * frac[k];
                bool last = k + 1 == steps;
                int next = last ? lips[k] + 1 : lips[k + 1];
                for (int j = lips[k] + 1; j <= next && j < n; j++)
                {
                    bed[j] = Mathf.Min(bed[j], top - acc - (!last && j < next ? 0.15f : 0f));
                    if (!last) { water[j] = top - acc + 0.05f; rise[j] = 0.45f; }
                }
            }
            float foot = top - H;
            int f0 = lips[steps - 1] + 1;
            var pool = new List<int>();
            for (int j = f0; j < Mathf.Min(n - 1, f0 + 3); j++) pool.Add(j);
            if (pool.Count == 0) return null;
            foreach (int j in pool) { bed[j] = Mathf.Min(bed[j], foot - 0.5f); water[j] = foot + 0.15f; rise[j] = 0.95f; }
            for (int i = 1; i < n; i++)
            {
                if (pool.Contains(i)) continue;
                bed[i] = pool.Contains(i - 1) ? Mathf.Min(bed[i], foot) : Mathf.Min(bed[i], bed[i - 1]);
                bool ledge = false;
                for (int k = 0; k + 1 < steps; k++) if (i > lips[k] && i <= lips[k + 1]) ledge = true;
                if (!ledge) water[i] = bed[i] + 0.22f;
            }
            return new RunCascade { height = H, foot = foot, poolWater = foot + 0.15f, lips = lips, pool = pool.ToArray() };
        }

        private List<RunCrossing> FindRunCrossings(List<Vector3> run)
        {
            var list = new List<RunCrossing>();
            foreach (var t in _tracks)
            {
                if (t.kind != TrackKind.Footpath) continue;
                var p = t.Points;
                for (int a = 1; a < p.Count; a++)
                    for (int k = 1; k < run.Count; k++)
                    {
                        if (!SegmentsIntersect(p[a - 1], p[a], run[k - 1], run[k], out Vector3 hit)) continue;
                        bool dup = false;
                        foreach (var c in list) if (TerrainUtil.FlatDistance(c.at, hit) < 12f) dup = true;
                        if (!dup) list.Add(new RunCrossing { index = k, at = hit, dir = TerrainUtil.Flat(p[a] - p[a - 1]).normalized, width = t.Width });
                    }
            }
            return list;
        }

        private static bool NearCrossing(List<RunCrossing> crossings, int i, int span)
        {
            foreach (var c in crossings) if (Mathf.Abs(i - c.index) <= span) return true;
            return false;
        }

        /// <summary>Wall width and the carve's full reach at a sample (for clearance checks).</summary>
        private static float RavineReach(float bankHigh, float bed) =>
            Mathf.Clamp(RavineFloor + WallRun * (bankHigh - bed), 6f, WallMax) + RimExtend;

        private bool ValidateRavine(RavineCandidate c, out string why)
        {
            why = null;
            int n = c.run.Count;
            foreach (var x in c.crossings)
            {
                float deep = 0f;
                for (int i = Mathf.Max(0, x.index - 2); i <= Mathf.Min(n - 1, x.index + 2); i++) deep = Mathf.Max(deep, Mathf.Min(c.bankL[i], c.bankR[i]) - c.bed[i]);
                if (deep > 2.6f) { why = "a footpath crossing too deep for a footbridge"; return false; }
            }
            if (c.crossings.Count > 2) { why = "too many footpath crossings"; return false; }
            Vector3 fallsLip = _fallsIndex >= 0 ? _creek[_fallsIndex] : Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = c.run[i];
                bool narrow = i > n - 8 || NearCrossing(c.crossings, i, 10); // the mouth and crossings are a narrow channel only
                float r = narrow ? 6.5f : RavineReach(Mathf.Max(c.bankL[i], c.bankR[i]), c.bed[i]);
                if (!NearCrossing(c.crossings, i, 6) && PathDistance(p) < r + 3f) { why = "too close to a path"; return false; }
                if (HasRoads && RoadDistance(p) < r + 3f) { why = "too close to the road"; return false; }
                foreach (var k in _keepOut) if (TerrainUtil.FlatDistance(p, k.p) < r + k.r) { why = "a structure in the way"; return false; }
                if (NearSite(p, r + 5f)) { why = "a destination in the way"; return false; }
                if (NearHiddenPlace(p, r + 10f)) { why = "a hidden place in the way"; return false; }
                if (_fallsIndex >= 0 && TerrainUtil.FlatDistance(p, fallsLip) < r + 25f) { why = "Willow Falls in the way"; return false; }
            }
            return true;
        }

        // ================================================================== ravine: carve

        /// <summary>Carve target across the ravine at distance d from the run, on one side (bankSide = that side's ground).</summary>
        private static float RavineProfile(float d, float bed, float bankSide, float rise, bool narrow, out float reach)
        {
            if (narrow) bankSide = Mathf.Min(bankSide, bed + 1.3f);
            float D = Mathf.Max(0.8f, bankSide - bed);
            float W = Mathf.Clamp(RavineFloor + WallRun * D, 6f, WallMax);
            reach = narrow ? Mathf.Min(W + RimExtend, 5.5f) : W + RimExtend;
            if (d < 1.3f) return bed;
            if (d < 2.3f) return bed + rise * Mathf.SmoothStep(0f, 1f, (d - 1.3f) / 1f);
            if (d < RavineFloor) return bed + rise + 0.25f * (d - 2.3f) / (RavineFloor - 2.3f);
            float baseH = bed + rise + 0.25f;
            if (d <= W)
            {
                float u = (d - RavineFloor) / (W - RavineFloor);
                return baseH + (bankSide - baseH) * Mathf.Pow(Mathf.Clamp01(u), WallPower);
            }
            float rim = Mathf.Min(1.2f, Mathf.Max(0f, bankSide - baseH) * WallPower / (W - RavineFloor));
            return bankSide + rim * (d - W);
        }

        private void CarveRavine()
        {
            var data = _terrain.terrainData;
            Vector3 tPos = _terrain.transform.position, size = data.size;
            int res = data.heightmapResolution;
            float cx = size.x / (res - 1), cz = size.z / (res - 1);
            int n = _run.Count;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in _run) { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
            float outer = WallMax + RimExtend + 2f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - outer - tPos.x) / cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((max.x + outer - tPos.x) / cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((min.y - outer - tPos.z) / cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((max.y + outer - tPos.z) / cz), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var heights = data.GetHeights(x0, z0, w, h);
            _ravineDepth = 0f;
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector3(tPos.x + (x0 + x) * cx, 0f, tPos.z + (z0 + z) * cz);
                float d = RunDistance(p, out int seg, out float t, out float side);
                if (d > outer) continue;
                int next = Mathf.Min(seg + 1, n - 1);
                bool narrow = seg >= n - 9 || NearCrossing(_runCrossings, seg, 6);
                // Willow Creek keeps its own banks: only the run's channel cuts through them at the mouth.
                if (d > 2.3f && CreekDistance(p) < 4.6f) continue;
                float bed = Mathf.Lerp(_runBed[seg], _runBed[next], t);
                float bank = side > 0f ? Mathf.Lerp(_runBankR[seg], _runBankR[next], t) : Mathf.Lerp(_runBankL[seg], _runBankL[next], t);
                float rise = Mathf.Lerp(_runRise[seg], _runRise[next], t);
                float target = RavineProfile(d, bed, bank, rise, narrow, out float reach);
                if (d > reach) continue;
                float cur = tPos.y + heights[z, x] * size.y;
                if (target < cur) heights[z, x] = Mathf.Clamp01((target - tPos.y) / size.y);
                if (!narrow) _ravineDepth = Mathf.Max(_ravineDepth, Mathf.Min(_runBankL[seg], _runBankR[seg]) - _runBed[seg]);
            }
            data.SetHeights(x0, z0, heights);
        }

        // ================================================================== ravine: the footpath

        /// <summary>
        /// One footpath in, from the nearest footpath, loop or pull-off to a spot on the ravine floor about 32 m below the cascade,
        /// looking up at it. Routed by the grade of each step (16 directions, so it can cross a ravine wall on a gentle diagonal),
        /// never through the run, never within 38 m of a hidden place; then the tread is benched into the slope (a smoothed profile,
        /// ±6 m) and, where it crosses Willow Creek, a footbridge spans it. Measured: max grade 9.5–11.5°, 95th percentile 4–6°.
        /// </summary>
        private void PlanRavinePath(Transform reserve, List<string> notes)
        {
            int n = _run.Count;
            int from = _cascade != null ? _cascade.pool[_cascade.pool.Length - 1] : 6;
            int j = n - 9;
            float acc = 0f;
            for (int k = from + 1; k < n - 8; k++)
            {
                acc += TerrainUtil.FlatDistance(_run[k - 1], _run[k]);
                if (acc >= 32f) { j = k; break; }
            }
            j = Mathf.Clamp(j, 1, n - 2);
            Vector3 side = Side(_run, j, out _);
            List<Vector3> best = null;
            float bestCost = float.MaxValue;
            Vector3 bestEnd = Vector3.zero;
            foreach (float sgn in new[] { 1f, -1f })
            {
                Vector3 end = TerrainUtil.Ground(_run[j] + side * sgn * 2.7f);
                var route = RouteRavinePath(end, out float cost);
                if (route != null && cost < bestCost) { bestCost = cost; best = route; bestEnd = end; }
            }
            if (best == null) { notes.Add("no footpath into Hemlock Ravine (no gentle way down): the ravine is unmarked"); return; }
            var path = Resample(Chaikin(Chaikin(best)), 2f);
            GradeRavinePath(path);
            for (int i = 0; i < path.Count; i++) path[i] = TerrainUtil.Ground(path[i]);
            _ravineViewIndex = j;
            _ravineView = TerrainUtil.Ground(bestEnd);

            var go = new GameObject("Track: Hemlock Ravine");
            go.transform.SetParent(reserve, false);
            var track = go.AddComponent<ReserveTrack>();
            track.title = "Hemlock Ravine";
            track.kind = TrackKind.Footpath;
            track.SetPath(path, 1.3f);
            _tracks.Add(track);
            _ravinePath = track;
        }

        private List<Vector3> RouteRavinePath(Vector3 end, out float cost)
        {
            cost = float.MaxValue;
            const float cell = 2.5f, margin = 260f;
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            Vector2 min = new Vector2(end.x, end.z), max = min;
            foreach (var p in _run) { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
            min -= Vector2.one * margin; max += Vector2.one * margin;
            min = Vector2.Max(min, new Vector2(tPos.x + 4f, tPos.z + 4f));
            max = Vector2.Min(max, new Vector2(tPos.x + size.x - 4f, tPos.z + size.z - 4f));
            int nx = Mathf.FloorToInt((max.x - min.x) / cell), nz = Mathf.FloorToInt((max.y - min.y) / cell);
            if (nx < 4 || nz < 4) return null;
            int count = nx * nz;
            var height = new float[count];
            var baseCost = new float[count];
            var bad = new bool[count];
            var goal = new bool[count];
            float half = _s.trailWidth * 0.5f;
            Vector3 At(int id) => new Vector3(min.x + (id % nx) * cell, 0f, min.y + (id / nx) * cell);
            for (int id = 0; id < count; id++)
            {
                Vector3 p = At(id);
                height[id] = TerrainUtil.SampleHeight(p);
                float slope = TerrainUtil.SlopeDegrees(p);
                float dt = RunDistance(p);
                float c = 1f + (slope / 30f) * (slope / 30f);
                bad[id] = dt < 2.2f || InKeepOut(p) || slope > 42f || NearHiddenPlace(p, 38f - 6f);
                if (CreekDistance(p) < 4f) c += 25f;
                if (dt >= 2.2f && dt < 3.6f) c *= 0.7f; // walk beside the run
                foreach (var s in _sites) if (TerrainUtil.FlatDistance(p, s.centre) < s.radius * 0.8f) c += 40f;
                baseCost[id] = c;
                // Goals: the loop, any footpath, a pull-off (not the road itself: the trail hierarchy).
                float g = _trail.DistanceToTrail(p);
                foreach (var t in _tracks) if (t.kind == TrackKind.Footpath) g = Mathf.Min(g, t.DistanceTo(p) - (t.Width * 0.5f - half));
                g = Mathf.Min(g, PadDistance(p));
                goal[id] = g < 1.2f;
            }
            int sx = Mathf.Clamp(Mathf.RoundToInt((end.x - min.x) / cell), 0, nx - 1), sz = Mathf.Clamp(Mathf.RoundToInt((end.z - min.y) / cell), 0, nz - 1);
            int start = sz * nx + sx;
            var dist = new float[count];
            var prev = new int[count];
            for (int i = 0; i < count; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[start] = 0f;
            var heap = new MinHeap();
            heap.Push(0f, start);
            int[] ddx = { 1, -1, 0, 0, 1, 1, -1, -1, 1, 2, -1, -2, 1, 2, -1, -2 };
            int[] ddz = { 0, 0, 1, -1, 1, -1, 1, -1, 2, 1, 2, 1, -2, -1, -2, -1 };
            int found = -1;
            while (heap.Count > 0)
            {
                var (k, u) = heap.Pop();
                if (k > dist[u]) continue;
                if (goal[u]) { found = u; break; }
                int ux = u % nx, uz = u / nx;
                for (int q = 0; q < 16; q++)
                {
                    int vx = ux + ddx[q], vz = uz + ddz[q];
                    if (vx < 0 || vz < 0 || vx >= nx || vz >= nz) continue;
                    int v = vz * nx + vx;
                    if (bad[v]) continue;
                    float len = Mathf.Sqrt(ddx[q] * ddx[q] + ddz[q] * ddz[q]) * cell;
                    float grade = Mathf.Abs(height[v] - height[u]) / len;
                    if (grade > 0.3f) continue; // about 17°
                    float step = len / cell * ((baseCost[u] + baseCost[v]) * 0.5f + (grade / 0.1f) * (grade / 0.1f));
                    float nd = k + step;
                    if (nd >= dist[v]) continue;
                    dist[v] = nd; prev[v] = u;
                    heap.Push(nd, v);
                }
            }
            if (found < 0) return null;
            cost = dist[found];
            var pts = new List<Vector3>();
            for (int u = found; u >= 0; u = prev[u]) pts.Add(At(u));
            pts[pts.Count - 1] = end;
            // Begin exactly on the path it leaves.
            Vector3 first = pts[0];
            Vector3 onPath = NearestPathPoint(first);
            if (TerrainUtil.FlatDistance(onPath, first) < 3f) pts.Insert(0, onPath);
            return pts;
        }

        /// <summary>Distance to the edge of the loop or any track except <paramref name="exclude"/> (loop-width rules, like PathDistance).</summary>
        private float OtherPathDistance(Vector3 p, ReserveTrack exclude)
        {
            float half = _s.trailWidth * 0.5f;
            float d = _trail.DistanceToTrail(p);
            foreach (var t in _tracks) if (t != exclude) d = Mathf.Min(d, t.DistanceTo(p) - (t.Width * 0.5f - half));
            return Mathf.Min(d, PadDistance(p));
        }

        private Vector3 NearestPathPoint(Vector3 p)
        {
            Vector3 best = p;
            float bd = float.MaxValue;
            void Consider(IReadOnlyList<Vector3> pts)
            {
                foreach (var q in pts) { float d = TerrainUtil.FlatDistance(q, p); if (d < bd) { bd = d; best = q; } }
            }
            Consider(_trail.Points);
            foreach (var t in _tracks) if (t.kind == TrackKind.Footpath) Consider(t.Points);
            foreach (var pad in _pads) if (TerrainUtil.FlatDistance(pad.p, p) < bd) { bd = TerrainUtil.FlatDistance(pad.p, p); best = pad.p; }
            return new Vector3(best.x, 0f, best.z);
        }

        /// <summary>Trail work: the tread follows a smoothed profile (±6 m), cut into a wall or built up across a dip, flat across.</summary>
        private void GradeRavinePath(List<Vector3> path)
        {
            var data = _terrain.terrainData;
            Vector3 tPos = _terrain.transform.position, size = data.size;
            int res = data.heightmapResolution;
            float cx = size.x / (res - 1), cz = size.z / (res - 1);
            int n = path.Count;
            var raw = new float[n];
            for (int i = 0; i < n; i++) raw[i] = TerrainUtil.SampleHeight(path[i]);
            var profile = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f; int cnt = 0;
                for (int k = Mathf.Max(0, i - 3); k <= Mathf.Min(n - 1, i + 3); k++) { sum += raw[k]; cnt++; }
                profile[i] = sum / cnt;
            }
            const float half = 0.75f, reach = 2.6f;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in path) { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
            int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - reach - tPos.x) / cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((max.x + reach - tPos.x) / cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((min.y - reach - tPos.z) / cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((max.y + reach - tPos.z) / cz), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var heights = data.GetHeights(x0, z0, w, h);
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                var q = new Vector2(tPos.x + (x0 + x) * cx, tPos.z + (z0 + z) * cz);
                float best = float.MaxValue, ph = 0f;
                for (int i = 1; i < n; i++)
                {
                    Vector2 a = new Vector2(path[i - 1].x, path[i - 1].z), b = new Vector2(path[i].x, path[i].z), ab = b - a;
                    if (Mathf.Abs(q.x - a.x) > reach + 3f && Mathf.Abs(q.x - b.x) > reach + 3f) continue;
                    if (Mathf.Abs(q.y - a.y) > reach + 3f && Mathf.Abs(q.y - b.y) > reach + 3f) continue;
                    float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
                    float d = Vector2.Distance(q, a + ab * t);
                    if (d < best) { best = d; ph = Mathf.Lerp(profile[i - 1], profile[i], t); }
                }
                if (best > reach) continue;
                var p3 = new Vector3(q.x, 0f, q.y);
                float wgt = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half, reach, best));
                if (RunDistance(p3) < 2.3f || CreekDistance(p3) < 5f) wgt = 0f;
                // Never touch the path it leaves: the old tread stays exactly as it was.
                wgt *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.5f, 5.5f, PathDistance(p3)));
                if (wgt <= 0f) continue;
                float cur = tPos.y + heights[z, x] * size.y;
                heights[z, x] = Mathf.Clamp01((Mathf.Lerp(cur, ph, wgt) - tPos.y) / size.y);
            }
            data.SetHeights(x0, z0, heights);
        }

        // ================================================================== settle what already stood there

        /// <summary>
        /// Everything already built where the ground moved follows it: objects shift by the change in ground height at their
        /// position; vegetation and props that would now stand in the run's channel, on the new footpath, in the Hidden Pond's
        /// water or inside the rock shelter's crag are removed. Creek reeds (one combined mesh) lose the stems the run undercut.
        /// </summary>
        private void SettleScenery(Transform reserve, float[,] before, float[,] after, HiddenPondPlan pond, ShelterPlan shelter)
        {
            var data = _terrain.terrainData;
            Vector3 tPos = _terrain.transform.position, size = data.size;
            int res = data.heightmapResolution;
            float Delta(Vector3 p)
            {
                float fx = Mathf.Clamp((p.x - tPos.x) / size.x * (res - 1), 0f, res - 1.001f);
                float fz = Mathf.Clamp((p.z - tPos.z) / size.z * (res - 1), 0f, res - 1.001f);
                int x = (int)fx, z = (int)fz;
                float tx = fx - x, tz = fz - z;
                float d00 = after[z, x] - before[z, x], d10 = after[z, x + 1] - before[z, x + 1];
                float d01 = after[z + 1, x] - before[z + 1, x], d11 = after[z + 1, x + 1] - before[z + 1, x + 1];
                return Mathf.Lerp(Mathf.Lerp(d00, d10, tx), Mathf.Lerp(d01, d11, tx), tz) * size.y;
            }
            bool Doomed(Vector3 p)
            {
                if (_run.Count > 1 && RunDistance(p) < 2.4f) return true;
                if (_ravinePath != null && _ravinePath.DistanceTo(p) < 1.4f) return true;
                if (pond != null && TerrainUtil.FlatDistance(p, pond.centre) < HiddenPondRadius(pond, p) + 0.8f) return true;
                if (shelter != null && shelter.InCrag(p)) return true;
                return false;
            }
            // Scenery (trees, bushes, rocks, logs, ferns: scanned art is named PH_…) may be lifted out; props and markers only move.
            bool Scenery(Transform t) =>
                t.GetComponent<EnvironmentFeature>() != null || t.GetComponent<FoliageSway>() != null || t.name.StartsWith("PH_") || t.name.StartsWith("Young ");
            bool Marker(Transform t) =>
                t.GetComponent<HabitatHotspot>() != null || t.GetComponent<ScheduleAnchor>() != null || t.GetComponent<WildlifeResidence>() != null ||
                t.GetComponent<AmbientSound>() != null || t.GetComponent<MistPatch>() != null || t.GetComponent<ScenicViewpoint>() != null ||
                t.GetComponent<ReserveDiscovery>() != null;

            var objects = new List<Transform>();
            var env = reserve.parent;
            if (env != null)
                foreach (Transform group in env)
                {
                    if (group == reserve || group.name == "Trail" || group.name == "Trail Props") continue;
                    foreach (Transform t in group) objects.Add(t);
                }
            foreach (Transform t in reserve)
            {
                if (t.GetComponent<ReserveDestination>() != null || t.GetComponent<ReserveTrack>() != null ||
                    t.GetComponent<ReserveParking>() != null) continue;
                if (t.GetComponent<ReserveCreek>() != null || t.name == "Hidden Places" || t.name == "Creek Ford")
                {
                    foreach (Transform c in t) objects.Add(c);
                    continue;
                }
                objects.Add(t);
            }

            var doomed = new List<GameObject>();
            foreach (var t in objects)
            {
                if (t == null) continue;
                Vector3 p = t.position;
                if (p == Vector3.zero) continue; // world-space meshes (creek water, combined reeds) sit at the origin
                bool marker = Marker(t);
                if (!marker && t.GetComponent<MeshRenderer>() == null && t.GetComponentInChildren<Renderer>() == null) continue;
                if (!marker && Scenery(t) && Doomed(p)) { doomed.Add(t.gameObject); continue; }
                float d = Delta(p);
                if (Mathf.Abs(d) < 0.02f) continue;
                t.position = p + Vector3.up * d;
                _settledMoved++;
            }
            foreach (var g in doomed) { Object.DestroyImmediate(g); _settledRemoved++; }
            RemoveWhere(_treeObjects, _ => false); // drop the destroyed entries
            RemoveWhere(_bushObjects, _ => false);

            // Combined creek reeds: rebuild without the stems standing where the ground moved.
            foreach (var name in new[] { "Creek Reeds", "Creek Cattails" })
            {
                if (!_combinedParts.TryGetValue(name, out var parts)) continue;
                var kept = new List<CombineInstance>();
                foreach (var ci in parts)
                {
                    Vector3 at = ci.transform.GetColumn(3);
                    if (Mathf.Abs(Delta(at)) < 0.03f && !Doomed(at)) kept.Add(ci);
                }
                if (kept.Count == parts.Count) continue;
                Transform host = null;
                foreach (Transform t in reserve)
                    if (t.GetComponent<ReserveCreek>() != null && t.Find(name) != null) host = t.Find(name);
                var mf = host != null ? host.GetComponent<MeshFilter>() : null;
                if (mf == null) continue;
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kept.ToArray(), true, true);
                mf.sharedMesh = GeneratedAssets.Save(mesh, "Mesh_" + name.Replace(" ", ""));
                _combinedParts[name] = kept;
                _settledRemoved += parts.Count - kept.Count;
            }
        }

        // ================================================================== ravine: build

        private void BuildRavine(Transform reserve)
        {
            var wood = GeneratedAssets.Material("Wood", new Color(0.52f, 0.39f, 0.26f));
            var post = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
            var group = new GameObject("Hemlock Ravine").transform;
            group.SetParent(reserve, false);
            int n = _run.Count;

            // The run on the trailhead map.
            var map = new GameObject("Hemlock Run");
            map.transform.SetParent(group, false);
            var line = map.AddComponent<ReserveCreek>();
            line.title = "Hemlock Run";
            var mapPoints = new List<Vector3>();
            for (int i = 0; i < n; i++) mapPoints.Add(new Vector3(_run[i].x, _runWater[i], _run[i].z));
            line.SetPath(mapPoints);
            BuildRunWater(map.transform);

            if (_cascade != null) BuildRunCascade(group);

            // Footbridges where the run passes under an existing footpath.
            foreach (var c in _runCrossings)
                Footbridge(group, new Vector3(c.at.x, 0f, c.at.z), c.dir, wood, post, false, Mathf.Max(1.8f, c.width + 0.5f));

            BuildRavinePathPieces(group, wood, post);
            BuildRavineScenery(group);

            // Brook voices along the run, mist on the floor at dawn.
            for (int i = 4; i < n - 6; i += 15)
            {
                var src = new GameObject("Brook Sound");
                src.transform.SetParent(group, false);
                src.transform.position = new Vector3(_run[i].x, _runWater[i] + 0.3f, _run[i].z);
                src.AddComponent<AudioSource>();
                var amb = src.AddComponent<AmbientSound>();
                amb.kind = AmbientKind.Brook;
                amb.volume = 0.3f;
                amb.audibleRadius = 24f;
            }
            for (int k = 1; k <= 2; k++)
            {
                int i = Mathf.Clamp(n * k / 3, 0, n - 1);
                Mist(group, new Vector3(_run[i].x, _runWater[i] + 0.6f, _run[i].z), new Vector3(12f, 1.4f, 12f), 0.8f);
            }

            // Wildlife (ids only; species that don't exist in the database are skipped by the spawner).
            int below = _cascade != null ? Mathf.Min(n - 1, _cascade.pool[_cascade.pool.Length - 1] + 10) : n / 3;
            Vector3 runSide = Side(_run, below, out _);
            Residence(group, "Hemlock Run", TerrainUtil.Ground(_run[below] + runSide * 2f), 9f, "louisiana_waterthrush");
            int mid = (below + n - 8) / 2;
            Anchor(group, ScheduleRole.Water, TerrainUtil.Ground(_run[Mathf.Clamp(mid, 0, n - 1)] + runSide * 3f), 10f);
            AddHotspot(group, "Run Water", HotspotType.Water, TerrainUtil.Ground(_run[Mathf.Clamp(mid, 0, n - 1)]), 7f);
        }

        /// <summary>The run's water: a narrow ribbon just above the bed, skipping the cascade's drops and the pool (a disc there).</summary>
        private void BuildRunWater(Transform parent)
        {
            int n = _run.Count;
            var skip = new HashSet<int>();
            if (_cascade != null)
            {
                foreach (int lip in _cascade.lips) skip.Add(lip + 1);
                foreach (int p in _cascade.pool) { skip.Add(p); skip.Add(p + 1); }
            }
            var verts = new List<Vector3>(n * 3);
            var uvs = new List<Vector2>(n * 3);
            var tris = new List<int>();
            float dist = 0f;
            const float half = 1.4f;
            for (int i = 0; i < n; i++)
            {
                Vector3 side = Side(_run, i, out _);
                if (i > 0) dist += TerrainUtil.FlatDistance(_run[i - 1], _run[i]);
                // The last samples run into Willow Creek's channel just below its water, so the creek's surface covers the join.
                float y = i >= n - 4 ? Mathf.Min(_runWater[i], _creekWater[_runMouth] - 0.04f) : _runWater[i];
                Vector3 c = new Vector3(_run[i].x, y, _run[i].z);
                verts.Add(c - side * half); verts.Add(c); verts.Add(c + side * half);
                uvs.Add(new Vector2(0f, dist / 3f)); uvs.Add(new Vector2(0.5f, dist / 3f)); uvs.Add(new Vector2(1f, dist / 3f));
                if (i == 0 || skip.Contains(i)) continue;
                int b = (i - 1) * 3, c0 = i * 3;
                for (int k = 0; k < 2; k++)
                {
                    tris.Add(b + k); tris.Add(c0 + k); tris.Add(b + k + 1);
                    tris.Add(b + k + 1); tris.Add(c0 + k); tris.Add(c0 + k + 1);
                }
            }
            var mesh = new Mesh { name = "RunWater" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = GeneratedAssets.Save(mesh, "Mesh_HemlockRunWater");
            var water = new GameObject("Run Water");
            water.transform.SetParent(parent, false);
            water.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = water.AddComponent<MeshRenderer>();
            // The same tea-coloured, rippled water as Willow Creek (never a mirror), flowing a little faster: it is steeper.
            mr.sharedMaterial = WaterMaterial("CreekWater", new Color(0.075f, 0.095f, 0.07f), 0.8f, new Vector2(3f, 3f), 0.55f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            water.AddComponent<WaterFlow>().speed = new Vector2(0f, 0.12f);
        }

        /// <summary>
        /// The cascade: each drop a narrow curtain over a scanned rock ledge (set into the lip, open side downstream, like Willow
        /// Falls' face), then a plunge pool. Every curtain gets a <see cref="Waterfall"/>, so the whole cascade swells after rain
        /// (spate); the last one also throws spray and carries the roar.
        /// </summary>
        private void BuildRunCascade(Transform group)
        {
            var cs = _cascade;
            int n = _run.Count;
            var casc = new GameObject("Hemlock Cascade").transform;
            casc.SetParent(group, false);
            casc.position = _run[cs.lips[0]];
            var mat = FallsMaterial();
            for (int k = 0; k < cs.lips.Length; k++)
            {
                int lipIndex = cs.lips[k];
                bool last = k == cs.lips.Length - 1;
                Vector3 lip = new Vector3(_run[lipIndex].x, _runWater[lipIndex], _run[lipIndex].z);
                Vector3 down = TerrainUtil.Flat(_run[Mathf.Min(n - 1, lipIndex + 1)] - _run[lipIndex]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, down);
                float below = last ? cs.poolWater : _runWater[Mathf.Min(n - 1, lipIndex + 1)];
                float drop = lip.y - below;
                if (drop < 0.3f) continue;
                float footBed = _runBed[Mathf.Min(n - 1, lipIndex + 1)];

                // The ledge: a scanned rock face scaled to the drop, set into the lip.
                if (_artOutcrops.Count > 0)
                {
                    var prefab = _artOutcrops[_artOutcrops.Count - 1];
                    var face = InstantiatePrefab(prefab, casc, lip, 1f);
                    var f = face.GetComponent<EnvironmentFeature>();
                    float h = f != null ? f.height : 4.7f;
                    float s = Mathf.Clamp((drop + 1.2f) / Mathf.Max(0.5f, h), 0.3f, 1.3f);
                    face.transform.localScale = prefab.transform.localScale * s;
                    face.transform.SetPositionAndRotation(new Vector3(lip.x, footBed - 0.5f, lip.z) - down * 0.5f,
                        Quaternion.LookRotation(down) * Quaternion.Euler(0f, R(-8f, 8f), 0f));
                    face.name = "Cascade Ledge";
                    if (f != null) { f.radius *= s; f.height *= s; }
                    MakeStatic(face);
                }

                var curtain = new GameObject("Cascade Curtain " + (k + 1));
                curtain.transform.SetParent(casc, false);
                curtain.AddComponent<MeshFilter>().sharedMesh = GeneratedAssets.Save(CurtainMesh(lip, down, side, below, 0.75f, 1.0f), "Mesh_CascadeCurtain" + (k + 1));
                var cr = curtain.AddComponent<MeshRenderer>();
                cr.sharedMaterial = mat;
                cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var flow = curtain.AddComponent<WaterFlow>();
                flow.speed = new Vector2(0f, -1.4f);
                flow.scrollBase = true;
                flow.wobble = 0f;
                var falls = curtain.AddComponent<Waterfall>();
                falls.curtain = flow;
                if (last)
                {
                    Vector3 poolAt = new Vector3(_run[cs.pool[1 % cs.pool.Length]].x, cs.poolWater, _run[cs.pool[1 % cs.pool.Length]].z);
                    falls.sprayMaterial = MistMaterial();
                    falls.sprayCentre = poolAt + Vector3.up * 0.3f;
                    falls.spraySize = new Vector3(2.2f, 0.4f, 1.6f);
                    falls.sprayRotation = Quaternion.LookRotation(down);
                    var roarGo = new GameObject("Cascade Voice");
                    roarGo.transform.SetParent(casc, false);
                    roarGo.transform.position = poolAt + Vector3.up;
                    roarGo.AddComponent<AudioSource>();
                    var roar = roarGo.AddComponent<AmbientSound>();
                    roar.kind = AmbientKind.Falls;
                    roar.volume = 0.45f;
                    roar.audibleRadius = 55f;
                    falls.roar = roarGo.GetComponent<AudioSource>();
                }
            }

            // The plunge pool: a small disc of still water over the bowl (its banks are carved 0.3 m above it).
            Vector3 pc = Vector3.zero;
            foreach (int j in cs.pool) pc += _run[j];
            pc /= cs.pool.Length;
            var pool = new GameObject("Plunge Pool");
            pool.transform.SetParent(casc, false);
            pool.transform.position = new Vector3(pc.x, cs.poolWater, pc.z);
            pool.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            pool.AddComponent<MeshFilter>().sharedMesh = GeneratedAssets.LoadOrCreate("Mesh_PondDisc", () => ProceduralMeshes.Disc(48));
            var pmr = pool.AddComponent<MeshRenderer>();
            pmr.sharedMaterial = WaterMaterial("CreekWater", new Color(0.075f, 0.095f, 0.07f), 0.8f, new Vector2(3f, 3f), 0.55f);
            pmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var drift = pool.AddComponent<WaterFlow>();
            drift.speed = new Vector2(0.02f, 0.03f);
            drift.wobble = 1f;

            // Boulders at the pool's edge, the phoebe's ledge, water for wildlife.
            Vector3 ps = Side(_run, cs.pool[0], out _);
            for (int i = 0; i < 4 && _art && _artBoulders.Count > 0; i++)
            {
                Vector3 p = TerrainUtil.Ground(pc + ps * (R() < 0.5f ? -1f : 1f) * R(2.2f, 3.2f) + Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward * R(0f, 1.2f));
                PlaceArt(_artBoulders, casc, p, R(0.45f, 0.75f), 0.3f, 10f);
            }
            Vector3 ledge = new Vector3(_run[cs.lips[0]].x, _runWater[cs.lips[0]] + 0.4f, _run[cs.lips[0]].z) + Side(_run, cs.lips[0], out _) * 1.6f;
            AddHotspot(casc, "Cascade Ledge", HotspotType.Perch, ledge, 5f);
            AddHotspot(casc, "Cascade Pool", HotspotType.Water, new Vector3(pc.x, cs.poolWater, pc.z), 6f);
            Residence(casc, "Cascade ledge", TerrainUtil.Ground(pc + ps * 2.5f), 6f, "eastern_phoebe");
            AddViewpoint(casc, "Hemlock Cascade", _ravineView != Vector3.zero ? _ravineView + Vector3.up * 1.6f : pc + ps * 6f + Vector3.up * 1.6f,
                new Vector3(_run[cs.lips[cs.lips.Length / 2]].x, cs.foot + cs.height * 0.5f, _run[cs.lips[cs.lips.Length / 2]].z));
        }

        /// <summary>The footpath's ribbon (when tracks have meshes), its sign where it leaves the path, a footbridge over Willow Creek, a seat at the end.</summary>
        private void BuildRavinePathPieces(Transform group, Material wood, Material post)
        {
            if (_ravinePath == null) return;
            var pts = _ravinePath.Points;
            var go = _ravinePath.gameObject;
            if (_trackMeshes && pts.Count > 1)
            {
                var mesh = GeneratedAssets.Save(ProceduralMeshes.TrailRibbon(pts, 1.3f, 0.6f, closed: false), "Mesh_Track_HemlockRavine");
                var surface = new GameObject("Track Surface");
                surface.transform.SetParent(go.transform, false);
                surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = surface.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GeneratedAssets.Material("TrackDirt", Color.white, 0.05f);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // Willow Creek crossings: a footbridge, as everywhere else in the reserve.
            var crossings = new List<Vector3>();
            for (int i = 1; i < pts.Count; i++)
                for (int k = 1; k < _creek.Count; k++)
                {
                    if (!SegmentsIntersect(pts[i - 1], pts[i], _creek[k - 1], _creek[k], out Vector3 hit)) continue;
                    bool dup = false;
                    foreach (var c in crossings) if (TerrainUtil.FlatDistance(c, hit) < 12f) dup = true;
                    if (dup) continue;
                    crossings.Add(hit);
                    Vector3 dir = TerrainUtil.Flat(pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 2)]).normalized;
                    Footbridge(group, hit, dir, wood, post, false, 1.8f);
                }

            // One sign where the footpath leaves the path, read in two seconds (title, pictograms, three words).
            if (pts.Count > 4)
            {
                Vector3 start = pts[0], dir = TerrainUtil.Flat(pts[3] - pts[0]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                // Beside the new path, off the one it leaves and out of the water: try both sides, then a little further in.
                Vector3 pos = Vector3.zero;
                bool placed = false;
                foreach (float ahead in new[] { 2.4f, 3.6f, 5f })
                {
                    foreach (float sgn in new[] { 1f, -1f })
                    {
                        Vector3 q = TerrainUtil.Ground(start + dir * ahead + side * sgn * 1.5f);
                        if (OtherPathDistance(q, _ravinePath) < 1.6f || CreekDistance(q) < 4f || _ravinePath.DistanceTo(q) < 1.1f) continue;
                        pos = q; placed = true; break;
                    }
                    if (placed) break;
                }
                if (!placed) pos = TerrainUtil.Ground(start + dir * 3.6f + side * 1.5f);
                Clear(pos, 1.5f);
                SignKit.Sign(group, pos, Quaternion.LookRotation(-dir), "HEMLOCK RAVINE", "to the cascade", new[] { SignIcon.Walk, SignIcon.Water }, 0, post);
                AddKeepOut(pos, 1.2f);
            }

            // A log seat at the path's end, looking up the ravine at the cascade.
            if (_ravineViewIndex > 0 && _cascade != null)
            {
                Vector3 target = _run[_cascade.lips[_cascade.lips.Length / 2]];
                Vector3 look = TerrainUtil.Flat(target - _ravineView).normalized;
                Vector3 seatPos = TerrainUtil.Ground(_ravineView - look * 0.9f + Vector3.Cross(Vector3.up, look) * 0.6f);
                if (RunDistance(seatPos) > 2.3f)
                {
                    var seat = PropBuilder.Group("Ravine Seat", group, seatPos, Quaternion.LookRotation(look));
                    PropBuilder.Box("Seat Log", seat, new Vector3(0f, 0.24f, 0f), new Vector3(1.7f, 0.36f, 0.4f), post);
                    PropBuilder.AddCollider(seat, new Vector3(0f, 0.24f, 0f), new Vector3(1.7f, 0.36f, 0.4f));
                    AddKeepOut(seatPos, 1.2f);
                }
            }
        }

        /// <summary>
        /// Hemlocks on the walls and rim, ferns on the floor and lower walls, mossy stones in the run, one fallen trunk across it.
        /// Through kit roles (conifers become the Environment Kit's conifers when it has them) and the private random stream.
        /// </summary>
        private void BuildRavineScenery(Transform group)
        {
            int n = _run.Count;
            int start = 0, end = n - 8;
            int planted = 0;
            for (int attempt = 0; attempt < 420 && planted < 46; attempt++)
            {
                int i = _rng.Next(start, end);
                Vector3 side = Side(_run, i, out _);
                float sgn = R() < 0.5f ? -1f : 1f;
                float D = (sgn > 0f ? _runBankR[i] : _runBankL[i]) - _runBed[i];
                float W = Mathf.Clamp(RavineFloor + WallRun * D, 6f, WallMax);
                Vector3 p = TerrainUtil.Ground(_run[i] + side * sgn * R(RavineFloor + 0.4f, W + 6f));
                if (PathDistance(p) < 2.6f || InKeepOut(p) || TerrainUtil.SlopeDegrees(p) > 46f || !IsFree(p, 2.6f)) continue;
                var tree = R() < 0.8f ? Conifer(group, p) : Deciduous(group, p); // hemlock ravine: mostly conifers
                float k = R(1f, 1.35f);
                tree.transform.localScale *= k;
                var feature = tree.GetComponent<EnvironmentFeature>();
                if (feature != null) { feature.radius *= k; feature.height *= k; }
                MakeStaticExceptSway(tree);
                AddDistanceCull(tree, 0.012f);
                _treeObjects.Add(tree);
                Occupy(p);
                planted++;
            }
            if (_art && _artFerns.Count > 0)
                for (int f = 0; f < 90; f++)
                {
                    int i = _rng.Next(start, end);
                    Vector3 side = Side(_run, i, out _);
                    Vector3 p = TerrainUtil.Ground(_run[i] + side * (R() < 0.5f ? -1f : 1f) * R(2.4f, RavineFloor + 3.5f));
                    if (PathDistance(p) < 1.4f || InKeepOut(p) || TerrainUtil.SlopeDegrees(p) > 40f) continue;
                    PlaceArt(_artFerns, group, p, R(0.8f, 1.35f), 0.02f, 8f);
                }
            for (int i = 4; i < end; i += _rng.Next(5, 10))
            {
                Vector3 side = Side(_run, i, out _);
                bool bank = R() < 0.5f;
                Vector3 p = _run[i] + side * (bank ? R(1.6f, 2.6f) * (R() < 0.5f ? -1f : 1f) : R(-0.6f, 0.6f));
                p.y = TerrainUtil.SampleHeight(p);
                if (_ravinePath != null && _ravinePath.DistanceTo(p) < 1.2f) continue;
                if (_art && _artRocks.Count > 0) PlaceArt(_artRocks, group, p, bank ? R(0.45f, 0.9f) : R(0.25f, 0.45f), 0.35f, 15f);
                else MeshPart("Stone", group, Pick(_rocks), Pick(_stone), p, Vector3.one * R(0.3f, 0.7f), Quaternion.Euler(R(0f, 30f), R(0f, 360f), R(0f, 30f)));
            }
            // A fallen hemlock across the run, a third of the way down: a natural bridge for squirrels, deadwood for fungi.
            int at = Mathf.Clamp((_cascade != null ? _cascade.pool[_cascade.pool.Length - 1] : 0) + (n / 3), 1, n - 9);
            Vector3 dirAt;
            Side(_run, at, out dirAt);
            if (_ravinePath == null || _ravinePath.DistanceTo(_run[at]) > 5f)
            {
                Vector3 p = new Vector3(_run[at].x, _runWater[at] + 0.35f, _run[at].z);
                GameObject log;
                if (_art && _artLogs.Count > 0) log = PlaceArt(_artLogs, group, p, 1.1f, 0f, 3f);
                else
                {
                    log = new GameObject("Fallen Log");
                    log.transform.SetParent(group, false);
                    log.transform.position = p;
                    MeshPart("Trunk", log.transform, _cylinder, Pick(_bark), Vector3.zero, new Vector3(0.45f, 3.4f, 0.45f), Quaternion.Euler(0f, 0f, 90f));
                    var col = log.AddComponent<CapsuleCollider>();
                    col.direction = 0; col.height = 6.8f; col.radius = 0.22f;
                }
                log.name = "Fallen Log";
                log.transform.rotation = Quaternion.LookRotation(dirAt) * Quaternion.Euler(0f, R(-20f, 20f), 0f);
            }
        }
    }
}
