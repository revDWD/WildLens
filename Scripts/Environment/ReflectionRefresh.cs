using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>
    /// Keeps a realtime reflection probe in step with the sky (1.5). The pond's probe used to render once on load, so
    /// under an overcast or golden sky the water still mirrored the blue morning it started with. Re-renders when the
    /// light period or the weather changes, and every <see cref="interval"/> seconds, time-sliced across frames.
    /// </summary>
    [RequireComponent(typeof(ReflectionProbe))]
    public class ReflectionRefresh : MonoBehaviour
    {
        public float interval = 20f;

        private ReflectionProbe _probe;
        private float _timer;
        private DayPeriod _period = (DayPeriod)(-1);
        private WeatherCondition _weather = (WeatherCondition)(-1);

        private void Start()
        {
            _probe = GetComponent<ReflectionProbe>();
            _probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            _probe.RenderProbe();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            var day = DayCycle.Instance;
            var weather = WeatherCycle.Instance;
            bool changed = (day != null && day.Period != _period) || (weather != null && weather.Condition != _weather);
            if (!changed && _timer < interval) return;
            if (day != null) _period = day.Period;
            if (weather != null) _weather = weather.Condition;
            _timer = 0f;
            _probe.RenderProbe();
        }
    }
}
