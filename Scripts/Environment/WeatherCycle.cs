using System;
using UnityEngine;

namespace WildLens.Environment
{
    public enum WeatherCondition { Fair, Cloudy, Overcast, LightRain } // append only

    /// <summary>
    /// Weather, the reserve's second rhythm (1.4). Like the day's light, it follows a pattern players can learn rather
    /// than dice: a four-day forecast that repeats.
    ///   Day 1  Fair: a few clouds build in the afternoon.
    ///   Day 2  Grey morning (soft, even light) clearing by midday.
    ///   Day 3  Fair morning; clouds build; a light shower mid-afternoon; clearing for golden hour.
    ///   Day 4  Overcast all day; light rain at dusk.
    /// It publishes <see cref="Cover"/> (0 clear .. 1 overcast) and <see cref="Rain"/> (0..1). <see cref="DayCycle"/> reads
    /// them every frame (dimmer, flatter sun, greyer and thicker haze, an overcast sky image when it's truly grey), the
    /// wind blows harder, rain falls around the camera with its own sound, <see cref="WildLens.Wildlife.WildlifeSchedule"/>
    /// sends meadow animals into woodland while it rains, and the HUD and trailhead board say what the weather is doing.
    /// Weather never spawns or removes animals.
    /// </summary>
    [DefaultExecutionOrder(-60)] // before DayCycle (-50), which reads Cover and Rain
    public class WeatherCycle : MonoBehaviour
    {
        public enum Pattern { Changing, AlwaysFair }

        [Tooltip("Changing: the four-day forecast. Always fair: the 1.3 reserve.")]
        public Pattern pattern = Pattern.Changing;
        [Tooltip("Which forecast day the first day is (0 fair, 1 grey morning, 2 afternoon shower, 3 overcast with dusk rain).")]
        [Range(0, 3)] public int firstDay;
        [Tooltip("Test a look in Play mode: tick, then set cover and rain by hand.")]
        public bool overrideWeather;
        [Range(0f, 1f)] public float overrideCover = 0.9f;
        [Range(0f, 1f)] public float overrideRain = 0.6f;

        [Header("Set by the builder")]
        public Material overcastSky;
        public Material rainMaterial;

        public static WeatherCycle Instance { get; private set; }
        public float Cover { get; private set; }
        public float Rain { get; private set; }
        public WeatherCondition Condition { get; private set; }
        /// <summary>Overcast enough to show the grey sky image (with hysteresis so it doesn't flicker).</summary>
        public bool OvercastSky { get; private set; }
        public int ForecastDay { get; private set; }
        public event Action<WeatherCondition> Changed;

        // ------------------------------------------------------------------ shared conditions (1.9)
        // Read-only helpers derived from Cover, Rain and the clock, so wildlife habits (and anything else) agree on what "after rain",
        // "fair" and "a fair evening" mean. They change no weather.

        /// <summary>Rain above this counts as raining (what WildlifeSchedule and the macro subjects have always used).</summary>
        public const float RainThreshold = 0.25f;
        private const float Never = -1e9f;
        private float _lastRainTime = Never; // day * 24 + hour when it last rained; per instance, so a scene reload forgets it

        public bool IsRaining => Rain > RainThreshold;

        /// <summary>Game hours since it last rained (0 while raining; very large if it has not rained since the clock started or was set back).</summary>
        public float HoursSinceRain
        {
            get
            {
                if (IsRaining) return 0f;
                var clock = DayCycle.Instance;
                if (clock == null) return 1e9f;
                float now = clock.Day * 24f + clock.Hour;
                return now < _lastRainTime ? 1e9f : now - _lastRainTime;
            }
        }

        /// <summary>True while it rains and for <paramref name="hours"/> game hours after (the same idea as the macro subjects' tracker).</summary>
        public bool RecentlyRained(float hours) => IsRaining || HoursSinceRain <= hours;

        /// <summary>It stopped raining within <paramref name="hours"/> game hours (false while it is still raining).</summary>
        public bool RainEndedWithin(float hours) => !IsRaining && HoursSinceRain <= hours;

