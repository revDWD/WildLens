using UnityEngine;
using UnityEngine.UI;
using WildLens.Core;

namespace WildLens.UI
{
    /// <summary>The prompts, in priority order (the first two that are wanted are shown).</summary>
    public enum PromptId { Park, Mount, Macro, Shutter, Zoom, Raise, Journal, Intro, Count }

    /// <summary>What the prompts need to know this frame (gathered by HudController; cheap, no allocation).</summary>
    public struct PromptContext
    {
        /// <summary>A menu is open or the mouse is free: nothing shows.</summary>
        public bool hidden;
        public bool riding;
        public bool bikeInReach;
        /// <summary>Riding, not already parking, and either stopped or inside a pull-off.</summary>
        public bool canPark;
        /// <summary>The pull-off's name when the bike is inside one (else null).</summary>
        public string parkPlace;
        public bool cameraLowered;
        public bool cameraRaised;
        public bool macroMode;
        public bool hasSubject;
        public bool macroNear;
        /// <summary>An animal (or a small subject) is ahead and close enough to be worth the camera.</summary>
        public bool subjectAhead;
        public int photosThisSession;
        public float secondsSincePhoto;
        public float raiseBlend;
    }

    /// <summary>
    /// A small, quiet prompt system (1.9) in place of the always-on hint block. At most two prompts at a time, bottom-centre:
    /// a key and one to three words on a faint pill. A prompt shows only when its action is possible and useful right now, fades
    /// in and out (0.25 s, unscaled), gives up after a few seconds if ignored, and stops for the session once the player has done
    /// the thing three times (or ignored it six times). Session memory only: nothing is saved.
    ///
    ///   Park     riding, stopped or in a pull-off          R  Park here · Cattail Pond
    ///   Mount    standing next to the bike                 R  Ride the bike
    ///   Macro    camera up, a small subject within 3 m     M  Macro lens
    ///   Shutter  camera up, a subject in the viewfinder    LMB  Shutter
    ///   Zoom     camera up                                 Scroll  Zoom
    ///   Raise    on foot, an animal ahead (or small life)  Hold RMB  Raise camera
    ///   Journal  once, a few seconds after the first photo J  Journal
    ///   Intro    the first seconds of a session, once      H  Controls
    /// </summary>
    public class PromptView
    {
        private const int N = (int)PromptId.Count;
        private const int SlotCount = 2;
        private const int GiveUpAfterIgnores = 6;
        private const float IntroFrom = 2.5f, IntroTo = 9.5f;
        private const float RaiseNotBefore = 14f;

        private sealed class Slot
        {
            public RectTransform root;
            public CanvasGroup group;
            public Image pill;
            public Text text;
            public int id = -1;
            public float alpha, target;
            public string place;
            public float y = float.NaN;
        }

        private readonly Slot[] _slots = new Slot[SlotCount];
        private readonly string[] _text = new string[N];
        private readonly int[] _acted = new int[N], _ignored = new int[N], _actedLimit = new int[N];
        private readonly float[] _visibleFor = new float[N], _ignoreAfter = new float[N];
        private readonly bool[] _suppressed = new bool[N], _want = new bool[N];
        private readonly int[] _desired = new int[SlotCount];
        private float _clock;
        private bool _introDone;

        public PromptView(RectTransform parent)
        {
            string raise = Prompt("Hold " + GameInput.Labels.RaiseCamera, "Raise camera");
            _text[(int)PromptId.Park] = Prompt(GameInput.Labels.Interact, "Park");
            _text[(int)PromptId.Mount] = Prompt(GameInput.Labels.Interact, "Ride the bike");
            _text[(int)PromptId.Macro] = Prompt(GameInput.Labels.Macro, "Macro lens");
            _text[(int)PromptId.Shutter] = Prompt(GameInput.Labels.ShutterClick, "Shutter");
            _text[(int)PromptId.Zoom] = Prompt(GameInput.Labels.Scroll, "Zoom");
            _text[(int)PromptId.Raise] = raise;
            _text[(int)PromptId.Journal] = Prompt(GameInput.Labels.Journal, "Journal");
            _text[(int)PromptId.Intro] = Prompt(GameInput.Labels.Controls, "Controls");

            for (int i = 0; i < N; i++) { _actedLimit[i] = 3; _ignoreAfter[i] = 8f; }
            _actedLimit[(int)PromptId.Journal] = 1;
            _actedLimit[(int)PromptId.Intro] = 1;
            _ignoreAfter[(int)PromptId.Macro] = 7f;
            _ignoreAfter[(int)PromptId.Raise] = 7f;

            for (int i = 0; i < SlotCount; i++)
            {
                var s = new Slot();
                s.root = UIFactory.CreateRect("Prompt " + i, parent);
                s.group = s.root.gameObject.AddComponent<CanvasGroup>();
                s.group.alpha = 0f;
                s.pill = UiStyle.CreatePill("Pill", s.root, UiStyle.PillFill);
                UIFactory.Stretch(s.pill.rectTransform);
                s.text = UIFactory.CreateText("Text", s.root, "", UiStyle.SizeBody, UiStyle.Text, TextAnchor.MiddleCenter);
                s.text.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIFactory.Stretch(s.text.rectTransform, 22f, 0f, 22f, 2f);
                UiStyle.SoftShadow(s.text);
                s.root.anchorMin = s.root.anchorMax = new Vector2(0.5f, 0f);
                s.root.pivot = new Vector2(0.5f, 0f);
                s.root.gameObject.SetActive(false);
                _slots[i] = s;
            }
        }

        /// <summary>Bold key, then the action in the same warm white.</summary>
        public static string Prompt(string key, string action) => "<b>" + key + "</b>   " + action;

