using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.Wildlife
{
    /// <summary>A usable environment feature (cached data, independent of the source object).</summary>
    public struct FeaturePoint
    {
        public Vector3 basePosition;
        public float radius;
        public float height;
        public FeatureKind kind;

        public Vector3 Top => basePosition + Vector3.up * height;
    }

    /// <summary>A habitat hotspot (cached; see <see cref="HabitatHotspot"/>).</summary>
    public struct Hotspot
    {
        public HotspotType type;
        public Vector3 position;
        public float radius;
        public float weight;
    }

    /// <summary>
    /// Shared environmental knowledge for animals: trees to climb or avoid, perches, habitat hotspots and the trail.
    /// Built once from <see cref="EnvironmentFeature"/> and <see cref="HabitatHotspot"/> components and terrain
    /// tree instances. If the scene has no hotspots, they are derived from the scenery (see DeriveHotspots).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class WildlifeWorld : MonoBehaviour
    {
        [SerializeField] private TrailPath trail;
        [Tooltip("Height assumed for terrain-painted trees, which carry no feature data.")]
        [SerializeField] private float terrainTreeHeight = 9f;

        public static WildlifeWorld Instance { get; private set; }
        public TrailPath Trail => trail;

        private readonly List<FeaturePoint> _trees = new List<FeaturePoint>();
        private readonly List<FeaturePoint> _perches = new List<FeaturePoint>();
        private readonly List<FeaturePoint> _obstacles = new List<FeaturePoint>();
        private readonly List<FeaturePoint> _bushes = new List<FeaturePoint>();
        private readonly List<Hotspot> _hotspots = new List<Hotspot>();

        // Uniform grid over XZ for fast "nearest tree" queries.
        private const float CellSize = 10f;
        private readonly Dictionary<Vector2Int, List<int>> _treeGrid = new Dictionary<Vector2Int, List<int>>();
        private readonly Dictionary<Vector2Int, List<int>> _obstacleGrid = new Dictionary<Vector2Int, List<int>>();

        public IReadOnlyList<FeaturePoint> Trees => _trees;
        public IReadOnlyList<FeaturePoint> Perches => _perches;
        public IReadOnlyList<Hotspot> Hotspots => _hotspots;
        /// <summary>True when hotspots were derived from scenery rather than authored.</summary>
        public bool HotspotsDerived { get; private set; }

        public void SetTrail(TrailPath path) => trail = path;

        private void Awake()
        {
            Instance = this;
            if (trail == null) trail = SceneQuery.FindOne<TrailPath>();
            Rebuild();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Rebuild()
        {
            _trees.Clear(); _perches.Clear(); _obstacles.Clear(); _bushes.Clear(); _hotspots.Clear();
            _treeGrid.Clear(); _obstacleGrid.Clear();

            foreach (var f in SceneQuery.FindAll<EnvironmentFeature>())
            {
                if (!f.isActiveAndEnabled) continue;
                Add(new FeaturePoint { basePosition = f.BasePosition, radius = f.radius, height = f.height, kind = f.kind });
            }

            foreach (var terrain in Terrain.activeTerrains)
            {
                var data = terrain.terrainData;
                if (data == null) continue;
                Vector3 origin = terrain.transform.position;
                foreach (var inst in data.treeInstances)
                {
                    Vector3 p = origin + Vector3.Scale(inst.position, data.size);
                    Add(new FeaturePoint
                    {
                        basePosition = p, radius = 0.3f * inst.widthScale,
                        height = terrainTreeHeight * inst.heightScale, kind = FeatureKind.Tree
                    });
                }
            }

            CollectHotspots();
        }

        private void CollectHotspots()
        {
            foreach (var h in SceneQuery.FindAll<HabitatHotspot>())
            {
                if (!h.isActiveAndEnabled || h.type == HotspotType.None) continue;
                _hotspots.Add(new Hotspot { type = h.type, position = h.transform.position, radius = h.radius, weight = h.weight });
            }
            HotspotsDerived = _hotspots.Count == 0;
            if (HotspotsDerived) DeriveHotspots();
        }

        // ------------------------------------------------------------------ hotspot queries

        /// <summary>Weighted random hotspot whose type is in <paramref name="types"/>.</summary>
        public bool TryPickHotspot(HotspotType types, System.Random rng, out Hotspot hotspot)
        {
            hotspot = default;
            float total = 0f;
            foreach (var h in _hotspots) if ((h.type & types) != 0) total += h.weight;
            if (total <= 0f) return false;
            float roll = (float)rng.NextDouble() * total;
            foreach (var h in _hotspots)
            {
                if ((h.type & types) == 0) continue;
                roll -= h.weight;
                if (roll <= 0f) { hotspot = h; return true; }
            }
            return false;
        }

        public bool IsInHotspot(Vector3 p, HotspotType types)
        {
            foreach (var h in _hotspots)
                if ((h.type & types) != 0 && TerrainUtil.FlatDistance(h.position, p) <= h.radius) return true;
            return false;
        }

        /// <summary>
        /// Derives hotspots from the scenery when none were authored: open ground near the trail (meadows),
        /// bush clusters (brush), the tallest trees near the trail (mature trees) and the tallest perches.
        /// </summary>
        private void DeriveHotspots()
        {
            if (trail != null && trail.Points.Count > 1)
            {
                var meadows = new List<Vector3>();
                for (float along = 0f; along < trail.Length; along += 15f)
                {
                    Vector3 p = trail.GetPoint(along);
                    Vector3 side = Vector3.Cross(Vector3.up, trail.GetDirection(along));
                    foreach (float off in new[] { -35f, -25f, -15f, 15f, 25f, 35f })
                    {
                        Vector3 c = TerrainUtil.Ground(p + side * off);
                        if (DistanceToNearestTree(c, 14f) >= 14f && TerrainUtil.SlopeDegrees(c) < 20f) meadows.Add(c);
                    }
                }
                AddSpread(meadows, HotspotType.Meadow, 3, 40f, 16f);

                var brush = new List<Vector3>();
                foreach (var b in _bushes)
                {
                    if (trail.DistanceToTrail(b.basePosition) > 35f) continue;
                    int neighbours = 0;
                    foreach (var o in _bushes) if (TerrainUtil.FlatDistance(o.basePosition, b.basePosition) < 5f) neighbours++;
                    if (neighbours >= 3) brush.Add(b.basePosition); // itself + two others
                }
                AddSpread(brush, HotspotType.BrushPile, 4, 30f, 9f);
            }

            var tall = new List<FeaturePoint>(_trees);
            tall.Sort((a, b) => b.height.CompareTo(a.height));
            var treeSpots = new List<Vector3>();
            foreach (var t in tall)
                if (trail == null || trail.DistanceToTrail(t.basePosition) < 30f) treeSpots.Add(t.basePosition);
            AddSpread(treeSpots, HotspotType.MatureTree, 5, 25f, 10f);

            var perches = new List<FeaturePoint>(_perches);
            perches.Sort((a, b) => b.height.CompareTo(a.height));
            var perchSpots = new List<Vector3>();
            foreach (var p in perches)
                if (trail == null || trail.DistanceToTrail(p.basePosition) < 60f) perchSpots.Add(p.basePosition);
            AddSpread(perchSpots, HotspotType.Perch, 4, 30f, 5f);
        }

        /// <summary>Adds up to <paramref name="max"/> hotspots from ordered candidates, keeping them apart.</summary>
        private void AddSpread(List<Vector3> candidates, HotspotType type, int max, float minSeparation, float radius)
        {
            var chosen = new List<Vector3>();
            foreach (var c in candidates)
            {
                if (chosen.Count >= max) break;
                bool clear = true;
                foreach (var q in chosen) if (TerrainUtil.FlatDistance(c, q) < minSeparation) { clear = false; break; }
                if (!clear) continue;
                chosen.Add(c);
                _hotspots.Add(new Hotspot { type = type, position = c, radius = radius, weight = 1f });
            }
        }

        private void Add(FeaturePoint p)
        {
            switch (p.kind)
            {
                case FeatureKind.Tree:
                    _trees.Add(p);
                    AddToGrid(_treeGrid, p.basePosition, _trees.Count - 1);
                    if (p.height >= 6f) _perches.Add(p);
                    AddObstacle(p);
                    break;
                case FeatureKind.Rock:
                    if (p.height >= 0.8f) _perches.Add(p); // boulders
                    AddObstacle(p);
                    break;
                case FeatureKind.Log:
                    AddObstacle(p);
                    break;
                case FeatureKind.Bush:
                    _bushes.Add(p);
                    break;
            }
        }

        private void AddObstacle(FeaturePoint p)
        {
            _obstacles.Add(p);
            AddToGrid(_obstacleGrid, p.basePosition, _obstacles.Count - 1);
        }

        private static Vector2Int Cell(Vector3 p) =>
            new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));

        private static void AddToGrid(Dictionary<Vector2Int, List<int>> grid, Vector3 p, int index)
        {
            var c = Cell(p);
            if (!grid.TryGetValue(c, out var list)) grid[c] = list = new List<int>();
            list.Add(index);
        }

        private static bool Nearest(Dictionary<Vector2Int, List<int>> grid, List<FeaturePoint> source, Vector3 p,
            float maxDistance, out FeaturePoint result, float minDistance = 0f)
        {
            result = default;
            float best = maxDistance;
            bool found = false;
            int r = Mathf.CeilToInt(maxDistance / CellSize);
            var c = Cell(p);
            for (int x = -r; x <= r; x++)
            for (int z = -r; z <= r; z++)
            {
                if (!grid.TryGetValue(new Vector2Int(c.x + x, c.y + z), out var list)) continue;
                foreach (int i in list)
                {
                    float d = TerrainUtil.FlatDistance(source[i].basePosition, p);
                    if (d < best && d >= minDistance)
                    {
                        best = d; result = source[i]; found = true;
                    }
                }
            }
            return found;
        }

        public bool TryGetNearestTree(Vector3 p, float maxDistance, out FeaturePoint tree) =>
            Nearest(_treeGrid, _trees, p, maxDistance, out tree);

        public bool TryGetNearestObstacle(Vector3 p, float maxDistance, out FeaturePoint obstacle) =>
            Nearest(_obstacleGrid, _obstacles, p, maxDistance, out obstacle);

        /// <summary>Distance to the nearest tree trunk (or maxDistance if none is closer).</summary>
        public float DistanceToNearestTree(Vector3 p, float maxDistance) =>
            TryGetNearestTree(p, maxDistance, out var t) ? TerrainUtil.FlatDistance(t.basePosition, p) : maxDistance;

        /// <summary>
        /// Random perch within <paramref name="radius"/>. Perches inside Perch hotspots (snags, lookout boulders)
        /// are four times as likely to be chosen, so hawks are seen in the same places again and again.
        /// </summary>
        public bool TryGetRandomPerch(Vector3 near, float radius, out FeaturePoint perch)
        {
            perch = default;
            float total = 0f;
            // Weighted reservoir sample so we don't allocate.
            foreach (var p in _perches)
            {
                if (TerrainUtil.FlatDistance(p.basePosition, near) > radius) continue;
                float w = IsInHotspot(p.basePosition, HotspotType.Perch) ? 4f : 1f;
                total += w;
                if (Random.value * total < w) perch = p;
            }
            return total > 0f;
        }
    }
}