        /// <summary>
        /// The ANIMALS' window for "the first sunshine after rain": two game hours. Small life keeps its own, longer window
        /// (<c>NaturalConditions.SmallLifeSunAfterRainHours</c>, three hours, for the second wave of butterflies). The two are different
        /// on purpose and both read this one memory of the rain; do not unify them.
        /// </summary>
        public const float AnimalSunAfterRainHours = 2f;

        /// <summary>The first sunshine after rain: not raining, it rained within about two hours, and the cloud has broken.</summary>
        public bool SunAfterRain => RainEndedWithin(AnimalSunAfterRainHours) && !OvercastSky && Cover < 0.6f;

        /// <summary>
        /// day * 24 + hour when it last rained (the forecast seeds it before the game's first frame; see <see cref="SeedRainMemory"/>);
        /// a very large negative number when there is no memory of rain. The one memory of the rain: NaturalConditions reads it too.
        /// </summary>
        public float LastRainTime => _lastRainTime;

        /// <summary>Whether there is any memory of rain at all.</summary>
        public bool HasRainMemory => _lastRainTime > Never * 0.5f;

        // The hour each forecast day's rain stops (-1: no rain that day), worked out once from <see cref="Days"/> itself by scanning in
        // hundredths of an hour: day 3 (index 2) 16.10, day 4 (index 3) 23.99 (the dusk rain runs on to midnight).
        private static float[] _rainEnds;

        private static float[] RainEnds()
        {
            if (_rainEnds != null) return _rainEnds;
            var ends = new float[Days.Length];
            for (int d = 0; d < Days.Length; d++)
            {
                ends[d] = -1f;
                for (int i = 0; i < 2400; i++)
                {
                    float h = i * 0.01f;
                    if (At(Days[d], h).y > RainThreshold) ends[d] = h;
                }
            }
            _rainEnds = ends;
            return ends;
        }

        /// <summary>
        /// The memory of rain from before the game began watching, from this cycle's own forecast: the last time it rained according to the
        /// day before and the hours already passed today (the dusk rain of forecast day 4 runs to midnight, so the morning after is damp;
        /// the shower of day 3 ended about 16:06). None for the always-fair pattern or a hand-set weather. Called at the start and
        /// whenever the clock is set back; it changes no weather, only what <see cref="HoursSinceRain"/> reports.
        /// </summary>
        private float SeedRainMemory(int day, float hour)
        {
            if (pattern == Pattern.AlwaysFair || overrideWeather) return Never;
            var ends = RainEnds();
            int today = (firstDay + day) % Days.Length;
            int yesterday = (today + Days.Length - 1) % Days.Length;
            float best = Never;
            float now = day * 24f + hour;
            if (ends[yesterday] > 0f)
            {
                float end = (day - 1) * 24f + ends[yesterday];
                if (end <= now && end > best) best = end;
            }
            if (ends[today] > 0f && hour >= ends[today])
            {
                float end = day * 24f + ends[today];
                if (end <= now && end > best) best = end;
            }
            return best;
        }

        /// <summary>A fair spell: not raining, not overcast, and at least an hour since the last rain (warm air rising again, wet leaves drying).</summary>
        public bool IsFair => !IsRaining && !OvercastSky && HoursSinceRain > 1f;

        /// <summary>Golden hour or dusk, and fair.</summary>
        public bool IsFairEvening
        {
            get
            {
                var clock = DayCycle.Instance;
                return clock != null && (clock.Period == DayPeriod.GoldenHour || clock.Period == DayPeriod.Dusk) && IsFair;
            }
        }

