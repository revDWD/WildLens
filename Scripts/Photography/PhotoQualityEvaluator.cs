using UnityEngine;

namespace WildLens.Photography
{
    public enum PhotoQuality { Poor, Good, Excellent }

    /// <summary>Raw measurements of one subject in a photograph (produced by <see cref="PhotoAnalyzer"/>).</summary>
    public struct SubjectMeasurements
    {
        /// <summary>Fraction (0-1) of sample points across the body that are in frame and unobstructed.</summary>
        public float visibility;
        /// <summary>Fraction of the frame covered by the subject's screen-space bounds.</summary>
        public float frameCoverage;
        /// <summary>Frame coverage relative to the species' ideal full-frame coverage (1 = ideal).</summary>
        public float sizeRatio;
        /// <summary>Distance of the subject's centre from frame centre: 0 = centred, 1 = at the frame edge.</summary>
        public float centerOffset;
        /// <summary>Camera-to-subject distance in metres.</summary>
        public float distance;
        /// <summary>Distance relative to the species' clear-photo distance (≤1 = crisp; beyond, haze softens detail).</summary>
        public float distanceRatio;
    }

    /// <summary>
    /// Classifies a photographed subject as Poor, Good or Excellent from four observable factors.
    /// The rules are deliberately explicit so players can learn them:
    ///
    ///   Excellent  visibility ≥ 85%, size ≥ 55% of ideal, centred (offset ≤ 0.22), within clear distance
    ///   Good       visibility ≥ 50%, size ≥ 22% of ideal, offset ≤ 0.45,          within 1.8× clear distance
    ///   Poor       anything else that was still identifiable
    ///
    /// For anything below Excellent, <see cref="Classify"/> also returns a short hint naming the first factor
    /// that held it back. There is no player-facing score; <see cref="Rating"/> exists only so the journal can
    /// keep the better of two photos of the same quality.
    /// </summary>
    public static class PhotoQualityEvaluator
    {
        private struct Tier
        {
            public float visibility, size, offset, distance;
        }

        private static readonly Tier ExcellentTier = new Tier { visibility = 0.85f, size = 0.55f, offset = 0.22f, distance = 1f };
        private static readonly Tier GoodTier = new Tier { visibility = 0.5f, size = 0.22f, offset = 0.45f, distance = 1.8f };

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

        /// <summary>Internal 0-1 ranking used to pick a species' best photo. Never shown to the player.</summary>
        public static float Rating(in SubjectMeasurements m)
        {
            float size = Mathf.Clamp01(m.sizeRatio);
            float center = 1f - Mathf.Clamp01(m.centerOffset);
            float clarity = 1f - Mathf.Clamp01((m.distanceRatio - 1f) / 1.5f);
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
            if (m.visibility < t.visibility) { hint = "partly hidden"; return false; }
            if (m.distanceRatio > t.distance) { hint = "too far away, get closer"; return false; }
            if (m.sizeRatio < t.size) { hint = "small in frame, zoom in"; return false; }
            if (m.centerOffset > t.offset) { hint = "off-centre"; return false; }
            hint = "";
            return true;
        }
    }
}
