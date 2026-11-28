using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.EditorTools.SmallLife;

namespace WildLens.EditorTools
{
    /// <summary>
    /// The Unity side of the generated small life (1.9): turns the pure models of <see cref="SmallLifeShapes"/> into meshes, materials,
    /// textures and a prefab under Assets/WildLens/Generated/SmallLife/Models, once, and reuses them on every rebuild. Nothing is
    /// downloaded and nothing is committed to the package: the folder is created in the user's project, like every generated asset.
    /// Bump <see cref="ModelVersion"/> when a recipe changes; the previous version's folder is then replaced.
    /// </summary>
    public static class SmallLifeModels
    {
        public const int ModelVersion = 1;
        public const string Folder = GeneratedAssets.Folder + "/SmallLife/Models";

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        public static float NativeSize(string key) => SmallLifeShapes.Size(key);

        /// <summary>The cached prefab for a model key (an enum name such as "Monarch"), built on first use. Null when it could not be made.</summary>
        public static GameObject Prefab(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            GameObject result = null;
            try
            {
                string folder = $"{Folder}/{key}_v{ModelVersion}";
                string prefabPath = $"{folder}/SL_{key}.prefab";
                result = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (result == null) result = Build(key, folder, prefabPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WildLens] Small life: could not build the generated '{key}' model: {e.Message}");
                result = null;
            }
            Cache[key] = result;
            return result;
        }

        private static GameObject Build(string key, string folder, string prefabPath)
        {
            var model = SmallLifeShapes.Build(key);
            if (model == null) return null;
            GeneratedAssets.EnsureFolder(GeneratedAssets.Folder + "/SmallLife");
            GeneratedAssets.EnsureFolder(Folder);
            RemoveOldVersions(key);
            GeneratedAssets.EnsureFolder(folder);

            var materials = new Dictionary<Mat, Material>();
            foreach (var mat in model.mats) materials[mat] = MakeMaterial(model, mat, folder);

            var root = new GameObject("SL_" + key);
            var byName = new Dictionary<string, Transform>();
            var origins = new Dictionary<string, Vector3>();
            try
            {
                foreach (var part in model.parts)
                {
                    var go = new GameObject(part.name);
                    Transform parent = root.transform;
                    Vector3 parentOrigin = Vector3.zero;
                    if (!string.IsNullOrEmpty(part.parent) && byName.TryGetValue(part.parent, out var pt)) { parent = pt; parentOrigin = origins[part.parent]; }
                    go.transform.SetParent(parent, false);
                    Vector3 origin = new Vector3(part.origin.x, part.origin.y, part.origin.z);
                    go.transform.localPosition = origin - parentOrigin;
                    if (Mathf.Abs(part.restRoll) > 0.001f) go.transform.localRotation = Quaternion.Euler(0f, 0f, part.restRoll);
                    byName[part.name] = go.transform;
                    origins[part.name] = origin;
                    if (part.empty || part.v.Count == 0) continue;
                    if (part.mat == null || !materials.TryGetValue(part.mat, out var material) || material == null) { Object.DestroyImmediate(go); continue; }
                    var mesh = MakeMesh(model, part);
                    AssetDatabase.CreateAsset(mesh, $"{folder}/{mesh.name}.asset");
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    bool halo = part.mat.kind == MatKind.Additive;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = !halo;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Deletes the folders of earlier versions of this model (and only this model's).</summary>
        private static void RemoveOldVersions(string key)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) return;
            foreach (string sub in AssetDatabase.GetSubFolders(Folder))
            {
                string name = System.IO.Path.GetFileName(sub);
                if (name.StartsWith(key + "_v") && name != $"{key}_v{ModelVersion}" && name.Length > key.Length + 2 && char.IsDigit(name[key.Length + 2]))
                    AssetDatabase.DeleteAsset(sub);
            }
        }

        private static Mesh MakeMesh(SmallLife.Model model, SmallLife.Part part)
        {
            var mesh = new Mesh { name = $"{model.key}_{part.name.Replace(' ', '_')}" };
            int n = part.v.Count;
            var v = new Vector3[n]; var nm = new Vector3[n]; var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(part.v[i].x, part.v[i].y, part.v[i].z);
                nm[i] = new Vector3(part.n[i].x, part.n[i].y, part.n[i].z);
                uv[i] = new Vector2(part.uv[i].x, part.uv[i].y);
            }
            if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = v;
            mesh.normals = nm;
            mesh.uv = uv;
            mesh.triangles = part.t.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D MakeTexture(SmallLife.Mat mat, string folder, bool mips)
        {
            Color[] pixels;
            int w, h;
            if (mat.IsPalette)
            {
                w = mat.PaletteWidth; h = 1;
                pixels = new Color[w];
                for (int i = 0; i < w; i++)
                {
                    var c = i < mat.palette.Count ? mat.palette[i] : mat.palette.Count > 0 ? mat.palette[mat.palette.Count - 1] : new Col(0.5f, 0.5f, 0.5f);
                    pixels[i] = new Color(c.r, c.g, c.b, 1f);
                }
            }
            else
            {
                w = mat.w; h = mat.h;
                pixels = new Color[mat.pixels.Length];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(mat.pixels[i].r, mat.pixels[i].g, mat.pixels[i].b, mat.pixels[i].a);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mips) { name = mat.name + "_Tex", wrapMode = TextureWrapMode.Clamp };
            tex.filterMode = mat.IsPalette ? FilterMode.Point : FilterMode.Bilinear;
            tex.SetPixels(pixels);
            tex.Apply(mips);
            AssetDatabase.CreateAsset(tex, $"{folder}/{tex.name}.asset");
            return tex;
        }

        private static Material MakeMaterial(SmallLife.Model model, SmallLife.Mat mat, string folder)
        {
            Material m;
            switch (mat.kind)
            {
                case MatKind.Additive:
                {
                    // An unlit additive sprite. The legacy particle shader draws in Built-in and in URP; the sprite shader is the fallback.
                    Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
                    if (sh == null) sh = Shader.Find("Mobile/Particles/Additive");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    if (sh == null) return null;
                    var tex = MakeTexture(mat, folder, false);
                    m = new Material(sh) { name = mat.name };
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                    if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                    if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(1f, 1f, 1f, 0.6f));
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
                }
                default:
                {
                    var tex = MakeTexture(mat, folder, !mat.IsPalette && mat.kind != MatKind.Additive);
                    m = MaterialLibrary.Create(Color.white, mat.smoothness, tex);
                    m.name = mat.name;
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                    if (mat.kind == MatKind.PaletteDouble || mat.kind == MatKind.Painted) MaterialLibrary.MakeDoubleSided(m);
                    if (mat.kind == MatKind.Painted)
                    {
                        // Alpha-tested, two-sided (wings): the same settings the project's foliage cards use.
                        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
                        if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 1f);
                        if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                        m.SetOverrideTag("RenderType", "TransparentCutout");
                    }
                    if (mat.kind == MatKind.Glow)
                    {
                        // Emission starts dark; MacroMotion raises it with a property block, so the keyword must already be on.
                        m.EnableKeyword("_EMISSION");
                        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                    break;
                }
            }
            AssetDatabase.CreateAsset(m, $"{folder}/{m.name}.mat");
            return m;
        }
    }
}
