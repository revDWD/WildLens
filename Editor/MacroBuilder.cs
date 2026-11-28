using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;
using WildLens.Photography;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Macro subjects in the reserve (1.5 groundwork; part of <see cref="VegetationBuilder"/>). Real CC0 plant scans
    /// placed where they'd grow, each carrying a <see cref="MacroSubject"/> so macro mode can find, focus on and grade it:
    /// dandelions on sunny verges of the Meadow Loop, lesser periwinkle in the fern glade's shade and on the grove floor.
    /// Subject data lives in Assets/WildLens/Data/Subjects (created once, then yours to tune, like species).
    /// Own random source: nothing else moves.
    /// </summary>
    public partial class VegetationBuilder
    {
        public const string SubjectsFolder = GeneratedAssets.DataFolder + "/Subjects";
        public int MacroSubjectsPlaced { get; private set; }

        public static Dictionary<string, NaturalistSubject> EnsureSubjectAssets()
        {
            GeneratedAssets.EnsureFolder(SubjectsFolder);
            var result = new Dictionary<string, NaturalistSubject>();
            foreach (var def in SubjectDefaults.CreateAll())
            {
                string path = $"{SubjectsFolder}/{def.name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<NaturalistSubject>(path);
                if (asset == null) { AssetDatabase.CreateAsset(def, path); asset = def; }
                else Object.DestroyImmediate(def); // keep the user's tuning
                result[asset.subjectId] = asset;
            }
            return result;
        }

        public void BuildMacroSubjects(Transform parent)
        {
            if (!_art) return;
            var prefabs = ArtLibrary.Prefabs(ArtKind.MacroPlant);
            var dandelions = prefabs.FindAll(p => p != null && p.name.Contains("dandelion"));
            var periwinkles = prefabs.FindAll(p => p != null && p.name.Contains("periwinkle"));
            if (dandelions.Count == 0 && periwinkles.Count == 0) return;
            var subjects = EnsureSubjectAssets();
            var rng = new System.Random(_s.seed * 271 + 11);
            float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var group = new GameObject("Macro Subjects").transform;
            group.SetParent(parent, false);

            // Dandelions: sunny verges along the Meadow Loop, a step or two off the tread.
            if (dandelions.Count > 0 && subjects.TryGetValue("common_dandelion", out var dandelion))
            {
                int placed = 0;
                for (int attempt = 0; attempt < 200 && placed < 12; attempt++)
                {
                    float d = Rf(0f, _trail.Length);
                    Vector3 c = _trail.GetPoint(d), dir = _trail.GetDirection(d);
                    Vector3 side = Vector3.Cross(Vector3.up, dir) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                    Vector3 p = TerrainUtil.Ground(c + side * Rf(_trail.Width * 0.5f + 0.7f, _trail.Width * 0.5f + 2.6f));
                    if (InKeepOut(p) || Forest(p) > 0.4f || TerrainUtil.SlopeDegrees(p) > 18f || CreekDistance(p) < 3f) continue;
                    Place(group, dandelions[rng.Next(dandelions.Count)], p, Rf(0.9f, 1.15f), dandelion, rng, true);
                    AddKeepOut(p, 0.7f); // no tall terrain grass over the flower (it is ~17 cm high)
                    placed++;
                }
            }

            // Lesser periwinkle: the fern glade's shade, and the grove floor.
            if (periwinkles.Count > 0 && subjects.TryGetValue("lesser_periwinkle", out var periwinkle))
            {
                var centres = new List<(Vector3 c, float rMin, float rMax, int count)>();
                if (_fernGlade.HasValue) centres.Add((_fernGlade.Value, 1.5f, 5f, 5));
                var grove = _sites.Find(x => x.kind == DestinationKind.OldGrowth);
                if (grove != null) centres.Add((grove.centre, 6f, 18f, 4));
                foreach (var (c, rMin, rMax, count) in centres)
                    for (int i = 0; i < count; i++)
                    {
                        float a = Rf(0f, Mathf.PI * 2f), r = Rf(rMin, rMax);
                        Vector3 p = TerrainUtil.Ground(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                        if (PathDistance(p) < 1.5f) continue;
                        Place(group, periwinkles[rng.Next(periwinkles.Count)], p, Rf(0.85f, 1.1f), periwinkle, rng, false);
                    }
            }
        }

        private void Place(Transform group, GameObject prefab, Vector3 p, float scale, NaturalistSubject subject, System.Random rng, bool headAtTop)
        {
            var go = InstantiatePrefab(prefab, group, p, scale);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            go.name = subject.displayName;
            var m = go.AddComponent<MacroSubject>();
            m.subject = subject;
            if (headAtTop)
            {
                // Frame and focus on the flower head, at the top of the plant.
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    var focus = new GameObject("Focus").transform;
                    focus.SetParent(go.transform, false);
                    focus.position = new Vector3(b.center.x, b.max.y - subject.size * 0.6f, b.center.z);
                    m.focusPoint = focus;
                    m.focusSize = subject.size * 1.4f;
                }
            }
            MakeStatic(go);
            MacroSubjectsPlaced++;
        }
    }
}
