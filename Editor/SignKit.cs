using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.UI;

namespace WildLens.EditorTools
{
    /// <summary>Pictograms painted on the reserve's signs (append only: icon files are named by value).</summary>
    public enum SignIcon { Dawn, Day, GoldenHour, Dusk, Binoculars, Paw, Bird, Water, Tree, Bike, Walk, Disc }

    /// <summary>
    /// The reserve's sign standard, round two (1.2): readable at a glance, like a national-park trailhead.
    /// Big bold titles (about 10 cm capitals), at most one short line under them, the destination's number disc, and
    /// pictograms instead of sentences (best times as sunrise / sun / sunset / moon). Text is measured from the font's
    /// real glyph advances, so boards are sized to their words and long words shrink to fit instead of spilling over.
    /// Icons are generated 128 px alpha textures (no third-party art), drawn as alpha-cut quads.
    /// </summary>
    public static class SignKit
    {
        // TextMesh: one glyph pixel at font size 64 is 0.1 × characterSize metres.
        public const int FontSize = 64;
        public const float TitleSize = 0.024f;   // ≈ 10 cm capitals: readable from ~15 m
        public const float LineSize = 0.015f;    // ≈ 6 cm
        public const float SmallSize = 0.011f;
        private const string IconFolder = GeneratedAssets.Folder + "/Icons";
        private const string IconVersion = "v1";

        public static readonly Color Cream = new Color(0.95f, 0.91f, 0.78f);
        public static readonly Color Ink = new Color(0.12f, 0.1f, 0.07f);
        public static readonly Color Green = new Color(0.2f, 0.33f, 0.19f);
        public static readonly Color Gold = new Color(0.98f, 0.72f, 0.3f);

        // ------------------------------------------------------------------ text

        /// <summary>Width in metres of the widest line of <paramref name="text"/> at a TextMesh character size.</summary>
        public static float TextWidth(string text, float charSize, bool bold)
        {
            var font = UIFactory.DefaultFont;
            var style = bold ? FontStyle.Bold : FontStyle.Normal;
            float best = 0f, line = 0f;
            if (font != null) font.RequestCharactersInTexture(text, FontSize, style);
            foreach (char c in text)
            {
                if (c == '\n') { best = Mathf.Max(best, line); line = 0f; continue; }
                if (font != null && font.GetCharacterInfo(c, out CharacterInfo ci, FontSize, style)) line += ci.advance;
                else line += FontSize * (char.IsUpper(c) ? 0.68f : 0.52f); // estimate if the font can't say
            }
            return Mathf.Max(best, line) * charSize * 0.1f;
        }

        /// <summary>The preferred character size, reduced if needed so the text fits <paramref name="maxWidth"/>.</summary>
        public static float Fit(string text, float preferred, float maxWidth, bool bold)
        {
            float w = TextWidth(text, preferred, bold);
            return w <= maxWidth || w <= 0f ? preferred : preferred * maxWidth / w;
        }

        /// <summary>Lettering facing +Z (the sign's front), anchored as asked.</summary>
        public static TextMesh Label(Transform parent, Vector3 localPos, string text, float charSize, Color colour,
            TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TextMesh reads on its -Z side
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = UIFactory.DefaultFont;
            tm.fontSize = FontSize;
            tm.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            tm.characterSize = charSize;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft
                ? TextAlignment.Left : TextAlignment.Center;
            tm.lineSpacing = 0.95f;
            tm.color = colour;
            var mr = go.GetComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tm;
        }

        // ------------------------------------------------------------------ icons

