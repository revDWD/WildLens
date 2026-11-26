using UnityEngine;
using WildLens.Core;
using WildLens.Data;

namespace WildLens.Wildlife
{
    /// <summary>
    /// Builds readable low-poly placeholder bodies from primitives, with pivots placed so procedural
    /// animation looks right (necks bend at the shoulders, legs swing from the hips, wings fold at the shoulder).
    /// Proportions and colours follow the real species so they are identifiable at a distance.
    /// Units: metres, +Z forward, root at ground contact.
    /// </summary>
    public static class PlaceholderAnimalBuilder
    {
        private static readonly Color Black = new Color(0.05f, 0.05f, 0.05f);

        public static AnimalRig Build(SpeciesDefinition species, Transform root)
        {
            var rig = root.gameObject.AddComponent<AnimalRig>();
            rig.body = PrimitiveMeshes.CreatePivot("Model", root, Vector3.zero);
            switch (species.placeholderBody)
            {
                case PlaceholderBody.Deer: BuildDeer(species, rig); break;
                case PlaceholderBody.Rabbit: BuildRabbit(species, rig); break;
                case PlaceholderBody.Fox: BuildFox(species, rig); break;
                case PlaceholderBody.Hawk: BuildHawk(species, rig); break;
                case PlaceholderBody.Squirrel: BuildSquirrel(species, rig); break;
                case PlaceholderBody.Generic: BuildGeneric(species, rig); break;
            }
            return rig;
        }

        // ------------------------------------------------------------------ species

        private static void BuildDeer(SpeciesDefinition s, AnimalRig rig)
        {
            Color c = s.primaryColor, light = s.secondaryColor, dark = s.accentColor;
            var b = rig.body;
            // 0.6 silhouette pass: deep chest and rounded haunch, a thick neck carried forward (an upright thin neck
            // with long ears read as a llama), darker back, white throat, dark lower legs.
            Color back = Color.Lerp(c, dark, 0.35f);
            Part("Body", PrimitiveType.Sphere, b, new Vector3(0f, 1.0f, 0f), new Vector3(0.44f, 0.48f, 1.1f), c);
            Part("Back", PrimitiveType.Sphere, b, new Vector3(0f, 1.1f, -0.02f), new Vector3(0.36f, 0.3f, 1.0f), back);
            Part("Chest", PrimitiveType.Sphere, b, new Vector3(0f, 0.98f, 0.36f), new Vector3(0.42f, 0.56f, 0.5f), c);
            Part("Haunch", PrimitiveType.Sphere, b, new Vector3(0f, 1.04f, -0.38f), new Vector3(0.46f, 0.5f, 0.52f), c);
            Part("Belly", PrimitiveType.Sphere, b, new Vector3(0f, 0.88f, 0.02f), new Vector3(0.36f, 0.3f, 0.86f), light);

            rig.neck = Pivot("Neck", b, new Vector3(0f, 1.12f, 0.46f));
            Part("NeckMesh", PrimitiveType.Cylinder, rig.neck, new Vector3(0f, 0.17f, 0.12f), new Vector3(0.2f, 0.22f, 0.24f), c, new Vector3(38f, 0f, 0f));
            Part("Throat", PrimitiveType.Sphere, rig.neck, new Vector3(0f, 0.22f, 0.24f), new Vector3(0.13f, 0.14f, 0.1f), light);
            rig.head = Pivot("Head", rig.neck, new Vector3(0f, 0.38f, 0.28f));
            Part("Skull", PrimitiveType.Sphere, rig.head, new Vector3(0f, 0.02f, 0.06f), new Vector3(0.19f, 0.19f, 0.27f), c);
            Part("Muzzle", PrimitiveType.Sphere, rig.head, new Vector3(0f, -0.04f, 0.22f), new Vector3(0.11f, 0.11f, 0.17f), c);
            Part("Nose", PrimitiveType.Sphere, rig.head, new Vector3(0f, -0.03f, 0.3f), new Vector3(0.07f, 0.06f, 0.05f), dark);
            Part("Chin", PrimitiveType.Sphere, rig.head, new Vector3(0f, -0.09f, 0.18f), new Vector3(0.08f, 0.05f, 0.08f), light);
            Eyes(rig.head, 0.085f, 0.04f, 0.13f, 0.035f);
            rig.ears = new[]
            {
                // Broad, short, held out to the sides: the white-tailed deer's head shape.
                Ear(rig.head, "EarL", new Vector3(-0.08f, 0.09f, -0.02f), new Vector3(0.08f, 0.13f, 0.03f), c, 58f, 0.06f),
                Ear(rig.head, "EarR", new Vector3(0.08f, 0.09f, -0.02f), new Vector3(0.08f, 0.13f, 0.03f), c, -58f, 0.06f),
            };

            rig.legLength = 0.86f;
            rig.legs = new[]
            {
                Leg(b, "LegFL", new Vector3(-0.14f, 0.86f, 0.42f), 0.86f, 0.075f, c),
                Leg(b, "LegFR", new Vector3(0.14f, 0.86f, 0.42f), 0.86f, 0.075f, c),
                Leg(b, "LegBL", new Vector3(-0.15f, 0.88f, -0.42f), 0.88f, 0.09f, c),
                Leg(b, "LegBR", new Vector3(0.15f, 0.88f, -0.42f), 0.88f, 0.09f, c),
            };
            Color shin = Color.Lerp(c, dark, 0.55f);
            foreach (var leg in rig.legs)
            {
                float len = leg.name.StartsWith("LegF") ? 0.86f : 0.88f;
                Part(leg.name + "Shin", PrimitiveType.Cylinder, leg, new Vector3(0f, -len * 0.78f, 0f), new Vector3(0.062f, len * 0.2f, 0.062f), shin);
                Part(leg.name + "Hoof", PrimitiveType.Sphere, leg, new Vector3(0f, -len + 0.02f, 0.015f), new Vector3(0.07f, 0.05f, 0.09f), dark);
            }

            rig.tail = Pivot("Tail", b, new Vector3(0f, 1.12f, -0.6f));
            Part("TailTop", PrimitiveType.Sphere, rig.tail, new Vector3(0f, -0.06f, -0.04f), new Vector3(0.11f, 0.2f, 0.07f), c);
            Part("TailUnder", PrimitiveType.Sphere, rig.tail, new Vector3(0f, -0.06f, -0.075f), new Vector3(0.1f, 0.18f, 0.04f), light);
        }

