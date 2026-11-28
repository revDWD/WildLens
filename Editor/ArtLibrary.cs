using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.Environment;
using Object = UnityEngine.Object;

namespace WildLens.EditorTools
{
    public enum ArtKind { Fern, Shrub, RockPiece, Boulder, Stump, Log, Outcrop }

    /// <summary>Sun and colour data measured from an HDRI (written by the asset pipeline into Art~/Sky/*.json).</summary>
    [Serializable]
    public class SkyInfo
    {
        public string name;
        public float sunElevation, sunAzimuth;
        public float[] sunDirection;
        public float[] sunColor;
        public float[] fogColor;
        public float[] zenithColor;
        public float horizonLuminance;
        public float sunLuminance;

        public Vector3 SunDirection => new Vector3(sunDirection[0], sunDirection[1], sunDirection[2]).normalized;
        public static Color ToColor(float[] c) => new Color(c[0], c[1], c[2]);
    }

    /// <summary>
    /// Scanned art shipped with the package in <c>Art~</c> (all CC0 from Poly Haven; see Art~/CREDITS.md):
    /// two HDRI skies, five terrain materials, bark and rock surfaces, and ten models (fern, shrubs, mossy rock sets,
    /// boulder, stump, fallen trunk, and two hillside rock faces).
    ///
    /// Folders ending in "~" are ignored by Unity's importer, so <see cref="Import"/> copies the files into
    /// Assets/WildLens/Art and applies import settings (normal maps, HDR sky without mips, alpha-coverage-preserving
    /// mips for foliage, FBX without materials). <see cref="Prefabs"/> turns each model into game-ready prefabs:
    /// pivot at the base, sized from Poly Haven's real-world dimensions, our materials, colliders and
    /// <see cref="EnvironmentFeature"/> data so wildlife can use them. "Set" models are split into separate pieces.
    /// </summary>
    public static class ArtLibrary
    {
        public const string Destination = GeneratedAssets.Root + "/Art";
        private const string PrefabFolder = Destination + "/Prefabs";
        private const string ImportVersion = "5"; // 2: Foliage card textures. 3: rock faces (outcrops). 4: plank texture. 5: overcast sky

        private struct ModelSpec
        {
            public string name;
            public ArtKind kind;
            public bool alpha;
            public float maxDimension; // metres, from Poly Haven metadata
            public bool split;
        }

        private static readonly ModelSpec[] Models =
        {
            new ModelSpec { name = "fern_02", kind = ArtKind.Fern, alpha = true, maxDimension = 1.97f },
            new ModelSpec { name = "shrub_04", kind = ArtKind.Shrub, alpha = true, maxDimension = 0.75f },
            new ModelSpec { name = "shrub_02", kind = ArtKind.Shrub, alpha = true, maxDimension = 5.34f, split = true },
            new ModelSpec { name = "rock_moss_set_01", kind = ArtKind.RockPiece, maxDimension = 8.0f, split = true },
            new ModelSpec { name = "rock_moss_set_02", kind = ArtKind.RockPiece, maxDimension = 8.28f, split = true },
            new ModelSpec { name = "boulder_01", kind = ArtKind.Boulder, maxDimension = 1.83f },
            new ModelSpec { name = "tree_stump_01", kind = ArtKind.Stump, maxDimension = 1.59f },
            new ModelSpec { name = "dead_tree_trunk_02", kind = ArtKind.Log, maxDimension = 4.05f },
            // 1.2: scanned hillside rock faces (open shells that face up and out, ~45° lean): outcrops on slopes.
            new ModelSpec { name = "rock_face_01", kind = ArtKind.Outcrop, maxDimension = 7.11f },
            new ModelSpec { name = "rock_face_02", kind = ArtKind.Outcrop, maxDimension = 4.87f },
        };

        /// <summary>Models whose mesh data is read when their prefab is made (outcrops are turned to face +Z).</summary>
        private static bool NeedsReadableMesh(string assetPath)
        {
            foreach (var spec in Models)
                if (spec.kind == ArtKind.Outcrop && assetPath.EndsWith("/" + spec.name + ".fbx")) return true;
            return false;
        }

        private static string _sourceRoot;
        private static bool _imported;

        // ------------------------------------------------------------------ location & import

