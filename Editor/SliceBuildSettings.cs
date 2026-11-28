using System.Collections.Generic;
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

    /// <summary>
    /// What <see cref="AnimalPrefabFactory"/> last built for a species (1.8): the model it came from, which factory version made it
    /// and the size, yaw and tint used. <see cref="manual"/> is true when a person chose those values (Wildlife Pack Setup,
    /// Animal Model Setup): such a prefab is never rebuilt automatically.
    /// </summary>
    [System.Serializable]
    public class AnimalBuildRecord
    {
        public string speciesId;
        public string sourceGuid;
        public int factoryVersion;
        public bool manual;
        public float size;
        public float yaw;
        public bool tint;
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
        [Tooltip("Free nature assets imported into this project (Unity's terrain demo and sample packs, free tree and rock packs), " +
                 "sorted by ecological role. Fill it with WildLens > Environment Kit > Scan Project for Nature Assets. " +
                 "Empty or unset: the bundled CC0 art and generated trees are used.")]
        public EnvironmentKit environmentKit;
        [Tooltip("Any trees, placed at random (older option). The Environment Kit above places trees by ecological role instead.")]
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
        [Tooltip("Regenerate WildLens's own terrain from the seed on every build (1.4 landforms: lowland, upland ridges, valleys, " +
                 "shelves), so rebuilds are repeatable and terrain improvements arrive. Your own terrains are never touched.")]
        public bool regenerateWildLensTerrain = true;

        [Header("Time of day (0.9)")]
        [Tooltip("Dawn → morning → midday → golden hour → dusk, with lighting, mist and wildlife rhythms. Off: one fixed moment (the sky mood above).")]
        public bool timeOfDay = true;
        [Tooltip("Hour the day starts at when you press Play (5 = dawn, 7.5 = morning, 16.5 = golden hour).")]
        [Range(4.5f, 20.5f)] public float startHour = 7.5f;
        [Tooltip("Real minutes per in-game hour. 1 = a day of daylight in about sixteen minutes. Hold T in game to let time pass.")]
        [Range(0.2f, 5f)] public float minutesPerHour = 1f;
        [Tooltip("Weather (1.4): a four-day forecast that repeats (fair · grey morning · afternoon shower · overcast with dusk rain). " +
                 "Off: always fair.")]
        public bool weather = true;
        [Tooltip("Which forecast day the first day is: 0 fair, 1 grey morning, 2 afternoon shower, 3 overcast with rain at dusk. " +
                 "Handy for screenshots of a particular weather.")]
        [Range(0, 3)] public int firstWeatherDay = 0;

        [Header("Wildlife expansion (1.0)")]
        [Tooltip("No longer needed: since 1.1 all species are always built (twenty-four in 1.8). Kept so older settings assets load. " +
                 "See Documentation~/WildlifeExpansion.md before adding your own species.")]
        public bool previewExpansionSpecies = false;

        [Header("Wildlife models (1.6)")]
        [Tooltip("On every rebuild, and whenever animal packs are imported, give each species that has no model yet the best " +
                 "matching animated model found in the project (the animal itself, else a stand-in, tinted). Untick to only " +
                 "use WildLens ▸ Wildlife Pack Setup by hand.")]
        public bool autoWildlifeFromPacks = true;
        [Tooltip("Species kept as placeholder bodies: automatic set-up never gives these a model. The Placeholder button in " +
                 "WildLens > Wildlife Pack Setup adds a species here; setting a model for it (the window or Animal Model Setup) removes it. " +
                 "Settings made before 1.7.1 load with this empty.")]
        public List<string> keepPlaceholder = new List<string>();
        [HideInInspector]
        [Tooltip("1.8: how each species prefab was built (source model, factory version, size, yaw, tint, and whether you chose them). " +
                 "Lets automatic set-up refresh prefabs made by an older version without touching ones you tuned. Older settings load with this empty.")]
        public List<AnimalBuildRecord> animalBuilds = new List<AnimalBuildRecord>();
        [Tooltip("On rebuild, while the Environment Kit is empty, scan the project for free nature packs (trees, shrubs, rocks, " +
                 "ground and grass) and use them. A kit that already has contents is left as it is.")]
        public bool autoEnvironmentKit = true;

        [Header("Small life (1.7)")]
        [Tooltip("On every rebuild, give each macro subject that has no model yet (butterflies, bees, dragonflies, frogs, newts, snails, " +
                 "fireflies, mushrooms, daisies) the best matching model or prefab found in the project, then place them in their " +
                 "habitats. Untick to only use WildLens ▸ Macro Subjects From Packs by hand.")]
        public bool autoMacroFromPacks = true;
        [Tooltip("1.9: a subject with no model from your packs gets one WildLens generates (butterflies, a dragonfly and damselfly, bees, a firefly, " +
                 "a ladybird, frogs, newts, a snail, mushrooms, wildflowers, moss: small stylised models made in code and saved under " +
                 "Assets/WildLens/Generated/SmallLife). A pack model always wins. Untick to leave small life out unless your packs supply it.")]
        public bool generatedSmallLife = true;

        [Header("Reserve geography (1.9)")]
        [Tooltip("Hemlock Ravine: where the land falls far enough toward Willow Creek, a steep, shaded side valley is cut by a small " +
                 "tributary (Hemlock Run) with a stepped cascade at its head, reached by one footpath. Skipped (one Console line says why) " +
                 "on terrain without the relief. Needs 'Shape terrain at destinations'.")]
        public bool hemlockRavine = true;
        [Tooltip("Four more unmarked hidden places, found and named once, never counted: the Cascade Overlook (above the ravine's " +
                 "cascade), a Hidden Pond in a wooded hollow, a Rock Shelter under an overhanging crag, and Old Cabin Ruins with a " +
                 "bluebird's fence. Each is skipped cleanly where the land offers no site.")]
        public bool moreHiddenPlaces = true;

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
