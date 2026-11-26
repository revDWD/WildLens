using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WildLens.Core;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Terrain surface pass (part of <see cref="VegetationBuilder"/>: it reuses the forest field, trail, trees and
    /// meadow hotspots so the ground agrees with what grows on it).
    ///
    /// Texture layers, generated as tileable textures with matching normal maps:
    ///   0 grass · 1 meadow (sunlit, flower-flecked) · 2 forest floor (leaf litter, moss) · 3 worn dirt (trail) · 4 rock (slopes)
    /// Terrain details (Unity terrain grass, which waves in the wind in Built-in and URP):
    ///   short grass in the open, tall seed-headed grass and wildflowers in meadows, ferns under woodland,
    ///   sparse along trail edges, none on the tread or on rock.
    ///
    /// The terrain's existing TerrainData is copied to Generated/Backup before it is first painted.
    /// </summary>
    public partial class VegetationBuilder
    {
        private const string TerrainFolder = GeneratedAssets.Folder + "/Terrain";
        private const string TextureVersion = "v1";

        public bool UsedTerrainDetails { get; private set; }
        public float AlphamapTexelSize { get; private set; }

        // ------------------------------------------------------------------ entry point

        public void PaintTerrain(bool details, bool backup, bool ownsTerrain)
        {
            var data = _terrain.terrainData;
            if (backup && !ownsTerrain) BackupTerrainData(data);
            Undo.RegisterCompleteObjectUndo(data, "Paint WildLens terrain");

            if (ownsTerrain)
            {
                // Our own fallback terrain: raise resolutions for crisp trails and dense grass.
                data.alphamapResolution = data.size.x > 500f ? 2048 : 1024; // keep ~0.45 m texels on the larger reserve
                data.SetDetailResolution(1024, 32);
            }

            var trailField = new TrailDistanceField(PathDistance, _terrain);
            var trees = new TreeGrid(_treeObjects);

            EditorUtility.DisplayProgressBar("WildLens", "Generating terrain textures…", 0.72f);
            data.terrainLayers = CreateTerrainLayers();

            EditorUtility.DisplayProgressBar("WildLens", "Painting terrain…", 0.76f);
            PaintSplat(data, trailField, trees);

            if (details)
            {
                EditorUtility.DisplayProgressBar("WildLens", "Planting terrain grass, flowers and ferns…", 0.8f);
                PaintDetails(data, trailField, trees);
                UsedTerrainDetails = true;
            }

            // Quality: keep the full-resolution splat close up, smoother LOD, instanced drawing.
            _terrain.basemapDistance = Mathf.Max(_terrain.basemapDistance, 300f);
            _terrain.heightmapPixelError = Mathf.Min(_terrain.heightmapPixelError, 4f);
            _terrain.drawInstanced = true;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(_terrain);
        }

        private static void BackupTerrainData(TerrainData data)
        {
            string path = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(path)) return;
            GeneratedAssets.EnsureFolder(GeneratedAssets.Folder + "/Backup");
            string backup = $"{GeneratedAssets.Folder}/Backup/{data.name}_before_WildLens.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(backup) != null) return; // keep the original backup
            if (AssetDatabase.CopyAsset(path, backup))
                Debug.Log($"[WildLens] Backed up the terrain's original data to {backup} before painting.");
        }

        // ------------------------------------------------------------------ splat

        private void PaintSplat(TerrainData data, TrailDistanceField trail, TreeGrid trees)
        {
            int res = data.alphamapResolution;
            Vector3 origin = _terrain.transform.position, size = data.size;
            AlphamapTexelSize = size.x / res;
            float half = _s.trailWidth * 0.5f;
            var map = new float[res, res, 5];

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                var p = new Vector3(origin.x + (x + 0.5f) / res * size.x, 0f, origin.z + (z + 0.5f) / res * size.z);
                float nx = (x + 0.5f) / res, nz = (z + 0.5f) / res;
                float slope = data.GetSteepness(nx, nz);
                float n = Mathf.PerlinNoise(_noiseOffset + p.x * 0.12f, p.z * 0.12f);
                float broad = Mathf.PerlinNoise(_noiseOffset * 1.3f + p.x * 0.02f, p.z * 0.02f);

                float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 38f, slope + (n - 0.5f) * 6f));
                float d = trail.Sample(p) + (n - 0.5f) * 0.5f;
                float tread = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.7f, half + 0.9f, d));
                float worn = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half, half + 2.6f, d));
                float forest = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.62f, Forest(p))),
                    (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 5f, trees.Nearest(p, 6f)))) * 0.9f);
                float mid = Mathf.PerlinNoise(_noiseOffset * 0.7f + p.x * 0.045f, p.z * 0.045f + 31f);
                float meadow = Mathf.Max(MeadowMask(p, n), (1f - forest) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.78f, broad)) * 0.6f);
                // Mid-scale patches (~20 m) of the second grass tint break up visible texture repetition.
                meadow = Mathf.Max(meadow, (1f - forest) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.7f, mid)) * 0.55f);

                // Willow Creek (0.8): stony bed, exposed cut banks, a dark moist margin.
                float creek = CreekDistance(p) + (n - 0.5f) * 0.8f;
                if (creek < 10f)
                {
                    float bed = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1.8f, creek));
                    float bankCut = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 4f, creek))) * (1f - bed);
                    rock = Mathf.Max(rock, bed * 0.85f);
                    tread = Mathf.Max(tread, bankCut * 0.6f);
                    forest = Mathf.Max(forest, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 10f, creek)));
                }

                float wRock = rock;
                float rest = 1f - wRock;
                float wDirt = rest * Mathf.Max(tread, worn * 0.28f);
                rest -= wDirt;
                float wForest = rest * forest;
                rest -= wForest;
                float wMeadow = rest * meadow;
                float wGrass = rest - wMeadow;

                map[z, x, 0] = wGrass;
                map[z, x, 1] = wMeadow;
                map[z, x, 2] = wForest;
                map[z, x, 3] = wDirt;
                map[z, x, 4] = wRock;
            }
            data.SetAlphamaps(0, 0, map);
        }

        private float MeadowMask(Vector3 p, float noise)
        {
            float best = 0f;
            foreach (var m in _meadows)
            {
                float d = TerrainUtil.FlatDistance(p, m.center) / (m.radius * 1.15f) + (noise - 0.5f) * 0.25f;
                best = Mathf.Max(best, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, d)));
            }
            return best;
        }

        // ------------------------------------------------------------------ details

        private void PaintDetails(TerrainData data, TrailDistanceField trail, TreeGrid trees)
        {
            var protos = new[]
            {
                Detail(DetailTexture("Grass", DrawGrass), new Color(0.4f, 0.53f, 0.22f), new Color(0.58f, 0.55f, 0.3f), 0.45f, 0.8f, 0.22f, 0.42f),
                Detail(DetailTexture("MeadowGrass", DrawMeadowGrass), new Color(0.48f, 0.55f, 0.26f), new Color(0.68f, 0.6f, 0.34f), 0.5f, 0.9f, 0.38f, 0.62f),
                Detail(DetailTexture("FlowersYellow", (px, n) => DrawFlowers(px, n, new Color(0.98f, 0.82f, 0.22f))), Color.white, new Color(0.95f, 0.9f, 0.8f), 0.35f, 0.55f, 0.3f, 0.5f),
                Detail(DetailTexture("FlowersWhite", (px, n) => DrawFlowers(px, n, new Color(0.97f, 0.96f, 0.92f))), Color.white, new Color(0.95f, 0.93f, 0.85f), 0.35f, 0.55f, 0.3f, 0.48f),
                Detail(DetailTexture("Fern", DrawFern), new Color(0.32f, 0.48f, 0.2f), new Color(0.45f, 0.5f, 0.24f), 0.7f, 1.1f, 0.45f, 0.75f),
                Detail(DetailTexture("FlowersPurple", (px, n) => DrawFlowers(px, n, new Color(0.62f, 0.45f, 0.86f))), Color.white, new Color(0.92f, 0.88f, 0.9f), 0.35f, 0.55f, 0.32f, 0.52f),
            };
            for (int i = 0; i < protos.Length; i++) protos[i] = Modern(protos[i]);
            data.detailPrototypes = protos;
            SetInstanceCountScatter(data);

            int res = data.detailResolution;
            Vector3 origin = _terrain.transform.position, size = data.size;
            float cellArea = (size.x / res) * (size.z / res);
            float half = _s.trailWidth * 0.5f;
            var layers = new int[6][,];
            for (int i = 0; i < 6; i++) layers[i] = new int[res, res];

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                var p = new Vector3(origin.x + (x + 0.5f) / res * size.x, 0f, origin.z + (z + 0.5f) / res * size.z);
                float slope = data.GetSteepness((x + 0.5f) / res, (z + 0.5f) / res);
                if (slope > 34f) continue;
                float d = trail.Sample(p);
                if (d < half + 0.35f) continue; // bare tread
                if (InKeepOut(p)) continue;     // inside the blind, on its path, under structures
                float creekD = CreekDistance(p);
                if (creekD < 2.2f) continue;    // water
                float edge = Mathf.InverseLerp(half + 0.35f, half + 2.8f, d); // sparse, trampled edges
                float n = Mathf.PerlinNoise(_noiseOffset + p.x * 0.15f, p.z * 0.15f);
                float forest = Mathf.Max(Forest(p), (1f - Mathf.InverseLerp(1.5f, 5f, trees.Nearest(p, 6f))) * 0.8f);
                float open = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, forest));
                float meadow = MeadowMask(p, n);
                float trunk = trees.Nearest(p, 2f);

                layers[0][z, x] = Count(10f * open * (1f - meadow * 0.5f) * edge * (0.6f + 0.8f * n), cellArea);
                layers[1][z, x] = Count(7f * meadow * edge * (0.5f + n), cellArea);
                float flowers = meadow * 0.7f + open * 0.04f;
                layers[2][z, x] = Count(flowers * edge * (n > 0.55f ? 1.2f : 0.2f), cellArea);
                layers[3][z, x] = Count(flowers * edge * (n < 0.4f ? 1f : 0.15f), cellArea);
                float wet = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 14f, creekD)); // ferns crowd the creek
                layers[4][z, x] = trunk < 0.9f ? 0 : Count((3f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.75f, forest)) + 4f * wet) * (0.4f + n), cellArea);
                // Purple asters in the meadows (0.8): colour that says "wildflower meadow" at a glance.
                layers[5][z, x] = Count(meadow * edge * (n > 0.4f && n < 0.58f ? 1.1f : 0.1f), cellArea);
            }
            for (int i = 0; i < 6; i++) data.SetDetailLayer(0, 0, i, layers[i]);

            data.wavingGrassStrength = 0.4f;
            data.wavingGrassSpeed = 0.45f;
            data.wavingGrassAmount = 0.35f;
            data.wavingGrassTint = new Color(0.85f, 0.87f, 0.76f);
            _terrain.detailObjectDistance = 150f; // grass to the mid-distance; bare ground showed beyond 110 m
            _terrain.detailObjectDensity = 1f;
        }

        // Instances per detail cell from a density in instances per square metre (stochastic rounding).
        private int Count(float perSquareMetre, float cellArea)
        {
            float v = Mathf.Max(0f, perSquareMetre) * cellArea;
            int whole = Mathf.FloorToInt(v);
            if (R() < v - whole) whole++;
            return Mathf.Min(whole, 16);
        }

        private static DetailPrototype Detail(Texture2D texture, Color healthy, Color dry, float minW, float maxW, float minH, float maxH)
        {
            return new DetailPrototype
            {
                prototypeTexture = texture,
                renderMode = DetailRenderMode.Grass,
                usePrototypeMesh = false,
                healthyColor = healthy,
                dryColor = dry,
                minWidth = minW, maxWidth = maxW,
                minHeight = minH, maxHeight = maxH,
                noiseSpread = 0.35f,
            };
        }

        /// <summary>
        /// Unity 2022.2+ / Unity 6 detail options, by reflection so older editors compile: grass leans with the slope
        /// (alignToGround) and scatters less grid-like (positionJitter). Visible on hillsides and creek banks.
        /// </summary>
        private static DetailPrototype Modern(DetailPrototype d)
        {
            void Set(string name, object value)
            {
                var prop = typeof(DetailPrototype).GetProperty(name);
                if (prop != null && prop.CanWrite) prop.SetValue(d, value);
            }
            Set("alignToGround", 0.7f);
            Set("positionJitter", 0.8f);
            return d;
        }

        /// <summary>
        /// Unity 2022.2+ defaults new terrains to "coverage" detail scattering, where values mean something else.
        /// Switch to instance counts by reflection so this compiles and behaves the same on every version.
        /// </summary>
        private static void SetInstanceCountScatter(TerrainData data)
        {
            var method = typeof(TerrainData).GetMethod("SetDetailScatterMode");
            var enumType = typeof(TerrainData).Assembly.GetType("UnityEngine.DetailScatterMode");
            if (method == null || enumType == null) return;
            try { method.Invoke(data, new[] { Enum.Parse(enumType, "InstanceCountMode") }); }
            catch (Exception e) { Debug.LogWarning("[WildLens] Could not set detail scatter mode: " + e.Message); }
        }

        // ------------------------------------------------------------------ layers & textures

        private TerrainLayer[] CreateTerrainLayers()
        {
            GeneratedAssets.EnsureFolder(TerrainFolder);
            if (_art)
            {
                // Photographed ground (Poly Haven, CC0). Same order and roles as the generated layers below.
                var scanned = new[]
                {
                    // No CC0 scan is lush grass: the leafy-grass scan is mostly brown litter, so it is tinted two ways.
                    // Fresh green for open ground, golden-olive for meadows, at unrelated tile sizes so the two
                    // patterns never line up (0.6: distant ground read as bare soil before).
                    ScannedLayer("Grass", "leafy_grass", 3f, new Color(0.72f, 0.95f, 0.6f)),
                    ScannedLayer("Meadow", "leafy_grass", 4.3f, new Color(0.82f, 0.93f, 0.55f)),
                    ScannedLayer("ForestFloor", "forest_leaves_02", 2.6f, new Color(0.92f, 0.9f, 0.88f)),
                    ScannedLayer("Dirt", "dirt_floor", 2.4f, new Color(0.84f, 0.78f, 0.72f)), // tone the orange down
                    ScannedLayer("Rock", "aerial_rocks_02", 6f, Color.white),
                };
                if (System.Array.TrueForAll(scanned, l => l != null)) return scanned;
            }
            return new[]
            {
                Layer("Grass", 4f, (float u, float v, out float h) => SurfaceGrass(u, v, out h, new Color(0.25f, 0.37f, 0.13f), new Color(0.38f, 0.48f, 0.19f), new Color(0.5f, 0.48f, 0.26f), 11)),
                Layer("Meadow", 5f, SurfaceMeadow),
                Layer("ForestFloor", 4f, SurfaceForestFloor),
                Layer("Dirt", 3f, SurfaceDirt),
                Layer("Rock", 6f, SurfaceRock),
            };
        }

        private TerrainLayer ScannedLayer(string role, string asset, float tile, Color tint)
        {
            var diffuse = ArtLibrary.Texture($"Terrain/{asset}_diff_2k.jpg");
            var normal = ArtLibrary.Texture($"Terrain/{asset}_nor_gl_1k.jpg");
            if (diffuse == null) return null;
            string path = $"{TerrainFolder}/TL_Scan_{role}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            bool created = layer == null;
            if (created) layer = new TerrainLayer();
            layer.diffuseTexture = diffuse;
            layer.normalMapTexture = normal;
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(tile, tile);
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f);
            layer.smoothness = 0f;
            layer.metallic = 0f;
            if (created) AssetDatabase.CreateAsset(layer, path);
            else EditorUtility.SetDirty(layer);
            return layer;
        }

        private delegate Color Surface(float u, float v, out float height);

        private TerrainLayer Layer(string name, float tile, Surface surface)
        {
            string layerPath = $"{TerrainFolder}/TL_{name}.terrainlayer";
            var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            string diffusePath = $"{TerrainFolder}/T_{name}_D_{TextureVersion}.png";
            string normalPath = $"{TerrainFolder}/T_{name}_N_{TextureVersion}.png";

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath) == null)
            {
                const int size = 512;
                var colors = new Color[size * size];
                var heights = new float[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    colors[y * size + x] = surface(x / (float)size, y / (float)size, out float h);
                    heights[y * size + x] = h;
                }
                WritePng(diffusePath, size, colors);
                WritePng(normalPath, size, NormalsFromHeight(heights, size, 2.5f));
                ConfigureImporter(diffusePath, false, false);
                ConfigureImporter(normalPath, true, false);
            }

            var layer = existing != null ? existing : new TerrainLayer();
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0f;
            layer.metallic = 0f;
            if (existing == null) AssetDatabase.CreateAsset(layer, layerPath);
            else EditorUtility.SetDirty(layer);
            return layer;
        }

        private Color SurfaceGrass(float u, float v, out float h, Color dark, Color light, Color dry, int seed)
        {
            float big = Fbm(u, v, 4, 3, seed), mid = Fbm(u, v, 16, 3, seed + 1), fine = Fbm(u, v, 96, 2, seed + 2);
            h = mid * 0.6f + fine * 0.4f;
            Color c = Color.Lerp(dark, light, Mathf.Clamp01(mid * 0.7f + fine * 0.5f - 0.1f));
            c = Color.Lerp(c, dry, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.8f, big)) * 0.45f);
            return c * Mathf.Lerp(0.88f, 1.08f, fine);
        }

        private Color SurfaceMeadow(float u, float v, out float h)
        {
            Color c = SurfaceGrass(u, v, out h, new Color(0.33f, 0.42f, 0.16f), new Color(0.5f, 0.54f, 0.24f), new Color(0.62f, 0.56f, 0.3f), 21);
            float speck = Hash01(Mathf.FloorToInt(u * 512f), Mathf.FloorToInt(v * 512f), 77);
            if (speck > 0.996f) c = Color.Lerp(c, new Color(0.97f, 0.85f, 0.3f), 0.8f);
            else if (speck > 0.992f) c = Color.Lerp(c, new Color(0.95f, 0.94f, 0.9f), 0.75f);
            return c;
        }

        private static Color SurfaceForestFloor(float u, float v, out float h)
        {
            float base_ = Fbm(u, v, 8, 4, 31), leaves = Cells(u, v, 48, 32), moss = Fbm(u, v, 5, 3, 33);
            h = base_ * 0.4f + leaves * 0.6f;
            Color soil = Color.Lerp(new Color(0.18f, 0.13f, 0.08f), new Color(0.3f, 0.22f, 0.13f), base_);
            Color leaf = Color.Lerp(new Color(0.38f, 0.25f, 0.12f), new Color(0.5f, 0.36f, 0.17f), Fbm(u, v, 64, 2, 34));
            Color c = Color.Lerp(soil, leaf, Mathf.SmoothStep(0f, 1f, leaves));
            return Color.Lerp(c, new Color(0.24f, 0.32f, 0.13f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.75f, moss)) * 0.7f);
        }

        private static Color SurfaceDirt(float u, float v, out float h)
        {
            float base_ = Fbm(u, v, 10, 4, 41), pebbles = Cells(u, v, 40, 42);
            h = base_ * 0.5f + pebbles * 0.5f;
            Color c = Color.Lerp(new Color(0.36f, 0.28f, 0.19f), new Color(0.5f, 0.41f, 0.29f), base_);
            return Color.Lerp(c, new Color(0.58f, 0.54f, 0.46f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, pebbles)) * 0.6f);
        }

        private static Color SurfaceRock(float u, float v, out float h)
        {
            float base_ = Fbm(u, v, 6, 5, 51), ridges = 1f - Mathf.Abs(Fbm(u, v, 12, 3, 52) * 2f - 1f);
            h = base_ * 0.5f + ridges * 0.5f;
            Color c = Color.Lerp(new Color(0.34f, 0.34f, 0.32f), new Color(0.56f, 0.55f, 0.51f), base_);
            c *= Mathf.Lerp(0.75f, 1f, Mathf.SmoothStep(0f, 1f, ridges));
            float lichen = Fbm(u, v, 20, 2, 53);
            return Color.Lerp(c, new Color(0.5f, 0.52f, 0.36f), Mathf.InverseLerp(0.68f, 0.85f, lichen) * 0.5f);
        }

        // ---- detail sprites (RGBA, transparent background, base at the bottom edge)

        private delegate void DetailPainter(Color[] px, int size);

        private Texture2D DetailTexture(string name, DetailPainter paint)
        {
            GeneratedAssets.EnsureFolder(TerrainFolder);
            string path = $"{TerrainFolder}/D_{name}_{TextureVersion}.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                const int size = 256;
                var px = new Color[size * size];
                for (int i = 0; i < px.Length; i++) px[i] = new Color(0.3f, 0.4f, 0.2f, 0f);
                paint(px, size);
                WritePng(path, size, px);
                ConfigureImporter(path, false, true);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private void DrawGrass(Color[] px, int n)
        {
            var rng = new System.Random(5);
            for (int i = 0; i < 16; i++) Blade(px, n, rng, 0.08f, 0.92f, 0.45f, 0.95f, 0.035f,
                new Color(0.2f, 0.3f, 0.1f), new Color(0.62f, 0.72f, 0.36f));
        }

        private void DrawMeadowGrass(Color[] px, int n)
        {
            var rng = new System.Random(6);
            for (int i = 0; i < 14; i++) Blade(px, n, rng, 0.08f, 0.92f, 0.55f, 0.98f, 0.03f,
                new Color(0.26f, 0.34f, 0.12f), new Color(0.75f, 0.72f, 0.42f));
            // Seed heads near the tips.
            for (int i = 0; i < 6; i++)
            {
                float x = (float)rng.NextDouble() * 0.7f + 0.15f, y = (float)rng.NextDouble() * 0.2f + 0.72f;
                Ellipse(px, n, x, y, 0.018f, 0.06f, new Color(0.82f, 0.74f, 0.5f));
            }
        }

        private void DrawFlowers(Color[] px, int n, Color petal)
        {
            var rng = new System.Random(petal.GetHashCode());
            for (int i = 0; i < 6; i++) Blade(px, n, rng, 0.1f, 0.9f, 0.3f, 0.6f, 0.025f, new Color(0.2f, 0.32f, 0.12f), new Color(0.4f, 0.55f, 0.25f));
            for (int i = 0; i < 4; i++)
            {
                float x = 0.2f + (float)rng.NextDouble() * 0.6f, y = 0.6f + (float)rng.NextDouble() * 0.3f;
                Line(px, n, x, 0f, x + ((float)rng.NextDouble() - 0.5f) * 0.05f, y, 0.008f, new Color(0.25f, 0.4f, 0.16f));
                for (int k = 0; k < 6; k++)
                {
                    float a = k / 6f * Mathf.PI * 2f;
                    Ellipse(px, n, x + Mathf.Cos(a) * 0.035f, y + Mathf.Sin(a) * 0.035f, 0.028f, 0.028f, petal);
                }
                Ellipse(px, n, x, y, 0.022f, 0.022f, new Color(0.9f, 0.62f, 0.15f));
            }
        }

        private void DrawFern(Color[] px, int n)
        {
            var rng = new System.Random(8);
            Color dark = new Color(0.18f, 0.32f, 0.12f), light = new Color(0.42f, 0.6f, 0.26f);
            for (int f = 0; f < 5; f++)
            {
                float angle = Mathf.Lerp(-55f, 55f, f / 4f) * Mathf.Deg2Rad + ((float)rng.NextDouble() - 0.5f) * 0.2f;
                float len = 0.75f + (float)rng.NextDouble() * 0.2f;
                Vector2 prev = new Vector2(0.5f, 0f);
                for (int s = 1; s <= 20; s++)
                {
                    float t = s / 20f;
                    // Fronds arch outward and droop toward the tip.
                    float a = angle * (0.6f + t * 0.8f);
                    Vector2 pnt = new Vector2(0.5f + Mathf.Sin(a) * len * t, Mathf.Cos(a) * len * t * (1f - t * 0.25f));
                    Line(px, n, prev.x, prev.y, pnt.x, pnt.y, 0.006f, dark);
                    float leaflet = 0.07f * (1f - t) + 0.015f;
                    Vector2 dir = (pnt - prev).normalized, side = new Vector2(-dir.y, dir.x);
                    Color c = Color.Lerp(dark, light, t);
                    Ellipse(px, n, pnt.x + side.x * leaflet, pnt.y + side.y * leaflet, leaflet * 0.9f, 0.012f, c);
                    Ellipse(px, n, pnt.x - side.x * leaflet, pnt.y - side.y * leaflet, leaflet * 0.9f, 0.012f, c);
                    prev = pnt;
                }
            }
        }

        // ---- tiny rasteriser (normalised 0-1 coordinates; y up)

        private static void Blade(Color[] px, int n, System.Random rng, float xMin, float xMax, float hMin, float hMax, float width, Color baseC, Color tipC)
        {
            float x0 = xMin + (float)rng.NextDouble() * (xMax - xMin);
            float h = hMin + (float)rng.NextDouble() * (hMax - hMin);
            float lean = ((float)rng.NextDouble() - 0.5f) * 0.35f;
            for (int y = 0; y < n; y++)
            {
                float t = y / (float)n / h;
                if (t > 1f) break;
                float cx = x0 + lean * t * t;
                float w = width * (1f - t) + 0.002f;
                Color c = Color.Lerp(baseC, tipC, t);
                int xa = Mathf.FloorToInt((cx - w) * n), xb = Mathf.CeilToInt((cx + w) * n);
                for (int x = Mathf.Max(0, xa); x <= Mathf.Min(n - 1, xb); x++)
                {
                    float cover = Mathf.Clamp01(w * n - Mathf.Abs(x + 0.5f - cx * n) + 0.5f);
                    Blend(px, n, x, y, c, cover);
                }
            }
        }

        private static void Line(Color[] px, int n, float x0, float y0, float x1, float y1, float width, Color c)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * n * 2f) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Ellipse(px, n, Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), width, width, c);
            }
        }

        private static void Ellipse(Color[] px, int n, float cx, float cy, float rx, float ry, Color c)
        {
            int x0 = Mathf.FloorToInt((cx - rx) * n), x1 = Mathf.CeilToInt((cx + rx) * n);
            int y0 = Mathf.FloorToInt((cy - ry) * n), y1 = Mathf.CeilToInt((cy + ry) * n);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(n - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(n - 1, x1); x++)
            {
                float dx = ((x + 0.5f) / n - cx) / rx, dy = ((y + 0.5f) / n - cy) / ry;
                float d = dx * dx + dy * dy;
                if (d <= 1f) Blend(px, n, x, y, c, Mathf.Clamp01((1f - d) * 4f));
            }
        }

        private static void Blend(Color[] px, int n, int x, int y, Color c, float cover)
        {
            int i = y * n + x;
            Color o = px[i];
            float a = cover + o.a * (1f - cover);
            Color rgb = a > 0f ? (c * cover + o * o.a * (1f - cover)) / a : c;
            px[i] = new Color(rgb.r, rgb.g, rgb.b, a);
        }

        // ---- tileable noise

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        private static float ValueNoise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int Wrap(int i) => ((i % period) + period) % period;
            float a = Hash01(Wrap(x0), Wrap(y0), seed), b = Hash01(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash01(Wrap(x0), Wrap(y0 + 1), seed), d = Hash01(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(u, v, period << o, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>Tileable cellular blobs (leaves, pebbles): 1 near a cell's random feature point, 0 far away.</summary>
        private static float Cells(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float best = 9f;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int gx = cx + i, gy = cy + j;
                int wx = ((gx % period) + period) % period, wy = ((gy % period) + period) % period;
                float fx = gx + Hash01(wx, wy, seed), fy = gy + Hash01(wx, wy, seed + 1);
                float r = 0.25f + Hash01(wx, wy, seed + 2) * 0.35f;
                float d = Mathf.Sqrt((x - fx) * (x - fx) + (y - fy) * (y - fy)) / r;
                best = Mathf.Min(best, d);
            }
            return Mathf.Clamp01(1f - best);
        }

        private static Color[] NormalsFromHeight(float[] h, int n, float strength)
        {
            var result = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = h[y * n + (x + 1) % n] - h[y * n + (x - 1 + n) % n];
                float dy = h[((y + 1) % n) * n + x] - h[((y - 1 + n) % n) * n + x];
                var nrm = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                result[y * n + x] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
            }
            return result;
        }

        private static void WritePng(string assetPath, int size, Color[] pixels)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureImporter(string assetPath, bool normalMap, bool detailSprite)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) return;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = detailSprite ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = detailSprite ? 1 : 4;
            importer.alphaIsTransparency = detailSprite;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ spatial helpers

        /// <summary>Distance to the trail sampled on a grid (bilinear), so painting a million texels stays fast.</summary>
        private class TrailDistanceField
        {
            private readonly float[,] _d;
            private readonly Vector3 _origin;
            private readonly float _cell;
            private readonly int _w, _h;

            public TrailDistanceField(System.Func<Vector3, float> distance, Terrain terrain)
            {
                _origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                _cell = Mathf.Max(0.5f, Mathf.Max(size.x, size.z) / 512f);
                _w = Mathf.CeilToInt(size.x / _cell) + 1;
                _h = Mathf.CeilToInt(size.z / _cell) + 1;
                _d = new float[_w, _h];
                for (int x = 0; x < _w; x++)
                for (int z = 0; z < _h; z++)
                    _d[x, z] = distance(_origin + new Vector3(x * _cell, 0f, z * _cell));
            }

            public float Sample(Vector3 p)
            {
                float fx = Mathf.Clamp((p.x - _origin.x) / _cell, 0f, _w - 1.001f);
                float fz = Mathf.Clamp((p.z - _origin.z) / _cell, 0f, _h - 1.001f);
                int x = (int)fx, z = (int)fz;
                float tx = fx - x, tz = fz - z;
                return Mathf.Lerp(Mathf.Lerp(_d[x, z], _d[x + 1, z], tx), Mathf.Lerp(_d[x, z + 1], _d[x + 1, z + 1], tx), tz);
            }
        }

        private class TreeGrid
        {
            private const float Cell = 6f;
            private readonly Dictionary<Vector2Int, List<Vector3>> _grid = new Dictionary<Vector2Int, List<Vector3>>();

            public TreeGrid(List<GameObject> trees)
            {
                foreach (var t in trees)
                {
                    if (t == null) continue;
                    Vector3 p = t.transform.position;
                    var c = new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));
                    if (!_grid.TryGetValue(c, out var list)) _grid[c] = list = new List<Vector3>();
                    list.Add(p);
                }
            }

            public float Nearest(Vector3 p, float max)
            {
                float best = max;
                var c = new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));
                for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    if (!_grid.TryGetValue(new Vector2Int(c.x + i, c.y + j), out var list)) continue;
                    foreach (var q in list) best = Mathf.Min(best, TerrainUtil.FlatDistance(p, q));
                }
                return best;
            }
        }
    }
}