        /// <summary>An icon quad facing +Z, <paramref name="size"/> metres square.</summary>
        public static GameObject Icon(Transform parent, Vector3 localPos, SignIcon icon, float size, Color tint)
        {
            var go = PrimitiveMeshes.CreatePart("Icon " + icon, PrimitiveType.Quad, parent, localPos, new Vector3(size, size, 1f),
                IconMaterial(icon, tint), new Vector3(0f, 180f, 0f));
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Icons for a destination's best times, read from its best-times words.</summary>
        public static SignIcon[] TimeIcons(string bestTimes)
        {
            var list = new List<SignIcon>();
            string t = (bestTimes ?? "").ToLowerInvariant();
            if (t.Contains("dawn") || t.Contains("early morning")) list.Add(SignIcon.Dawn);
            if (t.Contains("midday") || (t.Contains("morning") && !t.Contains("early morning"))) list.Add(SignIcon.Day);
            if (t.Contains("golden") || t.Contains("late afternoon") || t.Contains("evening")) list.Add(SignIcon.GoldenHour);
            if (t.Contains("dusk")) list.Add(SignIcon.Dusk);
            return list.ToArray();
        }

        public static Color TimeTint(SignIcon icon) => icon == SignIcon.GoldenHour || icon == SignIcon.Day ? Gold : Cream;

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        public static Material IconMaterial(SignIcon icon, Color tint)
        {
            string key = $"Icon_{icon}_{ColorUtility.ToHtmlStringRGB(tint)}";
            if (Materials.TryGetValue(key, out var cached) && cached != null) return cached;
            string path = $"{GeneratedAssets.Folder}/M_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = MaterialLibrary.Create(tint, 0.05f, IconTexture(icon));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = MaterialLibrary.LitShader;
            MaterialLibrary.ApplyColor(mat, tint);
            foreach (var prop in new[] { "_BaseMap", "_MainTex" })
                if (mat.HasProperty(prop)) mat.SetTexture(prop, IconTexture(icon));
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 1f);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            MaterialLibrary.MakeDoubleSided(mat);
            EditorUtility.SetDirty(mat);
            Materials[key] = mat;
            return mat;
        }

