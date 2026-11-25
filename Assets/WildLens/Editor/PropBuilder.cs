using UnityEngine;
using WildLens.Core;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>Trail furniture: trailhead sign, numbered marker posts and benches facing the meadow.</summary>
    public static class PropBuilder
    {
        private static Material _wood, _woodDark, _markerCap;

        public static void Build(TrailPath trail, TrailBuilder.Loop loop, Transform parent)
        {
            _wood = GeneratedAssets.Material("Wood", new Color(0.52f, 0.39f, 0.26f));
            _woodDark = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
            _markerCap = GeneratedAssets.Material("MarkerCap", new Color(0.85f, 0.55f, 0.15f), 0.3f);

            float half = trail.Width * 0.5f;

            // Trailhead sign beside the start, facing back along the trail toward the player's spawn.
            {
                Vector3 p = trail.GetPoint(6f), dir = trail.GetDirection(6f);
                Vector3 outward = Outward(p, loop);
                Vector3 pos = TerrainUtil.Ground(p + outward * (half + 1.6f));
                var sign = Group("Trailhead Sign", parent, pos, Quaternion.LookRotation(-dir));
                Box("PostL", sign, new Vector3(-0.75f, 0.9f, 0f), new Vector3(0.12f, 1.8f, 0.12f), _woodDark);
                Box("PostR", sign, new Vector3(0.75f, 0.9f, 0f), new Vector3(0.12f, 1.8f, 0.12f), _woodDark);
                Box("Board", sign, new Vector3(0f, 1.45f, 0f), new Vector3(1.7f, 0.8f, 0.07f), _wood);
                Label(sign, new Vector3(0f, 1.45f, 0.045f),
                    "WILDLENS NATURE RESERVE\nMeadow Loop Trail\n" + Mathf.RoundToInt(trail.Length) + " m loop", 0.016f);
                AddCollider(sign, new Vector3(0f, 0.9f, 0f), new Vector3(1.7f, 1.8f, 0.2f));
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

        private static Vector3 Outward(Vector3 p, TrailBuilder.Loop loop)
        {
            Vector3 o = TerrainUtil.Flat(p - loop.center);
            return o.sqrMagnitude > 0.001f ? o.normalized : Vector3.right;
        }

        private static Transform Group(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            return go.transform;
        }

        private static void Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            PrimitiveMeshes.CreatePart(name, PrimitiveType.Cube, parent, localPos, size, mat);
        }

        private static void AddCollider(Transform t, Vector3 center, Vector3 size)
        {
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        private static void Label(Transform parent, Vector3 localPos, string text, float charSize)
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
            tm.color = new Color(0.12f, 0.1f, 0.07f);
            var mr = go.GetComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
        }
    }
}
