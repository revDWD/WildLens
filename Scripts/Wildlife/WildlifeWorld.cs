using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
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

    /// <summary>
    /// Shared environmental knowledge for animals: trees to climb or avoid, perches, and the trail.
    /// Built once from <see cref="EnvironmentFeature"/> components and terrain tree instances.
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

        // Uniform grid over XZ for fast "nearest tree" queries.
        private const float CellSize = 10f;
        private readonly Dictionary<Vector2Int, List<int>> _treeGrid = new Dictionary<Vector2Int, List<int>>();
        private readonly Dictionary<Vector2Int, List<int>> _obstacleGrid = new Dictionary<Vector2Int, List<int>>();

        public IReadOnlyList<FeaturePoint> Trees => _trees;
        public IReadOnlyList<FeaturePoint> Perches => _perches;

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
            _trees.Clear(); _perches.Clear(); _obstacles.Clear();
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

        public bool TryGetRandomPerch(Vector3 near, float radius, out FeaturePoint perch)
        {
            perch = default;
            int count = 0;
            // Reservoir sample so we don't allocate.
            foreach (var p in _perches)
            {
                if (TerrainUtil.FlatDistance(p.basePosition, near) > radius) continue;
                count++;
                if (Random.Range(0, count) == 0) perch = p;
            }
            return count > 0;
        }
    }
}