        /// <summary>The player did what the prompt suggests: after three times it stops for the session.</summary>
        public void Acted(PromptId id)
        {
            int i = (int)id;
            _acted[i]++;
            _suppressed[i] = true;
        }

        /// <summary>Retire a prompt for the session (the controls card was opened, so "H Controls" has done its job).</summary>
        public void Retire(PromptId id)
        {
            _acted[(int)id] = _actedLimit[(int)id];
            if (id == PromptId.Intro) _introDone = true;
        }

        public void Update(in PromptContext c, float dt)
        {
            if (!c.hidden) _clock += dt;

            // ---- which prompts could show right now
            for (int i = 0; i < N; i++) _want[i] = false;
            if (!c.hidden)
            {
                Consider(PromptId.Park, c.riding && c.canPark, dt);
                Consider(PromptId.Mount, !c.riding && c.bikeInReach && c.cameraLowered, dt);
                Consider(PromptId.Macro, c.cameraRaised && !c.macroMode && c.macroNear, dt);
                Consider(PromptId.Shutter, c.cameraRaised && (c.hasSubject || (c.macroMode && c.macroNear)), dt);
                Consider(PromptId.Zoom, c.cameraRaised, dt);
                Consider(PromptId.Raise, !c.riding && c.cameraLowered && c.subjectAhead && _clock > RaiseNotBefore, dt);
                Consider(PromptId.Journal, !c.riding && c.cameraLowered && c.photosThisSession > 0 && c.secondsSincePhoto > 6f && c.secondsSincePhoto < 600f, dt);
                bool introWindow = !_introDone && _clock >= IntroFrom && _clock <= IntroTo && c.cameraLowered && !c.riding;
                if (_clock > IntroTo) _introDone = true;
                _want[(int)PromptId.Intro] = introWindow && _acted[(int)PromptId.Intro] < _actedLimit[(int)PromptId.Intro];
            }
            else
            {
                for (int i = 0; i < N; i++) _visibleFor[i] = 0f;
            }

            // ---- the first two, in priority order
            _desired[0] = _desired[1] = -1;
            int found = 0;
            for (int i = 0; i < N && found < SlotCount; i++)
                if (_want[i]) _desired[found++] = i;

            // ---- slots: keep what is still wanted, fade out what is not, bring in what is new
            for (int k = 0; k < SlotCount; k++)
            {
                var s = _slots[k];
                if (s.id >= 0 && s.id != _desired[0] && s.id != _desired[1]) s.target = 0f;
                else if (s.id >= 0) s.target = 1f;
                s.alpha = Mathf.MoveTowards(s.alpha, s.target, dt / UiStyle.Fade);
                if (s.id >= 0 && s.alpha <= 0f && s.target <= 0f) { s.id = -1; s.root.gameObject.SetActive(false); }
            }
            for (int d = 0; d < SlotCount; d++)
            {
                int id = _desired[d];
                if (id < 0 || SlotHolds(id)) continue;
                for (int k = 0; k < SlotCount; k++)
                {
                    var s = _slots[k];
                    if (s.id >= 0) continue;
                    Assign(s, id, c.parkPlace);
                    break;
                }
            }

            // The park prompt names the pull-off when it knows it.
            for (int k = 0; k < SlotCount; k++)
            {
                var s = _slots[k];
                if (s.id == (int)PromptId.Park && s.target > 0f && !ReferenceEquals(s.place, c.parkPlace)) Assign(s, s.id, c.parkPlace, keepAlpha: true);
            }

            // ---- place: lowest at the bottom margin; lifted clear of the viewfinder's data row while the camera is up
            float baseY = Mathf.Lerp(UiStyle.Margin, UiStyle.Margin + 62f, c.raiseBlend);
            for (int k = 0; k < SlotCount; k++)
            {
                var s = _slots[k];
                if (s.id < 0) continue;
                float intro = s.id == (int)PromptId.Intro ? 0.8f : 1f;
                s.group.alpha = s.alpha * intro;
                float y = baseY + k * (UiStyle.PillHeight + 10f) - (1f - s.alpha) * 8f;
                if (s.y != y) { s.y = y; s.root.anchoredPosition = new Vector2(0f, y); }
            }
        }

        private bool SlotHolds(int id)
        {
            for (int k = 0; k < SlotCount; k++) if (_slots[k].id == id) return true;
            return false;
        }

        private void Assign(Slot s, int id, string place, bool keepAlpha = false)
        {
            s.id = id;
            s.place = place;
            string t = _text[id];
            if (id == (int)PromptId.Park && !string.IsNullOrEmpty(place))
                t = Prompt(GameInput.Labels.Interact, "Park here") + "  <color=#" + UiStyle.Hex(UiStyle.Text) + "A6>" + place + "</color>";
            s.text.text = t;
            float w = Mathf.Max(150f, s.text.preferredWidth + 52f);
            s.root.sizeDelta = new Vector2(w, UiStyle.PillHeight);
            s.target = 1f;
            if (!keepAlpha) { s.alpha = 0f; s.group.alpha = 0f; s.y = float.NaN; }
            s.root.gameObject.SetActive(true);
        }

        /// <summary>Timers and limits: shown while it applies, quiet after a few ignored seconds, gone after three uses.</summary>
        private void Consider(PromptId id, bool applies, float dt)
        {
            int i = (int)id;
            if (!applies || _acted[i] >= _actedLimit[i] || _ignored[i] >= GiveUpAfterIgnores)
            {
                _visibleFor[i] = 0f;
                if (!applies) _suppressed[i] = false; // it can come back the next time it applies
                return;
            }
            if (_suppressed[i]) return;
            _visibleFor[i] += dt;
            if (_visibleFor[i] > _ignoreAfter[i]) { _suppressed[i] = true; _ignored[i]++; return; }
            _want[i] = true;
        }
    }
}