        private static void BuildRabbit(SpeciesDefinition s, AnimalRig rig)
        {
            Color c = s.primaryColor, light = s.secondaryColor, dark = s.accentColor;
            var b = rig.body;
            Part("Body", PrimitiveType.Sphere, b, new Vector3(0f, 0.17f, 0.02f), new Vector3(0.22f, 0.22f, 0.32f), c);
            Part("Haunch", PrimitiveType.Sphere, b, new Vector3(0f, 0.15f, -0.08f), new Vector3(0.26f, 0.25f, 0.24f), c);

            rig.neck = Pivot("Neck", b, new Vector3(0f, 0.23f, 0.13f));
            rig.head = Pivot("Head", rig.neck, new Vector3(0f, 0.03f, 0.04f));
            Part("Skull", PrimitiveType.Sphere, rig.head, new Vector3(0f, 0f, 0.03f), new Vector3(0.14f, 0.13f, 0.16f), c);
            Part("Nose", PrimitiveType.Sphere, rig.head, new Vector3(0f, -0.015f, 0.105f), new Vector3(0.03f, 0.025f, 0.02f), dark);
            Eyes(rig.head, 0.055f, 0.02f, 0.06f, 0.025f);
            rig.ears = new[]
            {
                Ear(rig.head, "EarL", new Vector3(-0.03f, 0.05f, -0.01f), new Vector3(0.045f, 0.17f, 0.025f), c, 8f, 0.085f),
                Ear(rig.head, "EarR", new Vector3(0.03f, 0.05f, -0.01f), new Vector3(0.045f, 0.17f, 0.025f), c, -8f, 0.085f),
            };

            rig.legLength = 0.1f;
            rig.legs = new[]
            {
                Leg(b, "LegFL", new Vector3(-0.05f, 0.1f, 0.1f), 0.1f, 0.035f, c),
                Leg(b, "LegFR", new Vector3(0.05f, 0.1f, 0.1f), 0.1f, 0.035f, c),
                HindFoot(b, "LegBL", new Vector3(-0.08f, 0.09f, -0.09f), c),
                HindFoot(b, "LegBR", new Vector3(0.08f, 0.09f, -0.09f), c),
            };

            rig.tail = Pivot("Tail", b, new Vector3(0f, 0.19f, -0.2f));
            Part("Cottontail", PrimitiveType.Sphere, rig.tail, new Vector3(0f, 0f, -0.01f), Vector3.one * 0.075f, light);
        }

