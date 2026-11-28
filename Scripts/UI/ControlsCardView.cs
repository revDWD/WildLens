using UnityEngine;
using UnityEngine.UI;
using WildLens.Core;

namespace WildLens.UI
{
    /// <summary>
    /// "Field Guide: Controls" (1.9): one paper card, in the journal's style, that lists every control in four groups
    /// (On foot, Camera, Trail bike, Journal). H opens and closes it (Esc closes it too). It opens the way the journal
    /// does: the world pauses, the cursor is freed and gameplay input is ignored until it closes.
    /// </summary>
    public class ControlsCardView
    {
        private readonly Canvas _canvas;
        private readonly WildLensGame _game;

        public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

        public ControlsCardView(Transform parent, WildLensGame game)
        {
            _game = game;
            _canvas = UIFactory.CreateCanvas("Controls Canvas", 10, parent);
            var root = (RectTransform)_canvas.transform;

            var dim = UIFactory.CreateImage("Dim", root, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Stretch(dim.rectTransform);

            // 1480 x 800: leaves at least 67 px above and below on the shortest canvas (21:9 is ~935 px tall).
            var book = UIFactory.CreateImage("Card", root, UIFactory.Paper);
            UIFactory.Place(book.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1480f, 800f));
            var b = book.rectTransform;
            var shadow = book.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(6f, -8f);

            var spine = UIFactory.CreateImage("Spine", b, UIFactory.Accent);
            spine.rectTransform.anchorMin = new Vector2(0f, 0f);
            spine.rectTransform.anchorMax = new Vector2(0f, 1f);
            spine.rectTransform.pivot = new Vector2(0f, 0.5f);
            spine.rectTransform.sizeDelta = new Vector2(14f, 0f);

            var title = UIFactory.CreateText("Title", b, "Field Guide  <size=24><i>Controls</i></size>", 44,
                UIFactory.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(56f, -34f), new Vector2(900f, 60f));
            var sub = UIFactory.CreateText("Subtitle", b, "A mouse and a keyboard are all you need.", 22,
                Faint(0.65f), TextAnchor.UpperRight, FontStyle.Italic);
            UIFactory.Place(sub.rectTransform, new Vector2(1f, 1f), new Vector2(-56f, -50f), new Vector2(700f, 32f));
            var rule = UIFactory.CreateImage("Rule", b, Faint(0.4f));
            UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(1380f, 2f));

            // Every key name comes from GameInput.Labels, so a rebinding there shows up here.
            const float colA = 56f, colB = 770f, top = -136f, row2 = -440f;

            Group(b, "On foot", colA, top, new[]
            {
                new[] { GameInput.Labels.Move, "Walk" },
                new[] { GameInput.Labels.Sprint, "Sprint" },
                new[] { GameInput.Labels.Crouch, "Crouch, to creep closer" },
                new[] { "Hold " + GameInput.Labels.Wait, "Let time pass" },
                new[] { GameInput.Labels.Cancel, "Free the mouse" },
                new[] { GameInput.Labels.Controls, "This page" },
            }, null);

            Group(b, "Camera", colB, top, new[]
            {
                new[] { "Hold " + GameInput.Labels.RaiseCamera, "Raise the camera" },
                new[] { GameInput.Labels.Scroll + "  ·  " + GameInput.Labels.ZoomIn + " / " + GameInput.Labels.ZoomOut, "Zoom, 1× to 16×" },
                new[] { GameInput.Labels.ShutterClick + "  ·  " + GameInput.Labels.Shutter, "Take the photograph" },
                new[] { GameInput.Labels.Macro, "Macro lens (camera up)" },
                new[] { "Hold in view", "Identify an animal" },
            }, null);

