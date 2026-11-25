using UnityEngine;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Settings for the vertical-slice scene builder. Stored as an asset so tuning (and prefab choices)
    /// persist between rebuilds. Assign your own prefabs to replace the generated placeholders.
    /// </summary>
    public class SliceBuildSettings : ScriptableObject
    {
        [Header("General")]
        public int seed = 7;

        [Header("Trail loop")]
        [Tooltip("Approximate radius of the loop (metres). Clamped to fit the terrain.")]
        public float trailRadius = 85f;
        [Tooltip("Width/height ratio of the loop's ellipse.")]
        [Range(1f, 2f)] public float trailAspect = 1.3f;
        public float trailWidth = 1.8f;
        public float markerSpacing = 60f;
        [Tooltip("Search the terrain for the gentlest area to place the loop instead of using its centre.")]
        public bool autoPlaceOnGentleGround = true;

        [Header("Vegetation & scenery")]
        [Tooltip("How far beyond the loop scenery is scattered.")]
        public float scatterMargin = 70f;
        public int treeCount = 420;
        public int bushCount = 170;
        public int rockCount = 70;
        public int logCount = 12;
        public bool scatterGrass = true;
        public int grassTuftCount = 7000;
        [Tooltip("Keep the inside of the loop as open meadow (good sight-lines for photography).")]
        public bool openMeadowInsideLoop = true;

        [Header("Use your own assets (optional)")]
        public GameObject[] treePrefabs = new GameObject[0];
        public GameObject[] bushPrefabs = new GameObject[0];
        public GameObject[] rockPrefabs = new GameObject[0];
        [Tooltip("If no tree prefabs are assigned, use the terrain's existing tree prototypes before falling back to placeholders.")]
        public bool useTerrainTreePrototypes = true;

        [Header("Scene")]
        public bool configureLightingAndFog = true;
        public bool disableOtherCameras = true;

        [Header("Fallback terrain (only if the scene has none)")]
        public Vector3 fallbackTerrainSize = new Vector3(400f, 40f, 400f);
    }
}
