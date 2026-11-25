using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WildLens.UI
{
    /// <summary>
    /// Camera-mode overlay: soft vignette, frame corners, faint rule-of-thirds guides, focus brackets that turn
    /// green when a subject sits under them, and the zoom level row (1× 2× 4× 8×) along the bottom.
    /// Fades and settles in with the camera-mode blend so raising the camera reads as one motion.
    /// </summary>
    public class ViewfinderView
    {
        private static readonly Color Line = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color FocusColor = new Color(0.62f, 1f, 0.55f, 1f);

        private readonly CanvasGroup _group;
        private readonly RectTransform _frame;
        private readonly List<Image> _focusMarks = new List<Image>();
        private readonly List<Text> _levelLabels = new List<Text>();
        private float _focus;

        public ViewfinderView(RectTransform parent, IReadOnlyList<float> zoomLevels)
        {
            var root = UIFactory.CreateRect("Viewfinder", parent);
            UIFactory.Stretch(root);
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            var vignette = UIFactory.CreateRawImage("Vignette", root);
            UIFactory.Stretch(vignette.rectTransform);
            vignette.texture = CreateVignette(128);
            vignette.color = new Color(0f, 0f, 0f, 0.55f);

            _frame = UIFactory.CreateRect("Frame", root);
            UIFactory.Stretch(_frame);

            // Frame corners
            const float inset = 70f, len = 70f, thick = 3f;
            foreach (var c in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                Vector2 dir = new Vector2(c.x < 0.5f ? 1f : -1f, c.y < 0.5f ? 1f : -1f);
                UIFactory.Place(UIFactory.CreateImage("Corner H", _frame, Line).rectTransform, c, dir * inset, new Vector2(len, thick));
                UIFactory.Place(UIFactory.CreateImage("Corner V", _frame, Line).rectTransform, c, dir * inset, new Vector2(thick, len));
            }

            // Rule-of-thirds guides
            Color guide = new Color(1f, 1f, 1f, 0.12f);
            for (int i = 1; i <= 2; i++)
            {
                var v = UIFactory.CreateImage("Third V", _frame, guide).rectTransform;
                v.anchorMin = new Vector2(i / 3f, 0f); v.anchorMax = new Vector2(i / 3f, 1f); v.sizeDelta = new Vector2(1.5f, 0f);
                var h = UIFactory.CreateImage("Third H", _frame, guide).rectTransform;
                h.anchorMin = new Vector2(0f, i / 3f); h.anchorMax = new Vector2(1f, i / 3f); h.sizeDelta = new Vector2(0f, 1.5f);
            }

            // Focus brackets around the centre
            const float bw = 64f, bh = 44f, bl = 16f, bt = 2f;
            foreach (var s in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                Vector2 corner = new Vector2(s.x * bw, s.y * bh);
                var h = UIFactory.CreateImage("Focus H", _frame, Line);
                UIFactory.Place(h.rectTransform, new Vector2(0.5f, 0.5f), corner - new Vector2(s.x * bl * 0.5f, 0f), new Vector2(bl, bt));
                var v = UIFactory.CreateImage("Focus V", _frame, Line);
                UIFactory.Place(v.rectTransform, new Vector2(0.5f, 0.5f), corner - new Vector2(0f, s.y * bl * 0.5f), new Vector2(bt, bl));
                _focusMarks.Add(h); _focusMarks.Add(v);
            }

            // Zoom level row
            float spacing = 74f;
            float start = -spacing * (zoomLevels.Count - 1) * 0.5f;
            for (int i = 0; i < zoomLevels.Count; i++)
            {
                var label = UIFactory.CreateText("Zoom " + zoomLevels[i], _frame, zoomLevels[i].ToString("0.#") + "×", 24, Line, TextAnchor.MiddleCenter);
                UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(start + i * spacing, 96f), new Vector2(70f, 34f));
                UIFactory.AddShadow(label);
                _levelLabels.Add(label);
            }
        }

        public void Update(float blend, int levelIndex, bool subjectInFocus, float dt)
        {
            _group.alpha = blend;
            _frame.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, blend);

            _focus = Mathf.MoveTowards(_focus, subjectInFocus ? 1f : 0f, dt * 8f);
            Color fc = Color.Lerp(Line, FocusColor, _focus);
            foreach (var m in _focusMarks) m.color = fc;

            for (int i = 0; i < _levelLabels.Count; i++)
            {
                bool current = i == levelIndex;
                _levelLabels[i].color = current ? Line : new Color(1f, 1f, 1f, 0.38f);
                _levelLabels[i].fontStyle = current ? FontStyle.Bold : FontStyle.Normal;
                _levelLabels[i].fontSize = current ? 28 : 22;
            }
        }

        private static Texture2D CreateVignette(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f) * 2f - 1f, v = y / (size - 1f) * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v) / 1.414f;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, r)));
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