        /// <summary>The icon's texture (white shape on transparent), generated once.</summary>
        public static Texture2D IconTexture(SignIcon icon)
        {
            string path = $"{IconFolder}/T_Icon_{icon}_{IconVersion}.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;
            GeneratedAssets.EnsureFolder(IconFolder);
            const int n = 128;
            var a = new float[n * n];
            var p = new Painter(a, n);
            Draw(icon, p);
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, a[i]);
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter imp)
            {
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = true;
                imp.mipMapsPreserveCoverage = true;
                imp.alphaTestReferenceValue = 0.5f;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Each pictogram from discs, strokes and triangles in a unit square (y up).</summary>
        private static void Draw(SignIcon icon, Painter p)
        {
            const float w = 0.075f; // stroke width
            switch (icon)
            {
                case SignIcon.Day:
                    p.Disc(0.5f, 0.5f, 0.2f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        p.Stroke(0.5f + Mathf.Cos(a) * 0.3f, 0.5f + Mathf.Sin(a) * 0.3f, 0.5f + Mathf.Cos(a) * 0.43f, 0.5f + Mathf.Sin(a) * 0.43f, w);
                    }
                    break;
                case SignIcon.Dawn:
                case SignIcon.GoldenHour:
                {
                    const float hy = 0.42f; // horizon
                    p.Disc(0.5f, hy, 0.21f, minY: hy + 0.05f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = Mathf.Lerp(0.2f, Mathf.PI - 0.2f, i / 4f);
                        p.Stroke(0.5f + Mathf.Cos(a) * 0.29f, hy + Mathf.Sin(a) * 0.29f, 0.5f + Mathf.Cos(a) * 0.41f, hy + Mathf.Sin(a) * 0.41f, w);
                    }
                    p.Stroke(0.06f, hy, 0.94f, hy, w);
                    // Arrow under the horizon: up for sunrise, down for sunset.
                    bool up = icon == SignIcon.Dawn;
                    float tip = up ? 0.3f : 0.1f, tail = up ? 0.08f : 0.32f;
                    p.Stroke(0.5f, tail, 0.5f, tip, w);
                    p.Stroke(0.5f, tip, 0.4f, tip + (up ? -0.09f : 0.09f), w);
                    p.Stroke(0.5f, tip, 0.6f, tip + (up ? -0.09f : 0.09f), w);
                    break;
                }
                case SignIcon.Dusk:
                    p.Disc(0.46f, 0.5f, 0.32f);
                    p.Disc(0.6f, 0.6f, 0.28f, erase: true);
                    p.Disc(0.78f, 0.22f, 0.04f);
                    p.Disc(0.86f, 0.42f, 0.03f);
                    break;
                case SignIcon.Binoculars:
                    p.Disc(0.3f, 0.36f, 0.19f);
                    p.Disc(0.7f, 0.36f, 0.19f);
                    p.Stroke(0.3f, 0.4f, 0.3f, 0.74f, 0.2f);
                    p.Stroke(0.7f, 0.4f, 0.7f, 0.74f, 0.2f);
                    p.Stroke(0.36f, 0.6f, 0.64f, 0.6f, 0.1f);
                    break;
                case SignIcon.Paw:
                    p.Ellipse(0.5f, 0.34f, 0.22f, 0.17f);
                    p.Ellipse(0.24f, 0.6f, 0.08f, 0.1f);
                    p.Ellipse(0.4f, 0.74f, 0.08f, 0.11f);
                    p.Ellipse(0.6f, 0.74f, 0.08f, 0.11f);
                    p.Ellipse(0.76f, 0.6f, 0.08f, 0.1f);
                    break;
                case SignIcon.Bird:
                    // A soaring bird: two curved wings meeting at a small body.
                    p.Stroke(0.06f, 0.5f, 0.18f, 0.62f, w * 1.4f);
                    p.Stroke(0.18f, 0.62f, 0.33f, 0.66f, w * 1.4f);
                    p.Stroke(0.33f, 0.66f, 0.5f, 0.48f, w * 1.4f);
                    p.Stroke(0.5f, 0.48f, 0.67f, 0.66f, w * 1.4f);
                    p.Stroke(0.67f, 0.66f, 0.82f, 0.62f, w * 1.4f);
                    p.Stroke(0.82f, 0.62f, 0.94f, 0.5f, w * 1.4f);
                    p.Disc(0.5f, 0.46f, 0.07f);
                    break;
                case SignIcon.Water:
                    p.Disc(0.5f, 0.36f, 0.24f);
                    p.Triangle(0.29f, 0.45f, 0.71f, 0.45f, 0.5f, 0.9f);
                    break;
                case SignIcon.Tree:
                    p.Triangle(0.2f, 0.3f, 0.8f, 0.3f, 0.5f, 0.62f);
                    p.Triangle(0.26f, 0.5f, 0.74f, 0.5f, 0.5f, 0.78f);
                    p.Triangle(0.32f, 0.68f, 0.68f, 0.68f, 0.5f, 0.94f);
                    p.Stroke(0.5f, 0.08f, 0.5f, 0.32f, 0.1f);
                    break;
                case SignIcon.Bike:
                    p.Ring(0.24f, 0.3f, 0.17f, w);
                    p.Ring(0.76f, 0.3f, 0.17f, w);
                    p.Stroke(0.24f, 0.3f, 0.45f, 0.56f, w);
                    p.Stroke(0.45f, 0.56f, 0.7f, 0.56f, w);
                    p.Stroke(0.7f, 0.56f, 0.76f, 0.3f, w);
                    p.Stroke(0.45f, 0.56f, 0.52f, 0.3f, w);
                    p.Stroke(0.66f, 0.7f, 0.78f, 0.7f, w);
                    p.Stroke(0.7f, 0.56f, 0.68f, 0.7f, w);
                    break;
                case SignIcon.Walk:
                    p.Disc(0.52f, 0.85f, 0.08f);
                    p.Stroke(0.5f, 0.74f, 0.46f, 0.45f, w * 1.3f);
                    p.Stroke(0.46f, 0.45f, 0.32f, 0.1f, w * 1.2f);
                    p.Stroke(0.46f, 0.45f, 0.6f, 0.26f, w * 1.2f);
                    p.Stroke(0.6f, 0.26f, 0.62f, 0.08f, w * 1.2f);
                    p.Stroke(0.5f, 0.68f, 0.34f, 0.5f, w);
                    p.Stroke(0.5f, 0.68f, 0.66f, 0.56f, w);
                    p.Stroke(0.7f, 0.66f, 0.76f, 0.06f, w * 0.8f); // walking stick
                    break;
                case SignIcon.Disc:
                    p.Disc(0.5f, 0.5f, 0.47f);
                    break;
            }
        }

        /// <summary>Anti-aliased alpha painter: shapes in unit coordinates, y up.</summary>
        private class Painter
        {
            private readonly float[] _a;
            private readonly int _n;
            public Painter(float[] a, int n) { _a = a; _n = n; }

            private void Each(System.Func<float, float, float> distance, bool erase = false, float minY = -1f)
            {
                float px = 1f / _n;
                for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    float u = (x + 0.5f) * px, v = (y + 0.5f) * px;
                    if (v < minY) continue;
                    float cover = Mathf.Clamp01(0.5f - distance(u, v) / px);
                    int i = y * _n + x;
                    _a[i] = erase ? Mathf.Min(_a[i], 1f - cover) : Mathf.Max(_a[i], cover);
                }
            }

            public void Disc(float cx, float cy, float r, bool erase = false, float minY = -1f) =>
                Each((u, v) => Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r, erase, minY);

            public void Ellipse(float cx, float cy, float rx, float ry) =>
                Each((u, v) => (Mathf.Sqrt((u - cx) * (u - cx) / (rx * rx) + (v - cy) * (v - cy) / (ry * ry)) - 1f) * Mathf.Min(rx, ry));

            public void Ring(float cx, float cy, float r, float width) =>
                Each((u, v) => Mathf.Abs(Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r) - width * 0.5f);

            public void Stroke(float x0, float y0, float x1, float y1, float width) =>
                Each((u, v) =>
                {
                    Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1), q = new Vector2(u, v);
                    Vector2 ab = b - a;
                    float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
                    return Vector2.Distance(q, a + ab * t) - width * 0.5f;
                });

