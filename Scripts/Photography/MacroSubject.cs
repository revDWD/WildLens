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

        private static readonly List<MacroSubject> ActiveList = new List<MacroSubject>();
        public static IReadOnlyList<MacroSubject> Active => ActiveList;
        private Renderer[] _renderers;
        private bool _visible = true;
        private float _nextPoll;

        // "Recently rained": raining now, or within the last three game hours (afterRain subjects: newts, snails).
        private const float AfterRainHours = 3f;
        private static float _lastRain = -1e9f, _nextRainCheck;
        private static bool _recentlyRained;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { ActiveList.Clear(); _lastRain = -1e9f; _nextRainCheck = 0f; _recentlyRained = false; }

        /// <summary>True while it rains and for three game hours after (cached, refreshed twice a second at most).</summary>
        public static bool RecentlyRained
        {
            get
            {
                if (Time.unscaledTime < _nextRainCheck) return _recentlyRained;
                _nextRainCheck = Time.unscaledTime + 0.5f;
                var weather = WeatherCycle.Instance;
                var day = DayCycle.Instance;
                bool raining = weather != null && weather.Rain > 0.25f;
                if (day == null) { _recentlyRained = raining; return _recentlyRained; }
                float now = day.Day * 24f + day.Hour; // the day counter handles midnight
                if (raining) _lastRain = now;
                else if (now < _lastRain) _lastRain = -1e9f; // the clock was set back: forget
                _recentlyRained = raining || now - _lastRain <= AfterRainHours;
                return _recentlyRained;
            }
        }

        /// <summary>False while hidden by <see cref="hideWhenNotShowing"/>.</summary>
        public bool Visible => _visible;

        private void Start()
        {
            if (!hideWhenNotShowing) return;
            Apply(IsShowing);
            _nextPoll = Time.time + Random.value; // per-instance phase: no frame has all the polls
        }

        private void Update()
        {
            if (!hideWhenNotShowing || Time.time < _nextPoll) return;
            _nextPoll = Time.time + 1f;
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
                return subject.IsShowing(day != null ? day.Period : DayPeriod.Midday, weather != null && weather.Rain > 0.25f, RecentlyRained);
            }
        }

        /// <summary>World bounds of the part to frame.</summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            if (focusPoint != null)
            {
                float s = focusSize > 0f ? focusSize : (subject != null ? subject.size : 0.05f);
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
