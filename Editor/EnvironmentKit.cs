using System;
using UnityEngine;

namespace WildLens.EditorTools
{
    /// <summary>
    /// One terrain grass / flower slot: an instanced mesh prefab (preferred, e.g. from Unity's free Terrain Sample Asset
    /// Pack) or a billboard texture. Empty: the generated WildLens grass is used.
    /// </summary>
    [Serializable]
    public class KitDetail
    {
        [Tooltip("A small plant prefab drawn as an instanced terrain detail mesh (Unity 2022.2+ draws it with its own material).")]
        public GameObject mesh;
        [Tooltip("Or a grass/flower texture drawn as a billboard (used only when no mesh is set).")]
        public Texture2D texture;
        [Tooltip("Multiplies how many are planted. Mesh grass usually covers more than a billboard: start around 0.5.")]
        [Range(0.05f, 2f)] public float density = 1f;
        [Tooltip("Size range. Meshes: scale of the prefab as authored. Textures: multiplies the generated grass sizes.")]
        public Vector2 scale = new Vector2(0.85f, 1.2f);

        public bool IsSet => mesh != null || texture != null;
    }

    /// <summary>
    /// The Environment Kit (1.2): free nature assets the user has imported into their own project (Unity's free terrain
    /// demo and sample packs, free tree and rock packs, more CC0 scans), sorted by the ecological role each one plays in
    /// the reserve. The builder puts each role where it belongs: conifers and broadleaf by the forest field, riparian trees
    /// along Willow Creek, old-growth giants in the grove, young trees at woodland edges, shrubs on edges, rocks in
    /// outcrops, and the terrain layers and grass in their painted zones. Every placed asset gets the colliders and
    /// <see cref="WildLens.Environment.EnvironmentFeature"/> data wildlife and photography need, so gameplay is unchanged.
    ///
    /// Store assets are never committed to the WildLens repository (their licences forbid redistribution); the kit only
    /// references assets in the user's project. An empty kit changes nothing: the bundled CC0 art and generated trees are
    /// used. Fill it with <b>WildLens ▸ Environment Kit ▸ Scan Project for Nature Assets</b>, then review.
    /// </summary>
    public class EnvironmentKit : ScriptableObject
    {
        [Header("Trees by ecological role")]
        [Tooltip("Pine, fir, spruce, hemlock: the dark woodland and the ridge.")]
        public GameObject[] conifers = new GameObject[0];
        [Tooltip("Oak, maple, beech and other broadleaf trees: most woodland, meadow edges and lone meadow trees.")]
        public GameObject[] broadleaf = new GameObject[0];
        [Tooltip("Birch, willow, alder, aspen: favoured along Willow Creek. Empty: broadleaf trees are used there.")]
        public GameObject[] riparian = new GameObject[0];
        [Tooltip("The biggest, oldest broadleaf trees: the grove's giants and the Grandmother Oak. Empty: broadleaf, scaled up.")]
        public GameObject[] oldGrowth = new GameObject[0];
        [Tooltip("Saplings and young trees where woodland thins into grassland. Empty: the role's trees, scaled down.")]
        public GameObject[] young = new GameObject[0];
        [Tooltip("Size multiplier for every kit tree (packs differ in how big they model trees).")]
        [Range(0.5f, 2f)] public float treeScale = 1f;
        [Tooltip("Once any kit tree exists, missing roles borrow from other roles instead of using generated trees, so " +
                 "photographic and generated trees are never mixed. Off: missing roles use the generated trees.")]
        public bool replaceAllGeneratedTrees = true;

        [Header("Shrubs, ground cover and geology")]
        public GameObject[] shrubs = new GameObject[0];
        public GameObject[] ferns = new GameObject[0];
        [Tooltip("Rocks up to ~2.5 m: outcrop pieces and creek stones.")]
        public GameObject[] rocks = new GameObject[0];
        [Tooltip("Large lone rocks in the open: landmarks and hawk perches.")]
        public GameObject[] boulders = new GameObject[0];
        public GameObject[] logs = new GameObject[0];
        public GameObject[] stumps = new GameObject[0];
        [Tooltip("On: kit shrubs, ferns, rocks, logs and stumps join the bundled CC0 scans. Off: they replace them " +
                 "(per category, only where the kit has any).")]
        public bool mixWithBundledArt = true;

        [Header("Terrain surface (painted by ecology; empty slots keep the bundled scans)")]
        public TerrainLayer grass;
        public TerrainLayer meadow;
        public TerrainLayer forestFloor;
        public TerrainLayer dirt;
        public TerrainLayer rock;

        [Header("Terrain grass and flowers (empty slots keep the generated grass)")]
        public KitDetail shortGrass = new KitDetail();
        public KitDetail meadowGrass = new KitDetail();
        public KitDetail yellowFlowers = new KitDetail();
        public KitDetail whiteFlowers = new KitDetail();
        public KitDetail fern = new KitDetail();
        public KitDetail purpleFlowers = new KitDetail();

        [Header("Last scan")]
        [TextArea(4, 20)] public string scanReport = "";

        public bool HasTrees => Any(conifers) || Any(broadleaf) || Any(riparian) || Any(oldGrowth) || Any(young);

        /// <summary>Terrain layer slots in the painter's order: grass, meadow, forest floor, dirt, rock.</summary>
        public TerrainLayer[] Layers => new[] { grass, meadow, forestFloor, dirt, rock };

        /// <summary>Detail slots in the painter's order: grass, meadow grass, yellow, white, fern, purple.</summary>
        public KitDetail[] Details => new[] { shortGrass, meadowGrass, yellowFlowers, whiteFlowers, fern, purpleFlowers };

        public bool IsEmpty
        {
            get
            {
                if (HasTrees || Any(shrubs) || Any(ferns) || Any(rocks) || Any(boulders) || Any(logs) || Any(stumps)) return false;
                foreach (var l in Layers) if (l != null) return false;
                foreach (var d in Details) if (d != null && d.IsSet) return false;
                return true;
            }
        }

        public static bool Any(GameObject[] list)
        {
            if (list == null) return false;
            foreach (var g in list) if (g != null) return true;
            return false;
        }

        /// <summary>One-line summary for the builder window and logs.</summary>
        public string Summary()
        {
            int Count(GameObject[] a) { int n = 0; if (a != null) foreach (var g in a) if (g != null) n++; return n; }
            int layers = 0, details = 0;
            foreach (var l in Layers) if (l != null) layers++;
            foreach (var d in Details) if (d != null && d.IsSet) details++;
            return $"trees {Count(conifers)} conifer · {Count(broadleaf)} broadleaf · {Count(riparian)} riparian · " +
                   $"{Count(oldGrowth)} old-growth · {Count(young)} young; shrubs {Count(shrubs)}, ferns {Count(ferns)}, " +
                   $"rocks {Count(rocks)}, boulders {Count(boulders)}, logs {Count(logs)}, stumps {Count(stumps)}; " +
                   $"terrain layers {layers}/5, grass & flowers {details}/6";
        }
    }
}