            public void Triangle(float ax, float ay, float bx, float by, float cx, float cy) =>
                Each((u, v) =>
                {
                    float E(float x0, float y0, float x1, float y1)
                    {
                        float dx = x1 - x0, dy = y1 - y0, len = Mathf.Sqrt(dx * dx + dy * dy);
                        return ((u - x0) * dy - (v - y0) * dx) / len;
                    }
                    float s = Mathf.Sign((bx - ax) * (cy - ay) - (by - ay) * (cx - ax));
                    return Mathf.Max(s * E(ax, ay, bx, by), Mathf.Max(s * E(bx, by, cx, cy), s * E(cx, cy, ax, ay)));
                });
        }

        // ------------------------------------------------------------------ signs

        /// <summary>
        /// A destination or wayfinding sign: [number disc] TITLE on the top row; pictograms and one short line under it.
        /// The board is sized to its words; two posts when it is wide. Returns the sign's root (its front faces +Z).
        /// </summary>
        public static Transform Sign(Transform parent, Vector3 pos, Quaternion rot, string title, string line, SignIcon[] icons,
            int number, Material post, float maxTitleWidth = 2.1f)
        {
            icons = icons ?? new SignIcon[0];
            var sign = PropBuilder.Group("Sign: " + title, parent, pos, rot);
            bool hasLine = !string.IsNullOrEmpty(line) || icons.Length > 0;
            const float disc = 0.3f, gap = 0.07f, iconSize = 0.13f, iconStep = 0.155f, margin = 0.13f;
            float titleSize = Fit(title, TitleSize, maxTitleWidth, true);
            float titleW = TextWidth(title, titleSize, true);
            float lineSize = string.IsNullOrEmpty(line) ? LineSize : Fit(line, LineSize, maxTitleWidth, false);
            float lineW = icons.Length * iconStep + (string.IsNullOrEmpty(line) ? 0f : TextWidth(line, lineSize, false));
            float lead = number > 0 ? disc + gap : 0f;
            float boardW = lead + Mathf.Max(titleW, lineW) + margin * 2f;
            float boardH = hasLine ? 0.62f : 0.4f;
            const float boardY = 1.38f;
            float topRow = boardY + (hasLine ? 0.12f : 0f), lowRow = boardY - 0.15f;

            foreach (float x in boardW > 1.1f ? new[] { -boardW * 0.5f + 0.14f, boardW * 0.5f - 0.14f } : new[] { 0f })
                PropBuilder.Box("Post", sign, new Vector3(x, (boardY + 0.15f) * 0.5f, 0f), new Vector3(0.11f, boardY + 0.15f, 0.11f), post);
            PropBuilder.Box("Board", sign, new Vector3(0f, boardY, 0.06f), new Vector3(boardW, boardH, 0.05f), PropBuilder.SignBoard);
            PropBuilder.Box("Rule", sign, new Vector3(0f, boardY + boardH * 0.5f - 0.035f, 0.087f), new Vector3(boardW - 0.08f, 0.014f, 0.005f), PropBuilder.SignTrim);
            float z = 0.088f, x0 = -boardW * 0.5f + margin;
            if (number > 0)
            {
                Icon(sign, new Vector3(x0 + disc * 0.5f, topRow, z), SignIcon.Disc, disc, Green);
                Label(sign, new Vector3(x0 + disc * 0.5f, topRow, z + 0.003f), number.ToString(), 0.026f, Cream, TextAnchor.MiddleCenter, true);
            }
            Label(sign, new Vector3(x0 + lead, topRow, z), title, titleSize, Cream, TextAnchor.MiddleLeft, true);
            if (hasLine)
            {
                float x = x0 + lead;
                foreach (var icon in icons)
                {
                    Icon(sign, new Vector3(x + iconSize * 0.5f, lowRow, z), icon, iconSize, TimeTint(icon));
                    x += iconStep;
                }
                if (!string.IsNullOrEmpty(line))
                    Label(sign, new Vector3(x + (icons.Length > 0 ? 0.03f : 0f), lowRow, z), line, lineSize, Cream, TextAnchor.MiddleLeft);
            }
            PropBuilder.AddCollider(sign, new Vector3(0f, boardY * 0.5f, 0f), new Vector3(Mathf.Min(boardW, 0.4f), boardY, 0.15f));
            return sign;
        }

        /// <summary>
        /// An angled interpretive lectern, redrawn for reading at a glance: a large pictogram, a bold title and two short
        /// lines (no paragraphs). The face is 1.3 × 0.78 m.
        /// </summary>
        public static Transform Panel(Transform parent, Vector3 pos, Vector3 facing, SignIcon icon, string title, string body,
            Material wood, Material post)
        {
            var panelMat = GeneratedAssets.Material("PanelFace", new Color(0.88f, 0.84f, 0.7f), 0.2f);
            var g = PropBuilder.Group("Interpretive Panel: " + title, parent, pos, Quaternion.LookRotation(facing));
            PropBuilder.Box("Leg L", g, new Vector3(-0.5f, 0.45f, 0f), new Vector3(0.09f, 0.9f, 0.09f), post);
            PropBuilder.Box("Leg R", g, new Vector3(0.5f, 0.45f, 0f), new Vector3(0.09f, 0.9f, 0.09f), post);
            var face = PropBuilder.Group("Face", g, pos + Vector3.up * 1.02f, Quaternion.LookRotation(facing) * Quaternion.Euler(-32f, 0f, 0f));
            PropBuilder.Box("Frame", face, new Vector3(0f, 0f, -0.03f), new Vector3(1.4f, 0.88f, 0.05f), wood);
            PropBuilder.Box("Panel", face, Vector3.zero, new Vector3(1.3f, 0.78f, 0.02f), panelMat);
            PropBuilder.Box("Band", face, new Vector3(0f, 0.31f, 0.012f), new Vector3(1.3f, 0.16f, 0.005f), PropBuilder.SignTrim);
            const float z = 0.016f;
            float textLeft = -0.65f + 0.42f, textW = 1.3f - 0.42f - 0.08f;
            Icon(face, new Vector3(-0.65f + 0.22f, -0.07f, z), icon, 0.34f, Green);
            float ts = Fit(title, 0.019f, 1.22f, true);
            Label(face, new Vector3(-0.61f, 0.31f, z + 0.002f), title, ts, Cream, TextAnchor.MiddleLeft, true);
            float bs = Fit(body, 0.0135f, textW, false);
            Label(face, new Vector3(textLeft, -0.08f, z), body, bs, Ink, TextAnchor.MiddleLeft);
            PropBuilder.AddCollider(g, new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 1.2f, 0.4f));
            return g;
        }
    }
}