        private static void BuildFox(SpeciesDefinition s, AnimalRig rig)
        {
            Color c = s.primaryColor, light = s.secondaryColor, dark = s.accentColor;
            var b = rig.body;
            Part("Body", PrimitiveType.Sphere, b, new Vector3(0f, 0.38f, 0f), new Vector3(0.22f, 0.24f, 0.62f), c);
            Part("Chest", PrimitiveType.Sphere, b, new Vector3(0f, 0.34f, 0.2f), new Vector3(0.17f, 0.19f, 0.25f), light);

            rig.neck = Pivot("Neck", b, new Vector3(0f, 0.44f, 0.27f));
            rig.head = Pivot("Head", rig.neck, new Vector3(0f, 0.07f, 0.06f));
            Part("Skull", PrimitiveType.Sphere, rig.head, new Vector3(0f, 0f, 0.02f), new Vector3(0.17f, 0.15f, 0.17f), c);
            Part("Snout", PrimitiveType.Cube, rig.head, new Vector3(0f, -0.025f, 0.13f), new Vector3(0.065f, 0.055f, 0.13f), light);
            Part("Nose", PrimitiveType.Sphere, rig.head, new Vector3(0f, -0.01f, 0.2f), Vector3.one * 0.03f, Black);
            Eyes(rig.head, 0.05f, 0.02f, 0.085f, 0.025f);
            rig.ears = new[]
            {
                Ear(rig.head, "EarL", new Vector3(-0.05f, 0.06f, 0f), new Vector3(0.055f, 0.1f, 0.02f), dark, 14f, 0.045f, PrimitiveType.Cube),
                Ear(rig.head, "EarR", new Vector3(0.05f, 0.06f, 0f), new Vector3(0.055f, 0.1f, 0.02f), dark, -14f, 0.045f, PrimitiveType.Cube),
            };

            rig.legLength = 0.32f;
            rig.legs = new[]
            {
                Leg(b, "LegFL", new Vector3(-0.07f, 0.32f, 0.2f), 0.32f, 0.045f, dark),
                Leg(b, "LegFR", new Vector3(0.07f, 0.32f, 0.2f), 0.32f, 0.045f, dark),
                Leg(b, "LegBL", new Vector3(-0.07f, 0.33f, -0.2f), 0.33f, 0.05f, dark),
                Leg(b, "LegBR", new Vector3(0.07f, 0.33f, -0.2f), 0.33f, 0.05f, dark),
            };

            rig.tail = Pivot("Tail", b, new Vector3(0f, 0.41f, -0.29f), new Vector3(-12f, 0f, 0f));
            Part("Brush", PrimitiveType.Sphere, rig.tail, new Vector3(0f, 0f, -0.21f), new Vector3(0.12f, 0.12f, 0.44f), c);
            Part("TailTip", PrimitiveType.Sphere, rig.tail, new Vector3(0f, 0f, -0.42f), Vector3.one * 0.085f, light);
        }

