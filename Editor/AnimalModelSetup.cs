using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WildLens.Data;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Turns an animated animal model (e.g. one bought from the Asset Store) into a WildLens species prefab:
    /// finds and classifies its clips by name, generates an Animator Controller driven by the parameters
    /// <see cref="Wildlife.AnimalAnimatorBridge"/> sets (Speed, Activity, GaitPhase), wraps the model in a prefab scaled
    /// to the species' real size with root motion off, and assigns it to the species asset. The AI, awareness,
    /// photography and journal need nothing else.
    /// </summary>
    public class AnimalModelSetup : EditorWindow
    {
        private enum Role { Idle, Walk, Trot, Run, Eat, Look, Sit, Lie, Groom, Fly, Glide, Land, TakeOff }

        // First matching keyword wins; checked in this order so "walk_eat" style names classify sensibly.
        private static readonly (Role role, string[] words)[] Keywords =
        {
            (Role.TakeOff, new[] { "takeoff", "take_off", "take off", "liftoff" }),
            (Role.Land, new[] { "landing", "land" }),
            (Role.Glide, new[] { "glide", "soar" }),
            (Role.Fly, new[] { "fly", "flap", "flight" }),
            (Role.Eat, new[] { "eat", "graze", "grazing", "feed", "drink", "sniff", "forage" }),
            (Role.Lie, new[] { "lie", "lying", "laydown", "lay_down", "rest", "sleep" }),
            (Role.Sit, new[] { "sit" }),
            (Role.Groom, new[] { "groom", "lick", "scratch", "shake" }),
            (Role.Look, new[] { "look", "alert", "listen", "attention", "turnhead", "head_turn" }),
            (Role.Run, new[] { "run", "gallop", "sprint", "flee" }),
            (Role.Trot, new[] { "trot", "canter", "jog" }),
            (Role.Walk, new[] { "walk", "hop", "jump" }),
            (Role.Idle, new[] { "idle", "stand", "breath", "perch" }),
        };

        // Real-world size used to scale the model: the larger of its body length and height (or wingspan for birds).
        private static float DefaultSize(PlaceholderBody body)
        {
            switch (body)
            {
                case PlaceholderBody.Deer: return 1.9f;
                case PlaceholderBody.Fox: return 1.0f;      // nose to tail tip
                case PlaceholderBody.Rabbit: return 0.42f;
                case PlaceholderBody.Squirrel: return 0.45f; // with tail
                case PlaceholderBody.Hawk: return 1.2f;     // wingspan if modelled wings-out, else set ~0.55
                default: return 1f;
            }
        }

        private SpeciesDefinition _species;
        private GameObject _model;
        private readonly List<Object> _extraClipSources = new List<Object>();
        private readonly Dictionary<Role, AnimationClip> _clips = new Dictionary<Role, AnimationClip>();
        private float _size = 1.9f, _yaw;
        private Vector2 _scroll;
        private string _log = "";

        [MenuItem("WildLens/Animal Model Setup", priority = 10)]
        public static void Open() => GetWindow<AnimalModelSetup>("Animal Model Setup");

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "Pick a species and an animated model (FBX or prefab). Clips are classified by name; correct any slot " +
                "by hand. Create builds an Animator Controller and a prefab in Assets/WildLens/Animals and assigns it to " +
                "the species. Re-run any time; rebuilding the scene is not needed (animals spawn on Play).", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _species = (SpeciesDefinition)EditorGUILayout.ObjectField("Species", _species, typeof(SpeciesDefinition), false);
            if (EditorGUI.EndChangeCheck() && _species != null) _size = DefaultSize(_species.placeholderBody);
            EditorGUI.BeginChangeCheck();
            _model = (GameObject)EditorGUILayout.ObjectField("Model (FBX / prefab)", _model, typeof(GameObject), false);
            bool modelChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.LabelField("Extra clip sources (animation FBXs or clips)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < _extraClipSources.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _extraClipSources[i] = EditorGUILayout.ObjectField(_extraClipSources[i], typeof(Object), false);
                if (GUILayout.Button("×", GUILayout.Width(22))) { _extraClipSources.RemoveAt(i); i--; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add clip source")) _extraClipSources.Add(null);

            if (modelChanged || GUILayout.Button("Find & classify clips")) Classify();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);
            foreach (Role r in System.Enum.GetValues(typeof(Role)))
            {
                _clips.TryGetValue(r, out var c);
                _clips[r] = (AnimationClip)EditorGUILayout.ObjectField(r.ToString(), c, typeof(AnimationClip), false);
            }

            EditorGUILayout.Space();
            _size = EditorGUILayout.FloatField(new GUIContent("Real size (m)", "Largest of body length and height; wingspan for a bird modelled wings-out."), _size);
            _yaw = EditorGUILayout.FloatField(new GUIContent("Yaw correction (°)", "If the model doesn't face +Z, rotate it here (often 90, -90 or 180)."), _yaw);

            using (new EditorGUI.DisabledScope(_species == null || _model == null || Application.isPlaying))
                if (GUILayout.Button("Create prefab & assign to species", GUILayout.Height(30))) Create();
            if (_species != null && _species.prefab != null && GUILayout.Button("Revert species to placeholder body"))
            {
                Undo.RecordObject(_species, "Revert species prefab");
                _species.prefab = null;
                EditorUtility.SetDirty(_species);
            }
            if (!string.IsNullOrEmpty(_log)) EditorGUILayout.HelpBox(_log, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private IEnumerable<AnimationClip> AllClips()
        {
            var sources = new List<Object> { _model };
            sources.AddRange(_extraClipSources);
            foreach (var src in sources)
            {
                if (src == null) continue;
                if (src is AnimationClip direct) { yield return direct; continue; }
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(src)))
                    if (a is AnimationClip clip && !clip.name.StartsWith("__preview__")) yield return clip;
            }
        }

        private void Classify()
        {
            _clips.Clear();
            var sb = new System.Text.StringBuilder();
            foreach (var clip in AllClips())
            {
                string n = clip.name.ToLowerInvariant();
                foreach (var (role, words) in Keywords)
                {
                    bool hit = false;
                    foreach (var w in words) if (n.Contains(w)) { hit = true; break; }
                    if (!hit) continue;
                    if (!_clips.ContainsKey(role) || _clips[role] == null) _clips[role] = clip;
                    sb.Append(clip.name).Append(" → ").Append(role).Append('\n');
                    break;
                }
            }
            if (!_clips.ContainsKey(Role.Idle) || _clips[Role.Idle] == null)
                foreach (var clip in AllClips()) { _clips[Role.Idle] = clip; break; } // something rather than T-pose
            _log = sb.Length > 0 ? "Classified:\n" + sb : "No clips recognised by name: assign them by hand.";
        }

        private AnimationClip Clip(Role r, params Role[] fallbacks)
        {
            if (_clips.TryGetValue(r, out var c) && c != null) return c;
            foreach (var f in fallbacks) if (_clips.TryGetValue(f, out c) && c != null) return c;
            return null;
        }

        private void Create()
        {
            string folder = GeneratedAssets.Root + "/Animals";
            GeneratedAssets.EnsureFolder(folder);
            string id = string.IsNullOrEmpty(_species.speciesId) ? _species.name : _species.speciesId;
            var controller = BuildController($"{folder}/{id}_Animator.controller");

            // Wrapper root (what the spawner instantiates and the AI moves) → yaw-corrected, scaled model.
            var root = new GameObject(_species.displayName);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(_model);
            if (model == null) model = Instantiate(_model);
            model.transform.SetParent(root.transform, false);
            model.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);

            var bounds = RendererBounds(model);
            float measured = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = measured > 0.0001f ? _size / measured : 1f;
            model.transform.localScale = model.transform.localScale * scale;
            bounds = RendererBounds(model);
            // Feet on the root's origin, centred over it.
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // the AI moves the animal; clips only animate the body
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            string prefabPath = $"{folder}/{id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);

            Undo.RecordObject(_species, "Assign species prefab");
            _species.prefab = prefab;
            EditorUtility.SetDirty(_species);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(prefab);
            _log = $"Created {prefabPath} (scale {scale:0.###}) and {Path.GetFileName(AssetDatabase.GetAssetPath(controller))}; " +
                   $"assigned to {_species.displayName}. Press Play: the species now spawns with this model.\n" + _log;
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

        private AnimatorController BuildController(string path)
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
            float walk = Mathf.Max(0.3f, _species.walkSpeed), run = Mathf.Max(walk * 2f, _species.runSpeed);
            var idle = Clip(Role.Idle, Role.Look);
            if (idle != null) tree.AddChild(idle, 0f);
            if (Clip(Role.Walk) != null) tree.AddChild(Clip(Role.Walk), walk);
            if (Clip(Role.Trot) != null) tree.AddChild(Clip(Role.Trot), Mathf.Lerp(walk, run, 0.4f));
            if (Clip(Role.Run) != null) tree.AddChild(Clip(Role.Run), run);
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
                { AnimalActivity.Grazing, StateFor(Clip(Role.Eat)) },
                { AnimalActivity.Foraging, StateFor(Clip(Role.Eat)) },
                { AnimalActivity.Sniffing, StateFor(Clip(Role.Eat, Role.Look)) },
                { AnimalActivity.LookingAround, StateFor(Clip(Role.Look)) },
                { AnimalActivity.Alert, StateFor(Clip(Role.Look)) },
                { AnimalActivity.Grooming, StateFor(Clip(Role.Groom, Role.Sit)) },
                { AnimalActivity.Resting, StateFor(Clip(Role.Lie, Role.Sit)) },
                { AnimalActivity.Sitting, StateFor(Clip(Role.Sit, Role.Lie)) },
                { AnimalActivity.Perched, StateFor(Clip(Role.Idle)) },
                { AnimalActivity.Flying, StateFor(Clip(Role.Fly)) },
                { AnimalActivity.Soaring, StateFor(Clip(Role.Glide, Role.Fly)) },
                { AnimalActivity.Gliding, StateFor(Clip(Role.Glide, Role.Fly)) },
                { AnimalActivity.Landing, StateFor(Clip(Role.Land, Role.Fly)) },
                { AnimalActivity.TakingOff, StateFor(Clip(Role.TakeOff, Role.Fly)) },
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
