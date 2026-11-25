using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    }

    /// <summary>Which placeholder body to build when no prefab is assigned.</summary>
    public enum PlaceholderBody { Deer, Rabbit, Fox, Hawk, Squirrel }

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
        public const int CurrentDataVersion = 3;
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

        public BehaviorOption PickBehavior(System.Random rng = null)
        {
            if (behaviors == null || behaviors.Count == 0) return null;
            float total = 0f;
            foreach (var b in behaviors) total += Mathf.Max(0f, b.weight);
            if (total <= 0f) return behaviors[0];
            float roll = (rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value) * total;
            foreach (var b in behaviors)
            {
                roll -= Mathf.Max(0f, b.weight);
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
