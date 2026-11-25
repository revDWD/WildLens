using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Core
{
    /// <summary>
    /// Creates simple lit materials that work in URP, HDRP or the Built-in pipeline.
    /// Used for placeholder art; real art should replace these with authored materials.
    /// </summary>
    public static class MaterialLibrary
    {
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();
        private static Shader _litShader;

        public static Shader LitShader
        {
            get
            {
                if (_litShader != null) return _litShader;
                _litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (_litShader == null) _litShader = Shader.Find("HDRP/Lit");
                if (_litShader == null) _litShader = Shader.Find("Standard");
                if (_litShader == null) _litShader = Shader.Find("Diffuse");
                return _litShader;
            }
        }

        /// <summary>Returns a shared runtime material for the colour (cached).</summary>
        public static Material Get(Color color, float smoothness = 0.12f)
        {
            int key = ((Color32)color).GetHashCode() ^ Mathf.RoundToInt(smoothness * 100f) * 7919;
            if (Cache.TryGetValue(key, out var mat) && mat != null) return mat;
            mat = Create(color, smoothness);
            Cache[key] = mat;
            return mat;
        }

        /// <summary>Creates a new (uncached) material instance.</summary>
        public static Material Create(Color color, float smoothness = 0.12f, Texture texture = null)
        {
            var mat = new Material(LitShader) { name = "WL_" + ColorUtility.ToHtmlStringRGB(color) };
            ApplyColor(mat, color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (texture != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            }
            mat.enableInstancing = true;
            return mat;
        }

        public static void ApplyColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }

        /// <summary>Two-sided rendering where the shader supports it (URP Lit "_Cull", Standard ignores).</summary>
        public static void MakeDoubleSided(Material mat)
        {
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            if (mat.HasProperty("_CullMode")) mat.SetFloat("_CullMode", 0f);
            mat.doubleSidedGI = true;
        }
    }
}
