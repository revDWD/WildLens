using UnityEngine;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.Photography
{
    /// <summary>
    /// The weather and hour rules behind small life's natural emergence (1.9), in one place: butterflies in sunshine, a second
    /// wave of them after a shower, dragonflies on warm days, fireflies on fair evenings, frogs after rain, treefrogs on wet nights,
    /// mushrooms the day after rain. Everything is derived from <see cref="DayCycle"/> and <see cref="WeatherCycle"/> only, so it is
    /// the same on every playthrough and follows the four-day forecast (fair, grey morning, afternoon shower, overcast with dusk rain).
    ///
    /// Rain is remembered: the last game hour at which it was raining. At the start of a game, and whenever the clock is replaced, that
    /// memory is seeded from the forecast (the shower on forecast day 2 ends about 16:06; the dusk rain of day 3 runs on to midnight),
    /// so the morning after a shower is damp even if the player was not there to see it fall. The seed is only for the time before the
    /// game began watching: afterwards the weather as it really was is what counts. The table below mirrors WeatherCycle's keys
    /// (a few lines of duplication on purpose, so this file depends only on WeatherCycle's public members).
    ///
    /// Cached and refreshed at most four times a second; nothing here allocates.
    /// </summary>
    public static class NaturalConditions
    {
        public const float RainThreshold = 0.25f;       // the same rain level the rest of macro mode calls "raining"
        public const float SunnyCover = 0.55f;           // below this the sun is out
        private const float CheckInterval = 0.25f;

        // Forecast days (WeatherCycle.Days): when the rain starts and ends, in hours (-1: none that day).
        private static readonly float[] RainStart = { -1f, -1f, 14.0f, 17.9f };
        private static readonly float[] RainEnd = { -1f, -1f, 16.1f, 23.99f };

        private static float _nextCheck = -1f;
        private static float _hour = 12f, _cover = 0.1f, _rain;
        private static int _day;
        private static bool _raining;
        private static float _lastRain = -1e9f;
        private static int _clockId = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _nextCheck = -1f; _hour = 12f; _cover = 0.1f; _rain = 0f; _day = 0; _raining = false; _lastRain = -1e9f; _clockId = int.MinValue;
        }

        /// <summary>Reads the clock and the weather (cached) and updates the memory of the last rain.</summary>
        public static void Refresh()
        {
            float t = Time.unscaledTime;
            if (t < _nextCheck) return;
            _nextCheck = t + CheckInterval;
            var clock = DayCycle.Instance;
            var weather = WeatherCycle.Instance;
            _cover = weather != null ? weather.Cover : 0.1f;
            _rain = weather != null ? weather.Rain : 0f;
            _raining = _rain > RainThreshold;
            if (clock == null) { _hour = 12f; _day = 0; if (_raining) _lastRain = 12f; return; }
            _hour = clock.Hour;
            _day = clock.Day;
            float now = _day * 24f + _hour;
            int id = clock.GetInstanceID();
            if (id != _clockId || now < _lastRain - 0.01f)
            {
                // A new clock (a scene reload) or the clock was set back: forget the old memory and seed from the forecast.
                _clockId = id;
                _lastRain = Seed(weather, _day, _hour);
            }
            if (_raining) _lastRain = now;
        }

        private static float Seed(WeatherCycle weather, int day, float hour)
        {
            if (weather == null || weather.overrideWeather) return -1e9f;
            int today = weather.ForecastDay;
            int yesterday = weather.pattern == WeatherCycle.Pattern.AlwaysFair ? 0 : (today + RainEnd.Length - 1) % RainEnd.Length;
            if (weather.pattern == WeatherCycle.Pattern.AlwaysFair) return -1e9f;
            float best = -1e9f;
            float now = day * 24f + hour;
            if (yesterday >= 0 && yesterday < RainEnd.Length && RainEnd[yesterday] > 0f)
            {
                float end = (day - 1) * 24f + RainEnd[yesterday];
                if (end <= now && end > best) best = end;
            }
            if (today >= 0 && today < RainEnd.Length && RainEnd[today] > 0f && hour >= RainEnd[today])
            {
                float end = day * 24f + RainEnd[today];
                if (end <= now && end > best) best = end;
            }
            return best;
        }

        // ------------------------------------------------------------------ the clock and the weather

        public static float Hour { get { Refresh(); return _hour; } }
        public static bool RainingNow { get { Refresh(); return _raining; } }
        public static float Cover { get { Refresh(); return _cover; } }

        /// <summary>Game hours since it last rained (0 while it rains; a very large number when there is no memory of rain).</summary>
        public static float HoursSinceRain
        {
            get
            {
                Refresh();
                if (_raining) return 0f;
                if (_lastRain < -1e8f) return 1e9f;
                return Mathf.Max(0f, _day * 24f + _hour - _lastRain);
            }
        }

        /// <summary>True while it rains and for <paramref name="hours"/> game hours after.</summary>
        public static bool RecentlyRained(float hours) => HoursSinceRain <= hours;

        /// <summary>It rained today or yesterday (the rest of that day and the next).</summary>
        public static bool RainedTodayOrYesterday
        {
            get
            {
                Refresh();
                if (_raining) return true;
                if (_lastRain < -1e8f) return false;
                return Mathf.FloorToInt(_lastRain / 24f) >= _day - 1;
            }
        }

        // ------------------------------------------------------------------ the named conditions

        /// <summary>Dry, not overcast, in daylight.</summary>
        public static bool Sunny { get { Refresh(); return !_raining && _cover < SunnyCover && _hour >= 6.5f && _hour < 18.5f; } }

        /// <summary>Sunshine within three game hours of a shower: butterflies and bees come out in greater numbers.</summary>
        public static bool SunAfterRain => Sunny && HoursSinceRain <= 3f && HoursSinceRain < 1e8f;

        /// <summary>The warm part of a dry day (dragonflies and damselflies).</summary>
        public static bool Warm { get { Refresh(); return !_raining && _cover < 0.8f && _hour >= 9.5f && _hour < 18.5f; } }

        /// <summary>
        /// A dry, clear evening with no rain that afternoon (within eight hours), from 18:45; <paramref name="stagger"/> (0 to 1) makes
        /// each firefly rise a little later, over about 20 game minutes, and go a little later too.
        /// </summary>
        public static bool FairEvening(float stagger)
        {
            Refresh();
            if (_raining || _cover >= 0.5f || HoursSinceRain <= 8f) return false;
            float start = 18.75f + Mathf.Clamp01(stagger) * 0.34f, end = 22f + Mathf.Clamp01(stagger) * 0.6f;
            return _hour >= start && _hour < end;
        }

        /// <summary>Evening, night or dawn within fourteen game hours of rain (gray treefrogs).</summary>
        public static bool WetNight { get { Refresh(); return (_hour >= 18.5f || _hour < 6.5f) && HoursSinceRain <= 14f; } }

        public static bool Check(SubjectCondition condition, float stagger)
        {
            switch (condition)
            {
                case SubjectCondition.Sunshine: return Sunny;
                case SubjectCondition.Warm: return Warm;
                case SubjectCondition.FairEvening: return FairEvening(stagger);
                case SubjectCondition.WetNight: return WetNight;
                default: return true;
            }
        }

        /// <summary>Whether a second tier of instances is out now.</summary>
        public static bool Tier(SubjectExtra extra)
        {
            switch (extra)
            {
                case SubjectExtra.SunAfterRain: return SunAfterRain;
                case SubjectExtra.AfterRain: return RecentlyRained(6f);
                case SubjectExtra.AfterRainOrDusk: return RecentlyRained(6f) || (Hour >= 18.5f || _hour < 7f);
                case SubjectExtra.RainedLately: return RainedTodayOrYesterday;
                default: return true;
            }
        }
    }
}
