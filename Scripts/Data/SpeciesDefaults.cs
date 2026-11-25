using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Data
{
    /// <summary>
    /// Tuned default values for the five vertical-slice species. The editor builder writes these out as assets,
    /// and the game falls back to them at runtime if no database asset is assigned. Tune the assets, not this file.
    /// </summary>
    public static class SpeciesDefaults
    {
        public static List<SpeciesDefinition> CreateAll()
        {
            return new List<SpeciesDefinition> { Deer(), Rabbit(), Fox(), Hawk(), Squirrel() };
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
            return s;
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
            s.groupCount = 3; s.groupSize = new Vector2Int(1, 3);
            s.spawnDistanceFromTrail = new Vector2(14f, 45f);
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 1.1f; s.runSpeed = 9f; s.turnSpeed = 80f; s.gaitFrequency = 1.4f;
            s.wanderRadius = 30f; s.behaviorChance = 0.8f;
            s.alertDistance = 26f; s.fleeDistance = 13f; s.fleeDistanceRange = new Vector2(25f, 40f);
            s.idealFrameCoverage = 0.14f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grazing, 5f, 6f, 14f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2f, 3f, 6f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 1f, 3f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Resting, 0.6f, 12f, 25f));
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
            s.groupCount = 6; s.groupSize = new Vector2Int(1, 1);
            s.spawnDistanceFromTrail = new Vector2(5f, 22f);
            s.gait = GaitStyle.Hop;
            s.walkSpeed = 0.9f; s.runSpeed = 7f; s.turnSpeed = 220f; s.gaitFrequency = 2.2f;
            s.wanderRadius = 12f; s.behaviorChance = 0.85f;
            s.alertDistance = 12f; s.fleeDistance = 5.5f; s.fleeDistanceRange = new Vector2(8f, 15f);
            s.idealFrameCoverage = 0.07f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grazing, 4f, 3f, 8f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sniffing, 2f, 1.5f, 3f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 1.5f, 2f, 4f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2f, 1.5f, 4f));
            return s;
        }

        public static SpeciesDefinition Fox()
        {
            var s = New("red_fox", "Red Fox", "Vulpes vulpes",
                "Solitary, curious hunter of field margins. Trots with a light, purposeful gait and often pauses to " +
                "listen for voles under the grass before pouncing. The white-tipped tail is a reliable field mark.",
                PlaceholderBody.Fox);
            s.primaryColor = new Color(0.78f, 0.36f, 0.12f);
            s.secondaryColor = new Color(0.95f, 0.93f, 0.9f);
            s.accentColor = new Color(0.14f, 0.1f, 0.08f);
            s.habitat = HabitatPreference.ForestEdge;
            s.groupCount = 2; s.groupSize = new Vector2Int(1, 1);
            s.spawnDistanceFromTrail = new Vector2(10f, 35f);
            s.gait = GaitStyle.Walk;
            s.walkSpeed = 1.5f; s.runSpeed = 8f; s.turnSpeed = 120f; s.gaitFrequency = 2.4f;
            s.wanderRadius = 55f; s.behaviorChance = 0.6f;
            s.alertDistance = 22f; s.fleeDistance = 11f; s.fleeDistanceRange = new Vector2(20f, 35f);
            s.idealFrameCoverage = 0.08f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Sniffing, 3f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2f, 2f, 5f));
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
            s.gait = GaitStyle.Fly;
            s.walkSpeed = 8f; s.runSpeed = 14f; s.turnSpeed = 45f; s.gaitFrequency = 3f;
            s.wanderRadius = 90f;
            s.alertDistance = 20f; s.fleeDistance = 12f;
            s.idealFrameCoverage = 0.05f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Soaring, 3f, 20f, 40f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Perched, 1.5f, 10f, 22f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Gliding, 1f, 8f, 15f));
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
            s.gait = GaitStyle.Bursty;
            s.walkSpeed = 1.6f; s.runSpeed = 4.5f; s.turnSpeed = 360f; s.gaitFrequency = 4f;
            s.wanderRadius = 12f; s.behaviorChance = 0.8f; s.maxSlope = 50f;
            s.alertDistance = 9f; s.fleeDistance = 4.5f; s.fleeDistanceRange = new Vector2(6f, 10f);
            s.idealFrameCoverage = 0.05f;
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Foraging, 4f, 2f, 5f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.LookingAround, 2f, 1f, 3f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Grooming, 1f, 2f, 3f));
            s.behaviors.Add(new BehaviorOption(AnimalActivity.Climbing, 3f, 3f, 8f));
            return s;
        }
    }
}
