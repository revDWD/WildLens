using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WildLens.Data;
using WildLens.Core;
using static WildLens.EditorTools.WildlifePackSetup;

namespace WildLens.EditorTools
{
    /// <summary>
    /// WildLens ▸ Asset Audit Report (1.7): a probe, not a builder. The director cannot see which animal, macro and
    /// vehicle packs were imported, so this walks every model and prefab under Assets/, classifies each by name (then by
    /// the folder it sits in) and writes WildLensAssetAudit.md next to the project's Assets folder (and to the
    /// clipboard) to paste into the chat. It changes nothing in the project. Categories: reserve species (exact or
    /// stand-in), other animals (possible new species), macro subjects, vehicles (for the bike drop-in), scenery
    /// (counts only) and animated models nothing recognised.
    /// </summary>
    public static class AssetAudit
    {
        public const string ReportFileName = "WildLensAssetAudit.md";
        private const int RowCap = 400;
        private const int ClipCap = 40;
        private const int ClipboardLimit = 60000;
        private const int OtherCap = 150;
        private const int OtherPerPack = 30;

        private enum Cat { Species, OtherAnimal, Macro, Vehicle, Scenery, UncategorisedAnimated, OtherStatic }

        private class Entry
        {
            public string[] keys;
            public string group, role;
            public bool bird, exotic;
        }

        private static Entry E(string group, string role, bool bird, bool exotic, params string[] keys) =>
            new Entry { keys = keys, group = group, role = role, bird = bird, exotic = exotic };

        // (b) Other animals: candidates for NEW species or stand-ins, each with a suggested reserve role.
        private static readonly Entry[] OtherAnimals =
        {
            E("waterfowl", "Cattail Pond waterfowl (new species)", true, false, "duck", "mallard", "goose", "swan"),
            E("reptile", "Cattail Pond: turtle basking on a log (new species)", false, false, "turtle", "tortoise"),
            E("woodpecker", "Old-growth grove: woodpecker on dead trunks (new species; never a stand-in)", true, false, "woodpecker"),
            E("corvid", "Ridge and meadow corvid (new species, or stand-in for the blackbird)", true, false, "crow", "raven"),
            E("songbird", "Meadow-edge songbird (stand-in for cardinal / jay / blackbird, or a new species)", true, false, "robin", "sparrow", "finch"),
            E("dove", "Meadow edge: ground-feeding dove (new species)", true, false, "dove", "pigeon"),
            E("hummingbird", "Meadow flowers: hummingbird (new species or macro-scale subject)", true, false, "hummingbird"),
            E("bat", "Dusk sky over Cattail Pond (new species, dusk rhythm)", true, false, "bat"),
            E("bear", "A bear that is not the black bear (polar, panda, toy…): not a reserve species", false, false, "bear"),
            E("beaver", "Willow Creek and the pond: lodge builder (new species; groundhog stand-in)", false, false, "beaver"),
            E("otter", "Willow Creek and the falls pool (new species)", false, false, "otter"),
            E("woodland mammal", "Woodland edge / creek bank mammal (new species)", false, false, "skunk", "badger", "porcupine", "mink", "weasel"),
            E("cat", "Ridge: rare predator (new species)", false, false, "bobcat", "lynx", "cougar"),
            E("large herbivore", "Pond shallows / grove edge herbivore (new species, or deer stand-in)", false, false, "moose", "elk"),
            E("boar", "Grove forager (new species)", false, false, "boar"),
            E("small mammal", "Forest floor and meadow small mammal (new species, or opossum / squirrel stand-in)", false, false, "mouse", "rat", "vole", "chipmunk"),
            E("reptile", "Sunny rocks and logs reptile (new species)", false, false, "snake", "lizard"),
            E("fish", "Willow Creek pools: ambient creek fish (new)", false, false, "fish", "trout", "salmon", "goldfish", "catfish", "minnow", "carp"),
            E("crustacean", "Creek shallows (macro-scale subject)", false, false, "crayfish", "crab"),
            E("hedgehog", "Not native to North America (stand-in only)", false, false, "hedgehog"),
            E("mole", "Meadow soil: rarely seen (new species)", false, false, "mole"),
            E("pet cat", "Domestic cat, not a reserve species (the bobcat would be the reserve's cat)", false, false, "cat", "kitty", "kitties", "kitten"),
            E("domestic", "Domestic animal, not a reserve species (possible stand-in, e.g. dog for the fox)", false, false, "horse", "cow", "sheep", "goat", "pig", "dog"),
            E("exotic", "not a Willow Creek animal", false, true, "lion", "tiger", "elephant", "zebra", "giraffe", "monkey", "penguin", "pinguin", "kangaroo", "crocodile", "shark", "dinosaur", "dragon="),
        };

        // (c) Macro subjects. Fungi first, so "Fly Agaric" is a mushroom, not a fly.
        private static readonly Entry[] MacroSubjects =
        {
            E("Fungi", "fungus", false, false, "mushroom", "shroom", "fungus", "fungi", "toadstool", "agaric", "bolete", "chanterelle", "puffball"),
            E("Plants", "wildflower", false, false, "flower", "wildflower", "blossom", "daisy", "dandelion", "clover", "lupine", "aster=", "buttercup", "poppy",
                "violet", "trillium", "bluebell", "thistle", "goldenrod", "milkweed", "cattail", "lily", "lilies", "orchid", "tulip", "sunflower"),
            E("Animals", "insect or small animal", false, false, "butterfly", "moth", "dragonfly", "damselfly", "bumblebee", "honeybee", "bee", "wasp", "hornet", "beetle",
                "ladybug", "ladybird", "mantis", "grasshopper", "cricket", "caterpillar", "ant", "spider", "firefly", "cicada", "fly=", "frog", "toad", "tadpole",
                "salamander", "newt", "snail", "slug", "worm", "earthworm"),
        };

        // Compound macro names, checked on the squashed name BEFORE the animal census so "Dragon_Fly" is an insect, not a dragon.
        private static readonly string[] MacroCompounds =
            { "dragonfly", "butterfly", "damselfly", "firefly", "ladybug", "ladybird", "bumblebee", "lightningbug" };

        // A STATIC model with one of these words and an animal word is a thing, not a creature ("Cricket_Bat", "Bee_Hive", "Bat_Box").
        private static readonly string[] StaticThingWords = { "bat", "cricket", "pot", "net", "house", "hive", "lamp", "box" };

        // Folders whose names say "animals live here": models under them are always opened, whatever they are called.
        private static readonly string[] AnimalContainerWords =
            { "animal", "animals", "creature", "creatures", "wildlife", "bird", "birds", "insect", "insects", "character", "characters", "fauna", "pet", "pets", "critter", "critters" };

        // A model with no mesh and one of these words is a rig, a spawner or a demo scene, not an animal ("_livingBirdsController").
        private static readonly string[] NoiseWords = { "controller", "manager", "spawner", "system", "demo" };

        // (d) Vehicles: strong words only. "bike" and "trail" alone are weak (see IsVehicle).
        private static readonly string[] StrongVehicle =
        {
            "motorcycle", "motorbike", "motocross", "moto", "dirtbike", "trailbike", "scooter", "moped", "enduro", "atv", "quad", "utv", "buggy",
            "jeep", "truck", "pickup", "car", "bicycle",
        };
        private static readonly string[] BikeQualifiers = { "trail", "dirt", "motor", "moto", "enduro", "offroad", "cross", "mountain", "mtb" };
        private static readonly string[] BikePivots = { "Lean", "Steer", "Front Wheel", "Rear Wheel", "Front Axle", "Swingarm", "Kickstand", "Seat Anchor" };

