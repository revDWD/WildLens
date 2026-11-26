using UnityEngine;
using WildLens.Core;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>Trail furniture: trailhead kiosk and bike, numbered marker posts and benches facing the meadow.</summary>
    public static class PropBuilder
    {
        private static Material _wood, _woodDark, _markerCap;

        public struct Footprint { public Vector3 position; public float radius; }

        /// <summary>Places the builder must keep clear of scenery (kiosk, bike parking).</summary>
        public static readonly System.Collections.Generic.List<Footprint> Footprints = new System.Collections.Generic.List<Footprint>();
        /// <summary>Where the bike is parked at the trailhead: the Reserve Road starts here (0.9).</summary>
        public static Vector3 BikeParking;

        public static void Build(TrailPath trail, TrailBuilder.Loop loop, Transform parent, bool trailBike = true)
        {
            _wood = GeneratedAssets.Material("Wood", new Color(0.52f, 0.39f, 0.26f));
            _woodDark = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
            _markerCap = GeneratedAssets.Material("MarkerCap", new Color(0.85f, 0.55f, 0.15f), 0.3f);

            float half = trail.Width * 0.5f;

            Footprints.Clear();
            BikeParking = Vector3.zero;
            Vector3 spawn = trail.GetPoint(0f);

            // Trailhead kiosk: a roofed park notice board with the live reserve board (ReserveInfoBoard), angled to
            // face the player's start so it's the first thing you see.
            {
                Vector3 p = trail.GetPoint(7f);
                Vector3 outward = Outward(p, loop);
                Vector3 pos = TerrainUtil.Ground(p + outward * (half + 2.9f));
                Vector3 face = TerrainUtil.Flat(spawn - pos).normalized;
                BuildKiosk(parent, pos, Quaternion.LookRotation(face));
                Footprints.Add(new Footprint { position = pos, radius = 3.5f });
            }

            // Trail bike parked just past the kiosk, pointing along the trail, ready to go.
            if (trailBike)
            {
                Vector3 p = trail.GetPoint(12f), dir = trail.GetDirection(12f);
                Vector3 pos = TerrainUtil.Ground(p + Outward(p, loop) * (half + 1.7f));
                var bike = new GameObject("Trail Bike");
                bike.transform.SetParent(parent, false);
                bike.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
                var model = new GameObject("Model").transform;
                model.SetParent(bike.transform, false);
                WildLens.Vehicles.TrailBikeModel.Build(model, (name, c, sm) => GeneratedAssets.Material(name, c, sm));
                bike.AddComponent<WildLens.Vehicles.TrailBike>();
                Footprints.Add(new Footprint { position = pos, radius = 2.5f });
                BikeParking = pos;
            }

            // Numbered marker posts.
            for (int k = 1; k < trail.MarkerCount; k++)
            {
                float d = k * trail.MarkerSpacing;
                Vector3 p = trail.GetPoint(d), dir = trail.GetDirection(d);
                Vector3 pos = TerrainUtil.Ground(p - Outward(p, loop) * (half + 0.6f));
                var post = Group("Trail Marker " + k, parent, pos, Quaternion.LookRotation(-dir));
                Box("Post", post, new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 1f, 0.14f), _woodDark);
                Box("Cap", post, new Vector3(0f, 1.02f, 0f), new Vector3(0.18f, 0.06f, 0.18f), _markerCap);
                Label(post, new Vector3(0f, 0.82f, 0.075f), k.ToString(), 0.03f);
                AddCollider(post, new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 1f, 0.14f));
            }

            // Benches on the outer edge, looking across the trail into the meadow.
            foreach (float f in new[] { 0.33f, 0.68f })
            {
                float d = trail.Length * f;
                Vector3 p = trail.GetPoint(d);
                Vector3 inward = -Outward(p, loop);
                Vector3 pos = TerrainUtil.Ground(p - inward * (half + 1.3f));
                var bench = Group("Bench", parent, pos, Quaternion.LookRotation(inward));
                Box("Seat", bench, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.07f, 0.42f), _wood);
                Box("Back", bench, new Vector3(0f, 0.78f, -0.2f), new Vector3(1.6f, 0.32f, 0.06f), _wood);
                Box("LegL", bench, new Vector3(-0.65f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.4f), _woodDark);
                Box("LegR", bench, new Vector3(0.65f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.4f), _woodDark);
                AddCollider(bench, new Vector3(0f, 0.45f, -0.02f), new Vector3(1.6f, 0.9f, 0.45f));
            }
        }

        /// <summary>
        /// Two log posts, a shingled gable roof, a framed backboard and a carved header plank. The board face is a
        /// <see cref="ReserveInfoBoard"/> that fills itself in at runtime.
        /// </summary>
        private static void BuildKiosk(Transform parent, Vector3 pos, Quaternion rot)
        {
            var roofMat = GeneratedAssets.Material("KioskRoof", new Color(0.24f, 0.27f, 0.22f), 0.15f);
            var kiosk = Group("Trailhead Kiosk", parent, pos, rot);
            const float W = 2.8f, boardY = 1.66f, boardH = 1.86f; // 0.9: taller, for the "now" line and best times
            foreach (float x in new[] { -W * 0.5f - 0.05f, W * 0.5f + 0.05f })
                PrimitiveMeshes.CreatePart("Post", PrimitiveType.Cylinder, kiosk, new Vector3(x, 1.45f, 0f), new Vector3(0.2f, 1.45f, 0.2f), _woodDark);
            Box("Backboard", kiosk, new Vector3(0f, boardY, -0.02f), new Vector3(W, boardH, 0.08f), _wood);
            Box("Frame Top", kiosk, new Vector3(0f, boardY + boardH * 0.5f, 0.03f), new Vector3(W + 0.06f, 0.07f, 0.07f), _woodDark);
            Box("Frame Bottom", kiosk, new Vector3(0f, boardY - boardH * 0.5f, 0.03f), new Vector3(W + 0.06f, 0.07f, 0.07f), _woodDark);
            Box("Ledge", kiosk, new Vector3(0f, boardY - boardH * 0.5f - 0.02f, 0.12f), new Vector3(W, 0.04f, 0.22f), _woodDark);
            // Gable roof: two pitched slabs meeting at a ridge over the board.
            foreach (float side in new[] { -1f, 1f })
            {
                var slab = PrimitiveMeshes.CreatePart("Roof", PrimitiveType.Cube, kiosk, new Vector3(0f, 2.86f, side * 0.33f),
                    new Vector3(W + 0.7f, 0.06f, 0.8f), roofMat, new Vector3(side * 26f, 0f, 0f));
                slab.name = side < 0f ? "Roof Back" : "Roof Front";
            }
            Box("Ridge", kiosk, new Vector3(0f, 3.04f, 0f), new Vector3(W + 0.72f, 0.08f, 0.1f), _woodDark);
            Box("Header Plank", kiosk, new Vector3(0f, 2.74f, 0.06f), new Vector3(1.9f, 0.24f, 0.05f), SignBoard);
            Label(kiosk, new Vector3(0f, 2.74f, 0.09f), "MEADOW LOOP TRAILHEAD", 0.012f, SignText);

            var face = new GameObject("Board Face");
            face.transform.SetParent(kiosk, false);
            face.transform.localPosition = new Vector3(0f, boardY, 0.025f);
            face.AddComponent<ReserveInfoBoard>().size = new Vector2(W - 0.16f, boardH - 0.12f);

            AddCollider(kiosk, new Vector3(0f, 1.5f, 0f), new Vector3(W + 0.3f, 3f, 0.3f));

            // Reserve guidelines: a small post beside the kiosk. Quiet evidence that this place is looked after.
            var rules = Group("Reserve Guidelines", parent, kiosk.TransformPoint(new Vector3(W * 0.5f + 1.3f, 0f, 0.3f)), kiosk.rotation * Quaternion.Euler(0f, -15f, 0f));
            rules.position = TerrainUtil.Ground(rules.position);
            Box("Post", rules, new Vector3(0f, 0.65f, 0f), new Vector3(0.1f, 1.3f, 0.1f), _woodDark);
            Box("Board", rules, new Vector3(0f, 1.2f, 0.06f), new Vector3(0.95f, 0.7f, 0.05f), SignBoard);
            Box("Rule", rules, new Vector3(0f, 1.48f, 0.087f), new Vector3(0.87f, 0.012f, 0.005f), SignTrim);
            Label(rules, new Vector3(0f, 1.18f, 0.09f),
                "PLEASE\nKeep to trails & footpaths\nRide slowly on the Reserve Road\nGive wildlife space\nLeave nothing but footprints", 0.0075f, SignText);
            AddCollider(rules, new Vector3(0f, 0.65f, 0f), new Vector3(0.12f, 1.3f, 0.12f));
        }

        private static Vector3 Outward(Vector3 p, TrailBuilder.Loop loop)
        {
            Vector3 o = TerrainUtil.Flat(p - loop.center);
            return o.sqrMagnitude > 0.001f ? o.normalized : Vector3.right;
        }

        internal static Transform Group(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            return go.transform;
        }

        internal static void Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            PrimitiveMeshes.CreatePart(name, PrimitiveType.Cube, parent, localPos, size, mat);
        }

        internal static void AddCollider(Transform t, Vector3 center, Vector3 size)
        {
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        /// <summary>Reserve sign standard (0.9): stained board, cream lettering, green rule.</summary>
        internal static Material SignBoard => GeneratedAssets.Material("SignBoard", new Color(0.24f, 0.16f, 0.1f), 0.2f);
        internal static Material SignTrim => GeneratedAssets.Material("SignTrim", new Color(0.24f, 0.36f, 0.22f), 0.3f);
        internal static readonly Color SignText = new Color(0.93f, 0.88f, 0.72f);

        internal static void Label(Transform parent, Vector3 localPos, string text, float charSize) =>
            Label(parent, localPos, text, charSize, new Color(0.12f, 0.1f, 0.07f));

        internal static void Label(Transform parent, Vector3 localPos, string text, float charSize, Color colour)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // TextMesh reads on its -Z side; flip so it faces the same way as the board front (+Z).
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = UI.UIFactory.DefaultFont;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = colour;
            var mr = go.GetComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
        }
    }
}
