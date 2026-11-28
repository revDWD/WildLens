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

        private static readonly List<MacroSubject> ActiveList = new List<MacroSubject>();
        public static IReadOnlyList<MacroSubject> Active => ActiveList;
        private Renderer[] _renderers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveList.Clear();

        private void OnEnable() { ActiveList.Add(this); _renderers = GetComponentsInChildren<Renderer>(); }
        private void OnDisable() => ActiveList.Remove(this);

        /// <summary>Whether the subject is out now (time of day and weather), per its data.</summary>
        public bool IsShowing
        {
            get
            {
                if (subject == null) return false;
                var day = DayCycle.Instance;
                var weather = WeatherCycle.Instance;
                return subject.IsShowing(day != null ? day.Period : DayPeriod.Midday, weather != null && weather.Rain > 0.25f);
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
