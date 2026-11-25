using System.Collections.Generic;
using UnityEngine;
using WildLens.Data;
using WildLens.Journal;

namespace WildLens.Photography
{
    /// <summary>
    /// Decides what is in a photograph and how good the shot is. For each candidate subject:
    ///  - in front of the camera and within range,
    ///  - visible: several points across its body are inside the frame and not occluded (line-of-sight rays),
    ///  - scored on frame coverage vs. the species' ideal, visibility, centring, and facing.
    /// Returns the best-scoring individual per species.
    /// </summary>
    public static class PhotoAnalyzer
    {
        public const float MinVisibility = 0.3f;
        /// <summary>Minimum frame coverage, as a fraction of the species' ideal, needed to identify a subject.</summary>
        public const float MinCoverageFraction = 0.02f;

        // Sample points in "extents space": centre plus six points on the principal axes.
        private static readonly Vector3[] SampleOffsets =
        {
            Vector3.zero,
            new Vector3(0.6f, 0f, 0f), new Vector3(-0.6f, 0f, 0f),
            new Vector3(0f, 0.6f, 0f), new Vector3(0f, -0.6f, 0f),
            new Vector3(0f, 0f, 0.6f), new Vector3(0f, 0f, -0.6f),
        };

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        public static List<PhotoSubject> Analyze(Camera cam, float maxDistance, Transform ignoreRoot)
        {
            Physics.SyncTransforms();
            var best = new Dictionary<string, PhotoSubject>();
            Vector3 camPos = cam.transform.position;

            foreach (var target in Photographable.Active)
            {
                if (target == null || target.Species == null) continue;
                if (!target.TryGetWorldBounds(out Bounds b)) continue;

                float distance = Vector3.Distance(camPos, b.center);
                if (distance > maxDistance) continue;
                Vector3 vc = cam.WorldToViewportPoint(b.center);
                if (vc.z <= 0f) continue;

                float coverage = FrameCoverage(cam, b);
                // Too small in frame to identify (a few pixels): the player needs to get closer or zoom.
                if (coverage < target.Species.idealFrameCoverage * MinCoverageFraction) continue;

                int visible = 0;
                foreach (var o in SampleOffsets)
                {
                    Vector3 point = b.center + Vector3.Scale(b.extents, o);
                    Vector3 v = cam.WorldToViewportPoint(point);
                    if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
                    if (HasLineOfSight(camPos, point, target, ignoreRoot)) visible++;
                }
                float visibility = visible / (float)SampleOffsets.Length;
                if (visibility < MinVisibility) continue;

                float centering = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(vc.x, vc.y), new Vector2(0.5f, 0.5f)) / 0.5f);
                Vector3 toCam = (camPos - target.transform.position).normalized;
                float dot = Vector3.Dot(target.transform.forward, toCam);
                float facing = dot >= 0f ? Mathf.Lerp(0.75f, 1f, dot) : Mathf.Lerp(0.75f, 0.3f, -dot); // side-on is a good field shot
                float size = Mathf.Sqrt(Mathf.Clamp01(coverage / target.Species.idealFrameCoverage));

                // Size dominates; composition terms count fully only once the subject is reasonably large.
                float composition = 0.2f * visibility + 0.15f * centering + 0.15f * facing;
                int score = Mathf.RoundToInt(100f * (0.5f * size + composition * (0.4f + 0.6f * size)));
                var subject = new PhotoSubject
                {
                    speciesId = target.Species.speciesId,
                    score = Mathf.Clamp(score, 1, 100),
                    quality = QualityLabel(score),
                    activity = target.CurrentActivity.ToString(),
                    distance = distance,
                    frameCoverage = coverage,
                    visibility = visibility,
                };

                if (!best.TryGetValue(subject.speciesId, out var existing) || subject.score > existing.score)
                    best[subject.speciesId] = subject;
            }

            var list = new List<PhotoSubject>(best.Values);
            list.Sort((a, c) => c.score.CompareTo(a.score));
            return list;
        }

        public static string QualityLabel(int score)
        {
            if (score >= 85) return "Excellent";
            if (score >= 65) return "Good";
            if (score >= 40) return "Fair";
            return "Distant";
        }

        /// <summary>Fraction of the frame covered by the subject's screen-space bounding rectangle (clipped to frame).</summary>
        private static float FrameCoverage(Camera cam, Bounds b)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 v = cam.WorldToViewportPoint(corner);
                if (v.z <= 0f) continue;
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
            if (minX > maxX) return 0f;
            float w = Mathf.Clamp01(maxX) - Mathf.Clamp01(minX);
            float h = Mathf.Clamp01(maxY) - Mathf.Clamp01(minY);
            return Mathf.Max(0f, w) * Mathf.Max(0f, h);
        }

        private static bool HasLineOfSight(Vector3 from, Vector3 to, Photographable target, Transform ignoreRoot)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.001f) return true;
            int count = Physics.RaycastNonAlloc(from, d / len, HitBuffer, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            float nearest = float.MaxValue;
            Collider nearestCollider = null;
            for (int i = 0; i < count; i++)
            {
                var col = HitBuffer[i].collider;
                if (col == null) continue;
                if (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot)) continue;
                if (HitBuffer[i].distance < nearest)
                {
                    nearest = HitBuffer[i].distance;
                    nearestCollider = col;
                }
            }
            // Nothing in the way, or the first thing hit is the subject itself.
            return nearestCollider == null || target.Owns(nearestCollider);
        }

        public static string DescribeActivity(string activityName)
        {
            return System.Enum.TryParse(activityName, out AnimalActivity a) ? AnimalActivityText.Describe(a) : activityName;
        }
    }
}
