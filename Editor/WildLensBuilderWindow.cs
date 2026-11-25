using System.IO;
using UnityEditor;
using UnityEngine;
using WildLens.Photography;

namespace WildLens.EditorTools
{
    /// <summary>WildLens ▸ Vertical Slice Builder: one place to (re)build, tune and reset the slice.</summary>
    public class WildLensBuilderWindow : EditorWindow
    {
        private SliceBuildSettings _settings;
        private Editor _settingsEditor;
        private Vector2 _scroll;

        [MenuItem("WildLens/Vertical Slice Builder", priority = 0)]
        public static void Open() => GetWindow<WildLensBuilderWindow>("WildLens Builder");

        [MenuItem("WildLens/Build Vertical Slice In Open Scene", priority = 1)]
        public static void QuickBuild()
        {
            if (Application.isPlaying) { Debug.LogWarning("[WildLens] Exit Play mode before building."); return; }
            WildLensSliceBuilder.Build(WildLensSliceBuilder.LoadOrCreateSettings());
        }

        [MenuItem("WildLens/Reset Journal Save Data", priority = 20)]
        public static void ResetSave()
        {
            string dir = Path.Combine(Application.persistentDataPath, "WildLens");
            string journal = Path.Combine(dir, "journal.json");
            if (!EditorUtility.DisplayDialog("Reset journal", "Delete the saved journal (photos on disk are kept)?\n\n" + journal, "Delete", "Cancel"))
                return;
            if (File.Exists(journal)) File.Delete(journal);
            Debug.Log("[WildLens] Journal save deleted.");
        }

        [MenuItem("WildLens/Open Photos Folder", priority = 21)]
        public static void OpenPhotos()
        {
            Directory.CreateDirectory(PhotoStorage.PhotoDirectory);
            EditorUtility.RevealInFinder(PhotoStorage.PhotoDirectory);
        }

        private void OnEnable()
        {
            _settings = WildLensSliceBuilder.LoadOrCreateSettings();
        }

        private void OnDisable()
        {
            if (_settingsEditor != null) DestroyImmediate(_settingsEditor);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("WildLens — Vertical Slice", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Open your terrain scene, then Build. Everything is created under one root object and can be rebuilt " +
                "or removed; the terrain's own data is not modified. Assign prefabs below to replace placeholder scenery.",
                MessageType.Info);

            if (_settings == null) _settings = WildLensSliceBuilder.LoadOrCreateSettings();
            Editor.CreateCachedEditor(_settings, null, ref _settingsEditor);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _settingsEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                bool exists = WildLensSliceBuilder.FindExistingRoot() != null;
                if (GUILayout.Button(exists ? "Rebuild Slice In Open Scene" : "Build Slice In Open Scene", GUILayout.Height(32)))
                    WildLensSliceBuilder.Build(_settings);
                using (new EditorGUI.DisabledScope(!exists))
                    if (GUILayout.Button("Remove Slice")) WildLensSliceBuilder.Remove();
                if (GUILayout.Button("Create / Update Species Assets Only"))
                {
                    var db = WildLensSliceBuilder.EnsureSpeciesAssets();
                    AssetDatabase.SaveAssets();
                    EditorGUIUtility.PingObject(db);
                }
            }
            EditorGUILayout.Space();
        }
    }
}
