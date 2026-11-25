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
