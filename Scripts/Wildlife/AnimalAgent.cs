using System;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;

namespace WildLens.Wildlife
{
    /// <summary>
    /// Base class for all wildlife brains. Owns species data, the current observable activity and the
    /// motion values the animation layer reads. Subclasses implement locomotion-specific behaviour.
    /// </summary>
    public abstract class AnimalAgent : MonoBehaviour
    {
        public SpeciesDefinition Species { get; private set; }
        public AnimalActivity Activity { get; private set; } = AnimalActivity.Wandering;
        public Vector3 Velocity { get; protected set; }
        public float Speed => Velocity.magnitude;
        public Vector3 HomePosition { get; protected set; }

        /// <summary>Gait cycle in radians, advanced by locomotion. Animation uses it so feet match movement.</summary>
        public float GaitPhase { get; protected set; }

        /// <summary>World point the animal is paying attention to (player when alert), or null.</summary>
        public Vector3? AttentionPoint { get; protected set; }

        public event Action<AnimalAgent, AnimalActivity> ActivityChanged;

        /// <summary>The home range the animal was given at spawn (its territory). Schedules move the current home
        /// around this, never far from it.</summary>
        public Vector3 OriginHome { get; private set; }

        /// <summary>
        /// Optional time-of-day weighting of behaviours (0.9), e.g. deer bed down at midday. Multiplies each option's
        /// weight when the animal picks what to do next. Set by <c>WildlifeSchedule</c>.
        /// </summary>
        public Func<AnimalActivity, float> ActivityBias { get; set; }

        /// <summary>Move the animal's home range (it walks or flies there on its own, at its own pace).</summary>
        public virtual void SetHome(Vector3 home) => HomePosition = home;

        protected BehaviorOption PickBehavior() => Species.PickBehavior(Rng, ActivityBias);

        protected WildlifeWorld World { get; private set; }
        /// <summary>Perception of the player (optional; animals without one simply never react).</summary>
        public WildlifeAwareness Awareness { get; private set; }
        protected System.Random Rng { get; private set; }

        /// <summary>Per-individual personality, so a group doesn't move in lock-step.</summary>
        protected float Temperament { get; private set; }
        protected float NoiseSeed { get; private set; }

        public virtual void Initialize(SpeciesDefinition species, Vector3 home, WildlifeWorld world, int seed)
        {
            Species = species;
            HomePosition = home;
            OriginHome = home;
            World = world;
            Awareness = GetComponent<WildlifeAwareness>();
            Rng = new System.Random(seed);
            Temperament = Range(0.85f, 1.15f);
            NoiseSeed = Range(0f, 1000f);
        }

        protected void SetActivity(AnimalActivity activity)
        {
            if (Activity == activity) return;
            Activity = activity;
            ActivityChanged?.Invoke(this, activity);
        }

        protected float Range(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
        protected float Range(Vector2 minMax) => Range(minMax.x, minMax.y);
        protected bool Chance(float probability) => Rng.NextDouble() < probability;

        /// <summary>Approximately normal random value in [-1, 1].</summary>
        protected float Gaussianish() => (Range(-1f, 1f) + Range(-1f, 1f) + Range(-1f, 1f)) / 3f;

        protected float Noise(float speed, float offset = 0f) =>
            Mathf.PerlinNoise(NoiseSeed + offset, Time.time * speed) * 2f - 1f;

        protected bool TryGetPlayer(out Vector3 position, out float distance)
        {
            position = PlayerPresence.Position;
            distance = PlayerPresence.Exists ? Vector3.Distance(position, transform.position) : float.MaxValue;
            return PlayerPresence.Exists;
        }

        protected virtual void Update()
        {
            if (Species == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Tick(dt);
        }

        protected abstract void Tick(float dt);
    }
}
