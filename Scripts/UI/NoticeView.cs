using UnityEngine;
using UnityEngine.UI;

namespace WildLens.UI
{
    /// <summary>
    /// The one quiet notice style (1.9): a small title and one line on a faint dark pill, fading in just under the top edge
    /// at the same place every time and fading out on its own. At most two are on screen: a third replaces the older one
    /// (a priority notice, such as the stale-scene warning, is only replaced by another priority notice when it must be).
    ///
    /// A hidden place is the exception, by design: its name appears once, large and unhurried, centre-top, then is gone
    /// (<see cref="ShowPlace"/>). Nothing here is counted.
    /// </summary>
    public class NoticeView
    {
        private const int SlotCount = 2;
        private const float TopY = UiStyle.Margin;          // pill top edge, from the top of the canvas
        private const float Gap = 10f;
        private const float MaxWidth = 1100f;
        // A notice is a title and one line, never wrapped: it shrinks to fit (22/18, 20/16, 18/14 px) and is clipped by the pill's
        // width after that. Its height is capped, so the two stacked notices never reach the place title below them.
        private const float MaxSlotHeight = 100f;
        private const float StackBottom = TopY + SlotCount * MaxSlotHeight + (SlotCount - 1) * Gap;   // 266 px
        private const float PlaceTop = StackBottom + 20f;

        private sealed class Slot
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image pill;
            public Text text;
            public float age = float.MaxValue, duration;
            public bool priority;
            public float height, y = float.NaN;
            public bool Active => age != float.MaxValue;
        }

        private readonly Slot[] _slots = new Slot[SlotCount];

        // The place name
        private readonly CanvasGroup _placeGroup;
        private readonly Text _placeName, _placeNote;
        private readonly RectTransform _placeRule;
        private float _placeAge = float.MaxValue;
        private const float PlaceIn = 1.0f, PlaceHold = 4.2f, PlaceOut = 1.6f;

