using UnityEngine;
using UnityEngine.UI;

namespace WildLens.UI
{
    /// <summary>
    /// The viewfinder blackout of a real shutter: two curtains snap closed from top and bottom and reopen,
    /// about 0.17 s in total. Triggered just after the frame is captured, so it never appears in photos.
    /// </summary>
    public class ShutterEffectView
    {
        private const float CloseTime = 0.05f;
        private const float HoldTime = 0.03f;
        private const float OpenTime = 0.09f;

        private readonly RectTransform _top, _bottom;
        private float _t = -1f;

        public ShutterEffectView(RectTransform parent)
        {
            _top = UIFactory.CreateImage("Shutter Top", parent, Color.black).rectTransform;
            _bottom = UIFactory.CreateImage("Shutter Bottom", parent, Color.black).rectTransform;
            foreach (var r in new[] { _top, _bottom }) { r.offsetMin = r.offsetMax = Vector2.zero; r.pivot = new Vector2(0.5f, 0.5f); }
            Apply(0f);
        }

        public void Trigger() => _t = 0f;

        public void Update(float dt)
        {
            if (_t < 0f) return;
            _t += dt;
            float closed;
            if (_t < CloseTime) closed = _t / CloseTime;
            else if (_t < CloseTime + HoldTime) closed = 1f;
            else closed = 1f - Mathf.Clamp01((_t - CloseTime - HoldTime) / OpenTime);
            Apply(closed);
            if (_t > CloseTime + HoldTime + OpenTime) _t = -1f;
        }

        private void Apply(float closed)
        {
            float h = 0.5f * closed;
            _top.anchorMin = new Vector2(0f, 1f - h); _top.anchorMax = Vector2.one;
            _bottom.anchorMin = Vector2.zero; _bottom.anchorMax = new Vector2(1f, h);
            bool visible = closed > 0.001f;
            _top.gameObject.SetActive(visible);
            _bottom.gameObject.SetActive(visible);
        }
    }
}
