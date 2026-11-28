using WildLens.Data;

namespace WildLens.Wildlife
{
    /// <summary>The weather as the habits see it (filled from WeatherCycle and DayCycle; plain data so the rules run anywhere).</summary>
    public struct HabitWeather
    {
        public bool raining;
        /// <summary>Not raining, not overcast, and at least an hour since the last rain (WeatherCycle.IsFair).</summary>
        public bool fair;
        /// <summary>The grey sky (WeatherCycle.OvercastSky).</summary>
        public bool overcast;
        /// <summary>The first sunshine after rain (WeatherCycle.SunAfterRain).</summary>
        public bool sunAfterRain;
        /// <summary>Game hours since it last rained; huge when it has not.</summary>
        public float hoursSinceRain;
        /// <summary>The hour of the day, 0..24.</summary>
        public float hour;
    }

    /// <summary>Where a species wants to be right now, and the flavour of its behaviour there.</summary>
    public struct HabitDecision
    {
        public RhythmPlace place;
        /// <summary>The day period the animal is keeping (usually the clock's; an overcast afternoon can run one period early).</summary>
        public int period;
        public bool raining;
        /// <summary>In the window after rain for a species that is more active then.</summary>
        public bool activeAfterRain;
        /// <summary>...and the sun is out.</summary>
        public bool sun;

        /// <summary>Equal keys need no new home or bias.</summary>
        public int Key => (int)place | (period << 4) | (raining ? 1 << 8 : 0) | (activeAfterRain ? 1 << 9 : 0) | (sun ? 1 << 10 : 0);
    }

    /// <summary>
    /// The rules behind WildlifeSchedule as pure functions of data (1.9): a species' rhythm, its weather habits, the day period and the
    /// weather give a place and a behaviour bias. No state, no randomness, no Unity objects: the same forecast gives the same day.
    /// Day periods are indices as in DailyRhythm.For (0 night, 1 dawn, 2 morning, 3 midday, 4 golden hour, 5 dusk).
    /// </summary>
    public static class WeatherHabitRules
    {
        /// <summary>The built-in rhythm of the five original species (they have no DailyRhythm data).</summary>
        public static RhythmPlace LegacyPlace(string species, int p)
        {
            switch (species)
            {
                case "white_tailed_deer":
                    return p == 5 ? RhythmPlace.Water : p == 3 ? RhythmPlace.Woodland : p == 0 ? RhythmPlace.Woodland : RhythmPlace.Meadow;
                case "eastern_cottontail":
                    return p == 1 || p == 5 ? RhythmPlace.MeadowEdge : RhythmPlace.Brush;
                case "red_fox":
                    return p == 1 || p == 5 ? RhythmPlace.MeadowEdge : p == 3 ? RhythmPlace.Woodland : RhythmPlace.Home;
                case "red_tailed_hawk":
                    return p == 2 || p == 3 || p == 4 ? RhythmPlace.Ridge : RhythmPlace.Perch;
                default:
                    return RhythmPlace.Home;
            }
        }

        /// <summary>Where the species is in this period, weather aside.</summary>
        public static RhythmPlace BasePlace(string species, DailyRhythm rhythm, int period) =>
            rhythm != null && rhythm.enabled ? rhythm.For(period) : LegacyPlace(species, period);

        public static HabitDecision Decide(string species, DailyRhythm rhythm, WeatherHabits habits, int period, HabitWeather w)
        {
            bool has = habits != null && habits.enabled;
            int eff = period;
            // A grey day runs the afternoon early: late midday keeps golden-hour hours, golden hour keeps dusk's.
            if (has && habits.overcastEarly && w.overcast && !w.raining)
            {
                if (period == 3 && w.hour >= 14f) eff = 4;
                else if (period == 4) eff = 5;
            }
            RhythmPlace place = BasePlace(species, rhythm, eff);

            if (has && habits.IsFairOnly(period) && !w.fair) place = habits.foulPlace;

            // After rain (never at night: the birds are asleep, and the first dawn light is the next day's business).
            if (has && !w.raining && period != 0 && habits.afterRain != null)
            {
                foreach (var stage in habits.afterRain)
                {
                    if (stage == null || w.hoursSinceRain > stage.untilHours) continue;
                    if (stage.needsSun && !w.sunAfterRain) continue;
                    place = stage.place;
                    break;
                }
            }

            if (w.raining)
            {
                if (has && habits.rainShelter) place = habits.rainPlace;
                // The general rule: open ground empties and hawks sit; animals already in brush, at water or at home stay put.
                else if (place == RhythmPlace.Meadow || place == RhythmPlace.MeadowEdge) place = RhythmPlace.Woodland;
                else if (place == RhythmPlace.Ridge) place = RhythmPlace.Perch;
            }

            return new HabitDecision
            {
                place = place,
                period = eff,
                raining = w.raining,
                activeAfterRain = has && habits.activeAfterRain && !w.raining && w.hoursSinceRain <= habits.AfterRainWindow(),
                sun = w.sunAfterRain,
            };
        }

        /// <summary>Behaviour weighting for a species at a time of day and in this weather (multiplies the species' own weights).</summary>
        public static float Bias(string species, HabitDecision d, AnimalActivity a)
        {
            int p = d.period;
            bool quiet = p == 3 || p == 0;
            bool active = p == 1 || p == 5 || p == 4;
            float f;
            switch (a)
            {
                case AnimalActivity.Resting: f = quiet ? 4f : active ? 0.4f : 1f; break;
                case AnimalActivity.Sitting: f = quiet && species == "eastern_cottontail" ? 2.5f : 1f; break;
                case AnimalActivity.Grazing:
                case AnimalActivity.Foraging: f = active ? 1.6f : quiet ? 0.6f : 1f; break;
                case AnimalActivity.Perched: f = species == "red_tailed_hawk" && !quiet ? 1.8f : 1f; break;
                case AnimalActivity.Climbing: f = species == "eastern_gray_squirrel" && !quiet ? 1.5f : quiet ? 0.5f : 1f; break;
                default: f = 1f; break;
            }
            // Rain (1.4): more resting and sitting tight, less foraging out in the open.
            if (d.raining)
                f *= a == AnimalActivity.Resting || a == AnimalActivity.Sitting ? 2f : a == AnimalActivity.Grazing || a == AnimalActivity.Foraging ? 0.6f : 1f;
            // After rain (1.9): for the species that make the most of it, feeding and flitting up, resting down.
            if (d.activeAfterRain)
            {
                switch (a)
                {
                    case AnimalActivity.Resting: f *= 0.4f; break;
                    case AnimalActivity.Sitting: f *= 0.7f; break;
                    case AnimalActivity.Grazing:
                    case AnimalActivity.Foraging: f *= d.sun ? 2f : 1.5f; break;
                    case AnimalActivity.Flying: f *= d.sun ? 1.5f : 1.2f; break;
                    case AnimalActivity.Perched: f *= d.sun ? 0.8f : 1f; break;
                }
            }
            return f;
        }

        /// <summary>
        /// A stable number in [0, 1) from a species id and two integers (no System.Random, no hash codes that differ between runs):
        /// the per-group and per-animal delays of weather-triggered moves come from it.
        /// </summary>
        public static float Hash01(string species, int a, int b)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (species != null) foreach (char c in species) h = (h ^ c) * 16777619u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
