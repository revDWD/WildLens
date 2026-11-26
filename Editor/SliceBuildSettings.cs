using UnityEngine;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Settings for the vertical-slice scene builder. Stored as an asset so tuning (and prefab choices)
    /// persist between rebuilds. Assign your own prefabs to replace the generated placeholders.
    /// </summary>
    public enum SkyMood
    {
        [Tooltip("Poly Haven 'Kloofendal 48d Partly Cloudy': late morning, sun 48° up, cumulus.")]
        LateMorning,
        [Tooltip("Poly Haven 'Kloppenheim 06': golden hour, sun 14° up behind broken cloud.")]
        GoldenHour,
        [Tooltip("Unity procedural sky (no HDRI).")]
        Procedural,
    }

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

        [Header("Art")]
        [Tooltip("Use the scanned CC0 art shipped with the package (HDRI skies, ground materials, bark, rocks, ferns, shrubs, logs).")]
        public bool useScannedArt = true;
        public SkyMood skyMood = SkyMood.LateMorning;
        [Tooltip("Scanned ferns placed under woodland near the trail.")]
        public int fernCount = 160;

        [Header("Terrain surface")]
        [Tooltip("Paint the terrain with generated grass / meadow / forest floor / dirt / rock layers. The terrain's original data is backed up first.")]
        public bool paintTerrain = true;
        [Tooltip("Use Unity terrain grass, flowers and ferns (they wave in the wind). Off, or on HDRP: mesh grass instead.")]
        public bool terrainDetailGrass = true;
        [Tooltip("Copy the terrain's TerrainData to Generated/Backup before the first paint.")]
        public bool backupTerrainData = true;

        [Header("Scenery")]
        [Tooltip("Meadow overlook and lookout, with view cones cleared of trees.")]
        public bool scenicViewpoints = true;
        [Tooltip("Wind that sways trees and bushes (plus a WindZone for prefab trees).")]
        public bool wind = true;

        [Header("Reserve (0.7)")]
        [Tooltip("Build destinations beyond the loop (ridge overlook, pond, old-growth grove, meadow blind) with reserve tracks.")]
        public bool buildReserve = true;
        [Tooltip("How far from the loop's centre destinations may be placed (metres). Limited by the terrain's size.")]
        public float reserveReach = 380f;
        [Tooltip("Dig the pond basin and raise the overlook hill where the ground doesn't already provide them. " +
                 "Your terrain is backed up first; rebuilding gives the same shape rather than digging deeper.")]
        public bool shapeTerrainAtDestinations = true;
        [Tooltip("Park a trail bike at the trailhead (R to ride and park).")]
        public bool trailBike = true;
        [Tooltip("The WildLens fallback terrain is grown to at least this size so the reserve has room (your own terrains are never resized).")]
        public float reserveTerrainSize = 900f;

        [Header("Time of day (0.9)")]
        [Tooltip("Dawn → morning → midday → golden hour → dusk, with lighting, mist and wildlife rhythms. Off: one fixed moment (the sky mood above).")]
        public bool timeOfDay = true;
        [Tooltip("Hour the day starts at when you press Play (5 = dawn, 7.5 = morning, 16.5 = golden hour).")]
        [Range(4.5f, 20.5f)] public float startHour = 7.5f;
        [Tooltip("Real minutes per in-game hour. 1 = a day of daylight in about sixteen minutes. Hold T in game to let time pass.")]
        [Range(0.2f, 5f)] public float minutesPerHour = 1f;

        [Header("Scene")]
        public bool configureLightingAndFog = true;
        [Tooltip("Raise shadow distance/cascades/resolution in the active render pipeline or Quality settings (project-wide).")]
        public bool configureRenderQuality = true;
        [Tooltip("URP only: add a global colour-grading volume (tonemapping, white balance, bloom, vignette).")]
        public bool postProcessing = true;
        public bool disableOtherCameras = true;

        [Header("Fallback terrain (only if the scene has none)")]
        public Vector3 fallbackTerrainSize = new Vector3(400f, 40f, 400f);
    }
}