        private static void BuildSquirrel(SpeciesDefinition s, AnimalRig rig)
        {
            Color c = s.primaryColor, light = s.secondaryColor, warm = s.accentColor;
            var b = rig.body;
            Part("Body", PrimitiveType.Sphere, b, new Vector3(0f, 0.1f, 0f), new Vector3(0.1f, 0.11f, 0.2f), c);
            Part("Belly", PrimitiveType.Sphere, b, new Vector3(0f, 0.085f, 0.02f), new Vector3(0.08f, 0.08f, 0.16f), light);

            rig.neck = Pivot("Neck", b, new Vector3(0f, 0.13f, 0.08f));
            rig.head = Pivot("Head", rig.neck, new Vector3(0f, 0.02f, 0.02f));
            Part("Skull", PrimitiveType.Sphere, rig.head, new Vector3(0f, 0f, 0.02f), new Vector3(0.075f, 0.075f, 0.09f), c);
            Part("Cheek", PrimitiveType.Sphere, rig.head, new Vector3(0f, -0.012f, 0.035f), new Vector3(0.06f, 0.05f, 0.06f), warm);
            Eyes(rig.head, 0.032f, 0.012f, 0.04f, 0.018f);
            rig.ears = new[]
            {
                Ear(rig.head, "EarL", new Vector3(-0.022f, 0.03f, 0f), new Vector3(0.02f, 0.035f, 0.01f), c, 10f, 0.016f),
                Ear(rig.head, "EarR", new Vector3(0.022f, 0.03f, 0f), new Vector3(0.02f, 0.035f, 0.01f), c, -10f, 0.016f),
            };

            rig.legLength = 0.07f;
            rig.legs = new[]
            {
                Leg(b, "LegFL", new Vector3(-0.035f, 0.07f, 0.06f), 0.07f, 0.022f, c),
                Leg(b, "LegFR", new Vector3(0.035f, 0.07f, 0.06f), 0.07f, 0.022f, c),
                Leg(b, "LegBL", new Vector3(-0.04f, 0.07f, -0.06f), 0.07f, 0.03f, c),
                Leg(b, "LegBR", new Vector3(0.04f, 0.07f, -0.06f), 0.07f, 0.03f, c),
            };

            // Big S-curved tail held up over the back.
            rig.tail = Pivot("Tail", b, new Vector3(0f, 0.12f, -0.09f));
            Part("TailBase", PrimitiveType.Sphere, rig.tail, new Vector3(0f, 0.06f, -0.05f), new Vector3(0.075f, 0.17f, 0.075f), c, new Vector3(-25f, 0f, 0f));
            Part("TailPlume", PrimitiveType.Sphere, rig.tail, new Vector3(0f, 0.17f, -0.04f), new Vector3(0.09f, 0.12f, 0.09f), c);
            Part("TailFringe", PrimitiveType.Sphere, rig.tail, new Vector3(0f, 0.17f, -0.055f), new Vector3(0.1f, 0.1f, 0.06f), light);
        }

        private static void BuildHawk(SpeciesDefinition s, AnimalRig rig)
        {
            Color c = s.primaryColor, light = s.secondaryColor, rufous = s.accentColor;
            Color beak = new Color(0.25f, 0.23f, 0.2f), feet = new Color(0.85f, 0.72f, 0.25f);
            var b = rig.body;
            Part("Body", PrimitiveType.Sphere, b, new Vector3(0f, 0.17f, 0f), new Vector3(0.2f, 0.2f, 0.42f), c);
            Part("Breast", PrimitiveType.Sphere, b, new Vector3(0f, 0.15f, 0.07f), new Vector3(0.17f, 0.17f, 0.25f), light);

            rig.neck = Pivot("Neck", b, new Vector3(0f, 0.21f, 0.18f));
            rig.head = Pivot("Head", rig.neck, new Vector3(0f, 0.02f, 0.03f));
            Part("Skull", PrimitiveType.Sphere, rig.head, Vector3.zero, new Vector3(0.12f, 0.12f, 0.13f), c);
            Part("Beak", PrimitiveType.Cube, rig.head, new Vector3(0f, -0.015f, 0.075f), new Vector3(0.03f, 0.035f, 0.05f), beak, new Vector3(20f, 0f, 0f));
            Eyes(rig.head, 0.045f, 0.015f, 0.04f, 0.022f);

            rig.wings = new[] { Wing(b, "WingL", -1f, c), Wing(b, "WingR", 1f, c) };

            rig.tail = Pivot("Tail", b, new Vector3(0f, 0.17f, -0.2f));
            Part("TailFan", PrimitiveType.Cube, rig.tail, new Vector3(0f, 0f, -0.11f), new Vector3(0.16f, 0.02f, 0.22f), rufous);

            rig.legLength = 0.08f;
            rig.legs = new[]
            {
                Leg(b, "LegL", new Vector3(-0.04f, 0.08f, 0.02f), 0.08f, 0.025f, feet),
                Leg(b, "LegR", new Vector3(0.04f, 0.08f, 0.02f), 0.08f, 0.025f, feet),
            };
        }

