using System.Collections.Generic;
using UnityEngine;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.Photography
{
    /// <summary>
    /// A small subject placed in the reserve (1.5 groundwork): a wildflower today; an insect, a frog or a fungus later.
    /// Macro mode looks for these instead of animals. Optional <see cref="focusPoint"/> marks the part worth focusing on
    /// (a flower head rather than the whole plant); otherwise the renderers' bounds are used.
    /// </summary>
    public class MacroSubject : MonoBehaviour
    {
        public NaturalistSubject subject;
        [Tooltip("The part to focus on and frame (a flower head). Empty: the whole object.")]
        public Transform focusPoint;
        [Tooltip("Size of the focus part in metres when a focus point is set (else the subject's own size).")]
        public float focusSize;
        [Tooltip("Hide the renderers while the subject is not showing (wrong hour, rain). Set for animals from packs; plants never vanish.")]
        public bool hideWhenNotShowing;
        [Tooltip("1.9: a second-tier instance (more butterflies in the sun after rain, more frogs after rain and at dusk, more mushrooms the day after rain): " +
                 "out only while that condition holds. None: always subject to the subject's own rules only.")]
        public SubjectExtra tier = SubjectExtra.None;
        [Tooltip("1.9: 0 to 1, spreads a group's emergence over time (fireflies rise over about twenty game minutes).")]
        [Range(0f, 1f)] public float stagger;

        private static readonly List<MacroSubject> ActiveList = new List<MacroSubject>();
        public static IReadOnlyList<MacroSubject> Active => ActiveList;
        private Renderer[] _renderers;
        private bool _visible = true;
        private float _nextPoll;

        // "Recently rained": raining now, or within the last three game hours (afterRain subjects: newts, snails). Since 1.9 the memory of
        // the rain lives in WeatherCycle (which seeds it from its own forecast), read here through NaturalConditions like every other
        // small-life rule. This three-hour window is small life's own (animals use two for the first sun after rain): leave it.
        private const float AfterRainHours = 3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { ActiveList.Clear(); }

        /// <summary>True while it rains and for three game hours after.</summary>
        public static bool RecentlyRained => NaturalConditions.RecentlyRained(AfterRainHours);

        /// <summary>False while hidden by <see cref="hideWhenNotShowing"/>.</summary>
        public bool Visible => _visible;

        private void Start()
        {
            _nextPoll = Time.time + Random.value; // per-instance phase: no frame has all the polls
            if (!hideWhenNotShowing) return;
            Apply(IsShowing);
        }

        // One cheap poll a second per instance. A hidden instance has its renderers off and does nothing else (its MacroMotion
        // returns at once), so the second tier of a subject costs nothing while it is out of season. Plants only keep the shared
        // memory of the rain up to date.
        private void Update()
        {
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + 1f;
            if (!hideWhenNotShowing) { NaturalConditions.Refresh(); return; }
            bool show = IsShowing;
            if (show != _visible) Apply(show);
        }

        private void Apply(bool show)
        {
            _visible = show;
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = show;
        }

        private void OnEnable() { ActiveList.Add(this); _renderers = GetComponentsInChildren<Renderer>(); if (!_visible) Apply(false); }
        private void OnDisable() => ActiveList.Remove(this);

        /// <summary>Whether the subject is out now (time of day and weather), per its data.</summary>
        public bool IsShowing
        {
            get
            {
                if (subject == null) return false;
                var day = DayCycle.Instance;
                var weather = WeatherCycle.Instance;
                if (!subject.IsShowing(day != null ? day.Period : DayPeriod.Midday, weather != null && weather.Rain > NaturalConditions.RainThreshold, RecentlyRained)) return false;
                if (subject.condition != SubjectCondition.None && !NaturalConditions.Check(subject.condition, stagger)) return false;
                return tier == SubjectExtra.None || NaturalConditions.Tier(tier);
            }
        }

        /// <summary>World bounds of the part to frame.</summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            if (focusPoint != null)
            {
                // A subject that names the part to grade (a fern's crown, a cattail's head) overrides the builder's whole-plant box.
                float s = subject != null && subject.focusPartSize > 0f ? subject.focusPartSize
                        : focusSize > 0f ? focusSize : (subject != null ? subject.size : 0.05f);
                bounds = new Bounds(focusPoint.position, Vector3.one * s);
                return true;
            }
            bounds = default;
            if (_renderers == null || _renderers.Length == 0) return false;
            bool any = false;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
