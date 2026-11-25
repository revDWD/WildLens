using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WildLens.Data;
using WildLens.Photography;

namespace WildLens.UI
{
    /// <summary>
    /// The print that comes out after each photograph.
    ///
    /// Ordinary shots: a small card slides in at the bottom-right with the picture, species, quality and a hint.
    /// Excellent shots are treated as keepers: a larger print rises at the bottom-centre with a warm gold mat,
    /// a field-note caption (species · behaviour · focal length) and stays longer. Every print briefly "develops"
    /// from white, a small moment that makes the capture feel physical. All fades on its own.
    /// </summary>
    public class PhotoPreviewView
    {
        private const float FadeSeconds = 0.45f;
        private const float SlideSeconds = 0.25f;
        private const float DevelopSeconds = 0.5f;
        private static readonly Color Paper = new Color(0.96f, 0.95f, 0.91f, 1f);
        private static readonly Color KeeperMat = new Color(0.93f, 0.84f, 0.6f, 1f);

        private readonly RectTransform _card;
        private readonly Image _cardImage;
        private readonly CanvasGroup _group;
        private readonly RectTransform _frame;
        private readonly RawImage _photo;
        private readonly Image _developWash;
        private readonly AspectRatioFitter _fitter;
        private readonly Text _caption;
        private readonly Text _stamp;

        private Vector2 _restPos, _offset;
        private float _showSeconds;
        private float _age = float.MaxValue;

        public PhotoPreviewView(RectTransform parent)
        {
            _cardImage = UIFactory.CreateImage("Photo Print", parent, Paper);
            _card = _cardImage.rectTransform;
            _group = _cardImage.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            var frame = UIFactory.CreateImage("Photo Area", _card, new Color(0.08f, 0.08f, 0.08f, 1f));
            _frame = frame.rectTransform;
            _photo = UIFactory.CreateRawImage("Photo", _frame);
            UIFactory.Stretch(_photo.rectTransform);
            _fitter = _photo.gameObject.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _fitter.aspectRatio = 16f / 9f;
            _developWash = UIFactory.CreateImage("Develop", _frame, Color.white);
            UIFactory.Stretch(_developWash.rectTransform);

            _caption = UIFactory.CreateText("Caption", _card, "", 21, UIFactory.Ink, TextAnchor.UpperLeft);
            _caption.lineSpacing = 1.05f;
            _stamp = UIFactory.CreateText("Keeper", _card, "KEEPER", 18, new Color(0.55f, 0.36f, 0.06f), TextAnchor.UpperRight, FontStyle.Bold);
        }

        public void Show(PhotoResult result)
        {
            _photo.texture = result.thumbnail;
            if (result.thumbnail != null) _fitter.aspectRatio = result.thumbnail.width / (float)result.thumbnail.height;

            bool keeper = false;
            if (result.identifications != null)
                foreach (var id in result.identifications)
                    if (PhotoQualityEvaluator.Parse(id.subject.quality) == PhotoQuality.Excellent) keeper = true;
            Layout(keeper);

            var sb = new StringBuilder();
            if (result.identifications == null || result.identifications.Count == 0)
            {
                sb.Append("<i>No wildlife identified</i>");
                string reason = PhotoAnalyzer.Describe(result.nearMiss);
                if (!string.IsNullOrEmpty(reason)) sb.Append("\n<size=16><color=#6B6458>").Append(reason).Append("</color></size>");
            }
            else
            {
                int shown = 0;
                foreach (var id in result.identifications)
                {
                    if (shown++ >= (keeper ? 2 : 3)) break;
                    var q = PhotoQualityEvaluator.Parse(id.subject.quality);
                    sb.Append("<b>").Append(id.species.displayName).Append("</b>  <color=#")
                      .Append(ColorUtility.ToHtmlStringRGB(QualityColor(q))).Append('>').Append(id.subject.quality).Append("</color>");
                    if (id.isNewSpecies) sb.Append("  <color=#3E7A2E><size=17>new</size></color>");
                    else if (id.isFirstPhoto) sb.Append("  <color=#3E7A2E><size=17>first photo</size></color>");
                    else if (id.isNewBest) sb.Append("  <color=#3E7A2E><size=17>new best</size></color>");
                    if (keeper && q == PhotoQuality.Excellent)
                    {
                        // Field-note caption: what it was doing and how it was taken.
                        sb.Append("\n<size=17><i>").Append(id.species.scientificName).Append("</i> · ")
                          .Append(PhotoAnalyzer.DescribeActivity(id.subject.activity));
                        if (result.record != null && result.record.focalLength > 0f)
                            sb.Append(" · ").Append(Mathf.RoundToInt(result.record.focalLength)).Append(" mm");
                        sb.Append("</size>");
                    }
                    else if (!string.IsNullOrEmpty(id.subject.hint))
                        sb.Append("\n<size=16><color=#6B6458>").Append(id.subject.hint).Append("</color></size>");
                    sb.Append('\n');
                }
            }
            _caption.text = sb.ToString().TrimEnd('\n');
            _age = 0f;
        }

        private void Layout(bool keeper)
        {
            _stamp.enabled = keeper;
            _cardImage.color = keeper ? KeeperMat : Paper;
            if (keeper)
            {
                // Larger print, bottom-centre, rising from below.
                _restPos = new Vector2(0f, 40f);
                _offset = new Vector2(0f, -70f);
                UIFactory.Place(_card, new Vector2(0.5f, 0f), _restPos, new Vector2(560f, 430f));
                UIFactory.Place(_frame, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(528f, 297f));
                UIFactory.Place(_caption.rectTransform, new Vector2(0f, 0f), new Vector2(20f, 12f), new Vector2(430f, 100f));
                UIFactory.Place(_stamp.rectTransform, new Vector2(1f, 0f), new Vector2(-18f, 84f), new Vector2(120f, 26f));
                _showSeconds = 5.5f;
            }
            else
            {
                _restPos = new Vector2(-36f, 36f);
                _offset = new Vector2(60f, 0f);
                UIFactory.Place(_card, new Vector2(1f, 0f), _restPos, new Vector2(352f, 300f));
                UIFactory.Place(_frame, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(332f, 187f));
                UIFactory.Place(_caption.rectTransform, new Vector2(0f, 0f), new Vector2(14f, 10f), new Vector2(324f, 92f));
                _showSeconds = 3.8f;
            }
        }

        public void Update(float dt)
        {
            if (_age == float.MaxValue) return;
            _age += dt;
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_age / SlideSeconds));
            float fade = 1f - Mathf.Clamp01((_age - _showSeconds) / FadeSeconds);
            _group.alpha = slide * fade;
            _card.anchoredPosition = _restPos + _offset * (1f - slide);
            float develop = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_age - 0.1f) / DevelopSeconds));
            _developWash.color = new Color(1f, 1f, 1f, develop * 0.9f);
            if (_age > _showSeconds + FadeSeconds) { _age = float.MaxValue; _group.alpha = 0f; }
        }

        private static Color QualityColor(PhotoQuality q)
        {
            switch (q)
            {
                case PhotoQuality.Excellent: return new Color(0.72f, 0.5f, 0.08f);
                case PhotoQuality.Good: return new Color(0.24f, 0.46f, 0.2f);
                default: return new Color(0.45f, 0.42f, 0.38f);
            }
        }
    }
}
