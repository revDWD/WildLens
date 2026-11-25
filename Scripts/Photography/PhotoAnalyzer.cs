using System.Collections.Generic;
using UnityEngine;
using WildLens.Data;
using WildLens.Journal;

namespace WildLens.Photography
{
    /// <summary>Why a subject in view didn't count (shown when a photo identifies nothing).</summary>
    public enum MissReason { None, TooSmall, Hidden, Cropped }

    /// <summary>Result of analysing one frame.</summary>
    public class PhotoAnalysis
    {
        public readonly List<PhotoSubject> subjects = new List<PhotoSubject>();
        /// <summary>When nothing was identified: the most helpful reason among animals that were in frame.</summary>
        public MissReason nearMiss = MissReason.None;
    }

    /// <summary>
    /// Works out what is in a photograph. <see cref="TryMeasure"/> handles one subject:
    ///  - in front of the camera, inside the frame and within range,
    ///  - frame fill (zoom-aware: measured from the current projection),
    ///  - visibility: sample points across the body that are in frame and not blocked (line-of-sight rays),
    ///  - centring and distance.
    /// <see cref="Analyze"/> runs it on every subject for a photo; <see cref="FramingAssistant"/> runs it on
    /// one subject at 10 Hz for the viewfinder. Classification lives in <see cref="PhotoQualityEvaluator"/>.
    /// </summary>
    public static class PhotoAnalyzer
    {
        // Sample points in "extents space", kept well inside the body's bounds so they land on the animal.
        private static readonly Vector3[] SampleOffsets =
        {
            Vector3.zero,
            new Vector3(0.45f, 0f, 0f), new Vector3(-0.45f, 0f, 0f),
            new Vector3(0f, 0.45f, 0f), new Vector3(0f, -0.35f, 0f),
            new Vector3(0f, 0f, 0.45f), new Vector3(0f, 0f, -0.45f),
            new Vector3(0f, 0.3f, 0.3f), new Vector3(0f, 0.3f, -0.3f),
        };

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        /// <summary>
        /// Measures one subject. Returns false if it isn't in frame at all. <paramref name="screenRect"/> is the
        /// subject's viewport-space rectangle (0-1), clipped to the frame.
        /// </summary>
        public static bool TryMeasure(Camera cam, Photographable target, float maxDistance, Transform ignoreRoot,
            bool checkVisibility, out SubjectMeasurements m, out Rect screenRect)
        {
            m = default;
            screenRect = default;
            if (target == null || target.Species == null || !target.TryGetWorldBounds(out Bounds b)) return false;

            Vector3 camPos = cam.transform.position;
            float distance = Vector3.Distance(camPos, b.center);
            if (distance > maxDistance) return false;
            Vector3 vc = cam.WorldToViewportPoint(b.center);
            if (vc.z <= 0f) return false;

            if (!ProjectBounds(cam, b, out Rect raw)) return false;
            screenRect = Rect.MinMaxRect(Mathf.Clamp01(raw.xMin), Mathf.Clamp01(raw.yMin), Mathf.Clamp01(raw.xMax), Mathf.Clamp01(raw.yMax));
            if (screenRect.width <= 0f || screenRect.height <= 0f) return false;

            m.frameFill = Mathf.Max(raw.width, raw.height); // viewport units: 1 = full frame width/height
            m.sizeScale = target.Species.photoSizeScale;
            m.centerOffset = Mathf.Max(Mathf.Abs(vc.x - 0.5f), Mathf.Abs(vc.y - 0.5f)) / 0.5f;
            m.distance = distance;
            m.distanceRatio = distance / Mathf.Max(1f, target.Species.clearPhotoDistance);
            m.visibility = checkVisibility ? Visibility(cam, b, target, ignoreRoot) : 1f;
            return true;
        }

        public static PhotoAnalysis Analyze(Camera cam, float maxDistance, Transform ignoreRoot)
        {
            Physics.SyncTransforms();
            var result = new PhotoAnalysis();
            var best = new Dictionary<string, PhotoSubject>();

            foreach (var target in Photographable.Active)
            {
                if (!TryMeasure(cam, target, maxDistance, ignoreRoot, true, out var m, out _)) continue;

                if (!PhotoQualityEvaluator.IsIdentifiable(m))
                {
                    result.nearMiss = MoreHelpful(result.nearMiss,
                        m.frameFill > 1f ? MissReason.Cropped :
                        m.NormalizedFill < PhotoQualityEvaluator.IdentifyFill ? MissReason.TooSmall : MissReason.Hidden);
                    continue;
                }

                var quality = PhotoQualityEvaluator.Classify(m, out string hint);
                var subject = new PhotoSubject
                {
                    speciesId = target.Species.speciesId,
                    quality = PhotoQualityEvaluator.Label(quality),
                    hint = hint,
                    rating = PhotoQualityEvaluator.Rating(m),
                    activity = target.CurrentActivity.ToString(),
                    distance = m.distance,
                    frameCoverage = m.frameFill,
                    visibility = m.visibility,
                };

                if (!best.TryGetValue(subject.speciesId, out var existing) || IsBetter(subject, existing))
                    best[subject.speciesId] = subject;
            }

            result.subjects.AddRange(best.Values);
            result.subjects.Sort((a, c) => IsBetter(a, c) ? -1 : IsBetter(c, a) ? 1 : 0);
            return result;
        }

        private static bool IsBetter(PhotoSubject a, PhotoSubject b)
        {
            var qa = PhotoQualityEvaluator.Parse(a.quality);
            var qb = PhotoQualityEvaluator.Parse(b.quality);
            return qa != qb ? qa > qb : a.rating > b.rating;
        }

        // Prefer the reason the player can act on most directly.
        private static MissReason MoreHelpful(MissReason current, MissReason candidate)
        {
            if (current == MissReason.None) return candidate;
            if (candidate == MissReason.TooSmall || current == MissReason.TooSmall) return MissReason.TooSmall;
            return current;
        }

        public static string Describe(MissReason reason)
        {
            switch (reason)
            {
                case MissReason.TooSmall: return "Too small to identify: zoom in";
                case MissReason.Hidden: return "View blocked: find a clearer line";
                case MissReason.Cropped: return "Too close: zoom out";
                default: return "";
            }
        }

        private static float Visibility(Camera cam, Bounds b, Photographable target, Transform ignoreRoot)
        {
            Vector3 camPos = cam.transform.position;
            int visible = 0;
            foreach (var o in SampleOffsets)
            {
                Vector3 point = b.center + Vector3.Scale(b.extents, o);
                Vector3 v = cam.WorldToViewportPoint(point);
                if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
                if (HasLineOfSight(camPos, point, target, ignoreRoot)) visible++;
            }
            return visible / (float)SampleOffsets.Length;
        }

        /// <summary>Viewport-space rectangle of the bounds (unclipped).</summary>
        private static bool ProjectBounds(Camera cam, Bounds b, out Rect rect)
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
            rect = minX > maxX ? default : Rect.MinMaxRect(minX, minY, maxX, maxY);
            return minX <= maxX;
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
