using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Scenic viewpoints (part of <see cref="VegetationBuilder"/>): composed places that invite the player to stop.
    ///   Meadow Overlook  a low timber platform with a rail where the trail passes closest to a meadow hotspot
    ///   Lookout          a bench at the highest point of the trail, facing back across the loop
    /// Trees, bushes and rocks inside each view cone are removed so the view actually exists. Each gets a
    /// <see cref="ScenicViewpoint"/> (used by the comparison-capture tool). Run after hotspots, before painting.
    /// </summary>
    public partial class VegetationBuilder
    {
        public int ViewpointsPlaced { get; private set; }

        public void BuildViewpoints(Transform parent, TrailBuilder.Loop loop)
        {
            var wood = GeneratedAssets.Material("Wood", new Color(0.52f, 0.39f, 0.26f));
            var woodDark = GeneratedAssets.Material("WoodDark", new Color(0.33f, 0.24f, 0.16f));
            float half = _trail.Width * 0.5f;
            Vector3? overlook = null;

            // Meadow Overlook: trail point nearest the meadow closest to the trail.
            if (_meadows.Count > 0)
            {
                Meadow best = _meadows[0];
                float bestDist = float.MaxValue;
                foreach (var m in _meadows)
                {
                    float d = _trail.DistanceToTrail(m.center);
                    if (d < bestDist) { bestDist = d; best = m; }
                }
                _trail.DistanceToTrail(best.center, out float along);
                Vector3 p = _trail.GetPoint(along);
                Vector3 toMeadow = TerrainUtil.Flat(best.center - p).normalized;
                Vector3 pos = TerrainUtil.Ground(p + toMeadow * (half + 1.8f));
                var platform = PropBuilder.Group("Meadow Overlook", parent, pos, Quaternion.LookRotation(toMeadow));
                PropBuilder.Box("Deck", platform, new Vector3(0f, 0.3f, 0f), new Vector3(2.8f, 0.12f, 2.2f), wood);
                foreach (var c in new[] { new Vector3(-1.3f, 0f, -1f), new Vector3(1.3f, 0f, -1f), new Vector3(-1.3f, 0f, 1f), new Vector3(1.3f, 0f, 1f) })
                    PropBuilder.Box("Post", platform, c + new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 1.1f, 0.12f), woodDark);
                PropBuilder.Box("Rail", platform, new Vector3(0f, 1.05f, 1.05f), new Vector3(2.8f, 0.08f, 0.08f), woodDark);
                PropBuilder.Box("Rail L", platform, new Vector3(-1.35f, 1.05f, 0f), new Vector3(0.08f, 0.08f, 2.2f), woodDark);
                PropBuilder.Box("Rail R", platform, new Vector3(1.35f, 1.05f, 0f), new Vector3(0.08f, 0.08f, 2.2f), woodDark);
                // Sign beside the steps, facing back toward the trail.
                var sign = PropBuilder.Group("Sign", platform, pos + platform.right * -1.9f - toMeadow * 0.9f, Quaternion.LookRotation(-toMeadow));
                PropBuilder.Box("Post", sign, new Vector3(0f, 0.45f, 0f), new Vector3(0.08f, 0.9f, 0.08f), woodDark);
                PropBuilder.Box("Board", sign, new Vector3(0f, 0.95f, 0f), new Vector3(0.9f, 0.45f, 0.06f), wood);
                PropBuilder.Label(sign, new Vector3(0f, 0.95f, 0.04f), "MEADOW OVERLOOK\nDeer graze here at dawn", 0.012f);
                PropBuilder.AddCollider(platform, new Vector3(0f, 0.3f, 0f), new Vector3(2.8f, 0.12f, 2.2f));

                float viewDist = TerrainUtil.FlatDistance(pos, best.center) + best.radius;
                ClearCone(pos, toMeadow, 22f, viewDist);
                AddViewpoint(parent, "Meadow Overlook", pos + Vector3.up * 1.95f, best.center + Vector3.up * 0.8f);
                overlook = pos;
            }

            // Lookout: highest trail point, away from the overlook, facing back across the loop.
            float bestHeight = float.MinValue, bestAlong = 0f;
            for (float a = 0f; a < _trail.Length; a += 2f)
            {
                Vector3 q = _trail.GetPoint(a);
                if (overlook.HasValue && TerrainUtil.FlatDistance(q, overlook.Value) < 40f) continue;
                float h = TerrainUtil.SampleHeight(q);
                if (h > bestHeight) { bestHeight = h; bestAlong = a; }
            }
            if (bestHeight > float.MinValue)
            {
                Vector3 p = _trail.GetPoint(bestAlong);
                Vector3 inward = TerrainUtil.Flat(loop.center - p).normalized;
                Vector3 pos = TerrainUtil.Ground(p - inward * (half + 1.4f));
                var bench = PropBuilder.Group("Lookout", parent, pos, Quaternion.LookRotation(inward));
                PropBuilder.Box("Seat", bench, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.45f), wood);
                PropBuilder.Box("Back", bench, new Vector3(0f, 0.8f, -0.22f), new Vector3(1.8f, 0.34f, 0.06f), wood);
                PropBuilder.Box("LegL", bench, new Vector3(-0.75f, 0.22f, 0f), new Vector3(0.09f, 0.45f, 0.42f), woodDark);
                PropBuilder.Box("LegR", bench, new Vector3(0.75f, 0.22f, 0f), new Vector3(0.09f, 0.45f, 0.42f), woodDark);
                PropBuilder.Box("Sign Post", bench, new Vector3(1.4f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.1f), woodDark);
                PropBuilder.Box("Sign", bench, new Vector3(1.4f, 1.15f, 0.06f), new Vector3(0.7f, 0.3f, 0.05f), wood);
                PropBuilder.Label(bench, new Vector3(1.4f, 1.15f, 0.09f), "LOOKOUT", 0.012f);
                PropBuilder.AddCollider(bench, new Vector3(0f, 0.45f, -0.02f), new Vector3(1.8f, 0.9f, 0.5f));

                ClearCone(pos, inward, 25f, 70f);
                Vector3 target = TerrainUtil.Ground(pos + inward * 60f) + Vector3.up * 2f;
                AddViewpoint(parent, "Lookout", pos + Vector3.up * 1.65f, target);
            }
        }

        private void AddViewpoint(Transform parent, string title, Vector3 eye, Vector3 lookAt)
        {
            var go = new GameObject("Viewpoint: " + title);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye));
            go.AddComponent<ScenicViewpoint>().title = title;
            ViewpointsPlaced++;
        }

        /// <summary>Removes trees, bushes and rocks inside a horizontal view cone (keeps the near trail-side ones out of frame).</summary>
        private void ClearCone(Vector3 origin, Vector3 direction, float halfAngle, float distance)
        {
            bool Inside(Vector3 p)
            {
                Vector3 to = TerrainUtil.Flat(p - origin);
                float d = to.magnitude;
                return d > 1.5f && d < distance && Vector3.Angle(direction, to) < halfAngle;
            }

            RemoveWhere(_treeObjects, Inside);
            RemoveWhere(_bushObjects, Inside);
            var rocks = new List<GameObject>();
            foreach (var f in SceneQuery.FindAll<EnvironmentFeature>())
                if (f.kind == FeatureKind.Rock && f.height < 1.2f && Inside(f.transform.position)) rocks.Add(f.gameObject);
            foreach (var r in rocks) Object.DestroyImmediate(r);
        }

        private static void RemoveWhere(List<GameObject> list, System.Func<Vector3, bool> inside)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var go = list[i];
                if (go == null) { list.RemoveAt(i); continue; }
                if (!inside(go.transform.position)) continue;
                Object.DestroyImmediate(go);
                list.RemoveAt(i);
            }
        }
    }
}
