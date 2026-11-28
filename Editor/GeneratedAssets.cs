using System.IO;
using UnityEditor;
using UnityEngine;
using WildLens.Core;

namespace WildLens.EditorTools
{
    /// <summary>Creates/updates assets the builder generates (materials, meshes, textures) under one folder.</summary>
    public static class GeneratedAssets
    {
        public const string Root = "Assets/WildLens";
        public const string Folder = Root + "/Generated";
        public const string DataFolder = Root + "/Data";
        public const string SpeciesFolder = DataFolder + "/Species";

        public static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(Folder);
            EnsureFolder(DataFolder);
            EnsureFolder(SpeciesFolder);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        public static Material Material(string name, Color color, float smoothness = 0.1f, Texture texture = null, bool doubleSided = false)
        {
            string path = $"{Folder}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = MaterialLibrary.Create(color, smoothness, texture);
                mat.name = "M_" + name;
                if (doubleSided) MaterialLibrary.MakeDoubleSided(mat);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                if (mat.shader != MaterialLibrary.LitShader) mat.shader = MaterialLibrary.LitShader;
                MaterialLibrary.ApplyColor(mat, color);
                if (texture != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
                }
                if (doubleSided) MaterialLibrary.MakeDoubleSided(mat);
                EditorUtility.SetDirty(mat);
            }
            if (WoodTints.TryGetValue(name, out var tint)) ApplyWoodGrain(mat, tint);
            return mat;
        }

        /// <summary>
        /// 1.2: the reserve's woodwork (kiosk, signs, decks, the blind, benches, bridges) gets the scanned CC0 plank texture
        /// (Poly Haven Weathered Brown Planks), tinted per role: light boards, dark posts, stained sign boards. Boxes made
        /// by <see cref="PropBuilder.Box"/> carry real-scale UVs, so the grain runs along each piece at its true size.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, Color> WoodTints =
            new System.Collections.Generic.Dictionary<string, Color>
            {
                { "Wood", new Color(1f, 0.94f, 0.86f) },
                { "WoodDark", new Color(0.6f, 0.53f, 0.46f) },
                { "SignBoard", new Color(0.66f, 0.56f, 0.47f) }, // 1.4: lighter: 1.3 read as a black slab in shade
            };

        public const string WoodTexture = "Surfaces/weathered_brown_planks_diff_1k.jpg";
        public const float WoodTextureSize = 1.8f; // metres covered by one repeat of the scan

        private static void ApplyWoodGrain(Material mat, Color tint)
        {
            if (!ArtLibrary.Import()) return;
            var albedo = ArtLibrary.Texture(WoodTexture);
            if (albedo == null) return;
            MaterialLibrary.ApplyColor(mat, tint);
            foreach (var prop in new[] { "_BaseMap", "_MainTex" })
            {
                if (!mat.HasProperty(prop)) continue;
                mat.SetTexture(prop, albedo);
                mat.SetTextureScale(prop, Vector2.one);
            }
            var normal = ArtLibrary.Texture("Surfaces/weathered_brown_planks_nor_gl_1k.jpg");
            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }

        /// <summary>Saves (replacing) a generated asset such as a mesh or texture.</summary>
        public static T Save<T>(T asset, string fileName) where T : Object
        {
            string path = $"{Folder}/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            asset.name = fileName;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Loads a cached generated asset, or creates and saves it.</summary>
        public static T LoadOrCreate<T>(string fileName, System.Func<T> create) where T : Object
        {
            string path = $"{Folder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            return Save(create(), fileName);
        }

        /// <summary>Tileable value-noise texture tinted between two colours, with optional specks.</summary>
        public static Texture2D NoiseTexture(int size, Color a, Color b, float frequency, int seed, float speckChance, Color speck)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var rng = new System.Random(seed);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = TileableNoise(u, v, frequency, seed) * 0.65f + TileableNoise(u, v, frequency * 3f, seed + 17) * 0.35f;
                Color c = Color.Lerp(a, b, n);
                if (rng.NextDouble() < speckChance) c = Color.Lerp(c, speck, 0.6f);
                px[y * size + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // Blend four Perlin samples so the texture wraps seamlessly.
        private static float TileableNoise(float u, float v, float f, int seed)
        {
            float o = seed * 13.37f % 100f;
            float n00 = Mathf.PerlinNoise(o + u * f, o + v * f);
            float n10 = Mathf.PerlinNoise(o + (u - 1f) * f, o + v * f);
            float n01 = Mathf.PerlinNoise(o + u * f, o + (v - 1f) * f);
            float n11 = Mathf.PerlinNoise(o + (u - 1f) * f, o + (v - 1f) * f);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v);
        }
    }
}