        // (hour, cloud cover, rain) keys for each forecast day.
        private static readonly Vector3[][] Days =
        {
            new[] { new Vector3(0f, 0.1f, 0f), new Vector3(12f, 0.15f, 0f), new Vector3(16f, 0.35f, 0f), new Vector3(20f, 0.25f, 0f), new Vector3(24f, 0.15f, 0f) },
            new[] { new Vector3(0f, 0.8f, 0f), new Vector3(9.5f, 0.85f, 0f), new Vector3(12f, 0.45f, 0f), new Vector3(15f, 0.2f, 0f), new Vector3(24f, 0.2f, 0f) },
            new[] { new Vector3(0f, 0.15f, 0f), new Vector3(11f, 0.3f, 0f), new Vector3(13.3f, 0.85f, 0f), new Vector3(14.2f, 0.92f, 0.75f),
                    new Vector3(15.6f, 0.85f, 0.5f), new Vector3(16.6f, 0.4f, 0f), new Vector3(24f, 0.25f, 0f) },
            new[] { new Vector3(0f, 0.72f, 0f), new Vector3(10f, 0.82f, 0f), new Vector3(17f, 0.9f, 0.1f), new Vector3(18.8f, 0.95f, 0.65f), new Vector3(24f, 0.85f, 0.25f) },
        };

        private ParticleSystem _rain;
        private AudioSource _rainAudio;
        private AudioLowPassFilter _rainMuffle;          // added the first time the listener is under a roof (1.10)
        private ParticleSystem.Particle[] _rainBuffer;   // preallocated for the shelter pass: no garbage per frame
        private Transform _camera;
        private bool _started;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            BuildRain();
            var clock = DayCycle.Instance;
            _lastRainTime = SeedRainMemory(clock != null ? clock.Day : 0, clock != null ? clock.Hour : 12f);
            Sample(true);
            _started = true;
        }

        private void Update() => Sample(false);

        private void Sample(bool force)
        {
            var clock = DayCycle.Instance;
            float hour = clock != null ? clock.Hour : 12f;
            int day = clock != null ? clock.Day : 0;
            ForecastDay = pattern == Pattern.AlwaysFair ? 0 : (firstDay + day) % Days.Length;
            Vector2 w = overrideWeather ? new Vector2(overrideCover, overrideRain) : At(Days[ForecastDay], hour);
            // Ease towards the target so jumps (midnight, overrides) blend over a few seconds.
            float k = force ? 1f : 1f - Mathf.Exp(-Time.deltaTime * 0.8f);
            Cover = Mathf.Lerp(Cover, w.x, k);
            Rain = Mathf.Lerp(Rain, w.y, k);
            if (OvercastSky ? Cover < 0.55f : Cover > 0.66f) OvercastSky = !OvercastSky;

            // 1.9: remember when it last rained (the clock set back, or a scene reload, forgets it).
            float nowHours = day * 24f + hour;
            if (nowHours < _lastRainTime) _lastRainTime = SeedRainMemory(day, hour);
            if (Rain > RainThreshold) _lastRainTime = nowHours;

            var c = Rain > 0.2f ? WeatherCondition.LightRain : Cover > 0.68f ? WeatherCondition.Overcast : Cover > 0.4f ? WeatherCondition.Cloudy : WeatherCondition.Fair;
            if (c != Condition || force)
            {
                bool changed = c != Condition;
                Condition = c;
                if (changed && _started) Changed?.Invoke(c);
            }

            if (WildLensWind.Instance != null) WildLensWind.Instance.WeatherMultiplier = 1f + Cover * 0.35f + Rain * 0.6f;
            UpdateRain();
        }