        // ------------------------------------------------------------------ generic (1.0 expansion framework)

        /// <summary>A placeholder body from a species' <see cref="BodyPlan"/>: new species need data, not code.</summary>
        private static void BuildGeneric(SpeciesDefinition s, AnimalRig rig)
        {
            var p = s.bodyPlan ?? new BodyPlan();
            switch (p.archetype)
            {
                case BodyArchetype.Quadruped: GenericQuadruped(s, p, rig); break;
                case BodyArchetype.WadingBird: GenericBird(s, p, rig, wading: true); break;
                case BodyArchetype.GroundBird: GenericBird(s, p, rig, wading: false); break;
                case BodyArchetype.PerchingBird:
                    BuildHawk(s, rig); // flight-capable body with wings, scaled to size
                    float k = Mathf.Max(0.1f, p.length / 0.42f);
                    rig.body.localScale = Vector3.one * k;
                    rig.legLength *= k;
                    break;
            }
        }

        private static void GenericQuadruped(SpeciesDefinition s, BodyPlan p, AnimalRig rig)
        {
            Color c = s.primaryColor, light = s.secondaryColor, dark = s.accentColor;
            var b = rig.body;
            float leg = p.legLength, bodyH = Mathf.Max(0.08f, p.height - leg), y = leg + bodyH * 0.5f, L = p.length;
            Part("Body", PrimitiveType.Sphere, b, new Vector3(0f, y, 0f), new Vector3(bodyH * 1.05f, bodyH, L * 0.75f), c);
            Part("Haunch", PrimitiveType.Sphere, b, new Vector3(0f, y + bodyH * 0.05f, -L * 0.18f), new Vector3(bodyH * 1.15f, bodyH * 1.05f, L * 0.42f), c);
            Part("Belly", PrimitiveType.Sphere, b, new Vector3(0f, y - bodyH * 0.18f, 0.02f), new Vector3(bodyH * 0.85f, bodyH * 0.6f, L * 0.55f), light);

            rig.neck = Pivot("Neck", b, new Vector3(0f, y + bodyH * 0.25f, L * 0.33f));
            rig.head = Pivot("Head", rig.neck, new Vector3(0f, p.neckLength, p.neckLength + p.headSize * 0.3f));
            float hs = p.headSize;
            Part("Skull", PrimitiveType.Sphere, rig.head, Vector3.zero, new Vector3(hs * 1.1f, hs * 0.95f, hs), c);
            Part("Snout", PrimitiveType.Sphere, rig.head, new Vector3(0f, -hs * 0.15f, hs * 0.45f + p.snoutLength * 0.4f), new Vector3(hs * 0.5f, hs * 0.42f, p.snoutLength + hs * 0.2f), light);
            Part("Nose", PrimitiveType.Sphere, rig.head, new Vector3(0f, -hs * 0.12f, hs * 0.5f + p.snoutLength * 0.9f), Vector3.one * hs * 0.18f, Black);
            if (p.faceMask) Part("Mask", PrimitiveType.Sphere, rig.head, new Vector3(0f, hs * 0.08f, hs * 0.3f), new Vector3(hs * 1.12f, hs * 0.3f, hs * 0.5f), dark);
            Eyes(rig.head, hs * 0.3f, hs * 0.1f, hs * 0.45f, hs * 0.15f);
            float e = p.earSize;
            rig.ears = new[]
            {
                Ear(rig.head, "EarL", new Vector3(-hs * 0.32f, hs * 0.38f, -hs * 0.05f), new Vector3(e, e * 1.2f, e * 0.4f), c, 20f, e * 0.4f),
                Ear(rig.head, "EarR", new Vector3(hs * 0.32f, hs * 0.38f, -hs * 0.05f), new Vector3(e, e * 1.2f, e * 0.4f), c, -20f, e * 0.4f),
            };

            rig.legLength = leg;
            float lx = bodyH * 0.32f, lz = L * 0.26f, lt = Mathf.Max(0.03f, bodyH * 0.28f);
            rig.legs = new[]
            {
                Leg(b, "LegFL", new Vector3(-lx, leg, lz), leg, lt, dark),
                Leg(b, "LegFR", new Vector3(lx, leg, lz), leg, lt, dark),
                Leg(b, "LegBL", new Vector3(-lx, leg, -lz), leg, lt * 1.1f, dark),
                Leg(b, "LegBR", new Vector3(lx, leg, -lz), leg, lt * 1.1f, dark),
            };

            if (p.tailLength > 0.01f)
            {
                rig.tail = Pivot("Tail", b, new Vector3(0f, y + bodyH * 0.15f, -L * 0.4f), new Vector3(-15f, 0f, 0f));
                Part("TailMesh", PrimitiveType.Sphere, rig.tail, new Vector3(0f, 0f, -p.tailLength * 0.5f), new Vector3(p.tailThickness, p.tailThickness, p.tailLength), c);
                if (p.tailRings)
                    for (int i = 1; i <= 3; i++)
                        Part("Ring", PrimitiveType.Sphere, rig.tail, new Vector3(0f, 0f, -p.tailLength * (0.22f + i * 0.2f)),
                            new Vector3(p.tailThickness * 1.06f, p.tailThickness * 1.06f, p.tailLength * 0.08f), dark);
            }
        }

