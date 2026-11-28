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
        /// <summary>Macro lens on (1.5).</summary>
        public bool macro;
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
        // One thin line weight and one warm white throughout; grade colours are quiet (1.9).
        private static readonly Color Line = UiStyle.WithAlpha(UiStyle.Text, 0.88f);
        private static readonly Color Dim = UiStyle.WithAlpha(UiStyle.Text, 0.35f);
        private static readonly Color GoodColor = UiStyle.Good;
        private static readonly Color ExcellentColor = UiStyle.Excellent;
        private const float Inset = 64f, CornerLength = 56f, LineThickness = 2f;
        private const float DataBaseline = 47f;     // bottom of the 30 px data row: its centre sits 62 px above the bottom edge
        // The row's left and right items start clear of the corner arms (which reach Inset + CornerLength = 120 px in from each side).
        // Checked at 16:9 (1920 wide), 16:10 (1821), 21:9 (2200) and 4:3 (1663): the centre zoom steps span 318 px around the middle,
        // so at 1663 they run x 672..991 while MACRO ends near x 318 and the focal text starts near x 1227 (its 300 px box).
        private const float RowSideInset = Inset + CornerLength + 16f;   // 136 px
        private static readonly string GoodHex = UiStyle.Hex(UiStyle.Good), ExcellentHex = UiStyle.Hex(UiStyle.Excellent);

        private readonly CanvasGroup _group;
        private readonly RectTransform _frame;
        private readonly RawImage _vignette;
        private readonly List<Image> _focusMarks = new List<Image>();
        private readonly List<Text> _levelLabels = new List<Text>();
        private readonly Text _focalText, _macroLabel;
        private int _shownFocal = -1;
        private bool _shownMacro;
        private string _shownName;
        private int _shownGrade = -2;
        private bool _shownIdentifiable;
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

            const float inset = Inset, len = CornerLength, thick = LineThickness;
            foreach (var c in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                Vector2 dir = new Vector2(c.x < 0.5f ? 1f : -1f, c.y < 0.5f ? 1f : -1f);
                UIFactory.Place(UIFactory.CreateImage("Corner H", _frame, Line).rectTransform, c, dir * inset, new Vector2(len, thick));
                UIFactory.Place(UIFactory.CreateImage("Corner V", _frame, Line).rectTransform, c, dir * inset, new Vector2(thick, len));
            }

            Color guide = UiStyle.WithAlpha(UiStyle.Text, 0.10f);
            for (int i = 1; i <= 2; i++)
            {
                var v = UIFactory.CreateImage("Third V", _frame, guide).rectTransform;
                v.anchorMin = new Vector2(i / 3f, 0f); v.anchorMax = new Vector2(i / 3f, 1f); v.sizeDelta = new Vector2(1f, 0f);
                var h = UIFactory.CreateImage("Third H", _frame, guide).rectTransform;
                h.anchorMin = new Vector2(0f, i / 3f); h.anchorMax = new Vector2(1f, i / 3f); h.sizeDelta = new Vector2(0f, 1f);
            }

            // Centre focus marks
            const float bw = 40f, bh = 28f, bl = 12f, bt = LineThickness;
            foreach (var s in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                Vector2 corner = new Vector2(s.x * bw, s.y * bh);
                var h = UIFactory.CreateImage("Focus H", _frame, Line);
                UIFactory.Place(h.rectTransform, new Vector2(0.5f, 0.5f), corner - new Vector2(s.x * bl * 0.5f, 0f), new Vector2(bl, bt));
                var v = UIFactory.CreateImage("Focus V", _frame, Line);
                UIFactory.Place(v.rectTransform, new Vector2(0.5f, 0.5f), corner - new Vector2(0f, s.y * bl * 0.5f), new Vector2(bt, bl));
                _focusMarks.Add(h); _focusMarks.Add(v);
            }

            // Data along the bottom edge, like an EVF: [focus dot  MACRO] ... [1x 2x 4x 8x 16x] ... [focal length  f-number]
            float spacing = 64f;
            float start = -spacing * (zoomLevels.Count - 1) * 0.5f;
            for (int i = 0; i < zoomLevels.Count; i++)
            {
                var label = UIFactory.CreateText("Zoom " + zoomLevels[i], _frame, zoomLevels[i].ToString("0.#") + "\u00d7", UiStyle.SizeSmall, Line, TextAnchor.MiddleCenter);
                UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(start + i * spacing, DataBaseline), new Vector2(62f, 30f));
                UiStyle.SoftShadow(label);
                _levelLabels.Add(label);
            }
            _focalText = UIFactory.CreateText("Focal Length", _frame, "", 20, UiStyle.WithAlpha(UiStyle.Text, 0.85f), TextAnchor.MiddleRight);
            UIFactory.Place(_focalText.rectTransform, new Vector2(1f, 0f), new Vector2(-RowSideInset, DataBaseline), new Vector2(300f, 30f));
            UiStyle.SoftShadow(_focalText);

            // Focus-confirm dot at the left of the row (as in an SLR viewfinder), and the lens mode beside it.
            _focusDot = UIFactory.CreateImage("Focus Confirm", _frame, GoodColor);
            UIFactory.Place(_focusDot.rectTransform, new Vector2(0f, 0f), new Vector2(RowSideInset, DataBaseline + 10f), new Vector2(10f, 10f));
            _macroLabel = UIFactory.CreateText("Mode", _frame, "MACRO", UiStyle.SizeSmall, Line, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(_macroLabel.rectTransform, new Vector2(0f, 0f), new Vector2(RowSideInset + 22f, DataBaseline), new Vector2(160f, 30f));
            UiStyle.SoftShadow(_macroLabel);
            _macroLabel.enabled = false;

            // Large transient readout when the zoom level changes: makes every step unmistakable.
            _zoomPop = UIFactory.CreateText("Zoom Pop", _frame, "", 56, Line, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(_zoomPop.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(300f, 80f));
            UiStyle.SoftShadow(_zoomPop, 0.6f);
            _zoomPop.color = new Color(1f, 1f, 1f, 0f);

            // Subject tracking box: a rect whose anchors are set to the subject's viewport rectangle.
            _subjectBox = UIFactory.CreateRect("Subject", _frame);
            _subjectGroup = _subjectBox.gameObject.AddComponent<CanvasGroup>();
            _subjectGroup.alpha = 0f;
            const float sl = 16f, st = LineThickness;
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
            UIFactory.Place(_subjectLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 30f), new Vector2(480f, 28f));
            _subjectLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            _subjectLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiStyle.SoftShadow(_subjectLabel);
            _progressBack = UIFactory.CreateImage("Observe Back", _subjectBox, new Color(0f, 0f, 0f, 0.4f)).rectTransform;
            UIFactory.Place(_progressBack, new Vector2(0.5f, 1f), new Vector2(0f, 20f), new Vector2(80f, 3f));
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
            float markScale = 1f + 0.4f * _focusPulse * _focusPulse; // a crisp snap on lock
            foreach (var m in _focusMarks) m.rectTransform.localScale = new Vector3(markScale, markScale, 1f);

            // In macro the telephoto steps do not apply: the row steps back, and MACRO takes the left of the row.
            for (int i = 0; i < _levelLabels.Count; i++)
            {
                bool current = i == s.zoomLevel && !s.macro;
                _levelLabels[i].color = s.macro ? UiStyle.WithAlpha(UiStyle.Text, 0.2f) : current ? Line : UiStyle.WithAlpha(UiStyle.Text, 0.38f);
                _levelLabels[i].fontStyle = current ? FontStyle.Bold : FontStyle.Normal;
                _levelLabels[i].fontSize = current ? UiStyle.SizeBody : UiStyle.SizeSmall;
            }
            int mm = Mathf.RoundToInt(s.focalLength);
            if (mm != _shownFocal || s.macro != _shownMacro)
            {
                _shownFocal = mm;
                _shownMacro = s.macro;
                _focalText.text = mm + " mm   f/" + (s.macro ? "8" : "4");
                _macroLabel.enabled = s.macro;
            }
            _lockBlend = Mathf.MoveTowards(_lockBlend, s.focusLocked ? 1f : 0f, dt * 10f);
            _focusDot.color = new Color(GoodColor.r, GoodColor.g, GoodColor.b, Mathf.Lerp(0.12f, 1f, Mathf.Max(_lockBlend, _focusPulse)));
            _focusDot.rectTransform.localScale = Vector3.one * (1f + 0.9f * _focusPulse);

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
            bool ident = s.subject.identifiable;
            int grade = known && ident ? (int)s.subject.previewQuality : -1;
            if (!ReferenceEquals(s.subjectName, _shownName) || grade != _shownGrade || ident != _shownIdentifiable)
            {
                _shownName = s.subjectName; _shownGrade = grade; _shownIdentifiable = ident;
                // The grade is a word beside the name, in a restrained colour: the brackets already show it, this reads it.
                string word = grade < 0 ? "" : "   <color=#" + (grade == (int)PhotoQuality.Excellent ? ExcellentHex : grade == (int)PhotoQuality.Good ? GoodHex : UiStyle.Hex(Line))
                                                 + ">" + s.subject.previewQuality + "</color>";
                _subjectLabel.text = known ? s.subjectName + word : ident ? "Observing\u2026" : "";
            }
            _subjectLabel.color = known ? Line : UiStyle.WithAlpha(UiStyle.Text, 0.7f);
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
