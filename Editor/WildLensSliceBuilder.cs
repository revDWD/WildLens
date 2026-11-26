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
#if WILDLENS_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

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
                var database = EnsureSpeciesAssets(s.previewExpansionSpecies);

                Progress("Terrain", 0.1f);
                var terrain = FindOrCreateTerrain(s);

                var root = new GameObject(RootName);
                var info = root.AddComponent<WildLens.Environment.SliceInfo>();
                info.packageVersion = WildLensVersion.Package;
                info.environmentVersion = WildLensVersion.Environment;
                info.scannedArt = s.useScannedArt && ArtLibrary.Available;
                var env = Child(root.transform, "Environment");

                bool ownsTerrain = IsWildLensTerrain(terrain);
                bool hdrp = PipelineName().Contains("HDRenderPipeline");
                // When the terrain is painted finely enough, the worn trail lives in the terrain texture.
                float texel = ownsTerrain ? terrain.terrainData.size.x / (terrain.terrainData.size.x > 500f ? 2048f : 1024f) : terrain.terrainData.size.x / terrain.terrainData.alphamapResolution;
                bool trailMesh = !s.paintTerrain || texel > 0.6f;

                Progress("Trail loop", 0.15f);
                var loop = TrailBuilder.Generate(terrain, s);
                var trail = TrailBuilder.CreateTrailObject(loop, s, env, trailMesh);

                Progress("Trail props", 0.2f);
                PropBuilder.Build(trail, loop, Child(env, "Trail Props"), s.trailBike);

                var veg = new VegetationBuilder(terrain, trail, loop, s);
                Transform reserve = null;
                if (s.buildReserve)
                {
                    Progress("Reserve: destinations & tracks", 0.25f);
                    reserve = Child(env, "Reserve");
                    veg.PlanReserve(reserve, s.shapeTerrainAtDestinations, trailMesh);
                }
                foreach (var k in PropBuilder.Footprints) veg.AddKeepOut(k.position, k.radius);
                Progress("Trees", 0.3f);
                veg.BuildTrees(Child(env, "Trees"));
                Progress("Rocks & logs", 0.5f);
                veg.BuildRocks(Child(env, "Rocks"));
                veg.BuildLogs(Child(env, "Logs"));
                Progress("Bushes", 0.6f);
                veg.BuildBushes(Child(env, "Bushes"));
                Progress("Habitat hotspots", 0.65f);
                veg.BuildHotspots(Child(env, "Habitat"));
                if (s.scenicViewpoints)
                {
                    Progress("Scenic viewpoints", 0.68f);
                    var viewpoints = Child(env, "Viewpoints");
                    veg.BuildViewpoints(viewpoints, loop);
                    veg.BuildCompositions(viewpoints);
                }
                if (reserve != null)
                {
                    Progress("Reserve: destinations", 0.69f);
                    veg.BuildDestinations(reserve);
                    Progress("Reserve: creek, pull-offs, signs", 0.695f);
                    veg.BuildEcosystem(reserve);
                }
                Progress("Undergrowth", 0.7f);
                veg.BuildUndergrowth(Child(env, "Undergrowth"));

                bool details = s.paintTerrain && s.terrainDetailGrass && !hdrp;
                if (s.paintTerrain) veg.PaintTerrain(details, s.backupTerrainData, ownsTerrain);
                if (!details)
                {
                    Progress("Grass", 0.82f);
                    veg.BuildGrass(Child(env, "Grass"));
                }
                if (s.wind) BuildWind(env);

                Progress("Lighting", 0.85f);
                if (s.configureLightingAndFog) ConfigureLighting(root.transform, s);
                if (s.configureLightingAndFog && s.timeOfDay) BuildDayCycle(root.transform, s);
                if (s.configureRenderQuality) ConfigureRenderQuality();
                if (s.postProcessing) BuildPostProcessing(root.transform, s);

                Progress("Player & systems", 0.9f);
                var player = BuildPlayer(root.transform, trail, s);
                BuildSystems(root.transform, database, trail, player);

                Undo.RegisterCreatedObjectUndo(root, "Build WildLens Vertical Slice");
                Undo.CollapseUndoOperations(undoGroup);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Selection.activeGameObject = root;

                Debug.Log($"[WildLens] Vertical slice built on '{terrain.name}': trail {trail.Length:0} m, " +
                          $"{veg.TreesPlaced} trees, {veg.BushesPlaced} bushes, {veg.RocksPlaced} rocks, {veg.HotspotsPlaced} habitat hotspots, " +
                          (veg.KitTreesPlaced + veg.KitPropsPlaced > 0 ? $"Environment Kit: {veg.KitTreesPlaced} trees, {veg.KitPropsPlaced} props; " : "") +
                          $"{veg.ViewpointsPlaced} viewpoints, {veg.DestinationsPlaced} reserve destinations ({veg.TrackLength / 1000f:0.0} km of tracks, creek {veg.CreekLength:0} m), terrain {(s.paintTerrain ? "painted" : "untouched")}, grass: {(details ? "terrain details (wind)" : veg.GrassPlaced + " mesh tufts")}. " +
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

        public static SpeciesDatabase EnsureSpeciesAssets() => EnsureSpeciesAssets(LoadOrCreateSettings().previewExpansionSpecies);

        /// <param name="includePreview">Also create and include the 1.0 expansion examples (raccoon, heron).</param>
        public static SpeciesDatabase EnsureSpeciesAssets(bool includePreview)
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
            var preview = SpeciesDefaults.CreateExpansionPreview();
            var previewIds = new HashSet<string>();
            foreach (var p in preview) previewIds.Add(p.speciesId);
            if (includePreview) defaults.AddRange(preview);
            else foreach (var p in preview) Object.DestroyImmediate(p);
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
                if (extra != null && !ordered.Contains(extra) && (includePreview || !previewIds.Contains(extra.speciesId))) ordered.Add(extra);

            db.species = ordered;
            EditorUtility.SetDirty(db);
            return db;
        }

        // ------------------------------------------------------------------ terrain

        private static Vector3 FallbackSize(SliceBuildSettings s)
        {
            Vector3 size = s.fallbackTerrainSize;
            if (!s.buildReserve) return size;
            // The reserve needs room to travel: grow our own terrain (never the user's).
            float side = Mathf.Max(size.x, size.z, s.reserveTerrainSize);
            return new Vector3(side, Mathf.Max(size.y, 60f), side);
        }

        private static Terrain FindOrCreateTerrain(SliceBuildSettings s)
        {
            var terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : SceneQuery.FindOne<Terrain>();
            Vector3 size = FallbackSize(s);
            if (terrain != null && terrain.terrainData != null)
            {
                if (IsWildLensTerrain(terrain) && terrain.terrainData.size.x < size.x - 1f)
                {
                    Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Grow WildLens terrain");
                    Undo.RecordObject(terrain.transform, "Grow WildLens terrain");
                    GenerateFallbackHeights(terrain.terrainData, size, s);
                    terrain.transform.position = new Vector3(-size.x * 0.5f, 0f, -size.z * 0.5f);
                    Debug.Log($"[WildLens] Grew the WildLens fallback terrain to {size.x:0} m for the reserve.");
                }
                return terrain;
            }

            Debug.LogWarning("[WildLens] No Terrain found in the scene; creating a fallback terrain.");
            var data = new TerrainData();
            GenerateFallbackHeights(data, size, s);

            var grassTex = GeneratedAssets.LoadOrCreate("T_TerrainGrass", () => GeneratedAssets.NoiseTexture(256,
                new Color(0.3f, 0.42f, 0.18f), new Color(0.42f, 0.5f, 0.24f), 8f, 11, 0.02f, new Color(0.5f, 0.45f, 0.3f)));
            var layer = new TerrainLayer { diffuseTexture = grassTex, tileSize = new Vector2(6f, 6f) };
            AssetDatabase.CreateAsset(layer, GeneratedAssets.Folder + "/TL_Grass.terrainlayer");
            data.terrainLayers = new[] { layer };
            GeneratedAssets.Save(data, "FallbackTerrainData");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain (WildLens fallback)";
            go.transform.position = new Vector3(-size.x * 0.5f, 0f, -size.z * 0.5f);
            Undo.RegisterCreatedObjectUndo(go, "Create fallback terrain");
            return go.GetComponent<Terrain>();
        }

        /// <summary>Rolling country rising to a rim at the edges (which keeps the world enclosed).</summary>
        private static void GenerateFallbackHeights(TerrainData data, Vector3 size, SliceBuildSettings s)
        {
            data.heightmapResolution = size.x > 500f ? 513 : 257;
            data.size = size;
            int res = data.heightmapResolution;
            var heights = new float[res, res];
            float o = s.seed * 3.1f;
            float scale = size.x / 400f; // keep hill sizes similar on a larger terrain
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float u = x / (float)(res - 1), v = z / (float)(res - 1);
                float rolling = Mathf.PerlinNoise(o + u * 3f * scale, o + v * 3f * scale) * 0.5f +
                                Mathf.PerlinNoise(o + u * 9f * scale, o + v * 9f * scale) * 0.12f;
                float rim = Mathf.Clamp01((Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) - 0.4f) * 4f);
                heights[z, x] = 0.12f + rolling * 0.3f + rim * rim * 0.3f;
            }
            data.SetHeights(0, 0, heights);
        }

        // ------------------------------------------------------------------ lighting

        /// <summary>
        /// Late-morning light: a warm sun high enough to read well but low enough for long soft shadows, sky-matched
        /// ambient, gentle exponential haze for atmospheric perspective, and a tuned procedural sky.
        /// </summary>
        private static string SkyAsset(SkyMood mood) =>
            mood == SkyMood.GoldenHour ? "kloppenheim_06_puresky" : "kloofendal_48d_partly_cloudy_puresky";

        private static void ConfigureLighting(Transform root, SliceBuildSettings s)
        {
            if (s.useScannedArt && s.skyMood != SkyMood.Procedural && ArtLibrary.Import())
            {
                var info = ArtLibrary.Sky(SkyAsset(s.skyMood));
                var sky = info != null ? ArtLibrary.SkyMaterial(info.name, s.skyMood == SkyMood.GoldenHour ? 1.1f : 1f) : null;
                if (sky != null)
                {
                    ConfigureHdriLighting(root, s.skyMood, info, sky);
                    return;
                }
                Debug.LogWarning("[WildLens] HDRI sky not found; using the procedural sky.");
            }
            ConfigureProceduralLighting(root);
        }

        /// <summary>
        /// The reserve's day (0.9): a DayCycle driving the sun, both sky images (morning and golden hour, rotated to the
        /// sun), fog, ambient light and grading through dawn, morning, midday, golden hour and dusk.
        /// </summary>
        private static void BuildDayCycle(Transform root, SliceBuildSettings s)
        {
            var go = new GameObject("Day Cycle");
            go.transform.SetParent(root, false);
            var day = go.AddComponent<DayCycle>();
            day.startHour = s.startHour;
            day.minutesPerHour = s.minutesPerHour;
            day.sun = FindOrCreateSun(root);
            if (s.useScannedArt && ArtLibrary.Import())
            {
                var morning = ArtLibrary.Sky(SkyAsset(SkyMood.LateMorning));
                var golden = ArtLibrary.Sky(SkyAsset(SkyMood.GoldenHour));
                if (morning != null)
                {
                    day.morningSky = ArtLibrary.SkyMaterial(morning.name, 1f);
                    day.morningSkySunAzimuth = Mathf.Atan2(morning.SunDirection.x, morning.SunDirection.z) * Mathf.Rad2Deg;
                    day.morningFog = SkyInfo.ToColor(morning.fogColor);
                }
                if (golden != null)
                {
                    day.goldenSky = ArtLibrary.SkyMaterial(golden.name, 1.1f);
                    day.goldenSkySunAzimuth = Mathf.Atan2(golden.SunDirection.x, golden.SunDirection.z) * Mathf.Rad2Deg;
                    day.goldenFog = SkyInfo.ToColor(golden.fogColor);
                }
            }
            Debug.Log($"[WildLens] Time of day on: starts at {s.startHour:0.0}h, {s.minutesPerHour:0.##} real min per hour (hold T to let time pass).");
        }

        private static Light FindOrCreateSun(Transform root)
        {
            foreach (var l in SceneQuery.FindAll<Light>())
                if (l.type == LightType.Directional) { Undo.RecordObjects(new Object[] { l, l.transform }, "Configure sun"); return l; }
            var go = new GameObject("Sun");
            go.transform.SetParent(root, false);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            return sun;
        }

        /// <summary>
        /// HDRI lighting: the sky image is the skybox, the source of ambient light and reflections; the directional
        /// light is aimed exactly at the sun in the image (measured when the asset was added, see Art~/Sky/*.json) and
        /// tinted with its measured colour; fog takes the sky's measured horizon colour so distant hills melt into
        /// the same haze the sky shows.
        /// </summary>
        private static void ConfigureHdriLighting(Transform root, SkyMood mood, SkyInfo info, Material sky)
        {
            var sun = FindOrCreateSun(root);
            sun.transform.rotation = Quaternion.LookRotation(-info.SunDirection);
            Color sunColor = SkyInfo.ToColor(info.sunColor);
            bool golden = mood == SkyMood.GoldenHour;
            sun.color = golden ? Color.Lerp(sunColor, new Color(1f, 0.72f, 0.45f), 0.3f) : sunColor;
            sun.intensity = golden ? 1.05f : 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = golden ? 0.75f : 0.85f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = golden ? 1.1f : 1f;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.9f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = golden ? 0.0034f : 0.0024f;
            RenderSettings.fogColor = SkyInfo.ToColor(info.fogColor);
            DynamicGI.UpdateEnvironment();
            Debug.Log($"[WildLens] Sky: {info.name} (sun {info.sunElevation:0.0}° high, azimuth {info.sunAzimuth:0}°); sun light aligned to the sky, fog matched to the horizon.");
            WarnGamma();
        }

        private static void WarnGamma()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                Debug.LogWarning("[WildLens] Project uses Gamma colour space. Linear (Project Settings > Player > Other) gives far better lighting and grading; changing it reimports textures.");
        }

        private static void ConfigureProceduralLighting(Transform root)
        {
            var sun = FindOrCreateSun(root);
            sun.transform.rotation = Quaternion.Euler(30f, -38f, 0f);
            sun.color = new Color(1f, 0.92f, 0.8f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowNormalBias = 0.3f;

            RenderSettings.sun = sun;
            // Exponential (not squared) haze: distant ridges fade into a blue-grey, near ground stays crisp.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0028f;
            RenderSettings.fogColor = new Color(0.69f, 0.76f, 0.8f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.66f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.57f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.17f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;

            var shader = Shader.Find("Skybox/Procedural");
            if (shader != null && (RenderSettings.skybox == null || RenderSettings.skybox.name.StartsWith("Default") ||
                                   RenderSettings.skybox.name.StartsWith("M_Sky")))
            {
                string path = GeneratedAssets.Folder + "/M_Sky.mat";
                var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (sky == null)
                {
                    sky = new Material(shader);
                    AssetDatabase.CreateAsset(sky, path);
                }
                sky.shader = shader;
                sky.SetFloat("_SunDisk", 2f); // high quality
                sky.SetFloat("_SunSize", 0.035f);
                sky.SetFloat("_SunSizeConvergence", 6f);
                sky.SetFloat("_AtmosphereThickness", 0.85f);
                sky.SetColor("_SkyTint", new Color(0.5f, 0.57f, 0.66f));
                sky.SetColor("_GroundColor", new Color(0.38f, 0.36f, 0.32f));
                sky.SetFloat("_Exposure", 1.25f);
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
            }
            DynamicGI.UpdateEnvironment();
            WarnGamma();
        }

        private static string PipelineName()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            return rp != null ? rp.GetType().Name : "Built-in";
        }

        private static bool IsWildLensTerrain(Terrain terrain)
        {
            string path = AssetDatabase.GetAssetPath(terrain.terrainData);
            return terrain.name.Contains("WildLens") || (!string.IsNullOrEmpty(path) && path.StartsWith(GeneratedAssets.Folder));
        }

        /// <summary>
        /// Shadow quality for an outdoor scene. Built-in: Quality settings. SRPs: properties on the pipeline asset set by
        /// reflection (names differ between versions; anything missing is skipped). Project-wide, so logged.
        /// </summary>
        private static void ConfigureRenderQuality()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp == null)
            {
                QualitySettings.shadowDistance = 160f;
                QualitySettings.shadowCascades = 4;
                QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
                QualitySettings.shadowProjection = UnityEngine.ShadowProjection.StableFit;
                QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                Debug.Log("[WildLens] Quality settings: shadow distance 160 m, 4 cascades, very high resolution.");
                return;
            }

            var changed = new List<string>();
            void Set(string property, object value)
            {
                var prop = rp.GetType().GetProperty(property);
                if (prop == null || !prop.CanWrite) return;
                try { prop.SetValue(rp, value); changed.Add(property + "=" + value); }
                catch (System.Exception) { /* value type differs in this version; skip */ }
            }
            Undo.RecordObject(rp, "Configure render quality");
            Set("shadowDistance", 160f);
            Set("shadowCascadeCount", 4);
            Set("mainLightShadowmapResolution", 2048);
            Set("supportsSoftShadows", true);
            Set("supportsHDR", true);
            Set("supportsCameraDepthTexture", true);
            EditorUtility.SetDirty(rp);
            Debug.Log($"[WildLens] {rp.name}: " + (changed.Count > 0 ? string.Join(", ", changed) : "no adjustable shadow properties found"));
        }

        private static void BuildWind(Transform env)
        {
            var go = new GameObject("Wind");
            go.transform.SetParent(env, false);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(1f, 0f, 0.35f));
            var zone = go.AddComponent<WindZone>();
            zone.mode = WindZoneMode.Directional;
            zone.windMain = 0.6f;
            zone.windTurbulence = 0.3f;
            zone.windPulseMagnitude = 0.4f;
            zone.windPulseFrequency = 0.08f;
            go.AddComponent<WildLensWind>();
        }

        /// <summary>
        /// URP: a global grading volume: ACES tonemapping, slightly warm white balance, a touch of contrast and
        /// saturation, gentle bloom on sun highlights, cool shadows and warm highlights, faint vignette.
        /// </summary>
        private static void BuildPostProcessing(Transform root, SliceBuildSettings s)
        {
            bool golden = s.useScannedArt && s.skyMood == SkyMood.GoldenHour;
#if WILDLENS_URP
            string path = GeneratedAssets.Folder + "/WildLensGrade.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>();
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var colour = Add<ColorAdjustments>();
            colour.postExposure.Override(golden ? 0.35f : 0.2f);
            colour.contrast.Override(8f);
            colour.saturation.Override(6f);
            var wb = Add<WhiteBalance>();
            wb.temperature.Override(golden ? 14f : 5f);
            wb.tint.Override(2f);
            var smh = Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.97f, 1f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1f, 0.95f, 0f));
            var bloom = Add<Bloom>();
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(golden ? 0.5f : 0.3f);
            bloom.scatter.Override(0.65f);
            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.4f);
            EditorUtility.SetDirty(profile);

            var go = new GameObject("Post Processing (Grade)");
            go.transform.SetParent(root, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
#else
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
                Debug.Log("[WildLens] Built-in pipeline: colour grading needs URP (or the Post Processing package). Lighting, fog and sky are configured.");
#endif
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
            camera.allowHDR = true;
#if WILDLENS_URP
            var cameraData = camera.GetUniversalAdditionalCameraData();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
#endif

            // Camera system: lens (FOV/zoom), camera mode, shutter, sounds, physical motion.
            camGo.AddComponent<ZoomLens>();
            camGo.AddComponent<CameraModeController>();
            var photo = camGo.AddComponent<PhotoCamera>();
            camGo.AddComponent<FramingAssistant>();
            camGo.AddComponent<FocusController>();
            camGo.AddComponent<LensEffects>();
            camGo.AddComponent<WildLens.Debugging.CameraDiagnostics>();
            camGo.AddComponent<WildLens.Observation.ObservationTracker>();
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
            go.AddComponent<WildlifeSchedule>(); // daily rhythms (idle without a DayCycle)

            var hud = go.AddComponent<HudController>();
            hud.Configure(photo);
            go.AddComponent<JournalUI>();
        }
    }
}
