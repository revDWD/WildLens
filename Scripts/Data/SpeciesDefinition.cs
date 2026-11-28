using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using WildLens.Sound;

namespace WildLens.Data
{
    public enum LocomotionType { Ground, Flying }

    /// <summary>How the body moves while travelling. Drives both steering and procedural animation.</summary>
    public enum GaitStyle { Walk, Hop, Bursty, Fly }

    /// <summary>Where individuals are spawned relative to the trail and vegetation.</summary>
    public enum HabitatPreference { OpenMeadow, ForestEdge, Trees, Sky }

    /// <summary>
    /// Kinds of habitat hotspot: places with elevated odds of particular wildlife (see HabitatHotspot).
    /// Flags, so a species can favour several.
    /// </summary>
    [Flags]
    public enum HotspotType
    {
        None = 0,
        Meadow = 1 << 0,      // open grassland: deer, foraging foxes
        BrushPile = 1 << 1,   // cover near grass: rabbits, foxes hunting them
        MatureTree = 1 << 2,  // big old trees: squirrels
        Perch = 1 << 3,       // tall snags and boulders overlooking open ground: hawks
        Water = 1 << 4,       // shallows and banks of the pond and creek: herons, raccoons (1.0; appended)
    }

    /// <summary>Which placeholder body to build when no prefab is assigned.</summary>
    public enum PlaceholderBody { Deer, Rabbit, Fox, Hawk, Squirrel, Generic } // append only (serialized as ints)

    /// <summary>Generic placeholder body families (1.0 expansion framework).</summary>
    public enum BodyArchetype { Quadruped, WadingBird, GroundBird, PerchingBird }

    /// <summary>
    /// Proportions for a generic placeholder body (<see cref="PlaceholderBody.Generic"/>), so a new species needs data, not
    /// code. Metres. A purchased model (Animal Model Setup) replaces the placeholder entirely.
    /// </summary>
    [Serializable]
    public class BodyPlan
    {
        public BodyArchetype archetype = BodyArchetype.Quadruped;
        [Tooltip("Nose to rump (birds: breast to tail base).")] public float length = 0.6f;
        [Tooltip("Height of the back (birds: of the body's top).")] public float height = 0.35f;
        public float legLength = 0.2f;
        public float neckLength = 0.08f;
        public float headSize = 0.14f;
        [Tooltip("Snout (mammals) or beak (birds) length.")] public float snoutLength = 0.07f;
        public float earSize = 0.05f;
        public float tailLength = 0.3f;
        public float tailThickness = 0.08f;
        [Tooltip("Dark band across the eyes (raccoon).")] public bool faceMask;
        [Tooltip("Dark rings on the tail (raccoon).")] public bool tailRings;
        [Tooltip("Pointed crest on the crown (cardinal, blue jay). 1.1")] public bool crest;
        [Tooltip("Accent-coloured shoulder patches (red-winged blackbird's epaulettes). 1.1")] public bool wingPatches;
        [Tooltip("If alpha > 0, colours the head (turkey's bare head, opossum's pale face). 1.1")]
        public Color headColor = new Color(0f, 0f, 0f, 0f);
    }

    /// <summary>Where a species spends each part of the day (see WildlifeSchedule). Append only.</summary>
    public enum RhythmPlace
    {
        Home, Meadow, MeadowEdge, Brush, Woodland, Water, Ridge, Perch,
        /// <summary>A gathering place the species shares with its own residences (the crows' trailhead roost). See WeatherHabits.roostLabel. Appended.</summary>
        Roost,
    }

    /// <summary>
    /// A species' daily rhythm as data (1.0). When <see cref="enabled"/>, WildlifeSchedule uses these instead of its
    /// built-in rules, so new species get rhythms without code changes.
    /// </summary>
    [Serializable]
    public class DailyRhythm
    {
        public bool enabled;
        public RhythmPlace dawn, morning, midday, goldenHour, dusk, night;

        /// <summary>By period index: 0 night, 1 dawn, 2 morning, 3 midday, 4 golden hour, 5 dusk (DayPeriod order).</summary>
        public RhythmPlace For(int period)
        {
            switch (period)
            {
                case 1: return dawn;
                case 2: return morning;
                case 3: return midday;
                case 4: return goldenHour;
                case 5: return dusk;
                default: return night;
            }
        }

