using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WildLens.Photography;

namespace WildLens.UI
{
    /// <summary>
    /// Small print-style card that slides in at the bottom-right after each photo: the picture, then one line
    /// per species identified with its quality (and a short hint for anything below Excellent). Fades out on its own.
    /// </summary>
    public class PhotoPreviewView
    {
        private const float ShowSeconds = 3.8f;
        private const float FadeSeconds = 0.4f;
        private const float SlideSeconds = 0.22f;

        private readonly RectTransform _card;
        private readonly CanvasGroup _group;
        private readonly RawImage _photo;
        private readonly AspectRatioFitter _fitter;
        private readonly Text _caption;
        private readonly Vector2 _restPos = new Vector2(-36f, 36f);
        private float _age = float.MaxValue;

        public PhotoPreviewView(RectTransform parent)
        {
            var bg = UIFactory.CreateImage("Photo Preview", parent, new Color(0.96f, 0.95f, 0.91f, 1f));
            _card = bg.rectTransform;
            UIFactory.Place(_card, new Vector2(1f, 0f), _restPos, new Vector2(352f, 300f));
            _group = bg.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            var frame = UIFactory.CreateImage("Photo Area", _card, new Color(0.1f, 0.1f, 0.1f, 1f));
            UIFactory.Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(332f, 187f));
            _photo = UIFactory.CreateRawImage("Photo", frame.transform);
            UIFactory.Stretch(_photo.rectTransform);
            _fitter = _photo.gameObject.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _fitter.aspectRatio = 16f / 9f;

            _caption = UIFactory.CreateText("Caption", _card, "", 21, UIFactory.Ink, TextAnchor.UpperLeft);
            UIFactory.Place(_caption.rectTransform, new Vector2(0f, 0f), new Vector2(14f, 10f), new Vector2(324f, 92f));
            _caption.lineSpacing = 1.05f;
        }

        public void Show(PhotoResult result)
        {
            _photo.texture = result.thumbnail;
            if (result.thumbnail != null) _fitter.aspectRatio = result.thumbnail.width / (float)result.thumbnail.height;

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
                    if (shown++ >= 3) break;
                    var q = PhotoQualityEvaluator.Parse(id.subject.quality);
                    sb.Append("<b>").Append(id.species.displayName).Append("</b>  <color=#")
                      .Append(ColorUtility.ToHtmlStringRGB(QualityColor(q))).Append('>').Append(id.subject.quality).Append("</color>");
                    if (id.isNewSpecies) sb.Append("  <color=#3E7A2E><size=17>new</size></color>");
                    else if (id.isFirstPhoto) sb.Append("  <color=#3E7A2E><size=17>first photo</size></color>");
                    else if (id.isNewBest) sb.Append("  <color=#3E7A2E><size=17>new best</size></color>");
                    if (!string.IsNullOrEmpty(id.subject.hint))
                        sb.Append("\n<size=16><color=#6B6458>").Append(id.subject.hint).Append("</color></size>");
                    sb.Append('\n');
                }
            }
            _caption.text = sb.ToString().TrimEnd('\n');
            _age = 0f;
        }

        public void Update(float dt)
        {
            if (_age == float.MaxValue) return;
            _age += dt;
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_age / SlideSeconds));
            float fade = 1f - Mathf.Clamp01((_age - ShowSeconds) / FadeSeconds);
            _group.alpha = slide * fade;
            _card.anchoredPosition = _restPos + new Vector2(60f * (1f - slide), 0f);
            if (_age > ShowSeconds + FadeSeconds) { _age = float.MaxValue; _group.alpha = 0f; }
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
