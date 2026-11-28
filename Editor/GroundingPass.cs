using UnityEngine;
using WildLens.Core;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Grounding pass (1.5), run at the end of every build. Props are placed from one point (their centre), so on a slope
    /// a two-post sign, a panel's legs, a bench or a deck corner hangs in the air on the low side (1.4 playtest: "signs
    /// float"). Every upright support (posts and legs, by name) is lengthened downwards until it reaches the terrain under
    /// its own footprint and is sunk a little into the ground, the way a real post is set. Supports standing on a deck or
    /// bridge (something solid just below them) are left alone, as are the ones already touching the ground.
    /// </summary>
    public static class GroundingPass
    {
        private static readonly string[] SupportNames = { "Post", "Leg L", "Leg R", "LegL", "LegR", "Leg", "Sign Post", "Stand Post" };
        private const float Sink = 0.18f;      // metres set into the ground
        private const float MaxExtend = 3.5f;  // longer than this is a design problem, not a slope

        public static int Run(Transform root)
        {
            int fixedCount = 0;
            Physics.SyncTransforms(); // colliders created during the build must be visible to the deck test below
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var t = r.transform;
                if (System.Array.IndexOf(SupportNames, t.name) < 0) continue;
                if (Vector3.Dot(t.up, Vector3.up) < 0.97f) continue; // only upright supports
                Bounds b = r.bounds;
                float bottom = b.min.y;
                // Lowest ground under the support's footprint (its four corners and centre).
                float ground = float.MaxValue;
                foreach (var o in new[] { Vector3.zero, new Vector3(b.extents.x, 0f, b.extents.z), new Vector3(-b.extents.x, 0f, b.extents.z),
                                          new Vector3(b.extents.x, 0f, -b.extents.z), new Vector3(-b.extents.x, 0f, -b.extents.z) })
                    ground = Mathf.Min(ground, TerrainUtil.SampleHeight(b.center + o));
                float target = ground - Sink;
                float extend = bottom - target;
                if (extend <= 0.02f || extend > MaxExtend) continue;
                if (StandsOnSomething(b, root)) continue;
                float height = b.size.y;
                if (height <= 0.001f) continue;
                float k = (height + extend) / height;
                Vector3 s = t.localScale;
                t.localScale = new Vector3(s.x, s.y * k, s.z);
                t.position += Vector3.down * (extend * 0.5f);
                fixedCount++;
            }
            if (fixedCount > 0) Debug.Log($"[WildLens] Grounding: {fixedCount} posts and legs lengthened to reach the ground on slopes.");
            return fixedCount;
        }

        /// <summary>A deck, bridge or other built surface directly under the support (it is meant to stand there).</summary>
        private static bool StandsOnSomething(Bounds b, Transform root)
        {
            var hits = Physics.RaycastAll(new Vector3(b.center.x, b.min.y + 0.05f, b.center.z), Vector3.down, 0.6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider is TerrainCollider) continue;
                if (h.collider.transform.IsChildOf(root)) return true;
            }
            return false;
        }
    }
}
