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
            int logs = SeatLogs(root);
            if (fixedCount > 0 || logs > 0)
                Debug.Log($"[WildLens] Grounding: {fixedCount} posts and legs lengthened, {logs} seat logs bedded on slopes.");
            return fixedCount + logs;
        }

        /// <summary>
        /// 1.6: log seats (a box with no legs) were placed level from their centre, so on a slope one end floated (the
        /// Creek Ford's seat in the 1.5 screenshots). Each is tilted along its length to follow the ground (at most 12°,
        /// a log lies on a slope, it doesn't climb one) and bedded 6 cm in.
        /// </summary>
        private static int SeatLogs(Transform root)
        {
            int count = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var t = r.transform;
                if (t.name != "Seat Log") continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if (StandsOnSomething(r.bounds, root)) continue;
                Vector3 ext = Vector3.Scale(mf.sharedMesh.bounds.extents, t.lossyScale);
                Vector3 along = t.right;
                Vector3 c = t.TransformPoint(mf.sharedMesh.bounds.center);
                float g1 = TerrainUtil.SampleHeight(c - along * ext.x), g2 = TerrainUtil.SampleHeight(c + along * ext.x);
                float g0 = TerrainUtil.SampleHeight(c);
                float angle = Mathf.Clamp(Mathf.Atan2(g2 - g1, ext.x * 2f) * Mathf.Rad2Deg, -12f, 12f);
                float targetCentre = Mathf.Min(g0, (g1 + g2) * 0.5f) + ext.y - 0.06f;
                if (Mathf.Abs(angle) < 0.5f && Mathf.Abs(c.y - targetCentre) < 0.03f) continue;
                if (Mathf.Abs(c.y - targetCentre) > 1.2f) continue; // something else is going on: leave it
                t.RotateAround(c, t.forward, angle);
                t.position += Vector3.up * (targetCentre - c.y);
                count++;
            }
            return count;
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
