using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WildLens.Data;

namespace WildLens.EditorTools
{
    /// <summary>What an animation clip is for, judged from its name.</summary>
    public enum ClipRole { Idle, Walk, Trot, Run, Eat, Look, Sit, Lie, Groom, Fly, Glide, Land, TakeOff }

    /// <summary>
    /// Turns an animated model into a WildLens species prefab (1.6: shared by <see cref="AnimalModelSetup"/>, one species
    /// at a time, and <see cref="WildlifePackSetup"/>, a whole pack at once). Classifies clips by name, builds an Animator
    /// Controller driven by the parameters <see cref="Wildlife.AnimalAnimatorBridge"/> sets (Speed, Activity, GaitPhase),
    /// wraps the model scaled to the species' real size with its feet on the origin and root motion off, optionally tints
    /// it to the species' colours (one songbird model can be a cardinal, a blue jay and a blackbird), and assigns it.
    /// The AI, awareness, photography and journal need nothing else.
    /// </summary>
    public static class AnimalPrefabFactory
    {
        public const string Folder = GeneratedAssets.Root + "/Animals";

        // <pure-begin> (name logic only: no Unity types; the throwaway test extracts everything between these markers)

        // First matching role wins; checked in this order so "walk_eat" style names classify sensibly. Keywords match
        // WHOLE WORDS of the clip name (split on non-letters and camelCase), optionally with a common suffix ("Running",
        // "Sitting", "Walks"), so "Threat" is not Eat, "Forest" is not Lie, "Trunk" is not Run, "Island" is not Land.
        // Entries listed in Compound are matched against the squashed name instead ("Take Off", "TakeOff", "take_off").
        // WITHIN a role, an earlier keyword outranks a later one (1.7.1): "Bear_WalkForward" (walk) beats "Bear_Jump" (jump).
        private static readonly (ClipRole role, string[] words)[] Keywords =
        {
            (ClipRole.TakeOff, new[] { "takeoff", "liftoff" }),
            (ClipRole.Land, new[] { "landing", "land" }),
            (ClipRole.Glide, new[] { "glide", "gliding", "soar" }),
            (ClipRole.Fly, new[] { "fly", "flies", "flap", "flight", "hover", "wing" }),
            (ClipRole.Eat, new[] { "eat", "graze", "grazing", "feed", "drink", "sniff", "forage", "foraging", "peck", "chew", "nibble" }),
            (ClipRole.Lie, new[] { "lie", "lying", "laying", "laydown", "rest", "sleep", "nap" }),
            (ClipRole.Sit, new[] { "sit" }),
            (ClipRole.Groom, new[] { "groom", "lick", "scratch", "shake", "shaking", "preen", "ruffle" }),
            // Singing is only ever the last choice for Look (a perched bird looking around is better than one that sings forever).
            (ClipRole.Look, new[] { "look", "alert", "listen", "attention", "turnhead", "headturn", "watch", "search", "worried", "sing", "call", "chirp", "tweet", "song" }),
            (ClipRole.Run, new[] { "run", "gallop", "sprint", "flee", "dash", "chase" }),
            (ClipRole.Trot, new[] { "trot", "canter", "jog" }),
            (ClipRole.Walk, new[] { "walk", "hop", "jump", "move", "crawl", "amble" }),
            (ClipRole.Idle, new[] { "idle", "stand", "breath", "perch", "stay", "wait" }),
        };

        private static readonly HashSet<string> Compound = new HashSet<string> { "takeoff", "liftoff", "laydown", "turnhead", "headturn" };

        // A clip with one of these words is never mapped (it would be a death, attack or hit-reaction pose in an idle slot).
        private static readonly string[] Excluded =
        {
            "death", "dead", "die", "dying", "attack", "attacking", "hit", "hurt", "damage", "roar", "bite", "biting", "kill",
            "stun", "stunned", "buff", "gethit", "knockback", "dodge", "block", "taunt", "spawn", "despawn",
        };

        // A clip with one of these words loses to a plain one of the same role (flyStraight over flyLeft, Idle over IdleCombat).
        private static readonly string[] Disfavoured =
        {
            "left", "right", "back", "backward", "backwards", "reverse", "strafe", "turn",
            "combat", "battle", "aggressive", "angry", "rare", "alt", "variant",
        };

        // Straight-ahead clips: a tiny bonus among otherwise equal ones.
        private static readonly string[] Forward = { "forward", "straight", "fwd" };

        private static readonly HashSet<string> Suffixes = new HashSet<string>
            { "s", "es", "d", "ed", "er", "ers", "ing", "ning", "ting", "ping", "ming", "ging", "ned", "ted", "ped", "loop", "cycle", "anim" };