        /// <summary>Absolute path of the package's Art~ folder, or null.</summary>
        public static string SourceRoot
        {
            get
            {
                if (_sourceRoot != null) return _sourceRoot.Length > 0 ? _sourceRoot : null;
                _sourceRoot = "";
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ArtLibrary).Assembly);
                var candidates = new List<string>();
                if (info != null) candidates.Add(Path.Combine(info.resolvedPath, "Art~"));
                // Fallback: the package copied into Assets (Editor/ArtLibrary.cs → ../Art~).
                foreach (string guid in AssetDatabase.FindAssets("ArtLibrary t:MonoScript"))
                {
                    string script = Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid));
                    candidates.Add(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(script)) ?? "", "Art~"));
                }
                foreach (var c in candidates)
                    if (Directory.Exists(c)) { _sourceRoot = c; break; }
                return _sourceRoot.Length > 0 ? _sourceRoot : null;
            }
        }

        public static bool Available => SourceRoot != null;

        /// <summary>Copies Art~ into the project (once per version) and configures importers. Returns false if unavailable.</summary>
        public static bool Import()
        {
            if (_imported) return true;
            string src = SourceRoot;
            if (src == null) return false;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string dstAbs = Path.Combine(projectRoot, Destination);
            string marker = Path.Combine(dstAbs, ".imported_v" + ImportVersion);
            var copied = new List<string>();
            if (!File.Exists(marker))
            {
                foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(src.Length).TrimStart('/', '\\');
                    if (rel.EndsWith(".md") || rel.EndsWith(".json")) continue;
                    string dst = Path.Combine(dstAbs, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(file).Length)
                    {
                        File.Copy(file, dst, true);
                        copied.Add((Destination + "/" + rel).Replace('\\', '/'));
                    }
                }
                File.WriteAllText(marker, "WildLens art import " + ImportVersion);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var path in copied) ConfigureImporter(path);
                Debug.Log($"[WildLens] Imported {copied.Count} scanned art files (CC0, Poly Haven) into {Destination}.");
            }
            _imported = true;
            return true;
        }

        private static void ConfigureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer is ModelImporter model)
            {
                model.materialImportMode = ModelImporterMaterialImportMode.None;
                model.importAnimation = false;
                model.importCameras = false;
                model.importLights = false;
                model.isReadable = NeedsReadableMesh(assetPath);
                model.SaveAndReimport();
                return;
            }
            if (!(importer is TextureImporter tex)) return;
            string file = Path.GetFileName(assetPath).ToLowerInvariant();
            tex.anisoLevel = 8;
            tex.mipmapEnabled = true;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.maxTextureSize = 2048;
            if (file.EndsWith(".hdr"))
            {
                // Equirectangular sky: no mips (avoids a seam where longitude wraps), clamp vertically.
                tex.textureShape = TextureImporterShape.Texture2D;
                tex.mipmapEnabled = false;
                tex.wrapModeU = TextureWrapMode.Repeat;
                tex.wrapModeV = TextureWrapMode.Clamp;
                tex.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            else if (file.Contains("_nor_gl"))
            {
                tex.textureType = TextureImporterType.NormalMap; // OpenGL-convention (Y+) normals, as Unity expects
            }
            else if (file.Contains("_rgba"))
            {
                tex.alphaIsTransparency = true;
                tex.mipMapsPreserveCoverage = true; // foliage keeps its density at a distance
                tex.alphaTestReferenceValue = 0.5f;
                tex.wrapMode = TextureWrapMode.Clamp;
            }
            tex.SaveAndReimport();
        }

        // ------------------------------------------------------------------ textures, skies, materials

        public static Texture2D Texture(string relative) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Destination + "/" + relative);

        public static SkyInfo Sky(string name)
        {
            if (SourceRoot == null) return null;
            string json = Path.Combine(SourceRoot, "Sky", name + ".json");
            return File.Exists(json) ? JsonUtility.FromJson<SkyInfo>(File.ReadAllText(json)) : null;
        }

        public static Material SkyMaterial(string name, float exposure)
        {
            var hdr = Texture($"Sky/{name}_2k.hdr");
            var shader = Shader.Find("Skybox/Panoramic");
            if (hdr == null || shader == null) return null;
            string path = $"{GeneratedAssets.Folder}/M_Sky_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetTexture("_MainTex", hdr);
            mat.SetFloat("_Mapping", 1f);   // latitude-longitude layout
            mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            mat.SetFloat("_ImageType", 0f); // 360°
            mat.SetFloat("_Rotation", 0f);  // sun directions in the JSON assume no rotation
            mat.SetFloat("_Exposure", exposure);
            mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Lit material with scanned albedo + normal map (works in URP and Built-in).</summary>
        public static Material SurfaceMaterial(string key, string diffuse, string normal, Color tint, Vector2 tiling, float smoothness = 0.1f)
        {
            var albedo = Texture(diffuse);
            if (albedo == null) return null;
            string path = $"{GeneratedAssets.Folder}/M_Scan_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = MaterialLibrary.Create(tint, smoothness); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = MaterialLibrary.LitShader;
            MaterialLibrary.ApplyColor(mat, tint);
            SetTexture(mat, albedo, tiling);
            SetNormal(mat, Texture(normal), tiling);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Alpha-tested, two-sided lit material (foliage, ferns, leaf cards).</summary>
        public static Material AlphaCutMaterial(string key, Texture2D albedo, Texture2D normal, Color tint)
        {
            var mat = FoliageMaterial(key, albedo, normal);
            MaterialLibrary.ApplyColor(mat, tint);
            return mat;
        }

        private static Material FoliageMaterial(string key, Texture2D albedo, Texture2D normal)
        {
            string path = $"{GeneratedAssets.Folder}/M_Scan_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = MaterialLibrary.Create(Color.white, 0.15f); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = MaterialLibrary.LitShader;
            MaterialLibrary.ApplyColor(mat, Color.white);
            SetTexture(mat, albedo, Vector2.one);
            SetNormal(mat, normal, Vector2.one);
            // Alpha-tested, two-sided leaves. URP Lit: _AlphaClip/_Cull. Standard: _Mode 1 (Cutout).
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 1f);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            MaterialLibrary.MakeDoubleSided(mat);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetTexture(Material mat, Texture tex, Vector2 tiling)
        {
            foreach (var prop in new[] { "_BaseMap", "_MainTex" })
            {
                if (!mat.HasProperty(prop)) continue;
                mat.SetTexture(prop, tex);
                mat.SetTextureScale(prop, tiling);
            }
        }

        private static void SetNormal(Material mat, Texture tex, Vector2 tiling)
        {
            if (tex == null || !mat.HasProperty("_BumpMap")) return;
            mat.SetTexture("_BumpMap", tex);
            mat.SetTextureScale("_BumpMap", tiling);
            mat.EnableKeyword("_NORMALMAP");
        }

        // ------------------------------------------------------------------ model prefabs

        public static List<GameObject> Prefabs(ArtKind kind)
        {
            var result = new List<GameObject>();
            if (!Import()) return result;
            foreach (var spec in Models)
                if (spec.kind == kind) result.AddRange(GetOrBuild(spec));
            return result;
        }

        private static List<GameObject> GetOrBuild(ModelSpec spec)
        {
            GeneratedAssets.EnsureFolder(PrefabFolder);
            var existing = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets($"PH_{spec.name} t:Prefab", new[] { PrefabFolder }))
                existing.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            if (existing.Count > 0) return existing;

            string folder = $"Models/{spec.name}";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Destination}/{folder}/{spec.name}.fbx");
            if (source == null) return existing;
            var albedo = Texture($"{folder}/{spec.name}_diff_rgba.png") ?? Texture($"{folder}/{spec.name}_diff.jpg");
            var normal = Texture($"{folder}/{spec.name}_nor_gl_1k.jpg");
            Material mat = spec.alpha ? FoliageMaterial(spec.name, albedo, normal)
                                      : SurfaceMaterial(spec.name, $"{folder}/{spec.name}_diff.jpg", $"{folder}/{spec.name}_nor_gl_1k.jpg", Color.white, Vector2.one, 0.12f);
            // A rock face is a scanned shell with an open back: seen from uphill it must read as rock, not a hole.
            if (spec.kind == ArtKind.Outcrop && mat != null) { MaterialLibrary.MakeDoubleSided(mat); EditorUtility.SetDirty(mat); }

            var instance = (GameObject)Object.Instantiate(source);
            instance.transform.position = Vector3.zero;
            try
            {
                var filters = instance.GetComponentsInChildren<MeshFilter>();
                if (spec.split && filters.Length > 1)
                {
                    int index = 0;
                    foreach (var mf in filters)
                    {
                        if (mf.sharedMesh == null) continue;
                        var root = new GameObject($"PH_{spec.name}_{index:00}");
                        var piece = new GameObject("Mesh");
                        piece.transform.SetParent(root.transform, false);
                        piece.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                        piece.transform.localScale = mf.transform.lossyScale;
                        piece.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                        piece.AddComponent<MeshRenderer>().sharedMaterial = mat;
                        Bounds b = Recenter(root, piece.transform);
                        if (Mathf.Max(b.size.x, b.size.y, b.size.z) < 0.25f) { Object.DestroyImmediate(root); continue; } // pebbles
                        Finish(root, spec.kind, b, piece);
                        existing.Add(Save(root, $"{PrefabFolder}/PH_{spec.name}_{index:00}.prefab"));
                        index++;
                    }
                }
                else
                {
                    var root = new GameObject($"PH_{spec.name}");
                    instance.transform.SetParent(root.transform, false);
                    foreach (var r in instance.GetComponentsInChildren<MeshRenderer>())
                    {
                        var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                        for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                        r.sharedMaterials = mats;
                    }
                    if (spec.kind == ArtKind.Outcrop) FaceForward(instance.transform);
                    // Size from the real dimensions in case the FBX unit scale differs from metres.
                    Bounds b = Recenter(root, instance.transform);
                    float measured = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    float ratio = measured > 0f ? spec.maxDimension / measured : 1f;
                    if (ratio < 0.5f || ratio > 2f)
                    {
                        instance.transform.localScale *= ratio;
                        b = Recenter(root, instance.transform);
                    }
                    Finish(root, spec.kind, b, instance.GetComponentInChildren<MeshFilter>()?.gameObject);
                    existing.Add(Save(root, $"{PrefabFolder}/PH_{spec.name}.prefab"));
                    instance = null; // now owned by root, destroyed with it
                }
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
            return existing;
        }

        /// <summary>
        /// Turns a scanned rock face so its open side looks along +Z (the builder then aims +Z downhill and the open back
        /// is buried in the slope). The facing is the summed vertex normal; FBX axis conversion can rotate the node, so
        /// it is measured rather than assumed. Measured in Python when added: both scans face up and toward +Z.
        /// </summary>
        private static void FaceForward(Transform content)
        {
            Vector3 sum = Vector3.zero;
            foreach (var mf in content.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                foreach (var n in mesh.normals) sum += mf.transform.TransformDirection(n);
            }
            Vector3 flat = new Vector3(sum.x, 0f, sum.z);
            if (flat.sqrMagnitude < 1e-6f) return;
            content.rotation = Quaternion.FromToRotation(flat.normalized, Vector3.forward) * content.rotation;
        }

        /// <summary>Moves <paramref name="content"/> so the combined bounds sit centred on the root's origin, base at y = 0.</summary>
        private static Bounds Recenter(GameObject root, Transform content)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds();
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            content.position -= new Vector3(b.center.x, b.min.y, b.center.z) - root.transform.position;
            b.center -= new Vector3(b.center.x, b.min.y, b.center.z); // centre is now (0, extents.y, 0)
            return b;
        }

        private static void Finish(GameObject root, ArtKind kind, Bounds b, GameObject meshObject)
        {
            float radius = Mathf.Max(b.extents.x, b.extents.z);
            switch (kind)
            {
                case ArtKind.RockPiece:
                case ArtKind.Boulder:
                    if (meshObject != null && meshObject.GetComponent<MeshFilter>() != null)
                    {
                        var col = meshObject.AddComponent<MeshCollider>();
                        col.sharedMesh = meshObject.GetComponent<MeshFilter>().sharedMesh;
                        col.convex = true;
                    }
                    else AddBox(root, b, false);
                    AddFeature(root, FeatureKind.Rock, radius, b.size.y);
                    break;
                case ArtKind.Outcrop:
                    // Static scenery: an exact (non-convex) collider, so the player walks over and around the real shape.
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                        if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    AddFeature(root, FeatureKind.Rock, radius, b.size.y);
                    break;
                case ArtKind.Stump:
                case ArtKind.Log:
                    AddBox(root, b, false);
                    AddFeature(root, FeatureKind.Log, radius, b.size.y);
                    break;
                case ArtKind.Shrub:
                    AddBox(root, b, true); // hides crouched players and blocks photo line-of-sight, not movement
                    AddFeature(root, FeatureKind.Bush, radius, b.size.y);
                    break;
                case ArtKind.Fern:
                    break; // low ground cover: no collider
            }
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = kind == ArtKind.Fern ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private static void AddBox(GameObject root, Bounds b, bool trigger)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size * 0.9f;
            box.isTrigger = trigger;
        }

        private static void AddFeature(GameObject root, FeatureKind kind, float radius, float height)
        {
            var f = root.AddComponent<EnvironmentFeature>();
            f.kind = kind;
            f.radius = radius;
            f.height = height;
        }

        private static GameObject Save(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
