using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using WildLens.Data;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Macro Subjects From Packs (1.7): finds butterflies, moths, dragonflies, bees, beetles, mantises, frogs, newts, snails,
    /// fireflies, mushrooms and daisies among the models and prefabs the user has imported, and assigns the best match to the
    /// field notebook's subjects that have no model yet. The small-life counterpart of <see cref="WildlifePackSetup"/>, with
    /// two differences: static meshes are welcome (a mushroom is not animated), and nothing is sized here (the builder scales
    /// each model to the subject's size). Nothing from a pack is copied: the subject asset just points at the user's own prefab.
    /// A subject that already has a model is never touched.
    /// </summary>
    public static class MacroPackSetup
    {
        // <pure-begin>
        private enum Kind { None, StandIn, Exact }

        /// <summary>Names that are the subject, then names of things that read like it at macro distance.</summary>
        public static readonly Dictionary<string, (string[] exact, string[] standIn)> Names = new Dictionary<string, (string[], string[])>
        {
            { "monarch_butterfly", (new[] { "monarch", "butterfly" }, new string[0]) },
            { "luna_moth", (new[] { "luna", "moth" }, new string[0]) },
            { "common_whitetail", (new[] { "dragonfly", "damselfly" }, new string[0]) },
            { "bumble_bee", (new[] { "bumblebee", "bee" }, new[] { "wasp" }) },
            { "ladybird_beetle", (new[] { "ladybug", "ladybird", "ladybeetle" }, new[] { "beetle", "bug" }) },
            { "praying_mantis", (new[] { "mantis" }, new[] { "grasshopper", "cricket", "stickinsect" }) },
            { "northern_leopard_frog", (new[] { "frog" }, new[] { "toad" }) },
            { "red_spotted_newt", (new[] { "newt", "salamander", "eft" }, new[] { "lizard", "gecko" }) },
            { "woodland_snail", (new[] { "snail" }, new[] { "slug" }) },
            { "firefly", (new[] { "firefly", "lightningbug" }, new string[0]) },
            { "woodland_mushroom", (new[] { "mushroom", "toadstool", "fungus", "fungi", "shroom" }, new string[0]) },
            { "oxeye_daisy", (new[] { "daisy", "oxeye" }, new[] { "flower", "wildflower" }) },
        };

        /// <summary>
        /// Words that rule a model out for EVERY subject (a bee hive, a potted daisy, a lamp, a toy, a particle effect).
        /// Compared with whole words only (and the plural), so "Spotted_Daisy" is fine and "Flower_Pot" is not.
        /// </summary>
        private static readonly string[] NeverSmallLife =
        {
            "hive", "house", "hut", "home", "box", "nest", "lamp", "light", "lantern", "statue", "toy", "pot", "vase", "bouquet",
            "bed", "bush", "net", "fish", "soup", "sign", "icon", "swarm", "particle", "particles", "vfx", "fx", "ui", "sprite",
            "decal", "card", "tile", "pattern", "print", "cloud", "tree", "costume", "hat", "mask",
        };

        /// <summary>Whole-word match of a name against the shared exclusion list.</summary>
        private static bool IsExcluded(List<string> words)
        {
            foreach (var bad in NeverSmallLife)
                foreach (var w in words)
                    if (w == bad || w == bad + "s") return true;
            return false;
        }

        /// <summary>
        /// What a model's file name (and, as a last resort, its folder's name) says about a subject. The folder can name a
        /// stand-in at most, never the real thing.
        /// </summary>
        private static Kind MatchName(string stem, string dir, string subjectId)
        {
            if (!Names.TryGetValue(subjectId, out var n)) return Kind.None;
            var words = Words(stem);
            if (IsExcluded(words)) return Kind.None;
            if (Has(words, n.exact)) return Kind.Exact;
            if (Has(words, n.standIn)) return Kind.StandIn;
            if (!string.IsNullOrEmpty(dir))
            {
                var dirWords = Words(dir);
                if (Has(dirWords, n.exact) || Has(dirWords, n.standIn)) return Kind.StandIn;
            }
            return Kind.None;
        }

        // Copied from WildlifePackSetup (that file is not shared on purpose).

        private static List<string> Words(string name)
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
            string all = string.Concat(words);
            if (words.Count > 1) words.Add(all); // the squashed name, so "Dragon_Fly" also reads as "dragonfly"
            return words;
        }

        private static bool Has(List<string> words, string[] names)
        {
            foreach (var k in names)
                foreach (var w in words)
                    if (k.Length <= 4 ? (w == k || w == k + "s") : w.Contains(k)) return true;
            return false;
        }
        // <pure-end>

        private const float MaxExtent = 3f;

        private class Candidate
        {
            public GameObject asset;
            public string path;
            public bool isPrefab, animated;
            public string stem, dir;
        }

        [MenuItem("WildLens/Macro Subjects From Packs", priority = 13)]
        public static void Menu()
        {
            if (Application.isPlaying) { Debug.Log("[WildLens] Leave Play mode first."); return; }
            int n = Run(true);
            if (n == 0) Debug.Log("[WildLens] Small life: nothing new to set up. Subjects that already have a model are left alone; clear a subject's Prefab to let it pick again.");
            var folder = AssetDatabase.LoadAssetAtPath<Object>(VegetationBuilder.SubjectsFolder);
            if (folder != null) { Selection.activeObject = folder; EditorGUIUtility.PingObject(folder); }
        }

        /// <summary>
        /// The hands-off route, run by every rebuild: each subject without a model gets its best match (never overwriting a
        /// choice already made). Returns how many were set up.
        /// </summary>
        public static int AutoSetUp() => Application.isPlaying ? 0 : Run(false);

        private static int Run(bool menu)
        {
            // The automatic path creates nothing until there is something to assign: with no packs, a project gets exactly
            // the behaviour it had before 1.7 (no Subjects folder from here; the build creates it only if it places plants).
            List<string> paths;
            Dictionary<string, NaturalistSubject> subjects;
            if (!menu && !AssetDatabase.IsValidFolder(VegetationBuilder.SubjectsFolder))
            {
                paths = NamedPaths(new List<string>(Names.Keys));
                if (paths.Count == 0) return 0;
                subjects = VegetationBuilder.EnsureSubjectAssets();
            }
            else
            {
                subjects = VegetationBuilder.EnsureSubjectAssets();
                paths = null;
            }
            var wanted = new List<NaturalistSubject>();
            foreach (var kv in subjects)
                if (kv.Value != null && kv.Value.prefab == null && Names.ContainsKey(kv.Key)) wanted.Add(kv.Value);
            if (wanted.Count == 0) return 0;
            wanted.Sort((a, b) => string.CompareOrdinal(a.subjectId, b.subjectId));

            if (paths == null)
            {
                var ids = new List<string>();
                foreach (var w in wanted) ids.Add(w.subjectId);
                paths = NamedPaths(ids);
            }
            if (paths.Count == 0) return 0;
            var candidates = LoadCandidates(paths);
            if (candidates.Count == 0) return 0;
            var sb = new StringBuilder();
            int done = 0;
            foreach (var subject in wanted)
            {
                Candidate best = null;
                int bestRank = -1;
                bool bestExact = false;
                foreach (var c in candidates)
                {
                    var kind = MatchName(c.stem, c.dir, subject.subjectId);
                    if (kind == Kind.None) continue;
                    // Exact before stand-in; then animated before static; then prefab before raw model; then path order
                    // (the list is already sorted, and only a strictly better rank replaces the current best).
                    int rank = (kind == Kind.Exact ? 4 : 0) + (c.animated ? 2 : 0) + (c.isPrefab ? 1 : 0);
                    if (rank > bestRank) { best = c; bestRank = rank; bestExact = kind == Kind.Exact; }
                }
                if (best == null) { sb.Append("\n  • ").Append(subject.displayName).Append(": no model found: stays out of the reserve"); continue; }
                subject.prefab = best.asset;
                EditorUtility.SetDirty(subject);
                sb.Append("\n  • ").Append(subject.displayName).Append(": ").Append(best.asset.name)
                  .Append(bestExact ? "" : " (stand-in)").Append("  ").Append(best.path);
                done++;
            }
            if (done > 0) AssetDatabase.SaveAssets();
            if (done > 0 || menu)
                Debug.Log($"[WildLens] Small life: {done} field-notebook subjects now use models from your packs." + sb +
                          (done > 0 ? "\nRebuild the slice to place them. Clear a subject's Prefab (Assets/WildLens/Data/Subjects) to take one out." : ""));
            return done;
        }

        /// <summary>
        /// Paths (outside WildLens's own folder, in path order) whose file or folder NAME could match one of the given
        /// subjects. Nothing is loaded here: a project with thousands of models costs one listing, not thousands of loads.
        /// </summary>
        private static List<string> NamedPaths(List<string> subjectIds)
        {
            var result = new List<string>();
            if (subjectIds == null || subjectIds.Count == 0) return result;
            var seen = new HashSet<string>();
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(string.CompareOrdinal); // deterministic whatever order the database answers in
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path) || path.StartsWith(GeneratedAssets.Root + "/") || path.Contains("/Editor/") || path.Contains("@")) continue;
                if (!seen.Add(path)) continue;
                string stem = System.IO.Path.GetFileNameWithoutExtension(path);
                if (IsLodOrCollider(stem)) continue;
                string dir = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? "");
                foreach (var id in subjectIds)
                    if (MatchName(stem, dir, id) != Kind.None) { result.Add(path); break; }
            }
            return result;
        }

        /// <summary>Loads only the name-matched paths and keeps those that are small and really have a mesh.</summary>
        private static List<Candidate> LoadCandidates(List<string> paths)
        {
            var list = new List<Candidate>();
            foreach (string path in paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                if (!HasMesh(go)) continue; // an empty prefab, a rig with no body: nothing to look at
                if (Extent(go) > MaxExtent) continue; // a tree, a building: not small life
                string stem = System.IO.Path.GetFileNameWithoutExtension(path);
                string dir = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? "");
                list.Add(new Candidate { asset = go, path = path, isPrefab = path.EndsWith(".prefab"), animated = IsAnimated(go), stem = stem, dir = dir });
            }
            return list;
        }

        private static bool HasMesh(GameObject go)
        {
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != null && smr.sharedMesh != null) return true;
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) return true;
            }
            return false;
        }

        private static bool IsAnimated(GameObject go)
        {
            if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) return true;
            var animator = go.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Length > 0) return true;
            var legacy = go.GetComponentInChildren<Animation>(true);
            return legacy != null && legacy.GetClipCount() > 0;
        }

        private static bool IsLodOrCollider(string stem)
        {
            string s = stem.ToLowerInvariant();
            if (s.Contains("collider") || s.Contains("collision")) return true;
            for (int i = 1; i <= 9; i++) if (s.EndsWith("lod" + i) || s.Contains("lod" + i + "_") || s.Contains("_lod" + i)) return true;
            return false;
        }

        /// <summary>Largest dimension of the asset's renderers (0 when it has none).</summary>
        private static float Extent(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return 0f;
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        }
    }
}