        /// <summary>Lower-case words of a name: split on non-letters (digits too) and camel case ("Armature|Walk_01" → armature, walk).</summary>
        internal static List<string> Tokens(string name)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(name)) return words;
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
            return words;
        }

        private static bool TokenIs(string token, string key) =>
            token == key || (token.Length > key.Length && token.StartsWith(key, System.StringComparison.Ordinal) && Suffixes.Contains(token.Substring(key.Length)));

        // Transition and pose clips ("Jump_ToIdle", "Rest_Pose", "T-Pose") are bridges between states, not a state: never mapped.
        private static readonly string[] NotStates = { "to", "transition", "pose", "tpose", "bind", "from" };

        // "Wings_Folded" is a bird at rest, not a bird in flight.
        private static readonly string[] WingsAway = { "folded", "fold", "closed" };

        private static bool AnyToken(List<string> tokens, string[] keys)
        {
            foreach (var t in tokens)
                foreach (var k in keys)
                    if (t == k) return true;
            return false;
        }

        private static int CountTokens(List<string> tokens, string[] keys)
        {
            int n = 0;
            foreach (var t in tokens)
                foreach (var k in keys)
                    if (t == k) { n++; break; }
            return n;
        }

        private static bool AnyWord(List<string> tokens, string[] keys)
        {
            foreach (var t in tokens)
                foreach (var k in keys)
                    if (TokenIs(t, k)) return true;
            return false;
        }

        private static bool HeadDown(string squashed) =>
            squashed.Contains("headlow") || squashed.Contains("headdown");

        /// <summary>Whether a keyword of a role hits the name's words (a "wing" in a folded-wings name does not).</summary>
        private static bool KeywordHit(string w, List<string> tokens, string squashed, bool wingsAway)
        {
            if (w == "wing" && wingsAway) return false;
            if (Compound.Contains(w)) return squashed.Contains(w);
            foreach (var t in tokens) if (TokenIs(t, w)) return true;
            return false;
        }

        /// <summary>The role a clip name stands for, or false (unrecognised, a death/attack/hit clip, or a transition/pose clip).</summary>
        internal static bool TryClassifyName(string clipName, out ClipRole role)
        {
            role = ClipRole.Idle;
            var tokens = Tokens(clipName);
            if (tokens.Count == 0) return false;
            string squashed = string.Concat(tokens);
            if (AnyToken(tokens, NotStates)) return false;
            foreach (var t in tokens)
                foreach (var x in Excluded)
                    if (TokenIs(t, x)) return false;
            // "Idle_HeadLow" is an animal with its head down: grazing, not standing.
            if (HeadDown(squashed) && !AnyWord(tokens, EatWords()) && AnyWord(tokens, new[] { "idle", "stand" })) { role = ClipRole.Eat; return true; }
            bool wingsAway = AnyToken(tokens, WingsAway);
            foreach (var (r, words) in Keywords)
                foreach (var w in words)
                    if (KeywordHit(w, tokens, squashed, wingsAway)) { role = r; return true; }
            return false;
        }

        /// <summary>True for a clip that must never be used as anything, not even a fallback (death, attack, hit reaction, stun, T-pose, transition).</summary>
        internal static bool IsIgnoredName(string clipName)
        {
            var tokens = Tokens(clipName);
            if (tokens.Count == 0) return true;
            if (AnyToken(tokens, NotStates)) return true;
            foreach (var t in tokens)
                foreach (var x in Excluded)
                    if (TokenIs(t, x)) return true;
            return false;
        }

        private static string[] EatWords()
        {
            foreach (var (r, words) in Keywords) if (r == ClipRole.Eat) return words;
            return new string[0];
        }

        /// <summary>Position in the role's keyword list of the earliest keyword the name hits (99 when none: the head-down Idle special case).</summary>
        private static int KeywordRank(List<string> tokens, ClipRole role)
        {
            string squashed = string.Concat(tokens);
            bool wingsAway = AnyToken(tokens, WingsAway);
            foreach (var (r, words) in Keywords)
            {
                if (r != role) continue;
                for (int i = 0; i < words.Length; i++)
                    if (KeywordHit(words[i], tokens, squashed, wingsAway)) return i;
            }
            return 99;
        }

        /// <summary>
        /// Lower is better, compared in this order: fewer disfavoured words (left/right/back/combat/rare…), then an earlier
        /// keyword of the role ("walk" before "jump"), then fewer words ("Idle" over "Idle_2_Look_Around"; a straight-ahead
        /// word counts as one less); an Idle with its head down is a poor Idle.
        /// </summary>
        internal static int ScoreName(string clipName, ClipRole role)
        {
            var tokens = Tokens(clipName);
            int bad = System.Math.Min(9, CountTokens(tokens, Disfavoured));
            int kw = System.Math.Min(99, KeywordRank(tokens, role));
            int plain = tokens.Count - (AnyToken(tokens, Forward) ? 1 : 0);
            if (role == ClipRole.Idle && HeadDown(string.Concat(tokens))) plain += 100;
            plain = System.Math.Max(0, System.Math.Min(plain, 999));
            return bad * 100000 + kw * 1000 + plain;
        }

        /// <summary>
        /// For each role, the index of the best clip name (lowest <see cref="ScoreName"/>; a tie keeps the earlier one).
        /// Unrecognised names are left out. No fallback for a missing Idle here (see <see cref="IdleFallbackIndex"/>).
        /// </summary>
        internal static Dictionary<ClipRole, int> ClassifyNames(IList<string> names)
        {
            var best = new Dictionary<ClipRole, int>();
            var bestScore = new Dictionary<ClipRole, int>();
            for (int i = 0; i < names.Count; i++)
            {
                if (!TryClassifyName(names[i], out var role)) continue;
                int score = ScoreName(names[i], role);
                if (!best.ContainsKey(role) || score < bestScore[role]) { best[role] = i; bestScore[role] = score; }
            }
            return best;
        }

        /// <summary>
        /// The clip to stand in for a missing Idle: the Look clip (a watching bird, not one that pecks for ever), else the first clip
        /// that is not ignored, else -1. When there is an Idle, its own index.
        /// </summary>
        internal static int IdleFallbackIndex(IList<string> names, Dictionary<ClipRole, int> best)
        {
            if (best.TryGetValue(ClipRole.Idle, out int i)) return i;
            if (best.TryGetValue(ClipRole.Look, out i)) return i;
            for (int k = 0; k < names.Count; k++) if (!IsIgnoredName(names[k])) return k;
            return -1;
        }

        // ---- pipeline names (shared with the Asset Audit)

        /// <summary>Why a shader will not render properly in URP, from its name alone (built-in pipeline shaders), or null. The one test the audit and the factory share.</summary>
        internal static string BuiltinShaderIssue(string shader)
        {
            if (string.IsNullOrEmpty(shader) || shader == "(missing shader)" || shader == "Hidden/InternalErrorShader") return "missing shader";
            if (shader == "(no material)") return null;
            if (shader.Contains("Universal Render Pipeline") || shader.StartsWith("Shader Graphs/") || shader.StartsWith("Hidden/Universal")) return null;
            if (shader == "Standard" || shader.StartsWith("Standard (") || shader.StartsWith("Legacy Shaders/") || shader.StartsWith("Mobile/") ||
                shader.StartsWith("Nature/") || shader == "Autodesk Interactive")
                return "built-in shader (pink in URP)";
            if (shader.StartsWith("Unlit/")) return "built-in unlit shader (flat, not lit by URP)";
            return null;
        }

        internal enum AlphaKind { Opaque, Cutout, Transparent }

        /// <summary>Opaque, cutout (alpha clip) or transparent, from a built-in shader's name, its _Mode (-1 when none) and its _ALPHATEST_ON keyword.</summary>
        internal static AlphaKind AlphaKindOf(string shader, float mode, bool alphaTestKeyword)
        {
            string s = shader ?? "";
            bool cutout = s.IndexOf("Cutout", System.StringComparison.OrdinalIgnoreCase) >= 0 || (mode > 0.5f && mode < 1.5f) || alphaTestKeyword;
            if (cutout) return AlphaKind.Cutout;
            bool transparent = s.IndexOf("Transparent", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               s.IndexOf("Fade", System.StringComparison.OrdinalIgnoreCase) >= 0 || (mode > 1.5f && mode < 3.5f);
            return transparent ? AlphaKind.Transparent : AlphaKind.Opaque;
        }

        /// <summary>Feathers and leaves are cutouts drawn on both sides; a shader that says so (or is a cutout) gets no back-face culling.</summary>
        internal static bool TwoSidedByName(string shader)
        {
            string s = (shader ?? "").Replace(" ", "").Replace("-", "").Replace("_", "");
            return s.IndexOf("Cutout", System.StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("Double", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   s.IndexOf("TwoSided", System.StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("2Sided", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---- real sizes (1.8)

        /// <summary>
        /// The real size in metres of each species a pack model can stand for, by species id. Birds: beak to tail along the body
        /// (<see cref="IsBird"/> measures a model's +Z length); mammals: the larger of nose-to-tail length and height. A species that
        /// is not listed (the five originals, the hawk) keeps its constant in <see cref="DefaultSize"/>. The heron is measured by
        /// its standing HEIGHT instead (see <see cref="MeasuredByHeight"/>): a heron is tall, not long, so its length would make a giant.
        /// Songbirds are deliberately not inflated: their photoSizeScale makes up for how small they are in the frame.
        /// </summary>
        private static readonly Dictionary<string, float> RealSize = new Dictionary<string, float>
        {
            { "northern_cardinal", 0.22f }, { "blue_jay", 0.28f }, { "red_winged_blackbird", 0.21f }, { "belted_kingfisher", 0.32f },
            { "eastern_phoebe", 0.17f }, { "american_crow", 0.45f }, { "american_robin", 0.25f }, { "black_capped_chickadee", 0.13f },
            { "american_goldfinch", 0.12f }, { "song_sparrow", 0.15f }, { "barred_owl", 0.50f }, { "wild_turkey", 1.0f },
            { "great_blue_heron", 1.2f }, { "spotted_sandpiper", 0.19f },
            { "eastern_bluebird", 0.18f }, { "tufted_titmouse", 0.16f }, { "louisiana_waterthrush", 0.15f }, // 1.9
            { "american_black_bear", 1.6f }, { "coyote", 1.25f }, { "raccoon", 0.80f }, { "virginia_opossum", 0.75f }, { "groundhog", 0.60f },
        };

        /// <summary>Species whose size is the model's standing height (Y), not its length.</summary>
        private static readonly HashSet<string> MeasuredByHeight = new HashSet<string> { "great_blue_heron" };

        /// <summary>The listed real size of a species, or false (the original five, the hawk, unknown ids).</summary>
        internal static bool TryRealSize(string speciesId, out float size)
        {
            size = 0f;
            return !string.IsNullOrEmpty(speciesId) && RealSize.TryGetValue(speciesId, out size);
        }

        internal static bool IsMeasuredByHeight(string speciesId) => !string.IsNullOrEmpty(speciesId) && MeasuredByHeight.Contains(speciesId);

        /// <summary>Whether a file name is one of the URP copies <c>{id}_{nn}_{material}_URP.mat</c> that material conversion makes for this species.</summary>
        internal static bool IsConversionCopyName(string fileName, string id)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(id)) return false;
            int n = id.Length;
            return fileName.Length > n + 8 && fileName.StartsWith(id + "_", System.StringComparison.Ordinal) &&
                   char.IsDigit(fileName[n + 1]) && char.IsDigit(fileName[n + 2]) && fileName[n + 3] == '_' &&
                   fileName.EndsWith("_URP.mat", System.StringComparison.Ordinal);
        }

        /// <summary>A name made only of letters, digits and underscores (at most 40 characters), "mat" when nothing is left.</summary>
        internal static string SafeName(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "") { if (sb.Length >= 40) break; sb.Append(char.IsLetterOrDigit(c) ? c : '_'); }
            return sb.Length == 0 ? "mat" : sb.ToString();
        }

        /// <summary>
        /// The file name of the converted copy of one source material: <c>{prefix}_{nn}_{material}_URP.mat</c>. The prefix is the species id
        /// (or a small-life subject and variant), the index the order in which this build met the source material, so every species keeps
        /// its own files and two materials with the same name never share one.
        /// </summary>
        internal static string ConversionCopyName(string prefix, int index, string materialName) =>
            $"{prefix}_{System.Math.Max(0, System.Math.Min(index, 99)):00}_{SafeName(materialName)}_URP.mat";

        /// <summary>The file name of the plain URP material that stands in for an empty or undrawable slot: <c>{prefix}_Fallback{rr}_{ss}_URP.mat</c>.</summary>
        internal static string FallbackName(string prefix, int rendererIndex, int slot) =>
            $"{prefix}_Fallback{System.Math.Max(0, System.Math.Min(rendererIndex, 99)):00}_{System.Math.Max(0, System.Math.Min(slot, 99)):00}_URP.mat";

        /// <summary>
        /// Whether URP draws a shader pass with this LightMode tag in its colour passes. Untagged passes are drawn (as SRPDefaultUnlit), and so
        /// are UniversalForward, UniversalForwardOnly, UniversalGBuffer and the old LightweightForward. Built-in pipeline modes (ForwardBase,
        /// ForwardAdd, Deferred, PrepassBase, PrepassFinal, Vertex, VertexLM, VertexLMRGBM, Always) are drawn by URP with its magenta error
        /// shader, and helper passes (ShadowCaster, DepthOnly, Meta, MotionVectors, Universal2D…) never colour a 3D object.
        /// </summary>
        internal static bool UrpDrawsLightMode(string lightMode)
        {
            if (string.IsNullOrEmpty(lightMode)) return true;
            switch (lightMode)
            {
                case "SRPDefaultUnlit":
                case "UniversalForward":
                case "UniversalForwardOnly":
                case "UniversalGBuffer":
                case "LightweightForward":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Whether a shader whose passes carry these LightMode tags ("" = untagged) has at least one pass URP draws. No passes: false.</summary>
        internal static bool UrpDrawsAnyPass(IList<string> lightModes)
        {
            if (lightModes == null || lightModes.Count == 0) return false;
            foreach (var m in lightModes) if (UrpDrawsLightMode(m)) return true;
            return false;
        }

        /// <summary>
        /// One line on a model's materials for the Console, e.g. "materials: 2 (Universal Render Pipeline/Lit ×2)", from the shader name of every
        /// material slot ("(no material)" for an empty one) and the problems still left (an empty list: none).
        /// </summary>
        internal static string MaterialSummary(IList<string> slotShaders, IList<string> problems)
        {
            var counts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            int n = 0;
            if (slotShaders != null)
                foreach (var s in slotShaders) { string k = string.IsNullOrEmpty(s) ? "(missing shader)" : s; counts.TryGetValue(k, out int c); counts[k] = c + 1; n++; }
            var parts = new List<string>();
            foreach (var kv in counts) parts.Add(kv.Value > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key);
            string line = $"materials: {n}" + (parts.Count > 0 ? " (" + string.Join(", ", parts) + ")" : "");
            if (problems != null && problems.Count > 0) line += "; STILL NOT DRAWABLE IN URP: " + string.Join("; ", problems);
            return line;
        }

        // <pure-end>

        /// <summary>Bump when the factory's output improves enough that prefabs it made before should be rebuilt (1.8: real sizes, looping clips, pack scripts stripped, materials converted; 3: tinted copies bake the species colours, so changed colours need a rebuild; 4: every material slot is checked for what URP can really draw (a
        /// shader whose passes are all built-in-only, a broken shader, an empty slot) and repaired, materials are updated in place instead of deleted and
        /// recreated, pack animation events are silenced, and lighter tints on textured stand-ins).</summary>
        internal const int FactoryVersion = 4;

        /// <summary>Every clip in the given assets (models, animation FBXs, clips, controllers).</summary>
        public static List<AnimationClip> ClipsIn(IEnumerable<Object> sources)
        {
            var list = new List<AnimationClip>();
            foreach (var src in sources)
            {
                if (src == null) continue;
                if (src is AnimationClip direct) { if (!list.Contains(direct)) list.Add(direct); continue; }
                string path = AssetDatabase.GetAssetPath(src);
                if (string.IsNullOrEmpty(path)) continue;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is AnimationClip clip && !clip.name.StartsWith("__preview__") && !list.Contains(clip)) list.Add(clip);
                if (src is GameObject go)
                {
                    // A prefab: the clips of its Animator's controller and of the model its skinned mesh comes from.
                    var animator = go.GetComponentInChildren<Animator>(true);
                    if (animator != null && animator.runtimeAnimatorController != null)
                        foreach (var clip in animator.runtimeAnimatorController.animationClips)
                            if (clip != null && !list.Contains(clip)) list.Add(clip);
                    foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        string meshPath = smr.sharedMesh != null ? AssetDatabase.GetAssetPath(smr.sharedMesh) : null;
                        if (string.IsNullOrEmpty(meshPath) || meshPath == path) continue;
                        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(meshPath))
                            if (a is AnimationClip clip && !clip.name.StartsWith("__preview__") && !list.Contains(clip)) list.Add(clip);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Clips classified by name into roles (the best-named clip per role, see <see cref="ClassifyNames"/>);
        /// <paramref name="log"/> gets one line per recognised clip.
        /// </summary>
        public static Dictionary<ClipRole, AnimationClip> Classify(IEnumerable<AnimationClip> clips, StringBuilder log = null)
        {
            var result = new Dictionary<ClipRole, AnimationClip>();
            var list = new List<AnimationClip>();
            foreach (var clip in clips) if (clip != null) list.Add(clip);
            var names = new List<string>();
            foreach (var clip in list) names.Add(clip.name);
            var best = ClassifyNames(names);
            foreach (var kv in best) result[kv.Key] = list[kv.Value];
            if (log != null)
                for (int i = 0; i < list.Count; i++)
                    if (TryClassifyName(names[i], out var role)) log.Append(names[i]).Append(" → ").Append(role).Append('\n');
            // No Idle: the Look clip, else the first clip that is not a death/attack/T-pose (never "whatever came first": that was a peck).
            if (!result.ContainsKey(ClipRole.Idle))
            {
                int idle = IdleFallbackIndex(names, best);
                if (idle >= 0) result[ClipRole.Idle] = list[idle];
            }
            return result;
        }

        /// <summary>
        /// The species' real size (the larger of nose-to-tail-tip length and height), used to scale a model: from the
        /// <see cref="RealSize"/> table (1.8), else the original five and the hawk from field measurements, else the body plan.
        /// </summary>
        public static float DefaultSize(SpeciesDefinition s)
        {
            if (s == null) return 1f;
            if (TryRealSize(s.speciesId, out float real)) return real;
            switch (s.placeholderBody)
            {
                case PlaceholderBody.Deer: return 1.9f;
                case PlaceholderBody.Fox: return 1.0f;      // nose to tail tip
                case PlaceholderBody.Rabbit: return 0.42f;
                case PlaceholderBody.Squirrel: return 0.45f; // with tail
                case PlaceholderBody.Hawk: return 0.55f;    // beak to tail (birds are scaled by length, see IsBird)
            }
            var b = s.bodyPlan;
            if (b == null) return 1f;
            float length = b.length + b.neckLength * 0.5f + b.headSize + b.snoutLength + b.tailLength * 0.8f;
            float height = b.height + b.legLength + b.neckLength + b.headSize;
            return Mathf.Max(0.12f, Mathf.Max(length, height));
        }

        /// <summary>
        /// Birds are scaled by body length (beak to tail, along +Z), not by their largest side: many bird models rest with
        /// their wings spread, which would shrink them to a sparrow.
        /// </summary>
        public static bool IsBird(SpeciesDefinition s) =>
            s != null && (s.locomotion == LocomotionType.Flying || s.placeholderBody == PlaceholderBody.Hawk ||
                          (s.placeholderBody == PlaceholderBody.Generic && s.bodyPlan != null && s.bodyPlan.archetype != BodyArchetype.Quadruped));

        /// <summary>Whether a role has a clip (directly or through a fallback).</summary>
        public static AnimationClip Clip(Dictionary<ClipRole, AnimationClip> clips, ClipRole r, params ClipRole[] fallbacks)
        {
            if (clips.TryGetValue(r, out var c) && c != null) return c;
            foreach (var f in fallbacks) if (clips.TryGetValue(f, out c) && c != null) return c;
            return null;
        }

        /// <summary>
        /// Builds the controller and prefab and assigns it to the species. Returns the prefab. <paramref name="manual"/> is true
        /// (the default) when a person chose the size, yaw and tint (the two setup windows): the build is then recorded as theirs and
        /// automatic set-up never rebuilds it. Automatic set-up passes false.
        /// </summary>
        public static GameObject Create(SpeciesDefinition species, GameObject source, Dictionary<ClipRole, AnimationClip> clips,
            float size, float yaw, bool tint, out string message, bool manual = true)
        {
            GeneratedAssets.EnsureFolder(Folder);
            string id = string.IsNullOrEmpty(species.speciesId) ? species.name : species.speciesId;
            var controller = BuildController(species, clips, $"{Folder}/{id}_Animator.controller");
            string prefabPath = $"{Folder}/{id}.prefab";

            // Built in a private preview scene (the open scene is never touched or dirtied), and always cleaned up.
            UnityEngine.SceneManagement.Scene preview = default;
            bool havePreview = false;
            try { preview = EditorSceneManager.NewPreviewScene(); havePreview = preview.IsValid(); }
            catch (System.Exception) { havePreview = false; }

            GameObject root = null, model = null, prefab;
            float scale = 1f;
            int converted = 0, tinted = 0, repaired = 0;
            string conversionNote = "", repairNote = "", materialLine = "";
            Color fallbackColour = species.primaryColor; fallbackColour.a = 1f;
            try
            {
                // Wrapper root (what the spawner instantiates and the AI moves) → yaw-corrected, scaled model.
                root = new GameObject(species.displayName);
                if (havePreview) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                model = havePreview ? (GameObject)PrefabUtility.InstantiatePrefab(source, preview) : (GameObject)PrefabUtility.InstantiatePrefab(source);
                if (model == null) model = Object.Instantiate(source);
                if (havePreview && model.scene != preview) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model, preview);
                // A pack prefab is unpacked completely first: its scripts, colliders and bodies are then removed from OUR copy (the
                // result is a new prefab that no longer depends on the pack's), and the pack's own assets are never modified.
                UnpackCompletely(model);
                model.transform.SetParent(root.transform, false);
                StripPackComponents(model);
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation; // keep the file's own axis fix

                // The idle pose is sampled on the object the Animator sits on (clip paths are relative to it), then the model is measured
                // and grounded as POSED: a bind pose may be a T-pose or a bird in flight.
                var animator = model.GetComponentInChildren<Animator>(true);
                if (animator == null) animator = model.AddComponent<Animator>();
                var idleClip = Clip(clips, ClipRole.Idle, ClipRole.Look);
                if (idleClip != null) idleClip.SampleAnimation(animator.gameObject, 0f);

                var bounds = PosedBounds(model, root.transform);
                float measured = IsMeasuredByHeight(species.speciesId) ? bounds.size.y
                    : IsBird(species) ? bounds.size.z : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (measured > 0.0001f) scale = size / measured;
                else Debug.LogWarning($"[WildLens] {species.displayName}: the model '{source.name}' has no measurable size (no mesh, or its renderers only appear at runtime), so it was left at scale 1. Pick another model or set its scale by hand.");
                model.transform.localScale = model.transform.localScale * scale;
                if (idleClip != null) idleClip.SampleAnimation(animator.gameObject, 0f);
                bounds = PosedBounds(model, root.transform);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; // the AI moves the animal; clips only animate the body
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.fireEvents = false; // pack clips may carry events for scripts we removed (1.10)

                // Materials the active pipeline cannot draw (a built-in shader in URP is pink) are replaced by converted COPIES, then tinted.
                // Every slot URP cannot draw (built-in or built-in-only shader, broken shader, empty slot) gets a converted COPY (1.9.1).
                try { converted = ConvertMaterialsForPipeline(model, id, Folder, fallbackColour, out conversionNote); }
                catch (System.Exception e) { conversionNote = "material conversion failed: " + e.Message; }
                try { tinted = tint ? Tint(model, species, id) : 0; }
                catch (System.Exception e) { conversionNote += (conversionNote.Length > 0 ? "; " : "") + "tint failed: " + e.Message; }
                // Verified after the tint, the last step that touches materials: anything still undrawable is replaced (prefix "_Fix").
                // Conversion copies a tint made unused are left on disk on purpose: an orphan .mat is harmless, deleting one is not (1.9.1).
                try { repaired = ConvertMaterialsForPipeline(model, id + "_Fix", Folder, fallbackColour, out repairNote); }
                catch (System.Exception e) { repairNote = "material check failed: " + e.Message; }
                materialLine = DescribeMaterials(model);

                prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (model != null) Object.DestroyImmediate(model); // only still alive when it never reached the root
                if (havePreview) { try { EditorSceneManager.ClosePreviewScene(preview); } catch (System.Exception) { } }
            }

            Undo.RecordObject(species, "Assign species prefab");
            species.prefab = prefab;
            EditorUtility.SetDirty(species);
            try { RecordBuild(species, source, size, yaw, tint, manual); } catch (System.Exception e) { Debug.LogWarning("[WildLens] Could not record the build of " + species.displayName + ": " + e.Message); }
            AssetDatabase.SaveAssets();
            try { WildlifePackSetup.SetKeepPlaceholder(species.speciesId, false); } catch (System.Exception) { } // a model was set: no longer kept as a placeholder
            message = $"{species.displayName}: {prefabPath} (scale {scale:0.###}" + (converted > 0 ? $", {converted} materials converted for URP" : "") +
                      (tinted > 0 ? $", {tinted} materials tinted" : "") + (repaired > 0 ? $", {repaired} more repaired after the tint" : "") +
                      (string.IsNullOrEmpty(conversionNote) ? "" : "; " + conversionNote) + (string.IsNullOrEmpty(repairNote) ? "" : "; " + repairNote) +
                      ") " + materialLine;
            return prefab;
        }

        /// <summary>
        /// The probe line for a built model (1.9.1), e.g. "materials: 2 (Universal Render Pipeline/Lit ×2)": every material slot's shader and,
        /// under URP, every slot URP still cannot draw. This is what the Console shows for each species, so a screenshot of it settles "why pink".
        /// </summary>
        internal static string DescribeMaterials(GameObject root)
        {
            var shaders = new List<string>();
            var problems = new List<string>();
            if (root == null) return MaterialSummary(shaders, problems);
            bool urp = UrpActive();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                {
                    shaders.Add(m == null ? "(no material)" : m.shader != null ? m.shader.name : "(missing shader)");
                    if (!urp) continue;
                    string issue = UrpDrawIssue(m);
                    if (issue != null) problems.Add($"{r.name}: {(m != null ? m.name : "empty slot")} ({issue})");
                }
            }
            return MaterialSummary(shaders, problems);
        }

        /// <summary>
        /// 1.9.1: repairs, in place, the materials of species prefabs the factory built that URP cannot draw, WITHOUT rebuilding them (so a
        /// model, size, yaw or tint chosen by hand in a setup window stays exactly as it is: only undrawable material slots change). Under any
        /// other pipeline nothing is done. Returns one Console line per prefab repaired (or that could not be).
        /// </summary>
        internal static List<string> RepairBuiltPrefabs(SpeciesDatabase db)
        {
            var lines = new List<string>();
            if (db == null || db.species == null || !UrpActive()) return lines;
            foreach (var sp in db.species)
            {
                if (sp == null || sp.prefab == null) continue;
                string path = AssetDatabase.GetAssetPath(sp.prefab);
                if (string.IsNullOrEmpty(path) || !path.StartsWith(Folder + "/", System.StringComparison.Ordinal)) continue; // the factory's own prefabs only
                bool bad = false;
                foreach (var r in sp.prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    foreach (var m in r.sharedMaterials) if (UrpDrawIssue(m) != null) { bad = true; break; }
                    if (bad) break;
                }
                if (!bad) continue;
                string id = string.IsNullOrEmpty(sp.speciesId) ? sp.name : sp.speciesId;
                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    Color c = sp.primaryColor; c.a = 1f;
                    int n = ConvertMaterialsForPipeline(contents, id + "_Fix", Folder, c, out string note);
                    string line = DescribeMaterials(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    lines.Add($"{sp.displayName}: {n} material slots repaired in {path}" + (string.IsNullOrEmpty(note) ? "" : "; " + note) + "; " + line);
                }
                catch (System.Exception e) { lines.Add($"{sp.displayName}: materials could not be repaired ({e.Message})"); }
                finally { if (contents != null) { try { PrefabUtility.UnloadPrefabContents(contents); } catch (System.Exception) { } } }
            }
            if (lines.Count > 0) AssetDatabase.SaveAssets();
            return lines;
        }

        // ---- what was built from what (1.8)

        /// <summary>The record of the last build for a species, or null (never built by 1.8, or settings from before it).</summary>
        internal static AnimalBuildRecord FindRecord(SliceBuildSettings settings, string speciesId)
        {
            if (settings == null || settings.animalBuilds == null || string.IsNullOrEmpty(speciesId)) return null;
            foreach (var r in settings.animalBuilds) if (r != null && r.speciesId == speciesId) return r;
            return null;
        }

        /// <summary>Writes (or replaces) the species' build record in the settings asset. <paramref name="source"/> may be null (nothing traceable).</summary>
        internal static void RecordBuild(SpeciesDefinition species, Object source, float size, float yaw, bool tint, bool manual)
        {
            if (species == null || string.IsNullOrEmpty(species.speciesId)) return;
            string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
            WriteRecord(species.speciesId, string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path), size, yaw, tint, manual);
        }

        internal static void WriteRecord(string speciesId, string sourceGuid, float size, float yaw, bool tint, bool manual)
        {
            var settings = WildLensSliceBuilder.LoadOrCreateSettings();
            if (settings == null) return;
            if (settings.animalBuilds == null) settings.animalBuilds = new List<AnimalBuildRecord>();
            var rec = FindRecord(settings, speciesId);
            Undo.RecordObject(settings, "Record species build");
            if (rec == null) { rec = new AnimalBuildRecord { speciesId = speciesId }; settings.animalBuilds.Add(rec); }
            rec.sourceGuid = sourceGuid ?? "";
            rec.factoryVersion = FactoryVersion;
            rec.manual = manual;
            rec.size = size; rec.yaw = yaw; rec.tint = tint;
            EditorUtility.SetDirty(settings);
        }

        private static readonly string[] ModelExtensions = { ".fbx", ".obj", ".blend", ".gltf", ".glb", ".dae", ".3ds", ".prefab", ".ma", ".mb", ".max" };

        private static bool IsModelPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", System.StringComparison.Ordinal)) return false;
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return System.Array.IndexOf(ModelExtensions, ext) >= 0;
        }

        /// <summary>
        /// The model files a species prefab was built from, best first (shared by the Asset Audit's "Used by species" column and the
        /// automatic refresh of old prefabs): the assets its objects are linked to, then (1.7.1 unpacked pack prefabs, which removes
        /// those links) the model files its meshes come from. The prefab's own path is never listed.
        /// </summary>
        internal static List<string> SourceModelPaths(GameObject prefab)
        {
            var list = new List<string>();
            if (prefab == null) return list;
            string own = AssetDatabase.GetAssetPath(prefab);
            void Add(string p) { if (!string.IsNullOrEmpty(p) && p != own && !list.Contains(p)) list.Add(p); }
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (src != null) Add(AssetDatabase.GetAssetPath(src));
            }
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != null && smr.sharedMesh != null) { string mp = AssetDatabase.GetAssetPath(smr.sharedMesh); if (IsModelPath(mp)) Add(mp); }
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf != null && mf.sharedMesh != null) { string mp = AssetDatabase.GetAssetPath(mf.sharedMesh); if (IsModelPath(mp)) Add(mp); }
            return list;
        }

        // ---- pack components (1.7.1)

        // Components a pack may ship that a WildLens animal must not carry, matched by type name so no module reference is needed:
        // navigation, effects (renderer first: it requires the system), lights, listeners and cameras (FlareLayer requires its camera).
        private static readonly string[] StripByTypeName =
            { "NavMeshAgent", "NavMeshObstacle", "ParticleSystemRenderer", "ParticleSystem", "Light", "AudioListener", "FlareLayer", "Camera" };

        private static bool IsWildLensType(System.Type t)
        {
            string ns = t != null ? t.Namespace : null;
            return ns != null && (ns == "WildLens" || ns.StartsWith("WildLens.", System.StringComparison.Ordinal));
        }

        private static bool RequiresType(System.Type required, Component target) => required != null && required.IsAssignableFrom(target.GetType());

        /// <summary>Whether another component on the same object has a RequireComponent for this one (removing it would log an error).</summary>
        private static bool RequiredByAnother(Component target)
        {
            foreach (var other in target.gameObject.GetComponents<Component>())
            {
                if (other == null || other == target) continue;
                foreach (var attr in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                    if (attr is RequireComponent rc && (RequiresType(rc.m_Type0, target) || RequiresType(rc.m_Type1, target) || RequiresType(rc.m_Type2, target)))
                        return true;
            }
            return false;
        }

        private static bool TryDestroy(Component c)
        {
            if (c == null) return false;
            try
            {
                if (RequiredByAnother(c)) return false; // a partner goes first, on a later pass
                Object.DestroyImmediate(c);
                return true;
            }
            catch (System.Exception) { return false; }
        }

        /// <summary>
        /// Packs ship scripts, physics, audio, effects and cameras of their own; a species prefab needs none of them (WildLens adds
        /// its own awareness, trigger collider and body when the animal spawns). Scripts that are not WildLens's are removed in up
        /// to three passes (one may require another), then joints, colliders, rigidbodies, audio and the by-name list. The
        /// Animator, the renderers and the bones stay. Run it on an UNPACKED instance, never on the pack's own asset.
        /// </summary>
        internal static void StripPackComponents(GameObject go)
        {
            if (go == null) return;
            // Scripts whose class is gone leave a "missing" component that nothing else can remove.
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                try { GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject); } catch (System.Exception) { }
            }
            for (int pass = 0; pass < 3; pass++)
            {
                int removed = 0, left = 0;
                foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (c == null || IsWildLensType(c.GetType())) continue;
                    left++;
                    if (TryDestroy(c)) removed++;
                }
                if (left == 0 || removed == 0) break;
            }
            foreach (var c in go.GetComponentsInChildren<Joint>(true)) TryDestroy(c);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) TryDestroy(c);
            foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) TryDestroy(c);
            foreach (var c in go.GetComponentsInChildren<AudioSource>(true)) TryDestroy(c);
            foreach (string typeName in StripByTypeName)
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                    if (c != null && c.GetType().Name == typeName) TryDestroy(c);
        }

        private static void UnpackCompletely(GameObject instance)
        {
            if (instance == null) return;
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(instance) && PrefabUtility.IsOutermostPrefabInstanceRoot(instance))
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            catch (System.Exception e) { Debug.LogWarning("[WildLens] Could not unpack " + instance.name + ": " + e.Message); }
        }

        // ---- materials for the active pipeline (1.7.1)

        /// <summary>True when the active render pipeline is URP.</summary>
        internal static bool UrpActive()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            return rp != null && rp.GetType().Name.IndexOf("Universal", System.StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Why a shader will not render correctly in the active pipeline (null: it will). The one test shared by the Asset Audit and
        /// <see cref="ConvertMaterialsForPipeline"/>, so the report and the fix agree.
        /// </summary>
        internal static string PipelineIssue(string shader, bool hasRp, bool hdrp)
        {
            if (string.IsNullOrEmpty(shader) || shader == "(missing shader)" || shader == "Hidden/InternalErrorShader") return "missing shader";
            if (shader == "(no material)") return null;
            if (!hdrp && (shader.Contains("HDRP") || shader.Contains("HDRenderPipeline"))) return "HDRP shader";
            if (hasRp) return BuiltinShaderIssue(shader);
            if (shader.Contains("Universal Render Pipeline")) return "URP shader (pink in Built-in)";
            return null;
        }

        private static readonly string[] MainTextureProps = { "_MainTex", "_BaseMap", "_BaseColorMap", "_DiffuseMap", "_Diffuse", "_AlbedoTex", "_Albedo" };
        private static readonly string[] ColourProps = { "_Color", "_BaseColor", "_MainColor", "_TintColor", "_DiffuseColor" };
        private static readonly string[] NormalProps = { "_BumpMap", "_NormalMap", "_NormalTex", "_Normal" };

        private static bool HasProp(Material m, string name, UnityEngine.Rendering.ShaderPropertyType type)
        {
            if (m == null || m.shader == null) return false;
            int i = m.shader.FindPropertyIndex(name);
            return i >= 0 && m.shader.GetPropertyType(i) == type;
        }
        private static bool HasFloat(Material m, string name) =>
            HasProp(m, name, ShaderPropertyType.Float) || HasProp(m, name, ShaderPropertyType.Range);

        /// <summary>
        /// Replaces, on every renderer under <paramref name="root"/>, each material whose shader the active pipeline cannot draw
        /// (a built-in "Standard", "Legacy Shaders/*", "Mobile/*", "Unlit/*" or an error shader under URP) with a converted COPY in
        /// <paramref name="folder"/> (the pack's own material is never touched). One copy per source material; names are
        /// deterministic (<c>{prefix}_{n}_{material}_URP.mat</c>) and an existing copy is updated in place (its GUID kept). Texture, colour, normal map,
        /// emission, metallic/smoothness, alpha clip or transparency and two-sidedness are carried over. Under any other pipeline
        /// nothing is changed. Returns how many materials were converted; <paramref name="note"/> says why none were when URP's
        /// shaders cannot be found. Reusable by anything that places pack models (call it on an instance or on prefab contents).
        /// </summary>
        internal static int ConvertMaterialsForPipeline(GameObject root, string namePrefix, string folder, out string note) =>
            ConvertMaterialsForPipeline(root, namePrefix, folder, new Color(0.75f, 0.73f, 0.7f, 1f), out note);

        /// <summary>
        /// As above (1.9.1), with the colour given to a plain URP material that fills an EMPTY slot (a missing material is drawn magenta). A
        /// slot is converted when <see cref="UrpDrawIssue"/> says URP cannot draw it: a built-in shader by name, a shader that does not compile
        /// or is not supported here, or one whose passes are all built-in-only (a pack's own surface shader, whatever it is called). Copies are
        /// updated IN PLACE when their file exists (its GUID, and every reference to it, survives); nothing is ever deleted.
        /// </summary>
        internal static int ConvertMaterialsForPipeline(GameObject root, string namePrefix, string folder, Color emptySlotColour, out string note)
        {
            note = "";
            if (root == null || !UrpActive()) return 0;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Shader target = null;
            string targetFrom = "";
            var copies = new Dictionary<Material, Material>();
            var notes = new List<string>();
            int made = 0, index = 0;
            for (int ri = 0; ri < renderers.Length; ri++)
            {
                var r = renderers[ri];
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src != null && copies.TryGetValue(src, out var done)) { mats[i] = done; changed = true; continue; }
                    string issue = UrpDrawIssue(src);
                    if (issue == null) continue;
                    if (target == null)
                    {
                        target = UrpLitShader(out targetFrom);
                        if (target == null) { note = "URP's Lit shader could not be found (neither by name nor from the pipeline asset): materials left as they were"; return 0; }
                        if (targetFrom != "Shader.Find") notes.Add("URP Lit taken from " + targetFrom);
                    }
                    try
                    {
                        GeneratedAssets.EnsureFolder(folder);
                        Material copy;
                        if (src == null)
                        {
                            copy = PlainMaterial($"{folder}/{FallbackName(namePrefix, ri, i)}", target, emptySlotColour);
                            notes.Add($"empty slot {i} on {r.name} filled");
                        }
                        else
                        {
                            copy = ConvertMaterial(src, $"{folder}/{ConversionCopyName(namePrefix, index++, src.name)}", target);
                            copies[src] = copy;
                            if (!issue.StartsWith("built-in shader", System.StringComparison.Ordinal) && !issue.StartsWith("built-in unlit", System.StringComparison.Ordinal))
                                notes.Add($"{src.name}: {issue}, converted");
                        }
                        mats[i] = copy;
                        changed = true;
                        made++;
                    }
                    catch (System.Exception e)
                    {
                        notes.Add("a material could not be converted (" + (src != null ? src.name : "empty slot") + ": " + e.Message + ")");
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
            if (notes.Count > 0) note = string.Join("; ", notes);
            return made;
        }

        // ---- what URP can draw (1.9.1)

        /// <summary>
        /// Why URP cannot draw a material (null: it can). Decided structurally, not only by name: no material (an empty or missing slot is
        /// magenta); no shader or the error shader; a built-in pipeline shader by name (<see cref="BuiltinShaderIssue"/>, which also sends
        /// built-in Unlit to Lit); a shader that is not supported here or has compile errors (an old pack shader on Unity 6); or a shader none of
        /// whose passes URP draws (<see cref="UrpDrawsAnyPass"/>: a surface shader keeps its built-in ForwardBase/ForwardAdd passes whatever it
        /// is named). Shared by the factory, the repair of built prefabs and the Asset Audit, so the probe and the fix agree.
        /// </summary>
        internal static string UrpDrawIssue(Material m)
        {
            if (m == null) return "no material";
            Shader sh = null;
            try { sh = m.shader; } catch (System.Exception) { }
            if (sh == null) return "missing shader";
            string name = sh.name ?? "";
            string byName = BuiltinShaderIssue(name);
            if (byName != null) return byName;
            try { if (!sh.isSupported) return "shader not supported here"; } catch (System.Exception) { }
            // Compile errors: asked of every shader but URP's own (so a false report there can never send URP Lit round in circles).
            bool urpOwn = name.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal) || name.StartsWith("Hidden/Universal", System.StringComparison.Ordinal);
            try { if (!urpOwn && ShaderUtil.ShaderHasError(sh)) return "shader has compile errors"; } catch (System.Exception) { }
            var modes = LightModes(sh);
            if (modes != null && !UrpDrawsAnyPass(modes))
            {
                var distinct = new SortedSet<string>();
                foreach (var x in modes) distinct.Add(string.IsNullOrEmpty(x) ? "untagged" : x);
                return "no pass URP draws (" + string.Join(", ", distinct) + ")";
            }
            return null;
        }

        /// <summary>The LightMode tag of every pass of the shader's active SubShader ("" when untagged), or null when it cannot be read.</summary>
        private static List<string> LightModes(Shader sh)
        {
            try
            {
                int n = sh.passCount;
                var tag = new ShaderTagId("LightMode");
                var list = new List<string>(n);
                for (int i = 0; i < n; i++)
                {
                    var v = sh.FindPassTagValue(i, tag);
                    list.Add(v.name ?? "");
                }
                return list;
            }
            catch (System.Exception) { return null; }
        }

        private static bool UsableTarget(Shader s)
        {
            if (s == null) return false;
            try
            {
                if (s.name == "Hidden/InternalErrorShader" || !s.isSupported) return false;
                var modes = LightModes(s);
                return modes == null || UrpDrawsAnyPass(modes);
            }
            catch (System.Exception) { return false; }
        }

        /// <summary>
        /// URP's Lit shader: by name, else the active pipeline asset's own default shader or default material's shader (read by reflection:
        /// those members are obsolete in some Unity 6 versions), else Simple Lit. Null only when none of them can draw.
        /// </summary>
        internal static Shader UrpLitShader(out string from)
        {
            from = "Shader.Find";
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (UsableTarget(s)) return s;
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null)
            {
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                try
                {
                    var p = rp.GetType().GetProperty("defaultShader", F);
                    if (p != null && p.GetValue(rp, null) is Shader ds && UsableTarget(ds)) { from = "the pipeline's default shader"; return ds; }
                }
                catch (System.Exception) { }
                try
                {
                    var p = rp.GetType().GetProperty("defaultMaterial", F);
                    if (p != null && p.GetValue(rp, null) is Material dm && dm != null && UsableTarget(dm.shader)) { from = "the pipeline's default material"; return dm.shader; }
                }
                catch (System.Exception) { }
            }
            s = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (UsableTarget(s)) { from = "Simple Lit"; return s; }
            return null;
        }

        // ---- a material's saved values, readable even when its shader is broken (1.9.1)

        private static SerializedProperty SavedEntry(SerializedObject so, string list, string name)
        {
            var arr = so.FindProperty("m_SavedProperties." + list);
            if (arr == null || !arr.isArray) return null;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                var first = e.FindPropertyRelative("first");
                if (first != null && first.propertyType == SerializedPropertyType.String && first.stringValue == name) return e.FindPropertyRelative("second");
            }
            return null;
        }

        /// <summary>A texture property: from the shader when it has it, else from the material's saved properties.</summary>
        private static bool TryTexture(Material src, SerializedObject so, string p, out Texture tex, out Vector2 scale, out Vector2 offset)
        {
            tex = null; scale = Vector2.one; offset = Vector2.zero;
            if (HasProp(src, p, ShaderPropertyType.Texture))
            {
                tex = src.GetTexture(p);
                scale = src.GetTextureScale(p); offset = src.GetTextureOffset(p);
                return tex != null;
            }
            try
            {
                var e = so != null ? SavedEntry(so, "m_TexEnvs", p) : null;
                if (e == null) return false;
                tex = e.FindPropertyRelative("m_Texture")?.objectReferenceValue as Texture;
                var sc = e.FindPropertyRelative("m_Scale"); if (sc != null) scale = sc.vector2Value;
                var of = e.FindPropertyRelative("m_Offset"); if (of != null) offset = of.vector2Value;
                return tex != null;
            }
            catch (System.Exception) { return false; }
        }

        private static bool TryColour(Material src, SerializedObject so, string p, out Color c)
        {
            c = Color.white;
            if (HasProp(src, p, ShaderPropertyType.Color)) { c = src.GetColor(p); return true; }
            try
            {
                var e = so != null ? SavedEntry(so, "m_Colors", p) : null;
                if (e == null || e.propertyType != SerializedPropertyType.Color) return false;
                c = e.colorValue;
                return true;
            }
            catch (System.Exception) { return false; }
        }

        private static bool TryFloat(Material src, SerializedObject so, string p, out float f)
        {
            f = 0f;
            if (HasFloat(src, p)) { f = src.GetFloat(p); return true; }
            try
            {
                var e = so != null ? SavedEntry(so, "m_Floats", p) : null;
                if (e == null || e.propertyType != SerializedPropertyType.Float) return false;
                f = e.floatValue;
                return true;
            }
            catch (System.Exception) { return false; }
        }

        /// <summary>
        /// Writes a freshly made material to <paramref name="path"/>: when a material already lives there its contents are replaced IN PLACE
        /// (its GUID survives, so a prefab saved earlier that uses it keeps a valid reference), else it is created. Never deletes a material.
        /// </summary>
        private static Material SaveInPlace(Material fresh, string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && existing != fresh)
            {
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(fresh);
                return existing;
            }
            // Something that is not a material sits at the path (never made by WildLens): the material goes beside it instead.
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) path = AssetDatabase.GenerateUniqueAssetPath(path);
            fresh.name = name;
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        /// <summary>A plain opaque URP material of one colour, for a slot that had no material at all.</summary>
        private static Material PlainMaterial(string path, Shader target, Color colour)
        {
            var m = new Material(target);
            colour.a = 1f;
            m.SetColor("_BaseColor", colour);
            if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return SaveInPlace(m, path);
        }

        private static Material ConvertMaterial(Material src, string path, Shader target)
        {
            var m = new Material(target);
            string sn = src.shader != null ? src.shader.name : "";
            SerializedObject so = null;
            try { so = new SerializedObject(src); } catch (System.Exception) { so = null; }

            // Colour and main texture (read from the material's saved values when its shader cannot say: a broken shader keeps them).
            Color colour = Color.white;
            foreach (var p in ColourProps)
                if (TryColour(src, so, p, out var c)) { colour = c; break; }
            m.SetColor("_BaseColor", colour);
            foreach (var p in MainTextureProps)
            {
                if (!TryTexture(src, so, p, out var tex, out var scale, out var offset)) continue;
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", scale);
                m.SetTextureOffset("_BaseMap", offset);
                if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", scale); m.SetTextureOffset("_MainTex", offset); }
                break;
            }

            // Normal map.
            foreach (var p in NormalProps)
            {
                if (!TryTexture(src, so, p, out var tex, out _, out _)) continue;
                m.SetTexture("_BumpMap", tex);
                if (TryFloat(src, so, "_BumpScale", out float bs)) m.SetFloat("_BumpScale", bs);
                m.EnableKeyword("_NORMALMAP");
                break;
            }

            // Emission.
            if (HasProp(src, "_EmissionColor", ShaderPropertyType.Color))
            {
                var e = src.GetColor("_EmissionColor");
                if (src.IsKeywordEnabled("_EMISSION") || e.maxColorComponent > 0.01f)
                {
                    m.SetColor("_EmissionColor", e);
                    if (HasProp(src, "_EmissionMap", ShaderPropertyType.Texture) && src.GetTexture("_EmissionMap") != null) m.SetTexture("_EmissionMap", src.GetTexture("_EmissionMap"));
                    m.EnableKeyword("_EMISSION");
                }
            }

            // Metallic and smoothness: carried over; a legacy diffuse shader has neither, so a matt 0.1.
            m.SetFloat("_Metallic", HasFloat(src, "_Metallic") ? src.GetFloat("_Metallic") : 0f);
            float smooth = HasFloat(src, "_Glossiness") ? src.GetFloat("_Glossiness") : HasFloat(src, "_Smoothness") ? src.GetFloat("_Smoothness") : 0.1f;
            m.SetFloat("_Smoothness", Mathf.Clamp01(smooth));

            // Alpha: cutout (feathers, leaves) or transparent.
            float mode = HasFloat(src, "_Mode") ? src.GetFloat("_Mode") : -1f;
            var alpha = AlphaKindOf(sn, mode, src.IsKeywordEnabled("_ALPHATEST_ON"));
            if (alpha == AlphaKind.Cutout)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", TryFloat(src, so, "_Cutoff", out float cut) ? Mathf.Clamp(cut, 0.05f, 0.95f) : 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (alpha == AlphaKind.Transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetShaderPassEnabled("ShadowCaster", false);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            if (TwoSidedByName(sn) || (HasFloat(src, "_Cull") && Mathf.Approximately(src.GetFloat("_Cull"), 0f)))
            {
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
            }
            return SaveInPlace(m, path); // same name every run, updated in place (1.9.1: never deleted and recreated)
        }

        /// <summary>Bounds of the model as posed now (skinned meshes baked), relative to <paramref name="root"/>.</summary>
        private static Bounds PosedBounds(GameObject model, Transform root)
        {
            bool any = false;
            var b = new Bounds();
            void Add(Vector3 world)
            {
                Vector3 p = world - root.position;
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in baked.vertices) Add(m.MultiplyPoint3x4(v));
                Object.DestroyImmediate(baked);
            }
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null)
                    foreach (var v in mf.sharedMesh.vertices) Add(mf.transform.TransformPoint(v));
            return any ? b : RendererBounds(model);
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            b.center -= go.transform.parent != null ? go.transform.parent.position : Vector3.zero;
            return b;
        }

        /// <summary>
        /// Species colours on a stand-in: the model's materials are copied (the pack's own are never changed) and coloured
        /// by how much of the body they cover: the largest the species' primary colour, the next its secondary, the third
        /// its accent. Flat-coloured stylised packs take this well; a photographic texture is tinted, not replaced.
        /// </summary>
        private const float DarkTintLuminance = 0.12f;
        private static readonly Color DarkTintFloor = new Color(0.16f, 0.15f, 0.15f, 1f);

        /// <summary>Species that ARE black or near-black: their tint keeps the old rule (only a true black is lifted, to the dark floor).</summary>
        private static readonly HashSet<string> MeantToBeDark = new HashSet<string> { "american_crow", "red_winged_blackbird", "american_black_bear" };

        /// <summary>Lightest a tint may be made darker than, for species that are not meant to be black (1.9.1: the turkey read near-black at dusk).</summary>
        private const float TintLuminanceFloor = 0.16f;

        /// <summary>
        /// The colour a tinted copy gets (1.9.1). The tint REPLACES the material's colour and multiplies its texture, so on a textured model the
        /// species colour comes out darker than it is. For species not meant to be black: the colour is lifted to make up for the texture
        /// (when the texture can be read, by its average brightness, at most ×1.5; when it cannot, 25% toward white), then kept at or above a
        /// luminance of 0.16, hue kept. The crow, the blackbird and the bear keep the earlier rule.
        /// </summary>
        private static Color TintColour(Color colour, Material source, string speciesId)
        {
            Color c = colour; c.a = 1f;
            if (speciesId != null && MeantToBeDark.Contains(speciesId))
                return c.grayscale < DarkTintLuminance ? DarkTintFloor : c;
            var tex = MainTexture(source);
            if (tex != null)
            {
                float avg = AverageLuminance(tex);
                if (avg > 0.01f) { float k = Mathf.Clamp(0.55f / avg, 1f, 1.5f); c = new Color(c.r * k, c.g * k, c.b * k, 1f); }
                else c = Color.Lerp(c, Color.white, 0.25f);
            }
            float g = c.grayscale;
            if (g < TintLuminanceFloor)
            {
                if (g > 0.01f) { float k = TintLuminanceFloor / g; c = new Color(c.r * k, c.g * k, c.b * k, 1f); }
                else c = DarkTintFloor;
            }
            c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); c.a = 1f;
            return c;
        }

        private static Texture MainTexture(Material m)
        {
            if (m == null) return null;
            foreach (var p in MainTextureProps)
                if (HasProp(m, p, ShaderPropertyType.Texture)) { var t = m.GetTexture(p); if (t != null) return t; }
            return null;
        }

        /// <summary>The average luminance of a readable texture (sampled from a small mip), or -1 when it cannot be read (most pack textures).</summary>
        private static float AverageLuminance(Texture tex)
        {
            try
            {
                if (!(tex is Texture2D t) || !t.isReadable) return -1f;
                int mip = Mathf.Max(0, t.mipmapCount - 4);
                var px = t.GetPixels32(mip);
                if (px == null || px.Length == 0) return -1f;
                double sum = 0; int n = 0;
                int step = Mathf.Max(1, px.Length / 4096);
                for (int i = 0; i < px.Length; i += step)
                {
                    var p = px[i];
                    if (p.a < 16) continue; // cut-out holes do not count
                    sum += (0.299 * p.r + 0.587 * p.g + 0.114 * p.b) / 255.0; n++;
                }
                return n == 0 ? -1f : (float)(sum / n);
            }
            catch (System.Exception) { return -1f; }
        }

        private static int Tint(GameObject model, SpeciesDefinition s, string id)
        {
            var coverage = new Dictionary<Material, int>();
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    int tris = mesh != null && i < mesh.subMeshCount ? (int)mesh.GetIndexCount(i) / 3 : 1;
                    coverage.TryGetValue(mats[i], out int c);
                    coverage[mats[i]] = c + tris;
                }
            }
            var order = new List<Material>(coverage.Keys);
            order.Sort((a, b) => coverage[b].CompareTo(coverage[a]));
            var colours = new[] { s.primaryColor, s.secondaryColor, s.accentColor };
            var copies = new Dictionary<Material, Material>();
            for (int i = 0; i < order.Count && i < colours.Length; i++)
            {
                string path = $"{Folder}/{id}_Tint{i}.mat";
                var copy = new Material(order[i]) { name = $"{id}_Tint{i}" };
                Color c = TintColour(colours[i], order[i], s.speciesId);
                if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", c);
                if (copy.HasProperty("_Color")) copy.SetColor("_Color", c);
                copy = SaveInPlace(copy, path); // 1.9.1: updated in place, never deleted and recreated
                copies[order[i]] = copy;
            }
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && copies.TryGetValue(mats[i], out var m)) { mats[i] = m; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
            return copies.Count;
        }

        private static bool OneShot(ClipRole r) => r == ClipRole.Land || r == ClipRole.TakeOff;

        /// <summary>
        /// Pack clips usually have Loop Time off, so a walk would play once and freeze. Every clip that is not already looping gets a
        /// looped COPY stored inside the controller (the pack's clip and import settings are never touched); one-shot roles
        /// (landing, take-off) keep the original. The same copy is used everywhere a clip is reused.
        /// </summary>
        private static AnimationClip LoopedCopy(AnimationClip clip, AnimatorController ctrl, Dictionary<AnimationClip, AnimationClip> copies)
        {
            if (clip == null || clip.isLooping) return clip;
            if (copies.TryGetValue(clip, out var done)) return done;
            var copy = Object.Instantiate(clip);
            copy.name = clip.name + " (looped)";
            copy.hideFlags = HideFlags.None;
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AssetDatabase.AddObjectToAsset(copy, ctrl);
            copies[clip] = copy;
            return copy;
        }

        private static AnimatorController BuildController(SpeciesDefinition species, Dictionary<ClipRole, AnimationClip> clips, string path)
        {
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Activity", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("GaitPhase", AnimatorControllerParameterType.Float);
            var sm = ctrl.layers[0].stateMachine;
            var loops = new Dictionary<AnimationClip, AnimationClip>();

            // The clip for a role (or its first fallback that has one), looped unless the role that supplied it is a one-shot.
            AnimationClip Pick(ClipRole r, params ClipRole[] fallbacks)
            {
                if (clips.TryGetValue(r, out var c) && c != null) return OneShot(r) ? c : LoopedCopy(c, ctrl, loops);
                foreach (var f in fallbacks)
                    if (clips.TryGetValue(f, out c) && c != null) return OneShot(f) ? c : LoopedCopy(c, ctrl, loops);
                return null;
            }

            // Locomotion: idle → walk → trot → run on Speed, thresholds from the species' own speeds.
            var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            float walk = Mathf.Max(0.3f, species.walkSpeed), run = Mathf.Max(walk * 2f, species.runSpeed);
            var idle = Pick(ClipRole.Idle, ClipRole.Look);
            if (idle != null) tree.AddChild(idle, 0f);
            var walkClip = Pick(ClipRole.Walk);
            if (walkClip != null) tree.AddChild(walkClip, walk);
            var trotClip = Pick(ClipRole.Trot);
            if (trotClip != null) tree.AddChild(trotClip, Mathf.Lerp(walk, run, 0.4f));
            var runClip = Pick(ClipRole.Run);
            if (runClip != null) tree.AddChild(runClip, run);
            var locomotion = sm.AddState("Locomotion");
            locomotion.motion = tree;
            sm.defaultState = locomotion;

            var states = new Dictionary<AnimationClip, AnimatorState>();
            AnimatorState StateFor(AnimationClip clip)
            {
                if (clip == null) return locomotion;
                if (!states.TryGetValue(clip, out var st))
                {
                    st = sm.AddState(clip.name);
                    st.motion = clip;
                    states[clip] = st;
                }
                return st;
            }

            var map = new Dictionary<AnimalActivity, AnimatorState>
            {
                { AnimalActivity.Grazing, StateFor(Pick(ClipRole.Eat)) },
                { AnimalActivity.Foraging, StateFor(Pick(ClipRole.Eat)) },
                { AnimalActivity.Sniffing, StateFor(Pick(ClipRole.Eat, ClipRole.Look)) },
                { AnimalActivity.LookingAround, StateFor(Pick(ClipRole.Look)) },
                { AnimalActivity.Alert, StateFor(Pick(ClipRole.Look)) },
                { AnimalActivity.Grooming, StateFor(Pick(ClipRole.Groom, ClipRole.Sit)) },
                { AnimalActivity.Resting, StateFor(Pick(ClipRole.Lie, ClipRole.Sit)) },
                { AnimalActivity.Sitting, StateFor(Pick(ClipRole.Sit, ClipRole.Lie)) },
                { AnimalActivity.Perched, StateFor(Pick(ClipRole.Idle)) },
                { AnimalActivity.Flying, StateFor(Pick(ClipRole.Fly)) },
                { AnimalActivity.Soaring, StateFor(Pick(ClipRole.Glide, ClipRole.Fly)) },
                { AnimalActivity.Gliding, StateFor(Pick(ClipRole.Glide, ClipRole.Fly)) },
                { AnimalActivity.Landing, StateFor(Pick(ClipRole.Land, ClipRole.Fly)) },
                { AnimalActivity.TakingOff, StateFor(Pick(ClipRole.TakeOff, ClipRole.Fly)) },
            };
            foreach (AnimalActivity a in System.Enum.GetValues(typeof(AnimalActivity)))
            {
                var target = map.TryGetValue(a, out var st) ? st : locomotion; // walking, running, fleeing, hopping…
                var t = sm.AddAnyStateTransition(target);
                t.AddCondition(AnimatorConditionMode.Equals, (int)a, "Activity");
                t.canTransitionToSelf = false;
                t.duration = 0.25f;
                t.hasExitTime = false;
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }
    }
}
