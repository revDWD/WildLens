using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using WildLens.Data;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Wildlife Pack Setup (1.6): one window that puts free animal packs to work across the whole reserve. It scans the
    /// project for animated animal models (any pack, any style, several packs at once), matches each to the species it
    /// is (a deer for the deer) or can stand in for (a wolf for the coyote, an eagle for the hawk, one songbird for the
    /// cardinal, the blue jay and the blackbird, tinted to each), and runs <see cref="AnimalPrefabFactory"/> on every
    /// ticked row. Species with no match keep their placeholder bodies. Nothing from a pack is copied into the
    /// package: the prefabs it makes live in the user's own Assets/WildLens/Animals.
    /// </summary>
    public class WildlifePackSetup : EditorWindow
    {
        /// <summary>How a model's name matches a species.</summary>
        public enum MatchKind { None, StandIn, Exact }

        /// <summary>Names that are the species, then names of animals that read like it at photographing distance.</summary>
        public static readonly Dictionary<string, (string[] exact, string[] standIn)> Names = new Dictionary<string, (string[], string[])>
        {
            { "white_tailed_deer", (new[] { "whitetail", "deer", "doe", "stag", "buck", "fawn" }, new[] { "roe", "elk", "antelope", "reindeer" }) },
            { "red_fox", (new[] { "fox" }, new[] { "shiba", "jackal" }) },
            { "coyote", (new[] { "coyote" }, new[] { "wolf", "jackal", "husky", "dog" }) },
            { "eastern_cottontail", (new[] { "cottontail", "rabbit", "bunny", "hare" }, new string[0]) },
            { "eastern_gray_squirrel", (new[] { "squirrel" }, new[] { "chipmunk" }) },
            { "raccoon", (new[] { "raccoon", "racoon" }, new string[0]) },
            { "virginia_opossum", (new[] { "opossum", "possum" }, new string[0]) },
            { "groundhog", (new[] { "groundhog", "woodchuck", "marmot" }, new[] { "beaver", "gopher", "prairiedog" }) },
            { "wild_turkey", (new[] { "turkey" }, new[] { "pheasant", "chicken", "hen", "peacock" }) },
            { "red_tailed_hawk", (new[] { "hawk", "buteo" }, new[] { "eagle", "falcon", "buzzard", "kite" }) },
            { "barred_owl", (new[] { "owl" }, new string[0]) },
            { "great_blue_heron", (new[] { "heron" }, new[] { "egret", "crane", "stork", "flamingo" }) },
            { "spotted_sandpiper", (new[] { "sandpiper" }, new[] { "plover", "shorebird", "seagull", "gull" }) },
            { "northern_cardinal", (new[] { "cardinal" }, new[] { "songbird", "finch", "sparrow", "robin", "bird" }) },
            { "blue_jay", (new[] { "bluejay", "jay" }, new[] { "songbird", "finch", "sparrow", "robin", "bird" }) },
            { "red_winged_blackbird", (new[] { "blackbird" }, new[] { "starling", "crow", "songbird", "sparrow", "bird" }) },
            { "belted_kingfisher", (new[] { "kingfisher" }, new[] { "songbird", "sparrow", "bird" }) },
            { "eastern_phoebe", (new[] { "phoebe", "flycatcher" }, new[] { "sparrow", "songbird", "finch", "bird" }) },
        };

        /// <summary>Lower-case words of a name: split on non-letters and camel case ("RedFox_Idle01" → red, fox, idle).</summary>
        public static List<string> Words(string name)
        {
            var words = new List<string>();
            var cur = new StringBuilder();
            char prev = ' ';
            foreach (char c in name)
            {
                bool letter = char.IsLetter(c);
                bool boundary = !letter || (char.IsUpper(c) && char.IsLower(prev));
                if (boundary && cur.Length > 0) { words.Add(cur.ToString().ToLowerInvariant()); cur.Clear(); }
                if (letter) cur.Append(c);
                prev = c;
            }
            if (cur.Length > 0) words.Add(cur.ToString().ToLowerInvariant());
            // Also the whole name squashed, so "WhiteTailDeer" and "blue_jay" match "whitetail" and "bluejay".
            string all = string.Concat(words);
            if (words.Count > 1) words.Add(all);
            return words;
        }

        /// <summary>
        /// How well a name matches: a short name word ("fox", "owl", "jay") must be a whole word, so "Foxglove" and
        /// "Bowl" don't count; longer ones may sit inside a word ("RedSquirrel01").
        /// </summary>
        public static MatchKind Match(string name, string speciesId)
        {
            if (!Names.TryGetValue(speciesId, out var n)) return MatchKind.None;
            var words = Words(name);
            if (Has(words, n.exact)) return MatchKind.Exact;
            return Has(words, n.standIn) ? MatchKind.StandIn : MatchKind.None;
        }

        private static bool Has(List<string> words, string[] names)
        {
            foreach (var k in names)
                foreach (var w in words)
                    if (k.Length <= 4 ? (w == k || w == k + "s") : w.Contains(k)) return true;
            return false;
        }

        private class Candidate
        {
            public GameObject asset;
            public string path;
            public MatchKind kind;
            public int clipCount;
            public List<AnimationClip> clips;
            public string Label => $"{(kind == MatchKind.Exact ? "" : "stand-in: ")}{asset.name}  ({clipCount} clips)  {System.IO.Path.GetDirectoryName(path)}";
        }

        private class Row
        {
            public SpeciesDefinition species;
            public List<Candidate> candidates = new List<Candidate>();
            public int pick;
            public bool on, tint;
            public float size, yaw;
            public Candidate Picked => pick >= 0 && pick < candidates.Count ? candidates[pick] : null;
        }

        private readonly List<Row> _rows = new List<Row>();
        private Vector2 _scroll;
        private string _log = "";
        private bool _scanned;

        [MenuItem("WildLens/Wildlife Pack Setup", priority = 11)]
        public static void Open() => GetWindow<WildlifePackSetup>("Wildlife Pack Setup");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Import any free animal packs into this project first (several are fine: styles may mix). Scan finds animated " +
                "models and matches them to the reserve's species, or to a stand-in (a wolf as the coyote, an eagle as the " +
                "hawk, one songbird for several birds, tinted to each). Check sizes, then Set Up. Species left unticked keep " +
                "their placeholder bodies. Animals spawn on Play: no rebuild needed.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Scan project", GUILayout.Height(26))) Scan();
                using (new EditorGUI.DisabledScope(!_scanned))
                    if (GUILayout.Button("Set up ticked species", GUILayout.Height(26))) SetUpTicked();
                EditorGUILayout.EndHorizontal();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var row in _rows)
            {
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(row.candidates.Count == 0))
                    row.on = EditorGUILayout.ToggleLeft(row.species.displayName + (row.species.prefab != null ? " ✓" : ""), row.on, GUILayout.Width(190));
                if (row.candidates.Count == 0)
                    EditorGUILayout.LabelField(row.species.prefab != null ? "model assigned (no new match)" : "no match: placeholder body", EditorStyles.miniLabel);
                else
                {
                    var labels = new string[row.candidates.Count];
                    for (int i = 0; i < labels.Length; i++) labels[i] = row.candidates[i].Label;
                    int pick = EditorGUILayout.Popup(row.pick, labels);
                    if (pick != row.pick) { row.pick = pick; row.tint = row.Picked.kind == MatchKind.StandIn; }
                    row.size = EditorGUILayout.FloatField(row.size, GUILayout.Width(46));
                    EditorGUILayout.LabelField("m", GUILayout.Width(14));
                    row.yaw = EditorGUILayout.FloatField(row.yaw, GUILayout.Width(40));
                    EditorGUILayout.LabelField("°", GUILayout.Width(12));
                    row.tint = EditorGUILayout.ToggleLeft("tint", row.tint, GUILayout.Width(44));
                }
                if (row.species.prefab != null && GUILayout.Button("Placeholder", GUILayout.Width(84)))
                {
                    Undo.RecordObject(row.species, "Revert species prefab");
                    row.species.prefab = null;
                    EditorUtility.SetDirty(row.species);
                }
                EditorGUILayout.EndHorizontal();
            }
            if (_rows.Count > 0)
                EditorGUILayout.LabelField("Size: mammals nose-to-tail or height, whichever is larger; birds beak to tail. " +
                                           "Yaw: turn a model that doesn't face +Z (often 90, -90 or 180).", EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(_log)) EditorGUILayout.HelpBox(_log, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            _rows.Clear();
            var db = AssetDatabase.LoadAssetAtPath<SpeciesDatabase>(WildLensSliceBuilder.DatabasePath) ?? WildLensSliceBuilder.EnsureSpeciesAssets();
            var models = FindAnimatedModels();
            _rows.AddRange(BuildRows(db, models));
            int matched = 0;
            foreach (var row in _rows) if (row.candidates.Count > 0) matched++;
            _scanned = true;
            _log = $"Found {models.Count} animated models; {matched} of {_rows.Count} species have a match.";
        }

        /// <summary>
        /// 1.6: the hands-off route. Every species without a model gets its best match (the animal itself, else a stand-in,
        /// tinted) at its default size. Run by every rebuild and after animal packs are imported; species that already
        /// have a model are never touched, so hand-made choices in the window stick. Returns how many were set up.
        /// </summary>
        public static int AutoSetUp(SpeciesDatabase db)
        {
            if (db == null || Application.isPlaying) return 0;
            bool anyMissing = false;
            foreach (var sp in db.species) if (sp != null && sp.prefab == null) { anyMissing = true; break; }
            if (!anyMissing) return 0;
            var models = FindAnimatedModels();
            if (models.Count == 0) return 0;
            var sb = new StringBuilder();
            int done = 0;
            foreach (var row in BuildRows(db, models))
            {
                if (row.species.prefab != null || row.Picked == null) continue;
                try
                {
                    var c = row.Picked;
                    AnimalPrefabFactory.Create(row.species, c.asset, AnimalPrefabFactory.Classify(c.clips), row.size, row.yaw, row.tint, out string message);
                    sb.Append("\n  • ").Append(message).Append(c.kind == MatchKind.StandIn ? $" (stand-in: {c.asset.name})" : "");
                    done++;
                }
                catch (System.Exception e)
                {
                    sb.Append("\n  • ").Append(row.species.displayName).Append(" failed: ").Append(e.Message);
                }
            }
            if (done > 0 || sb.Length > 0)
                Debug.Log($"[WildLens] Wildlife models: {done} species now use animal models from your packs." + sb +
                          "\nPress Play to see them. To change one, open WildLens ▸ Wildlife Pack Setup.");
            return done;
        }

        private static List<Row> BuildRows(SpeciesDatabase db, List<Model> models)
        {
            var rows = new List<Row>();
            foreach (var species in db.species)
            {
                if (species == null) continue;
                var row = new Row { species = species, size = AnimalPrefabFactory.DefaultSize(species) };
                foreach (var m in models)
                {
                    var kind = Match(m.asset.name, species.speciesId);
                    if (kind == MatchKind.None) kind = Match(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(m.path)), species.speciesId);
                    if (kind == MatchKind.None) continue;
                    row.candidates.Add(new Candidate { asset = m.asset, path = m.path, kind = kind, clips = m.clips, clipCount = m.clipCount });
                }
                // Best first: the species itself over a stand-in, then the model with the most recognised clips.
                row.candidates.Sort((a, b) => a.kind != b.kind ? b.kind.CompareTo(a.kind) : b.clipCount.CompareTo(a.clipCount));
                row.on = row.candidates.Count > 0 && species.prefab == null;
                row.tint = row.Picked != null && row.Picked.kind == MatchKind.StandIn;
                rows.Add(row);
            }
            return rows;
        }

        private struct Model { public GameObject asset; public string path; public List<AnimationClip> clips; public int clipCount; }

        /// <summary>Models and prefabs outside WildLens's own folder with a skinned body and at least one clip.</summary>
        private static List<Model> FindAnimatedModels()
        {
            var list = new List<Model>();
            var seen = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(GeneratedAssets.Root + "/") || !seen.Add(path)) continue;
                if (path.Contains("@")) continue; // "Deer@Walk.fbx": an animation file, used as a clip source below
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) continue;
                var sources = new List<Object> { go };
                // Clips kept beside the model: "Deer@Walk.fbx" files and loose clips named for it.
                string dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                string stem = System.IO.Path.GetFileNameWithoutExtension(path);
                foreach (string cg in AssetDatabase.FindAssets("t:AnimationClip", new[] { dir }))
                {
                    string cp = AssetDatabase.GUIDToAssetPath(cg);
                    string file = System.IO.Path.GetFileNameWithoutExtension(cp);
                    if (file.StartsWith(stem + "@") || file.StartsWith(stem + "_")) sources.Add(AssetDatabase.LoadAssetAtPath<Object>(cp));
                }
                var clips = AnimalPrefabFactory.ClipsIn(sources);
                if (clips.Count == 0) continue;
                list.Add(new Model { asset = go, path = path, clips = clips, clipCount = AnimalPrefabFactory.Classify(clips).Count });
            }
            return list;
        }

        private void SetUpTicked()
        {
            var sb = new StringBuilder();
            int done = 0;
            foreach (var row in _rows)
            {
                if (!row.on || row.Picked == null) continue;
                try
                {
                    var c = row.Picked;
                    var clips = AnimalPrefabFactory.Classify(c.clips);
                    AnimalPrefabFactory.Create(row.species, c.asset, clips, row.size, row.yaw, row.tint, out string message);
                    sb.Append("✓ ").Append(message).Append(c.kind == MatchKind.StandIn ? $" — stand-in: {c.asset.name}" : "").Append('\n');
                    row.on = false;
                    done++;
                }
                catch (System.Exception e)
                {
                    sb.Append("✗ ").Append(row.species.displayName).Append(": ").Append(e.Message).Append('\n');
                    Debug.LogException(e);
                }
            }
            AssetDatabase.SaveAssets();
            _log = $"Set up {done} species. Press Play and photograph them; adjust a size or yaw and set up again if one looks wrong.\n" + sb;
            Debug.Log("[WildLens] Wildlife Pack Setup: " + _log);
        }
    }

    /// <summary>
    /// 1.6: importing an animal pack is enough. When models or prefabs arrive outside Assets/WildLens, WildLens waits for
    /// the import to finish and runs <see cref="WildlifePackSetup.AutoSetUp"/> (if the builder setting is on).
    /// </summary>
    public class WildlifePackImportWatcher : AssetPostprocessor
    {
        private static bool _queued;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_queued || Application.isPlaying) return;
            foreach (var path in imported)
            {
                if (path.StartsWith(GeneratedAssets.Root + "/")) continue;
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".fbx" && ext != ".prefab" && ext != ".blend" && ext != ".gltf" && ext != ".glb") continue;
                _queued = true;
                EditorApplication.delayCall += Run;
                return;
            }
        }

        private static void Run()
        {
            _queued = false;
            if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) { _queued = true; EditorApplication.delayCall += Run; return; }
            var settings = AssetDatabase.LoadAssetAtPath<SliceBuildSettings>(WildLensSliceBuilder.SettingsPath);
            if (settings != null && !settings.autoWildlifeFromPacks) return;
            var db = AssetDatabase.LoadAssetAtPath<SpeciesDatabase>(WildLensSliceBuilder.DatabasePath);
            if (db != null) WildlifePackSetup.AutoSetUp(db); // no database yet: the first build will do it
        }
    }
}
