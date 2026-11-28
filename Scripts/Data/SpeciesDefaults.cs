using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Data
{
    /// <summary>
    /// Tuned default values for the reserve's species (the five originals, the 1.1 expansion, the 1.4 and 1.5 additions, and the six residents of the trailhead, glades, meadow and deep woods: twenty-four in all). The editor builder writes these out as assets,
    /// and the game falls back to them at runtime if no database asset is assigned. Tune the assets, not this file.
    /// </summary>
    public static class SpeciesDefaults
    {
        public static List<SpeciesDefinition> CreateAll()
        {
            return new List<SpeciesDefinition>
            {
                Deer(), Rabbit(), Fox(), Hawk(), Squirrel(),
                // 1.1 expansion: all generic bodies and data rhythms (Documentation~/WildlifeExpansion.md).
                Heron(), Raccoon(), Turkey(), BarredOwl(), Groundhog(), Cardinal(), BlueJay(), Blackbird(), Opossum(), Coyote(),
                // 1.4: the creek itself had no specialists. Both live only at the Creek Ford (groupCount 0: residents only).
                Kingfisher(), Sandpiper(),
                // 1.5: the waterfall's resident.
                Phoebe(),
                // 1.8: six species a place was waiting for (trailhead and road, glades, blind meadow, creek brush, deep woods).
                // Appended, never reordered: ids and list order are permanent.
                Crow(), Robin(), Chickadee(), Goldfinch(), SongSparrow(), BlackBear(),
                // 1.9: three more neighbours for the new places (residents only: they live where a residence places them).
                EasternBluebird(), TuftedTitmouse(), LouisianaWaterthrush(),
            };
        }

        /// <summary>
        /// Pre-1.1 opt-in examples. Since 1.1 every species is part of <see cref="CreateAll"/>, so this is empty; kept so
        /// the builder's old "Preview expansion species" setting still compiles and does nothing harmful.
        /// </summary>
        public static List<SpeciesDefinition> CreateExpansionPreview()
        {
            return new List<SpeciesDefinition>();
        }

        // ------------------------------------------------------------------ 1.1 expansion species

        public static SpeciesDefinition Turkey()
        {
            var s = New("wild_turkey", "Wild Turkey", "Meleagris gallopavo",
                "Big, wary ground bird that moves through open woodland and field edges in loose flocks, scratching for " +
                "acorns and insects. Look for the bare blue-and-red head and the bronze sheen on the back. Crosses roads unhurried.",
                PlaceholderBody.Generic);
            s.bodyPlan = new BodyPlan
            {
                archetype = BodyArchetype.GroundBird, length = 0.62f, height = 0.78f, legLength = 0.36f, neckLength = 0.24f,
                headSize = 0.075f, snoutLength = 0.03f, tailLength = 0.32f, tailThickness = 0.04f,
                headColor = new Color(0.55f, 0.62f, 0.78f),
            };
            // 1.9: darker bronze-brown overall, a lighter bronze for the second material, and the bare red of head, wattle and legs as the accent.
            s.primaryColor = new Color(0.25f, 0.18f, 0.11f);
            s.secondaryColor = new Color(0.36f, 0.26f, 0.15f);
            s.accentColor = new Color(0.66f, 0.2f, 0.16f);
            s.habitat = HabitatPreference.ForestEdge;
            s.groupCount = 1; s.groupSize = new Vector2Int(4, 7);
            s.spawnDistanceFromTrail = new Vector2(18f, 50f);
            s.preferredHotspots = HotspotType.Meadow | HotspotType.BrushPile; s.hotspotAffinity = 0.8f;
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 0.8f; s.runSpeed = 5f; s.turnSpeed = 90f; s.gaitFrequency = 1.8f;
            s.wanderRadius = 35f; s.behaviorChance = 0.75f; s.runLegChance = 0f;
            Awareness(s, aware: 34f, alert: 22f, flee: 11f, gain: 0.9f, decay: 0.12f, patience: 4f, startle: 1.1f);
            s.fleeDistanceRange = new Vector2(18f, 30f);
            s.photoSizeScale = 0.9f; s.clearPhotoDistance = 30f; s.observationTime = 1.2f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Foraging, 5f, 4f, 10f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 0.7f, 3f, 6f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Resting, 0.5f, 8f, 16f));
            s.rhythm = new DailyRhythm(RhythmPlace.MeadowEdge, RhythmPlace.Meadow, RhythmPlace.Woodland, RhythmPlace.Meadow, RhythmPlace.MeadowEdge, RhythmPlace.Woodland);
            s.weatherHabits = Habits(RhythmPlace.Woodland, true, After(RhythmPlace.Meadow, 3f, true));
            Hint(s, "Flocks wait out the rain under the trees, then come out to scratch the meadow once the sun returns.");
            return s;
        }

        public static SpeciesDefinition BarredOwl()
        {
            var s = New("barred_owl", "Barred Owl", "Strix varia",
                "A round-headed, dark-eyed owl of mature woods near water. Sits close to the trunk through the day, " +
                "unbothered unless you crowd it, then drifts out at dusk to hunt the meadow edges on silent wings.",
                PlaceholderBody.Generic);
            s.bodyPlan = new BodyPlan { archetype = BodyArchetype.PerchingBird, length = 0.38f, headSize = 0.17f, tailLength = 0.2f };
            s.primaryColor = new Color(0.46f, 0.39f, 0.31f);
            s.secondaryColor = new Color(0.86f, 0.82f, 0.72f);
            s.accentColor = new Color(0.3f, 0.24f, 0.18f);
            s.habitat = HabitatPreference.Sky;
            s.locomotion = LocomotionType.Flying;
            s.groupCount = 0; s.groupSize = new Vector2Int(1, 1); // lives in the grove (resident)
            s.spawnDistanceFromTrail = new Vector2(0f, 30f);
            s.preferredHotspots = HotspotType.MatureTree | HotspotType.Perch; s.hotspotAffinity = 0.9f;
            s.gait = GaitStyle.Fly;
            s.walkSpeed = 5.5f; s.runSpeed = 9f; s.turnSpeed = 70f; s.gaitFrequency = 2.4f;
            s.wanderRadius = 40f;
            s.flightAltitude = new Vector2(6f, 11f); s.circleRadius = new Vector2(9f, 16f);
            Awareness(s, aware: 26f, alert: 15f, flee: 8f, gain: 0.7f, decay: 0.18f, patience: 6f, startle: 0.9f);
            s.photoSizeScale = 0.75f; s.clearPhotoDistance = 28f; s.observationTime = 1.3f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Perched, 6f, 45f, 90f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Flying, 1f, 8f, 15f));
            // 1.9: at dusk it moves to a snag at the meadow's edge (a perch to be photographed on), not out over open ground; rain keeps it in the grove.
            s.rhythm = new DailyRhythm(RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Perch, RhythmPlace.MeadowEdge);
            s.weatherHabits = Habits(RhythmPlace.Woodland, false);
            Hint(s, "On a dry evening look for it on a snag at the meadow's edge; in rain it stays in the grove.");
            return s;
        }

        public static SpeciesDefinition Groundhog()
        {
            var s = New("groundhog", "Groundhog", "Marmota monax",
                "A stout, grizzled marmot of field edges and road verges. Grazes close to its burrow and often sits bolt " +
                "upright to look around, a sentry on the bank. Out in the morning and late afternoon; underground otherwise.",
                PlaceholderBody.Generic);
            s.bodyPlan = new BodyPlan
            {
                archetype = BodyArchetype.Quadruped, length = 0.5f, height = 0.27f, legLength = 0.08f, neckLength = 0.04f,
                headSize = 0.13f, snoutLength = 0.035f, earSize = 0.025f, tailLength = 0.14f, tailThickness = 0.06f,
            };
            s.primaryColor = new Color(0.45f, 0.36f, 0.25f);
            s.secondaryColor = new Color(0.6f, 0.5f, 0.37f);
            s.accentColor = new Color(0.18f, 0.13f, 0.1f);
            s.habitat = HabitatPreference.ForestEdge;
            s.groupCount = 2; s.groupSize = new Vector2Int(1, 1);
            s.spawnDistanceFromTrail = new Vector2(6f, 25f);
            s.preferredHotspots = HotspotType.BrushPile | HotspotType.Meadow; s.hotspotAffinity = 0.8f;
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 0.5f; s.runSpeed = 3.5f; s.turnSpeed = 140f; s.gaitFrequency = 2.6f;
            s.wanderRadius = 10f; s.behaviorChance = 0.85f; s.runLegChance = 0f;
            Awareness(s, aware: 22f, alert: 13f, flee: 6f, gain: 1f, decay: 0.15f, patience: 4f, startle: 1.1f);
            s.fleeDistanceRange = new Vector2(6f, 10f);
            s.photoSizeScale = 0.75f; s.clearPhotoDistance = 20f; s.observationTime = 1f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grazing, 4f, 4f, 10f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sitting, 3f, 4f, 10f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Resting, 1f, 10f, 20f));
            s.rhythm = new DailyRhythm(RhythmPlace.Home, RhythmPlace.MeadowEdge, RhythmPlace.Brush, RhythmPlace.MeadowEdge, RhythmPlace.Home, RhythmPlace.Home);
            return s;
        }

        private static SpeciesDefinition Songbird(string id, string display, string sci, string notes, float length, float head,
            Color primary, Color secondary, Color accent, HotspotType hotspots, int groups, Vector2Int size)
        {
            var s = New(id, display, sci, notes, PlaceholderBody.Generic);
            s.bodyPlan = new BodyPlan { archetype = BodyArchetype.PerchingBird, length = length, headSize = head, tailLength = 0.08f };
            s.primaryColor = primary; s.secondaryColor = secondary; s.accentColor = accent;
            s.habitat = HabitatPreference.Sky;
            s.locomotion = LocomotionType.Flying;
            s.groupCount = groups; s.groupSize = size;
            s.spawnDistanceFromTrail = new Vector2(0f, 25f);
            s.preferredHotspots = hotspots; s.hotspotAffinity = 0.9f;
            s.gait = GaitStyle.Fly;
            s.walkSpeed = 4.5f; s.runSpeed = 8f; s.turnSpeed = 200f; s.gaitFrequency = 9f;
            s.wanderRadius = 25f;
            s.flightAltitude = new Vector2(3.5f, 7f); s.circleRadius = new Vector2(4f, 9f);
            Awareness(s, aware: 16f, alert: 9f, flee: 4.5f, gain: 1f, decay: 0.25f, patience: 3f, startle: 1.2f);
            s.photoSizeScale = 0.4f; s.clearPhotoDistance = 12f; s.observationTime = 1f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Perched, 6f, 8f, 22f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Flying, 2f, 4f, 8f));
            return s;
        }

        public static SpeciesDefinition Cardinal()
        {
            var s = Songbird("northern_cardinal", "Northern Cardinal", "Cardinalis cardinalis",
                "The red bird of thickets and hedgerows, crested, with a black face and a heavy seed-cracking bill. " +
                "Shy in the open; busiest at the brush edge and the feeder at first and last light. Listen for its clear whistle.",
                0.1f, 0.036f, new Color(0.78f, 0.1f, 0.08f), new Color(0.82f, 0.2f, 0.14f), new Color(0.06f, 0.05f, 0.05f),
                HotspotType.BrushPile, 2, new Vector2Int(1, 2));
            s.bodyPlan.crest = true; s.bodyPlan.faceMask = true;
            s.rhythm = new DailyRhythm(RhythmPlace.Brush, RhythmPlace.Brush, RhythmPlace.Woodland, RhythmPlace.Brush, RhythmPlace.Brush, RhythmPlace.Woodland);
            Bathes(s, "After rain it comes down to bathe at the water's edge.");
            return s;
        }

        public static SpeciesDefinition BlueJay()
        {
            var s = Songbird("blue_jay", "Blue Jay", "Cyanocitta cristata",
                "A loud, crested jay of oaks and mature woods: bright blue above, white below, with a black necklace. Raises " +
                "the alarm at hawks and people alike. Most active in the morning, carrying acorns through the canopy.",
                0.12f, 0.04f, new Color(0.27f, 0.45f, 0.78f), new Color(0.9f, 0.91f, 0.93f), new Color(0.08f, 0.08f, 0.1f),
                HotspotType.MatureTree, 2, new Vector2Int(1, 3));
            s.bodyPlan.crest = true; s.bodyPlan.faceMask = true;
            Awareness(s, aware: 20f, alert: 12f, flee: 6f, gain: 1.1f, decay: 0.22f, patience: 2.5f, startle: 1.2f);
            s.rhythm = new DailyRhythm(RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Woodland);
            return s;
        }

        public static SpeciesDefinition Blackbird()
        {
            var s = Songbird("red_winged_blackbird", "Red-winged Blackbird", "Agelaius phoeniceus",
                "Glossy black males flash scarlet shoulder patches from the tops of cattails and sing a gurgling " +
                "'konk-la-ree' across the marsh. Never far from the pond and creek; noisiest at dawn.",
                0.1f, 0.032f, new Color(0.06f, 0.06f, 0.07f), new Color(0.1f, 0.1f, 0.11f), new Color(0.86f, 0.14f, 0.06f),
                HotspotType.Water, 2, new Vector2Int(2, 3));
            s.bodyPlan.wingPatches = true;
            s.rhythm = new DailyRhythm(RhythmPlace.Water, RhythmPlace.Water, RhythmPlace.Water, RhythmPlace.Water, RhythmPlace.Water, RhythmPlace.Water);
            Bathes(s, "After rain it drops down to the water's edge to bathe and drink.");
            return s;
        }

        public static SpeciesDefinition Opossum()
        {
            var s = New("virginia_opossum", "Virginia Opossum", "Didelphis virginiana",
                "North America's only marsupial: grizzled grey, with a pale pointed face, black ears and a long bare tail. " +
                "Ambles slowly along creek banks and woodland floors after dark; caught out at dusk it simply freezes.",
                PlaceholderBody.Generic);
            s.bodyPlan = new BodyPlan
            {
                archetype = BodyArchetype.Quadruped, length = 0.5f, height = 0.3f, legLength = 0.1f, neckLength = 0.05f,
                headSize = 0.12f, snoutLength = 0.09f, earSize = 0.035f, tailLength = 0.32f, tailThickness = 0.035f,
                headColor = new Color(0.92f, 0.9f, 0.86f),
            };
            s.primaryColor = new Color(0.6f, 0.58f, 0.56f);
            s.secondaryColor = new Color(0.84f, 0.82f, 0.78f);
            s.accentColor = new Color(0.1f, 0.09f, 0.09f);
            s.habitat = HabitatPreference.ForestEdge;
            s.groupCount = 1; s.groupSize = new Vector2Int(1, 1);
            s.spawnDistanceFromTrail = new Vector2(10f, 35f);
            s.preferredHotspots = HotspotType.Water | HotspotType.MatureTree; s.hotspotAffinity = 0.8f;
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 0.55f; s.runSpeed = 2.4f; s.turnSpeed = 100f; s.gaitFrequency = 2.2f;
            s.wanderRadius = 25f; s.behaviorChance = 0.7f;
            // Slow and tolerant: freezes rather than flees.
            Awareness(s, aware: 18f, alert: 10f, flee: 4f, gain: 0.7f, decay: 0.2f, patience: 7f, startle: 0.8f);
            s.fleeDistanceRange = new Vector2(5f, 9f);
            s.photoSizeScale = 0.75f; s.clearPhotoDistance = 18f; s.observationTime = 1.1f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Foraging, 3f, 3f, 8f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sniffing, 3f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 1f, 2f, 4f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Resting, 2f, 12f, 25f));
            s.rhythm = new DailyRhythm(RhythmPlace.Woodland, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Woodland, RhythmPlace.Water, RhythmPlace.Water);
            s.weatherHabits = new WeatherHabits { enabled = true, overcastEarly = true };
            Hint(s, "A grey afternoon brings it out early.");
            return s;
        }

        public static SpeciesDefinition Coyote()
        {
            var s = New("coyote", "Coyote", "Canis latrans",
                "Lean, grizzled wild dog of open country, larger and greyer than a fox, with a black-tipped tail carried low. " +
                "Rarely seen and gone at the first sign of you. Hunts the far meadows at dawn and dusk.",
                PlaceholderBody.Generic);
            s.bodyPlan = new BodyPlan
            {
                archetype = BodyArchetype.Quadruped, length = 0.95f, height = 0.62f, legLength = 0.38f, neckLength = 0.13f,
                headSize = 0.19f, snoutLength = 0.13f, earSize = 0.075f, tailLength = 0.38f, tailThickness = 0.1f,
            };
            s.primaryColor = new Color(0.55f, 0.47f, 0.36f);
            s.secondaryColor = new Color(0.82f, 0.76f, 0.66f);
            s.accentColor = new Color(0.24f, 0.19f, 0.15f);
            s.habitat = HabitatPreference.OpenMeadow;
            s.groupCount = 0; s.groupSize = new Vector2Int(1, 1); // one individual, resident below the ridge
            s.spawnDistanceFromTrail = new Vector2(45f, 110f);
            s.preferredHotspots = HotspotType.Meadow; s.hotspotAffinity = 0.7f;
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 1.4f; s.runSpeed = 10f; s.turnSpeed = 110f; s.gaitFrequency = 2f;
            s.wanderRadius = 80f; s.behaviorChance = 0.6f; s.runLegChance = 0.03f;
            Awareness(s, aware: 60f, alert: 40f, flee: 22f, gain: 1.1f, decay: 0.1f, patience: 3f, startle: 1.2f);
            s.fleeDistanceRange = new Vector2(35f, 60f);
            s.photoSizeScale = 0.95f; s.clearPhotoDistance = 45f; s.observationTime = 1.4f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sniffing, 3f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2.5f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sitting, 1f, 4f, 8f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Resting, 1f, 8f, 16f));
            s.rhythm = new DailyRhythm(RhythmPlace.Meadow, RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.MeadowEdge, RhythmPlace.Meadow, RhythmPlace.Meadow);
            return s;
        }

        public static SpeciesDefinition Raccoon()
        {
            var s = New("raccoon", "Raccoon", "Procyon lotor",
                "A masked forager of creek banks and woodland edges. Mostly out at dusk and through the night, feeling " +
                "for crayfish and frogs at the water's edge with nimble forepaws. The ringed tail and black mask are unmistakable.",
                PlaceholderBody.Generic);
            s.bodyPlan = new BodyPlan
            {
                archetype = BodyArchetype.Quadruped, length = 0.6f, height = 0.33f, legLength = 0.16f, neckLength = 0.05f,
                headSize = 0.15f, snoutLength = 0.06f, earSize = 0.045f, tailLength = 0.3f, tailThickness = 0.09f,
                faceMask = true, tailRings = true,
            };
            s.primaryColor = new Color(0.47f, 0.45f, 0.42f);
            s.secondaryColor = new Color(0.86f, 0.85f, 0.82f);
            s.accentColor = new Color(0.1f, 0.09f, 0.09f);
            s.habitat = HabitatPreference.ForestEdge;
            s.groupCount = 1; s.groupSize = new Vector2Int(1, 2);
            s.spawnDistanceFromTrail = new Vector2(15f, 45f);
            s.preferredHotspots = HotspotType.Water | HotspotType.MatureTree; s.hotspotAffinity = 0.8f;
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 0.8f; s.runSpeed = 4.5f; s.turnSpeed = 110f; s.gaitFrequency = 2.6f;
            s.wanderRadius = 30f; s.behaviorChance = 0.7f; s.runLegChance = 0.02f;
            Awareness(s, aware: 24f, alert: 14f, flee: 7f, gain: 0.8f, decay: 0.2f, patience: 5f, startle: 1f);
            s.fleeDistanceRange = new Vector2(10f, 18f);
            s.photoSizeScale = 0.8f; s.clearPhotoDistance = 24f; s.observationTime = 1.2f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Foraging, 3f, 3f, 7f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sniffing, 2f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2f, 2f, 4f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sitting, 1f, 3f, 6f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 1f, 3f, 6f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Resting, 1.5f, 10f, 20f));
            s.rhythm = new DailyRhythm(RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Home, RhythmPlace.Water, RhythmPlace.Water);
            s.weatherHabits = new WeatherHabits { enabled = true, overcastEarly = true };
            Hint(s, "On a grey day it comes down to the water long before dusk.");
            return s;
        }

        public static SpeciesDefinition Heron()
        {
            var s = New("great_blue_heron", "Great Blue Heron", "Ardea herodias",
                "A tall, patient hunter of the shallows. Stands motionless for minutes, neck folded, then strikes. Wary: " +
                "it watches walkers from far off. Best found at the pond's edge in the quiet of early morning.",
                PlaceholderBody.Generic);
            s.bodyPlan = new BodyPlan
            {
                archetype = BodyArchetype.WadingBird, length = 0.8f, height = 0.95f, legLength = 0.6f, neckLength = 0.42f,
                headSize = 0.085f, snoutLength = 0.15f, tailLength = 0.14f, tailThickness = 0.05f,
            };
            s.primaryColor = new Color(0.45f, 0.52f, 0.6f);
            s.secondaryColor = new Color(0.86f, 0.87f, 0.88f);
            s.accentColor = new Color(0.12f, 0.12f, 0.14f);
            s.habitat = HabitatPreference.OpenMeadow;
            s.groupCount = 1; s.groupSize = new Vector2Int(1, 1);
            s.spawnDistanceFromTrail = new Vector2(20f, 60f);
            s.preferredHotspots = HotspotType.Water; s.hotspotAffinity = 1f;
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 0.4f; s.runSpeed = 2.2f; s.turnSpeed = 60f; s.gaitFrequency = 0.9f;
            s.wanderRadius = 14f; s.behaviorChance = 0.85f; s.runLegChance = 0f;
            Awareness(s, aware: 42f, alert: 28f, flee: 16f, gain: 1f, decay: 0.25f, patience: 3f, startle: 1.2f);
            s.fleeDistanceRange = new Vector2(20f, 35f);
            s.photoSizeScale = 1f; s.clearPhotoDistance = 45f; s.observationTime = 1.4f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 4f, 6f, 14f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Foraging, 2f, 3f, 6f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 1f, 3f, 6f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Resting, 0.5f, 8f, 16f));
            s.rhythm = new DailyRhythm(RhythmPlace.Water, RhythmPlace.Water, RhythmPlace.Water, RhythmPlace.Water, RhythmPlace.Water, RhythmPlace.Water);
            s.weatherHabits = Habits(null, true, After(RhythmPlace.Water, 2.5f));
            Hint(s, "It hunts hardest at dawn and right after rain, when the shallows are stirred up.");
            return s;
        }

        // ------------------------------------------------------------------ 1.4 creek specialists

        public static SpeciesDefinition Kingfisher()
        {
            var s = Songbird("belted_kingfisher", "Belted Kingfisher", "Megaceryle alcyon",
                "A big-headed, ragged-crested fisher of clear creeks: slate blue above with a white collar. It rattles as it " +
                "flies low along the water, hovers, and plunges from an overhanging snag. Best at the ford in the early morning.",
                0.3f, 0.07f, new Color(0.36f, 0.45f, 0.56f), new Color(0.92f, 0.93f, 0.94f), new Color(0.1f, 0.1f, 0.12f),
                HotspotType.Water, 0, new Vector2Int(1, 1));
            s.bodyPlan.crest = true;
            s.flightAltitude = new Vector2(1.5f, 4f);   // low over the water
            s.circleRadius = new Vector2(8f, 16f);      // patrols up and down the reach
            s.wanderRadius = 30f;
            Awareness(s, aware: 28f, alert: 18f, flee: 9f, gain: 1.1f, decay: 0.22f, patience: 2.5f, startle: 1.3f);
            s.clearPhotoDistance = 22f;
            s.rhythm = new DailyRhythm(RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home);
            return s;
        }

        public static SpeciesDefinition Phoebe()
        {
            var s = Songbird("eastern_phoebe", "Eastern Phoebe", "Sayornis phoebe",
                "A soft grey-brown flycatcher that wags its tail and calls its own name, 'fee-bee'. Nests on rock ledges by " +
                "running water: at Willow Falls it sallies out from the ledge after insects over the pool.",
                0.17f, 0.036f, new Color(0.42f, 0.4f, 0.37f), new Color(0.9f, 0.88f, 0.8f), new Color(0.12f, 0.12f, 0.12f),
                HotspotType.Perch, 0, new Vector2Int(1, 1));
            s.flightAltitude = new Vector2(1.5f, 4f);
            s.circleRadius = new Vector2(3f, 7f);
            s.wanderRadius = 12f;
            s.rhythm = new DailyRhythm(RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home);
            return s;
        }

        public static SpeciesDefinition Sandpiper()
        {
            var s = New("spotted_sandpiper", "Spotted Sandpiper", "Actitis macularius",
                "A small shorebird that teeters its tail as it walks the gravel bars and stones of the creek, white below with " +
                "dark spots in summer. Flies off low over the water on stiff, flickering wings. Patient watchers at the ford see it.",
                PlaceholderBody.Generic);
            s.bodyPlan = new BodyPlan
            {
                archetype = BodyArchetype.WadingBird, length = 0.19f, height = 0.18f, legLength = 0.07f, neckLength = 0.03f,
                headSize = 0.032f, snoutLength = 0.026f, tailLength = 0.05f, tailThickness = 0.02f,
            };
            s.primaryColor = new Color(0.48f, 0.42f, 0.32f);
            s.secondaryColor = new Color(0.93f, 0.92f, 0.88f);
            s.accentColor = new Color(0.86f, 0.55f, 0.25f);
            s.habitat = HabitatPreference.OpenMeadow;
            s.groupCount = 0; s.groupSize = new Vector2Int(1, 2);
            s.spawnDistanceFromTrail = new Vector2(10f, 40f);
            s.preferredHotspots = HotspotType.Water; s.hotspotAffinity = 1f;
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 0.7f; s.runSpeed = 2.6f; s.turnSpeed = 160f; s.gaitFrequency = 3.2f;
            s.wanderRadius = 5f; s.behaviorChance = 0.8f; s.runLegChance = 0f;
            Awareness(s, aware: 20f, alert: 12f, flee: 6f, gain: 1f, decay: 0.25f, patience: 3f, startle: 1.2f);
            s.fleeDistanceRange = new Vector2(10f, 18f);
            s.photoSizeScale = 0.45f; s.clearPhotoDistance = 14f; s.observationTime = 1.2f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Foraging, 4f, 3f, 7f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 0.8f, 2f, 4f));
            s.rhythm = new DailyRhythm(RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home);
            return s;
        }

        // ------------------------------------------------------------------ 1.8 residents (a place first)

        // 1.8: the songbird factory's 'length' is the BODY (breast to tail base, about 0.45 of the bird's total length, as for the
        // cardinal 0.1 / jay 0.12 / blackbird 0.1); PlaceholderAnimalBuilder scales the hawk body by length / 0.42. The six below
        // first passed the TOTAL length, which made their placeholders about 1.3 to 2 times too big next to the older birds.
        public static SpeciesDefinition Crow()
        {
            var s = Songbird("american_crow", "American Crow", "Corvus brachyrhynchos",
                "All black from bill to tail, bold and clever, and quick to learn where people leave crumbs. Watch the trailhead and " +
                "the road verges; it is the loudest bird in the reserve in the first hours of the day.",
                0.2f, 0.038f, new Color(0.06f, 0.06f, 0.07f), new Color(0.1f, 0.1f, 0.11f), new Color(0.03f, 0.03f, 0.04f),
                HotspotType.MatureTree | HotspotType.Perch, 0, new Vector2Int(2, 3));
            s.bodyPlan.tailLength = 0.18f;
            s.walkSpeed = 6f; s.runSpeed = 10f; s.turnSpeed = 150f; s.gaitFrequency = 4.5f;
            s.wanderRadius = 35f;
            s.flightAltitude = new Vector2(4f, 10f); s.circleRadius = new Vector2(6f, 14f);
            // Bolder than the songbirds, warier than a tame chickadee, nearer than the hawk.
            Awareness(s, aware: 26f, alert: 15f, flee: 7f, gain: 1f, decay: 0.2f, patience: 3.5f, startle: 1.1f);
            s.photoSizeScale = 0.55f; s.clearPhotoDistance = 22f; s.observationTime = 1f;
            s.behaviors.Clear();
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Perched, 5f, 8f, 25f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Flying, 3f, 5f, 10f));
            s.rhythm = new DailyRhythm(RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home);
            // 1.9: it gathers at its residence at the trailhead as the light goes, and roosts there.
            s.rhythm = new DailyRhythm(RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Roost, RhythmPlace.Roost);
            s.weatherHabits = new WeatherHabits { enabled = true, roostLabel = "Trailhead" };
            Hint(s, "At dusk the crows gather at the trailhead to roost.");
            return s;
        }

        public static SpeciesDefinition Robin()
        {
            var s = Songbird("american_robin", "American Robin", "Turdus migratorius",
                "A grey-brown thrush with a brick-orange breast, running and pausing across open grass to listen for worms. " +
                "It sings from the treetops at first light and keeps to the lawns and meadow edges by day.",
                0.11f, 0.022f, new Color(0.34f, 0.31f, 0.28f), new Color(0.78f, 0.38f, 0.16f), new Color(0.13f, 0.11f, 0.1f),
                HotspotType.Meadow, 1, new Vector2Int(2, 2));
            s.bodyPlan.tailLength = 0.1f;
            s.walkSpeed = 5f; s.gaitFrequency = 7f;
            // The flying brain has no ground-foraging, so it perches long and flits short.
            Awareness(s, aware: 18f, alert: 10f, flee: 5f, gain: 1f, decay: 0.25f, patience: 3f, startle: 1.2f);
            s.photoSizeScale = 0.45f; s.clearPhotoDistance = 14f;
            s.behaviors.Clear();
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Perched, 7f, 10f, 26f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Flying, 2f, 4f, 8f));
            s.rhythm = new DailyRhythm(RhythmPlace.Meadow, RhythmPlace.Meadow, RhythmPlace.Woodland, RhythmPlace.Meadow, RhythmPlace.Brush, RhythmPlace.Home);
            s.weatherHabits = Habits(RhythmPlace.Brush, true, After(RhythmPlace.Meadow, 1f), After(RhythmPlace.Water, 2.5f));
            Hint(s, "In the first hour after rain it works the open grass for worms, then bathes at the water's edge.");
            return s;
        }

        public static SpeciesDefinition Chickadee()
        {
            var s = Songbird("black_capped_chickadee", "Black-capped Chickadee", "Poecile atricapillus",
                "A tiny, round bird with a black cap and bib, white cheeks and a soft grey back. The tamest bird in the woods: " +
                "stand still and it will come close, calling its own name, 'chick-a-dee-dee-dee'.",
                0.06f, 0.021f, new Color(0.5f, 0.52f, 0.54f), new Color(0.88f, 0.87f, 0.82f), new Color(0.06f, 0.06f, 0.07f),
                HotspotType.MatureTree, 0, new Vector2Int(2, 3));
            s.bodyPlan.headColor = new Color(0.07f, 0.07f, 0.08f); // the black cap and bib
            s.walkSpeed = 4f; s.gaitFrequency = 11f;
            s.wanderRadius = 14f;
            s.flightAltitude = new Vector2(3.5f, 5.5f); s.circleRadius = new Vector2(3f, 6f);
            // The smallest distances in the reserve: it accepts you sooner than anything else here.
            Awareness(s, aware: 10f, alert: 5.5f, flee: 2.5f, gain: 0.6f, decay: 0.35f, patience: 8f, startle: 0.6f);
            s.photoSizeScale = 0.35f; s.clearPhotoDistance = 9f; s.observationTime = 0.9f;
            s.behaviors.Clear();
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Perched, 6f, 6f, 16f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Flying, 2f, 3f, 6f));
            s.rhythm = new DailyRhythm(RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home);
            return s;
        }

        public static SpeciesDefinition Goldfinch()
        {
            var s = Songbird("american_goldfinch", "American Goldfinch", "Spinus tristis",
                "In summer the male is lemon-yellow with a black cap and black wings, bouncing over the meadow in dipping flight. " +
                "It feeds on wildflower and thistle seed and calls 'per-chick-o-ree' as it goes.",
                0.055f, 0.018f, new Color(0.93f, 0.8f, 0.1f), new Color(0.95f, 0.88f, 0.3f), new Color(0.06f, 0.06f, 0.07f),
                HotspotType.Meadow, 0, new Vector2Int(3, 4));
            s.bodyPlan.wingPatches = true;
            s.walkSpeed = 4.5f; s.gaitFrequency = 10f;
            Awareness(s, aware: 15f, alert: 8.5f, flee: 4f, gain: 1.1f, decay: 0.25f, patience: 3f, startle: 1.2f);
            s.photoSizeScale = 0.35f; s.clearPhotoDistance = 10f;
            s.rhythm = new DailyRhythm(RhythmPlace.Brush, RhythmPlace.Meadow, RhythmPlace.Meadow, RhythmPlace.Meadow, RhythmPlace.Brush, RhythmPlace.Home);
            Bathes(s, "After rain it comes to the water's edge to bathe.");
            return s;
        }

        public static SpeciesDefinition SongSparrow()
        {
            var s = Songbird("song_sparrow", "Song Sparrow", "Melospiza melodia",
                "A streaky brown sparrow with a dark spot at the centre of its breast, singing a bright, varied song from low brush. " +
                "It stays close to thickets and the creek, pumping its tail as it dives back into cover.",
                0.07f, 0.021f, new Color(0.45f, 0.34f, 0.24f), new Color(0.82f, 0.78f, 0.7f), new Color(0.25f, 0.17f, 0.12f),
                HotspotType.BrushPile, 2, new Vector2Int(1, 1));
            s.wanderRadius = 18f;
            Awareness(s, aware: 16f, alert: 9f, flee: 4.5f, gain: 1.1f, decay: 0.25f, patience: 3f, startle: 1.2f);
            s.photoSizeScale = 0.4f; s.clearPhotoDistance = 11f;
            s.rhythm = new DailyRhythm(RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home);
            Bathes(s, "After rain it comes down to the water's edge to bathe.");
            return s;
        }

        public static SpeciesDefinition BlackBear()
        {
            var s = New("american_black_bear", "American Black Bear", "Ursus americanus",
                "A large, shy forest bear: black, with a tan muzzle, round ears and hardly any tail. It feeds on berries, grubs and sedges at the " +
                "woods' edge at dawn and dusk and keeps well away from people; stay still and far back and it will settle.",
                PlaceholderBody.Generic);
            s.bodyPlan = new BodyPlan
            {
                archetype = BodyArchetype.Quadruped, length = 1.3f, height = 0.9f, legLength = 0.42f, neckLength = 0.18f,
                headSize = 0.26f, snoutLength = 0.14f, earSize = 0.09f, tailLength = 0.08f, tailThickness = 0.1f,
            };
            s.primaryColor = new Color(0.07f, 0.065f, 0.06f);
            s.secondaryColor = new Color(0.48f, 0.37f, 0.25f); // the tan muzzle (and a little under the chest)
            s.accentColor = new Color(0.05f, 0.045f, 0.04f);
            s.habitat = HabitatPreference.ForestEdge;
            s.groupCount = 0; s.groupSize = new Vector2Int(1, 1); // exactly one, resident in the deep woods
            s.spawnDistanceFromTrail = new Vector2(45f, 110f);
            s.preferredHotspots = HotspotType.MatureTree; s.hotspotAffinity = 0.5f;
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 1.1f; s.runSpeed = 9f; s.turnSpeed = 70f; s.gaitFrequency = 1.3f;
            s.wanderRadius = 45f; s.behaviorChance = 0.75f; s.runLegChance = 0.02f; s.maxSlope = 40f;
            // The widest berth in the reserve, and it only ever moves away; keep still at a distance and it settles.
            Awareness(s, aware: 75f, alert: 50f, flee: 28f, gain: 1f, decay: 0.12f, patience: 5f, startle: 1f);
            s.fleeDistanceRange = new Vector2(45f, 75f);
            s.photoSizeScale = 1.15f; s.clearPhotoDistance = 60f; s.observationTime = 1.6f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Foraging, 5f, 4f, 10f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sitting, 2.5f, 6f, 14f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Resting, 2.5f, 10f, 22f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2f, 3f, 6f));
            s.rhythm = new DailyRhythm(RhythmPlace.MeadowEdge, RhythmPlace.Woodland, RhythmPlace.Home, RhythmPlace.Woodland, RhythmPlace.Water, RhythmPlace.Home);
            // 1.9: it keeps to deep cover while it rains, and comes down to the creek at dusk only on a dry, fair evening.
            s.weatherHabits = Habits(RhythmPlace.Home, false);
            s.weatherHabits.fairOnlyPeriods = WeatherHabits.PeriodMask(5);
            s.weatherHabits.foulPlace = RhythmPlace.Home;
            Hint(s, "It keeps to deep cover in the rain and comes down to the creek at dusk only when the evening is dry.");
            return s;
        }

        // ------------------------------------------------------------------ 1.9 residents for the new places
        // Residents only (groupCount 0): they exist where a WildlifeResidence names them. 'length' is the BODY, about 0.45 of the bird's
        // total length (as for the chickadee 0.06 / 0.13, the sparrow 0.07 / 0.15, the robin 0.11 / 0.25); the real total is in
        // AnimalPrefabFactory.RealSize.

        public static SpeciesDefinition EasternBluebird()
        {
            var s = Songbird("eastern_bluebird", "Eastern Bluebird", "Sialia sialis",
                "A small thrush of open meadows: bright blue above, rusty breast, white belly. It sits upright on a fence post or low " +
                "branch, then drops to the grass for an insect and flies back to the same perch. Its song is a soft, warbling 'tu-a-wee'.",
                0.08f, 0.021f, new Color(0.25f, 0.42f, 0.8f), new Color(0.93f, 0.91f, 0.87f), new Color(0.72f, 0.4f, 0.2f),
                HotspotType.Meadow, 0, new Vector2Int(1, 2));
            s.bodyPlan.tailLength = 0.08f;
            s.walkSpeed = 4.5f; s.gaitFrequency = 8f;
            s.wanderRadius = 20f;
            s.flightAltitude = new Vector2(3.5f, 5.5f); s.circleRadius = new Vector2(3f, 7f);
            Awareness(s, aware: 18f, alert: 10f, flee: 5f, gain: 1f, decay: 0.25f, patience: 3f, startle: 1.2f);
            s.photoSizeScale = 0.4f; s.clearPhotoDistance = 12f; s.observationTime = 1f;
            s.behaviors.Clear();
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Perched, 7f, 10f, 26f)); // long on its post, short flights
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Flying, 2f, 3f, 7f));
            s.rhythm = new DailyRhythm(RhythmPlace.MeadowEdge, RhythmPlace.Meadow, RhythmPlace.Meadow, RhythmPlace.Meadow, RhythmPlace.MeadowEdge, RhythmPlace.Home);
            s.weatherHabits = Habits(RhythmPlace.Woodland, true, After(RhythmPlace.Meadow, 2.5f, true));
            Hint(s, "It is out on the meadow posts in the first sun after rain.");
            return s;
        }

        public static SpeciesDefinition TuftedTitmouse()
        {
            var s = Songbird("tufted_titmouse", "Tufted Titmouse", "Baeolophus bicolor",
                "A small grey bird with a pointed crest, big dark eyes and rusty flanks, bold and nearly as tame as the chickadee. " +
                "It works the trunks and branches of the woods all day, calling 'peter-peter-peter'.",
                0.07f, 0.02f, new Color(0.5f, 0.52f, 0.56f), new Color(0.88f, 0.87f, 0.83f), new Color(0.72f, 0.48f, 0.32f),
                HotspotType.MatureTree, 0, new Vector2Int(1, 2));
            s.bodyPlan.crest = true; s.bodyPlan.tailLength = 0.07f;
            s.walkSpeed = 4f; s.gaitFrequency = 10f;
            s.wanderRadius = 16f;
            s.flightAltitude = new Vector2(3.5f, 5.5f); s.circleRadius = new Vector2(3f, 6f);
            // Nearly as accepting as the chickadee.
            Awareness(s, aware: 11f, alert: 6f, flee: 3f, gain: 0.65f, decay: 0.33f, patience: 7f, startle: 0.65f);
            s.photoSizeScale = 0.37f; s.clearPhotoDistance = 9.5f; s.observationTime = 0.9f;
            s.behaviors.Clear();
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Perched, 6f, 6f, 16f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Flying, 2f, 3f, 6f));
            s.rhythm = new DailyRhythm(RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Woodland, RhythmPlace.Home);
            return s;
        }

        public static SpeciesDefinition LouisianaWaterthrush()
        {
            var s = Songbird("louisiana_waterthrush", "Louisiana Waterthrush", "Parkesia motacilla",
                "A brown, streaked warbler of shaded creeks and waterfalls that bobs its whole rear half as it walks the wet stones. " +
                "Wary, and hard to see against the rocks; its ringing song carries over the sound of the water.",
                0.065f, 0.019f, new Color(0.38f, 0.32f, 0.26f), new Color(0.9f, 0.88f, 0.82f), new Color(0.2f, 0.16f, 0.13f),
                HotspotType.Water, 0, new Vector2Int(1, 1));
            s.bodyPlan.tailLength = 0.07f;
            s.walkSpeed = 4f; s.gaitFrequency = 9f;
            s.wanderRadius = 12f;
            s.flightAltitude = new Vector2(1.5f, 3.5f); s.circleRadius = new Vector2(3f, 6f);
            // Wary: it notices you early and goes to cover.
            Awareness(s, aware: 20f, alert: 12f, flee: 6.5f, gain: 1.2f, decay: 0.2f, patience: 2.5f, startle: 1.3f);
            s.photoSizeScale = 0.4f; s.clearPhotoDistance = 11f; s.observationTime = 1.1f;
            s.behaviors.Clear();
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Perched, 5f, 6f, 16f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Flying, 2.5f, 3f, 6f));
            s.rhythm = new DailyRhythm(RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home, RhythmPlace.Home);
            s.weatherHabits = Habits(RhythmPlace.Home, true, After(RhythmPlace.Water, 2.5f));
            Hint(s, "It is busiest along the creek just after rain.");
            return s;
        }

        public static SpeciesDefinition CreateById(string speciesId)
        {
            foreach (var s in CreateAll())
            {
                if (s.speciesId == speciesId) return s;
                Object.DestroyImmediate(s);
            }
            return null;
        }

        /// <summary>
        /// Copies the gameplay tuning that has changed since data version 1 (awareness, behaviour set, run legs,
        /// photo requirements, observation, habitat hotspots, group size) from <paramref name="source"/>,
        /// leaving identity, visuals, prefab and movement untouched.
        /// </summary>
        public static void ApplyTuning(SpeciesDefinition target, SpeciesDefinition source)
        {
            target.photoSizeScale = source.photoSizeScale;
            target.observationTime = source.observationTime;
            target.preferredHotspots = source.preferredHotspots;
            target.hotspotAffinity = source.hotspotAffinity;
            target.groupCount = source.groupCount;
            target.groupSize = source.groupSize;
            target.awarenessRadius = source.awarenessRadius;
            target.alertRadius = source.alertRadius;
            target.fleeRadius = source.fleeRadius;
            target.suspicionGain = source.suspicionGain;
            target.suspicionDecay = source.suspicionDecay;
            target.alertPatience = source.alertPatience;
            target.startleSensitivity = source.startleSensitivity;
            target.fleeDistanceRange = source.fleeDistanceRange;
            target.runLegChance = source.runLegChance;
            target.clearPhotoDistance = source.clearPhotoDistance;
            target.behaviorChance = source.behaviorChance;
            target.behaviors = new List<BehaviorOption>();
            foreach (var b in source.behaviors)
                target.behaviors.Add(new BehaviorOption(b.activity, b.weight, b.duration.x, b.duration.y));
            ApplyHabits(target, source);
            target.dataVersion = SpeciesDefinition.CurrentDataVersion;
        }

        /// <summary>
        /// Species whose rhythm changed in 1.9 (the owl's dusk perch, the crow's roost); their DailyRhythm is copied to existing assets.
        /// Every other rhythm is left as the asset has it.
        /// </summary>
        private static readonly HashSet<string> RhythmRevisedIn19 = new HashSet<string> { "barred_owl", "american_crow" };

        /// <summary>Species whose colours changed in 1.9 (the fox and the turkey read as a dog and a chicken); their three colours are copied.</summary>
        private static readonly HashSet<string> ColoursRevisedIn19 = new HashSet<string> { "red_fox", "wild_turkey" };

        /// <summary>
        /// Brings the natural-events data to an existing asset (1.9): weather habits, field notes (which carry the hints), and, for the few
        /// species that changed, the rhythm and colours. Everything else (awareness, behaviours, groups, prefab, hand-tuned values) is left alone.
        /// A species without habits in <paramref name="source"/> keeps whatever the asset has.
        /// </summary>
        public static void ApplyHabits(SpeciesDefinition target, SpeciesDefinition source)
        {
            if (target == null || source == null) return;
            if (source.weatherHabits != null && source.weatherHabits.enabled) target.weatherHabits = source.weatherHabits.Clone();
            MigrateNotes(target, source);
            if (RhythmRevisedIn19.Contains(source.speciesId) && source.rhythm != null && source.rhythm.enabled && RhythmIsPre19Default(target.rhythm, source.speciesId))
                target.rhythm = new DailyRhythm(source.rhythm.dawn, source.rhythm.morning, source.rhythm.midday, source.rhythm.goldenHour, source.rhythm.dusk, source.rhythm.night);
            if (ColoursRevisedIn19.Contains(source.speciesId))
            {
                target.primaryColor = source.primaryColor;
                target.secondaryColor = source.secondaryColor;
                target.accentColor = source.accentColor;
            }
        }

        /// <summary>
        /// Field notes: an empty note or one still equal to the species' previous default (the default without its 1.9 hint) takes the new
        /// default; a hand-edited note keeps its text and only gains the hint sentence, if it does not already contain it.
        /// </summary>
        private static void MigrateNotes(SpeciesDefinition target, SpeciesDefinition source)
        {
            if (string.IsNullOrEmpty(source.fieldNotes)) return;
            string notes = target.fieldNotes ?? "";
            if (!_hints.TryGetValue(source.speciesId, out var hints) || hints.Count == 0)
            {
                if (notes.Trim().Length == 0) target.fieldNotes = source.fieldNotes;
                return;
            }
            string baseNote = source.fieldNotes;
            for (int i = hints.Count - 1; i >= 0; i--)
            {
                string tail = " " + hints[i];
                if (baseNote.EndsWith(tail, System.StringComparison.Ordinal)) baseNote = baseNote.Substring(0, baseNote.Length - tail.Length);
            }
            baseNote = baseNote.TrimEnd();
            if (notes.Trim().Length == 0 || notes.Trim() == baseNote.Trim()) { target.fieldNotes = source.fieldNotes; return; }
            foreach (var h in hints)
                if (notes.IndexOf(h, System.StringComparison.Ordinal) < 0) notes = notes.TrimEnd() + " " + h;
            target.fieldNotes = notes;
        }

        /// <summary>The owl's and the crow's rhythm before 1.9; only a rhythm still equal to it is replaced (a hand-tuned one stays).</summary>
        private static bool RhythmIsPre19Default(DailyRhythm r, string id)
        {
            if (r == null || !r.enabled) return true;
            RhythmPlace W = RhythmPlace.Woodland, E = RhythmPlace.MeadowEdge, H = RhythmPlace.Home;
            var old = id == "barred_owl" ? new DailyRhythm(W, W, W, W, E, E) : new DailyRhythm(H, H, H, H, H, H);
            return r.dawn == old.dawn && r.morning == old.morning && r.midday == old.midday && r.goldenHour == old.goldenHour && r.dusk == old.dusk && r.night == old.night;
        }

        /// <summary>The hint sentences each species' default notes end with (by species id), so a migration can tell them from the base note.</summary>
        private static readonly Dictionary<string, List<string>> _hints = new Dictionary<string, List<string>>();

        // ------------------------------------------------------------------ 1.9 natural events (weather habits as data)

        private static AfterRainStage After(RhythmPlace place, float untilHours, bool needsSun = false) => new AfterRainStage(place, untilHours, needsSun);

        /// <summary>Weather habits: where it sits in the rain (null: the general rule), the steps after rain, and whether it is livelier then.</summary>
        private static WeatherHabits Habits(RhythmPlace? rain, bool active, params AfterRainStage[] after)
        {
            var h = new WeatherHabits { enabled = true, activeAfterRain = active };
            if (rain.HasValue) { h.rainShelter = true; h.rainPlace = rain.Value; }
            if (after != null) h.afterRain.AddRange(after);
            return h;
        }

        /// <summary>Adds one short natural sentence to the field notes that hints at a habit (a hint, never an instruction).</summary>
        private static void Hint(SpeciesDefinition s, string sentence)
        {
            s.fieldNotes = s.fieldNotes.TrimEnd() + " " + sentence;
            if (!_hints.TryGetValue(s.speciesId, out var list)) _hints[s.speciesId] = list = new List<string>();
            if (!list.Contains(sentence)) list.Add(sentence);
        }

        /// <summary>Songbirds sit tight in brush while it rains and come down to bathe at the water's edge after it.</summary>
        private static void Bathes(SpeciesDefinition s, string hint)
        {
            s.weatherHabits = Habits(RhythmPlace.Brush, true, After(RhythmPlace.Water, 2.5f));
            Hint(s, hint);
        }

        private static SpeciesDefinition New(string id, string display, string sci, string notes, PlaceholderBody body)
        {
            var s = ScriptableObject.CreateInstance<SpeciesDefinition>();
            s.name = "Species_" + display.Replace(" ", "").Replace("-", "");
            s.speciesId = id;
            s.displayName = display;
            s.scientificName = sci;
            s.fieldNotes = notes;
            s.placeholderBody = body;
            s.dataVersion = SpeciesDefinition.CurrentDataVersion;
            return s;
        }

        private static void Awareness(SpeciesDefinition s, float aware, float alert, float flee, float gain, float decay,
            float patience, float startle)
        {
            s.awarenessRadius = aware; s.alertRadius = alert; s.fleeRadius = flee;
            s.suspicionGain = gain; s.suspicionDecay = decay;
            s.alertPatience = patience; s.startleSensitivity = startle;
        }

        public static SpeciesDefinition Deer()
        {
            var s = New("white_tailed_deer", "White-tailed Deer", "Odocoileus virginianus",
                "Graceful browser of meadows and woodland edges. When alarmed it raises its tail to flash the white " +
                "underside, a warning to the rest of the group. Most active at dawn and dusk.", PlaceholderBody.Deer);
            s.primaryColor = new Color(0.55f, 0.38f, 0.24f);
            s.secondaryColor = new Color(0.88f, 0.84f, 0.77f);
            s.accentColor = new Color(0.12f, 0.09f, 0.07f);
            s.habitat = HabitatPreference.OpenMeadow;
            s.groupCount = 3; s.groupSize = new Vector2Int(2, 3);
            s.spawnDistanceFromTrail = new Vector2(14f, 45f);
            s.preferredHotspots = HotspotType.Meadow; s.hotspotAffinity = 0.8f;
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 1.1f; s.runSpeed = 9f; s.turnSpeed = 80f; s.gaitFrequency = 1.4f;
            s.wanderRadius = 30f; s.behaviorChance = 0.8f; s.runLegChance = 0.07f;
            // Notices you from far away, then watches for a long time; freeze and it settles.
            Awareness(s, aware: 36f, alert: 24f, flee: 10f, gain: 0.8f, decay: 0.08f, patience: 6f, startle: 0.9f);
            s.fleeDistanceRange = new Vector2(18f, 28f);
            s.photoSizeScale = 1f; s.clearPhotoDistance = 30f; s.observationTime = 1.2f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grazing, 5f, 6f, 14f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2.5f, 3f, 6f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 0.8f, 3f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Resting, 0.5f, 12f, 25f));
            s.weatherHabits = Habits(RhythmPlace.Woodland, true, After(RhythmPlace.Meadow, 3f, true));
            Hint(s, "It waits out rain under the trees and steps out to feed in the first sun afterwards.");
            return s;
        }

        public static SpeciesDefinition Rabbit()
        {
            var s = New("eastern_cottontail", "Eastern Cottontail", "Sylvilagus floridanus",
                "Small rabbit of grassy clearings that rarely strays far from cover. Freezes when it senses danger, " +
                "relying on camouflage, then bolts in a zig-zag. Look for the bright white tail.", PlaceholderBody.Rabbit);
            s.primaryColor = new Color(0.52f, 0.44f, 0.36f);
            s.secondaryColor = new Color(0.92f, 0.9f, 0.86f);
            s.accentColor = new Color(0.2f, 0.16f, 0.13f);
            s.habitat = HabitatPreference.OpenMeadow;
            s.groupCount = 7; s.groupSize = new Vector2Int(1, 1);
            s.spawnDistanceFromTrail = new Vector2(5f, 22f);
            s.preferredHotspots = HotspotType.BrushPile | HotspotType.Meadow; s.hotspotAffinity = 0.75f;
            s.gait = GaitStyle.Hop;
            s.walkSpeed = 0.9f; s.runSpeed = 7f; s.turnSpeed = 220f; s.gaitFrequency = 2.2f;
            s.wanderRadius = 12f; s.behaviorChance = 0.85f;
            // Suspicion builds quickly, but like real cottontails it freezes (long Alert) rather than bolting.
            Awareness(s, aware: 16f, alert: 10f, flee: 4f, gain: 1.3f, decay: 0.12f, patience: 3f, startle: 1.2f);
            s.fleeDistanceRange = new Vector2(6f, 10f);
            s.photoSizeScale = 0.7f; s.clearPhotoDistance = 12f; s.observationTime = 1f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grazing, 4f, 3f, 8f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sitting, 2.5f, 3f, 7f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 1f, 2f, 4f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sniffing, 1f, 1.5f, 3f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 1f, 1.5f, 3f));
            s.weatherHabits = Habits(null, false, After(RhythmPlace.MeadowEdge, 8f));
            Hint(s, "After a wet night, look for it out on the meadow edge in the early light.");
            return s;
        }

        public static SpeciesDefinition Fox()
        {
            var s = New("red_fox", "Red Fox", "Vulpes vulpes",
                "Solitary, curious hunter of field margins. Trots with a light, purposeful gait and often pauses to " +
                "listen for voles under the grass before pouncing. The white-tipped tail is a reliable field mark.",
                PlaceholderBody.Fox);
            // 1.9: a brighter orange-red (the model's own colour multiplies it, so it needs headroom), a clean white and near-black socks.
            s.primaryColor = new Color(0.93f, 0.42f, 0.12f);
            s.secondaryColor = new Color(0.97f, 0.95f, 0.92f);
            s.accentColor = new Color(0.1f, 0.08f, 0.07f);
            s.habitat = HabitatPreference.ForestEdge;
            s.groupCount = 2; s.groupSize = new Vector2Int(1, 1);
            s.spawnDistanceFromTrail = new Vector2(10f, 35f);
            s.preferredHotspots = HotspotType.BrushPile | HotspotType.Meadow; s.hotspotAffinity = 0.6f;
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 1.5f; s.runSpeed = 8f; s.turnSpeed = 120f; s.gaitFrequency = 2.4f;
            s.wanderRadius = 55f; s.behaviorChance = 0.6f; s.runLegChance = 0.04f;
            Awareness(s, aware: 26f, alert: 15f, flee: 8f, gain: 0.9f, decay: 0.1f, patience: 4f, startle: 1f);
            s.fleeDistanceRange = new Vector2(14f, 22f);
            s.photoSizeScale = 0.85f; s.clearPhotoDistance = 25f; s.observationTime = 1.2f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sniffing, 3f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sitting, 1f, 4f, 8f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Resting, 1f, 8f, 16f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 1f, 3f, 6f));
            return s;
        }

        public static SpeciesDefinition Hawk()
        {
            var s = New("red_tailed_hawk", "Red-tailed Hawk", "Buteo jamaicensis",
                "Broad-winged raptor that circles high over open country on rising air, scanning for prey. Often " +
                "perches on tall trees or rocks at field edges. Adults show a brick-red tail from above.",
                PlaceholderBody.Hawk);
            s.primaryColor = new Color(0.42f, 0.29f, 0.19f);
            s.secondaryColor = new Color(0.9f, 0.84f, 0.74f);
            s.accentColor = new Color(0.72f, 0.34f, 0.2f);
            s.habitat = HabitatPreference.Sky;
            s.locomotion = LocomotionType.Flying;
            s.groupCount = 2; s.groupSize = new Vector2Int(1, 1);
            s.spawnDistanceFromTrail = new Vector2(0f, 40f);
            s.preferredHotspots = HotspotType.Perch; s.hotspotAffinity = 0.8f;
            s.gait = GaitStyle.Fly;
            s.walkSpeed = 8f; s.runSpeed = 14f; s.turnSpeed = 45f; s.gaitFrequency = 3f;
            s.wanderRadius = 90f;
            // Awareness applies while perched; a flying hawk ignores the observer.
            Awareness(s, aware: 40f, alert: 26f, flee: 14f, gain: 0.9f, decay: 0.08f, patience: 3f, startle: 1f);
            s.photoSizeScale = 0.8f; s.clearPhotoDistance = 45f; s.observationTime = 1.5f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Flying, 3f, 20f, 40f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Perched, 3f, 15f, 30f));
            // Soars over the ridge only in fair weather (morning to golden hour); in rain, under grey skies or just after rain it sits on a snag.
            s.weatherHabits = Habits(RhythmPlace.Perch, false);
            s.weatherHabits.fairOnlyPeriods = WeatherHabits.PeriodMask(2, 3, 4);
            s.weatherHabits.foulPlace = RhythmPlace.Perch;
            Hint(s, "It rides the warm air over the ridge on fair days; when it is grey or wet it waits on a snag.");
            return s;
        }

        public static SpeciesDefinition Squirrel()
        {
            var s = New("eastern_gray_squirrel", "Eastern Gray Squirrel", "Sciurus carolinensis",
                "Energetic tree squirrel that forages in quick dashes across the ground, stopping often to sit up and " +
                "look around. Escapes up the nearest trunk and, unusually, descends head-first.",
                PlaceholderBody.Squirrel);
            s.primaryColor = new Color(0.5f, 0.49f, 0.47f);
            s.secondaryColor = new Color(0.88f, 0.87f, 0.84f);
            s.accentColor = new Color(0.45f, 0.33f, 0.24f);
            s.habitat = HabitatPreference.Trees;
            s.groupCount = 6; s.groupSize = new Vector2Int(1, 1);
            s.spawnDistanceFromTrail = new Vector2(4f, 20f);
            s.preferredHotspots = HotspotType.MatureTree; s.hotspotAffinity = 0.8f;
            s.gait = GaitStyle.Bursty;
            s.walkSpeed = 1.6f; s.runSpeed = 4.5f; s.turnSpeed = 360f; s.gaitFrequency = 4f;
            s.wanderRadius = 12f; s.behaviorChance = 0.8f; s.maxSlope = 50f;
            // Tolerant of a quiet observer; when pushed it shoots up the trunk and watches from there.
            Awareness(s, aware: 12f, alert: 7f, flee: 3.5f, gain: 0.9f, decay: 0.15f, patience: 2.5f, startle: 1.2f);
            s.fleeDistanceRange = new Vector2(5f, 8f);
            s.photoSizeScale = 0.6f; s.clearPhotoDistance = 10f; s.observationTime = 1f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Foraging, 4f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sitting, 2f, 2f, 4f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 1.5f, 1f, 3f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 1f, 2f, 3f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Climbing, 3f, 3f, 8f));
            return s;
        }
    }
}
