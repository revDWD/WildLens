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

        // <pure-begin> (name logic only: no Unity types; the throwaway test extracts everything between these markers)

        /// <summary>
        /// Names that are the species, then names of animals that read like it at photographing distance. Order matters:
        /// among stand-ins, a name earlier in the list ranks higher ("wolf" before "dog" for the coyote, "bluebird" before
        /// "bird" for the blue jay).
        /// </summary>
        public static readonly Dictionary<string, (string[] exact, string[] standIn)> Names = new Dictionary<string, (string[], string[])>
        {
            { "white_tailed_deer", (new[] { "whitetail", "deer", "doe", "stag", "buck", "fawn" }, new[] { "roe", "elk", "reindeer", "caribou", "moose", "antelope", "gazelle", "impala" }) },
            { "red_fox", (new[] { "fox" }, new[] { "shiba", "fennec", "jackal", "dog" }) }, // a dog is the last resort: the coyote takes a wolf first
            { "coyote", (new[] { "coyote" }, new[] { "wolf", "dingo", "dhole", "jackal", "husky", "shepherd", "dog" }) },
            { "eastern_cottontail", (new[] { "cottontail", "rabbit", "bunny", "hare" }, new string[0]) },
            { "eastern_gray_squirrel", (new[] { "squirrel", "squirel" }, new[] { "chipmunk" }) },
            { "raccoon", (new[] { "raccoon", "racoon" }, new[] { "badger", "redpanda", "coati" }) },
            { "virginia_opossum", (new[] { "opossum", "possum" }, new[] { "rat", "mouse" }) },
            { "groundhog", (new[] { "groundhog", "woodchuck", "marmot" }, new[] { "beaver", "gopher", "prairiedog", "capybara", "hamster", "guineapig" }) },
            { "wild_turkey", (new[] { "turkey" }, new[] { "pheasant", "grouse", "partridge", "quail", "guineafowl", "chicken", "rooster", "hen", "peacock" }) },
            { "red_tailed_hawk", (new[] { "hawk", "buteo" }, new[] { "eagle", "falcon", "buzzard", "harrier", "osprey", "kite", "vulture", "raptor" }) },
            { "barred_owl", (new[] { "owl" }, new string[0]) },
            { "great_blue_heron", (new[] { "heron" }, new[] { "egret", "bittern", "ibis", "spoonbill", "pelican", "crane", "stork", "flamingo" }) },
            { "spotted_sandpiper", (new[] { "sandpiper" }, new[] { "killdeer", "plover", "dunlin", "snipe", "shorebird", "wader", "tern", "seagull", "gull" }) },
            { "northern_cardinal", (new[] { "cardinal", "redbird" }, new[] { "songbird", "finch", "sparrow", "robin", "oriole", "canary", "chickadee", "wren", "tit", "thrush", "warbler", "swallow", "bird" }) },
            { "blue_jay", (new[] { "bluejay", "jay" }, new[] { "bluebird", "songbird", "finch", "sparrow", "robin", "chickadee", "tit", "wren", "thrush", "warbler", "swallow", "bird" }) },
            // The crow and the sparrow are species of their own now and both are "owned elsewhere"; a small sparrow tinted black reads as a
            // blackbird better than a crow shrunk to blackbird size, so sparrow ranks ahead of crow here.
            { "red_winged_blackbird", (new[] { "blackbird" }, new[] { "grackle", "starling", "sparrow", "songbird", "crow", "raven", "thrush", "bird" }) },
            { "belted_kingfisher", (new[] { "kingfisher" }, new[] { "songbird", "sparrow", "bird" }) },
            { "eastern_phoebe", (new[] { "phoebe", "flycatcher" }, new[] { "sparrow", "songbird", "finch", "wren", "chickadee", "bird" }) },
            // 1.7.1: species of their own (the bird and bear packs were found in the field audit).
            { "american_crow", (new[] { "crow", "raven" }, new string[0]) },
            { "american_robin", (new[] { "robin" }, new string[0]) },
            { "black_capped_chickadee", (new[] { "chickadee", "tit=" }, new string[0]) },
            { "american_goldfinch", (new[] { "goldfinch", "finch", "canary" }, new string[0]) },
            { "song_sparrow", (new[] { "sparrow" }, new[] { "wren" }) },
            { "american_black_bear", (new[] { "blackbear", "bear" }, new string[0]) }, // "bear" is a whole word: never "beard" or "bearing"
        };

        /// <summary>Words that rule a model out for ONE species only ("polar" is not a black bear, "red panda" is still a raccoon stand-in). "=" means whole word.</summary>
        public static readonly Dictionary<string, string[]> SpeciesNever = new Dictionary<string, string[]>
        {
            { "american_black_bear", new[] { "polar=", "panda=", "teddy=", "koala=" } },
        };

        /// <summary>
        /// Species where, among equally ranked candidates, a model whose name (or folder) has one of these words comes first, in
        /// this order: a pack's "Bear_1..Bear_4" are colour variants, and a black bear should be the black or dark one.
        /// </summary>
        public static readonly Dictionary<string, string[]> Prefer = new Dictionary<string, string[]>
        {
            { "american_black_bear", new[] { "black", "dark" } },
        };

        /// <summary>
        /// Species tinted to their own colours even when the model is the real thing (an Exact match): a pack's bears are usually
        /// brown grizzlies, and the black bear is black.
        /// </summary>
        public static readonly HashSet<string> TintExact = new HashSet<string> { "american_black_bear" };

        /// <summary>Whether a match is tinted by default: every stand-in, and the species in <see cref="TintExact"/>.</summary>
        internal static bool TintByDefault(string speciesId, MatchKind kind) =>
            kind == MatchKind.StandIn || (kind == MatchKind.Exact && speciesId != null && TintExact.Contains(speciesId));

        /// <summary>Lower is preferred: the index of the first <see cref="Prefer"/> word in the model's name (then, further back, in its folder); a large number when none.</summary>
        internal static int PreferScore(string speciesId, string name, string folder)
        {
            if (speciesId == null || !Prefer.TryGetValue(speciesId, out var words)) return 0;
            for (int i = 0; i < words.Length; i++) if (HasWord(name, words[i])) return i;
            for (int i = 0; i < words.Length; i++) if (HasWord(folder, words[i])) return 100 + i;
            return 1000;
        }

        /// <summary>Words that make a folder or file name look like an animal pack, beyond the species keywords themselves.</summary>
        internal static readonly string[] AnimalContainerWords = { "animal", "animals", "creature", "creatures", "wildlife", "bird", "birds", "fauna" };

        private static List<string> _importKeys;

        /// <summary>
        /// Whether an imported model or prefab could be an animal: its file name or its folder has a species keyword, a reserved
        /// animal word or an animal container word. The import watcher uses this so importing a tree or a rock pack does nothing.
        /// </summary>
        internal static bool LooksLikeAnimalImport(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            if (_importKeys == null)
            {
                var keys = new List<string>(AnimalContainerWords);
                keys.AddRange(Reserved);
                foreach (var kv in Names) { keys.AddRange(kv.Value.exact); keys.AddRange(kv.Value.standIn); }
                _importKeys = keys;
            }
            string p = assetPath.Replace('\\', '/');
            string stem = System.IO.Path.GetFileNameWithoutExtension(p);
            string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(p) ?? "");
            var keyArray = _importKeys.ToArray();
            return Has(Words(stem), keyArray) || Has(Words(folder), keyArray);
        }

        /// <summary>
        /// Animals that are future species of their own: never used as a stand-in, not even through a generic folder name
        /// ("Birds/Woodpecker" must not become the cardinal).
        /// </summary>
        public static readonly string[] Reserved = { "woodpecker", "hummingbird", "duck", "mallard", "goose", "swan" };

        /// <summary>
        /// A model whose own name has one of these words is not an animal here (a beetle is not a stag, a skull is not a deer, a
        /// hat is not a fox). "fly" is deliberately absent ("Eagle_Fly.fbx" is a model with a clip in its name); the insect
        /// compounds are listed whole instead.
        /// </summary>
        public static readonly string[] NeverAnimals =
        {
            "beetle", "moth", "bee", "spider", "bug", "ant", "butterfly", "dragonfly", "damselfly", "firefly", "horsefly", "mantis",
            "snail", "fish", "net", "statue", "toy", "hat", "mask", "costume", "skeleton", "bone", "bones", "skull", "meat", "trophy",
            "rug", "pelt",
            // Rigs, spawners and demo scenes that sit next to a pack's animals ("_livingBirdsController" is not a bird).
            "controller", "manager", "spawner", "system", "demo",
        };

        /// <summary>Lower-case words of a name: split on non-letters and camel case ("RedFox_Idle01" → red, fox, idle).</summary>
        public static List<string> Words(string name)
        {
            var words = AnimalPrefabFactory.Tokens(name);
            // Also the whole name squashed, so "WhiteTailDeer" and "blue_jay" match "whitetail" and "bluejay".
            if (words.Count > 1) words.Add(string.Concat(words));
            return words;
        }

        /// <summary>
        /// One keyword against one name word. A short key ("fox", "owl", "jay", "cat", "ant") must be a whole word (or its
        /// plural), so "Foxglove", "Bowl", "Cattail" and "Plant" don't count; longer ones may sit inside a word
        /// ("RedSquirrel01"). A key ending in "=" is always whole-word ("aster=" is not "Disaster").
        /// </summary>
        internal static bool KeyHit(string key, string word)
        {
            bool whole = key.EndsWith("=");
            string k = whole ? key.Substring(0, key.Length - 1) : key;
            return whole || k.Length <= 4 ? (word == k || word == k + "s") : word.Contains(k);
        }

        /// <summary>Index in <paramref name="names"/> of the first keyword some word hits, or -1.</summary>
        internal static int HasIndex(List<string> words, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
                foreach (var w in words)
                    if (KeyHit(names[i], w)) return i;
            return -1;
        }

        internal static bool Has(List<string> words, string[] names) => HasIndex(words, names) >= 0;

        /// <summary>
        /// How well a name matches: a short name word ("fox", "owl", "jay") must be a whole word, so "Foxglove" and
        /// "Bowl" don't count; longer ones may sit inside a word ("RedSquirrel01").
        /// </summary>
        public static MatchKind Match(string name, string speciesId) => Match(name, speciesId, out _);

        /// <summary>As above; <paramref name="rank"/> is the position of the keyword hit (lower = closer to the species).</summary>
        internal static MatchKind Match(string name, string speciesId, out int rank)
        {
            rank = int.MaxValue;
            if (!Names.TryGetValue(speciesId, out var n)) return MatchKind.None;
            var words = Words(name);
            if (Has(words, Reserved)) return MatchKind.None;
            foreach (var w in words)
                foreach (var bad in NeverAnimals)
                    if (w == bad || w == bad + "s") return MatchKind.None;
            if (SpeciesNever.TryGetValue(speciesId, out var never) && Has(words, never)) return MatchKind.None;
            int i = HasIndex(words, n.exact);
            if (i >= 0) { rank = i; return MatchKind.Exact; }
            i = HasIndex(words, n.standIn);
            if (i >= 0) { rank = i; return MatchKind.StandIn; }
            return MatchKind.None;
        }

        /// <summary>
        /// The model's own name first; only when that says nothing, the folder it sits in ("Deer/Model01"). A folder can name
        /// a stand-in at most, never the real thing, and its rank is pushed behind every name-based stand-in.
        /// </summary>
        internal static MatchKind MatchAsset(string assetName, string folderName, string speciesId, out int rank)
        {
            var kind = Match(assetName, speciesId, out rank);
            // A species' own never-words also count in the folder: a "Bear" in a "PolarBears" folder is no black bear.
            if (kind != MatchKind.None && !string.IsNullOrEmpty(folderName) && SpeciesNever.TryGetValue(speciesId, out var never) && Has(Words(folderName), never))
            { rank = int.MaxValue; return MatchKind.None; }
            if (kind != MatchKind.None || string.IsNullOrEmpty(folderName)) return kind;
            var nameWords = Words(assetName);
            if (Has(nameWords, Reserved)) return MatchKind.None;
            foreach (var w in nameWords)
                foreach (var bad in NeverAnimals)
                    if (w == bad || w == bad + "s") return MatchKind.None;
            var fromFolder = Match(folderName, speciesId, out int folderRank);
            if (fromFolder == MatchKind.None) return MatchKind.None;
            rank = 1000 + folderRank; // after every keyword hit in a model's own name
            return MatchKind.StandIn;
        }

        /// <summary>One model as the ranking sees it (no Unity types, so the ranking can be tested without Unity).</summary>
        internal struct ModelRef { public string path, name, folder; public int clips; public bool prefab; }

        /// <summary>A model that matched a species, in rank order.</summary>
        internal struct Ranked { public int model; public MatchKind kind; public int rank; public bool owned, hq; }

        internal static bool HasWord(string name, string word)
        {
            foreach (var t in AnimalPrefabFactory.Tokens(name)) if (t == word) return true;
            return false;
        }

        /// <summary>For each model path, the species it is the real thing for (so it is not also the first choice as someone's stand-in).</summary>
        internal static Dictionary<string, List<string>> ExactOwners(IList<ModelRef> models, IList<string> speciesIds)
        {
            var owners = new Dictionary<string, List<string>>();
            foreach (var m in models)
                foreach (var id in speciesIds)
                    if (MatchAsset(m.name, m.folder, id, out _) == MatchKind.Exact)
                    {
                        if (!owners.TryGetValue(m.path, out var list)) owners[m.path] = list = new List<string>();
                        list.Add(id);
                    }
            return owners;
        }

        /// <summary>
        /// The models that match a species, best first: the species itself over a stand-in; a model that is not another species'
        /// own over one that is; the keyword closest to the species ("wolf" before "dog"); the most recognised clip roles; the
        /// preferred name for the species (<see cref="Prefer"/>: the black bear over the brown ones); the
        /// high-quality variant ("lb_crowHQ" over "lb_crow"); a prefab over a raw model; then by path, so the pick is the same
        /// on every machine.
        /// </summary>
        internal static List<Ranked> RankFor(string speciesId, IList<ModelRef> models, Dictionary<string, List<string>> owners)
        {
            var list = new List<Ranked>();
            for (int i = 0; i < models.Count; i++)
            {
                var m = models[i];
                var kind = MatchAsset(m.name, m.folder, speciesId, out int rank);
                if (kind == MatchKind.None) continue;
                bool owned = kind == MatchKind.StandIn && owners != null && owners.TryGetValue(m.path, out var o) && o.Count > 0;
                list.Add(new Ranked { model = i, kind = kind, rank = rank, owned = owned, hq = HasWord(m.name, "hq") });
            }
            list.Sort((a, b) =>
            {
                if (a.kind != b.kind) return b.kind.CompareTo(a.kind);
                if (a.owned != b.owned) return a.owned ? 1 : -1;
                if (a.rank != b.rank) return a.rank.CompareTo(b.rank);
                var ma = models[a.model]; var mb = models[b.model];
                if (ma.clips != mb.clips) return mb.clips.CompareTo(ma.clips);
                int pa = PreferScore(speciesId, ma.name, ma.folder), pb = PreferScore(speciesId, mb.name, mb.folder);
                if (pa != pb) return pa.CompareTo(pb);
                if (a.hq != b.hq) return a.hq ? -1 : 1;
                if (ma.prefab != mb.prefab) return ma.prefab ? -1 : 1;
                return string.CompareOrdinal(ma.path, mb.path);
            });
            return list;
        }

        // <pure-end>

        private class Candidate
        {
            public GameObject asset;
            public string path;
            public MatchKind kind;
            public int clipCount, rank;
            public bool ownedElsewhere; // exact match for another species: a poorer stand-in here
            public List<AnimationClip> clips;
            public string Label => $"{(kind == MatchKind.Exact ? "" : "stand-in: ")}{asset.name}  ({clipCount} clips)  {System.IO.Path.GetDirectoryName(path)}";
        }

        private class Row
        {
            public SpeciesDefinition species;
            public List<Candidate> candidates = new List<Candidate>();
            public int pick;
            public bool on, tint, kept;
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
                    row.on = EditorGUILayout.ToggleLeft(row.species.displayName + (row.species.prefab != null ? " ✓" : row.kept ? " (kept)" : ""), row.on, GUILayout.Width(190));
                if (row.candidates.Count == 0)
                    EditorGUILayout.LabelField(row.species.prefab != null ? "model assigned (no new match)" : "no match: placeholder body", EditorStyles.miniLabel);
                else
                {
                    var labels = new string[row.candidates.Count];
                    for (int i = 0; i < labels.Length; i++) labels[i] = row.candidates[i].Label;
                    int pick = EditorGUILayout.Popup(row.pick, labels);
                    if (pick != row.pick) { row.pick = pick; row.tint = TintByDefault(row.species.speciesId, row.Picked.kind); }
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
                    SetKeepPlaceholder(row.species.speciesId, true); // sticks: automatic set-up skips it from now on
                    row.kept = true;
                    row.on = false;
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
            _rows.AddRange(BuildRows(db, models, AssetDatabase.LoadAssetAtPath<SliceBuildSettings>(WildLensSliceBuilder.SettingsPath)));
            int matched = 0;
            foreach (var row in _rows) if (row.candidates.Count > 0) matched++;
            _scanned = true;
            _log = $"Found {models.Count} animated models; {matched} of {_rows.Count} species have a match.";
        }

        /// <summary>
        /// 1.6: the hands-off route. Every species without a model gets its best match (the animal itself, else a stand-in,
        /// tinted) at its default size. Run by every rebuild and after animal packs are imported. A species that already has a
        /// model is never replaced by a different one, so hand-made choices in the window stick; since 1.8 a prefab that this
        /// automatic route built with an older factory is rebuilt from the same model (see <see cref="RefreshOldBuilds"/>).
        /// Returns how many were set up or refreshed.
        /// </summary>
        public static int AutoSetUp(SpeciesDatabase db)
        {
            if (db == null || Application.isPlaying) return 0;
            var settings = AssetDatabase.LoadAssetAtPath<SliceBuildSettings>(WildLensSliceBuilder.SettingsPath);
            int refreshed = 0;
            try { refreshed = RefreshOldBuilds(db, settings); }
            catch (System.Exception e) { Debug.LogWarning("[WildLens] Refreshing older wildlife models failed: " + e.Message); }
            settings = AssetDatabase.LoadAssetAtPath<SliceBuildSettings>(WildLensSliceBuilder.SettingsPath); // a record may just have created it
            bool anyMissing = false;
            foreach (var sp in db.species)
                if (sp != null && sp.prefab == null && !IsKeptPlaceholder(settings, sp.speciesId)) { anyMissing = true; break; }
            if (!anyMissing) return refreshed;
            var models = FindAnimatedModels();
            if (models.Count == 0) return refreshed;
            var sb = new StringBuilder();
            var unmatched = new List<string>();
            int done = 0;
            foreach (var row in BuildRows(db, models, settings))
            {
                if (row.species.prefab != null || row.kept) continue; // a model already assigned, or sent back to a placeholder by hand: never touched
                if (row.Picked == null) { unmatched.Add(row.species.displayName); continue; }
                try
                {
                    var c = row.Picked;
                    AnimalPrefabFactory.Create(row.species, c.asset, AnimalPrefabFactory.Classify(c.clips), row.size, row.yaw, row.tint, out string message, manual: false);
                    sb.Append("\n  • ").Append(message).Append(c.kind == MatchKind.StandIn ? $" (stand-in: {c.asset.name})" : "");
                    done++;
                }
                catch (System.Exception e)
                {
                    sb.Append("\n  • ").Append(row.species.displayName).Append(" failed: ").Append(e.Message);
                }
            }
            if (done > 0 || sb.Length > 0 || unmatched.Count > 0)
                Debug.Log($"[WildLens] Wildlife models: {done} species now use animal models from your packs." + sb +
                          (unmatched.Count > 0 ? "\nNo match yet (placeholder bodies): " + string.Join(", ", unmatched) + "." : "") +
                          "\nPress Play to see them. To change one, open WildLens ▸ Wildlife Pack Setup." +
                          "\nRun WildLens ▸ Asset Audit Report for the full list of what was found.");
            return done + refreshed;
        }

        /// <summary>
        /// 1.8: species prefabs the factory built before this version (wrong sizes, pack scripts left in, built-in materials, clips that
        /// play once) are rebuilt from the same model with today's defaults. A species is refreshed only when ALL of these hold:
        /// it has a prefab; it is not kept as a placeholder; its build record is missing or older than
        /// <see cref="AnimalPrefabFactory.FactoryVersion"/>; the record (if any) is not <c>manual</c> (set by a person in a setup
        /// window); and the prefab is the factory's own file (Assets/WildLens/Animals/{id}.prefab). The model is the recorded
        /// one, else traced back from the prefab's meshes; a prefab whose model cannot be found is left alone (and recorded as
        /// current, so it is not tried again). Recorded yaw and tint are kept, the size is the current default.
        /// </summary>
        internal static int RefreshOldBuilds(SpeciesDatabase db, SliceBuildSettings settings)
        {
            if (db == null || db.species == null) return 0;
            var refreshedNames = new List<string>();
            var failed = new List<string>();
            bool touched = false;
            foreach (var sp in db.species)
            {
                if (sp == null || sp.prefab == null || string.IsNullOrEmpty(sp.speciesId) || IsKeptPlaceholder(settings, sp.speciesId)) continue;
                var rec = AnimalPrefabFactory.FindRecord(settings, sp.speciesId);
                if (rec != null && (rec.manual || rec.factoryVersion >= AnimalPrefabFactory.FactoryVersion)) continue;
                string id = sp.speciesId;
                try
                {
                    // A prefab somebody assigned by hand (not the factory's file) is theirs: record it so it is never looked at again.
                    if (AssetDatabase.GetAssetPath(sp.prefab) != $"{AnimalPrefabFactory.Folder}/{id}.prefab")
                    {
                        touched = true; AnimalPrefabFactory.WriteRecord(id, "", AnimalPrefabFactory.DefaultSize(sp), 0f, false, true);
                        continue;
                    }
                    string srcPath = null;
                    if (rec != null && !string.IsNullOrEmpty(rec.sourceGuid)) srcPath = AssetDatabase.GUIDToAssetPath(rec.sourceGuid);
                    if (string.IsNullOrEmpty(srcPath) || AssetDatabase.LoadAssetAtPath<GameObject>(srcPath) == null)
                    {
                        srcPath = null;
                        foreach (var candidate in AnimalPrefabFactory.SourceModelPaths(sp.prefab))
                            if (AssetDatabase.LoadAssetAtPath<GameObject>(candidate) != null) { srcPath = candidate; break; }
                    }
                    var src = string.IsNullOrEmpty(srcPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
                    if (src == null || src.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
                    {
                        touched = true; AnimalPrefabFactory.WriteRecord(id, "", rec != null ? rec.size : AnimalPrefabFactory.DefaultSize(sp), rec != null ? rec.yaw : 0f, rec != null && rec.tint, false);
                        continue; // cannot be traced: left alone, not retried
                    }
                    var clips = CollectClips(srcPath, src);
                    if (clips.Count == 0)
                    {
                        touched = true; AnimalPrefabFactory.WriteRecord(id, AssetDatabase.AssetPathToGUID(srcPath), AnimalPrefabFactory.DefaultSize(sp), rec != null ? rec.yaw : 0f, rec != null && rec.tint, false);
                        continue;
                    }
                    var kind = MatchAsset(src.name, System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(srcPath)), id, out _);
                    bool tint = (rec != null ? rec.tint : kind == MatchKind.StandIn) || TintByDefault(id, kind);
                    float yaw = rec != null ? rec.yaw : 0f;
                    AnimalPrefabFactory.Create(sp, src, AnimalPrefabFactory.Classify(clips), AnimalPrefabFactory.DefaultSize(sp), yaw, tint, out _, manual: false);
                    refreshedNames.Add(sp.displayName);
                }
                catch (System.Exception e)
                {
                    failed.Add(sp.displayName + " (" + e.Message + ")");
                    try { touched = true; AnimalPrefabFactory.WriteRecord(id, rec != null ? rec.sourceGuid : "", rec != null ? rec.size : AnimalPrefabFactory.DefaultSize(sp), rec != null ? rec.yaw : 0f, rec != null && rec.tint, false); } catch (System.Exception) { }
                }
            }
            if (touched) AssetDatabase.SaveAssets(); // records written without a build (foreign or untraceable prefabs)
            if (refreshedNames.Count > 0)
                Debug.Log("[WildLens] Wildlife models refreshed for 1.8 (sizes, looping clips, materials): " + string.Join(", ", refreshedNames) + "." +
                          (failed.Count > 0 ? "\nCould not refresh: " + string.Join("; ", failed) + "." : "") +
                          "\nRebuilt from the same models; a model you set by hand in a setup window is never rebuilt automatically.");
            else if (failed.Count > 0)
                Debug.LogWarning("[WildLens] Could not refresh older wildlife models: " + string.Join("; ", failed) + ".");
            return refreshedNames.Count;
        }

        private static List<Row> BuildRows(SpeciesDatabase db, List<Model> models, SliceBuildSettings settings = null)
        {
            var rows = new List<Row>();
            var refs = new List<ModelRef>();
            foreach (var m in models)
                refs.Add(new ModelRef
                {
                    path = m.path, name = m.asset.name, clips = m.clipCount, prefab = m.path.EndsWith(".prefab"),
                    folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(m.path)),
                });
            var ids = new List<string>();
            foreach (var species in db.species) if (species != null) ids.Add(species.speciesId);
            var owners = ExactOwners(refs, ids);
            foreach (var species in db.species)
            {
                if (species == null) continue;
                var row = new Row { species = species, size = AnimalPrefabFactory.DefaultSize(species) };
                foreach (var r in RankFor(species.speciesId, refs, owners))
                {
                    var m = models[r.model];
                    row.candidates.Add(new Candidate { asset = m.asset, path = m.path, kind = r.kind, rank = r.rank, ownedElsewhere = r.owned, clips = m.clips, clipCount = m.clipCount });
                }
                row.kept = IsKeptPlaceholder(settings, species.speciesId);
                row.on = row.candidates.Count > 0 && species.prefab == null && !row.kept;
                row.tint = row.Picked != null && TintByDefault(species.speciesId, row.Picked.kind);
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>Whether the species was sent back to its placeholder body by hand (the Placeholder button); automatic set-up leaves it alone.</summary>
        internal static bool IsKeptPlaceholder(SliceBuildSettings settings, string speciesId) =>
            settings != null && settings.keepPlaceholder != null && !string.IsNullOrEmpty(speciesId) && settings.keepPlaceholder.Contains(speciesId);

        /// <summary>Adds the species to (or removes it from) the settings' keep-as-placeholder list. Null-safe for settings assets from before 1.7.1.</summary>
        internal static void SetKeepPlaceholder(string speciesId, bool keep)
        {
            if (string.IsNullOrEmpty(speciesId)) return;
            var settings = keep ? WildLensSliceBuilder.LoadOrCreateSettings() : AssetDatabase.LoadAssetAtPath<SliceBuildSettings>(WildLensSliceBuilder.SettingsPath);
            if (settings == null) return;
            if (settings.keepPlaceholder == null) settings.keepPlaceholder = new List<string>();
            bool has = settings.keepPlaceholder.Contains(speciesId);
            if (has == keep) return;
            Undo.RecordObject(settings, keep ? "Keep species as placeholder" : "Use a model for species");
            if (keep) settings.keepPlaceholder.Add(speciesId); else settings.keepPlaceholder.RemoveAll(x => x == speciesId);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets(); // SaveAssetIfDirty is not in the 2021.1 reference assemblies the compile check uses
        }

        private struct Model { public GameObject asset; public string path; public List<AnimationClip> clips; public int clipCount; }

        /// <summary>
        /// Every clip a model can use: its own, and the ones kept beside it ("Deer@Walk.fbx" files and loose clips named
        /// "Deer_..."). Shared with the Asset Audit. <paramref name="dirCache"/> (optional) remembers each folder's clip
        /// files, which makes a scan of thousands of models fast.
        /// </summary>
        internal static List<AnimationClip> CollectClips(string path, GameObject go, Dictionary<string, List<string>> dirCache = null)
        {
            var sources = new List<Object> { go };
            string dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string stem = System.IO.Path.GetFileNameWithoutExtension(path);
            List<string> clipFiles = null;
            if (dirCache == null || !dirCache.TryGetValue(dir, out clipFiles))
            {
                clipFiles = new List<string>();
                foreach (string cg in AssetDatabase.FindAssets("t:AnimationClip", new[] { dir }))
                    clipFiles.Add(AssetDatabase.GUIDToAssetPath(cg));
                if (dirCache != null) dirCache[dir] = clipFiles;
            }
            foreach (string cp in clipFiles)
            {
                string file = System.IO.Path.GetFileNameWithoutExtension(cp);
                if (cp != path && (file.StartsWith(stem + "@") || file.StartsWith(stem + "_"))) sources.Add(AssetDatabase.LoadAssetAtPath<Object>(cp));
            }
            return AnimalPrefabFactory.ClipsIn(sources);
        }

        /// <summary>Models and prefabs outside WildLens's own folder with a skinned body and at least one clip.</summary>
        private static List<Model> FindAnimatedModels()
        {
            var list = new List<Model>();
            var seen = new HashSet<string>();
            var dirCache = new Dictionary<string, List<string>>();
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if ((path.StartsWith(GeneratedAssets.Root + "/") && !path.StartsWith(ArtLibrary.AnimalsFolder + "/")) || !seen.Add(path)) continue;
                if (path.Contains("@")) continue; // "Deer@Walk.fbx": an animation file, used as a clip source below
                try
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null || go.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) continue;
                    var clips = CollectClips(path, go, dirCache);
                    if (clips.Count == 0) continue;
                    list.Add(new Model { asset = go, path = path, clips = clips, clipCount = AnimalPrefabFactory.Classify(clips).Count });
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[WildLens] Skipped {path}: {e.Message}");
                }
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
        // A pack that carries scripts triggers a domain reload right after it imports, which throws away a queued delayCall and
        // every static field. "An import is pending" therefore lives in SessionState (it survives the reload) and is picked up
        // again by an [InitializeOnLoadMethod] once the scripts have recompiled (1.7.1).
        private const string PendingKey = "WildLens.WildlifeImportPending";
        private static bool _queued;

        private static bool Pending
        {
            get { try { return SessionState.GetBool(PendingKey, false); } catch (System.Exception) { return false; } }
            set { try { SessionState.SetBool(PendingKey, value); } catch (System.Exception) { } }
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Application.isPlaying || Application.isBatchMode) return;
            foreach (var path in imported)
            {
                if (path.StartsWith(GeneratedAssets.Root + "/")) continue;
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".fbx" && ext != ".prefab" && ext != ".blend" && ext != ".gltf" && ext != ".glb") continue;
                if (!WildlifePackSetup.LooksLikeAnimalImport(path)) continue; // a tree or rock pack is not an animal pack
                Pending = true;
                Queue();
                return;
            }
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode && Pending) Queue(); };
            if (Pending && !EditorApplication.isPlayingOrWillChangePlaymode) Queue();
        }

        private static void Queue()
        {
            if (_queued) return;
            _queued = true;
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            _queued = false;
            if (!Pending) return;
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return; // picked up again on EnteredEditMode
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Queue(); return; } // a reload may still come: Pending survives it
            Pending = false; // before the work, so a failure cannot loop
            var settings = AssetDatabase.LoadAssetAtPath<SliceBuildSettings>(WildLensSliceBuilder.SettingsPath);
            if (settings != null && !settings.autoWildlifeFromPacks) return;
            var db = AssetDatabase.LoadAssetAtPath<SpeciesDatabase>(WildLensSliceBuilder.DatabasePath);
            if (db == null) return; // no database yet: the first build will do it
            try { WildlifePackSetup.AutoSetUp(db); }
            catch (System.Exception e) { Debug.LogWarning("[WildLens] Wildlife set-up after import failed: " + e.Message); }
        }
    }
}
