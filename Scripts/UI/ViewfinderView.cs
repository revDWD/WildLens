using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WildLens.Photography;

namespace WildLens.UI
{
    /// <summary>What the viewfinder needs each frame (gathered by HudController).</summary>
    public struct ViewfinderState
    {
        public float blend;
        public int zoomLevel;
        public float focalLength;
        public bool hasSubject;
        public TrackedSubject subject;
        public bool subjectCentred;
        /// <summary>Focus has locked on the subject.</summary>
        public bool focusLocked;
        /// <summary>Focus is still travelling (pulling) toward its target.</summary>
        public bool focusHunting;
        /// <summary>Species name if known, else null.</summary>
        public string subjectName;
        /// <summary>0-1 observation progress for an unknown species (ignored when the name is known).</summary>
        public float observationProgress;
    }

    /// <summary>
    /// Camera-mode overlay: vignette that deepens with zoom, frame corners, faint thirds, centre focus marks,
    /// the zoom row (1× … 16×) with an equivalent focal length, and subject tracking brackets.
    ///
    /// Subject brackets wrap the tracked animal and are coloured by the photo you'd get right now:
    /// dim = too small/hidden to identify, white = Poor, green = Good, gold = Excellent. Above them, the species
    /// name if known; otherwise "Observing…" with a progress bar that fills as you hold it in view.
    /// </summary>
    public class ViewfinderView
    {
        private static readonly Color Line = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color GoodColor = new Color(0.62f, 1f, 0.55f, 1f);
        private static readonly Color ExcellentColor = new Color(1f, 0.84f, 0.38f, 1f);

        private readonly CanvasGroup _group;
        private readonly RectTransform _frame;
        private readonly RawImage _vignette;
        private readonly List<Image> _focusMarks = new List<Image>();
        private readonly List<Text> _levelLabels = new List<Text>();
        private readonly Text _focalText;
        private readonly Text _zoomPop;
        private float _zoomPopAge = float.MaxValue;
        private float _focusPulse;

        private readonly RectTransform _subjectBox;
        private readonly CanvasGroup _subjectGroup;
        private readonly List<Image> _subjectMarks = new List<Image>();
        private readonly Text _subjectLabel;
        private readonly RectTransform _progressBack, _progressFill;

        private float _focus, _subjectAlpha, _lockBlend;
        private readonly Image _focusDot;
        private Rect _smoothedRect;