        // (e) Scenery noticed: counts only.
        private static readonly string[] TreeWords = { "tree", "pine", "oak", "birch", "maple", "spruce", "fir", "cedar", "willow", "aspen", "beech", "elm", "palm", "sapling", "trunk" };
        private static readonly string[] RockWords = { "rock", "boulder", "stone", "cliff", "pebble", "cobble", "crag" };
        private static readonly string[] PlantWords = { "grass", "bush", "bushes", "shrub", "fern", "plant=", "vine", "moss", "reed", "hedge", "weed", "foliage", "ivy", "leaf", "branch", "log", "stump", "bamboo", "cactus" };
        private static readonly string[] PropWords =
            { "fence", "bench", "sign", "house", "cabin", "barn", "tent", "bridge", "lamp", "crate", "barrel", "wall", "door", "window", "chair", "table", "bucket", "cart", "shed", "tower", "ladder", "campfire", "feeder", "birdhouse", "nest" };

        // Whole-word-only family of generic container folders when deciding what a "pack" is.
        private static readonly HashSet<string> GenericFolders = new HashSet<string>
            { "plugins", "thirdparty", "third party", "3rd party", "packs", "asset packs", "assetpacks", "imported", "models", "art" };

        private class Info
        {
            public string path, name, pack, folder;
            public bool skinned, hasRenderers, animated, isBird, isAtFile;
            public long tris;
            public Vector3 size;
            public bool sizeOdd;
            public int materials;
            public List<string> shaders = new List<string>();
            public List<string> pipelineIssues = new List<string>();
            public string rig = "none";
            public List<KeyValuePair<string, string>> clips = new List<KeyValuePair<string, string>>(); // name → role or ""
            public List<ClipRole> roles = new List<ClipRole>();
            public Cat cat = Cat.OtherStatic;
            public List<(string id, MatchKind kind, int rank)> species = new List<(string, MatchKind, int)>();
            public Entry census; public string censusWord;
            public Entry macro; public string macroWord;
            public string sceneryKind = "";
            // vehicles
            public int wheelNodes; public bool steerNode; public float length; public List<string> pivots = new List<string>();
            public bool owned, unopened;
            public bool byFolder;                          // macro or vehicle recognised only by a folder above the model
            public bool hidden, animFile;                  // left out of the animal tables (rigs, demos); an animation-only file
            public List<string> nodeNames = new List<string>(); // vehicles: up to 12 child node names
            public int ClipCount => clips.Count;
        }

        [MenuItem("WildLens/Asset Audit Report", priority = 12)]
        public static void Menu() => Run();

