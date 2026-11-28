using System;

// PURE C# (no UnityEngine). The scheduling rules of the wildlife voices, in one place so a harness can simulate a day of them.
namespace WildLens.Sound
{
    /// <summary>The world as the voices see it (filled by the VoiceDirector from DayCycle, WeatherCycle and NaturalConditions).</summary>
    public struct VoiceConditions
    {
        public int period;            // DayPeriod: 0 night, 1 dawn, 2 morning, 3 midday, 4 golden hour, 5 dusk
        public float hour;
        public bool raining, fair, sunAfterRain, wetNight;
        public float hoursSinceRain;
        public bool accelerated;      // the clock is running fast (the night, or T held): only the beds, and now and then a call
    }

    public static class VoiceRules
    {
        /// <summary>The share of a voice's rate left while the clock runs fast (night, holding T): calls thin out, they never chipmunk.</summary>
        public const float AcceleratedAnimals = 0.2f;
        /// <summary>Animals within this distance (m) of a caller, of its own species, stay quiet for a few seconds after it.</summary>
        public const float InhibitRadius = 25f, AnswerRadius = 40f;
        public const float InhibitMin = 3f, InhibitMax = 6f, AnswerDelayMin = 1f, AnswerDelayMax = 3f;
        /// <summary>The longest a single voice may be heard: a call beyond this is never started.</summary>
        public const int MaxAnimalVoices = 6, MaxFrogVoices = 4;
        /// <summary>Fraction of the range over which a voice fades to nothing, so nothing is cut off at the limit.</summary>
        public const float EdgeFade = 0.3f;

        /// <summary>
        /// Calls per minute for one individual now (0 when nothing allows it). <paramref name="frog"/>: a bed-like voice, not thinned
        /// by fast time. Pure: the same hour and weather always give the same rate; the dice are the director's.
        /// </summary>
        public static float Rate(VoiceRecipe r, VoiceConditions c, bool frog)
        {
            if (r == null || !r.enabled || r.perMinute <= 0f) return 0f;
            if (r.fairOnly && !c.fair) return 0f;
            if (r.needsWetNight && !c.wetNight) return 0f;
            float pf = Period(r, c.period);
            // The last hour of the night is already pre-dawn: robins start in the dark.
            if (c.period == 0 && c.hour >= 4.4f && c.hour < 5f) pf = Math.Max(pf, Period(r, 1) * 0.35f);
            float rate = r.perMinute * pf;
            if (c.raining) rate *= r.rain;
            else if (c.sunAfterRain) rate *= r.afterRain;
            if (c.hoursSinceRain <= 6f) rate *= r.wet;
            if (c.accelerated && !frog) rate *= AcceleratedAnimals;
            return rate < 0f ? 0f : rate;
        }

        private static float Period(VoiceRecipe r, int p) => r.period != null && p >= 0 && p < r.period.Length ? r.period[p] : 1f;

        /// <summary>Chance of starting a call in a poll of <paramref name="dt"/> seconds at <paramref name="ratePerMinute"/> (a Poisson process: wide natural jitter).</summary>
        public static float Chance(float ratePerMinute, float dt) => ratePerMinute <= 0f ? 0f : 1f - (float)Math.Exp(-ratePerMinute * dt / 60f);

        /// <summary>Volume fade toward the limit of the range: 1 up to 70% of it, then smoothly to 0 at the range.</summary>
        public static float Edge(float distance, float range)
        {
            float start = range * (1f - EdgeFade);
            if (distance <= start) return 1f;
            if (distance >= range) return 0f;
            float t = (distance - start) / (range - start);
            return 1f - t * t * (3f - 2f * t);
        }

        /// <summary>The cricket bed level 0..1 from the game hour and the weather: dry evenings and nights, thinner just after rain, none in it.</summary>
        public static float CricketLevel(float hour, bool raining, float hoursSinceRain)
        {
            if (raining) return 0f;
            float ramp;
            if (hour >= 18.5f) ramp = Clamp01((hour - 18.5f) / 1.0f);         // rising from the start of dusk, full by 19:30
            else if (hour < 5.5f) ramp = Clamp01((5.5f - hour) / 1.0f);        // fading out through dawn
            else ramp = 0f;
            return ramp * (hoursSinceRain < 3f ? 0.35f : 1f);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
