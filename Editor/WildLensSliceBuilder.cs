using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WildLens.CameraSystem;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;
using WildLens.Photography;
using WildLens.Player;
using WildLens.UI;
using WildLens.Wildlife;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Builds the playable vertical slice into the open scene on top of its existing Terrain:
    /// trail loop, scenery, lighting/atmosphere, player rig, game systems and species data.
    /// Everything lives under one root object so it can be rebuilt or removed cleanly; the terrain's own
    /// data is never modified (a fallback terrain is created only if the scene has none).
    /// </summary>
    public static class WildLensSliceBuilder
    {
        public const string RootName = "WildLens Vertical Slice";
        public const string SettingsPath = GeneratedAssets.DataFolder + "/SliceBuildSettings.asset";
        public const string DatabasePath = GeneratedAssets.DataFolder + "/SpeciesDatabase.asset";

        public static SliceBuildSettings LoadOrCreateSettings()
        {
            GeneratedAssets.EnsureFolders();
            var s = AssetDatabase.LoadAssetAtPath<SliceBuildSettings>(SettingsPath);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<SliceBuildSettings>();
            AssetDatabase.CreateAsset(s, SettingsPath);
            AssetDatabase.SaveAssets();
            return s;
        }

        public static GameObject FindExistingRoot()
        {
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == RootName) return go;
            return null;
        }

        public static void Remove()
        {
            var root = FindExistingRoot();
            if (root != null) Undo.DestroyObjectImmediate(root);
        }

        public static void Build(SliceBuildSettings s)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build WildLens Vertical Slice");

            try
            {
                GeneratedAssets.EnsureFolders();
                Remove();

                Progress("Species data", 0.05f);
                var database = EnsureSpeciesAssets();

                Progress("Terrain", 0.1f);
                var terrain = FindOrCreateTerrain(s);

                var root = new GameObject(RootName);
                var env = Child(root.transform, "Environment");

                Progress("Trail loop", 0.15f);
                var loop = TrailBuilder.Generate(terrain, s);
                var trail = TrailBuilder.CreateTrailObject(loop, s, env);

                Progress("Trail props", 0.2f);
                PropBuilder.Build(trail, loop, Child(env, "Trail Props"));

                var veg = new VegetationBuilder(terrain, trail, loop, s);
                Progress("Trees", 0.3f);
                veg.BuildTrees(Child(env, "Trees"));
                Progress("Rocks & logs", 0.5f);
                veg.BuildRocks(Child(env, "Rocks"));
                veg.BuildLogs(Child(env, "Logs"));
                Progress("Bushes", 0.6f);
                veg.BuildBushes(Child(env, "Bushes"));
                Progress("Grass", 0.7f);
                veg.BuildGrass(Child(env, "Grass"));

                Progress("Lighting", 0.85f);
                if (s.configureLightingAndFog) ConfigureLighting(root.transform);

                Progress("Player & systems", 0.9f);
                var player = BuildPlayer(root.transform, trail, s);
                BuildSystems(root.transform, database, trail, player);

                Undo.RegisterCreatedObjectUndo(root, "Build WildLens Vertical Slice");
                Undo.CollapseUndoOperations(undoGroup);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Selection.activeGameObject = root;

                Debug.Log($"[WildLens] Vertical slice built on '{terrain.name}': trail {trail.Length:0} m, " +
                          $"{veg.TreesPlaced} trees, {veg.BushesPlaced} bushes, {veg.RocksPlaced} rocks, {veg.GrassPlaced} grass tufts. " +
                          "Save the scene, then press Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void Progress(string step, float t) =>
            EditorUtility.DisplayProgressBar("WildLens", "Building vertical slice: " + step + "…", t);

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ------------------------------------------------------------------ data

        public static SpeciesDatabase EnsureSpeciesAssets()
        {
            GeneratedAssets.EnsureFolders();
            var db = AssetDatabase.LoadAssetAtPath<SpeciesDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<SpeciesDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            // Existing species assets are kept as-is so designer tuning survives rebuilds.
            var defaults = SpeciesDefaults.CreateAll();
            var ordered = new List<SpeciesDefinition>();
            foreach (var def in defaults)
            {
                string path = $"{GeneratedAssets.SpeciesFolder}/{def.name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>(path);
                if (asset == null)
                {
                    AssetDatabase.CreateAsset(def, path);
                    asset = def;
                }
                else Object.DestroyImmediate(def);
                ordered.Add(asset);
            }
            foreach (var extra in db.species)
                if (extra != null && !ordered.Contains(extra)) ordered.Add(extra);

            db.species = ordered;
            EditorUtility.SetDirty(db);
            return db;
        }

        // ------------------------------------------------------------------ terrain

        private static Terrain FindOrCreateTerrain(SliceBuildSettings s)
        {
            var terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : SceneQuery.FindOne<Terrain>();
            if (terrain != null && terrain.terrainData != null) return terrain;

            Debug.LogWarning("[WildLens] No Terrain found in the scene; creating a fallback terrain.");
            var data = new TerrainData { heightmapResolution = 257 };
            data.size = s.fallbackTerrainSize;
            int res = data.heightmapResolution;
            var heights = new float[res, res];
            float o = s.seed * 3.1f;
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1), v = z / (float)(res - 1);
                float rolling = Mathf.PerlinNoise(o + u * 3f, o + v * 3f) * 0.5f + Mathf.PerlinNoise(o + u * 9f, o + v * 9f) * 0.12f;
                float rim = Mathf.Clamp01((Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) - 0.35f) * 3f);
                heights[z, x] = 0.12f + rolling * 0.35f + rim * rim * 0.35f;
            }
            data.SetHeights(0, 0, heights);

            var grassTex = GeneratedAssets.LoadOrCreate("T_TerrainGrass", () => GeneratedAssets.NoiseTexture(256,
                new Color(0.3f, 0.42f, 0.18f), new Color(0.42f, 0.5f, 0.24f), 8f, 11, 0.02f, new Color(0.5f, 0.45f, 0.3f)));
            var layer = new TerrainLayer { diffuseTexture = grassTex, tileSize = new Vector2(6f, 6f) };
            AssetDatabase.CreateAsset(layer, GeneratedAssets.Folder + "/TL_Grass.terrainlayer");
            data.terrainLayers = new[] { layer };
            GeneratedAssets.Save(data, "FallbackTerrainData");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain (WildLens fallback)";
            go.transform.position = new Vector3(-s.fallbackTerrainSize.x * 0.5f, 0f, -s.fallbackTerrainSize.z * 0.5f);
            Undo.RegisterCreatedObjectUndo(go, "Create fallback terrain");
            return go.GetComponent<Terrain>();
        }

        // ------------------------------------------------------------------ lighting

        private static void ConfigureLighting(Transform root)
        {
            Light sun = null;
            foreach (var l in SceneQuery.FindAll<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                var go = new GameObject("Sun");
                go.transform.SetParent(root, false);
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            else Undo.RecordObjects(new Object[] { sun, sun.transform }, "Configure sun");

            // Warm mid-morning light, low enough for long soft shadows.
            sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;

            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0045f;
            RenderSettings.fogColor = new Color(0.7f, 0.77f, 0.75f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.27f, 0.22f);

            if (RenderSettings.skybox == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader != null)
                {
                    string path = GeneratedAssets.Folder + "/M_Sky.mat";
                    var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (sky == null)
                    {
                        sky = new Material(shader);
                        sky.SetFloat("_SunSize", 0.04f);
                        sky.SetFloat("_AtmosphereThickness", 0.9f);
                        sky.SetColor("_SkyTint", new Color(0.55f, 0.65f, 0.75f));
                        sky.SetFloat("_Exposure", 1.2f);
                        AssetDatabase.CreateAsset(sky, path);
                    }
                    RenderSettings.skybox = sky;
                }
            }
        }

        // ------------------------------------------------------------------ player & systems

        private static PhotoCamera BuildPlayer(Transform root, TrailPath trail, SliceBuildSettings s)
        {
            if (s.disableOtherCameras)
            {
                foreach (var cam in SceneQuery.FindAll<Camera>())
                {
                    if (!cam.enabled || cam.gameObject.scene != EditorSceneManager.GetActiveScene()) continue;
                    Undo.RecordObject(cam, "Disable camera");
                    cam.enabled = false;
                    var listener = cam.GetComponent<AudioListener>();
                    if (listener != null) { Undo.RecordObject(listener, "Disable listener"); listener.enabled = false; }
                    Debug.Log($"[WildLens] Disabled existing camera '{cam.name}' in favour of the player camera.");
                }
            }

            Vector3 start = TerrainUtil.Ground(trail.GetPoint(0f)) + Vector3.up * 0.1f;
            Vector3 dir = trail.GetDirection(0f);

            var player = new GameObject("Player");
            player.transform.SetParent(root, false);
            player.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir));
            player.tag = "Player";

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f; cc.stepOffset = 0.4f; cc.skinWidth = 0.05f;

            var pivot = new GameObject("Camera Pivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.65f, 0f);

            var camGo = new GameObject("Player Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot, false);
            var camera = camGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1200f;
            camera.fieldOfView = 65f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();

            // Camera system: lens (FOV/zoom), camera mode, shutter, sounds, physical motion.
            camGo.AddComponent<ZoomLens>();
            camGo.AddComponent<CameraModeController>();
            var photo = camGo.AddComponent<PhotoCamera>();
            camGo.AddComponent<CameraAudio>();
            var rig = camGo.AddComponent<PlayerCameraRig>();
            var controller = player.AddComponent<FirstPersonController>();
            controller.Configure(pivot, rig);
            rig.Configure(controller);
            photo.Configure(camera, player.transform);
            return photo;
        }

        private static void BuildSystems(Transform root, SpeciesDatabase db, TrailPath trail, PhotoCamera photo)
        {
            var go = new GameObject("WildLens Systems");
            go.transform.SetParent(root, false);

            var game = go.AddComponent<WildLensGame>();
            var so = new SerializedObject(game);
            so.FindProperty("speciesDatabase").objectReferenceValue = db;
            so.ApplyModifiedPropertiesWithoutUndo();

            var world = go.AddComponent<WildlifeWorld>();
            world.SetTrail(trail);
            var spawner = go.AddComponent<WildlifeSpawner>();
            spawner.Configure(db, world);

            var hud = go.AddComponent<HudController>();
            hud.Configure(photo);
            go.AddComponent<JournalUI>();
        }
    }
}