        public ViewfinderView(RectTransform parent, IReadOnlyList<float> zoomLevels)
        {
            var root = UIFactory.CreateRect("Viewfinder", parent);
            UIFactory.Stretch(root);
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            _vignette = UIFactory.CreateRawImage("Vignette", root);
            UIFactory.Stretch(_vignette.rectTransform);
            _vignette.texture = CreateVignette(128);

            _frame = UIFactory.CreateRect("Frame", root);
            UIFactory.Stretch(_frame);

            const float inset = 70f, len = 70f, thick = 3f;
            foreach (var c in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                Vector2 dir = new Vector2(c.x < 0.5f ? 1f : -1f, c.y < 0.5f ? 1f : -1f);
                UIFactory.Place(UIFactory.CreateImage("Corner H", _frame, Line).rectTransform, c, dir * inset, new Vector2(len, thick));
                UIFactory.Place(UIFactory.CreateImage("Corner V", _frame, Line).rectTransform, c, dir * inset, new Vector2(thick, len));
            }

            Color guide = new Color(1f, 1f, 1f, 0.12f);
            for (int i = 1; i <= 2; i++)
            {
                var v = UIFactory.CreateImage("Third V", _frame, guide).rectTransform;
                v.anchorMin = new Vector2(i / 3f, 0f); v.anchorMax = new Vector2(i / 3f, 1f); v.sizeDelta = new Vector2(1.5f, 0f);
                var h = UIFactory.CreateImage("Third H", _frame, guide).rectTransform;
                h.anchorMin = new Vector2(0f, i / 3f); h.anchorMax = new Vector2(1f, i / 3f); h.sizeDelta = new Vector2(0f, 1.5f);
            }

            // Centre focus marks
            const float bw = 40f, bh = 28f, bl = 12f, bt = 2f;
            foreach (var s in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                Vector2 corner = new Vector2(s.x * bw, s.y * bh);
                var h = UIFactory.CreateImage("Focus H", _frame, Line);
                UIFactory.Place(h.rectTransform, new Vector2(0.5f, 0.5f), corner - new Vector2(s.x * bl * 0.5f, 0f), new Vector2(bl, bt));
                var v = UIFactory.CreateImage("Focus V", _frame, Line);
                UIFactory.Place(v.rectTransform, new Vector2(0.5f, 0.5f), corner - new Vector2(0f, s.y * bl * 0.5f), new Vector2(bt, bl));
                _focusMarks.Add(h); _focusMarks.Add(v);
            }

            // Zoom row + focal length
            float spacing = 70f;
            float start = -spacing * (zoomLevels.Count - 1) * 0.5f;
            for (int i = 0; i < zoomLevels.Count; i++)
            {
                var label = UIFactory.CreateText("Zoom " + zoomLevels[i], _frame, zoomLevels[i].ToString("0.#") + "×", 24, Line, TextAnchor.MiddleCenter);
                UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(start + i * spacing, 104f), new Vector2(68f, 34f));
                UIFactory.AddShadow(label);
                _levelLabels.Add(label);
            }
            _focalText = UIFactory.CreateText("Focal Length", _frame, "", 18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
            UIFactory.Place(_focalText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(260f, 24f));
            UIFactory.AddShadow(_focalText);

            // Focus-confirm indicator, bottom-left of the frame (as in an SLR viewfinder).
            _focusDot = UIFactory.CreateImage("Focus Confirm", _frame, GoodColor);
            UIFactory.Place(_focusDot.rectTransform, new Vector2(0f, 0f), new Vector2(98f, 98f), new Vector2(12f, 12f));

            // Large transient readout when the zoom level changes: makes every step unmistakable.
            _zoomPop = UIFactory.CreateText("Zoom Pop", _frame, "", 64, Line, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(_zoomPop.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(300f, 90f));
            UIFactory.AddShadow(_zoomPop, 2f);
            _zoomPop.color = new Color(1f, 1f, 1f, 0f);

            // Subject tracking box: a rect whose anchors are set to the subject's viewport rectangle.
            _subjectBox = UIFactory.CreateRect("Subject", _frame);
            _subjectGroup = _subjectBox.gameObject.AddComponent<CanvasGroup>();
            _subjectGroup.alpha = 0f;
            const float sl = 18f, st = 2.5f;
            foreach (var c in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                Vector2 dir = new Vector2(c.x < 0.5f ? 1f : -1f, c.y < 0.5f ? 1f : -1f);
                var h = UIFactory.CreateImage("Subject H", _subjectBox, Line);
                // Both arms start at the same point just outside the corner, forming an L.
                Vector2 outset = new Vector2(-dir.x * 6f, -dir.y * 6f);
                UIFactory.Place(h.rectTransform, c, outset, new Vector2(sl, st));
                var v = UIFactory.CreateImage("Subject V", _subjectBox, Line);
                UIFactory.Place(v.rectTransform, c, outset, new Vector2(st, sl));
                _subjectMarks.Add(h); _subjectMarks.Add(v);
            }
            _subjectLabel = UIFactory.CreateText("Subject Label", _subjectBox, "", 20, Line, TextAnchor.LowerCenter);
            UIFactory.Place(_subjectLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 30f), new Vector2(360f, 28f));
            _subjectLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            UIFactory.AddShadow(_subjectLabel);
            _progressBack = UIFactory.CreateImage("Observe Back", _subjectBox, new Color(0f, 0f, 0f, 0.45f)).rectTransform;
            UIFactory.Place(_progressBack, new Vector2(0.5f, 1f), new Vector2(0f, 20f), new Vector2(90f, 4f));
            _progressFill = UIFactory.CreateImage("Observe Fill", _progressBack, Line).rectTransform;
            _progressFill.anchorMin = Vector2.zero; _progressFill.anchorMax = new Vector2(0f, 1f);
            _progressFill.offsetMin = _progressFill.offsetMax = Vector2.zero;
        }

        /// <summary>Flash the zoom level large for a moment.</summary>
        public void ShowZoomPop(float magnification)
        {
            _zoomPop.text = magnification.ToString("0.#") + "×";
            _zoomPopAge = 0f;
        }

        /// <summary>Brief pulse of the centre focus marks when focus locks.</summary>
        public void PulseFocus() => _focusPulse = 1f;

