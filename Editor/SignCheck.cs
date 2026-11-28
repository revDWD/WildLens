using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WildLens.EditorTools
{
    /// <summary>
    /// WildLens ▸ Check Sign Lettering (1.4): a probe for "the sign exists but I can't read it". For every TextMesh in
    /// the slice it finds the board it is painted on (Board, Panel, Finger, Plate, Header Plank) and measures the
    /// lettering's generated glyph mesh in the board's own space. A label is reported when any glyph lies off the board
    /// face, when it sits behind the face (unreadable from the front), or when it is too small to read from 6 m.
    /// Runs automatically a moment after each build (once glyph meshes exist) and from the menu at any time.
    /// Built after 1.3's signs came out mirrored, which the code could not see and the screenshots showed at once.
    /// </summary>
    public static class SignCheck
    {
        private static readonly string[] BoardNames = { "Board", "Panel", "Finger", "Plate", "Header Plank" };
        private const float MinCapHeight = 0.03f; // metres: about the smallest capital readable at 5 m

        [MenuItem("WildLens/Check Sign Lettering", priority = 23)]
        public static void Run() => Run(true);

        /// <summary>Called by the builder after a build: waits for the glyph meshes, then checks quietly unless something is wrong.</summary>
        public static void RunAfterBuild()
        {
            int frames = 0;
            void Tick()
            {
                if (++frames < 3) { EditorApplication.delayCall += Tick; return; }
                Run(false);
            }
            EditorApplication.delayCall += Tick;
        }

        public static void Run(bool verbose)
        {
            var root = WildLensSliceBuilder.FindExistingRoot();
            if (root == null) { if (verbose) Debug.LogWarning("[WildLens] Sign check: no slice in this scene."); return; }
            int checkedCount = 0, unmeasured = 0;
            var problems = new List<string>();
            foreach (var tm in root.GetComponentsInChildren<TextMesh>(true))
            {
                if (string.IsNullOrEmpty(tm.text)) continue;
                var mf = tm.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                var board = FindBoard(tm.transform);
                if (board == null) continue; // trail-marker numbers on posts and the like: nothing to overflow
                checkedCount++;
                if (mesh == null || mesh.vertexCount == 0) { unmeasured++; continue; }

                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                foreach (var v in mesh.vertices)
                {
                    Vector3 b = board.InverseTransformPoint(tm.transform.TransformPoint(v));
                    min = Vector3.Min(min, b);
                    max = Vector3.Max(max, b);
                }
                Vector3 size = board.lossyScale;
                const float slack = 0.02f; // fraction of the board: glyph boxes include a little padding
                string where = Path(tm.transform, root.transform);
                string text = tm.text.Replace("\n", " / ");
                if (min.x < -0.5f - slack || max.x > 0.5f + slack || min.y < -0.5f - slack || max.y > 0.5f + slack)
                {
                    float overX = Mathf.Max(-0.5f - min.x, max.x - 0.5f) * size.x, overY = Mathf.Max(-0.5f - min.y, max.y - 0.5f) * size.y;
                    problems.Add($"off the board by {Mathf.Max(overX, overY) * 100f:0} cm: \"{text}\" ({where})");
                }
                // Lettering must stand proud of a face (front +Z or, for two-sided signs, back -Z), not inside the board.
                float eps = 0.002f / Mathf.Max(0.001f, size.z);
                if (min.z > -0.5f + eps && max.z < 0.5f - eps)
                    problems.Add($"buried inside the board: \"{text}\" ({where})");
                // TextMesh reads from its -Z side: that side must face away from the board.
                Vector3 readable = -tm.transform.forward;
                Vector3 outward = board.forward * (max.z + min.z >= 0f ? 1f : -1f);
                if (Vector3.Dot(readable, outward) < 0.5f)
                    problems.Add($"faces into the board (mirrored or hidden): \"{text}\" ({where})");
                float cap = (max.y - min.y) * size.y / Mathf.Max(1, tm.text.Split('\n').Length) * 0.7f;
                if (cap < MinCapHeight)
                    problems.Add($"small ({cap * 100f:0.0} cm capitals): \"{text}\" ({where})");
            }

            var sb = new StringBuilder();
            sb.Append($"[WildLens] Sign check: {checkedCount} labels on boards, {problems.Count} problem(s)");
            if (unmeasured > 0) sb.Append($", {unmeasured} not measured yet (look at the scene once, then run WildLens ▸ Check Sign Lettering)");
            sb.Append('.');
            foreach (var p in problems) sb.Append("\n  • ").Append(p);
            if (problems.Count > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        /// <summary>The board this label is painted on: the nearest board-named box among its siblings and up two levels.</summary>
        private static Transform FindBoard(Transform label)
        {
            Transform best = null;
            float bestD = float.MaxValue;
            var t = label.parent;
            for (int level = 0; level < 3 && t != null; level++, t = t.parent)
            {
                foreach (Transform child in t)
                {
                    if (child == label || System.Array.IndexOf(BoardNames, child.name) < 0 || child.GetComponent<MeshRenderer>() == null) continue;
                    float d = Vector3.Distance(child.position, label.position);
                    if (d < bestD) { bestD = d; best = child; }
                }
                if (best != null) return best;
            }
            return null;
        }

        private static string Path(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var p = t.parent; p != null && p != root; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join(" › ", parts);
        }
    }
}