        private static Vector2 At(Vector3[] keys, float hour)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                if (hour > keys[i].x) continue;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(keys[i - 1].x, keys[i].x, hour));
                return new Vector2(Mathf.Lerp(keys[i - 1].y, keys[i].y, t), Mathf.Lerp(keys[i - 1].z, keys[i].z, t));
            }
            var last = keys[keys.Length - 1];
            return new Vector2(last.y, last.z);
        }

        /// <summary>The trailhead board's conditions line.</summary>
        public string ConditionText(DayPeriod period)
        {
            switch (Condition)
            {
                case WeatherCondition.LightRain: return "Light rain";
                case WeatherCondition.Overcast: return "Overcast";
                case WeatherCondition.Cloudy: return "Clouds building";
                default: return period == DayPeriod.Dawn ? "Misty" : period == DayPeriod.Night ? "Clear night" : "Fair";
            }
        }

        // ------------------------------------------------------------------ rain

        private void BuildRain()
        {
            if (rainMaterial == null) return;
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            _rain = go.AddComponent<ParticleSystem>();
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _rain.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = 0.018f;
            main.startColor = new Color(0.8f, 0.85f, 0.9f, 0.55f);
            main.maxParticles = 5000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var vel = _rain.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.8f, 1.6f); // a little wind slant
            vel.y = new ParticleSystem.MinMaxCurve(-11f, -9f);
            vel.z = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 0.5f, 36f);
            var emission = _rain.emission;
            emission.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 1f;
            r.velocityScale = 0.045f;
            r.sharedMaterial = rainMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _rain.Play();

            var audioGo = new GameObject("Rain Sound");
            audioGo.transform.SetParent(transform, false);
            _rainAudio = audioGo.AddComponent<AudioSource>();
            _rainAudio.clip = RainClip();
            _rainAudio.loop = true;
            _rainAudio.spatialBlend = 0f;
            _rainAudio.volume = 0f;
            _rainAudio.Play();
        }

        private void UpdateRain()
        {
            if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
            // 1.10 roofs: the listener's cover is kept up to date even when there is no rain system (cheap: five samples a second,
            // nothing at all when there are no shelters).
            if (_camera != null) RainShelter.Tick(_camera.position, Time.deltaTime);
            if (_rain == null) return;
            if (_camera != null) _rain.transform.position = _camera.position + Vector3.up * 11f;
            var emission = _rain.emission;
            emission.rateOverTime = Rain * 3800f;

            // Drops that fall into a roofed volume are removed (only while it rains and a shelter is within the rain's reach).
            if (_camera != null && Rain > 0.02f && RainShelter.Count > 0)
            {
                if (_rainBuffer == null) _rainBuffer = new ParticleSystem.Particle[_rain.main.maxParticles];
                RainShelter.KillRain(_rain, _camera.position, 22f, _rainBuffer); // the emitter is 36 m across: 18 m to its edge, a little beyond
            }
            float cover = RainShelter.ListenerCover;
            if (_rainAudio != null)
            {
                _rainAudio.volume = Mathf.Clamp01(Rain) * 0.45f * (1f - 0.3f * cover); // under a roof: quieter ...
                if (cover > 0.01f && _rainMuffle == null) _rainMuffle = _rainAudio.gameObject.AddComponent<AudioLowPassFilter>();
                if (_rainMuffle != null)
                {
                    // ... and duller: the cutoff falls from open (22 kHz) to about 1.6 kHz (drumming on a roof, not hissing on leaves).
                    bool on = cover > 0.01f;
                    if (_rainMuffle.enabled != on) _rainMuffle.enabled = on;
                    if (on) _rainMuffle.cutoffFrequency = 22000f * Mathf.Pow(1600f / 22000f, cover);
                }
            }
        }

        /// <summary>Rain on leaves: filtered noise with a scatter of soft ticks (synthesised, loops over 6 s).</summary>
        private static AudioClip RainClip()
        {
            const int rate = 22050;
            int n = rate * 6;
            var d = new float[n];
            var rng = new System.Random(31);
            float lp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * 0.35f;
                float hp = lp - prev; prev = lp;
                d[i] = (hp * 0.55f + lp * 0.12f) * 0.6f;
            }
            for (int b = 0; b < 2400; b++)
            {
                int start = rng.Next(n - 200);
                float amp = 0.05f + (float)rng.NextDouble() * 0.12f;
                for (int k = 0; k < 120; k++) d[start + k] += ((float)rng.NextDouble() * 2f - 1f) * amp * Mathf.Exp(-k / 18f);
            }
            var clip = AudioClip.Create("Rain", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
