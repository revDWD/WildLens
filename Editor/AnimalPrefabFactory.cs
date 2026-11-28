using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
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

        // First matching keyword wins; checked in this order so "walk_eat" style names classify sensibly.
        private static readonly (ClipRole role, string[] words)[] Keywords =
        {
            (ClipRole.TakeOff, new[] { "takeoff", "take_off", "take off", "liftoff" }),
            (ClipRole.Land, new[] { "landing", "land" }),
            (ClipRole.Glide, new[] { "glide", "soar" }),
            (ClipRole.Fly, new[] { "fly", "flap", "flight" }),
            (ClipRole.Eat, new[] { "eat", "graze", "grazing", "feed", "drink", "sniff", "forage", "peck" }),
            (ClipRole.Lie, new[] { "lie", "lying", "laydown", "lay_down", "rest", "sleep" }),
            (ClipRole.Sit, new[] { "sit" }),
            (ClipRole.Groom, new[] { "groom", "lick", "scratch", "shake", "preen" }),
            (ClipRole.Look, new[] { "look", "alert", "listen", "attention", "turnhead", "head_turn", "headturn" }),
            (ClipRole.Run, new[] { "run", "gallop", "sprint", "flee" }),
            (ClipRole.Trot, new[] { "trot", "canter", "jog" }),
            (ClipRole.Walk, new[] { "walk", "hop", "jump" }),
            (ClipRole.Idle, new[] { "idle", "stand", "breath", "perch" }),
        };

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

        /// <summary>Clips classified by name into roles; <paramref name="log"/> gets one line per recognised clip.</summary>
        public static Dictionary<ClipRole, AnimationClip> Classify(IEnumerable<AnimationClip> clips, StringBuilder log = null)
        {
            var result = new Dictionary<ClipRole, AnimationClip>();
            AnimationClip first = null;
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (first == null) first = clip;
                string n = clip.name.ToLowerInvariant();
                foreach (var (role, words) in Keywords)
                {
                    bool hit = false;
                    foreach (var w in words) if (n.Contains(w)) { hit = true; break; }
                    if (!hit) continue;
                    if (!result.ContainsKey(role)) result[role] = clip;
                    log?.Append(clip.name).Append(" → ").Append(role).Append('\n');
                    break;
                }
            }
            if (!result.ContainsKey(ClipRole.Idle) && first != null) result[ClipRole.Idle] = first; // something rather than T-pose
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
            model.transform.SetParent(root.transform, false);
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var bounds = RendererBounds(model);
            float measured = IsBird(species) ? bounds.size.z : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = measured > 0.0001f ? size / measured : 1f;
            model.transform.localScale = model.transform.localScale * scale;
            bounds = RendererBounds(model);
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z); // feet on the origin

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // the AI moves the animal; clips only animate the body
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            int tinted = tint ? Tint(model, species, id) : 0;

            string prefabPath = $"{Folder}/{id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            Undo.RecordObject(species, "Assign species prefab");
            species.prefab = prefab;
            EditorUtility.SetDirty(species);
            AssetDatabase.SaveAssets();
            message = $"{species.displayName}: {prefabPath} (scale {scale:0.###}" + (tinted > 0 ? $", {tinted} materials tinted" : "") + ")";
            return prefab;
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