        public NoticeView(RectTransform parent)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var s = new Slot();
                s.root = UIFactory.CreateRect("Notice " + i, parent);
                s.group = s.root.gameObject.AddComponent<CanvasGroup>();
                s.group.alpha = 0f;
                s.pill = UiStyle.CreatePill("Pill", s.root, UiStyle.PillFill);
                UIFactory.Stretch(s.pill.rectTransform);
                s.text = UIFactory.CreateText("Text", s.root, "", UiStyle.SizeBody, UiStyle.Text, TextAnchor.MiddleCenter);
                s.text.lineSpacing = 1.1f;
                s.text.horizontalOverflow = HorizontalWrapMode.Overflow;
                s.root.gameObject.AddComponent<RectMask2D>();
                UiStyle.SoftShadow(s.text);
                s.root.gameObject.SetActive(false);
                _slots[i] = s;
            }

            // Place name: centre-top, a quarter of the way down, in a calm italic with a short rule beneath.
            var place = UIFactory.CreateRect("Place Name", parent);
            UIFactory.Place(place, new Vector2(0.5f, 1f), new Vector2(0f, -PlaceTop), new Vector2(1200f, 150f));
            _placeGroup = place.gameObject.AddComponent<CanvasGroup>();
            _placeGroup.alpha = 0f;
            _placeName = UIFactory.CreateText("Name", place, "", 58, UiStyle.Text, TextAnchor.UpperCenter, FontStyle.Italic);
            UIFactory.Place(_placeName.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1200f, 76f));
            _placeName.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiStyle.SoftShadow(_placeName, 0.65f);
            _placeRule = UIFactory.CreateImage("Rule", place, UiStyle.WithAlpha(UiStyle.Text, 0.55f)).rectTransform;
            UIFactory.Place(_placeRule, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(96f, 1.5f));
            _placeNote = UIFactory.CreateText("Note", place, "", UiStyle.SizeBody, UiStyle.TextSoft, TextAnchor.UpperCenter);
            UIFactory.Place(_placeNote.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(1000f, 40f));
            UiStyle.SoftShadow(_placeNote, 0.65f);
            place.gameObject.SetActive(false);
        }

        /// <summary>A quiet notice: a small bold title and one line (either may be empty).</summary>
        public void Show(string title, string line, float seconds, bool priority = false, bool alert = false)
        {
            // A free slot first; else the older of the non-priority ones; else the older of both.
            Slot slot = null;
            foreach (var s in _slots) if (!s.Active) { slot = s; break; }
            if (slot == null)
            {
                foreach (var s in _slots)
                    if (!s.priority && (slot == null || s.age > slot.age)) slot = s;
                if (slot == null)
                {
                    if (!priority) return; // both slots hold priority notices: let them finish
                    slot = _slots[0].age >= _slots[1].age ? _slots[0] : _slots[1];
                }
            }

            slot.pill.color = alert ? UiStyle.PillFillAlert : UiStyle.PillFill;

            // One line each for title and text: step the sizes down until it fits the widest pill, then clip.
            int[] bodySizes = { UiStyle.SizeBody, UiStyle.SizeBody - 2, UiStyle.SizeBody - 4 };
            int[] smallSizes = { UiStyle.SizeSmall, UiStyle.SizeSmall - 2, UiStyle.SizeSmall - 4 };
            for (int k = 0; k < bodySizes.Length; k++)
            {
                slot.text.fontSize = bodySizes[k];
                slot.text.text = Compose(title, line, alert, smallSizes[k]);
                if (slot.text.preferredWidth + 64f <= MaxWidth) break;
            }

            float width = Mathf.Clamp(slot.text.preferredWidth + 64f, 280f, MaxWidth);
            float textHeight = Mathf.Max(30f, slot.text.preferredHeight);
            UIFactory.Place(slot.text.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width - 48f, textHeight));
            slot.height = Mathf.Min(MaxSlotHeight, textHeight + 26f);
            slot.root.anchorMin = slot.root.anchorMax = new Vector2(0.5f, 1f);
            slot.root.pivot = new Vector2(0.5f, 1f);
            slot.root.sizeDelta = new Vector2(width, slot.height);

            slot.duration = seconds;
            slot.priority = priority;
            slot.age = 0f;
            slot.y = float.NaN;
            slot.group.alpha = 0f;
            slot.root.gameObject.SetActive(true);
        }

        private static string Compose(string title, string line, bool alert, int smallSize)
        {
            string body = string.IsNullOrEmpty(title) ? "" : "<b>" + (alert ? "<color=#" + UiStyle.Hex(UiStyle.Alert) + ">" + title + "</color>" : title) + "</b>";
            if (!string.IsNullOrEmpty(line))
                body += (body.Length > 0 ? "\n" : "") + "<size=" + smallSize + "><color=#" + UiStyle.Hex(UiStyle.Text) + "CC>" + line + "</color></size>";
            return body;
        }

        /// <summary>A hidden place: its name, once, large and calm, centre-top. No pill, no counter.</summary>
        public void ShowPlace(string name, string note)
        {
            _placeName.text = name;
            _placeNote.text = note ?? "";
            _placeAge = 0f;
            _placeGroup.alpha = 0f;
            _placeGroup.gameObject.SetActive(true);
        }

        public void Update(float dt)
        {
            // Older notice on top, newer beneath it, so a new arrival never shoves the one being read.
            var first = _slots[0];
            var second = _slots[1];
            if (first.Active && second.Active && second.age > first.age) { first = _slots[1]; second = _slots[0]; }
            float y = TopY;
            for (int i = 0; i < SlotCount; i++)
            {
                var s = i == 0 ? first : second;
                if (!s.Active) continue;
                s.age += dt;
                float inA = Mathf.Clamp01(s.age / UiStyle.Fade);
                float outA = 1f - Mathf.Clamp01((s.age - s.duration) / UiStyle.Fade);
                s.group.alpha = inA * outA;
                float slide = (1f - Mathf.SmoothStep(0f, 1f, inA)) * 10f; // settles down into place
                float target = -y + slide;
                if (s.y != target) { s.y = target; s.root.anchoredPosition = new Vector2(0f, target); }
                y += s.height + Gap;
                if (s.age > s.duration + UiStyle.Fade)
                {
                    s.age = float.MaxValue;
                    s.group.alpha = 0f;
                    s.root.gameObject.SetActive(false);
                }
            }

            if (_placeAge != float.MaxValue)
            {
                _placeAge += dt;
                float a = Mathf.Clamp01(_placeAge / PlaceIn) * (1f - Mathf.Clamp01((_placeAge - PlaceIn - PlaceHold) / PlaceOut));
                _placeGroup.alpha = a * a * (3f - 2f * a);
                if (_placeAge > PlaceIn + PlaceHold + PlaceOut)
                {
                    _placeAge = float.MaxValue;
                    _placeGroup.alpha = 0f;
                    _placeGroup.gameObject.SetActive(false);
                }
            }
        }
    }
}
