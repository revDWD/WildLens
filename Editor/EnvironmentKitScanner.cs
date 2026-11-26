using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WildLens.EditorTools
{
    /// <summary>
    /// WildLens ▸ Environment Kit ▸ Scan Project for Nature Assets (1.2). Looks through the user's project (never the
    /// WildLens package or its generated folder) for nature assets from free packs they imported, and sorts them into
    /// the <see cref="EnvironmentKit"/>'s ecological roles:
    ///   * prefabs, models and SpeedTree assets, classified by the words in their names and folders (pine → conifer,
    ///     birch → riparian, oak → broadleaf, sapling → young, shrub, fern, rock/boulder by size, log, stump, grass and
    ///     flowers); unnamed trees by silhouette (narrow and tall → conifer);
    ///   * every TerrainData in the project (a demo scene's terrain): its tree prototypes, detail meshes/textures and
    ///     terrain layers, which is how the trees and grass of Unity's free terrain demo are found;
    ///   * TerrainLayer assets, by name (grass, forest floor, dirt, rock).
    /// Rejected, with the reason in the report: stylised/low-poly/fantasy art (fidelities must not mix), the wrong biome
    /// (palm, cactus, snow, autumn), dead trees, LOD/billboard/collider parts, and anything whose shaders don't belong to
    /// the active render pipeline (it would render pink; run the Render Pipeline Converter, then scan again).
    /// The scan only fills empty slots unless asked to replace; review the kit afterwards, it is only a first guess.
    /// </summary>
    public static class EnvironmentKitScanner
    {
        public const string KitPath = GeneratedAssets.DataFolder + "/EnvironmentKit.asset";
        private const int MaxPerRole = 8;

        [MenuItem("WildLens/Environment Kit/Scan Project for Nature Assets", priority = 30)]
        public static void ScanMenu() => ScanAndAssign(false);

        [MenuItem("WildLens/Environment Kit/Rescan (Replace Kit Contents)", priority = 31)]
        public static void RescanMenu()
        {
            if (EditorUtility.DisplayDialog("Rescan Environment Kit",
                    "Replace everything in the Environment Kit with a fresh scan? Hand-made changes to the kit are lost.", "Replace", "Cancel"))
                ScanAndAssign(true);
        }

        [MenuItem("WildLens/Environment Kit/Select Kit", priority = 32)]
        public static void SelectKit()
        {
            var kit = LoadOrCreateKit();
            Selection.activeObject = kit;
            EditorGUIUtility.PingObject(kit);
        }

        public static EnvironmentKit LoadOrCreateKit()
        {
            GeneratedAssets.EnsureFolders();
            var kit = AssetDatabase.LoadAssetAtPath<EnvironmentKit>(KitPath);
            if (kit != null) return kit;
            kit = ScriptableObject.CreateInstance<EnvironmentKit>();
            AssetDatabase.CreateAsset(kit, KitPath);
            AssetDatabase.SaveAssets();
            return kit;
        }

        /// <summary>Scans, stores the result in the kit, and points the builder settings at the kit.</summary>
        public static EnvironmentKit ScanAndAssign(bool replace)
        {
            var kit = LoadOrCreateKit();
            Undo.RecordObject(kit, "Scan Environment Kit");
            try { Scan(kit, replace); }
            finally { EditorUtility.ClearProgressBar(); }
            EditorUtility.SetDirty(kit);
            var settings = WildLensSliceBuilder.LoadOrCreateSettings();
            if (settings.environmentKit != kit)
            {
                Undo.RecordObject(settings, "Assign Environment Kit");
                settings.environmentKit = kit;
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = kit;
            Debug.Log("[WildLens] Environment Kit scan:\n" + kit.scanReport);
            EditorUtility.DisplayDialog("Environment Kit", kit.IsEmpty
                ? "No usable nature assets were found in this project.\n\nImport a free pack first (see the kit's report, or Documentation~/EnvironmentMigration.md in the WildLens package), then scan again."
                : "Found: " + kit.Summary() + "\n\nReview the kit (it is selected), then Rebuild the slice.", "OK");
            return kit;
        }

        // ------------------------------------------------------------------ scan

        private enum Role { None, Conifer, Broadleaf, Riparian, OldGrowth, Young, Shrub, Fern, Rock, Boulder, Log, Stump, Detail }

        private class Found
        {
            public GameObject prefab;
            public string path;
            public Role role;
            public float height, width;
            public bool explicitOld;
        }

        private class DetailFound
        {
            public GameObject mesh;
            public Texture2D texture;
            public string path;
            public List<string> tokens;
        }

        private static readonly string[] Excluded =
        {
            "stylized", "stylised", "toon", "cartoon", "lowpoly", "fantasy", "palm", "cactus", "cacti", "tropical", "desert",
            "snow", "snowy", "winter", "autumn", "mobile", "burnt", "burned", "alien", "voxel", "pixel", "handpainted", "polygon",
            "synty", "pot", "potted", "vase", "planter", "indoor", "plastic",
        };
        private static readonly string[] Parts = { "billboard", "impostor", "imposter", "collider", "collision", "proxy" };
        private static readonly string[] ConiferWords = { "pine", "fir", "spruce", "conifer", "hemlock", "cedar", "larch", "douglas", "sequoia", "redwood", "evergreen", "yew" };
        private static readonly string[] RiparianWords = { "birch", "aspen", "alder", "willow", "poplar", "cottonwood", "sycamore" };
        private static readonly string[] BroadleafWords = { "oak", "maple", "beech", "elm", "linden", "lime", "chestnut", "hornbeam", "walnut", "hickory", "ash", "broadleaf", "broadleave", "deciduous", "hardwood" };
        private static readonly string[] TreeWords = { "tree" };
        private static readonly string[] OldWords = { "old", "giant", "ancient", "huge", "big", "large", "veteran" };
        private static readonly string[] YoungWords = { "sapling", "seedling", "young" };
        private static readonly string[] ShrubWords = { "bush", "shrub", "hedge", "shrubbery", "hazel", "juniper", "huckleberry", "blueberry", "rhododendron", "laurel", "holly", "bramble", "blackberry" };
        private static readonly string[] FernWords = { "fern", "bracken" };
        private static readonly string[] RockWords = { "rock", "stone", "boulder", "cliff", "granite", "outcrop", "pebble" };
        private static readonly string[] LogWords = { "log", "deadwood", "driftwood", "fallen", "branch", "branches" };
        private static readonly string[] StumpWords = { "stump" };
        private static readonly string[] DetailWords = { "grass", "flower", "clover", "weed", "dandelion", "daisy", "wildflower", "plant", "lupin", "lupine", "aster", "buttercup", "yarrow", "thistle", "goldenrod", "meadow", "herb", "sedge", "bluebell", "poppy" };
        private static readonly string[] DeadWords = { "dead", "snag", "dry" };

        public static void Scan(EnvironmentKit kit, bool replace)
        {
            var report = new StringBuilder();
            var rejected = new List<string>();
            var found = new List<Found>();
            var details = new List<DetailFound>();
            var layers = new List<(TerrainLayer layer, string path)>();
            var seen = new HashSet<GameObject>();
            string pipeline = PipelineName();

            // 1. Terrains in the project (demo scenes): their prototypes are vetted vegetation.
            var terrainGuids = AssetDatabase.FindAssets("t:TerrainData");
            foreach (string guid in terrainGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Skip(path)) continue;
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                if (data == null) continue;
                // A terrain WildLens already painted holds WildLens's own grass and layers: those are skipped by path.
                foreach (var tp in data.treePrototypes)
                    if (tp != null && tp.prefab != null && !Skip(AssetDatabase.GetAssetPath(tp.prefab)) && seen.Add(tp.prefab))
                        Consider(tp.prefab, AssetDatabase.GetAssetPath(tp.prefab), true, found, rejected);
                foreach (var dp in data.detailPrototypes)
                {
                    if (dp == null) continue;
                    if (dp.usePrototypeMesh && dp.prototype != null)
                    {
                        if (Skip(AssetDatabase.GetAssetPath(dp.prototype))) continue;
                        if (!seen.Add(dp.prototype)) continue;
                        string problem = PipelineProblem(dp.prototype, pipeline);
                        if (problem != null) { rejected.Add($"{AssetDatabase.GetAssetPath(dp.prototype)}: {problem}"); continue; }
                        string p = AssetDatabase.GetAssetPath(dp.prototype);
                        details.Add(new DetailFound { mesh = dp.prototype, path = p, tokens = Tokens(p) });
                    }
                    else if (!dp.usePrototypeMesh && dp.prototypeTexture != null)
                    {
                        string p = AssetDatabase.GetAssetPath(dp.prototypeTexture);
                        if (Skip(p)) continue;
                        details.Add(new DetailFound { texture = dp.prototypeTexture, path = p, tokens = Tokens(p) });
                    }
                }
                foreach (var l in data.terrainLayers)
                {
                    string lp = l != null ? AssetDatabase.GetAssetPath(l) : null;
                    if (l != null && l.diffuseTexture != null && !Skip(lp) && !layers.Exists(x => x.layer == l)) layers.Add((l, lp));
                }
            }

            // 2. Terrain layers anywhere in the project.
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainLayer"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Skip(path)) continue;
                var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (l != null && l.diffuseTexture != null && !layers.Exists(x => x.layer == l)) layers.Add((l, path));
            }

            // 3. Prefabs, models and SpeedTree assets. A prefab wins over the model it wraps (same file name).
            var guids = AssetDatabase.FindAssets("t:GameObject");
            var paths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Skip(path)) paths.Add(path);
            }
            var prefabNames = new HashSet<string>(paths.Where(p => p.EndsWith(".prefab")).Select(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant()));
            paths.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (i % 50 == 0) EditorUtility.DisplayProgressBar("WildLens", "Scanning for nature assets… " + Path.GetFileName(path), i / (float)paths.Count);
                if (!path.EndsWith(".prefab") && prefabNames.Contains(Path.GetFileNameWithoutExtension(path).ToLowerInvariant())) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || !seen.Add(go)) continue;
                var tokens = Tokens(path);
                var nameTokens = Tokens(Path.GetFileNameWithoutExtension(path));
                // Grass and flowers: by file name, or by folder when the name says nothing else (a rock in "Plants" is a rock).
                bool detail = Has(nameTokens, DetailWords) || (Has(tokens, DetailWords) && !LooksNatural(nameTokens));
                if (detail && !Has(nameTokens, TreeWords) && !Has(nameTokens, ShrubWords))
                {
                    Bounds db = Measure(go);
                    if (db.size.y > 0.02f && db.size.y < 1.6f && PipelineProblem(go, pipeline) == null && !Unfit(tokens))
                    {
                        details.Add(new DetailFound { mesh = go, path = path, tokens = tokens });
                        continue;
                    }
                }
                Consider(go, path, false, found, rejected);
            }

            // 4. Fill the kit.
            Fill(ref kit.conifers, Pick(found, Role.Conifer), replace);
            Fill(ref kit.broadleaf, Pick(found, Role.Broadleaf), replace);
            Fill(ref kit.riparian, Pick(found, Role.Riparian), replace);
            var old = Pick(found, Role.OldGrowth);
            if (old.Count == 0) old = TallestBroadleaf(found); // no named giants: the biggest broadleaf trees
            Fill(ref kit.oldGrowth, old, replace);
            Fill(ref kit.young, Pick(found, Role.Young), replace);
            Fill(ref kit.shrubs, Pick(found, Role.Shrub), replace);
            Fill(ref kit.ferns, Pick(found, Role.Fern), replace);
            Fill(ref kit.rocks, Pick(found, Role.Rock), replace);
            Fill(ref kit.boulders, Pick(found, Role.Boulder), replace);
            Fill(ref kit.logs, Pick(found, Role.Log), replace);
            Fill(ref kit.stumps, Pick(found, Role.Stump), replace);
            FillLayers(kit, layers, replace);
            FillDetails(kit, details, replace);

            report.AppendLine($"Scanned {paths.Count} models/prefabs, {terrainGuids.Length} terrains, {layers.Count} terrain layers ({pipeline}).");
            report.AppendLine("Kit: " + kit.Summary());
            report.AppendLine($"Rejected {rejected.Count}" + (rejected.Count > 0 ? " (first 25):" : "."));
            foreach (var r in rejected.Take(25)) report.AppendLine("  " + r);
            if (rejected.Exists(r => r.Contains("pipeline")))
                report.AppendLine("Some assets use shaders from another render pipeline. Window > Rendering > Render Pipeline Converter " +
                                  "(or the pack's own URP package) fixes them; then scan again.");
            report.AppendLine("Review each role (names can mislead), then Rebuild the slice. Empty slots keep the bundled art.");
            kit.scanReport = report.ToString();
        }

        // ------------------------------------------------------------------ classification

        private static void Consider(GameObject go, string path, bool fromTerrain, List<Found> found, List<string> rejected)
        {
            var tokens = Tokens(path);
            var nameTokens = Tokens(Path.GetFileNameWithoutExtension(path));
            if (Unfit(tokens)) { if (LooksNatural(tokens)) rejected.Add($"{path}: style or biome doesn't fit the reserve"); return; }
            // LOD1+, billboards and colliders are parts of a tree, not trees (LOD0 is often the pack's main asset).
            if (Regex.IsMatch(Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), "lod[1-9]") ||
                nameTokens.Any(t => Parts.Any(p => t.StartsWith(p)))) return;
            if (!fromTerrain && !LooksNatural(tokens)) return;

            Bounds b = Measure(go);
            float h = b.size.y, w = Mathf.Max(b.size.x, b.size.z), max = Mathf.Max(h, w);
            if (max < 0.05f) return;
            Role role = Classify(tokens, nameTokens, h, w, fromTerrain, out bool explicitOld, out string why);
            if (role == Role.None) { if (why != null) rejected.Add($"{path}: {why}"); return; }
            string problem = PipelineProblem(go, PipelineName());
            if (problem != null) { rejected.Add($"{path}: {problem}"); return; }
            found.Add(new Found { prefab = go, path = path, role = role, height = h, width = w, explicitOld = explicitOld });
        }

        private static bool LooksNatural(List<string> t) =>
            Has(t, ConiferWords) || Has(t, RiparianWords) || Has(t, BroadleafWords) || Has(t, TreeWords) || Has(t, ShrubWords) ||
            Has(t, FernWords) || Has(t, RockWords) || Has(t, LogWords) || Has(t, StumpWords) || Has(t, YoungWords);

        private static Role Classify(List<string> t, List<string> name, float h, float w, bool fromTerrain, out bool explicitOld, out string why)
        {
            explicitOld = false;
            why = null;
            float max = Mathf.Max(h, w);
            // The file name decides first; folder names only break ties (a "Rocks" folder may hold a tree).
            List<string> primary = LooksNatural(name) ? name : t;
            if (Has(primary, StumpWords)) return h < 3f ? Role.Stump : Role.None;
            if (Has(primary, LogWords) || (Has(primary, new[] { "trunk" }) && !Has(primary, TreeWords)))
                return max < 12f ? Role.Log : Role.None;
            if (Has(primary, FernWords)) return h < 2.5f ? Role.Fern : Role.None;
            if (Has(primary, RockWords))
            {
                if (max < 0.3f) return Role.None;
                if (max > 12f) { why = "rock too large to scatter (a cliff)"; return Role.None; }
                return max > 2.5f ? Role.Boulder : Role.Rock;
            }
            if (Has(primary, ShrubWords)) return h < 5f ? Role.Shrub : Role.Broadleaf;

            bool treeish = fromTerrain || Has(primary, TreeWords) || Has(primary, ConiferWords) || Has(primary, RiparianWords) ||
                           Has(primary, BroadleafWords) || Has(primary, YoungWords);
            if (!treeish) return Role.None;
            if (Has(primary, DeadWords)) { why = "dead tree (not used yet)"; return Role.None; }
            if (h < 1.5f) return Role.None;
            if (Has(primary, YoungWords) || (Has(primary, new[] { "small" }) && h < 7f) || h < 3.5f) return Role.Young;
            if (Has(primary, ConiferWords)) return Role.Conifer;
            if (Has(primary, RiparianWords)) return Role.Riparian;
            if (Has(primary, BroadleafWords))
            {
                explicitOld = Has(primary, OldWords);
                return explicitOld ? Role.OldGrowth : Role.Broadleaf;
            }
            // An unnamed tree: a narrow, tall silhouette is a conifer.
            return w / Mathf.Max(0.1f, h) < 0.42f ? Role.Conifer : Role.Broadleaf;
        }

        private static List<GameObject> Pick(List<Found> found, Role role) =>
            found.Where(f => f.role == role).OrderBy(f => f.path, System.StringComparer.Ordinal).Take(MaxPerRole).Select(f => f.prefab).ToList();

        private static List<GameObject> TallestBroadleaf(List<Found> found)
        {
            var broad = found.Where(f => f.role == Role.Broadleaf).OrderByDescending(f => f.height).ToList();
            if (broad.Count < 3) return new List<GameObject>();
            float median = broad[broad.Count / 2].height;
            return broad.Where(f => f.height >= median * 1.25f && f.height > 12f).Take(3).Select(f => f.prefab).ToList();
        }

        private static void Fill(ref GameObject[] slot, List<GameObject> items, bool replace)
        {
            if (items.Count == 0) { if (replace) slot = new GameObject[0]; return; }
            if (replace || !EnvironmentKit.Any(slot)) slot = items.ToArray();
        }

        private static void FillLayers(EnvironmentKit kit, List<(TerrainLayer layer, string path)> layers, bool replace)
        {
            var grassy = new List<TerrainLayer>();
            TerrainLayer forest = null, dirt = null, rock = null;
            foreach (var (layer, path) in layers.OrderBy(l => l.path, System.StringComparer.Ordinal))
            {
                var t = Tokens(path + " " + layer.name + " " + (layer.diffuseTexture != null ? layer.diffuseTexture.name : ""));
                if (Unfit(t) || Has(t, new[] { "sand", "beach", "lava", "volcanic", "ice", "asphalt", "concrete", "brick", "tile" })) continue;
                if (Has(t, new[] { "forest", "leaves", "leaf", "litter", "needle", "needles", "humus", "moss", "mossy", "floor" })) forest = forest ?? layer;
                else if (Has(t, new[] { "grass", "grassy", "meadow", "lawn", "field" })) grassy.Add(layer);
                else if (Has(t, new[] { "dirt", "mud", "soil", "earth", "path", "trail", "track" })) dirt = dirt ?? layer;
                else if (Has(t, new[] { "rock", "rocky", "cliff", "stone", "gravel", "scree", "granite" })) rock = rock ?? layer;
            }
            void Set(ref TerrainLayer slot, TerrainLayer value) { if (value != null && (replace || slot == null)) slot = value; else if (replace) slot = value; }
            Set(ref kit.grass, grassy.Count > 0 ? grassy[0] : null);
            Set(ref kit.meadow, grassy.Count > 1 ? grassy[1] : null);
            Set(ref kit.forestFloor, forest);
            Set(ref kit.dirt, dirt);
            Set(ref kit.rock, rock);
        }

        private static void FillDetails(EnvironmentKit kit, List<DetailFound> found, bool replace)
        {
            // Meshes first (they look far better than billboards), then by path for a stable result.
            var list = found.Where(d => !Unfit(d.tokens)).OrderBy(d => d.mesh != null ? 0 : 1).ThenBy(d => d.path, System.StringComparer.Ordinal).ToList();
            var used = new HashSet<Object>();
            DetailFound Take(System.Func<DetailFound, bool> match)
            {
                foreach (var d in list)
                {
                    Object key = d.mesh != null ? (Object)d.mesh : d.texture;
                    if (used.Contains(key) || !match(d)) continue;
                    used.Add(key);
                    return d;
                }
                return null;
            }
            bool Grass(DetailFound d) => Has(d.tokens, new[] { "grass", "sedge", "meadow" }) && !Has(d.tokens, new[] { "flower" });
            bool Flower(DetailFound d) => Has(d.tokens, new[] { "flower", "wildflower", "daisy", "dandelion", "lupin", "lupine", "aster", "buttercup", "yarrow", "thistle", "goldenrod", "clover", "bluebell", "poppy" });
            bool Colour(DetailFound d, params string[] words) => Flower(d) && Has(d.tokens, words);

            var shortGrass = Take(d => Grass(d) && Has(d.tokens, new[] { "short", "small", "lawn", "low" })) ?? Take(Grass);
            var meadowGrass = Take(d => Grass(d) && Has(d.tokens, new[] { "tall", "meadow", "wild", "dry", "seed", "long" })) ?? Take(Grass);
            var yellow = Take(d => Colour(d, "yellow", "buttercup", "dandelion", "goldenrod", "gold"));
            var white = Take(d => Colour(d, "white", "daisy", "yarrow"));
            var purple = Take(d => Colour(d, "purple", "violet", "lupin", "lupine", "aster", "lavender", "thistle", "blue", "bluebell", "pink", "clover"));
            yellow = yellow ?? Take(Flower);
            white = white ?? Take(Flower);
            purple = purple ?? Take(Flower);
            var fern = Take(d => Has(d.tokens, FernWords));

            void Set(KitDetail slot, DetailFound d)
            {
                if (slot == null) return;
                if (d == null) { if (replace) { slot.mesh = null; slot.texture = null; } return; }
                if (!replace && slot.IsSet) return;
                slot.mesh = d.mesh;
                slot.texture = d.mesh == null ? d.texture : null;
                slot.density = d.mesh != null ? 0.5f : 1f;
                slot.scale = new Vector2(0.85f, 1.2f);
            }
            Set(kit.shortGrass, shortGrass);
            Set(kit.meadowGrass, meadowGrass);
            Set(kit.yellowFlowers, yellow);
            Set(kit.whiteFlowers, white);
            Set(kit.purpleFlowers, purple);
            Set(kit.fern, fern);
        }

        // ------------------------------------------------------------------ helpers

        private static bool Skip(string path) =>
            string.IsNullOrEmpty(path) || !path.StartsWith("Assets/") || path.StartsWith(GeneratedAssets.Root + "/") ||
            path.Contains("/Editor/") || path.Contains("/Gizmos/");

        /// <summary>Lower-case words of a path: camelCase and separators split, digits dropped ("SM_Fir03_LOD0" → sm, fir, lod).</summary>
        private static List<string> Tokens(string path)
        {
            string s = Regex.Replace(path, "([a-z])([A-Z])", "$1 $2");
            return Regex.Split(s.ToLowerInvariant(), "[^a-z]+").Where(x => x.Length > 1).ToList();
        }

        /// <summary>Stylised, low-poly or wrong-biome art (also catches "LowPoly" and "Hand_Painted", split into two words).</summary>
        private static bool Unfit(List<string> t)
        {
            if (Has(t, Excluded)) return true;
            for (int i = 0; i + 1 < t.Count; i++)
                if ((t[i] == "low" && t[i + 1] == "poly") || (t[i] == "hand" && t[i + 1] == "painted")) return true;
            return false;
        }

        private static bool Has(List<string> tokens, string[] words)
        {
            foreach (var t in tokens)
                foreach (var w in words)
                    if (t == w || t == w + "s" || t == w + "es") return true;
            return false;
        }

        /// <summary>Size of the asset as it would be instantiated (mesh bounds through the hierarchy, root scale kept).</summary>
        private static Bounds Measure(GameObject root)
        {
            Matrix4x4 toRoot = Matrix4x4.Scale(root.transform.localScale) * root.transform.worldToLocalMatrix;
            bool any = false;
            var b = new Bounds();
            void Add(Mesh mesh, Transform t)
            {
                if (mesh == null) return;
                Matrix4x4 m = toRoot * t.localToWorldMatrix;
                Bounds mb = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) Add(mf.sharedMesh, mf.transform);
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Add(smr.sharedMesh, smr.transform);
            return b;
        }

        private static string PipelineName()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp == null) return "Built-in";
            string n = rp.GetType().Name;
            return n.Contains("Universal") ? "URP" : n.Contains("HDRenderPipeline") ? "HDRP" : n;
        }

        /// <summary>Why this asset would render wrongly in the active pipeline, or null if its shaders fit.</summary>
        private static string PipelineProblem(GameObject go, string pipeline)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) return "a material is missing";
                    var sh = m.shader;
                    if (sh == null || sh.name == "Hidden/InternalErrorShader") return "a shader is broken (pink)";
                    string n = sh.name;
                    bool hdrp = n.StartsWith("HDRP/") || n.StartsWith("HDRenderPipeline");
                    bool urp = n.StartsWith("Universal Render Pipeline/");
                    bool builtin = n == "Standard" || n == "Standard (Specular setup)" || n.StartsWith("Legacy Shaders/") ||
                                   n.StartsWith("Nature/Tree Creator") || n == "Nature/SpeedTree" || n == "Nature/SpeedTree8" ||
                                   n.StartsWith("Nature/Tree Soft Occlusion");
                    if (pipeline == "URP" && (hdrp || builtin)) return $"shader '{n}' is for another render pipeline";
                    if (pipeline == "Built-in" && (hdrp || urp)) return $"shader '{n}' is for another render pipeline";
                    if (pipeline == "HDRP" && (urp || builtin)) return $"shader '{n}' is for another render pipeline";
                }
            }
            return null;
        }
    }
}
