using System.Collections.Generic;
using UnityEditor;
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
        private SpeciesDefinition _species;
        private GameObject _model;
        private readonly List<Object> _extraClipSources = new List<Object>();
        private readonly Dictionary<ClipRole, AnimationClip> _clips = new Dictionary<ClipRole, AnimationClip>();
        private float _size = 1.9f, _yaw;
        private bool _tint;
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
            if (EditorGUI.EndChangeCheck() && _species != null) _size = AnimalPrefabFactory.DefaultSize(_species);
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
            foreach (ClipRole r in System.Enum.GetValues(typeof(ClipRole)))
            {
                _clips.TryGetValue(r, out var c);
                _clips[r] = (AnimationClip)EditorGUILayout.ObjectField(r.ToString(), c, typeof(AnimationClip), false);
            }

            EditorGUILayout.Space();
            _size = EditorGUILayout.FloatField(new GUIContent("Real size (m)", "Mammals: the larger of nose-to-tail length and height. Birds: beak to tail."), _size);
            _yaw = EditorGUILayout.FloatField(new GUIContent("Yaw correction (°)", "If the model doesn't face +Z, rotate it here (often 90, -90 or 180)."), _yaw);
            _tint = EditorGUILayout.Toggle(new GUIContent("Tint to species colours", "For a stand-in (one songbird model as a cardinal): copies its materials in the species' colours."), _tint);

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

        private void Classify()
        {
            var sources = new List<Object> { _model };
            sources.AddRange(_extraClipSources);
            var sb = new System.Text.StringBuilder();
            _clips.Clear();
            foreach (var kv in AnimalPrefabFactory.Classify(AnimalPrefabFactory.ClipsIn(sources), sb)) _clips[kv.Key] = kv.Value;
            _log = sb.Length > 0 ? "Classified:\n" + sb : "No clips recognised by name: assign them by hand.";
        }

        private void Create()
        {
            var prefab = AnimalPrefabFactory.Create(_species, _model, _clips, _size, _yaw, _tint, out string message);
            EditorGUIUtility.PingObject(prefab);
            _log = "Created " + message + ". Press Play: the species now spawns with this model.\n" + _log;
        }
    }
}
