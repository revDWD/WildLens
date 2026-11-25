using UnityEngine;

namespace WildLens.Photography
{
    public enum PhotoQuality { Poor, Good, Excellent }

    /// <summary>Raw measurements of one subject in a photograph (produced by <see cref="PhotoAnalyzer"/>).</summary>
    public struct SubjectMeasurements
    {
        /// <summary>Fraction (0-1) of sample points across the body that are in frame and unobstructed.</summary>
        public float visibility;
        /// <summary>
        /// How much of the frame the subject spans: the larger of (subject height / frame height) and
        /// (subject width / frame width). 0.1 = a tenth of the frame; above 1 the subject is cropped.
        /// Linear, so doubling the zoom doubles it: the measure photographers use by eye.
        /// </summary>
        public float frameFill;
        /// <summary>Species multiplier on the fill thresholds (small animals need less).</summary>
        public float sizeScale;
        /// <summary>Offset of the subject's centre from frame centre, per axis: 0 = centred, 0.33 = on a third, 1 = at the edge.</summary>
        public float centerOffset;
        /// <summary>Camera-to-subject distance in metres.</summary>
        public float distance;
        /// <summary>Distance relative to the species' clear-photo distance (≤1 = crisp; beyond, haze softens detail).</summary>
        public float distanceRatio;

        /// <summary>Frame fill normalised by species: compare against the evaluator's thresholds.</summary>
        public float NormalizedFill => frameFill / Mathf.Max(0.01f, sizeScale);
    }

    /// <summary>
    /// Classifies a photographed subject as Poor, Good or Excellent from four observable factors.
    /// The rules are explicit so players can learn them (fill values are × the species' <c>photoSizeScale</c>):
    ///
    ///   Identified  fill ≥ 2.5% of frame, visibility ≥ 30%              (any zoom, from across a meadow)
    ///   Good        fill ≥ 10%,  visibility ≥ 45%, centre offset ≤ 0.75, within 2.5× clear distance
    ///   Excellent   fill ≥ 25%,  visibility ≥ 75%, on or inside the thirds (≤ 0.4), within clear distance
    ///   Poor        identified but not Good
    ///
    /// Zoom and distance both act through fill, so zooming in is always the fix for "small in frame", while
    /// the clear distance means the very best photos still reward a careful approach. A cropped subject
    /// (fill above ~1) loses visibility and gets "too close, zoom out".
    ///
    /// For anything below Excellent, <see cref="Classify"/> returns a short hint naming the first factor that
    /// held it back. There is no player-facing score; <see cref="Rating"/> only breaks ties between photos of
    /// the same class when the journal picks a species' best.
    /// </summary>
    public static class PhotoQualityEvaluator
    {
        public const float IdentifyFill = 0.025f;
        public const float IdentifyVisibility = 0.3f;

        private struct Tier
        {
            public float fill, visibility, offset, distance;
        }

        private static readonly Tier ExcellentTier = new Tier { fill = 0.25f, visibility = 0.75f, offset = 0.4f, distance = 1f };
        private static readonly Tier GoodTier = new Tier { fill = 0.10f, visibility = 0.45f, offset = 0.75f, distance = 2.5f };

        public static bool IsIdentifiable(in SubjectMeasurements m) =>
            m.NormalizedFill >= IdentifyFill && m.visibility >= IdentifyVisibility;

        public static PhotoQuality Classify(in SubjectMeasurements m, out string hint)
        {
            if (Meets(m, ExcellentTier, out _)) { hint = ""; return PhotoQuality.Excellent; }
            if (Meets(m, GoodTier, out _))
            {
                Meets(m, ExcellentTier, out hint);
                return PhotoQuality.Good;
            }
            Meets(m, GoodTier, out hint);
            return PhotoQuality.Poor;
        }

        /// <summary>Classification without the hint (live framing preview).</summary>
        public static PhotoQuality Classify(in SubjectMeasurements m) => Classify(m, out _);

        /// <summary>Internal 0-1 ranking used to pick a species' best photo. Never shown to the player.</summary>
        public static float Rating(in SubjectMeasurements m)
        {
            float fill = m.NormalizedFill;
            float size = fill <= 0.45f ? Mathf.Clamp01(fill / 0.45f) : Mathf.Clamp01(1f - (fill - 0.9f) * 2f);
            float center = 1f - Mathf.Clamp01(Mathf.Abs(m.centerOffset - 0.2f) / 0.8f); // slightly off-centre is ideal
            float clarity = 1f - Mathf.Clamp01((m.distanceRatio - 0.5f) / 2f);
            return (m.visibility + size + center + clarity) * 0.25f;
        }

        public static string Label(PhotoQuality q) => q.ToString();

        /// <summary>Maps labels from older saves ("Fair", "Distant") onto the current classes.</summary>
        public static PhotoQuality Parse(string label)
        {
            if (System.Enum.TryParse(label, out PhotoQuality q)) return q;
            return PhotoQuality.Poor;
        }

        // Checks factors in the order a photographer would fix them; reports the first failure.
        private static bool Meets(in SubjectMeasurements m, Tier t, out string hint)
        {
            float fill = m.NormalizedFill;
            if (m.frameFill > 1f && m.visibility < t.visibility) { hint = "too close, zoom out"; return false; }
            if (m.visibility < t.visibility) { hint = "partly hidden"; return false; }
            if (fill < t.fill) { hint = "small in frame, zoom in"; return false; }
            if (m.distanceRatio > t.distance) { hint = "too far for fine detail, get closer"; return false; }
            if (m.centerOffset > t.offset) { hint = "off-centre"; return false; }
            hint = "";
            return true;
        }
    }
}