        /// <summary>
        /// Scans every model and prefab under Assets/, writes WildLensAssetAudit.md beside the Assets folder, copies it to
        /// the clipboard and returns the Markdown. Read-only: nothing in the project is changed.
        /// </summary>
        public static string Run()
        {
            string text;
            int scanned = 0;
            try
            {
                text = Build(out scanned, out string oneLine);
                string file = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportFileName);
                File.WriteAllText(file, text, new UTF8Encoding(false));
                bool full = text.Length <= ClipboardLimit;
                GUIUtility.systemCopyBuffer = full ? text : _shortText;
                Debug.Log($"[WildLens] Asset audit: {oneLine}. Saved {file}" +
                          (full ? " and copied the full report to the clipboard."
                                : " (the full report is too long for the clipboard: a SHORT version was copied: summary, animals, species coverage, macro, vehicles and unrecognised animated models; open the file for the per-clip detail and the rest)."));
                if (!Application.isBatchMode &&
                    EditorUtility.DisplayDialog("WildLens Asset Audit",
                        full ? "Saved WildLensAssetAudit.md in your project folder and copied the FULL report to the clipboard. Paste it into the chat with Claude."
                             : "Saved WildLensAssetAudit.md in your project folder. The full report is too long for the clipboard, so a SHORT version was copied (summary, animals with their clip names, species coverage, macro subjects, vehicles, unrecognised animated models). Paste it into the chat with Claude; the file has everything.",
                        "Show file", "Close"))
                    EditorUtility.RevealInFinder(file);
                return text;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[WildLens] Asset audit failed: " + e);
                return "Asset audit failed: " + e.Message;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static string _shortText = "";

        // ---------------------------------------------------------------------------------------------------------

        private static string Build(out int scanned, out string oneLine)
        {
            bool hasRp = GraphicsSettings.currentRenderPipeline != null;
            string rpName = hasRp ? GraphicsSettings.currentRenderPipeline.GetType().Name : "Built-in Render Pipeline";
            bool hdrp = hasRp && rpName.IndexOf("HD", System.StringComparison.Ordinal) >= 0;

            // Species list: the asset if built, else the defaults (never changes the project).
            var db = AssetDatabase.LoadAssetAtPath<SpeciesDatabase>(WildLensSliceBuilder.DatabasePath);
            List<SpeciesDefinition> speciesList;
            var temp = new List<SpeciesDefinition>();
            bool fromAsset = db != null && db.species.Count > 0;
            if (fromAsset) speciesList = db.species;
            else { temp = SpeciesDefaults.CreateAll(); speciesList = temp; }
            var speciesById = new Dictionary<string, SpeciesDefinition>();
            foreach (var sp in speciesList) if (sp != null && !speciesById.ContainsKey(sp.speciesId)) speciesById[sp.speciesId] = sp;

            var infos = new List<Info>();
            int atFiles = 0, skippedFolders = 0, failed = 0, notOpened = 0, loaded = 0;
            var dirCache = new Dictionary<string, List<string>>();
            var seen = new HashSet<string>();
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" });
            int total = guids.Length, index = 0;
            try
            {
                foreach (string guid in guids)
                {
                    index++;
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;
                    if ((path.StartsWith(GeneratedAssets.Root + "/") && !path.StartsWith(ArtLibrary.AnimalsFolder + "/")) ||
                        path.Contains("/Editor/") || path.Contains("/Gizmos/")) { skippedFolders++; continue; }
                    if (path.Contains("@")) { atFiles++; continue; } // animation-only file: counted as a clip source for its model
                    if (index % 5 == 0) EditorUtility.DisplayProgressBar("WildLens Asset Audit", path, (float)index / Mathf.Max(1, total));
                    if (!ShouldOpen(path))
                    {
                        // Named like scenery or a prop (or nothing recognisable) and not under an animal-ish folder: counted, not opened.
                        infos.Add(Unopened(path));
                        notOpened++;
                        continue;
                    }
                    try
                    {
                        loaded++;
                        var info = Analyse(path, dirCache, hasRp, hdrp, speciesById);
                        if (info != null) infos.Add(info);
                    }
                    catch (System.Exception e)
                    {
                        failed++;
                        Debug.LogWarning($"[WildLens] Asset audit skipped {path}: {e.Message}");
                    }
                    if (loaded % 200 == 0) EditorUtility.UnloadUnusedAssetsImmediate();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            scanned = infos.Count;

            // Which models the species prefabs were built from (best effort).
            var usedBy = new Dictionary<string, List<string>>();
            var builtFrom = new Dictionary<string, string>();
            foreach (var sp in speciesList)
            {
                if (sp == null || sp.prefab == null) continue;
                try
                {
                    // Shared with the automatic refresh of old prefabs (1.8): linked assets first, then the model files the meshes come from.
                    foreach (string sp2 in AnimalPrefabFactory.SourceModelPaths(sp.prefab))
                    {
                        if (!usedBy.TryGetValue(sp2, out var l)) usedBy[sp2] = l = new List<string>();
                        if (!l.Contains(sp.speciesId)) l.Add(sp.speciesId);
                        if (!builtFrom.ContainsKey(sp.speciesId)) builtFrom[sp.speciesId] = sp2;
                    }
                }
                catch (System.Exception) { /* best effort */ }
            }

            // A model that is the real thing for one species is a poorer stand-in for another.
            foreach (var i in infos) i.owned = i.species.Exists(s => s.kind == MatchKind.Exact);

            string report = Compose(infos, speciesList, speciesById, fromAsset, usedBy, builtFrom, rpName, hasRp, atFiles, skippedFolders, failed, notOpened, out oneLine);
            foreach (var t in temp) if (t != null) Object.DestroyImmediate(t);
            return report;
        }

        // --- per asset -------------------------------------------------------------------------------------------

        private static List<string> _allSpeciesKeys;

        /// <summary>
        /// Whether an asset must be opened to be classified. Decided from the path alone: anything under an animal-ish
        /// folder, or with a species, other-animal, macro or vehicle word anywhere in its path, is opened; the rest
        /// (scenery, props, nothing recognisable) is only counted.
        /// </summary>
        private static bool ShouldOpen(string path)
        {
            var pathWords = AnimalPrefabFactory.Tokens(path);
            if (Has(pathWords, AnimalContainerWords)) return true;
            if (_allSpeciesKeys == null)
            {
                _allSpeciesKeys = new List<string>();
                foreach (var kv in Names) { _allSpeciesKeys.AddRange(kv.Value.exact); _allSpeciesKeys.AddRange(kv.Value.standIn); }
            }
            if (Has(pathWords, _allSpeciesKeys.ToArray())) return true;
            foreach (var e in OtherAnimals) if (Has(pathWords, e.keys)) return true;
            foreach (var e in MacroSubjects) if (Has(pathWords, e.keys)) return true;
            if (Has(pathWords, StrongVehicle) || Has(pathWords, new[] { "bike" })) return true;
            string sq = Squash(Path.GetFileNameWithoutExtension(path));
            foreach (var c in MacroCompounds) if (sq.Contains(c)) return true;
            return false;
        }

        /// <summary>An asset counted from its name alone: scenery when its name or folder says so, else other static.</summary>
        private static Info Unopened(string path)
        {
            var info = new Info { path = path, name = Path.GetFileNameWithoutExtension(path), pack = PackOf(path), unopened = true };
            info.folder = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
            string scenery = SceneryKind(Words(info.name));
            if (scenery == "") scenery = SceneryKind(Words(info.folder));
            if (scenery != "") { info.cat = Cat.Scenery; info.sceneryKind = scenery; }
            else info.cat = Cat.OtherStatic;
            return info;
        }

        private static Info Analyse(string path, Dictionary<string, List<string>> dirCache, bool hasRp, bool hdrp, Dictionary<string, SpeciesDefinition> speciesById)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return null;
            var info = new Info { path = path, name = Path.GetFileNameWithoutExtension(path), pack = PackOf(path) };
            info.folder = Path.GetFileName(Path.GetDirectoryName(path) ?? "");

            // Geometry, materials, shaders.
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var mats = new HashSet<Material>();
            var shaderNames = new SortedSet<string>();
            bool anyBounds = false; Bounds bounds = new Bounds();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) { mesh = smr.sharedMesh; info.skinned = true; }
                else if (r is MeshRenderer) { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                if (mesh != null)
                {
                    info.hasRenderers = true;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                        if (mesh.GetTopology(s) == MeshTopology.Triangles) info.tris += mesh.GetIndexCount(s) / 3;
                    try
                    {
                        var b = r.bounds;
                        if (b.size.sqrMagnitude < 1e-12f) b = MeshBoundsInWorld(mesh, r.transform);
                        if (!anyBounds) { bounds = b; anyBounds = true; } else bounds.Encapsulate(b);
                    }
                    catch (System.Exception) { }
                }
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { shaderNames.Add("(no material)"); continue; }
                    mats.Add(m);
                    string sn = m.shader != null ? m.shader.name : "";
                    shaderNames.Add(string.IsNullOrEmpty(sn) ? "(missing shader)" : sn);
                }
            }
            if (anyBounds) info.size = bounds.size;
            float maxSide = Mathf.Max(info.size.x, Mathf.Max(info.size.y, info.size.z));
            info.sizeOdd = anyBounds && (maxSide > 60f || maxSide < 0.02f);
            info.materials = mats.Count;
            info.shaders.AddRange(shaderNames);
            var issues = new SortedSet<string>();
            foreach (var sn in shaderNames) { string issue = AnimalPrefabFactory.PipelineIssue(sn, hasRp, hdrp); if (issue != null) issues.Add(issue); }
            // 1.9.1: under URP, also what the shader really does (a pack's own surface shader has only built-in passes, whatever its name).
            if (hasRp && !hdrp && AnimalPrefabFactory.UrpActive())
            {
                if (shaderNames.Contains("(no material)")) issues.Add("empty material slot (drawn magenta)");
                foreach (var m in mats)
                {
                    string issue = AnimalPrefabFactory.UrpDrawIssue(m);
                    if (issue != null && !issue.StartsWith("built-in", System.StringComparison.Ordinal) && issue != "missing shader")
                        issues.Add((m.shader != null ? m.shader.name : "?") + ": " + issue);
                }
            }
            info.pipelineIssues.AddRange(issues);

            // Rig and clips.
            info.rig = RigOf(path, go);
            var clips = CollectClips(path, go, dirCache);
            var roleSet = new HashSet<ClipRole>();
            foreach (var c in clips)
            {
                if (c == null) continue;
                string role = "";
                if (AnimalPrefabFactory.TryClassifyName(c.name, out var cr)) { role = cr.ToString(); roleSet.Add(cr); }
                info.clips.Add(new KeyValuePair<string, string>(c.name, role));
            }
            info.roles.AddRange(roleSet);
            info.roles.Sort();
            info.animated = info.skinned && info.clips.Count > 0;

            Categorise(info, go, speciesById);
            return info;
        }