            Group(b, "Trail bike", colA, row2, new[]
            {
                new[] { GameInput.Labels.Interact, "Ride it, or park and step off" },
                new[] { GameInput.Labels.Forward + " / " + GameInput.Labels.Back, "Throttle and brake" },
                new[] { GameInput.Labels.Left + " / " + GameInput.Labels.Right, "Steer" },
                new[] { "Stopped", "Raise the camera" },
            }, "Engines carry: park short of wildlife and walk the last stretch.");

            Group(b, "Journal", colB, row2, new[]
            {
                new[] { GameInput.Labels.Journal + "  ·  " + GameInput.Labels.JournalAlt, "Open the field journal" },
                new[] { GameInput.Labels.Forward + " / " + GameInput.Labels.Back + "  ·  wheel", "Choose an entry" },
                new[] { GameInput.Labels.Left + " / " + GameInput.Labels.Right, "Wildlife  ·  Field notebook" },
                new[] { GameInput.Labels.Cancel, "Close" },
            }, null);

            var footer = UIFactory.CreateText("Footer", b, GameInput.Labels.Controls + "  /  " + GameInput.Labels.Cancel + "   close", 20,
                Faint(0.7f), TextAnchor.LowerCenter);
            UIFactory.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(900f, 30f));

            _canvas.gameObject.SetActive(false);
        }

        private static Color Faint(float a) => new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, a);

        /// <summary>One group: a heading, a rule, and two aligned columns (keys, actions), then an optional italic note.</summary>
        private static void Group(RectTransform parent, string heading, float x, float y, string[][] rows, string note)
        {
            const float width = 640f, keyWidth = 250f, pitch = 1.28f;
            var h = UIFactory.CreateText("Heading " + heading, parent, heading, 30, UIFactory.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(h.rectTransform, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, 40f));
            var r = UIFactory.CreateImage("Heading Rule", parent, Faint(0.25f));
            UIFactory.Place(r.rectTransform, new Vector2(0f, 1f), new Vector2(x, y - 44f), new Vector2(width, 1.5f));

            var keys = new System.Text.StringBuilder();
            var acts = new System.Text.StringBuilder();
            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0) { keys.Append('\n'); acts.Append('\n'); }
                keys.Append(rows[i][0]);
                acts.Append(rows[i][1]);
            }
            float listTop = y - 58f;
            float listHeight = rows.Length * 24f * 1.16f * pitch + 12f;
            var k = UIFactory.CreateText("Keys", parent, keys.ToString(), 24, UIFactory.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(k.rectTransform, new Vector2(0f, 1f), new Vector2(x, listTop), new Vector2(keyWidth, listHeight));
            var a = UIFactory.CreateText("Actions", parent, acts.ToString(), 24, UIFactory.Ink, TextAnchor.UpperLeft);
            UIFactory.Place(a.rectTransform, new Vector2(0f, 1f), new Vector2(x + keyWidth + 14f, listTop), new Vector2(width - keyWidth - 14f, listHeight));
            foreach (var t in new[] { k, a })
            {
                t.lineSpacing = pitch;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; // a long line must never wrap: it would break the row alignment
            }

            if (!string.IsNullOrEmpty(note))
            {
                var n = UIFactory.CreateText("Note", parent, note, 20, Faint(0.7f), TextAnchor.UpperLeft, FontStyle.Italic);
                UIFactory.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(x, listTop - listHeight - 4f), new Vector2(width, 30f));
                n.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        /// <summary>Call every frame (also while the HUD is hidden). Returns true on the frame it opens.</summary>
        public bool Update()
        {
            if (IsOpen)
            {
                if (GameInput.ControlsTogglePressed || GameInput.CancelPressed) SetOpen(false);
                return false;
            }
            if (_game != null && !_game.IsUiOpen && _game.UiClosedFrame != Time.frameCount && GameInput.ControlsTogglePressed)
            {
                SetOpen(true);
                return true;
            }
            return false;
        }

        public void SetOpen(bool open)
        {
            if (_canvas == null || IsOpen == open) return;
            _canvas.gameObject.SetActive(open);
            if (_game != null) _game.SetUiOpen(open);
        }
    }
}
