using System;
using UnityEngine;
using WildLens.Core;
#if WILDLENS_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace WildLens.Environment
{
    public enum DayPeriod { Night, Dawn, Morning, Midday, GoldenHour, Dusk }

    /// <summary>
    /// The reserve's day (0.9). Time runs from dawn to dusk at <see cref="minutesPerHour"/> real minutes per in-game
    /// hour (default 1: a day of daylight in about sixteen minutes); night passes as a short time-lapse. Holding T while
    /// standing still lets time pass quickly ("wait for the light").
    ///
    /// Periods: Dawn 05:00–07:00 · Morning 07:00–11:00 · Midday 11:00–15:30 · Golden hour 15:30–18:30 ·
    /// Dusk 18:30–20:30 · Night.
    ///
    /// Each frame it sets the sun's path (east → south → west, low at the ends of the day), colour and strength; swaps
    /// between the morning and golden-hour sky images and rotates them so the sun in the sky lines up with the light;
    /// sets sky exposure and tint, ambient strength, fog colour and density (thick at dawn), and the URP white balance
    /// and exposure. Other systems read <see cref="Hour"/> and <see cref="Period"/> and listen to <see cref="PeriodChanged"/>.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class DayCycle : MonoBehaviour
    {
        [Header("Clock")]
        [Range(0f, 24f)] public float startHour = 7.5f;
        [Tooltip("Real minutes per in-game hour.")]
        [Min(0.05f)] public float minutesPerHour = 1f;
        [Tooltip("How much faster time runs while the player holds T (standing still).")]
        public float waitSpeed = 30f;
        [Tooltip("How much faster night passes.")]
        public float nightSpeed = 45f;

        [Header("Scene (set by the builder)")]
        public Light sun;
        public Material morningSky, goldenSky;
        [Tooltip("Azimuth (degrees, atan2(x, z)) of the sun in each sky image as shipped.")]
        public float morningSkySunAzimuth, goldenSkySunAzimuth;
        public Color morningFog = new Color(0.72f, 0.78f, 0.84f), goldenFog = new Color(0.86f, 0.74f, 0.62f);
        public float baseSunIntensity = 1.45f;

        public static DayCycle Instance { get; private set; }
        public float Hour { get; private set; }
        /// <summary>Days since Play began (1.4: the weather's forecast day).</summary>
        public int Day { get; private set; }
        public DayPeriod Period { get; private set; }
        public bool IsWaiting { get; private set; }
        public event Action<DayPeriod> PeriodChanged;

        private Material _morning, _golden, _overcast, _current;
        private float _envTimer;
        private bool _started;
#if WILDLENS_URP
        private WhiteBalance _wb;
        private ColorAdjustments _colour;
        private float _baseTemperature, _baseExposure;
#endif

        public static string PeriodName(DayPeriod p)
        {
            switch (p)
            {
                case DayPeriod.Dawn: return "Dawn";
                case DayPeriod.Morning: return "Morning";
                case DayPeriod.Midday: return "Midday";
                case DayPeriod.GoldenHour: return "Golden hour";
                case DayPeriod.Dusk: return "Dusk";
                default: return "Night";
            }
        }

        public static DayPeriod PeriodAt(float hour)
        {
            if (hour >= 5f && hour < 7f) return DayPeriod.Dawn;
            if (hour >= 7f && hour < 11f) return DayPeriod.Morning;
            if (hour >= 11f && hour < 15.5f) return DayPeriod.Midday;
            if (hour >= 15.5f && hour < 18.5f) return DayPeriod.GoldenHour;
            if (hour >= 18.5f && hour < 20.5f) return DayPeriod.Dusk;
            return DayPeriod.Night;
        }

        public string ClockText => $"{Mathf.FloorToInt(Hour):00}:{Mathf.FloorToInt(Hour % 1f * 60f):00}";

        private void Awake()
        {
            Instance = this;
            Hour = Mathf.Repeat(startHour, 24f);
            Period = PeriodAt(Hour);
            // Work on copies: never modify the sky assets during Play.
            if (morningSky != null) _morning = new Material(morningSky);
            if (goldenSky != null) _golden = new Material(goldenSky);
            if (sun == null) sun = RenderSettings.sun;
        }

        private void Start()
        {
#if WILDLENS_URP
            foreach (var v in SceneQuery.FindAll<Volume>())
            {
                if (!v.isGlobal || v.sharedProfile == null) continue;
                var profile = v.profile; // instance copy: Play mode never edits the asset
                if (profile.TryGet(out _wb)) _baseTemperature = _wb.temperature.value;
                if (profile.TryGet(out _colour)) _baseExposure = _colour.postExposure.value;
                break;
            }
#endif
            Apply(true);
            _started = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_morning != null) Destroy(_morning);
            if (_golden != null) Destroy(_golden);
            if (_overcast != null) Destroy(_overcast);
        }

        private void Update()
        {
            float speed = 1f;
            IsWaiting = GameInput.WaitHeld && PlayerPresence.Exists && PlayerPresence.IsStill && PlayerPresence.EngineNoise <= 0f;
            if (IsWaiting) speed = waitSpeed;
            if (Period == DayPeriod.Night) speed = nightSpeed;
            float next = Hour + Time.deltaTime / (minutesPerHour * 60f) * speed;
            if (next >= 24f) Day++;
            Hour = Mathf.Repeat(next, 24f);
            Apply(false);
        }

        /// <summary>Jump the clock (debug, or a future "start at dawn" option).</summary>
        public void SetHour(float hour)
        {
            Hour = Mathf.Repeat(hour, 24f);
            Apply(true);
        }

        // ------------------------------------------------------------------ lighting

        private struct Key
        {
            public float hour, sun, ambient, skyExposure, fogDensity, temperature, exposure;
            public Color sunColour, skyTint, fogTint;
            public Key(float h, float s, Color sc, float amb, float skyExp, Color tint, float fog, Color fogTint, float temp, float exp)
            { hour = h; sun = s; sunColour = sc; ambient = amb; skyExposure = skyExp; skyTint = tint; fogDensity = fog; this.fogTint = fogTint; temperature = temp; exposure = exp; }
        }

        // Hand-tuned keys through the day: what each period should *feel* like.
        private static readonly Key[] Keys =
        {
            new Key(0f,    0f,   new Color(0.5f, 0.55f, 0.8f),  0.12f, 0.06f, new Color(0.35f, 0.38f, 0.55f), 0.004f,  new Color(0.12f, 0.14f, 0.2f), -10f, -0.6f),
            new Key(4.6f,  0f,   new Color(0.6f, 0.55f, 0.75f), 0.18f, 0.12f, new Color(0.42f, 0.4f, 0.55f),  0.0065f, new Color(0.35f, 0.36f, 0.45f), -8f, -0.5f),
            new Key(5.6f,  0.25f, new Color(1f, 0.58f, 0.42f),  0.45f, 0.45f, new Color(0.58f, 0.46f, 0.5f),  0.0105f, new Color(0.86f, 0.74f, 0.74f), 6f, -0.25f),  // dawn mist
            new Key(6.6f,  0.7f, new Color(1f, 0.74f, 0.55f),   0.75f, 0.75f, new Color(0.55f, 0.5f, 0.5f),   0.0065f, new Color(0.9f, 0.84f, 0.8f), 4f, -0.1f),
            new Key(8.5f,  1f,   new Color(1f, 0.93f, 0.83f),   1f,    1f,    new Color(0.5f, 0.5f, 0.5f),    0.0028f, Color.white, 0f, 0f),
            new Key(13f,   1.05f, new Color(1f, 0.97f, 0.93f),  1.05f, 1.05f, new Color(0.5f, 0.5f, 0.5f),    0.0022f, Color.white, -2f, 0.05f),
            new Key(16.5f, 0.85f, new Color(1f, 0.82f, 0.58f),  1f,    1.05f, new Color(0.52f, 0.5f, 0.47f),  0.003f,  Color.white, 6f, 0f),     // golden hour
            new Key(18.3f, 0.5f, new Color(1f, 0.6f, 0.36f),    0.75f, 0.85f, new Color(0.56f, 0.47f, 0.44f), 0.0036f, new Color(1f, 0.88f, 0.8f), 12f, 0f),
            new Key(19.5f, 0.12f, new Color(0.95f, 0.45f, 0.38f), 0.42f, 0.38f, new Color(0.5f, 0.42f, 0.55f), 0.0045f, new Color(0.62f, 0.52f, 0.62f), 4f, -0.2f), // dusk
            new Key(20.6f, 0f,   new Color(0.5f, 0.5f, 0.75f),  0.16f, 0.1f,  new Color(0.36f, 0.38f, 0.55f), 0.0042f, new Color(0.18f, 0.2f, 0.28f), -8f, -0.5f),
            new Key(24f,   0f,   new Color(0.5f, 0.55f, 0.8f),  0.12f, 0.06f, new Color(0.35f, 0.38f, 0.55f), 0.004f,  new Color(0.12f, 0.14f, 0.2f), -10f, -0.6f),
        };

        private static Key Sample(float hour)
        {
            for (int i = 1; i < Keys.Length; i++)
            {
                if (hour > Keys[i].hour) continue;
                Key a = Keys[i - 1], b = Keys[i];
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.hour, b.hour, hour));
                return new Key(hour, Mathf.Lerp(a.sun, b.sun, t), Color.Lerp(a.sunColour, b.sunColour, t), Mathf.Lerp(a.ambient, b.ambient, t),
                    Mathf.Lerp(a.skyExposure, b.skyExposure, t), Color.Lerp(a.skyTint, b.skyTint, t), Mathf.Lerp(a.fogDensity, b.fogDensity, t),
                    Color.Lerp(a.fogTint, b.fogTint, t), Mathf.Lerp(a.temperature, b.temperature, t), Mathf.Lerp(a.exposure, b.exposure, t));
            }
            return Keys[Keys.Length - 1];
        }

        /// <summary>Compass direction (degrees, atan2(x, z)) and elevation of the sun: rises east, sets west.</summary>
        public static void SunPosition(float hour, out float azimuth, out float elevation)
        {
            float t = Mathf.InverseLerp(5.4f, 19.8f, hour);
            azimuth = Mathf.Lerp(95f, 265f, t); // east (+X = 90°) through south (−Z = 180°) to west (−X = 270°)
            elevation = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 52f - 3f;
        }

        private void Apply(bool force)
        {
            var newPeriod = PeriodAt(Hour);
            if (newPeriod != Period)
            {
                Period = newPeriod;
                PeriodChanged?.Invoke(Period);
            }
            else if (force && _started) PeriodChanged?.Invoke(Period);

            var k = Sample(Hour);
            // 1.4: weather on top of the light. Cloud dims and softens the sun, flattens and cools the scene, and thickens
            // and greys the haze; rain thickens it more.
            var weather = WeatherCycle.Instance;
            float cover = weather != null ? weather.Cover : 0f, rain = weather != null ? weather.Rain : 0f;
            SunPosition(Hour, out float az, out float el);
            Vector3 toSun = Quaternion.Euler(-Mathf.Max(el, -6f), az, 0f) * Vector3.forward;
            if (sun != null)
            {
                // Below the horizon, keep the light just under it and fade it out; a moonlit fill keeps night readable.
                sun.transform.rotation = Quaternion.LookRotation(-toSun);
                sun.intensity = baseSunIntensity * k.sun * Mathf.Lerp(1f, 0.3f, cover);
                sun.color = Color.Lerp(k.sunColour, new Color(0.85f, 0.88f, 0.92f), cover * 0.6f);
                sun.shadowStrength = Mathf.Lerp(0.5f, 0.85f, Mathf.Clamp01(k.sun)) * Mathf.Lerp(1f, 0.3f, cover);
                sun.enabled = k.sun > 0.01f;
            }

            bool golden = Hour >= 15.5f && Hour < 21f;
            var sky = golden ? (_golden != null ? _golden : _morning) : (_morning != null ? _morning : _golden);
            if (weather != null && weather.OvercastSky && weather.overcastSky != null)
            {
                if (_overcast == null) _overcast = new Material(weather.overcastSky); // a copy: assets stay untouched in Play
                sky = _overcast;
            }
            if (sky != null)
            {
                if (_current != sky || force)
                {
                    RenderSettings.skybox = sky;
                    _current = sky;
                    _envTimer = 0f;
                }
                // Turn the sky image so its sun sits where the light is.
                float skyAz = sky == _golden ? goldenSkySunAzimuth : morningSkySunAzimuth;
                float targetAz = Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg;
                if (sky != _overcast && sky.HasProperty("_Rotation")) sky.SetFloat("_Rotation", Mathf.Repeat(skyAz - targetAz, 360f));
                float exposure = sky == _overcast ? k.skyExposure * 0.95f : k.skyExposure * (sky == _golden ? 1.05f : 1f) * Mathf.Lerp(1f, 0.78f, cover);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", exposure);
                if (sky.HasProperty("_Tint")) sky.SetColor("_Tint", Color.Lerp(k.skyTint, new Color(0.47f, 0.48f, 0.5f), cover * 0.6f));
            }

            RenderSettings.ambientIntensity = k.ambient * Mathf.Lerp(1f, 0.9f, cover);
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.25f, 0.9f, Mathf.Clamp01(k.ambient));
            Color fogBase = Color.Lerp(morningFog, goldenFog, golden ? Mathf.InverseLerp(15.5f, 18f, Hour) : 0f);
            Color greyHaze = new Color(0.66f, 0.68f, 0.7f) * Mathf.Clamp01(k.ambient + 0.1f);
            RenderSettings.fogColor = Color.Lerp(fogBase * k.fogTint, greyHaze, cover * 0.65f);
            RenderSettings.fogDensity = k.fogDensity * (1f + cover * 1.2f + rain * 1.6f);

#if WILDLENS_URP
            if (_wb != null) _wb.temperature.value = _baseTemperature + k.temperature - cover * 6f;
            if (_colour != null) _colour.postExposure.value = _baseExposure + k.exposure + cover * 0.12f;
#endif
            // Re-derive ambient light from the (re-tinted, rotated) sky every few seconds; cheap at this rate.
            _envTimer -= Time.deltaTime;
            if (_envTimer <= 0f || force)
            {
                _envTimer = 2.5f;
                DynamicGI.UpdateEnvironment();
            }
        }
    }
}
