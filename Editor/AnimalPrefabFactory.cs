using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
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

        // <pure-end>

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
        /// The species' real size (the larger of nose-to-tail-tip length and height), used to scale a model: the original
        /// five from field measurements, the others from their body plan.
        /// </summary>
        public static float DefaultSize(SpeciesDefinition s)
        {
            if (s == null) return 1f;
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

        /// <summary>Builds the controller and prefab and assigns it to the species. Returns the prefab.</summary>
        public static GameObject Create(SpeciesDefinition species, GameObject source, Dictionary<ClipRole, AnimationClip> clips,
            float size, float yaw, bool tint, out string message)
        {
            GeneratedAssets.EnsureFolder(Folder);
            string id = string.IsNullOrEmpty(species.speciesId) ? species.name : species.speciesId;
            var controller = BuildController(species, clips, $"{Folder}/{id}_Animator.controller");

            // Wrapper root (what the spawner instantiates and the AI moves) → yaw-corrected, scaled model.
            var root = new GameObject(species.displayName);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (model == null) model = Object.Instantiate(source);
            // A pack prefab is unpacked completely first: its scripts, colliders and bodies are then removed from OUR copy (the
            // result is a new prefab that no longer depends on the pack's), and the pack's own assets are never modified.
            UnpackCompletely(model);
            model.transform.SetParent(root.transform, false);
            StripPackComponents(model);
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation; // keep the file's own axis fix

            var bounds = RendererBounds(model);
            float measured = IsBird(species) ? bounds.size.z : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = measured > 0.0001f ? size / measured : 1f;
            model.transform.localScale = model.transform.localScale * scale;
            // Feet on the origin, measured in the idle pose (a bind pose may be a T-pose or a bird in flight).
            var idleClip = Clip(clips, ClipRole.Idle, ClipRole.Look);
            if (idleClip != null) idleClip.SampleAnimation(model, 0f);
            bounds = PosedBounds(model, root.transform);
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // the AI moves the animal; clips only animate the body
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // Materials the active pipeline cannot draw (a built-in shader in URP is pink) are replaced by converted COPIES, then tinted.
            int converted = 0;
            string conversionNote = "";
            try { converted = ConvertMaterialsForPipeline(model, id, Folder, out conversionNote); }
            catch (System.Exception e) { conversionNote = "material conversion failed: " + e.Message; }
            int tinted = tint ? Tint(model, species, id) : 0;

            string prefabPath = $"{Folder}/{id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            Undo.RecordObject(species, "Assign species prefab");
            species.prefab = prefab;
            EditorUtility.SetDirty(species);
            AssetDatabase.SaveAssets();
            try { WildlifePackSetup.SetKeepPlaceholder(species.speciesId, false); } catch (System.Exception) { } // a model was set: no longer kept as a placeholder
            message = $"{species.displayName}: {prefabPath} (scale {scale:0.###}" + (converted > 0 ? $", {converted} materials converted for URP" : "") +
                      (tinted > 0 ? $", {tinted} materials tinted" : "") + (string.IsNullOrEmpty(conversionNote) ? "" : "; " + conversionNote) + ")";
            return prefab;
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

        private static string SafeName(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "") sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.Length == 0 ? "mat" : sb.ToString();
        }

        /// <summary>
        /// Replaces, on every renderer under <paramref name="root"/>, each material whose shader the active pipeline cannot draw
        /// (a built-in "Standard", "Legacy Shaders/*", "Mobile/*", "Unlit/*" or an error shader under URP) with a converted COPY in
        /// <paramref name="folder"/> (the pack's own material is never touched). One copy per source material; names are
        /// deterministic (<c>{prefix}_{n}_{material}_URP.mat</c>) and an existing copy is overwritten. Texture, colour, normal map,
        /// emission, metallic/smoothness, alpha clip or transparency and two-sidedness are carried over. Under any other pipeline
        /// nothing is changed. Returns how many materials were converted; <paramref name="note"/> says why none were when URP's
        /// shaders cannot be found. Reusable by anything that places pack models (call it on an instance or on prefab contents).
        /// </summary>
        internal static int ConvertMaterialsForPipeline(GameObject root, string namePrefix, string folder, out string note)
        {
            note = "";
            if (root == null || !UrpActive()) return 0;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Shader target = null;
            var copies = new Dictionary<Material, Material>();
            int made = 0;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (copies.TryGetValue(src, out var done)) { mats[i] = done; changed = true; continue; }
                    string shaderName = src.shader != null ? src.shader.name : "";
                    if (BuiltinShaderIssue(shaderName) == null) continue;
                    if (target == null)
                    {
                        target = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
                        if (target == null) { note = "URP shaders not found: materials left as they were"; return 0; }
                    }
                    try
                    {
                        GeneratedAssets.EnsureFolder(folder);
                        string path = $"{folder}/{namePrefix}_{made:00}_{SafeName(src.name)}_URP.mat";
                        var copy = ConvertMaterial(src, path, target);
                        copies[src] = copy;
                        mats[i] = copy;
                        changed = true;
                        made++;
                    }
                    catch (System.Exception e)
                    {
                        note = "a material could not be converted (" + src.name + ": " + e.Message + ")";
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
            return made;
        }

        private static Material ConvertMaterial(Material src, string path, Shader target)
        {
            AssetDatabase.DeleteAsset(path); // overwrite: same name every run
            var m = new Material(target) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            string sn = src.shader != null ? src.shader.name : "";

            // Colour and main texture.
            Color colour = Color.white;
            foreach (var p in ColourProps) if (HasProp(src, p, ShaderPropertyType.Color)) { colour = src.GetColor(p); break; }
            m.SetColor("_BaseColor", colour);
            foreach (var p in MainTextureProps)
            {
                if (!HasProp(src, p, ShaderPropertyType.Texture)) continue;
                var tex = src.GetTexture(p);
                if (tex == null) continue;
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", src.GetTextureScale(p));
                m.SetTextureOffset("_BaseMap", src.GetTextureOffset(p));
                if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", src.GetTextureScale(p)); m.SetTextureOffset("_MainTex", src.GetTextureOffset(p)); }
                break;
            }

            // Normal map.
            foreach (var p in NormalProps)
            {
                if (!HasProp(src, p, ShaderPropertyType.Texture)) continue;
                var tex = src.GetTexture(p);
                if (tex == null) continue;
                m.SetTexture("_BumpMap", tex);
                if (HasFloat(src, "_BumpScale")) m.SetFloat("_BumpScale", src.GetFloat("_BumpScale"));
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
                m.SetFloat("_Cutoff", HasFloat(src, "_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
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
            AssetDatabase.CreateAsset(m, path);
            return m;
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
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in baked.vertices) Add(m.MultiplyPoint3x4(v));
                Object.DestroyImmediate(baked);
            }
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null)
                    foreach (var v in mf.sharedMesh.vertices) Add(mf.transform.TransformPoint(v));
            return any ? b : RendererBounds(model);
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
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
                AssetDatabase.DeleteAsset(path);
                var copy = new Material(order[i]) { name = $"{id}_Tint{i}" };
                Color c = colours[i];
                c.a = 1f;
                if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", c);
                if (copy.HasProperty("_Color")) copy.SetColor("_Color", c);
                AssetDatabase.CreateAsset(copy, path);
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

        private static AnimatorController BuildController(SpeciesDefinition species, Dictionary<ClipRole, AnimationClip> clips, string path)
        {
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Activity", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("GaitPhase", AnimatorControllerParameterType.Float);
            var sm = ctrl.layers[0].stateMachine;

            // Locomotion: idle → walk → trot → run on Speed, thresholds from the species' own speeds.
            var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            float walk = Mathf.Max(0.3f, species.walkSpeed), run = Mathf.Max(walk * 2f, species.runSpeed);
            var idle = Clip(clips, ClipRole.Idle, ClipRole.Look);
            if (idle != null) tree.AddChild(idle, 0f);
            if (Clip(clips, ClipRole.Walk) != null) tree.AddChild(Clip(clips, ClipRole.Walk), walk);
            if (Clip(clips, ClipRole.Trot) != null) tree.AddChild(Clip(clips, ClipRole.Trot), Mathf.Lerp(walk, run, 0.4f));
            if (Clip(clips, ClipRole.Run) != null) tree.AddChild(Clip(clips, ClipRole.Run), run);
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
                { AnimalActivity.Grazing, StateFor(Clip(clips, ClipRole.Eat)) },
                { AnimalActivity.Foraging, StateFor(Clip(clips, ClipRole.Eat)) },
                { AnimalActivity.Sniffing, StateFor(Clip(clips, ClipRole.Eat, ClipRole.Look)) },
                { AnimalActivity.LookingAround, StateFor(Clip(clips, ClipRole.Look)) },
                { AnimalActivity.Alert, StateFor(Clip(clips, ClipRole.Look)) },
                { AnimalActivity.Grooming, StateFor(Clip(clips, ClipRole.Groom, ClipRole.Sit)) },
                { AnimalActivity.Resting, StateFor(Clip(clips, ClipRole.Lie, ClipRole.Sit)) },
                { AnimalActivity.Sitting, StateFor(Clip(clips, ClipRole.Sit, ClipRole.Lie)) },
                { AnimalActivity.Perched, StateFor(Clip(clips, ClipRole.Idle)) },
                { AnimalActivity.Flying, StateFor(Clip(clips, ClipRole.Fly)) },
                { AnimalActivity.Soaring, StateFor(Clip(clips, ClipRole.Glide, ClipRole.Fly)) },
                { AnimalActivity.Gliding, StateFor(Clip(clips, ClipRole.Glide, ClipRole.Fly)) },
                { AnimalActivity.Landing, StateFor(Clip(clips, ClipRole.Land, ClipRole.Fly)) },
                { AnimalActivity.TakingOff, StateFor(Clip(clips, ClipRole.TakeOff, ClipRole.Fly)) },
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
