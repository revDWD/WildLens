using UnityEngine;
using UnityEngine.UI;

namespace WildLens.UI
{
    /// <summary>
    /// One short line near the top of the screen that fades in and out: species identified by observation,
    /// lens level chosen while the camera is lowered. A newer notice replaces the current one.
    /// </summary>
    public class NoticeView
    {
        private const float Fade = 0.25f;

        private readonly Text _text;
        private float _age = float.MaxValue, _duration;

        public NoticeView(RectTransform parent)
        {
            _text = UIFactory.CreateText("Notice", parent, "", 26, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(_text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 70f));
            UIFactory.AddShadow(_text, 2f);
            _text.color = new Color(1f, 1f, 1f, 0f);
        }

        public void Show(string richText, float seconds)
        {
            _text.text = richText;
            _duration = seconds;
            _age = 0f;
        }

        public void Update(float dt)
        {
            if (_age == float.MaxValue) return;
            _age += dt;
            float a = Mathf.Clamp01(_age / Fade) * (1f - Mathf.Clamp01((_age - _duration) / Fade));
            _text.color = new Color(1f, 1f, 1f, a);
            if (_age > _duration + Fade) _age = float.MaxValue;
        }
    }
}