        private static void Categorise(Info info, GameObject go, Dictionary<string, SpeciesDefinition> speciesById)
        {
            var nameWords = Words(info.name);
            var folderWords = Words(info.folder);
            var ancestorWords = AncestorWords(info.path); // every folder above the model: macro subjects and vehicles may be named by any of them

            // Rigs, spawners and demo scenes beside a pack's animals, and animation-only files (no mesh in a folder of animations).
            if (!info.hasRenderers && Has(nameWords, NoiseWords)) { info.cat = Cat.OtherStatic; info.hidden = true; return; }
            if (!info.hasRenderers && FolderSaysAnimations(info.folder)) { info.cat = Cat.OtherStatic; info.hidden = true; info.animFile = true; return; }

            // A compound macro name ("DragonFly", "Dragon_Fly", "Lightning Bug") is an insect before anything else: the species
            // and census checks would read the "dragon" in it.
            string compound = null;
            string squashedName = Squash(info.name);
            foreach (var c in MacroCompounds) if (squashedName.Contains(c)) { compound = c; break; }

            if (compound != null)
            {
                foreach (var e in MacroSubjects) if (e.group == "Animals") { info.macro = e; info.macroWord = compound; break; }
            }
            else
            {
                // (a) reserve species
                foreach (var id in Names.Keys)
                {
                    var kind = MatchAsset(info.name, info.folder, id, out int rank);
                    if (kind != MatchKind.None) info.species.Add((id, kind, rank));
                }
                info.species.Sort((a, b) => a.kind != b.kind ? b.kind.CompareTo(a.kind) : a.rank != b.rank ? a.rank.CompareTo(b.rank) : string.CompareOrdinal(a.id, b.id));

                // (b) other animals, (c) macro subjects: name words first, then the folder's.
                info.census = FindEntry(OtherAnimals, nameWords, folderWords, out info.censusWord, out _);
                info.macro = FindEntry(MacroSubjects, nameWords, ancestorWords, out info.macroWord, out info.byFolder);
            }

            // (e) scenery words in the model's own name.
            string scenery = SceneryKind(nameWords);

            // Static, scenery-named things that also carry an animal word ("Butterfly Bush"): note only.
            bool animalWord = info.species.Count > 0 || info.census != null || info.macro != null;
            // A static thing with an animal word and a thing word ("Cricket_Bat", "Bee_Hive", "Bat_Box", "Flower_Pot") is neither.
            if (animalWord && !info.skinned && info.clips.Count == 0 && Has(nameWords, StaticThingWords))
            {
                info.cat = Cat.OtherStatic;
                info.species.Clear(); info.census = null; info.macro = null;
                return;
            }
            if (animalWord && !info.animated && scenery != "")
            {
                info.cat = Cat.Scenery; info.sceneryKind = scenery;
                info.species.Clear(); info.census = null; info.macro = null;
                return;
            }

            if (info.species.Count > 0)
            {
                info.cat = Cat.Species;
                var def = speciesById.TryGetValue(info.species[0].id, out var d) ? d : null;
                info.isBird = def != null && def.locomotion == LocomotionType.Flying;
                if (info.census != null && info.census.bird && def == null) info.isBird = true;
                return;
            }
            if (info.census != null) { info.cat = Cat.OtherAnimal; info.isBird = info.census.bird; return; }
            if (info.macro != null) { info.cat = Cat.Macro; return; }
            if (IsVehicle(info, go, nameWords, ancestorWords)) { info.cat = Cat.Vehicle; return; }
            if (scenery == "") scenery = SceneryKind(folderWords);
            if (scenery != "" && !info.animated) { info.cat = Cat.Scenery; info.sceneryKind = scenery; return; }
            info.cat = info.animated ? Cat.UncategorisedAnimated : Cat.OtherStatic;
        }

        private static Entry FindEntry(Entry[] table, List<string> nameWords, List<string> folderWords, out string word, out bool byFolder)
        {
            byFolder = false;
            foreach (var words in new[] { nameWords, folderWords })
            {
                foreach (var e in table)
                {
                    int i = HasIndex(words, e.keys);
                    if (i >= 0) { word = e.keys[i].TrimEnd('='); return e; }
                }
                byFolder = true; // the name said nothing: whatever the folders say is "by folder"
            }
            byFolder = false;
            word = null;
            return null;
        }

        /// <summary>The words of every folder above the asset (below Assets), each with its squashed whole: "SinuousShrooms" gives sinuous, shrooms, sinuousshrooms.</summary>
        private static List<string> AncestorWords(string path)
        {
            var all = new List<string>();
            foreach (var folder in MacroPackSetup.Ancestors(path)) all.AddRange(Words(folder));
            return all;
        }

        private static bool FolderSaysAnimations(string folder)
        {
            foreach (var t in AnimalPrefabFactory.Tokens(folder)) if (t.StartsWith("anim", System.StringComparison.Ordinal)) return true;
            return false;
        }

        private static string SceneryKind(List<string> words)
        {
            if (Has(words, TreeWords)) return "Trees";
            if (Has(words, RockWords)) return "Rocks";
            if (Has(words, PlantWords)) return "Plants and grass";
            if (Has(words, PropWords)) return "Props";
            return "";
        }

