using System.Collections.Generic;
using UnityEngine;
using WildLens.Data;

namespace WildLens.Photography
{
    /// <summary>One small subject in a macro frame.</summary>
    public struct MacroHit
    {
        public MacroSubject source;
        public NaturalistSubject subject;
        public float distance, fill;
        public bool sharp, centred;
        public PhotoQuality quality;
        /// <summary>What held it back ("soft focus", "too small", "off-centre"); empty for Excellent.</summary>
        public string hint;
    }

    /// <summary>
    /// Macro grading (1.5 groundwork), the close-range counterpart of <see cref="PhotoAnalyzer"/>. Judged the way a
    /// macro photographer would: frame fill (the subject's largest side against the frame), focus (at a few tens of
    /// centimetres depth of field is millimetres, so focus distance must match the subject's within ~12%), and
    /// composition (near the centre). Occluded subjects don't count. Reach: 2.5 m (a macro lens works close).
    /// </summary>
    public static class MacroAnalyzer
    {
        public const float MaxDistance = 2.5f;
        private const float SharpTolerance = 0.12f;

        /// <summary>The subjects in frame, best first.</summary>
        public static List<MacroHit> Analyze(Camera cam, float focusDistance)
        {
            var hits = new List<MacroHit>();
            if (cam == null) return hits;
            Vector3 eye = cam.transform.position;
            foreach (var m in MacroSubject.Active)
            {
                if (m == null || !m.IsShowing || !m.TryGetBounds(out Bounds b)) continue;
                float dist = Vector3.Distance(eye, b.center);
                if (dist > MaxDistance) continue;
                if (!Project(cam, b, out Rect r)) continue;
                if (Occluded(eye, b.center, dist, m)) continue;
                float fill = Mathf.Max(r.width, r.height);
                if (fill < 0.06f) continue; // a speck: not a photo of it
                var hit = new MacroHit
                {
                    source = m,
                    subject = m.subject,
                    distance = dist,
                    fill = Mathf.Min(1f, fill),
                    sharp = Mathf.Abs(focusDistance - dist) / Mathf.Max(0.05f, dist) < SharpTolerance,
                    centred = Vector2.Distance(r.center, new Vector2(0.5f, 0.5f)) < 0.22f,
                };
                Vector2 need = m.subject.fillForGrade;
                if (hit.sharp && hit.fill >= need.y && hit.centred) { hit.quality = PhotoQuality.Excellent; hit.hint = ""; }
                else if (hit.sharp && hit.fill >= need.x) { hit.quality = PhotoQuality.Good; hit.hint = hit.centred ? "fill more of the frame" : "off-centre"; }
                else { hit.quality = PhotoQuality.Poor; hit.hint = !hit.sharp ? "soft focus" : "too small in the frame"; }
                hits.Add(hit);
            }
            hits.Sort((a, c) => c.quality != a.quality ? c.quality.CompareTo(a.quality) : c.fill.CompareTo(a.fill));
            return hits;
        }

        /// <summary>The subject nearest the frame centre within reach (macro autofocus target), or null.</summary>
        public static MacroSubject Centred(Camera cam, out float distance)
        {
            distance = 0f;
            MacroSubject best = null;
            float bestOff = 0.3f;
            if (cam == null) return null;
            foreach (var m in MacroSubject.Active)
            {
                if (m == null || !m.IsShowing || !m.TryGetBounds(out Bounds b)) continue;
                float d = Vector3.Distance(cam.transform.position, b.center);
                if (d > MaxDistance) continue;
                Vector3 v = cam.WorldToViewportPoint(b.center);
                if (v.z <= 0f) continue;
                float off = Vector2.Distance(new Vector2(v.x, v.y), new Vector2(0.5f, 0.5f));
                if (off < bestOff) { bestOff = off; best = m; distance = d; }
            }
            return best;
        }

        private static bool Project(Camera cam, Bounds b, out Rect r)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 v = cam.WorldToViewportPoint(c);
                if (v.z <= 0f) { r = default; return false; }
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            if (max.x < 0f || max.y < 0f || min.x > 1f || min.y > 1f) { r = default; return false; }
            min = Vector2.Max(min, Vector2.zero);
            max = Vector2.Min(max, Vector2.one);
            r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return r.width > 0f && r.height > 0f;
        }

        private static bool Occluded(Vector3 eye, Vector3 target, float dist, MacroSubject m)
        {
            Vector3 dir = (target - eye) / Mathf.Max(0.001f, dist);
            if (!Physics.Raycast(eye, dir, out var hit, dist - 0.04f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.transform.IsChildOf(m.transform)) return false;
            var cc = hit.collider as CharacterController;
            return cc == null; // the photographer's own capsule never blocks
        }
    }
}