        public DailyRhythm() { }
        public DailyRhythm(RhythmPlace dawn, RhythmPlace morning, RhythmPlace midday, RhythmPlace golden, RhythmPlace dusk, RhythmPlace night)
        {
            enabled = true;
            this.dawn = dawn; this.morning = morning; this.midday = midday; goldenHour = golden; this.dusk = dusk; this.night = night;
        }
    }

    /// <summary>One step of where a species goes after rain stops: it applies while at most <see cref="untilHours"/> game hours have passed.</summary>
    [Serializable]
    public class AfterRainStage
    {
        public RhythmPlace place = RhythmPlace.Water;
        [Min(0f)] [Tooltip("Game hours after the rain stopped that this step lasts (the next step starts after it).")] public float untilHours = 2f;
        [Tooltip("Only in the first sunshine after rain (not raining, clouds broken); in grey weather the step is skipped.")] public bool needsSun;

        public AfterRainStage() { }
        public AfterRainStage(RhythmPlace place, float untilHours, bool needsSun = false)
        {
            this.place = place; this.untilHours = untilHours; this.needsSun = needsSun;
        }
    }

    /// <summary>
    /// How the weather changes where a species chooses to be (1.9, "natural events"). Pure data read by WildlifeSchedule: no
    /// triggers, no spawning, nothing aimed at the player. The world's own conditions (hour, weather now, weather recently, the four-day
    /// forecast) decide, so the same pattern repeats and a watcher can learn it. Off by default (old assets behave exactly as before).
    /// </summary>
    [Serializable]
    public class WeatherHabits
    {
        public bool enabled;

        [Header("While it rains")]
        [Tooltip("Where the species sits while it rains. Off: the general rule (animals of open ground go to the woods, hawks to their perches).")]
        public bool rainShelter;
        public RhythmPlace rainPlace = RhythmPlace.Woodland;

        [Header("After rain")]
        [Tooltip("Steps in order of untilHours (robin: meadow for the first hour, then the water's edge).")]
        public List<AfterRainStage> afterRain = new List<AfterRainStage>();
        [Tooltip("More active and out in the open for a while after rain (more feeding and flitting, less resting).")]
        public bool activeAfterRain;

        [Header("Fair weather only")]
        [Tooltip("Bit per day period (1 << DayPeriod: 1 dawn, 2 morning, 3 midday, 4 golden hour, 5 dusk). In those periods the rhythm's place is used only in fair weather.")]
        public int fairOnlyPeriods;
        [Tooltip("Where it is in a fair-weather-only period when the weather is not fair (grey, raining or just after rain).")]
        public RhythmPlace foulPlace = RhythmPlace.Perch;

        [Header("Other")]
        [Tooltip("On an overcast day it keeps the hours of the next period of the day (raccoons out early on the grey day).")]
        public bool overcastEarly;
        [Tooltip("For RhythmPlace.Roost: the label of the residence it gathers at (\"Trailhead\"); empty: its first residence.")]
        public string roostLabel = "";

        public bool IsFairOnly(int period) => period >= 0 && period < 31 && (fairOnlyPeriods & (1 << period)) != 0;

        /// <summary>Bit mask for <see cref="fairOnlyPeriods"/> from day period indices (0 night, 1 dawn, 2 morning, 3 midday, 4 golden hour, 5 dusk).</summary>
        public static int PeriodMask(params int[] periods)
        {
            int m = 0;
            if (periods != null) foreach (int p in periods) if (p >= 0 && p < 31) m |= 1 << p;
            return m;
        }

        /// <summary>The longest game-hour window after rain in which this species shows any habit.</summary>
        public float AfterRainWindow()
        {
            float w = 0f;
            if (afterRain != null) foreach (var s in afterRain) if (s != null && s.untilHours > w) w = s.untilHours;
            return activeAfterRain ? Mathf.Max(w, 2f) : w;
        }