        private static bool IsVehicle(Info info, GameObject go, List<string> nameWords, List<string> folderWords)
        {
            bool byName = Has(nameWords, StrongVehicle);
            bool strong = byName || Has(folderWords, StrongVehicle); // any folder above the model ("MotorcyclePack_/…") can name it
            bool bike = Has(nameWords, new[] { "bike" });
            Transform[] nodes = null;
            if (!strong && bike)
            {
                // "Bike" alone is weak: it needs a qualifier ("DirtBike", "Trail_Bike") or real wheel nodes.
                strong = Has(nameWords, BikeQualifiers);
                byName = strong;
                if (!strong) { nodes = go.GetComponentsInChildren<Transform>(true); strong = CountWheels(nodes) >= 2; byName = strong; }
            }
            if (!strong) return false;
            info.byFolder = !byName;
            if (nodes == null) nodes = go.GetComponentsInChildren<Transform>(true);
            info.wheelNodes = CountWheels(nodes);
            var norm = new HashSet<string>();
            var seenNames = new HashSet<string>();
            foreach (var t in nodes)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("handlebar") || n.Contains("handle") || n.Contains("fork") || n.Contains("steer")) info.steerNode = true;
                norm.Add(Squash(t.name));
                if (t != go.transform && info.nodeNames.Count < 12 && seenNames.Add(t.name)) info.nodeNames.Add(t.name);
            }
            foreach (var p in BikePivots) if (norm.Contains(Squash(p))) info.pivots.Add(p);
            info.length = Mathf.Max(info.size.x, info.size.z);
            return true;
        }

        private static int CountWheels(Transform[] nodes)
        {
            int n = 0;
            foreach (var t in nodes)
            {
                string s = t.name.ToLowerInvariant();
                if (s.Contains("wheel") || s.Contains("tire") || s.Contains("tyre")) n++;
            }
            return n;
        }

        private static string Squash(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        private static string PackOf(string path)
        {
            var parts = path.Split('/');
            if (parts.Length <= 2) return "(Assets root)";
            string first = parts[1];
            if (GenericFolders.Contains(first.ToLowerInvariant()) && parts.Length > 3) return first + "/" + parts[2];
            return first;
        }

        private static string RigOf(string path, GameObject go)
        {
            var parts = new List<string>();
            try
            {
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp != null)
                    switch (imp.animationType)
                    {
                        case ModelImporterAnimationType.Human: parts.Add("Humanoid"); break;
                        case ModelImporterAnimationType.Generic: parts.Add("Generic"); break;
                        case ModelImporterAnimationType.Legacy: parts.Add("Legacy"); break;
                        default: parts.Add("none"); break;
                    }
                else
                {
                    var animator = go.GetComponentInChildren<Animator>(true);
                    if (animator != null) parts.Add(animator.avatar != null && animator.avatar.isHuman ? "Humanoid" : "Animator");
                }
                if (go.GetComponentInChildren<Animation>(true) != null) parts.Add("Animation component (legacy)");
            }
            catch (System.Exception) { }
            return parts.Count == 0 ? "none" : string.Join(" + ", parts);
        }

        /// <summary>Triangle count and size of a model that was only counted by name, for the "Other static models" list.</summary>
        private static void QuickGeometry(Info info)
        {
            try
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(info.path);
                if (go == null) return;
                bool any = false; Bounds bounds = new Bounds();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : (r is MeshRenderer ? r.GetComponent<MeshFilter>()?.sharedMesh : null);
                    if (mesh == null) continue;
                    info.hasRenderers = true;
                    for (int k = 0; k < mesh.subMeshCount; k++)
                        if (mesh.GetTopology(k) == MeshTopology.Triangles) info.tris += mesh.GetIndexCount(k) / 3;
                    var b = r.bounds;
                    if (b.size.sqrMagnitude < 1e-12f) b = MeshBoundsInWorld(mesh, r.transform);
                    if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
                }
                if (any)
                {
                    info.size = bounds.size;
                    float maxSide = Mathf.Max(info.size.x, Mathf.Max(info.size.y, info.size.z));
                    info.sizeOdd = maxSide > 60f || maxSide < 0.02f;
                }
            }
            catch (System.Exception) { /* a model that will not load stays "-" */ }
        }

        private static Bounds MeshBoundsInWorld(Mesh mesh, Transform t)
        {
            var b = mesh.bounds;
            var m = t.localToWorldMatrix;
            var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + new Vector3((i & 1) == 0 ? -b.extents.x : b.extents.x, (i & 2) == 0 ? -b.extents.y : b.extents.y, (i & 4) == 0 ? -b.extents.z : b.extents.z);
                result.Encapsulate(m.MultiplyPoint3x4(corner));
            }
            return result;
        }

        // --- report ----------------------------------------------------------------------------------------------

        private static string Esc(string s) => (s ?? "").Replace("|", "\\|").Replace("\n", " ");
        private static string Num(float f) => f.ToString("0.00", CultureInfo.InvariantCulture);
        private static string Size(Info i) => !i.hasRenderers ? "-" : $"{Num(i.size.x)}\u00d7{Num(i.size.y)}\u00d7{Num(i.size.z)}" + (i.sizeOdd ? " (?)" : "");
        private static string PipelineCell(Info i) => i.pipelineIssues.Count == 0 ? "yes" : "NO: " + string.Join(", ", i.pipelineIssues);
        private static string Yes(bool b) => b ? "yes" : "no";

        private static string SpeciesName(string id, Dictionary<string, SpeciesDefinition> by) =>
            by.TryGetValue(id, out var d) && d != null && !string.IsNullOrEmpty(d.displayName) ? d.displayName : id;

        private static string Represents(Info i)
        {
            if (i.cat == Cat.Species)
            {
                var parts = new List<string>();
                foreach (var s in i.species) { if (parts.Count >= 4) break; parts.Add($"{s.id} ({(s.kind == MatchKind.Exact ? "Exact" : "Stand-in")})"); }
                string r = string.Join("; ", parts);
                if (i.species.Count > 4) r += $" +{i.species.Count - 4} more";
                if (i.census != null && !i.species.Exists(s => s.kind == MatchKind.Exact)) r += $"; also: {i.censusWord}";
                return r;
            }
            return $"new: {i.censusWord}" + (i.census.exotic ? " (not a Willow Creek animal)" : "");
        }

        private static string[] Needed(Info i) => i.isBird
            ? new[] { "Idle", "Fly", "Glide", "TakeOff", "Land" }
            : new[] { "Idle", "Walk", "Run", "Eat", "Look" };

        private static string Missing(Info i)
        {
            var have = new HashSet<string>();
            foreach (var r in i.roles) have.Add(r.ToString());
            var miss = new List<string>();
            foreach (var n in Needed(i)) if (!have.Contains(n)) miss.Add(n);
            return miss.Count == 0 ? "none" : string.Join(", ", miss);
        }

        /// <summary>Up to 12 clips as <c>name→Role</c> (a plain name when unrecognised), then "+N more": the short version's per-animal clip summary.</summary>
        private static string ClipNames(Info i)
        {
            if (i.clips.Count == 0) return "-";
            var parts = new List<string>();
            for (int k = 0; k < i.clips.Count && k < 12; k++)
                parts.Add(i.clips[k].Value == "" ? i.clips[k].Key : i.clips[k].Key + "\u2192" + i.clips[k].Value);
            string r = string.Join(", ", parts);
            if (i.clips.Count > 12) r += $", +{i.clips.Count - 12} more";
            return r;
        }

        private static string RolesCell(Info i)
        {
            if (i.roles.Count == 0) return "-";
            var s = new List<string>();
            foreach (var r in i.roles) s.Add(r.ToString());
            return string.Join(", ", s);
        }

        private static string Compose(List<Info> infos, List<SpeciesDefinition> speciesList, Dictionary<string, SpeciesDefinition> speciesById, bool fromAsset,
            Dictionary<string, List<string>> usedBy, Dictionary<string, string> builtFrom, string rpName, bool hasRp, int atFiles, int skippedFolders, int failed, int notOpened, out string oneLine)
        {
            var species = infos.FindAll(i => i.cat == Cat.Species);
            var others = infos.FindAll(i => i.cat == Cat.OtherAnimal);
            var macros = infos.FindAll(i => i.cat == Cat.Macro);
            var vehicles = infos.FindAll(i => i.cat == Cat.Vehicle);
            var scenery = infos.FindAll(i => i.cat == Cat.Scenery);
            var uncat = infos.FindAll(i => i.cat == Cat.UncategorisedAnimated);
            var otherStaticList = infos.FindAll(i => i.cat == Cat.OtherStatic && !i.hidden);
            int otherStatic = otherStaticList.Count;
            var animFiles = infos.FindAll(i => i.animFile);
            int hiddenOther = infos.FindAll(i => i.hidden && !i.animFile).Count;
            var animals = new List<Info>(species); animals.AddRange(others);
            animals.Sort((a, b) =>
            {
                bool ae = a.species.Exists(s => s.kind == MatchKind.Exact), be = b.species.Exists(s => s.kind == MatchKind.Exact);
                if (ae != be) return ae ? -1 : 1;
                if (a.cat != b.cat) return a.cat.CompareTo(b.cat);
                int c = string.CompareOrdinal(a.pack, b.pack);
                return c != 0 ? c : string.CompareOrdinal(a.path, b.path);
            });

            oneLine = $"{infos.Count} models/prefabs scanned; {species.Count} match reserve species, {others.Count} other animals, {macros.Count} macro, {vehicles.Count} vehicles, {uncat.Count} unclassified animated";

            // ---- Summary
            var sum = new StringBuilder();
            sum.Append("# WildLens Asset Audit\n\n");
            sum.Append("## Summary\n\n");
            sum.Append($"- Unity {Application.unityVersion}; WildLens {WildLensVersion.Package}; render pipeline: {rpName}\n");
            sum.Append($"- Models and prefabs scanned: {infos.Count} (animation-only `@` files counted as clip sources: {atFiles}; skipped as WildLens/Editor/Gizmos: {skippedFolders}; failed: {failed})\n");
            sum.Append($"- {notOpened} other models not opened (scenery/props by name, outside animal folders); counted from their names only\n");
            sum.Append($"- Species table from: {(fromAsset ? "the SpeciesDatabase asset" : "built-in defaults (no database asset yet)")}\n\n");
            sum.Append("| Category | Assets |\n|---|---|\n");
            sum.Append($"| (a) Reserve species (exact or stand-in) | {species.Count} |\n");
            sum.Append($"| (b) Other animals (new species or stand-ins) | {others.Count} |\n");
            int mA = macros.FindAll(i => i.macro.group == "Animals").Count, mP = macros.FindAll(i => i.macro.group == "Plants").Count, mF = macros.FindAll(i => i.macro.group == "Fungi").Count;
            sum.Append($"| (c) Macro subjects (Animals {mA} / Plants {mP} / Fungi {mF}) | {macros.Count} |\n");
            sum.Append($"| (d) Vehicles | {vehicles.Count} |\n");
            sum.Append($"| (e) Scenery noticed (counts only) | {scenery.Count} |\n");
            sum.Append($"| (f) Animated models nothing recognised | {uncat.Count} |\n");
            sum.Append($"| Other static models (listed below) | {otherStatic} |\n");
            if (animFiles.Count > 0 || hiddenOther > 0)
                sum.Append($"| Animation-only files and rigs/demos left out of the animal tables | {animFiles.Count + hiddenOther} |\n");
            sum.Append('\n');

            var packs = new SortedDictionary<string, List<Info>>(System.StringComparer.Ordinal);
            foreach (var i in infos) { if (!packs.TryGetValue(i.pack, out var l)) packs[i.pack] = l = new List<Info>(); l.Add(i); }
            sum.Append("| Pack | Animated | Static | Categories found |\n|---|---|---|---|\n");
            int packRows = 0;
            foreach (var kv in packs)
            {
                if (++packRows > RowCap) { sum.Append($"| +{packs.Count - RowCap} more packs | | | |\n"); break; }
                int anim = kv.Value.FindAll(i => i.animated).Count;
                var cats = new SortedSet<string>();
                foreach (var i in kv.Value) if (i.cat != Cat.OtherStatic) cats.Add(i.cat == Cat.UncategorisedAnimated ? "unclassified animated" : i.cat == Cat.OtherAnimal ? "other animals" : i.cat.ToString().ToLowerInvariant());
                sum.Append($"| {Esc(kv.Key)} | {anim} | {kv.Value.Count - anim} | {(cats.Count == 0 ? "-" : string.Join(", ", cats))} |\n");
            }
            sum.Append('\n');

            // ---- Master table of animals
            var master = new StringBuilder();
            master.Append("## Animals: master table\n\n");
            if (animals.Count == 0) master.Append("No animal models were recognised by name.\n\n");
            else
            {
                master.Append("| Asset | Pack | Represents | Tris | Size (m) | Skinned | Clips | Clip names | Roles recognised | Roles missing | Pipeline OK | Used by species |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
                int n = 0;
                foreach (var i in animals)
                {
                    if (++n > RowCap) { master.Append($"\n+{animals.Count - RowCap} more\n"); break; }
                    string used = usedBy.TryGetValue(i.path, out var u) ? string.Join(", ", u) : "-";
                    master.Append($"| {Esc(i.path)} | {Esc(i.pack)} | {Esc(Represents(i))} | {i.tris} | {Size(i)} | {Yes(i.skinned)} | {i.ClipCount} | {Esc(ClipNames(i))} | {RolesCell(i)} | {(i.animated ? Missing(i) : "n/a (no clips)")} | {Esc(PipelineCell(i))} | {Esc(used)} |\n");
                }
                master.Append("\nSuggested reserve role for the non-species animals:\n\n| Asset | Word | Suggested role | Flag |\n|---|---|---|---|\n");
                n = 0;
                foreach (var i in others)
                {
                    if (++n > RowCap) { master.Append($"\n+{others.Count - RowCap} more\n"); break; }
                    master.Append($"| {Esc(i.path)} | {i.censusWord} | {Esc(i.census.role)} | {(i.census.exotic ? "not a Willow Creek animal" : i.census.bird ? "bird (fly roles)" : "")} |\n");
                }
                master.Append('\n');
            }

            // Animation-only files (no mesh, in a folder of animations) are one line per folder, not one row each.
            if (animFiles.Count > 0)
            {
                var perFolder = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
                foreach (var f in animFiles) { string d = (Path.GetDirectoryName(f.path) ?? "").Replace('\\', '/'); perFolder.TryGetValue(d, out int c); perFolder[d] = c + 1; }
                master.Append("Animation-only files (no mesh), not listed one by one:\n\n");
                foreach (var kv in perFolder) master.Append($"- {kv.Value} animation files in `{Esc(kv.Key)}`\n");
                master.Append('\n');
            }

            // ---- Species coverage
            var cov = new StringBuilder();
            cov.Append("## Species coverage\n\n");
            cov.Append("| Species | Status | Prefab assigned | Best candidate | Other candidates |\n|---|---|---|---|---|\n");
            int real = 0, standIn = 0, placeholder = 0;
            foreach (var sp in speciesList)
            {
                if (sp == null) continue;
                string id = sp.speciesId;
                var cands = new List<Info>();
                foreach (var i in species) if (i.species.Exists(s => s.id == id)) cands.Add(i);
                cands.Sort((a, b) =>
                {
                    var ma = a.species.Find(s => s.id == id); var mb = b.species.Find(s => s.id == id);
                    if (a.animated != b.animated) return a.animated ? -1 : 1;
                    if (ma.kind != mb.kind) return mb.kind.CompareTo(ma.kind);
                    if (a.owned != b.owned && ma.kind == MatchKind.StandIn) return a.owned ? 1 : -1;
                    if (ma.rank != mb.rank) return ma.rank.CompareTo(mb.rank);
                    if (a.ClipCount != b.ClipCount) return b.ClipCount.CompareTo(a.ClipCount);
                    return string.CompareOrdinal(a.path, b.path);
                });
                string status = "placeholder: nothing found";
                if (sp.prefab != null)
                {
                    // Already set up: say whether what is in use is the real thing or a stand-in.
                    MatchKind used = MatchKind.None;
                    if (builtFrom.TryGetValue(id, out var src))
                        foreach (var c in cands) if (c.path == src) { used = c.species.Find(s => s.id == id).kind; break; }
                    if (used == MatchKind.None && cands.Count > 0) used = cands[0].species.Find(s => s.id == id).kind;
                    status = used == MatchKind.StandIn ? "stand-in in use" : "real model in use";
                }
                else if (cands.Count > 0)
                {
                    var top = cands[0]; var m = top.species.Find(s => s.id == id);
                    if (!top.animated) status = "placeholder: static model only";
                    else status = m.kind == MatchKind.Exact ? "real model available (not set up yet)" : "stand-in available";
                }
                if (status.StartsWith("real")) real++; else if (status.StartsWith("stand")) standIn++; else placeholder++;
                string assigned = sp.prefab != null ? "yes" + (builtFrom.TryGetValue(id, out var bf) ? $" (from {Esc(bf)})" : "") : "no";
                string best = "-", rest = "-";
                if (cands.Count > 0)
                {
                    best = Cand(cands[0], id);
                    var more = new List<string>();
                    for (int k = 1; k < cands.Count && k <= 5; k++) more.Add(Cand(cands[k], id));
                    if (cands.Count > 6) more.Add($"+{cands.Count - 6} more");
                    if (more.Count > 0) rest = string.Join("; ", more);
                }
                cov.Append($"| {Esc(SpeciesName(id, speciesById))} | {status} | {assigned} | {Esc(best)} | {Esc(rest)} |\n");
            }
            cov.Append($"\nReal models: {real}; stand-ins available: {standIn}; placeholders: {placeholder}.\n\n");

            // ---- Per-clip detail (the file only)
            var detailSb = new StringBuilder();
            var rest2 = detailSb;
            rest2.Append("## Per-clip detail\n\nAnimated animals and unclassified animated models: `clip name → role`.\n\n");
            var detail = new List<Info>(animals.FindAll(i => i.ClipCount > 0));
            detail.AddRange(uncat);
            int dn = 0;
            foreach (var i in detail)
            {
                if (++dn > RowCap) { rest2.Append($"\n+{detail.Count - RowCap} more assets\n"); break; }
                rest2.Append($"**{Esc(i.path)}** ({i.ClipCount} clips, rig: {i.rig})\n\n");
                var line = new List<string>();
                for (int k = 0; k < i.clips.Count && k < ClipCap; k++)
                    line.Add($"`{i.clips[k].Key}` → {(i.clips[k].Value == "" ? "unrecognised" : i.clips[k].Value)}");
                rest2.Append(string.Join("; ", line));
                if (i.clips.Count > ClipCap) rest2.Append($"; +{i.clips.Count - ClipCap} more");
                rest2.Append("\n\n");
            }

            // ---- Macro
            var macroSb = new StringBuilder();
            rest2 = macroSb;
            rest2.Append("## Macro subjects\n\n");
            if (macros.Count == 0) rest2.Append("None found by name.\n\n");
            else
            {
                rest2.Append("| Group | Asset | Pack | Word | Tris | Size (m) | Skinned | Clips | Pipeline OK |\n|---|---|---|---|---|---|---|---|---|\n");
                macros.Sort((a, b) => { int c = string.CompareOrdinal(a.macro.group, b.macro.group); return c != 0 ? c : string.CompareOrdinal(a.path, b.path); });
                int n = 0;
                foreach (var i in macros)
                {
                    if (++n > RowCap) { rest2.Append($"\n+{macros.Count - RowCap} more\n"); break; }
                    rest2.Append($"| {i.macro.group} | {Esc(i.path)}{(i.byFolder ? " (by folder)" : "")} | {Esc(i.pack)} | {i.macroWord}{(i.byFolder ? " (by folder)" : "")} | {i.tris} | {Size(i)} | {Yes(i.skinned)} | {i.ClipCount} | {Esc(PipelineCell(i))} |\n");
                }
                rest2.Append('\n');
            }

            // ---- Vehicles
            var vehSb = new StringBuilder();
            rest2 = vehSb;
            rest2.Append("## Vehicles\n\n");
            if (vehicles.Count == 0) rest2.Append("None found by name.\n\n");
            else
            {
                rest2.Append("The bike drop-in wants child pivots named: " + string.Join(", ", BikePivots) + ".\n\n");
                rest2.Append("| Asset | Pack | Tris | Size (m) | Length (m) | Wheel nodes | Handlebar/fork node | Drop-in pivots found | Child nodes (up to 12) | Pipeline OK |\n|---|---|---|---|---|---|---|---|---|---|\n");
                int n = 0;
                foreach (var i in vehicles)
                {
                    if (++n > RowCap) { rest2.Append($"\n+{vehicles.Count - RowCap} more\n"); break; }
                    rest2.Append($"| {Esc(i.path)}{(i.byFolder ? " (by folder)" : "")} | {Esc(i.pack)} | {i.tris} | {Size(i)} | {Num(i.length)} | {i.wheelNodes} | {Yes(i.steerNode)} | {i.pivots.Count}/{BikePivots.Length}{(i.pivots.Count > 0 ? " (" + string.Join(", ", i.pivots) + ")" : "")} | {Esc(i.nodeNames.Count == 0 ? "-" : string.Join(", ", i.nodeNames))} | {Esc(PipelineCell(i))} |\n");
                }
                rest2.Append('\n');
            }

            // ---- Uncategorised animated
            var uncatSb = new StringBuilder();
            rest2 = uncatSb;
            rest2.Append("## Animated models nothing recognised\n\nSkinned models with clips but no keyword hit; they may be animals with odd names.\n\n");
            if (uncat.Count == 0) rest2.Append("None.\n\n");
            else
            {
                rest2.Append("| Asset | Pack | Tris | Size (m) | Clips | Roles recognised | Rig | Pipeline OK |\n|---|---|---|---|---|---|---|---|\n");
                int n = 0;
                foreach (var i in uncat)
                {
                    if (++n > RowCap) { rest2.Append($"\n+{uncat.Count - RowCap} more\n"); break; }
                    rest2.Append($"| {Esc(i.path)} | {Esc(i.pack)} | {i.tris} | {Size(i)} | {i.ClipCount} | {RolesCell(i)} | {Esc(i.rig)} | {Esc(PipelineCell(i))} |\n");
                }
                rest2.Append('\n');
            }

            // ---- Scenery counts (the file only)
            var scenerySb = new StringBuilder();
            rest2 = scenerySb;
            rest2.Append("## Scenery noticed\n\nCounts only (named like trees, rocks, plants, props; includes things such as a \"Butterfly Bush\" shrub).\n\n");
            if (scenery.Count == 0) rest2.Append("None.\n");
            else
            {
                rest2.Append("| Pack | Trees | Rocks | Plants and grass | Props |\n|---|---|---|---|---|\n");
                var byPack = new SortedDictionary<string, int[]>(System.StringComparer.Ordinal);
                foreach (var i in scenery)
                {
                    if (!byPack.TryGetValue(i.pack, out var c)) byPack[i.pack] = c = new int[4];
                    c[i.sceneryKind == "Trees" ? 0 : i.sceneryKind == "Rocks" ? 1 : i.sceneryKind == "Plants and grass" ? 2 : 3]++;
                }
                int n = 0;
                foreach (var kv in byPack)
                {
                    if (++n > RowCap) { rest2.Append($"| +{byPack.Count - RowCap} more packs | | | | |\n"); break; }
                    rest2.Append($"| {Esc(kv.Key)} | {kv.Value[0]} | {kv.Value[1]} | {kv.Value[2]} | {kv.Value[3]} |\n");
                }
            }

            // ---- Other static models: names of everything nothing above recognised, grouped by pack.
            var otherSb = new StringBuilder();
            otherSb.Append("## Other static models\n\nModels and prefabs nothing above recognised, listed so unfamiliar packs can be read by name (grouped by pack; " +
                           $"at most {OtherPerPack} per pack and {OtherCap} in all).\n\n");
            if (otherStaticList.Count == 0) otherSb.Append("None.\n\n");
            else
            {
                otherStaticList.Sort((a, b) => { int c = string.CompareOrdinal(a.pack, b.pack); return c != 0 ? c : string.CompareOrdinal(a.path, b.path); });
                otherSb.Append("| Pack | Asset | Tris | Size (m) |\n|---|---|---|---|\n");
                string lastPack = null; int inPack = 0, listed = 0, skipped = 0;
                foreach (var i in otherStaticList)
                {
                    if (i.pack != lastPack) { if (skipped > 0) { otherSb.Append($"| | +{skipped} more in this pack | | |\n"); skipped = 0; } lastPack = i.pack; inPack = 0; }
                    if (listed >= OtherCap || inPack >= OtherPerPack) { skipped++; continue; }
                    if (i.unopened && !i.hasRenderers) QuickGeometry(i); // counted from its name so far: read its size now (only the ones listed)
                    otherSb.Append($"| {Esc(i.pack)} | {Esc(i.path)} | {(i.hasRenderers ? i.tris.ToString() : "-")} | {Size(i)} |\n");
                    inPack++; listed++;
                }
                if (skipped > 0) otherSb.Append($"| | +{skipped} more in this pack | | |\n");
                otherSb.Append('\n');
            }

            // The clipboard version: the sections that matter most, in this order, with the long tables trimmed to fit.
            // 1.9.1: what the species prefabs WildLens built actually use, slot by slot (the probe for a pink animal), near the top of both versions.
            string built = BuiltAnimalsSection(speciesList, speciesById);
            _shortText = HardCap(ShrinkTables(sum.ToString() + built + master + cov + macroSb + vehSb + uncatSb + otherSb, ClipboardLimit - 400), ClipboardLimit);
            return sum.ToString() + built + master + cov + macroSb + vehSb + uncatSb + otherSb + detailSb + scenerySb;
        }

        private const int BuiltRowCap = 120;
        private const int SmallLifeMatCap = 40;

        /// <summary>
        /// "WildLens animals as built" (1.9.1): for each species with a prefab in WildLens's Animals folder, every renderer's material slots with
        /// the shader, whether URP can draw it (the same test the factory uses: <see cref="AnimalPrefabFactory.UrpDrawIssue"/>), the material's
        /// file (or MISSING when the slot points at a material that no longer exists, or "empty"), its main texture and its colour. Then the
        /// converted small-life pack materials. Read-only.
        /// </summary>
        private static string BuiltAnimalsSection(List<SpeciesDefinition> speciesList, Dictionary<string, SpeciesDefinition> speciesById)
        {
            var sb = new StringBuilder();
            sb.Append("## WildLens animals as built\n\n");
            bool urp = AnimalPrefabFactory.UrpActive();
            sb.Append($"Factory version {AnimalPrefabFactory.FactoryVersion}; URP active: {(urp ? "yes" : "no")}. One row per material slot of each species prefab WildLens built (`{AnimalPrefabFactory.Folder}`).\n\n");
            sb.Append("| Species | Prefab | Renderer | Slot | Material file | Shader | URP draws it | Main texture | Colour |\n|---|---|---|---|---|---|---|---|---|\n");
            int rows = 0, bad = 0, prefabs = 0;
            foreach (var sp in speciesList)
            {
                if (sp == null || sp.prefab == null) continue;
                string path = AssetDatabase.GetAssetPath(sp.prefab);
                if (string.IsNullOrEmpty(path) || !path.StartsWith(AnimalPrefabFactory.Folder + "/", System.StringComparison.Ordinal)) continue;
                prefabs++;
                string name = SpeciesName(sp.speciesId, speciesById);
                try
                {
                    foreach (var r in sp.prefab.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null) continue;
                        var mats = r.sharedMaterials;
                        SerializedProperty arr = null;
                        try { arr = new SerializedObject(r).FindProperty("m_Materials"); } catch (System.Exception) { arr = null; }
                        if (mats.Length == 0) { if (++rows <= BuiltRowCap) sb.Append($"| {Esc(name)} | {Esc(path)} | {Esc(r.name)} | - | (no slots) | - | - | - | - |\n"); continue; }
                        for (int i = 0; i < mats.Length; i++)
                        {
                            var m = mats[i];
                            string file, shader, draws, tex = "-", col = "-";
                            if (m == null)
                            {
                                bool missing = false;
                                try { if (arr != null && i < arr.arraySize) { var e = arr.GetArrayElementAtIndex(i); missing = e.objectReferenceValue == null && e.objectReferenceInstanceIDValue != 0; } }
                                catch (System.Exception) { }
                                file = missing ? "MISSING (points at a deleted material)" : "empty";
                                shader = "-";
                            }
                            else
                            {
                                string mp = AssetDatabase.GetAssetPath(m);
                                file = string.IsNullOrEmpty(mp) ? "(not an asset)" : mp;
                                shader = m.shader != null ? m.shader.name : "(missing shader)";
                                foreach (var p in new[] { "_BaseMap", "_MainTex" })
                                    if (m.HasProperty(p) && m.GetTexture(p) != null) { tex = m.GetTexture(p).name; break; }
                                foreach (var p in new[] { "_BaseColor", "_Color" })
                                    if (m.HasProperty(p)) { var c = m.GetColor(p); col = $"{Num(c.r)}, {Num(c.g)}, {Num(c.b)}"; break; }
                            }
                            string issue = urp ? AnimalPrefabFactory.UrpDrawIssue(m) : null;
                            draws = !urp ? "n/a (not URP)" : issue == null ? "yes" : "NO: " + issue;
                            if (issue != null) bad++;
                            if (++rows <= BuiltRowCap)
                                sb.Append($"| {Esc(name)} | {Esc(path)} | {Esc(r.name)} | {i} | {Esc(file)} | {Esc(shader)} | {Esc(draws)} | {Esc(tex)} | {col} |\n");
                        }
                    }
                }
                catch (System.Exception e) { sb.Append($"| {Esc(name)} | {Esc(path)} | (could not be read: {Esc(e.Message)}) | | | | | | |\n"); }
            }
            if (rows > BuiltRowCap) sb.Append($"| +{rows - BuiltRowCap} more slots | | | | | | | | |\n");
            sb.Append(prefabs == 0 ? "\nNo species prefab has been built yet.\n\n" : $"\n{prefabs} species prefabs, {rows} material slots, {bad} that URP cannot draw{(bad > 0 ? " (these show magenta: Rebuild the slice, then run this audit again)" : "")}.\n\n");

            // Small life from packs: the converted copies the macro builder made.
            string folder = GeneratedAssets.Folder + "/SmallLife/Materials";
            if (AssetDatabase.IsValidFolder(folder))
            {
                var guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
                if (guids.Length > 0)
                {
                    sb.Append("Small-life pack materials (converted copies):\n\n| Material file | Shader | URP draws it | Main texture |\n|---|---|---|---|\n");
                    int n = 0;
                    foreach (var g in guids)
                    {
                        if (++n > SmallLifeMatCap) { sb.Append($"| +{guids.Length - SmallLifeMatCap} more | | | |\n"); break; }
                        string mp = AssetDatabase.GUIDToAssetPath(g);
                        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                        string shader = m == null ? "-" : m.shader != null ? m.shader.name : "(missing shader)";
                        string issue = urp ? AnimalPrefabFactory.UrpDrawIssue(m) : null;
                        string tex = "-";
                        if (m != null) foreach (var p in new[] { "_BaseMap", "_MainTex" }) if (m.HasProperty(p) && m.GetTexture(p) != null) { tex = m.GetTexture(p).name; break; }
                        sb.Append($"| {Esc(mp)} | {Esc(shader)} | {(!urp ? "n/a (not URP)" : issue == null ? "yes" : "NO: " + Esc(issue))} | {Esc(tex)} |\n");
                    }
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        private static string HardCap(string text, int limit) =>
            text.Length <= limit ? text : text.Substring(0, limit - 80) + "\n\n(cut off here: open WildLensAssetAudit.md for the rest)\n";

        /// <summary>
        /// Drops table rows (never headers, never whole sections) from the longest table first until the text fits,
        /// leaving "+N more rows in the file" where rows were removed.
        /// </summary>
        private static string ShrinkTables(string text, int limit)
        {
            if (text.Length <= limit) return text;
            var lines = new List<string>(text.Split('\n'));
            // Data rows grouped by table: a table is a run of lines starting with '|'; its first two lines are header and separator.
            var tables = new List<List<int>>();
            int i0 = 0;
            while (i0 < lines.Count)
            {
                if (!lines[i0].StartsWith("|")) { i0++; continue; }
                int j = i0;
                while (j < lines.Count && lines[j].StartsWith("|")) j++;
                var rows = new List<int>();
                for (int k = i0 + 2; k < j; k++) rows.Add(k);
                tables.Add(rows);
                i0 = j;
            }
            var dropped = new HashSet<int>();
            int length = text.Length;
            var dropCount = new int[tables.Count];
            while (length > limit)
            {
                int best = -1, bestLeft = 0;
                for (int t = 0; t < tables.Count; t++)
                {
                    int left = tables[t].Count - dropCount[t];
                    if (left > bestLeft) { best = t; bestLeft = left; }
                }
                if (best < 0) break;
                int row = tables[best][tables[best].Count - 1 - dropCount[best]];
                dropped.Add(row);
                dropCount[best]++;
                length -= lines[row].Length + 1;
            }
            var sb = new StringBuilder();
            for (int n = 0; n < lines.Count; n++)
            {
                if (dropped.Contains(n))
                {
                    // One marker per table, where its first dropped row was.
                    int t = tables.FindIndex(r => r.Contains(n));
                    int first = tables[t][tables[t].Count - dropCount[t]];
                    if (n == first) sb.Append($"| +{dropCount[t]} more rows in the file |\n");
                    continue;
                }
                sb.Append(lines[n]);
                if (n < lines.Count - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        private static string Cand(Info i, string id)
        {
            var m = i.species.Find(s => s.id == id);
            return $"{i.path} ({(m.kind == MatchKind.Exact ? "Exact" : "Stand-in")}, {i.ClipCount} clips{(i.animated ? "" : ", static")})";
        }
    }
}