        public void Update(in ViewfinderState s, float dt)
        {
            if (_zoomPopAge != float.MaxValue)
            {
                _zoomPopAge += dt;
                float a = 1f - Mathf.Clamp01((_zoomPopAge - 0.45f) / 0.35f);
                float scale = Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(_zoomPopAge / 0.15f));
                _zoomPop.color = new Color(1f, 1f, 1f, a * 0.95f);
                _zoomPop.rectTransform.localScale = Vector3.one * scale;
                if (_zoomPopAge > 0.8f) _zoomPopAge = float.MaxValue;
            }
            _focusPulse = Mathf.MoveTowards(_focusPulse, 0f, dt * 4f);
            _group.alpha = s.blend;
            _frame.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, s.blend);
            // The vignette deepens a little at long focal lengths: a visible cue that you're zoomed in.
            _vignette.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.45f, 0.75f, Mathf.InverseLerp(26f, 420f, s.focalLength)));

            _focus = Mathf.MoveTowards(_focus, s.subjectCentred ? 1f : 0f, dt * 8f);
            Color fc = Color.Lerp(Line, GoodColor, Mathf.Max(_focus, _focusPulse));
            foreach (var m in _focusMarks) m.color = fc;
            float markScale = 1f + 0.25f * _focusPulse;
            foreach (var m in _focusMarks) m.rectTransform.localScale = new Vector3(markScale, markScale, 1f);

            for (int i = 0; i < _levelLabels.Count; i++)
            {
                bool current = i == s.zoomLevel;
                _levelLabels[i].color = current ? Line : new Color(1f, 1f, 1f, 0.38f);
                _levelLabels[i].fontStyle = current ? FontStyle.Bold : FontStyle.Normal;
                _levelLabels[i].fontSize = current ? 28 : 22;
            }
            _focalText.text = Mathf.RoundToInt(s.focalLength) + " mm   f/4";
            _lockBlend = Mathf.MoveTowards(_lockBlend, s.focusLocked ? 1f : 0f, dt * 10f);
            _focusDot.color = new Color(GoodColor.r, GoodColor.g, GoodColor.b, Mathf.Lerp(0.12f, 1f, _lockBlend));

            UpdateSubject(s, dt);
        }

        private void UpdateSubject(in ViewfinderState s, float dt)
        {
            _subjectAlpha = Mathf.MoveTowards(_subjectAlpha, s.hasSubject ? 1f : 0f, dt * 6f);
            _subjectGroup.alpha = _subjectAlpha;
            if (!s.hasSubject) return;

            // Pad small subjects so the brackets never collapse onto them, then ease toward the target rect.
            Rect r = s.subject.screenRect;
            float minW = 0.035f, minH = 0.06f;
            Rect target = Rect.MinMaxRect(
                Mathf.Min(r.xMin, r.center.x - minW), Mathf.Min(r.yMin, r.center.y - minH),
                Mathf.Max(r.xMax, r.center.x + minW), Mathf.Max(r.yMax, r.center.y + minH));
            bool snap = _smoothedRect.width <= 0f || _subjectAlpha < 0.05f;
            float k = snap ? 1f : 1f - Mathf.Exp(-18f * dt);
            _smoothedRect = Rect.MinMaxRect(
                Mathf.Lerp(_smoothedRect.xMin, target.xMin, k), Mathf.Lerp(_smoothedRect.yMin, target.yMin, k),
                Mathf.Lerp(_smoothedRect.xMax, target.xMax, k), Mathf.Lerp(_smoothedRect.yMax, target.yMax, k));
            _subjectBox.anchorMin = _smoothedRect.min;
            _subjectBox.anchorMax = _smoothedRect.max;
            _subjectBox.offsetMin = _subjectBox.offsetMax = Vector2.zero;
            // Autofocus feel: brackets breathe while focus hunts, then snap still when it locks.
            float hunt = s.focusHunting && s.subject.identifiable ? Mathf.Sin(Time.unscaledTime * 18f) * 0.025f : 0f;
            _subjectBox.localScale = Vector3.one * (1f + hunt);

            Color c = !s.subject.identifiable ? Dim
                : s.subject.previewQuality == PhotoQuality.Excellent ? ExcellentColor
                : s.subject.previewQuality == PhotoQuality.Good ? GoodColor : Line;
            foreach (var m in _subjectMarks) m.color = c;

            bool known = !string.IsNullOrEmpty(s.subjectName);
            _subjectLabel.text = known ? s.subjectName : s.subject.identifiable ? "Observing…" : "";
            _subjectLabel.color = known ? Line : new Color(1f, 1f, 1f, 0.75f);
            bool showProgress = !known && s.subject.identifiable;
            _progressBack.gameObject.SetActive(showProgress);
            if (showProgress) _progressFill.anchorMax = new Vector2(Mathf.Clamp01(s.observationProgress), 1f);
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
