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
    public class VegetationBuilder
    {
        private readonly Terrain _terrain;
        private readonly TrailPath _trail;
        private readonly TrailBuilder.Loop _loop;
        private readonly SliceBuildSettings _s;
        private readonly System.Random _rng;
        private readonly Vector2 _min, _max;
        private readonly float _noiseOffset;
        private readonly Dictionary<Vector2Int, List<Vector3>> _occupied = new Dictionary<Vector2Int, List<Vector3>>();
        private const float OccupancyCell = 4f;

        // Shared placeholder assets
        private Mesh _cone, _cylinder;
        private Mesh[] _canopies, _rocks;
        private Material[] _bark, _conifer, _leaf, _bush, _stone;

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
            return Mathf.Clamp01(n);
        }

        private bool IsFree(Vector3 p, float spacing)
        {
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
            _cone = GeneratedAssets.LoadOrCreate("Mesh_Cone", () => ProceduralMeshes.Cone(12));
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
        }

        private T Pick<T>(T[] array) => array[_rng.Next(array.Length)];

        private static void MakeStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
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

            int attempts = _s.treeCount * 15;
            float corridor = _s.trailWidth * 0.5f + 2.5f;
            while (TreesPlaced < _s.treeCount && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                if (_trail.DistanceToTrail(p) < corridor) continue;
                float f = Forest(p);
                if (R() > Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.65f, f))) continue;
                if (TerrainUtil.SlopeDegrees(p) > 38f || !IsFree(p, 2.6f)) continue;

                GameObject tree;
                if (prefabs.Count > 0)
                {
                    tree = InstantiatePrefab(prefabs[_rng.Next(prefabs.Count)], parent, p, R(0.85f, 1.25f));
                    AddFeature(tree, FeatureKind.Tree, 0.3f * tree.transform.localScale.x, RenderedHeight(tree));
                }
                else
                {
                    bool conifer = Mathf.PerlinNoise(_noiseOffset * 0.5f + p.x * 0.03f, p.z * 0.03f) > 0.5f;
                    tree = conifer ? Conifer(parent, p) : Deciduous(parent, p);
                }
                MakeStatic(tree);
                Occupy(p);
                TreesPlaced++;
            }
        }

        private GameObject Conifer(Transform parent, Vector3 p)
        {
            float h = R(8f, 15f);
            float trunkR = 0.16f + h * 0.012f;
            var root = new GameObject("Conifer");
            root.transform.SetParent(parent, false);
            root.transform.position = p;
            root.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);

            float trunkLen = h * 0.35f;
            MeshPart("Trunk", root.transform, _cylinder, Pick(_bark), new Vector3(0f, trunkLen * 0.5f, 0f),
                new Vector3(trunkR * 2f, trunkLen * 0.5f, trunkR * 2f), Quaternion.identity);
            var mat = Pick(_conifer);
            for (int i = 0; i < 3; i++)
            {
                float baseY = h * (0.2f + i * 0.22f);
                float rad = h * 0.27f * (1f - i * 0.24f) * R(0.9f, 1.1f);
                MeshPart("Foliage" + i, root.transform, _cone, mat, new Vector3(0f, baseY, 0f),
                    new Vector3(rad, h * 0.4f, rad), Quaternion.Euler(0f, R(0f, 360f), 0f));
            }

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
            float h = R(7f, 12f);
            float trunkR = 0.15f + h * 0.014f;
            var root = new GameObject("Broadleaf");
            root.transform.SetParent(parent, false);
            root.transform.position = p;
            root.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);

            float trunkLen = h * 0.6f;
            MeshPart("Trunk", root.transform, _cylinder, Pick(_bark), new Vector3(0f, trunkLen * 0.5f, 0f),
                new Vector3(trunkR * 2f, trunkLen * 0.5f, trunkR * 2f), Quaternion.Euler(R(-3f, 3f), 0f, R(-3f, 3f)));
            var mat = Pick(_leaf);
            float cy = h * 0.72f, cw = h * 0.48f;
            MeshPart("Canopy", root.transform, Pick(_canopies), mat, new Vector3(0f, cy, 0f),
                new Vector3(cw, h * 0.4f, cw), Quaternion.Euler(0f, R(0f, 360f), 0f));
            int extra = _rng.Next(1, 3);
            for (int i = 0; i < extra; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 off = new Vector3(Mathf.Cos(a), R(-0.1f, 0.05f), Mathf.Sin(a)) * cw * 0.35f;
                MeshPart("CanopyLobe" + i, root.transform, Pick(_canopies), mat, new Vector3(off.x, cy + off.y * h, off.z),
                    Vector3.one * cw * R(0.55f, 0.75f), Quaternion.Euler(0f, R(0f, 360f), 0f));
            }

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
            int attempts = _s.bushCount * 15;
            while (BushesPlaced < _s.bushCount && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                float d = _trail.DistanceToTrail(p);
                if (d < _s.trailWidth * 0.5f + 1.2f) continue;
                float f = Forest(p);
                float chance = Mathf.Clamp01(1f - Mathf.Abs(f - 0.45f) * 3f) * 0.7f + (d < 8f ? 0.3f : 0f); // edges & trailside
                if (R() > chance || !IsFree(p, 1.6f)) continue;

                GameObject bush;
                float s = R(0.6f, 1.4f);
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
                    for (int i = 0; i < lobes; i++)
                    {
                        float a = R(0f, Mathf.PI * 2f), r = i == 0 ? 0f : R(0.25f, 0.5f) * s;
                        MeshPart("Lobe" + i, bush.transform, Pick(_canopies), mat,
                            new Vector3(Mathf.Cos(a) * r, 0.35f * s, Mathf.Sin(a) * r),
                            new Vector3(1.2f, 0.85f, 1.2f) * s * R(0.7f, 1f), Quaternion.Euler(0f, R(0f, 360f), 0f));
                    }
                    var col = bush.AddComponent<SphereCollider>();
                    col.isTrigger = true;
                    col.center = new Vector3(0f, 0.4f * s, 0f);
                    col.radius = 0.55f * s;
                    AddFeature(bush, FeatureKind.Bush, 0.6f * s, 0.9f * s);
                }
                MakeStatic(bush);
                Occupy(p);
                BushesPlaced++;
            }
        }

        public void BuildRocks(Transform parent)
        {
            var prefabs = new List<GameObject>();
            foreach (var p in _s.rockPrefabs) if (p != null) prefabs.Add(p);
            int attempts = _s.rockCount * 15;
            while (RocksPlaced < _s.rockCount && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                if (_trail.DistanceToTrail(p) < _s.trailWidth * 0.5f + 0.8f) continue;
                float roll = R();
                float size = roll < 0.7f ? R(0.35f, 1f) : roll < 0.94f ? R(1f, 2f) : R(2.2f, 3.6f);
                if (size > 2f && Forest(p) > 0.45f) continue; // big boulders in the open: landmarks & hawk perches
                if (!IsFree(p, 1f + size * 0.5f)) continue;

                GameObject rock;
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
                    rock.transform.position = p - Vector3.up * scale.y * 0.12f;
                    rock.transform.rotation = Quaternion.Euler(R(-6f, 6f), R(0f, 360f), R(-6f, 6f));
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
            int placed = 0, attempts = _s.logCount * 30;
            while (placed < _s.logCount && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                if (Forest(p) < 0.45f || _trail.DistanceToTrail(p) < 4f || !IsFree(p, 3f)) continue;
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

            int attempts = _s.grassTuftCount * 4;
            while (GrassPlaced < _s.grassTuftCount && attempts-- > 0)
            {
                Vector3 p = RandomPoint();
                if (_trail.DistanceToTrail(p) < _s.trailWidth * 0.5f + 0.25f) continue;
                float f = Forest(p);
                if (R() > 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, f)) * 0.85f) continue;
                if (TerrainUtil.SlopeDegrees(p) > 40f) continue;

                float dryness = Mathf.PerlinNoise(_noiseOffset + p.x * 0.04f, p.z * 0.04f);
                int type = R() < 0.05f ? (R() < 0.6f ? 2 : 3) : (dryness > 0.6f ? 1 : 0);
                var key = new Vector3Int(Mathf.FloorToInt(p.x / chunk), Mathf.FloorToInt(p.z / chunk), type);
                if (!buffers.TryGetValue(key, out var buf)) buffers[key] = buf = new MeshBuffer();

                Vector3 origin = new Vector3(key.x * chunk, 0f, key.y * chunk);
                Vector3 local = p - origin + Vector3.down * 0.03f;
                float scale = R(0.7f, 1.3f) * (1f - f * 0.3f);
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
                        float h = R(0.22f, 0.55f) * scale;
                        Vector3 lean = new Vector3(R(-1f, 1f), 0f, R(-1f, 1f)) * h * 0.25f;
                        buf.Blade(local + new Vector3(o.x, 0f, o.y), R(0f, Mathf.PI), h, R(0.035f, 0.06f), lean);
                    }
                }
                GrassPlaced++;
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
