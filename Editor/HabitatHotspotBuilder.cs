using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Habitat hotspot placement (part of <see cref="VegetationBuilder"/> so it can reuse its forest field,
    /// random source, occupancy grid and materials). Every hotspot has a visible landscape cue, so players can
    /// learn to read the land:
    ///   Meadow      open, flower-rich grass (grass pass adds extra tufts and flowers inside)   → deer
    ///   BrushPile   a tangle of fallen branches at the woodland edge                          → rabbits, foxes
    ///   MatureTree  a few trees near the trail grown into big old trees                       → squirrels
    ///   Perch       dead snags at meadow edges (plus boulders, which are already perches)     → hawks
    /// Run after trees/rocks/bushes and before grass.
    /// </summary>
    public partial class VegetationBuilder
    {
        private struct Meadow
        {
            public Vector3 center;
            public float radius;
        }

        private readonly List<Meadow> _meadows = new List<Meadow>();
        public int HotspotsPlaced { get; private set; }

        public void BuildHotspots(Transform parent)
        {
            BuildMeadows(parent);
            BuildBrushPiles(parent);
            BuildMatureTrees(parent);
            BuildSnags(parent);
        }

        private void AddHotspot(Transform parent, string name, HotspotType type, Vector3 position, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var h = go.AddComponent<HabitatHotspot>();
            h.type = type;
            h.radius = radius;
            HotspotsPlaced++;
        }

        private float TreeClearance(Vector3 p, float max)
        {
            float best = max;
            foreach (var t in _treeObjects)
                if (t != null) best = Mathf.Min(best, TerrainUtil.FlatDistance(t.transform.position, p));
            return best;
        }

        private static bool FarFrom(Vector3 p, List<Vector3> others, float distance)
        {
            foreach (var o in others) if (TerrainUtil.FlatDistance(p, o) < distance) return false;
            return true;
        }

        // ------------------------------------------------------------------ meadows

        private void BuildMeadows(Transform parent)
        {
            const float radius = 18f;
            var chosen = new List<Vector3>();
            for (int i = 0; i < 1500 && chosen.Count < 3; i++)
            {
                Vector3 p = RandomPoint();
                float d = _trail.DistanceToTrail(p);
                if (d < 12f || d > 45f || Forest(p) > 0.35f || TerrainUtil.SlopeDegrees(p) > 18f) continue;
                if (TreeClearance(p, 14f) < 14f || !FarFrom(p, chosen, 45f)) continue;
                chosen.Add(p);
                _meadows.Add(new Meadow { center = p, radius = radius });
                AddHotspot(parent, "Meadow Hotspot", HotspotType.Meadow, p, radius);
            }
        }

        // ------------------------------------------------------------------ brush piles

        private void BuildBrushPiles(Transform parent)
        {
            var chosen = new List<Vector3>();
            for (int i = 0; i < 1500 && chosen.Count < 4; i++)
            {
                Vector3 p = RandomPoint();
                float d = _trail.DistanceToTrail(p);
                float f = Forest(p);
                if (d < 8f || d > 28f || f < 0.3f || f > 0.62f || TerrainUtil.SlopeDegrees(p) > 25f) continue;
                if (!IsFree(p, 3f) || !FarFrom(p, chosen, 35f)) continue;
                chosen.Add(p);
                BrushPile(parent, p);
                Occupy(p);
                AddHotspot(parent, "Brush Pile Hotspot", HotspotType.BrushPile, p, 10f);
            }
        }

        private void BrushPile(Transform parent, Vector3 p)
        {
            var pile = new GameObject("Brush Pile");
            pile.transform.SetParent(parent, false);
            pile.transform.position = p;
            pile.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);

            int sticks = _rng.Next(7, 11);
            for (int i = 0; i < sticks; i++)
            {
                float len = R(1.4f, 3f), rad = R(0.05f, 0.13f);
                Vector3 pos = new Vector3(R(-0.8f, 0.8f), R(0.1f, 0.65f), R(-0.6f, 0.6f));
                var rot = Quaternion.Euler(90f + R(-25f, 25f), R(0f, 180f), 0f);
                MeshPart("Branch", pile.transform, _cylinder, Pick(_bark), pos, new Vector3(rad * 2f, len * 0.5f, rad * 2f), rot);
            }
            for (int i = 0; i < 2; i++)
            {
                Vector3 pos = new Vector3(R(-1.2f, 1.2f), 0.3f, R(-1f, 1f));
                MeshPart("Bramble", pile.transform, Pick(_canopies), Pick(_bush), pos, new Vector3(1.1f, 0.7f, 1.1f) * R(0.7f, 1f),
                    Quaternion.Euler(0f, R(0f, 360f), 0f));
            }

            var solid = pile.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, 0.35f, 0f);
            solid.size = new Vector3(2.6f, 0.7f, 2f);
            var cover = pile.AddComponent<BoxCollider>();
            cover.isTrigger = true; // hides a crouched player and blocks photo line-of-sight
            cover.center = new Vector3(0f, 0.55f, 0f);
            cover.size = new Vector3(3f, 1.1f, 2.4f);

            var f = pile.AddComponent<EnvironmentFeature>();
            f.kind = FeatureKind.Log;
            f.radius = 1.6f;
            f.height = 0.9f;
            MakeStatic(pile);
        }

        // ------------------------------------------------------------------ mature trees

        private void BuildMatureTrees(Transform parent)
        {
            var candidates = new List<GameObject>();
            foreach (var t in _treeObjects)
            {
                if (t == null) continue;
                float d = _trail.DistanceToTrail(t.transform.position);
                if (d >= 6f && d <= 30f) candidates.Add(t);
            }
            candidates.Sort((a, b) => Height(b).CompareTo(Height(a)));

            var chosen = new List<Vector3>();
            foreach (var t in candidates)
            {
                if (chosen.Count >= 5) break;
                Vector3 p = t.transform.position;
                if (!FarFrom(p, chosen, 30f)) continue;
                chosen.Add(p);

                // Grow it into a big old tree: wider trunk, taller crown, more to climb.
                t.name = "Mature " + t.name;
                t.transform.localScale *= 1.4f;
                var feature = t.GetComponent<EnvironmentFeature>();
                if (feature != null)
                {
                    feature.radius *= 1.4f;
                    feature.height *= 1.4f;
                }
                AddHotspot(parent, "Mature Tree Hotspot", HotspotType.MatureTree, p, 10f);
            }
        }

        private static float Height(GameObject t)
        {
            var f = t.GetComponent<EnvironmentFeature>();
            return f != null ? f.height : 0f;
        }

        // ------------------------------------------------------------------ perches

        private void BuildSnags(Transform parent)
        {
            var dead = GeneratedAssets.Material("DeadWood", new Color(0.45f, 0.42f, 0.38f));
            var chosen = new List<Vector3>();
            foreach (var m in _meadows)
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    float a = R(0f, Mathf.PI * 2f);
                    Vector3 p = TerrainUtil.Ground(m.center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (m.radius + R(3f, 8f)));
                    if (PathDistance(p) < 6f || !IsFree(p, 2.5f) || !FarFrom(p, chosen, 25f)) continue;
                    chosen.Add(p);
                    Snag(parent, p, dead);
                    Occupy(p);
                    AddHotspot(parent, "Perch Hotspot", HotspotType.Perch, p, 5f);
                    break;
                }
            }
        }

        private void Snag(Transform parent, Vector3 p, Material mat)
        {
            float h = R(9f, 12f), r = R(0.2f, 0.28f);
            var snag = new GameObject("Dead Snag");
            snag.transform.SetParent(parent, false);
            snag.transform.position = p;
            snag.transform.rotation = Quaternion.Euler(R(-3f, 3f), R(0f, 360f), R(-3f, 3f));
            MeshPart("Trunk", snag.transform, _cylinder, mat, new Vector3(0f, h * 0.5f, 0f), new Vector3(r * 2f, h * 0.5f, r * 2f), Quaternion.identity);
            for (int i = 0; i < 2; i++)
            {
                float y = h * R(0.55f, 0.85f), len = R(1.2f, 2.2f);
                var rot = Quaternion.Euler(0f, R(0f, 360f), R(55f, 75f));
                Vector3 offset = rot * Vector3.up * (len * 0.5f);
                MeshPart("Branch", snag.transform, _cylinder, mat, new Vector3(0f, y, 0f) + offset,
                    new Vector3(r * 0.7f, len * 0.5f, r * 0.7f), rot);
            }
            var col = snag.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.height = h;
            col.radius = r;
            var f = snag.AddComponent<EnvironmentFeature>();
            f.kind = FeatureKind.Tree;
            f.radius = r;
            f.height = h;
            MakeStatic(snag);
        }
    }
}