        private static void GenericBird(SpeciesDefinition s, BodyPlan p, AnimalRig rig, bool wading)
        {
            Color c = s.primaryColor, light = s.secondaryColor, dark = s.accentColor;
            Color beak = wading ? new Color(0.85f, 0.7f, 0.25f) : new Color(0.35f, 0.3f, 0.25f), feet = wading ? new Color(0.35f, 0.32f, 0.25f) : new Color(0.55f, 0.4f, 0.35f);
            var b = rig.body;
            float leg = p.legLength, bodyH = Mathf.Max(0.08f, p.height - leg), y = leg + bodyH * 0.5f, L = p.length;
            Part("Body", PrimitiveType.Sphere, b, new Vector3(0f, y, 0f), new Vector3(bodyH * 0.95f, bodyH, L * 0.6f), c, new Vector3(wading ? -12f : -5f, 0f, 0f));
            Part("Breast", PrimitiveType.Sphere, b, new Vector3(0f, y - bodyH * 0.1f, L * 0.15f), new Vector3(bodyH * 0.8f, bodyH * 0.85f, L * 0.35f), light);
            Part("WingL", PrimitiveType.Sphere, b, new Vector3(-bodyH * 0.42f, y + bodyH * 0.05f, -L * 0.05f), new Vector3(bodyH * 0.2f, bodyH * 0.7f, L * 0.55f), Color.Lerp(c, dark, 0.3f));
            Part("WingR", PrimitiveType.Sphere, b, new Vector3(bodyH * 0.42f, y + bodyH * 0.05f, -L * 0.05f), new Vector3(bodyH * 0.2f, bodyH * 0.7f, L * 0.55f), Color.Lerp(c, dark, 0.3f));

            rig.neck = Pivot("Neck", b, new Vector3(0f, y + bodyH * 0.3f, L * 0.25f));
            float nl = p.neckLength;
            if (wading)
            {
                // The heron's S-curved neck: forward, up, then forward again.
                Part("NeckLow", PrimitiveType.Cylinder, rig.neck, new Vector3(0f, nl * 0.25f, nl * 0.12f), new Vector3(0.05f, nl * 0.28f, 0.05f), light, new Vector3(30f, 0f, 0f));
                Part("NeckHigh", PrimitiveType.Cylinder, rig.neck, new Vector3(0f, nl * 0.72f, nl * 0.15f), new Vector3(0.045f, nl * 0.25f, 0.045f), light, new Vector3(-20f, 0f, 0f));
            }
            else Part("NeckMesh", PrimitiveType.Cylinder, rig.neck, new Vector3(0f, nl * 0.5f, 0f), new Vector3(bodyH * 0.25f, nl * 0.5f, bodyH * 0.25f), c);
            rig.head = Pivot("Head", rig.neck, new Vector3(0f, nl, nl * 0.25f));
            float hs = p.headSize;
            Part("Skull", PrimitiveType.Sphere, rig.head, Vector3.zero, new Vector3(hs, hs, hs * 1.2f), wading ? light : c);
            if (wading) Part("Crown", PrimitiveType.Sphere, rig.head, new Vector3(0f, hs * 0.3f, -hs * 0.2f), new Vector3(hs * 0.7f, hs * 0.35f, hs * 1.3f), dark);
            Part("Beak", PrimitiveType.Cube, rig.head, new Vector3(0f, -hs * 0.05f, hs * 0.55f + p.snoutLength * 0.5f), new Vector3(hs * 0.25f, hs * 0.22f, p.snoutLength), beak);
            Eyes(rig.head, hs * 0.38f, hs * 0.1f, hs * 0.2f, hs * 0.18f);

            rig.legLength = leg;
            rig.legs = new[]
            {
                Leg(b, "LegL", new Vector3(-bodyH * 0.18f, leg, 0f), leg, Mathf.Max(0.015f, leg * 0.04f), feet),
                Leg(b, "LegR", new Vector3(bodyH * 0.18f, leg, 0f), leg, Mathf.Max(0.015f, leg * 0.04f), feet),
            };
            if (p.tailLength > 0.01f)
            {
                rig.tail = Pivot("Tail", b, new Vector3(0f, y, -L * 0.3f), new Vector3(wading ? 20f : -35f, 0f, 0f));
                Part("TailFan", PrimitiveType.Cube, rig.tail, new Vector3(0f, 0f, -p.tailLength * 0.5f), new Vector3(wading ? bodyH * 0.6f : bodyH * 1.6f, 0.03f, p.tailLength), dark);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale,
            Color color, Vector3 euler = default)
        {
            var go = PrimitiveMeshes.CreatePart(name, type, parent, pos, scale, MaterialLibrary.Get(color), euler);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        private static Transform Pivot(string name, Transform parent, Vector3 pos, Vector3 euler = default) =>
            PrimitiveMeshes.CreatePivot(name, parent, pos, euler);

        private static Transform Leg(Transform parent, string name, Vector3 hip, float length, float thickness, Color color)
        {
            var pivot = Pivot(name, parent, hip);
            Part(name + "Mesh", PrimitiveType.Cylinder, pivot, new Vector3(0f, -length * 0.5f, 0f),
                new Vector3(thickness, length * 0.5f, thickness), color);
            return pivot;
        }

        private static Transform HindFoot(Transform parent, string name, Vector3 hip, Color color)
        {
            var pivot = Pivot(name, parent, hip);
            Part(name + "Thigh", PrimitiveType.Sphere, pivot, new Vector3(0f, -0.03f, 0f), new Vector3(0.06f, 0.09f, 0.1f), color);
            Part(name + "Foot", PrimitiveType.Cube, pivot, new Vector3(0f, -0.08f, 0.04f), new Vector3(0.045f, 0.025f, 0.13f), color);
            return pivot;
        }

        private static Transform Ear(Transform head, string name, Vector3 pos, Vector3 scale, Color color, float tilt,
            float lift, PrimitiveType type = PrimitiveType.Sphere)
        {
            var pivot = Pivot(name, head, pos, new Vector3(0f, 0f, tilt));
            Part(name + "Mesh", type, pivot, new Vector3(0f, lift, 0f), scale, color);
            return pivot;
        }

        private static void Eyes(Transform head, float x, float y, float z, float size)
        {
            Part("EyeL", PrimitiveType.Sphere, head, new Vector3(-x, y, z), Vector3.one * size, Black);
            Part("EyeR", PrimitiveType.Sphere, head, new Vector3(x, y, z), Vector3.one * size, Black);
        }

        private static Transform Wing(Transform body, string name, float side, Color color)
        {
            var pivot = Pivot(name, body, new Vector3(0.08f * side, 0.2f, 0.04f));
            // Broad inner wing plus a narrower "fingered" outer section.
            Part(name + "Inner", PrimitiveType.Cube, pivot, new Vector3(0.28f * side, 0f, 0f), new Vector3(0.56f, 0.025f, 0.26f), color);
            Part(name + "Outer", PrimitiveType.Cube, pivot, new Vector3(0.66f * side, 0f, -0.02f), new Vector3(0.24f, 0.02f, 0.2f),
                color * 0.75f + new Color(0f, 0f, 0f, 0.25f));
            return pivot;
        }
    }
}
