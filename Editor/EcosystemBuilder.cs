using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// The ecosystem pass (0.8): what turns "a collection of destinations" into "a living landscape".
    ///
    ///   Willow Creek   the reserve's backbone: a stream from the ridge's flank down to the pond, routed downhill,
    ///                  carved into the terrain (repeatably), with flowing water, footbridges where trails cross,
    ///                  bed and bank rocks, a fallen log, reeds, riparian woodland, brook sounds and creek-side deer.
    ///   Pull-offs      gravel bike parking short of each destination (so the engine doesn't scatter the wildlife)
    ///                  and a scenic pull-off with a bench midway along longer tracks.
    ///   Storytelling   interpretive panels, numbered track marker posts, a deer-crossing sign, nest boxes, a ranger's
    ///                  log in the blind: the quiet evidence that people look after this place.
    ///   Residences     deer that drink at the creek, deer whose range straddles a track, cottontails on the meadow edge.
    ///
    /// Planning (<see cref="PlanCreek"/>, <see cref="PlanParking"/>) happens before scenery so trees, painting and
    /// grass respect the creek and the pull-offs; <see cref="BuildEcosystem"/> runs after the destinations.
    /// </summary>
    public partial class VegetationBuilder
    {
        private readonly List<Vector3> _creek = new List<Vector3>();
        private float[] _creekBed, _creekWater;
        private float[,] _creekField;
        private Vector2 _creekFieldOrigin;
        private const float CreekFieldCell = 4f;
        private readonly List<(Vector3 p, float r)> _pads = new List<(Vector3, float)>();
        private readonly List<(Vector3 p, string title, Vector3 view)> _scenicStops = new List<(Vector3, string, Vector3)>();
        private Transform _reserveParent;

        public float CreekLength { get { float l = 0f; for (int i = 1; i < _creek.Count; i++) l += Vector3.Distance(_creek[i - 1], _creek[i]); return l; } }

        // ================================================================== queries

        /// <summary>Horizontal distance to the creek's centre-line (cached grid; float.MaxValue if there is no creek).</summary>
        private float CreekDistance(Vector3 p)
        {
            if (_creekField == null) return float.MaxValue;
            int w = _creekField.GetLength(0), h = _creekField.GetLength(1);
            float fx = (p.x - _creekFieldOrigin.x) / CreekFieldCell, fz = (p.z - _creekFieldOrigin.y) / CreekFieldCell;
            if (fx < 0f || fz < 0f || fx > w - 1.001f || fz > h - 1.001f) return float.MaxValue;
            int x = (int)fx, z = (int)fz;
            float tx = fx - x, tz = fz - z;
            return Mathf.Lerp(Mathf.Lerp(_creekField[x, z], _creekField[x + 1, z], tx), Mathf.Lerp(_creekField[x, z + 1], _creekField[x + 1, z + 1], tx), tz);
        }

        private float CreekDistanceExact(Vector3 p, out int segment, out float t)
        {
            segment = 0; t = 0f;
            float best = float.MaxValue;
            for (int i = 1; i < _creek.Count; i++)
            {
                Vector3 a = _creek[i - 1], b = _creek[i];
                if (Mathf.Abs(p.x - a.x) > 12f && Mathf.Abs(p.x - b.x) > 12f) continue;
                if (Mathf.Abs(p.z - a.z) > 12f && Mathf.Abs(p.z - b.z) > 12f) continue;
                Vector2 q = new Vector2(p.x, p.z), a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z), ab = b2 - a2;
                float tt = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(q - a2, ab) / ab.sqrMagnitude) : 0f;
                float d = Vector2.Distance(q, a2 + ab * tt);
                if (d < best) { best = d; segment = i - 1; t = tt; }
            }
            return best;
        }

        private float PadDistance(Vector3 p)
        {
            float d = float.MaxValue;
            foreach (var pad in _pads) d = Mathf.Min(d, Mathf.Max(0f, TerrainUtil.FlatDistance(p, pad.p) - pad.r));
            return d;
        }

        // ================================================================== creek: plan, carve, water

        public void PlanCreek(Transform parent, bool shapeTerrain)
        {
            _reserveParent = parent;
            Site ridge = _sites.Find(s => s.kind == DestinationKind.RidgeOverlook);
            Site pond = _sites.Find(s => s.kind == DestinationKind.Wetland);
            if (!shapeTerrain || (ridge == null && pond == null)) return; // a creek must be carved; no shaping, no creek

            Vector3 source, mouth;
            if (ridge != null && pond != null)
            {
                Vector3 dir = TerrainUtil.Flat(pond.centre - ridge.centre).normalized;
                source = TerrainUtil.Ground(ridge.centre + dir * 32f);
                mouth = pond.centre - dir * pond.water * 0.6f;
            }
            else if (pond != null)
            {
                source = HighestPoint(pond.centre, 150f);
                mouth = pond.centre - TerrainUtil.Flat(pond.centre - source).normalized * pond.water * 0.6f;
            }
            else
            {
                source = TerrainUtil.Ground(ridge.centre + TerrainUtil.Flat(_loop.center - ridge.centre).normalized * 32f);
                mouth = LowestPoint(source, 180f);
            }
            if (TerrainUtil.FlatDistance(source, mouth) < 90f) return;

            var path = CreekPath(source, mouth);
            if (path == null || path.Count < 4) return;
            path = Resample(Chaikin(Chaikin(path)), 4f);
            Meander(path);
            path = Resample(Chaikin(path), 2.5f);
            for (int i = 0; i < path.Count; i++) path[i] = TerrainUtil.Ground(path[i]);
            _creek.AddRange(path);

            ComputeBed(pond);
            CarveCreek();
            BuildCreekField();

            var go = new GameObject("Willow Creek");
            go.transform.SetParent(parent, false);
            var creek = go.AddComponent<ReserveCreek>();
            creek.title = "Willow Creek";
            var mapPoints = new List<Vector3>();
            for (int i = 0; i < _creek.Count; i++) mapPoints.Add(new Vector3(_creek[i].x, _creekWater[i], _creek[i].z));
            creek.SetPath(mapPoints);
            BuildCreekWater(go.transform);

            // The creek's corridor is part of the reserve: scatter scenery along it too.
            foreach (var p in _creek)
            {
                _min = Vector2.Min(_min, new Vector2(p.x - 40f, p.z - 40f));
                _max = Vector2.Max(_max, new Vector2(p.x + 40f, p.z + 40f));
            }
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            _min = Vector2.Max(_min, new Vector2(tPos.x + 2f, tPos.z + 2f));
            _max = Vector2.Min(_max, new Vector2(tPos.x + size.x - 2f, tPos.z + size.z - 2f));
        }

        private Vector3 HighestPoint(Vector3 awayFrom, float minDistance)
        {
            Vector3 best = _loop.center; float bestH = float.MinValue;
            ForReserveGrid(p =>
            {
                if (TerrainUtil.FlatDistance(p, awayFrom) < minDistance) return;
                float h = TerrainUtil.SampleHeight(p);
                if (h > bestH) { bestH = h; best = p; }
            });
            return TerrainUtil.Ground(best);
        }

        private Vector3 LowestPoint(Vector3 awayFrom, float minDistance)
        {
            Vector3 best = _loop.center; float bestH = float.MaxValue;
            ForReserveGrid(p =>
            {
                if (TerrainUtil.FlatDistance(p, awayFrom) < minDistance || LoopRadius(p) < 1.3f) return;
                float h = TerrainUtil.SampleHeight(p);
                if (h < bestH) { bestH = h; best = p; }
            });
            return TerrainUtil.Ground(best);
        }

        private void ForReserveGrid(System.Action<Vector3> visit)
        {
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            for (float x = tPos.x + 45f; x < tPos.x + size.x - 45f; x += 12f)
            for (float z = tPos.z + 45f; z < tPos.z + size.z - 45f; z += 12f)
            {
                var p = new Vector3(x, 0f, z);
                if (TerrainUtil.FlatDistance(p, _loop.center) > _s.reserveReach) continue;
                visit(p);
            }
        }

        /// <summary>A* that prefers going downhill and gentle ground, and keeps away from structures and the blind's meadow.</summary>
        private List<Vector3> CreekPath(Vector3 from, Vector3 to)
        {
            const float cell = 5f;
            Vector3 tPos = _terrain.transform.position, size = _terrain.terrainData.size;
            int w = Mathf.FloorToInt(size.x / cell), h = Mathf.FloorToInt(size.z / cell);
            Vector2Int Node(Vector3 p) => new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((p.x - tPos.x) / cell), 2, w - 3),
                                                         Mathf.Clamp(Mathf.RoundToInt((p.z - tPos.z) / cell), 2, h - 3));
            Vector3 World(Vector2Int n) => new Vector3(tPos.x + n.x * cell, 0f, tPos.z + n.y * cell);
            var heights = new Dictionary<Vector2Int, float>();
            float H(Vector2Int n) { if (!heights.TryGetValue(n, out float v)) heights[n] = v = TerrainUtil.SampleHeight(World(n)); return v; }
            var extra = new Dictionary<Vector2Int, float>();
            float Extra(Vector2Int n)
            {
                if (extra.TryGetValue(n, out float v)) return v;
                Vector3 p = World(n);
                v = 0f;
                float slope = TerrainUtil.SlopeDegrees(p);
                v += (slope / 14f) * (slope / 14f);
                if (LoopRadius(p) < 0.85f) v += 2f;
                foreach (var s in _sites)
                {
                    float d = TerrainUtil.FlatDistance(p, s.centre);
                    if (s.kind == DestinationKind.RidgeOverlook && d < 18f) v += 30f;
                    if (s.kind == DestinationKind.ObservationBlind && d < s.radius * 0.8f) v += 30f;
                    if (s.kind == DestinationKind.OldGrowth && d < 10f) v += 10f;
                }
                if (InKeepOut(p)) v += 40f;
                extra[n] = v;
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
                    float len = d.x != 0 && d.y != 0 ? 1.414f : 1f;
                    float climb = Mathf.Max(0f, H(n) - H(cur));
                    float step = len * (1f + Extra(n)) + climb * 2.5f; // water runs downhill
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

        /// <summary>Lateral S-bends so the creek reads as a natural stream rather than a routed line.</summary>
        private void Meander(List<Vector3> p)
        {
            if (p.Count < 3) return;
            float total = 0f;
            var along = new float[p.Count];
            for (int i = 1; i < p.Count; i++) along[i] = total += TerrainUtil.FlatDistance(p[i - 1], p[i]);
            float phase = R(0f, 6.28f), wavelength = R(32f, 44f);
            var result = new Vector3[p.Count];
            for (int i = 0; i < p.Count; i++)
            {
                Vector3 dir = TerrainUtil.Flat(p[Mathf.Min(p.Count - 1, i + 1)] - p[Mathf.Max(0, i - 1)]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Min(along[i], total - along[i]) / 25f);
                float amp = 3.2f * (0.7f + 0.3f * Mathf.Sin(along[i] * 0.013f));
                result[i] = p[i] + side * Mathf.Sin(along[i] / wavelength * Mathf.PI * 2f + phase) * amp * fade;
            }
            for (int i = 0; i < p.Count; i++) p[i] = result[i];
        }

        /// <summary>
        /// Bed and water heights from the banks 7 m either side (outside anything the carve touches, so rebuilding
        /// gives the same creek), never rising downstream, and meeting the pond at its water level.
        /// </summary>
        private void ComputeBed(Site pond)
        {
            int n = _creek.Count;
            _creekBed = new float[n];
            _creekWater = new float[n];
            float floor = _terrain.transform.position.y + 0.05f;
            float prev = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = TerrainUtil.Flat(_creek[Mathf.Min(n - 1, i + 1)] - _creek[Mathf.Max(0, i - 1)]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float bank = Mathf.Min(TerrainUtil.SampleHeight(_creek[i] + side * 7f), TerrainUtil.SampleHeight(_creek[i] - side * 7f));
                float bed = Mathf.Min(bank - 1.1f, prev);
                bed = Mathf.Max(bed, bank - 3f); // through a rise: a deeper cut, not a canyon
                if (pond != null && pond.waterLevel != 0f && TerrainUtil.FlatDistance(_creek[i], pond.centre) < pond.water * 1.4f)
                    bed = Mathf.Min(bed, pond.waterLevel - 0.3f);
                bed = Mathf.Max(bed, floor);
                _creekBed[i] = bed;
                prev = bed;
            }
            for (int i = 0; i < n; i++) _creekWater[i] = _creekBed[i] + 0.32f;
        }

        private void CarveCreek()
        {
            var data = _terrain.terrainData;
            if (_s.backupTerrainData && !IsOwnTerrain()) BackupTerrainData(data);
            Vector3 tPos = _terrain.transform.position, size = data.size;
            int res = data.heightmapResolution;
            float cx = size.x / (res - 1), cz = size.z / (res - 1);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in _creek) { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
            const float flat = 1.2f, reach = 4.6f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - reach - tPos.x) / cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((max.x + reach - tPos.x) / cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((min.y - reach - tPos.z) / cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((max.y + reach - tPos.z) / cz), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var heights = data.GetHeights(x0, z0, w, h);
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector3(tPos.x + (x0 + x) * cx, 0f, tPos.z + (z0 + z) * cz);
                float d = CreekDistanceExact(p, out int seg, out float t);
                if (d > reach) continue;
                float bed = Mathf.Lerp(_creekBed[seg], _creekBed[Mathf.Min(seg + 1, _creekBed.Length - 1)], t);
                float target = d < flat ? bed : bed + 2.3f * Mathf.Pow((d - flat) / (reach - flat), 1.6f);
                float cur = tPos.y + heights[z, x] * size.y;
                heights[z, x] = Mathf.Clamp01((Mathf.Min(cur, target) - tPos.y) / size.y);
            }
            data.SetHeights(x0, z0, heights);
        }

        private void BuildCreekField()
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in _creek) { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
            const float margin = 60f;
            _creekFieldOrigin = min - Vector2.one * margin;
            int w = Mathf.CeilToInt((max.x - min.x + margin * 2f) / CreekFieldCell) + 1;
            int h = Mathf.CeilToInt((max.y - min.y + margin * 2f) / CreekFieldCell) + 1;
            _creekField = new float[w, h];
            for (int x = 0; x < w; x++)
            for (int z = 0; z < h; z++)
            {
                Vector2 q = _creekFieldOrigin + new Vector2(x, z) * CreekFieldCell;
                float best = float.MaxValue;
                for (int i = 1; i < _creek.Count; i += 1)
                {
                    Vector2 a = new Vector2(_creek[i - 1].x, _creek[i - 1].z), b = new Vector2(_creek[i].x, _creek[i].z), ab = b - a;
                    float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
                    best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
                }
                _creekField[x, z] = best;
            }
        }

        private void BuildCreekWater(Transform parent)
        {
            int n = _creek.Count;
            var verts = new List<Vector3>(n * 3);
            var uvs = new List<Vector2>(n * 3);
            var tris = new List<int>();
            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = TerrainUtil.Flat(_creek[Mathf.Min(n - 1, i + 1)] - _creek[Mathf.Max(0, i - 1)]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                if (i > 0) dist += TerrainUtil.FlatDistance(_creek[i - 1], _creek[i]);
                Vector3 c = new Vector3(_creek[i].x, _creekWater[i], _creek[i].z);
                verts.Add(c - side * 2.3f); verts.Add(c); verts.Add(c + side * 2.3f);
                uvs.Add(new Vector2(0f, dist / 4f)); uvs.Add(new Vector2(0.5f, dist / 4f)); uvs.Add(new Vector2(1f, dist / 4f));
                if (i == 0) continue;
                int b = (i - 1) * 3, c0 = i * 3;
                for (int k = 0; k < 2; k++)
                {
                    tris.Add(b + k); tris.Add(c0 + k); tris.Add(b + k + 1);
                    tris.Add(b + k + 1); tris.Add(c0 + k); tris.Add(c0 + k + 1);
                }
            }
            var mesh = new Mesh { name = "CreekWater" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = GeneratedAssets.Save(mesh, "Mesh_CreekWater");
            var water = new GameObject("Creek Water");
            water.transform.SetParent(parent, false);
            water.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = water.AddComponent<MeshRenderer>();
            // 1.2: tea-coloured water with moving ripples. Before, a near-mirror (smoothness 0.94) reflected the sky's
            // saturated zenith at grazing angles, and the creek read as a flat blue ribbon.
            mr.sharedMaterial = WaterMaterial("CreekWater", new Color(0.075f, 0.095f, 0.07f), 0.8f, new Vector2(3f, 3f), 0.55f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            water.AddComponent<WaterFlow>().speed = new Vector2(0f, 0.09f); // downstream (V runs along the creek)
        }

        /// <summary>Water surface material: dark base, tileable ripple normal map (generated once), moderate smoothness.</summary>
        private static Material WaterMaterial(string name, Color colour, float smoothness, Vector2 tiling, float normalStrength)
        {
            var mat = GeneratedAssets.Material(name, colour, smoothness);
            var ripples = RippleNormal();
            if (ripples != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", ripples);
                mat.SetTextureScale("_BumpMap", tiling);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", normalStrength);
                mat.EnableKeyword("_NORMALMAP");
            }
            // Existing materials keep their old smoothness unless it is set here (GeneratedAssets only updates colour).
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Tileable small ripples: two octaves of value noise plus a gentle cross-wave, as a normal map.</summary>
        private static Texture2D RippleNormal()
        {
            string path = GeneratedAssets.Folder + "/Terrain/T_WaterRipples_N_v1.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int n = 256;
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                h[y * n + x] = Fbm(u, v, 6, 3, 911) * 0.8f + Mathf.Sin((u * 5f + v * 3f) * Mathf.PI * 2f) * 0.06f;
            }
            GeneratedAssets.EnsureFolder(GeneratedAssets.Folder + "/Terrain");
            WritePng(path, n, NormalsFromHeight(h, n, 14f));
            ConfigureImporter(path, true, false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ================================================================== pull-offs

        /// <summary>
        /// A gravel pull-off short of each destination (beyond the engine's earshot of the residents) and a scenic
        /// pull-off with a bench midway along longer tracks. Runs after the tracks are routed and before scenery.
        /// </summary>
        public void PlanParking(bool shapeTerrain)
        {
            if (_reserveParent == null || _sites.Count == 0) return;
            var wood = GeneratedAssets.Material("Wood", new Color(0.52f, 0.39f, 0.26f));
            var post = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
            foreach (var s in _sites)
            {
                var pts = s.track.Points;
                if (pts.Count < 6) continue;
                float back = s.kind == DestinationKind.ObservationBlind ? 45f : s.kind == DestinationKind.RidgeOverlook ? 22f : 35f;
                int i = PointBackFromEnd(pts, back);
                Vector3 pad = PullOffSpot(pts, i, out Vector3 side);
                float walk = s.track.Length - AlongTrack(pts, i) + 6f;
                string hint = s.kind == DestinationKind.ObservationBlind ? $"walk {walk:0} m quietly to the blind" : $"walk {walk:0} m to {s.title}";
                AddPullOff(pad, side, $"{s.title} parking", hint, shapeTerrain, wood, post,
                    "BIKE PARKING", $"walk to {s.title}", new[] { SignIcon.Bike, SignIcon.Walk }, false);
                _destinationPads.Add(_pads[_pads.Count - 1].p);

                // Midway scenic pull-off on tracks long enough to want a pause.
                if (s.track.Length > 160f)
                {
                    int mid = BestViewPoint(pts, out Vector3 view);
                    if (mid > 0)
                    {
                        Vector3 spot = PullOffSpot(pts, mid, out Vector3 sd);
                        if (PadDistance(spot) > 25f)
                            AddPullOff(spot, sd, "Scenic pull-off", "a bench with a view", shapeTerrain, wood, post, "SCENIC PULL-OFF", "a bench with a view", new[] { SignIcon.Binoculars }, true, view);
                    }
                }
            }
        }

        private static int PointBackFromEnd(IReadOnlyList<Vector3> pts, float distance)
        {
            float acc = 0f;
            for (int i = pts.Count - 1; i > 0; i--)
            {
                acc += Vector3.Distance(pts[i], pts[i - 1]);
                if (acc >= distance) return i - 1;
            }
            return pts.Count / 2;
        }

        private static float AlongTrack(IReadOnlyList<Vector3> pts, int index)
        {
            float acc = 0f;
            for (int i = 1; i <= index && i < pts.Count; i++) acc += Vector3.Distance(pts[i - 1], pts[i]);
            return acc;
        }

        private Vector3 PullOffSpot(IReadOnlyList<Vector3> pts, int i, out Vector3 side)
        {
            Vector3 dir = TerrainUtil.Flat(pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
            side = Vector3.Cross(Vector3.up, dir);
            Vector3 a = TerrainUtil.Ground(pts[i] + side * 3.6f), b = TerrainUtil.Ground(pts[i] - side * 3.6f);
            bool useB = TerrainUtil.SlopeDegrees(b) + (CreekDistance(b) < 6f ? 100f : 0f) < TerrainUtil.SlopeDegrees(a) + (CreekDistance(a) < 6f ? 100f : 0f);
            if (useB) side = -side;
            return useB ? b : a;
        }

        /// <summary>Track point (not near either end) with the lowest ground to one side: the best view while riding.</summary>
        private int BestViewPoint(IReadOnlyList<Vector3> pts, out Vector3 viewDir)
        {
            int best = -1; float bestScore = float.MaxValue; viewDir = Vector3.forward;
            for (int i = pts.Count / 4; i < pts.Count * 3 / 4; i += 3)
            {
                Vector3 dir = TerrainUtil.Flat(pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float h0 = TerrainUtil.SampleHeight(pts[i]);
                foreach (var sd in new[] { side, -side })
                {
                    float score = 0f;
                    for (float r = 40f; r <= 120f; r += 40f) score += TerrainUtil.SampleHeight(pts[i] + sd * r) - h0;
                    if (score < bestScore) { bestScore = score; best = i; viewDir = sd; }
                }
            }
            return best;
        }

        private void AddPullOff(Vector3 pad, Vector3 side, string title, string hint, bool shape, Material wood, Material post,
            string signTitle, string signLine, SignIcon[] signIcons, bool bench, Vector3 view = default)
        {
            const float r = 3.4f;
            if (shape)
            {
                float hc = TerrainUtil.SampleHeight(pad);
                Shape(pad, r + 2.5f, (d, h) => Mathf.Lerp(hc, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r, r + 2.5f, d))));
            }
            pad = TerrainUtil.Ground(pad);
            _pads.Add((pad, r));
            AddKeepOut(pad, r + 1f);

            var go = new GameObject("Pull-off: " + title);
            go.transform.SetParent(_reserveParent, false);
            go.transform.position = pad;
            var parking = go.AddComponent<ReserveParking>();
            parking.title = title;
            parking.hint = hint;
            parking.radius = r;

            Vector3 signPos = TerrainUtil.Ground(pad + side * (r + 0.6f));
            Plank(go.transform, signTitle, signLine, signIcons, 0, signPos, Quaternion.LookRotation(-side), post);
            if (bench)
            {
                Vector3 look = view == Vector3.zero ? side : view;
                var b = PropBuilder.Group("Bench", go.transform, TerrainUtil.Ground(pad + look * 2.2f), Quaternion.LookRotation(look));
                PropBuilder.Box("Seat", b, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.45f), wood);
                PropBuilder.Box("Back", b, new Vector3(0f, 0.8f, -0.22f), new Vector3(1.8f, 0.34f, 0.06f), wood);
                PropBuilder.Box("LegL", b, new Vector3(-0.75f, 0.22f, 0f), new Vector3(0.09f, 0.45f, 0.42f), post);
                PropBuilder.Box("LegR", b, new Vector3(0.75f, 0.22f, 0f), new Vector3(0.09f, 0.45f, 0.42f), post);
                PropBuilder.AddCollider(b, new Vector3(0f, 0.45f, -0.02f), new Vector3(1.8f, 0.9f, 0.5f));
                _scenicStops.Add((pad, title, look));
            }
        }

        // ================================================================== build (after scenery & destinations)

        public void BuildEcosystem(Transform parent)
        {
            var wood = GeneratedAssets.Material("Wood", new Color(0.52f, 0.39f, 0.26f));
            var post = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
            if (_creek.Count > 1) BuildCreekLife(parent, wood, post);
            BuildFord(parent, wood, post); // 1.4
            BuildWoodlandCover(parent);
            BuildTrackMarkers(parent, post);
            BuildRoadMarkers(parent);
            BuildResidences(parent, wood, post);
            foreach (var stop in _scenicStops) AddViewpoint(parent, stop.title, stop.p + stop.view * 2.2f + Vector3.up * 1.2f, TerrainUtil.Ground(stop.p + stop.view * 80f) + Vector3.up * 2f);
        }

        private void BuildCreekLife(Transform parent, Material wood, Material post)
        {
            var creek = parent.Find("Willow Creek");
            if (creek == null) creek = parent;
            int n = _creek.Count;

            // Clear the channel of anything scattered into it.
            bool InChannel(Vector3 p) => CreekDistance(p) < 3.2f;
            RemoveWhere(_treeObjects, InChannel);
            RemoveWhere(_bushObjects, InChannel);

            // Crossings: footbridges where the loop or a track meets the creek.
            var crossings = new List<(Vector3 p, Vector3 dir)>();
            var crossingWidth = new List<float>();
            void Cross(IReadOnlyList<Vector3> path, bool closed, float width)
            {
                int count = closed ? path.Count : path.Count - 1;
                for (int i = 0; i < count; i++)
                {
                    Vector3 a = path[i], b = path[(i + 1) % path.Count];
                    for (int k = 1; k < n; k++)
                    {
                        if (!SegmentsIntersect(a, b, _creek[k - 1], _creek[k], out Vector3 hit)) continue;
                        bool near = false;
                        foreach (var c in crossings) if (TerrainUtil.FlatDistance(c.p, hit) < 12f) near = true;
                        if (!near) { crossings.Add((hit, TerrainUtil.Flat(b - a).normalized)); crossingWidth.Add(width); }
                    }
                }
            }
            foreach (var t in _tracks) if (t.kind == TrackKind.Road) Cross(t.Points, false, RoadWidth + 0.6f); // road bridges first (wider)
            Cross(_trail.Points, true, 2.3f);
            foreach (var t in _tracks) if (t.kind != TrackKind.Road) Cross(t.Points, false, 2f);
            for (int i = 0; i < crossings.Count; i++)
                Footbridge(creek, crossings[i].p, crossings[i].dir, wood, post, i == 0, crossingWidth[i]);

            // Bed stones and bank rocks (erosion), a fallen log, reeds, brook voices.
            var stone = GeneratedAssets.Material("CreekStone", new Color(0.42f, 0.41f, 0.38f), 0.35f);
            for (int i = 6; i < n - 6; i += _rng.Next(5, 11))
            {
                Vector3 dir = TerrainUtil.Flat(_creek[i + 1] - _creek[i - 1]).normalized, side = Vector3.Cross(Vector3.up, dir);
                if (PathDistance(_creek[i]) < 6f) continue;
                bool bank = R() < 0.45f;
                Vector3 p = _creek[i] + side * (bank ? R(2.2f, 3.4f) * (R() < 0.5f ? -1f : 1f) : R(-0.9f, 0.9f));
                p.y = TerrainUtil.SampleHeight(p);
                if (_art && _artRocks.Count > 0)
                    PlaceArt(_artRocks, creek, p, bank ? R(0.5f, 1f) : R(0.25f, 0.5f), 0.35f, 15f);
                else
                    MeshPart("Stone", creek, Pick(_rocks), stone, p, Vector3.one * (bank ? R(0.6f, 1.1f) : R(0.3f, 0.6f)), Quaternion.Euler(R(0f, 30f), R(0f, 360f), R(0f, 30f)));
            }
            int logAt = -1;
            for (int i = n / 3; i < n * 2 / 3; i++) if (PathDistance(_creek[i]) > 30f) { logAt = i; break; }
            if (logAt > 0)
            {
                Vector3 dir = TerrainUtil.Flat(_creek[logAt + 1] - _creek[logAt - 1]).normalized;
                Vector3 p = new Vector3(_creek[logAt].x, _creekWater[logAt] + 0.25f, _creek[logAt].z);
                GameObject log;
                if (_art && _artLogs.Count > 0) log = PlaceArt(_artLogs, creek, p, 1f, 0f, 4f);
                else
                {
                    log = new GameObject("Fallen Log");
                    log.transform.SetParent(creek, false);
                    log.transform.position = p;
                    MeshPart("Trunk", log.transform, _cylinder, Pick(_bark), Vector3.zero, new Vector3(0.5f, 3.5f, 0.5f), Quaternion.Euler(0f, 0f, 90f));
                }
                log.name = "Fallen Log (creek crossing)";
                // Forward runs along the creek, so the placeholder trunk (local X) spans it from bank to bank.
                log.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, R(-15f, 15f), 0f);
            }

            var reedSpots = new List<Vector3>();
            for (int i = 4; i < n - 4; i += 2)
            {
                if (R() > 0.55f || PathDistance(_creek[i]) < 5f) continue;
                Vector3 dir = TerrainUtil.Flat(_creek[i + 1] - _creek[i - 1]).normalized, side = Vector3.Cross(Vector3.up, dir);
                reedSpots.Add(_creek[i] + side * R(1.7f, 2.5f) * (R() < 0.5f ? -1f : 1f));
            }
            PlantReeds(creek, reedSpots, "Creek");

            for (int i = 0; i < n; i += 22)
            {
                var src = new GameObject("Brook Sound");
                src.transform.SetParent(creek, false);
                src.transform.position = new Vector3(_creek[i].x, _creekWater[i] + 0.3f, _creek[i].z);
                src.AddComponent<AudioSource>();
                var amb = src.AddComponent<AmbientSound>();
                amb.kind = AmbientKind.Brook;
                amb.volume = 0.35f;
                amb.audibleRadius = 28f;
            }

            // Dawn mist settles in the creek's low reaches (0.9).
            for (int k = 1; k <= 4; k++)
            {
                int i = Mathf.Clamp(n * k / 5, 0, n - 1);
                Mist(creek, new Vector3(_creek[i].x, _creekWater[i] + 0.5f, _creek[i].z), new Vector3(16f, 1.2f, 16f), 0.7f);
            }
            // Water anchors: where deer come to drink at dusk.
            for (int k = 1; k <= 3; k++)
            {
                int i = Mathf.Clamp(n * k / 4, 0, n - 1);
                Vector3 dir = TerrainUtil.Flat(_creek[Mathf.Min(n - 1, i + 1)] - _creek[Mathf.Max(0, i - 1)]).normalized;
                Anchor(creek, ScheduleRole.Water, TerrainUtil.Ground(_creek[i] + Vector3.Cross(Vector3.up, dir) * 4.5f), 10f);
            }

            // Deer come down to drink where the creek runs through open ground.
            int drink = -1; float bestOpen = float.MaxValue;
            for (int i = n / 5; i < n * 4 / 5; i += 4)
            {
                float f = Forest(_creek[i]) + (PathDistance(_creek[i]) < 25f ? 1f : 0f);
                if (f < bestOpen) { bestOpen = f; drink = i; }
            }
            if (drink > 0)
            {
                Vector3 p = TerrainUtil.Ground(_creek[drink]);
                Residence(parent, "Creek-side deer", p, 16f, "white_tailed_deer", "virginia_opossum");
                AddHotspot(parent, "Creek Meadow Hotspot", HotspotType.Meadow, p, 14f);
            }
        }

        // ================================================================== time of day (0.9)

        private void Anchor(Transform parent, ScheduleRole role, Vector3 p, float radius)
        {
            var go = new GameObject("Schedule: " + role);
            go.transform.SetParent(parent, false);
            go.transform.position = p;
            var a = go.AddComponent<ScheduleAnchor>();
            a.role = role;
            a.radius = radius;
        }

        /// <summary>Woodland cover where deer bed down at midday: dense stands away from paths, spread over the reserve.</summary>
        private void BuildWoodlandCover(Transform parent)
        {
            var chosen = new List<Vector3>();
            for (int i = 0; i < 4000 && chosen.Count < 9; i++)
            {
                Vector3 p = RandomPoint();
                if (Forest(p) < 0.6f || PathDistance(p) < 10f || CreekDistance(p) < 5f || TerrainUtil.SlopeDegrees(p) > 25f) continue;
                bool far = true;
                foreach (var c in chosen) if (TerrainUtil.FlatDistance(c, p) < 70f) { far = false; break; }
                if (!far) continue;
                chosen.Add(p);
                Anchor(parent, ScheduleRole.Woodland, p, 14f);
            }
        }

        private Material _mist;

        private void Mist(Transform parent, Vector3 at, Vector3 size, float strength)
        {
            if (_mist == null) _mist = MistMaterial();
            if (_mist == null) return;
            var go = new GameObject("Dawn Mist");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var m = go.AddComponent<MistPatch>();
            m.size = size;
            m.material = _mist;
            m.strength = strength;
        }

        /// <summary>A soft, noisy puff texture and an unlit transparent particle material for the active pipeline.</summary>
        private static Material MistMaterial()
        {
            bool srp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            var shader = srp ? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             : (Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit"));
            if (shader == null) return null;
            var tex = GeneratedAssets.LoadOrCreate("T_MistPuff", () =>
            {
                const int size = 64;
                var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float n = Mathf.PerlinNoise(x * 0.11f, y * 0.11f) * 0.6f + Mathf.PerlinNoise(x * 0.27f + 7f, y * 0.27f) * 0.4f;
                    float a = Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r) * (0.55f + 0.45f * n);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                t.Apply();
                return t;
            });
            string path = GeneratedAssets.Folder + "/M_DawnMist.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            foreach (var prop in new[] { "_BaseMap", "_MainTex" }) if (mat.HasProperty(prop)) mat.SetTexture(prop, tex);
            foreach (var prop in new[] { "_BaseColor", "_Color" }) if (mat.HasProperty(prop)) mat.SetColor(prop, new Color(1f, 1f, 1f, 0f));
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0f));
            if (srp)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
            }
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static bool SegmentsIntersect(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 hit)
        {
            hit = Vector3.zero;
            Vector2 p = new Vector2(a.x, a.z), r = new Vector2(b.x - a.x, b.z - a.z);
            Vector2 q = new Vector2(c.x, c.z), s = new Vector2(d.x - c.x, d.z - c.z);
            float denom = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denom) < 1e-6f) return false;
            Vector2 qp = q - p;
            float t = (qp.x * s.y - qp.y * s.x) / denom, u = (qp.x * r.y - qp.y * r.x) / denom;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            Vector2 h = p + r * t;
            hit = new Vector3(h.x, 0f, h.y);
            return true;
        }

        private void Footbridge(Transform parent, Vector3 at, Vector3 dir, Material wood, Material post, bool panel, float width = 2.3f)
        {
            const float len = 9.5f;
            float hA = TerrainUtil.SampleHeight(at - dir * len * 0.5f), hB = TerrainUtil.SampleHeight(at + dir * len * 0.5f);
            float deck = Mathf.Max(hA, hB) + 0.1f;
            Clear(at, 6f);
            var bridge = PropBuilder.Group("Footbridge", parent, new Vector3(at.x, deck, at.z), Quaternion.LookRotation(dir));
            PropBuilder.Box("Deck", bridge, new Vector3(0f, -0.05f, 0f), new Vector3(width, 0.1f, len), wood);
            for (float z = -len * 0.5f + 0.4f; z < len * 0.5f; z += 0.9f)
                PropBuilder.Box("Plank Gap", bridge, new Vector3(0f, 0.003f, z), new Vector3(width, 0.005f, 0.04f), post);
            PropBuilder.Box("Stringer L", bridge, new Vector3(-width * 0.5f + 0.1f, -0.22f, 0f), new Vector3(0.15f, 0.25f, len), post);
            PropBuilder.Box("Stringer R", bridge, new Vector3(width * 0.5f - 0.1f, -0.22f, 0f), new Vector3(0.15f, 0.25f, len), post);
            foreach (float x in new[] { -width * 0.5f, width * 0.5f })
            {
                for (float z = -len * 0.5f + 1f; z <= len * 0.5f - 0.9f; z += 2.5f)
                    PropBuilder.Box("Rail Post", bridge, new Vector3(x, 0.5f, z), new Vector3(0.1f, 1f, 0.1f), post);
                PropBuilder.Box("Rail", bridge, new Vector3(x, 0.98f, 0f), new Vector3(0.08f, 0.08f, len - 1.6f), wood);
                PropBuilder.AddCollider(bridge, new Vector3(x, 0.5f, 0f), new Vector3(0.1f, 1f, len - 1.6f));
            }
            // The deck is a separate collider the bike rides on.
            var deckCol = new GameObject("Deck Collider");
            deckCol.transform.SetParent(bridge, false);
            var box = deckCol.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.05f, 0f);
            box.size = new Vector3(width, 0.1f, len);
            deckCol.AddComponent<RideableSurface>();
            AddKeepOut(at, 5f);

            if (panel)
                InterpretivePanel(parent, TerrainUtil.Ground(at - dir * (len * 0.5f + 2f) + Vector3.Cross(Vector3.up, dir) * 2.4f), -dir,
                    SignIcon.Water, "WILLOW CREEK", "From the ridge to the pond.\nDeer follow it to drink.", wood, post);
        }

        private void PlantReeds(Transform group, List<Vector3> clumps, string name)
        {
            if (clumps.Count == 0) return;
            var stems = new List<CombineInstance>();
            var heads = new List<CombineInstance>();
            Mesh capsule = PrimitiveMeshes.Get(PrimitiveType.Capsule);
            foreach (var c in clumps)
            {
                int k = _rng.Next(4, 9);
                for (int i = 0; i < k; i++)
                {
                    Vector3 p = new Vector3(c.x + R(-0.45f, 0.45f), 0f, c.z + R(-0.45f, 0.45f));
                    p.y = TerrainUtil.SampleHeight(p);
                    float h = R(0.9f, 1.6f);
                    var rot = Quaternion.Euler(R(-9f, 9f), R(0f, 360f), R(-9f, 9f));
                    stems.Add(new CombineInstance { mesh = _cylinder, transform = Matrix4x4.TRS(p + rot * Vector3.up * (h * 0.5f), rot, new Vector3(0.02f, h * 0.5f, 0.02f)) });
                    if (R() < 0.3f)
                        heads.Add(new CombineInstance { mesh = capsule, transform = Matrix4x4.TRS(p + rot * Vector3.up * (h * 0.82f), rot, new Vector3(0.045f, 0.1f, 0.045f)) });
                }
            }
            CombinedStatic(group, name + " Reeds", stems, new Color(0.4f, 0.46f, 0.22f));
            CombinedStatic(group, name + " Cattails", heads, new Color(0.3f, 0.19f, 0.1f));
        }

        private void CombinedStatic(Transform group, string name, List<CombineInstance> parts, Color colour)
        {
            if (parts.Count == 0) return;
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh = GeneratedAssets.Save(mesh, "Mesh_" + name.Replace(" ", ""));
            var go = new GameObject(name);
            go.transform.SetParent(group, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GeneratedAssets.Material(name.Replace(" ", ""), colour);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // ================================================================== storytelling

        /// <summary>An angled interpretive lectern: a title and a few lines, like the ones on real reserve trails.</summary>
        /// <remarks>1.2: a pictogram, a bold title and two short lines (<see cref="SignKit.Panel"/>), not a paragraph.</remarks>
        private void InterpretivePanel(Transform parent, Vector3 pos, Vector3 facing, SignIcon icon, string title, string body, Material wood, Material post)
        {
            SignKit.Panel(parent, pos, facing, icon, title, body, wood, post);
            Clear(pos, 1.5f);
            AddKeepOut(pos, 1.5f);
        }

        /// <summary>Numbered posts every ~100 m along each track, capped in the track's colour.</summary>
        private void BuildTrackMarkers(Transform parent, Material post)
        {
            var colours = new[] { new Color(0.85f, 0.55f, 0.15f), new Color(0.25f, 0.5f, 0.75f), new Color(0.35f, 0.6f, 0.25f), new Color(0.75f, 0.3f, 0.25f) };
            foreach (var s in _sites)
            {
                var pts = s.track.Points;
                var cap = GeneratedAssets.Material("TrackCap" + s.number, colours[(s.number - 1) % colours.Length], 0.3f);
                float acc = 0f, next = 100f;
                for (int i = 1; i < pts.Count - 1; i++)
                {
                    acc += Vector3.Distance(pts[i - 1], pts[i]);
                    if (acc < next || s.track.Length - acc < 40f) continue;
                    next += 100f;
                    Vector3 dir = TerrainUtil.Flat(pts[i + 1] - pts[i - 1]).normalized, side = Vector3.Cross(Vector3.up, dir);
                    Vector3 p = TerrainUtil.Ground(pts[i] + side * 2f);
                    if (CreekDistance(p) < 5f || PadDistance(p) < 2f) continue;
                    var g = PropBuilder.Group($"Track Marker {s.number}", parent, p, Quaternion.LookRotation(-dir));
                    PropBuilder.Box("Post", g, new Vector3(0f, 0.5f, 0f), new Vector3(0.13f, 1f, 0.13f), post);
                    PropBuilder.Box("Cap", g, new Vector3(0f, 1.02f, 0f), new Vector3(0.17f, 0.06f, 0.17f), cap);
                    PropBuilder.Label(g, new Vector3(0f, 0.82f, 0.07f), s.number.ToString(), 0.028f);
                    PropBuilder.AddCollider(g, new Vector3(0f, 0.5f, 0f), new Vector3(0.13f, 1f, 0.13f));
                    Clear(p, 1f);
                }
            }
        }

        private void Residence(Transform parent, string label, Vector3 p, float radius, params string[] species)
        {
            var go = new GameObject("Residence: " + label);
            go.transform.SetParent(parent, false);
            go.transform.position = p;
            var r = go.AddComponent<WildlifeResidence>();
            r.label = label;
            r.radius = radius;
            r.species = species;
        }

        /// <summary>Encounter variety from placement alone: deer that cross a track, cottontails along the meadow edge.</summary>
        private void BuildResidences(Transform parent, Material wood, Material post)
        {
            // Deer crossing: the most open stretch of the longest track, away from both ends.
            ReserveTrack longest = null;
            foreach (var t in _tracks) if (longest == null || t.Length > longest.Length) longest = t;
            if (longest != null && longest.Points.Count > 20)
            {
                var pts = longest.Points;
                int best = -1; float bestScore = float.MaxValue;
                for (int i = pts.Count / 4; i < pts.Count * 3 / 4; i += 2)
                {
                    float score = Forest(pts[i]) + (PadDistance(pts[i]) < 20f ? 2f : 0f) + (CreekDistance(pts[i]) < 15f ? 2f : 0f);
                    if (score < bestScore) { bestScore = score; best = i; }
                }
                if (best > 0)
                {
                    Vector3 p = pts[best];
                    Residence(parent, "Deer crossing", p, 22f, "white_tailed_deer");
                    AddHotspot(parent, "Deer Crossing Meadow", HotspotType.Meadow, p, 18f);
                    Vector3 dir = TerrainUtil.Flat(pts[Mathf.Min(pts.Count - 1, best - 12 + 1)] - pts[Mathf.Max(0, best - 12 - 1)]).normalized;
                    int signAt = Mathf.Max(1, best - 14);
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    Plank(parent, "DEER CROSSING", "slow down", new[] { SignIcon.Paw }, 0, TerrainUtil.Ground(pts[signAt] + side * 2.2f), Quaternion.LookRotation(-dir), post);
                }
            }

            // Cottontails on the blind meadow's brushy edge.
            var blind = _sites.Find(s => s.kind == DestinationKind.ObservationBlind);
            if (blind != null)
            {
                Vector3 side = Vector3.Cross(Vector3.up, blind.facing);
                Vector3 edge = TerrainUtil.Ground(blind.centre + side * blind.radius * 0.75f);
                Residence(parent, "Meadow-edge cottontails", edge, 10f, "eastern_cottontail", "groundhog");
            }
        }
    }
}
