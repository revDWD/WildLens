using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Scatters trees, bushes, rocks, logs and grass around the trail loop.
    /// A low-frequency "forest" noise field creates woodland patches and clearings; the inside of the loop is
    /// kept as open meadow and the trail corridor is kept clear, giving readable sight-lines for photography.
    /// Uses assigned prefabs or terrain tree prototypes where available, otherwise generated placeholders.
    /// </summary>
    public partial class VegetationBuilder
    {
        private readonly Terrain _terrain;
        private readonly TrailPath _trail;
        private readonly TrailBuilder.Loop _loop;
        private readonly SliceBuildSettings _s;
        private readonly System.Random _rng;
        private Vector2 _min, _max; // grows to cover the reserve (PlanReserve)
        private readonly float _noiseOffset;
        private readonly Dictionary<Vector2Int, List<Vector3>> _occupied = new Dictionary<Vector2Int, List<Vector3>>();
        private const float OccupancyCell = 4f;

        // Shared placeholder assets
        private Mesh _cone, _cylinder;
        private Mesh[] _canopies, _rocks;
        private Material[] _bark, _conifer, _leaf, _bush, _stone;
        private Material _pineBark;
        private List<GameObject> _artFerns = new List<GameObject>(), _artShrubs = new List<GameObject>(),
            _artRocks = new List<GameObject>(), _artBoulders = new List<GameObject>(),
            _artStumps = new List<GameObject>(), _artLogs = new List<GameObject>(), _artOutcrops = new List<GameObject>();
        private bool _art;
        public int FernsPlaced { get; private set; }
        private readonly List<GameObject> _treeObjects = new List<GameObject>();
        private readonly List<GameObject> _bushObjects = new List<GameObject>();

        public int TreesPlaced { get; private set; }
        public int BushesPlaced { get; private set; }
        public int RocksPlaced { get; private set; }
        public int GrassPlaced { get; private set; }

        public VegetationBuilder(Terrain terrain, TrailPath trail, TrailBuilder.Loop loop, SliceBuildSettings settings)
        {
            _terrain = terrain;
            _trail = trail;
            _loop = loop;
            _s = settings;
            _rng = new System.Random(settings.seed * 31 + 5);
            _noiseOffset = (float)_rng.NextDouble() * 500f;

            Vector3 tPos = terrain.transform.position, size = terrain.terrainData.size;
            float ex = loop.radiusX * 1.2f + settings.scatterMargin, ez = loop.radiusZ * 1.2f + settings.scatterMargin;
            _min = new Vector2(Mathf.Max(tPos.x + 2f, loop.center.x - ex), Mathf.Max(tPos.z + 2f, loop.center.z - ez));
            _max = new Vector2(Mathf.Min(tPos.x + size.x - 2f, loop.center.x + ex), Mathf.Min(tPos.z + size.z - 2f, loop.center.z + ez));
            PrepareAssets();
        }

        // ------------------------------------------------------------------ fields

        private float R() => (float)_rng.NextDouble();
        private float R(float a, float b) => a + R() * (b - a);

        private Vector3 RandomPoint() => TerrainUtil.Ground(new Vector3(R(_min.x, _max.x), 0f, R(_min.y, _max.y)));

        /// <summary>Normalised elliptical distance from the loop centre (1 = on the loop).</summary>
        private float LoopRadius(Vector3 p)
        {
            float dx = (p.x - _loop.center.x) / _loop.radiusX, dz = (p.z - _loop.center.z) / _loop.radiusZ;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Forest density 0..1 (higher = woodland).</summary>
        private float Forest(Vector3 p)
        {
            float n = Mathf.PerlinNoise(_noiseOffset + p.x * 0.014f, _noiseOffset + p.z * 0.014f) * 0.75f
                      + Mathf.PerlinNoise(_noiseOffset + p.x * 0.05f, _noiseOffset - p.z * 0.05f) * 0.25f;
            float r = LoopRadius(p);
            if (_s.openMeadowInsideLoop) n -= Mathf.Clamp01((0.85f - r) / 0.4f) * 0.55f;
            if (r > 1.15f) n += 0.15f;
            n += ReserveForestBias(p);
            return Mathf.Clamp01(n);
        }

        private bool IsFree(Vector3 p, float spacing)
        {
            if (InKeepOut(p) || CreekDistance(p) < 3.6f) return false;
            var c = new Vector2Int(Mathf.FloorToInt(p.x / OccupancyCell), Mathf.FloorToInt(p.z / OccupancyCell));
            int r = Mathf.CeilToInt(spacing / OccupancyCell);
            for (int x = -r; x <= r; x++)
            for (int z = -r; z <= r; z++)
            {
                if (!_occupied.TryGetValue(new Vector2Int(c.x + x, c.y + z), out var list)) continue;
                foreach (var q in list) if (TerrainUtil.FlatDistance(p, q) < spacing) return false;
            }
            return true;
        }

        private void Occupy(Vector3 p)
        {
            var c = new Vector2Int(Mathf.FloorToInt(p.x / OccupancyCell), Mathf.FloorToInt(p.z / OccupancyCell));
            if (!_occupied.TryGetValue(c, out var list)) _occupied[c] = list = new List<Vector3>();
            list.Add(p);
        }

        // ------------------------------------------------------------------ assets

        private void PrepareAssets()
        {
            // Open cone (no base cap): from below you see branches, not a black disc (0.7 fix).
            _cone = GeneratedAssets.LoadOrCreate("Mesh_ConeOpen", () => ProceduralMeshes.Cone(12, cap: false));
            _cylinder = PrimitiveMeshes.Get(PrimitiveType.Cylinder);
            _canopies = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                _canopies[i] = GeneratedAssets.LoadOrCreate("Mesh_Canopy" + i, () => ProceduralMeshes.Blob(10 + k, 0.35f, 2.2f, false));
            }
            _rocks = new Mesh[4];
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                _rocks[i] = GeneratedAssets.LoadOrCreate("Mesh_Rock" + i, () => ProceduralMeshes.Blob(50 + k, 0.6f, 1.6f, true));
            }

            _bark = new[]
            {
                GeneratedAssets.Material("Bark0", new Color(0.33f, 0.25f, 0.18f)),
                GeneratedAssets.Material("Bark1", new Color(0.4f, 0.32f, 0.24f)),
            };
            _conifer = new[]
            {
                GeneratedAssets.Material("Conifer0", new Color(0.14f, 0.28f, 0.16f)),
                GeneratedAssets.Material("Conifer1", new Color(0.18f, 0.33f, 0.18f)),
            };
            _leaf = new[]
            {
                GeneratedAssets.Material("Leaf0", new Color(0.3f, 0.45f, 0.19f)),
                GeneratedAssets.Material("Leaf1", new Color(0.37f, 0.5f, 0.21f)),
                GeneratedAssets.Material("Leaf2", new Color(0.44f, 0.52f, 0.2f)),
            };
            _bush = new[]
            {
                GeneratedAssets.Material("Bush0", new Color(0.22f, 0.36f, 0.16f)),
                GeneratedAssets.Material("Bush1", new Color(0.29f, 0.4f, 0.17f)),
            };
            _stone = new[]
            {
                GeneratedAssets.Material("Stone0", new Color(0.5f, 0.49f, 0.46f), 0.2f),
                GeneratedAssets.Material("Stone1", new Color(0.42f, 0.42f, 0.4f), 0.2f),
                GeneratedAssets.Material("Stone2", new Color(0.46f, 0.47f, 0.41f), 0.15f),
            };

            // Scanned art (CC0, shipped in the package): replaces flat colours and primitive props where available.
            _art = _s.useScannedArt && ArtLibrary.Import();
            if (!_art) { MergeKitProps(); return; }
            var barkA = ArtLibrary.SurfaceMaterial("BarkBroadleaf", "Surfaces/bark_brown_02_diff_1k.jpg", "Surfaces/bark_brown_02_nor_gl_1k.jpg", Color.white, new Vector2(2f, 3f));
            var barkB = ArtLibrary.SurfaceMaterial("BarkBroadleafDark", "Surfaces/bark_brown_02_diff_1k.jpg", "Surfaces/bark_brown_02_nor_gl_1k.jpg", new Color(0.82f, 0.78f, 0.74f), new Vector2(2f, 3f));
            if (barkA != null && barkB != null) _bark = new[] { barkA, barkB };
            _pineBark = ArtLibrary.SurfaceMaterial("BarkPine", "Surfaces/pine_bark_diff_1k.jpg", "Surfaces/pine_bark_nor_gl_1k.jpg", Color.white, new Vector2(2f, 4f));
            var rockA = ArtLibrary.SurfaceMaterial("MossyRock", "Surfaces/mossy_rock_diff_1k.jpg", "Surfaces/mossy_rock_nor_gl_1k.jpg", Color.white, Vector2.one, 0.15f);
            var rockB = ArtLibrary.SurfaceMaterial("MossyRockGrey", "Surfaces/mossy_rock_diff_1k.jpg", "Surfaces/mossy_rock_nor_gl_1k.jpg", new Color(0.86f, 0.86f, 0.84f), Vector2.one, 0.15f);
            if (rockA != null && rockB != null) _stone = new[] { rockA, rockB };
            _artFerns = ArtLibrary.Prefabs(ArtKind.Fern);
            _artShrubs = ArtLibrary.Prefabs(ArtKind.Shrub);
            _artRocks = ArtLibrary.Prefabs(ArtKind.RockPiece);
            _artBoulders = ArtLibrary.Prefabs(ArtKind.Boulder);
            _artStumps = ArtLibrary.Prefabs(ArtKind.Stump);
            _artLogs = ArtLibrary.Prefabs(ArtKind.Log);
            _artOutcrops = ArtLibrary.Prefabs(ArtKind.Outcrop);
            PrepareFoliageCards();
            PrepareTreeParts();
            MergeKitProps(); // 1.2: the user's Environment Kit joins (or replaces) the bundled scans
        }

        private T Pick<T>(T[] array) => array[_rng.Next(array.Length)];

        private void MakeStatic(GameObject go)
        {
            if (KeepDynamic(go)) return;
            foreach (var t in go.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>Adds wind sway to a plant's foliage parts and takes them out of static batching (they move).</summary>
        private static void AddSway(GameObject root, List<Transform> parts, float height, float amplitude, float frequency)
        {
            var sway = root.AddComponent<FoliageSway>();
            sway.Configure(parts.ToArray(), height, amplitude, frequency);
        }

        private void MakeStaticExceptSway(GameObject go)
        {
            if (KeepDynamic(go)) return;
            MakeStatic(go);
            var sway = go.GetComponent<FoliageSway>();
            if (sway == null) return;
            // Swaying parts and everything parented to them (foliage cards) must not be static-batched: they move.
            foreach (var part in sway.Parts)
                if (part != null)
                    foreach (var t in part.GetComponentsInChildren<Transform>())
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        /// <summary>
        /// Ecological structure instead of uniform density (0.8). The same tree budget, distributed as:
        ///   dense woodland (forest &gt; 0.62)   closed canopy, tight spacing: the fern-floored interior
        ///   woodland (0.45–0.62)              clumped stands with gaps
        ///   quiet woodland (0.3–0.45)         small groves scattered through grass: transition zones
        ///   open grassland (&lt; 0.3)            the occasional lone tree, so meadows and view corridors stay open
        /// Because placement stops at the budget, packing the dense zones tighter leaves the open ground emptier.
        /// </summary>
        private float TreeChance(Vector3 p, float f)
        {
            float clump = Mathf.PerlinNoise(_noiseOffset * 1.7f + p.x * 0.09f, p.z * 0.09f);
            if (f > 0.62f) return 1f;
            if (f > 0.45f) return Mathf.Lerp(0.25f, 0.9f, Mathf.InverseLerp(0.45f, 0.62f, f)) * (0.4f + clump);
            if (f > 0.3f) return clump > 0.62f ? 0.4f : 0.03f;
            return clump > 0.82f ? 0.1f : 0f;
        }

        private static float TreeSpacing(float f) => f > 0.62f ? 2.2f : f > 0.45f ? 2.8f : 4f;

        /// <summary>
        /// Stops drawing a tree once it is tiny on screen. With the larger reserve (0.7) most trees are far from the
        /// player at any moment; this keeps frame rate steady without visible popping at normal distances.
        /// </summary>
        private static void AddDistanceCull(GameObject tree, float screenHeight)
        {
            if (tree.GetComponentInChildren<LODGroup>() != null) return; // the prefab brings its own LODs
            var lod = tree.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(screenHeight, tree.GetComponentsInChildren<Renderer>()) });
            lod.RecalculateBounds();
        }

        // ---- tree pass (0.9): trees with structure. Read from the trail (trunks, limbs, crown base), at photo distance
        // (ragged crowns, depth) and from overlooks (varied silhouettes and sizes).

        private Mesh _frustumTrunk, _frustumLimb, _frustumSpire;
        private Material _birchBark;

        private void PrepareTreeParts()
        {
            _frustumTrunk = GeneratedAssets.LoadOrCreate("Mesh_FrustumTrunk", () => ProceduralMeshes.Frustum(10, 0.7f));
            _frustumLimb = GeneratedAssets.LoadOrCreate("Mesh_FrustumLimb", () => ProceduralMeshes.Frustum(8, 0.4f));
            _frustumSpire = GeneratedAssets.LoadOrCreate("Mesh_FrustumSpire", () => ProceduralMeshes.Frustum(8, 0.08f));
            _birchBark = GeneratedAssets.Material("BirchBark", new Color(0.66f, 0.65f, 0.6f), 0.08f); // 1.2: grey-white, not glowing
        }

        /// <summary>A tapered tube from <paramref name="from"/> along <paramref name="dir"/> (local to the tree root).</summary>
        private void Limb(Transform root, string name, Mesh mesh, Material mat, Vector3 from, Vector3 dir, float length, float radius)
        {
            MeshPart(name, root, mesh, mat, from, new Vector3(radius, length, radius), Quaternion.FromToRotation(Vector3.up, dir.normalized));
        }

        /// <summary>A leaf cluster: dark core inside a shell of leaf cards. Returns the swaying part.</summary>
        /// <summary>A leaf cluster drawn from its own random source (1.2), so extra crown detail never shifts placement.</summary>
        private Transform Cluster(Transform root, Vector3 at, float size, float squash, Material cards, System.Random rng)
        {
            var core = MeshPart("Crown", root, _canopies[rng.Next(_canopies.Length)], _leafCore, at,
                new Vector3(size, size * squash, size) * 0.82f, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            var go = new GameObject("Cards");
            go.transform.SetParent(core.transform, false);
            go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = FoliageCards.Canopy(rng);
            go.AddComponent<MeshRenderer>().sharedMaterial = cards;
            return core.transform;
        }

        private Transform Cluster(Transform root, Vector3 at, float size, float squash, Material cards)
        {
            var core = MeshPart("Crown", root, Pick(_canopies), _leafCore, at,
                new Vector3(size, size * squash, size) * 0.82f, Quaternion.Euler(0f, R(0f, 360f), 0f));
            AddCards(core.transform, FoliageCards.Canopy(_rng), cards);
            return core.transform;
        }

        /// <summary>
        /// A fir/spruce that grows like one: a tapering trunk to the tip, a crown of branch whorls (cone-shaped but ragged),
        /// a slim dark core for depth, and a few dead lower branches below the living crown.
        /// </summary>
        private GameObject WhorlConifer(Transform parent, Vector3 p)
        {
            float h = R(9f, 17f);
            float trunkR = 0.14f + h * 0.012f;
            var root = new GameObject("Conifer");
            root.transform.SetParent(parent, false);
            root.transform.position = p;
            root.transform.rotation = Quaternion.Euler(R(-1.5f, 1.5f), R(0f, 360f), R(-1.5f, 1.5f));
            var bark = _pineBark != null ? _pineBark : Pick(_bark);
            MeshPart("Trunk", root.transform, _frustumSpire, bark, Vector3.zero, new Vector3(trunkR, h * 0.97f, trunkR), Quaternion.identity);

            // Two forms (1.0): fir/spruce (crown most of the way down) and pine (tall bare trunk, short broad crown).
            bool pine = R() < 0.3f;
            if (pine) root.name = "Pine";
            float crownBase = pine ? h * R(0.52f, 0.66f) : Mathf.Max(2.5f, h * R(0.12f, 0.3f));
            float crownH = h - crownBase + 0.4f;
            float crownR = pine ? crownH * R(0.42f, 0.55f) : crownH * R(0.24f, 0.33f);
            var parts = new List<Transform>();
            var core = MeshPart("Core", root.transform, _cone, _coniferCore, new Vector3(0f, crownBase + crownH * 0.05f, 0f),
                new Vector3(crownR * 0.32f, crownH * 0.88f, crownR * 0.32f), Quaternion.identity);
            parts.Add(core.transform);
            var crown = MeshPart("Crown", root.transform, FoliageCards.ConiferCrown(_rng), _branchCards[_rng.Next(_branchCards.Length)],
                new Vector3(0f, crownBase, 0f), new Vector3(crownR, crownH, crownR), Quaternion.Euler(0f, R(0f, 360f), 0f));
            parts.Add(crown.transform);
            // Dead lower branches: bare twigs below the living crown, as on real forest conifers.
            int dead = _rng.Next(2, 5);
            for (int i = 0; i < dead; i++)
            {
                float y = R(1.9f, crownBase);
                float a = R(0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(a), R(-0.35f, -0.1f), Mathf.Sin(a));
                Limb(root.transform, "Dead Branch", _frustumLimb, bark, new Vector3(0f, y, 0f), dir, R(0.6f, 1.3f), trunkR * 0.18f);
            }
            AddSway(root, parts, h, R(1.1f, 1.7f), R(0.26f, 0.34f));

            var trunkCol = root.AddComponent<CapsuleCollider>();
            trunkCol.center = new Vector3(0f, 1.5f, 0f); trunkCol.height = 3f; trunkCol.radius = trunkR;
            var foliage = root.AddComponent<CapsuleCollider>();
            foliage.isTrigger = true;
            foliage.center = new Vector3(0f, crownBase + crownH * 0.45f, 0f); foliage.height = crownH; foliage.radius = crownR * 0.7f;
            AddFeature(root, FeatureKind.Tree, trunkR, h * 0.95f);
            return root;
        }

        /// <summary>
        /// Broadleaf trees with real structure, in three forms so stands vary:
        ///   spreading (oak-like)   short trunk, 3–5 limbs angled out, a wide, low crown of clusters
        ///   ovate (maple-like)     taller trunk, 2–3 steep limbs, a narrower, taller crown
        ///   multi-stem (birch-like) 2–3 slender pale stems from the ground, light crowns
        /// Limbs show beneath and between the leaf clusters, so a tree reads as a tree from the trail.
        /// </summary>
        private GameObject BranchingBroadleaf(Transform parent, Vector3 p)
        {
            float roll = R();
            int form = roll < 0.14f ? 2 : roll < 0.45f ? 1 : 0;
            float h = form == 2 ? R(8f, 13f) : R(8f, 14f);
            float trunkR = form == 2 ? 0.08f + h * 0.006f : 0.14f + h * 0.016f;
            var root = new GameObject(form == 0 ? "Broadleaf (spreading)" : form == 1 ? "Broadleaf (ovate)" : "Birch");
            root.transform.SetParent(parent, false);
            root.transform.position = p;
            root.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            var bark = form == 2 ? _birchBark : Pick(_bark);
            var cards = form == 2 ? _leafCards[2 % _leafCards.Length] : _leafCards[_rng.Next(_leafCards.Length)];
            var parts = new List<Transform>();
            float top = 0f, crownW;
            Vector3 crownCentre;

            if (form == 2)
            {
                int stems = _rng.Next(2, 4);
                crownW = h * 0.3f;
                crownCentre = Vector3.up * h * 0.75f;
                for (int sIdx = 0; sIdx < stems; sIdx++)
                {
                    float az = sIdx * 360f / stems + R(-30f, 30f);
                    Vector3 dir = Quaternion.Euler(0f, az, 0f) * Quaternion.Euler(R(4f, 11f), 0f, 0f) * Vector3.up;
                    float hs = h * R(0.78f, 1f);
                    Vector3 basePt = Quaternion.Euler(0f, az, 0f) * Vector3.forward * 0.12f;
                    Limb(root.transform, "Stem", _frustumLimb, bark, basePt, dir, hs, trunkR);
                    foreach (float t in new[] { 0.62f, 0.8f, 0.97f })
                    {
                        Vector3 at = basePt + dir * hs * t + new Vector3(R(-0.4f, 0.4f), 0f, R(-0.4f, 0.4f));
                        parts.Add(Cluster(root.transform, at, h * R(0.13f, 0.19f) * (1.15f - t * 0.4f), R(0.95f, 1.2f), cards));
                        top = Mathf.Max(top, at.y + h * 0.08f);
                    }
                }
            }
            else
            {
                float fork = h * (form == 0 ? R(0.3f, 0.4f) : R(0.42f, 0.52f));
                if (form == 1) fork *= 0.78f; // 1.4: crown starts lower (a third of the height): no bare-pole lollipops
                Vector3 lean = new Vector3(R(-0.06f, 0.06f), 1f, R(-0.06f, 0.06f));
                Limb(root.transform, "Trunk", _frustumTrunk, bark, Vector3.zero, lean, fork + 0.3f, trunkR);
                Vector3 forkPt = lean.normalized * fork;
                crownW = h * (form == 0 ? R(0.58f, 0.7f) : R(0.38f, 0.46f));
                int limbs = form == 0 ? _rng.Next(3, 6) : _rng.Next(2, 4);
                float spread = form == 0 ? R(38f, 55f) : R(16f, 28f);
                if (form == 0) spread *= 0.8f; // 1.2: limbs rise more steeply: a dome, not an umbrella (read as acacia)
                float az0 = R(0f, 360f);
                var limbEnds = new List<(Vector3 dir, float len)>();
                for (int i = 0; i < limbs; i++)
                {
                    float az = az0 + i * 360f / limbs + R(-22f, 22f);
                    Vector3 dir = Quaternion.Euler(0f, az, 0f) * Quaternion.Euler(spread + R(-10f, 10f), 0f, 0f) * Vector3.up;
                    float len = (h - fork) * R(0.55f, 0.78f);
                    Limb(root.transform, "Limb", _frustumLimb, bark, forkPt - dir * 0.15f, dir, len, trunkR * 0.62f);
                    Vector3 end = forkPt + dir * len;
                    float size = crownW * R(0.4f, 0.55f);
                    float squash = form == 0 ? R(0.7f, 0.85f) : R(0.95f, 1.15f);
                    if (form == 0) squash = 0.6f + squash * 0.45f; // 1.2: rounder clusters (0.9–1.0), no flat-topped puffs
                    parts.Add(Cluster(root.transform, end, size, squash, cards));
                    top = Mathf.Max(top, end.y + size * 0.35f);
                    limbEnds.Add((dir, len));
                }
                // 1.2: fill the crown from its own random source (placement unchanged): clusters part-way along each limb
                // close the gaps between the limb tips, and the spreading form gets a raised top, so silhouettes read as
                // temperate broadleaf crowns rather than flat-topped savanna trees.
                var detail = new System.Random((Mathf.RoundToInt(p.x * 10f) * 73856093) ^ (Mathf.RoundToInt(p.z * 10f) * 19349663) ^ _s.seed);
                float D(float a, float b) => a + (float)detail.NextDouble() * (b - a);
                for (int li = 0; li < limbEnds.Count; li += limbEnds.Count <= 3 ? 1 : 2) // few limbs: every one; else alternate
                {
                    var (dir, len) = limbEnds[li];
                    Vector3 mid = forkPt + dir * len * D(0.5f, 0.68f) + new Vector3(D(-0.3f, 0.3f), D(0f, 0.4f), D(-0.3f, 0.3f));
                    parts.Add(Cluster(root.transform, mid, crownW * D(0.32f, 0.42f), D(0.9f, 1.05f), cards, detail));
                }
                // A leading cluster over the fork fills the crown's centre.
                Vector3 lead = forkPt + Vector3.up * (h - fork) * R(0.65f, 0.8f);
                float leadSize = crownW * (form == 0 ? 0.5f : 0.58f);
                parts.Add(Cluster(root.transform, lead, leadSize, form == 0 ? 0.95f : 1.15f, cards));
                top = Mathf.Max(top, lead.y + leadSize * 0.4f);
                if (form == 0)
                    for (int k = 0; k < 1; k++)
                    {
                        float a = D(0f, Mathf.PI * 2f);
                        Vector3 cap = lead + new Vector3(Mathf.Cos(a) * crownW * 0.18f, leadSize * D(0.3f, 0.45f), Mathf.Sin(a) * crownW * 0.18f);
                        float capSize = crownW * D(0.32f, 0.4f);
                        parts.Add(Cluster(root.transform, cap, capSize, D(0.85f, 1f), cards, detail));
                        top = Mathf.Max(top, cap.y + capSize * 0.35f);
                    }
                crownCentre = (forkPt + lead) * 0.5f + Vector3.up * (h - fork) * 0.15f;
            }
            AddSway(root, parts, h, R(1.3f, 2.1f), R(0.3f, 0.42f));

            var trunkCol = root.AddComponent<CapsuleCollider>();
            trunkCol.center = new Vector3(0f, 1.5f, 0f); trunkCol.height = 3f; trunkCol.radius = Mathf.Max(0.12f, trunkR);
            var canopy = root.AddComponent<SphereCollider>();
            canopy.isTrigger = true;
            canopy.center = crownCentre; canopy.radius = crownW * 0.45f;
            AddFeature(root, FeatureKind.Tree, trunkR, Mathf.Max(top, h * 0.85f));
            return root;
        }

        // ---- foliage cards (0.6): alpha-cut leaf/branch cards over a darker core volume

        private bool _cards;
        private Material[] _leafCards, _branchCards;
        private Material _leafCore, _coniferCore;

        private void PrepareFoliageCards()
        {
            _cards = _art && FoliageCards.Available;
            if (!_cards) return;
            _leafCards = new[]
            {
                FoliageCards.LeafMaterial("A", new Color(1f, 1f, 1f)),
                FoliageCards.LeafMaterial("B", new Color(0.88f, 0.96f, 0.82f)),
                FoliageCards.LeafMaterial("C", new Color(1.05f, 1f, 0.82f)),
                FoliageCards.LeafMaterial("D", new Color(0.8f, 0.94f, 0.9f)),   // 1.0: blue-green (shade-tolerant species)
                FoliageCards.LeafMaterial("E", new Color(1.08f, 1.06f, 0.72f)), // 1.0: fresh yellow-green (young growth)
            };
            _branchCards = new[]
            {
                FoliageCards.BranchMaterial("A", new Color(0.95f, 1f, 0.95f)),
                FoliageCards.BranchMaterial("B", new Color(0.82f, 0.9f, 0.85f)),
            };
            // Dark cores give depth in the gaps between cards, like shaded inner foliage.
            _leafCore = GeneratedAssets.Material("LeafCore", new Color(0.12f, 0.19f, 0.08f));
            _coniferCore = GeneratedAssets.Material("ConiferCore", new Color(0.07f, 0.13f, 0.08f));
        }

        private void AddCards(Transform part, Mesh mesh, Material mat)
        {
            var go = new GameObject("Cards");
            go.transform.SetParent(part, false);
            go.transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
        }

        private static GameObject MeshPart(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Places a scanned-art prefab (which already carries colliders and wildlife data), sunk slightly and tilted to sit naturally.</summary>
        private GameObject PlaceArt(List<GameObject> options, Transform parent, Vector3 p, float scale, float sink, float tilt)
        {
            var prefab = options[_rng.Next(options.Count)];
            var go = InstantiatePrefab(prefab, parent, p, scale);
            if (_kitProps.TryGetValue(prefab, out var kitKind)) EnsurePropGameplay(go, kitKind);
            var f = go.GetComponent<EnvironmentFeature>();
            float h = f != null ? f.height * scale : 0f;
            go.transform.position = p - Vector3.up * h * sink;
            go.transform.rotation = Quaternion.Euler(R(-tilt, tilt), R(0f, 360f), R(-tilt, tilt));
            MakeStatic(go);
            return go;
        }

        private GameObject InstantiatePrefab(GameObject prefab, Transform parent, Vector3 pos, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            go.transform.localScale = prefab.transform.localScale * scale;
            return go;
        }

        private static float RenderedHeight(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return 1f;
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return Mathf.Max(0.5f, b.max.y - go.transform.position.y);
        }

        private static void AddFeature(GameObject go, FeatureKind kind, float radius, float height)
        {
            var f = go.AddComponent<EnvironmentFeature>();
            f.kind = kind;
            f.radius = radius;
            f.height = height;
        }

        // ------------------------------------------------------------------ trees

        public void BuildTrees(Transform parent)
        {
            var prefabs = new List<GameObject>();
            foreach (var p in _s.treePrefabs) if (p != null) prefabs.Add(p);
            if (prefabs.Count == 0 && _s.useTerrainTreePrototypes && _terrain.terrainData.treePrototypes != null)
                foreach (var proto in _terrain.terrainData.treePrototypes)
                    if (proto != null && proto.prefab != null) prefabs.Add(proto.prefab);

            int attempts = Scaled(_s.treeCount) * 15;
            float corridor = _s.trailWidth * 0.5f + 2.5f;
            while (TreesPlaced < Scaled(_s.treeCount) && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                if (PathDistance(p) < corridor) continue;
                if (HasRoads && RoadDistance(p) < _s.trailWidth * 0.5f + 7f) continue; // 1.0: open sight lines along the road
                float f = Forest(p);
                if (R() > TreeChance(p, f)) continue;
                if (TerrainUtil.SlopeDegrees(p) > 38f || !IsFree(p, TreeSpacing(f))) continue;

                GameObject tree;
                if (prefabs.Count > 0)
                {
                    tree = InstantiatePrefab(prefabs[_rng.Next(prefabs.Count)], parent, p, R(0.85f, 1.25f));
                    AddFeature(tree, FeatureKind.Tree, 0.3f * tree.transform.localScale.x, RenderedHeight(tree));
                }
                else
                {
                    bool conifer = Mathf.PerlinNoise(_noiseOffset * 0.5f + p.x * 0.03f, p.z * 0.03f) > 0.5f;
                    // 1.2: with an Environment Kit, birches and willows crowd Willow Creek.
                    tree = HasKitTrees && CreekDistance(p) < 16f && R() < 0.75f ? KitTree(TreeRole.Riparian, parent, p) : null;
                    if (tree == null) tree = conifer ? Conifer(parent, p) : Deciduous(parent, p);
                }
                // 1.0: young trees where woodland thins into grassland, so edges grade instead of stopping dead.
                bool youngEdge = prefabs.Count == 0 && f > 0.3f && f < 0.52f && R() < 0.38f;
                if (youngEdge && IsKit(tree) && KitTreeOptions(TreeRole.Young).Count > 0)
                {
                    Object.DestroyImmediate(tree); // the kit has real saplings: use one as authored
                    KitTreesPlaced--;
                    tree = KitTree(TreeRole.Young, parent, p);
                }
                else if (youngEdge)
                {
                    float k = R(0.42f, 0.65f);
                    tree.transform.localScale *= k;
                    tree.name = "Young " + tree.name;
                    var feature = tree.GetComponent<EnvironmentFeature>();
                    if (feature != null) { feature.radius *= k; feature.height *= k; }
                }
                MakeStaticExceptSway(tree);
                if (prefabs.Count == 0) AddDistanceCull(tree, 0.012f);
                Occupy(p);
                TreesPlaced++;
                _treeObjects.Add(tree);
            }
        }

        private GameObject Conifer(Transform parent, Vector3 p)
        {
            if (HasKitTrees) { var kit = KitTree(TreeRole.Conifer, parent, p); if (kit != null) return kit; }
            if (_cards) return WhorlConifer(parent, p);
            float h = R(8f, 15f);
            float trunkR = 0.16f + h * 0.012f;
            var root = new GameObject("Conifer");
            root.transform.SetParent(parent, false);
            root.transform.position = p;
            root.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);

            float trunkLen = h * 0.35f;
            MeshPart("Trunk", root.transform, _cylinder, _pineBark != null ? _pineBark : Pick(_bark), new Vector3(0f, trunkLen * 0.5f, 0f),
                new Vector3(trunkR * 2f, trunkLen * 0.5f, trunkR * 2f), Quaternion.identity);
            // Stacked, slightly irregular tiers; darker shaded lower tiers, fresher growth at the top.
            int tiers = _rng.Next(4, 6);
            var parts = new List<Transform>();
            for (int i = 0; i < tiers; i++)
            {
                float k = i / (float)(tiers - 1);
                // The lowest tier clears head height so you never walk inside a tree (0.7: seen as a black slab).
                float baseY = Mathf.Lerp(Mathf.Max(2.6f, h * 0.16f), h * 0.72f, k);
                float rad = h * Mathf.Lerp(0.28f, 0.09f, k) * R(0.88f, 1.12f);
                var mat = _cards ? _coniferCore : (k < 0.5f ? _conifer[0] : _conifer[1]);
                float coreScale = _cards ? 0.8f : 1f; // cards extend past a slimmer dark core
                var tier = MeshPart("Foliage" + i, root.transform, _cone, mat, new Vector3(R(-0.1f, 0.1f), baseY, R(-0.1f, 0.1f)),
                    new Vector3(rad * coreScale, h * Mathf.Lerp(0.32f, 0.26f, k), rad * coreScale), Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f)));
                if (_cards) AddCards(tier.transform, FoliageCards.ConiferTier(_rng), _branchCards[k < 0.5f ? 1 : 0]);
                parts.Add(tier.transform);
            }
            AddSway(root, parts, h, R(1.2f, 1.8f), R(0.28f, 0.36f));

            var trunkCol = root.AddComponent<CapsuleCollider>();
            trunkCol.center = new Vector3(0f, 1.5f, 0f); trunkCol.height = 3f; trunkCol.radius = trunkR;
            var foliage = root.AddComponent<CapsuleCollider>();
            foliage.isTrigger = true; // blocks photo line-of-sight, not the player
            foliage.center = new Vector3(0f, h * 0.55f, 0f); foliage.height = h * 0.8f; foliage.radius = h * 0.17f;
            AddFeature(root, FeatureKind.Tree, trunkR, h * 0.95f);
            return root;
        }

        private GameObject Deciduous(Transform parent, Vector3 p)
        {
            if (HasKitTrees) { var kit = KitTree(TreeRole.Broadleaf, parent, p); if (kit != null) return kit; }
            if (_cards) return BranchingBroadleaf(parent, p);
            float h = R(7f, 12f);
            float trunkR = 0.15f + h * 0.014f;
            var root = new GameObject("Broadleaf");
            root.transform.SetParent(parent, false);
            root.transform.position = p;
            root.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);

            float trunkLen = h * 0.6f;
            MeshPart("Trunk", root.transform, _cylinder, Pick(_bark), new Vector3(0f, trunkLen * 0.5f, 0f),
                new Vector3(trunkR * 2f, trunkLen * 0.5f, trunkR * 2f), Quaternion.Euler(R(-3f, 3f), 0f, R(-3f, 3f)));
            // A crown built from several lobes in two leaf shades breaks up the "lollipop" silhouette.
            int shade = _rng.Next(_leaf.Length);
            var mat = _cards ? _leafCore : _leaf[shade];
            var alt = _cards ? _leafCore : _leaf[(shade + 1) % _leaf.Length];
            float cy = h * 0.72f, cw = h * 0.48f;
            var parts = new List<Transform>();
            parts.Add(MeshPart("Canopy", root.transform, Pick(_canopies), mat, new Vector3(0f, cy, 0f),
                new Vector3(cw, h * 0.38f, cw), Quaternion.Euler(0f, R(0f, 360f), 0f)).transform);
            int extra = _rng.Next(3, 6);
            for (int i = 0; i < extra; i++)
            {
                float a = i / (float)extra * Mathf.PI * 2f + R(-0.4f, 0.4f);
                Vector3 off = new Vector3(Mathf.Cos(a), R(-0.12f, 0.1f), Mathf.Sin(a)) * cw * R(0.3f, 0.45f);
                parts.Add(MeshPart("CanopyLobe" + i, root.transform, Pick(_canopies), i % 2 == 0 ? alt : mat,
                    new Vector3(off.x, cy + off.y * h, off.z), Vector3.one * cw * R(0.45f, 0.7f),
                    Quaternion.Euler(0f, R(0f, 360f), 0f)).transform);
            }
            if (_cards)
            {
                var cards = _leafCards[_rng.Next(_leafCards.Length)];
                foreach (var part in parts)
                {
                    part.localScale *= 0.82f; // dark core sits inside the leaf cards
                    AddCards(part, FoliageCards.Canopy(_rng), cards);
                }
            }
            AddSway(root, parts, h, R(1.4f, 2.2f), R(0.3f, 0.42f));

            var trunkCol = root.AddComponent<CapsuleCollider>();
            trunkCol.center = new Vector3(0f, 1.5f, 0f); trunkCol.height = 3f; trunkCol.radius = trunkR;
            var canopy = root.AddComponent<SphereCollider>();
            canopy.isTrigger = true;
            canopy.center = new Vector3(0f, cy, 0f); canopy.radius = cw * 0.42f;
            AddFeature(root, FeatureKind.Tree, trunkR, cy + h * 0.18f);
            return root;
        }

        // ------------------------------------------------------------------ bushes, rocks, logs

        public void BuildBushes(Transform parent)
        {
            var prefabs = new List<GameObject>();
            foreach (var p in _s.bushPrefabs) if (p != null) prefabs.Add(p);
            int attempts = Scaled(_s.bushCount) * 15;
            while (BushesPlaced < Scaled(_s.bushCount) && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                float d = PathDistance(p);
                if (d < _s.trailWidth * 0.5f + 1.2f) continue;
                if (HasRoads && RoadDistance(p) < _s.trailWidth * 0.5f + 3.5f) continue;
                float f = Forest(p);
                float chance = Mathf.Clamp01(1f - Mathf.Abs(f - 0.45f) * 3f) * 0.7f + (d < 8f ? 0.3f : 0f); // edges & trailside
                if (R() > chance || !IsFree(p, 1.6f)) continue;

                GameObject bush;
                float s = R(0.6f, 1.4f);
                if (prefabs.Count == 0 && _artShrubs.Count > 0 && (HasKitShrubs || (d < 30f && R() < 0.65f)))
                {
                    // Scanned shrubs where the player will see them up close.
                    bush = PlaceArt(_artShrubs, parent, p, R(0.8f, 1.3f), 0.02f, 3f);
                    Occupy(p);
                    BushesPlaced++;
                    _bushObjects.Add(bush);
                    continue;
                }
                if (prefabs.Count > 0)
                {
                    bush = InstantiatePrefab(prefabs[_rng.Next(prefabs.Count)], parent, p, s);
                    AddFeature(bush, FeatureKind.Bush, 0.6f * s, RenderedHeight(bush));
                }
                else
                {
                    bush = new GameObject("Bush");
                    bush.transform.SetParent(parent, false);
                    bush.transform.position = p;
                    var mat = Pick(_bush);
                    int lobes = _rng.Next(2, 5);
                    var parts = new List<Transform>();
                    for (int i = 0; i < lobes; i++)
                    {
                        float a = R(0f, Mathf.PI * 2f), r = i == 0 ? 0f : R(0.25f, 0.5f) * s;
                        parts.Add(MeshPart("Lobe" + i, bush.transform, Pick(_canopies), i == lobes - 1 ? Pick(_bush) : mat,
                            new Vector3(Mathf.Cos(a) * r, 0.35f * s, Mathf.Sin(a) * r),
                            new Vector3(1.2f, 0.85f, 1.2f) * s * R(0.7f, 1f), Quaternion.Euler(0f, R(0f, 360f), 0f)).transform);
                    }
                    AddSway(bush, parts, 0.9f * s, R(2f, 3f), R(0.45f, 0.6f));
                    var col = bush.AddComponent<SphereCollider>();
                    col.isTrigger = true;
                    col.center = new Vector3(0f, 0.4f * s, 0f);
                    col.radius = 0.55f * s;
                    AddFeature(bush, FeatureKind.Bush, 0.6f * s, 0.9f * s);
                }
                MakeStaticExceptSway(bush);
                Occupy(p);
                BushesPlaced++;
                _bushObjects.Add(bush);
            }
        }

        /// <summary>
        /// Bedrock showing through on real slopes (1.2): scanned hillside rock faces turned to face downhill and sunk into
        /// the ground so their open backs are buried. Few and large; geology, not decoration. Steep ground first.
        /// </summary>
        private void BuildOutcrops(Transform parent)
        {
            if (_artOutcrops.Count == 0) return;
            int wanted = Mathf.Max(3, Scaled(9)), placed = 0;
            var candidates = new List<(Vector3 p, float slope)>();
            for (int i = 0; i < 1500; i++)
            {
                Vector3 p = RandomPoint();
                float slope = TerrainUtil.SlopeDegrees(p);
                if (slope < 20f || slope > 48f) continue;
                if (PathDistance(p) < 7f || (HasRoads && RoadDistance(p) < 9f) || CreekDistance(p) < 6f || InKeepOut(p)) continue;
                candidates.Add((p, slope));
            }
            candidates.Sort((a, b) => b.slope.CompareTo(a.slope));
            foreach (var c in candidates)
            {
                if (placed >= wanted) break;
                if (!IsFree(c.p, 9f)) continue;
                PlaceOutcrop(parent, c.p, R(0.55f, 0.9f));
                placed++;
            }
        }

        /// <summary>One rock face at <paramref name="p"/>, its open side facing downhill, base sunk into the slope.</summary>
        private GameObject PlaceOutcrop(Transform parent, Vector3 p, float scale)
        {
            Vector3 n = TerrainUtil.SampleNormal(p);
            Vector3 downhill = TerrainUtil.Flat(n);
            if (downhill.sqrMagnitude < 1e-4f) downhill = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
            var go = InstantiatePrefab(_artOutcrops[_rng.Next(_artOutcrops.Count)], parent, p, scale);
            var f = go.GetComponent<EnvironmentFeature>();
            float h = f != null ? f.height * scale : 3f;
            go.transform.position = p - Vector3.up * h * R(0.18f, 0.28f);
            go.transform.rotation = Quaternion.LookRotation(downhill.normalized) * Quaternion.Euler(0f, R(-20f, 20f), 0f);
            if (f != null) { f.radius *= scale; f.height *= scale; }
            MakeStatic(go);
            Occupy(p);
            RocksPlaced++;
            return go;
        }

        public void BuildRocks(Transform parent)
        {
            BuildOutcrops(parent);
            var prefabs = new List<GameObject>();
            foreach (var p in _s.rockPrefabs) if (p != null) prefabs.Add(p);
            // Rocks read as geology, not confetti: most sit in outcrops (clusters, favouring slopes and woodland
            // edges) and are embedded in the ground; a few large lone boulders stand in the open.
            int attempts = Scaled(_s.rockCount) * 15;
            Vector3 clusterCenter = Vector3.zero;
            int clusterRemaining = 0;
            while (RocksPlaced < Scaled(_s.rockCount) && attempts-- > 0)
            {
                Vector3 p;
                bool inCluster = clusterRemaining > 0;
                if (inCluster)
                {
                    float a = R(0f, Mathf.PI * 2f), r = Mathf.Sqrt(R()) * 3.5f;
                    p = TerrainUtil.Ground(clusterCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                }
                else
                {
                    p = RandomPoint();
                    float slope = TerrainUtil.SlopeDegrees(p);
                    // Start an outcrop more often on slopes and where woods meet open ground.
                    float startChance = 0.35f + Mathf.InverseLerp(8f, 30f, slope) * 0.5f + (Mathf.Abs(Forest(p) - 0.45f) < 0.12f ? 0.15f : 0f);
                    if (R() < startChance)
                    {
                        clusterCenter = p;
                        clusterRemaining = _rng.Next(3, 7);
                        inCluster = true;
                    }
                }
                if (PathDistance(p) < _s.trailWidth * 0.5f + 0.8f) { if (inCluster) clusterRemaining--; continue; }
                float roll = R();
                float size = inCluster
                    ? (clusterRemaining == 1 ? R(1.2f, 2.4f) : R(0.3f, 1.2f))
                    : (roll < 0.75f ? R(0.3f, 0.8f) : R(2.2f, 3.6f));
                if (inCluster) clusterRemaining--;
                if (size > 2f && Forest(p) > 0.45f) continue; // big boulders in the open: landmarks & hawk perches
                if (!IsFree(p, 1f + size * 0.5f)) continue;

                GameObject rock;
                if (prefabs.Count == 0 && _art && inCluster && _artRocks.Count > 0 && R() < 0.65f)
                {
                    // Scanned mossy rock pieces make up most of each outcrop.
                    rock = PlaceArt(_artRocks, parent, p, R(0.6f, 1.5f), 0.12f, 10f);
                    Occupy(p);
                    RocksPlaced++;
                    continue;
                }
                if (prefabs.Count == 0 && _art && !inCluster && size > 2f && _artBoulders.Count > 0)
                {
                    rock = PlaceArt(_artBoulders, parent, p, R(1f, 1.7f), 0.1f, 6f);
                    Occupy(p);
                    RocksPlaced++;
                    continue;
                }
                if (prefabs.Count > 0)
                {
                    rock = InstantiatePrefab(prefabs[_rng.Next(prefabs.Count)], parent, p, size);
                }
                else
                {
                    rock = new GameObject(size > 2f ? "Boulder" : "Rock");
                    rock.transform.SetParent(parent, false);
                    var mesh = Pick(_rocks);
                    Vector3 scale = new Vector3(size * R(0.9f, 1.3f), size * R(0.55f, 0.9f), size * R(0.9f, 1.3f));
                    rock.transform.position = p - Vector3.up * scale.y * R(0.15f, 0.3f); // embedded, not resting on top
                    rock.transform.rotation = Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f));
                    rock.transform.localScale = scale;
                    rock.AddComponent<MeshFilter>().sharedMesh = mesh;
                    rock.AddComponent<MeshRenderer>().sharedMaterial = Pick(_stone);
                    var col = rock.AddComponent<MeshCollider>();
                    col.sharedMesh = mesh;
                    col.convex = true;
                }
                AddFeature(rock, FeatureKind.Rock, size * 0.5f, prefabs.Count > 0 ? RenderedHeight(rock) : rock.transform.localScale.y * 0.48f);
                MakeStatic(rock);
                Occupy(p);
                RocksPlaced++;
            }
        }

        public void BuildLogs(Transform parent)
        {
            int placed = 0, attempts = Scaled(_s.logCount) * 30;
            while (placed < Scaled(_s.logCount) && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                if (Forest(p) < 0.45f || PathDistance(p) < 4f || !IsFree(p, 3f)) continue;
                if (_artLogs.Count > 0)
                {
                    PlaceArt(_artLogs, parent, p, R(0.8f, 1.2f), 0.15f, 3f);
                    Occupy(p);
                    placed++;
                    continue;
                }
                float len = R(2.5f, 5f), rad = R(0.18f, 0.3f);
                var log = new GameObject("Fallen Log");
                log.transform.SetParent(parent, false);
                log.transform.position = p + Vector3.up * rad * 0.8f;
                log.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                MeshPart("Trunk", log.transform, _cylinder, Pick(_bark), Vector3.zero,
                    new Vector3(rad * 2f, len * 0.5f, rad * 2f), Quaternion.Euler(90f, 0f, R(-4f, 4f)));
                var col = log.AddComponent<CapsuleCollider>();
                col.direction = 2; // along local Z
                col.height = len;
                col.radius = rad;
                AddFeature(log, FeatureKind.Log, len * 0.5f, rad * 2f);
                MakeStatic(log);
                Occupy(p);
                placed++;
            }

            // A few old stumps: small forest-floor storytelling.
            int stumps = 0;
            attempts = 400;
            var cut = GeneratedAssets.Material("StumpTop", new Color(0.62f, 0.5f, 0.34f));
            while (stumps < Mathf.Max(4, _s.logCount * 2 / 3) && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                if (Forest(p) < 0.4f || PathDistance(p) < 3f || !IsFree(p, 2f)) continue;
                if (_artStumps.Count > 0)
                {
                    PlaceArt(_artStumps, parent, p, R(0.7f, 1.1f), 0.1f, 4f);
                    Occupy(p);
                    stumps++;
                    continue;
                }
                float rad = R(0.22f, 0.4f), h = R(0.25f, 0.6f);
                var stump = new GameObject("Stump");
                stump.transform.SetParent(parent, false);
                stump.transform.position = p;
                MeshPart("Trunk", stump.transform, _cylinder, Pick(_bark), new Vector3(0f, h * 0.5f, 0f),
                    new Vector3(rad * 2f, h * 0.5f, rad * 2f), Quaternion.identity);
                MeshPart("Cut", stump.transform, _cylinder, cut, new Vector3(0f, h, 0f),
                    new Vector3(rad * 1.9f, 0.01f, rad * 1.9f), Quaternion.identity);
                var col = stump.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, h * 0.5f, 0f); col.height = h; col.radius = rad;
                AddFeature(stump, FeatureKind.Log, rad + 0.2f, h);
                MakeStatic(stump);
                Occupy(p);
                stumps++;
            }
        }

        // ------------------------------------------------------------------ undergrowth (scanned ferns)

        /// <summary>Ferns under woodland and along shaded trail edges, where the player walks past them.</summary>
        public void BuildUndergrowth(Transform parent)
        {
            if (_artFerns.Count == 0 || _s.fernCount <= 0) return;
            int attempts = _s.fernCount * 20;
            while (FernsPlaced < _s.fernCount && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                float d = PathDistance(p);
                if (d < _s.trailWidth * 0.5f + 0.8f || d > 35f) continue;
                float f = Forest(p);
                if (R() > Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.6f, f)) || TerrainUtil.SlopeDegrees(p) > 30f) continue;
                PlaceArt(_artFerns, parent, p, R(0.6f, 1.1f), 0f, 6f);
                FernsPlaced++;
            }
        }

        // ------------------------------------------------------------------ grass

        private class MeshBuffer
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<int> t = new List<int>();

            public void Blade(Vector3 basePos, float angle, float height, float width, Vector3 lean)
            {
                Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (width * 0.5f);
                int i = v.Count;
                v.Add(basePos - side); v.Add(basePos + side); v.Add(basePos + Vector3.up * height + lean);
                n.Add(Vector3.up); n.Add(Vector3.up); n.Add(Vector3.up); // up-normals shade like the ground
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            public void Blossom(Vector3 c, float r)
            {
                int i = v.Count;
                Vector3[] pts = { c + Vector3.up * r, c - Vector3.up * r * 0.4f, c + Vector3.right * r, c - Vector3.right * r, c + Vector3.forward * r, c - Vector3.forward * r };
                foreach (var p in pts) { v.Add(p); n.Add(Vector3.up); }
                int[] f = { 0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5 };
                for (int k = 0; k < f.Length; k += 3)
                {
                    // Both windings so heads render from any side in every pipeline.
                    t.Add(i + f[k]); t.Add(i + f[k + 1]); t.Add(i + f[k + 2]);
                    t.Add(i + f[k]); t.Add(i + f[k + 2]); t.Add(i + f[k + 1]);
                }
            }
        }

        public void BuildGrass(Transform parent)
        {
            if (!_s.scatterGrass || _s.grassTuftCount <= 0) return;
            const float chunk = 50f;
            string[] typeNames = { "Green", "Dry", "FlowerYellow", "FlowerWhite" };
            Material[] mats =
            {
                GeneratedAssets.Material("GrassGreen", new Color(0.36f, 0.5f, 0.2f), 0.05f, null, true),
                GeneratedAssets.Material("GrassDry", new Color(0.55f, 0.55f, 0.28f), 0.05f, null, true),
                GeneratedAssets.Material("FlowerYellow", new Color(0.95f, 0.8f, 0.25f), 0.1f, null, true),
                GeneratedAssets.Material("FlowerWhite", new Color(0.95f, 0.94f, 0.9f), 0.1f, null, true),
            };
            var buffers = new Dictionary<Vector3Int, MeshBuffer>(); // (chunkX, chunkZ, type)

            void AddTuft(Vector3 p, float f, float flowerChance)
            {
                float dryness = Mathf.PerlinNoise(_noiseOffset + p.x * 0.04f, p.z * 0.04f);
                int type = R() < flowerChance ? (R() < 0.6f ? 2 : 3) : (dryness > 0.6f ? 1 : 0);
                var key = new Vector3Int(Mathf.FloorToInt(p.x / chunk), Mathf.FloorToInt(p.z / chunk), type);
                if (!buffers.TryGetValue(key, out var buf)) buffers[key] = buf = new MeshBuffer();

                Vector3 origin = new Vector3(key.x * chunk, 0f, key.y * chunk);
                Vector3 local = p - origin + Vector3.down * 0.03f;
                // Kept below ~0.5 m: grass doesn't block photo line-of-sight, so it mustn't visually hide rabbits.
                float scale = R(0.6f, 1.1f) * (1f - f * 0.3f);
                if (type >= 2)
                {
                    float h = R(0.3f, 0.5f);
                    Vector3 lean = new Vector3(R(-0.05f, 0.05f), 0f, R(-0.05f, 0.05f));
                    buf.Blade(local, R(0f, Mathf.PI), h, 0.02f, lean);
                    buf.Blossom(local + Vector3.up * h + lean, 0.035f);
                }
                else
                {
                    int blades = _rng.Next(5, 9);
                    for (int b = 0; b < blades; b++)
                    {
                        Vector2 o = new Vector2(R(-1f, 1f), R(-1f, 1f)) * 0.12f;
                        float h = R(0.22f, 0.5f) * scale;
                        Vector3 lean = new Vector3(R(-1f, 1f), 0f, R(-1f, 1f)) * h * 0.25f;
                        buf.Blade(local + new Vector3(o.x, 0f, o.y), R(0f, Mathf.PI), h, R(0.035f, 0.06f), lean);
                    }
                }
                GrassPlaced++;
            }

            int attempts = _s.grassTuftCount * 4;
            int target = _s.grassTuftCount;
            while (GrassPlaced < target && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                if (PathDistance(p) < _s.trailWidth * 0.5f + 0.25f) continue;
                float f = Forest(p);
                if (R() > 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, f)) * 0.85f) continue;
                if (TerrainUtil.SlopeDegrees(p) > 40f) continue;
                AddTuft(p, f, 0.05f);
            }

            // Meadow hotspots: lusher, flower-rich grass. The visual cue players learn means "deer country".
            foreach (var m in _meadows)
            {
                int extra = Mathf.RoundToInt(_s.grassTuftCount * 0.06f);
                for (int i = 0; i < extra; i++)
                {
                    float a = R(0f, Mathf.PI * 2f), r = Mathf.Sqrt(R()) * m.radius;
                    Vector3 p = TerrainUtil.Ground(m.center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                    if (PathDistance(p) < _s.trailWidth * 0.5f + 0.25f || TerrainUtil.SlopeDegrees(p) > 40f) continue;
                    AddTuft(p, 0f, 0.22f);
                }
            }

            // All grass meshes live as sub-assets of one container asset.
            string path = GeneratedAssets.Folder + "/GrassMeshes.asset";
            AssetDatabase.DeleteAsset(path);
            Object container = null;
            foreach (var kv in buffers)
            {
                var key = kv.Key;
                var buf = kv.Value;
                var mesh = new Mesh { name = $"Grass_{typeNames[key.z]}_{key.x}_{key.y}" };
                if (buf.v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(buf.v);
                mesh.SetNormals(buf.n);
                mesh.SetTriangles(buf.t, 0);
                mesh.RecalculateBounds();
                if (container == null) { AssetDatabase.CreateAsset(mesh, path); container = mesh; }
                else AssetDatabase.AddObjectToAsset(mesh, container);

                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(key.x * chunk, 0f, key.y * chunk);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mats[key.z];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }
    }
}