        public WeatherHabits Clone()
        {
            var c = (WeatherHabits)MemberwiseClone();
            c.afterRain = new List<AfterRainStage>();
            if (afterRain != null) foreach (var s in afterRain) if (s != null) c.afterRain.Add(new AfterRainStage(s.place, s.untilHours, s.needsSun));
            return c;
        }
    }

    [Serializable]
    public class BehaviorOption
    {
        public AnimalActivity activity = AnimalActivity.LookingAround;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Seconds (min, max)")] public Vector2 duration = new Vector2(2f, 5f);

        public BehaviorOption() { }

        public BehaviorOption(AnimalActivity activity, float weight, float minSeconds, float maxSeconds)
        {
            this.activity = activity;
            this.weight = weight;
            duration = new Vector2(minSeconds, maxSeconds);
        }
    }

    /// <summary>
    /// All data describing a species: identity, journal text, spawning, movement and senses.
    /// One asset per species; adding a species is data work plus (optionally) a prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "WildLens/Species Definition", fileName = "Species_")]
    public class SpeciesDefinition : ScriptableObject
    {
        /// <summary>Data layout version. Assets below this are upgraded by the editor (SpeciesAssetMigrator).</summary>
        public const int CurrentDataVersion = 5; // 4: weather habits (SpeciesDefaults.ApplyHabits); 5: the turkey's brighter colours and the voice field notes (same step); voices themselves live in VoiceBank, not on assets
        [HideInInspector] public int dataVersion; // 0 on assets created before versioning

        [Header("Identity")]
        [Tooltip("Stable id used in save files. Never change after release.")]
        public string speciesId = "species";
        public string displayName = "Species";
        public string scientificName = "";
        [TextArea(3, 8)] public string fieldNotes = "";

        [Header("Visuals")]
        [Tooltip("Optional authored prefab. If empty a placeholder body is generated.")]
        public GameObject prefab;
        public PlaceholderBody placeholderBody = PlaceholderBody.Deer;
        public Color primaryColor = new Color(0.5f, 0.4f, 0.3f);
        public Color secondaryColor = new Color(0.9f, 0.85f, 0.8f);
        public Color accentColor = new Color(0.15f, 0.12f, 0.1f);
        [Tooltip("Proportions when Placeholder Body is Generic.")]
        public BodyPlan bodyPlan = new BodyPlan();

        [Header("Daily rhythm (1.0)")]
        [Tooltip("Where this species spends each period of the day. Off: the built-in rules for the five original species.")]
        public DailyRhythm rhythm = new DailyRhythm();

        [Header("Population")]
        public HabitatPreference habitat = HabitatPreference.OpenMeadow;
        [Min(0)] public int groupCount = 3;
        [Tooltip("Individuals per group (min, max inclusive)")]
        public Vector2Int groupSize = new Vector2Int(1, 1);
        [Tooltip("Distance from the trail centre-line (min, max) that groups are placed at.")]
        public Vector2 spawnDistanceFromTrail = new Vector2(10f, 40f);
        [Tooltip("Habitat hotspots this species favours.")]
        public HotspotType preferredHotspots = HotspotType.None;
        [Range(0f, 1f)] [Tooltip("Chance that a group's home range is centred on a preferred hotspot (otherwise anywhere suitable).")]
        public float hotspotAffinity = 0.7f;

        [Header("Movement")]
        public LocomotionType locomotion = LocomotionType.Ground;
        public GaitStyle gait = GaitStyle.Walk;
        [Min(0f)] public float walkSpeed = 1.2f;
        [Min(0f)] public float runSpeed = 6f;
        [Tooltip("Degrees per second")] public float turnSpeed = 120f;
        [Tooltip("Steps or wing beats per second at walk speed")] public float gaitFrequency = 1.6f;
        [Tooltip("How far an individual roams from its home position")] public float wanderRadius = 25f;
        [Range(0f, 1f)] [Tooltip("Chance to perform a behaviour after each wander leg")]
        public float behaviorChance = 0.7f;
        [Range(0f, 1f)] [Tooltip("Chance that a wander leg is a short run instead of a walk (e.g. deer bounding).")]
        public float runLegChance = 0f;
        public float maxSlope = 35f;
        [Min(0f)] [Tooltip("Bank dwellers (duck, turtle, snake; 1.10): the animal never steps onto ground more than this many metres below its home, so it stays out of the water. 0 = off (every other species).")]
        public float maxStepDown = 0f;

        [Header("Flight (flying species, 1.1)")]
        [Tooltip("Cruising height above the ground (min, max). Hawks soar at 20-38 m; songbirds flit at 3-8 m.")]
        public Vector2 flightAltitude = new Vector2(20f, 38f);
        [Tooltip("Radius of circling flight (min, max).")]
        public Vector2 circleRadius = new Vector2(16f, 34f);

        [Header("Weather habits (1.9)")]
        [Tooltip("How rain, grey days and fair days change where this species goes. Off by default.")]
        public WeatherHabits weatherHabits = new WeatherHabits();

        [Header("Voice (2.0)")]
        [Tooltip("What it sounds like and when it is heard. Leave Enabled OFF (the default): the VoiceDirector then uses the species' default voice from VoiceBank, " +
                 "so every asset, old or new, is heard and follows later retuning. Tick Enabled and edit the notes only to override this one species by hand.")]
        public VoiceRecipe voice = new VoiceRecipe();

        [Header("Behaviours")]
        public List<BehaviorOption> behaviors = new List<BehaviorOption>();

        [Header("Awareness (see WildlifeAwareness)")]
        [Tooltip("Within this distance the animal can start to notice the player (Calm → Aware).")]
        public float awarenessRadius = 30f;
        [Tooltip("Within this distance a suspicious animal stops and watches the player (Aware → Alert).")]
        [FormerlySerializedAs("alertDistance")] public float alertRadius = 20f;
        [Tooltip("Closer than this the animal bolts (→ Fleeing).")]
        [FormerlySerializedAs("fleeDistance")] public float fleeRadius = 10f;
        [Tooltip("How fast suspicion builds (per second, scaled by distance, player noise and line of sight).")]
        public float suspicionGain = 1f;
        [Tooltip("How fast suspicion fades (per second) when the player is out of range or keeping still.")]
        public float suspicionDecay = 0.35f;
        [Tooltip("Seconds an alert animal tolerates being approached before it flees.")]
        public float alertPatience = 3f;
        [Tooltip("Multiplier on alert radius within which a sprinting player startles the animal into flight.")]
        public float startleSensitivity = 1f;
        [Tooltip("How far the animal runs when fleeing (min, max)")]
        public Vector2 fleeDistanceRange = new Vector2(15f, 30f);

        [Header("Photography")]
        [Tooltip("Multiplier on the frame-fill needed to identify / grade this species (below 1 = small animals get credit for smaller framing). See PhotoQualityEvaluator.")]
        [Range(0.3f, 1.5f)] public float photoSizeScale = 1f;
        [Tooltip("Excellent photos must be taken within this distance (detail and haze); Good within 2.5x.")]
        public float clearPhotoDistance = 30f;
        [Tooltip("Seconds of steady observation (centred in camera mode) needed to identify the species without a photo.")]
        public float observationTime = 1.2f;

        public BehaviorOption PickBehavior(System.Random rng = null, Func<AnimalActivity, float> bias = null)
        {
            if (behaviors == null || behaviors.Count == 0) return null;
            float W(BehaviorOption b) => Mathf.Max(0f, b.weight) * (bias != null ? Mathf.Max(0f, bias(b.activity)) : 1f);
            float total = 0f;
            foreach (var b in behaviors) total += W(b);
            if (total <= 0f) return behaviors[0];
            float roll = (rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value) * total;
            foreach (var b in behaviors)
            {
                roll -= W(b);
                if (roll <= 0f) return b;
            }
            return behaviors[behaviors.Count - 1];
        }

        private void OnValidate()
        {
            if (alertRadius > awarenessRadius) alertRadius = awarenessRadius;
            if (fleeRadius > alertRadius) fleeRadius = alertRadius;
            if (groupSize.x < 1) groupSize.x = 1;
            if (groupSize.y < groupSize.x) groupSize.y = groupSize.x;
        }
    }
}
