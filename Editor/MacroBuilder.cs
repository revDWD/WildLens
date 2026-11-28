using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
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
                else
                {
                    // Keep the user's tuning. An asset from before 1.9 gets the new fields (generated model, weather rules, second tier) from the defaults.
                    if (asset.dataVersion < NaturalistSubject.CurrentDataVersion)
                    {
                        asset.UpgradeToV2(def, SubjectDefaults.V1Count(def.subjectId));
                        EditorUtility.SetDirty(asset);
                    }
                    Object.DestroyImmediate(def);
                }
                result[asset.subjectId] = asset;
            }
            return result;
        }

        public void BuildMacroSubjects(Transform parent)
        {
            var prefabs = _art ? ArtLibrary.Prefabs(ArtKind.MacroPlant) : new List<GameObject>();
            var dandelions = prefabs.FindAll(p => p != null && p.name.Contains("dandelion"));
            var periwinkles = prefabs.FindAll(p => p != null && p.name.Contains("periwinkle"));
            // 1.7: with no bundled plants and no pack models there is nothing to place (and nothing is created, as before). Since 1.9 the
            // generated models are a third source (SliceBuildSettings.generatedSmallLife, on by default).
            if (dandelions.Count == 0 && periwinkles.Count == 0 && !AnyPackSubjects() && !_s.generatedSmallLife) return;
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

            // 1.7: small life from the user's packs, 1.9: and generated. Everything below uses its own generators; the sequence above is untouched.
            BuildSmallLife(group, parent, subjects);
        }

        // ================================================================== 1.7: small life from the user's packs

        private static readonly string[] BundledPlantIds = { "common_dandelion", "lesser_periwinkle" };

        private static bool IsBundledPlant(string id) => System.Array.IndexOf(BundledPlantIds, id) >= 0;

        /// <summary>True when any subject other than the two bundled plants has a model (cheap: asset load only).</summary>
        private static bool AnyPackSubjects()
        {
            if (!AssetDatabase.IsValidFolder(SubjectsFolder)) return false;
            foreach (string guid in AssetDatabase.FindAssets("t:NaturalistSubject", new[] { SubjectsFolder }))
            {
                var s = AssetDatabase.LoadAssetAtPath<NaturalistSubject>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.prefab != null && !IsBundledPlant(s.subjectId) && s.count > 0) return true;
            }
            return false;
        }

        /// <summary>FNV-1a: the same number on every machine (string.GetHashCode is not).</summary>
        private static int StableHash(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in text) h = (h ^ c) * 16777619u;
                return (int)(h & 0x7fffffffu);
            }
        }

        /// <summary>A flower (head position) an insect can visit, and which kind it is.</summary>
        private struct FlowerSpot
        {
            public Vector3 p;
            public string kind;
            public bool used;
        }

        // Which flowers each insect prefers, in order: monarchs milkweed first, swallowtails milkweed and black-eyed Susan, and so on.
        private static string[] FlowerPreference(string subjectId)
        {
            switch (subjectId)
            {
                case "monarch_butterfly": return new[] { "common_milkweed" };
                case "eastern_tiger_swallowtail": return new[] { "common_milkweed", "black_eyed_susan" };
                case "bumble_bee": return new[] { "black_eyed_susan", "oxeye_daisy", "common_milkweed" };
                case "honey_bee": return new[] { "oxeye_daisy", "black_eyed_susan" };
                case "cabbage_white": return new[] { "oxeye_daisy", "common_dandelion" };
                case "ladybird_beetle": return new[] { "black_eyed_susan", "oxeye_daisy" };
                default: return new string[0];
            }
        }

        // How far above a flower's focus point an insect sits (the umbel's top, the cone's dome, a daisy's disc).
        private static float FlowerLift(string kind)
        {
            switch (kind)
            {
                case "common_milkweed": return 0.032f;
                case "black_eyed_susan": return 0.012f;
                case "oxeye_daisy": return 0.005f;
                default: return 0.01f;
            }
        }

        private static bool IsFlowerSubject(NaturalistSubject s) =>
            s.group == SubjectGroup.Wildflower && s.where == SubjectHabitat.MeadowVerge && s.behaviour == SubjectBehaviour.Still;

        /// <summary>The model a subject will be placed from: its own pack model, else a generated one (when allowed), else none.</summary>
        private bool HasModel(NaturalistSubject s) => s.prefab != null || GeneratedWanted(s);

        private bool GeneratedWanted(NaturalistSubject s) => _s.generatedSmallLife && s.generatedModel != SmallLifeModel.None;

        private void BuildSmallLife(Transform group, Transform envParent, Dictionary<string, NaturalistSubject> subjects)
        {
            var list = new List<NaturalistSubject>();
            foreach (var s in subjects.Values)
                if (s != null && s.count > 0 && !IsBundledPlant(s.subjectId) && HasModel(s)) list.Add(s);
            if (list.Count == 0) return;
            // Flowers first (an insect needs a flower to sit on), then the rest; each in id order, as before.
            list.Sort((a, b) =>
            {
                int pa = IsFlowerSubject(a) ? 0 : 1, pb = IsFlowerSubject(b) ? 0 : 1;
                return pa != pb ? pa.CompareTo(pb) : string.CompareOrdinal(a.subjectId, b.subjectId);
            });

            // The dandelions just placed, then (below) every generated flower as it is placed: the flowers that butterflies and bees visit.
            var flowers = new List<FlowerSpot>();
            foreach (var m in group.GetComponentsInChildren<MacroSubject>())
                if (m != null && m.subject != null && m.subject.subjectId == "common_dandelion")
                    flowers.Add(new FlowerSpot { p = m.focusPoint != null ? m.focusPoint.position : m.transform.position, kind = "common_dandelion" });

            List<Transform> wood = null, trunks = null, rocks = null;
            List<(Vector3 centre, float radius)> creekSpots = null;
            List<Vector3> fireflyCentres = null;
            var controllers = new Dictionary<string, RuntimeAnimatorController>();
            int total = 0, generated = 0, extras = 0;
            var summary = new System.Text.StringBuilder();
            try { Physics.SyncTransforms(); } catch (System.Exception) { }

            foreach (var s in list)
            {
                var rng = new System.Random(unchecked(_s.seed * 613 + 29 + StableHash(s.subjectId)));
                int placed = 0, placedExtra = 0;
                int extraCount = s.extraWhen != SubjectExtra.None ? Mathf.Clamp(Mathf.RoundToInt(s.count * s.extraFraction), 0, s.count) : 0;
                var onLog = new Dictionary<Transform, int>(); // 1.8: at most two of a species on one log or stump (1.9: or boulder, or trunk)
                bool packModel = s.prefab != null;
                string[] prefer = FlowerPreference(s.subjectId);
                int waterMode = s.group == SubjectGroup.Damselfly ? 1 : s.subjectId == "common_cattail" ? 2 : 0; // creek only, pond only
                for (int i = 0; i < s.count; i++)
                {
                    Vector3 surface = default, normal = Vector3.up;
                    bool onGround = true, ok = false;
                    var habitat = s.where;
                    bool extra = i >= s.count - extraCount;
                    if (s.group == SubjectGroup.Firefly && habitat == SubjectHabitat.MeadowVerge)
                    {
                        // Clusters: three along the verge and one at the pond (if there is one), the fireflies scattered round each.
                        if (fireflyCentres == null) fireflyCentres = FireflyCentres(rng);
                        ok = TryFireflySpot(rng, fireflyCentres, i, out surface);
                        onGround = false;
                    }
                    else if (habitat == SubjectHabitat.Flowers)
                    {
                        ok = TryFlower(rng, flowers, prefer, out surface);
                        onGround = false;
                        if (!ok) { habitat = SubjectHabitat.MeadowVerge; onGround = true; }
                    }
                    else if (habitat == SubjectHabitat.Trunk)
                    {
                        if (trunks == null) trunks = FindTrunks(envParent);
                        ok = TryTrunk(rng, trunks, onLog, out surface, out normal);
                        onGround = false;
                        if (!ok) habitat = SubjectHabitat.DeadWood;
                    }
                    else if (habitat == SubjectHabitat.Rock)
                    {
                        if (rocks == null) rocks = FindRocks(envParent);
                        ok = TryDeadWood(rng, rocks, onLog, out surface);
                        onGround = false;
                        if (!ok) habitat = SubjectHabitat.WoodlandFloor;
                    }
                    if (!ok && habitat == SubjectHabitat.DeadWood)
                    {
                        if (wood == null) wood = FindDeadWood(envParent);
                        ok = TryDeadWood(rng, wood, onLog, out surface);
                        onGround = false;
                        if (!ok) { habitat = SubjectHabitat.WoodlandFloor; onGround = true; }
                    }
                    if (!ok)
                    {
                        switch (habitat)
                        {
                            case SubjectHabitat.WaterEdge:
                                if (creekSpots == null) creekSpots = FindCreekWaterSpots();
                                ok = TryWaterEdge(rng, creekSpots, waterMode, out surface);
                                onGround = true;
                                break;
                            case SubjectHabitat.WoodlandFloor: ok = TryWoodlandFloor(rng, out surface); onGround = true; break;
                            default: ok = TryVerge(rng, out surface); onGround = true; break;
                        }
                    }
                    if (!ok) continue;
                    MacroSubject placedSubject = packModel
                        ? PlaceModel(group, s, surface, rng, controllers)
                        : PlaceGenerated(group, s, surface, normal, rng);
                    if (placedSubject == null) continue;
                    if (extra) { placedSubject.tier = s.extraWhen; placedSubject.hideWhenNotShowing = true; placedExtra++; }
                    if (s.condition == SubjectCondition.FairEvening) placedSubject.stagger = (float)rng.NextDouble();
                    if (onGround) AddKeepOut(surface, 0.7f); // no tall terrain grass over a small subject
                    if (IsFlowerSubject(s) && placedSubject.focusPoint != null)
                        flowers.Add(new FlowerSpot { p = placedSubject.focusPoint.position + Vector3.up * FlowerLift(s.subjectId), kind = s.subjectId });
                    placed++;
                }
                total += placed;
                extras += placedExtra;
                if (!packModel) generated += placed;
                summary.Append("\n  ").Append(s.displayName).Append(": ").Append(placed);
                if (placedExtra > 0) summary.Append(" (").Append(placedExtra).Append(" in the second tier)");
                summary.Append(packModel ? ", pack model" : s.generatedModel == SmallLifeModel.FernScan ? ", bundled fern scan" : ", generated");
            }
            if (total > 0)
                Debug.Log($"[WildLens] Small life: {total} macro subjects placed ({generated} generated, {total - generated} from your packs; {extras} of them in second tiers that show only in the right weather)." + summary);
        }

        // ---- habitats: each returns the point the subject's underside rests on

        /// <summary>Sunny verges of the Meadow Loop (the dandelion rules, with its own random source).</summary>
        private bool TryVerge(System.Random rng, out Vector3 p)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                float d = (float)rng.NextDouble() * _trail.Length;
                Vector3 c = _trail.GetPoint(d), dir = _trail.GetDirection(d);
                Vector3 side = Vector3.Cross(Vector3.up, dir) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                float off = _trail.Width * 0.5f + 0.7f + (float)rng.NextDouble() * 1.9f;
                p = TerrainUtil.Ground(c + side * off);
                if (InKeepOut(p) || Forest(p) > 0.4f || TerrainUtil.SlopeDegrees(p) > 18f || CreekDistance(p) < 3f) continue;
                return true;
            }
            p = default;
            return false;
        }

        /// <summary>
        /// A spot on an unused flower, a few centimetres off-centre: the first unused flower of the insect's preferred kinds (in order), else
        /// any unused one. One insect per flower.
        /// </summary>
        private static bool TryFlower(System.Random rng, List<FlowerSpot> flowers, string[] prefer, out Vector3 p)
        {
            p = default;
            if (flowers.Count == 0) return false;
            int start = rng.Next(flowers.Count);
            int pick = -1;
            if (prefer != null)
                foreach (string kind in prefer)
                {
                    for (int k = 0; k < flowers.Count && pick < 0; k++)
                    {
                        int i = (start + k) % flowers.Count;
                        if (!flowers[i].used && flowers[i].kind == kind) pick = i;
                    }
                    if (pick >= 0) break;
                }
            for (int k = 0; k < flowers.Count && pick < 0; k++)
            {
                int i = (start + k) % flowers.Count;
                if (!flowers[i].used) pick = i;
            }
            if (pick < 0) return false;
            var f = flowers[pick];
            f.used = true;
            flowers[pick] = f;
            float lift = f.kind == "common_dandelion" ? 0.01f : 0f; // generated flowers already carry their own lift
            float spread = f.kind == "common_milkweed" ? 0.025f : 0.015f;
            p = f.p + new Vector3((float)(rng.NextDouble() - 0.5) * 2f * spread, lift, (float)(rng.NextDouble() - 0.5) * 2f * spread);
            return true;
        }

        /// <summary>The fern glade and the old-growth grove floor (as the periwinkles), else shaded ground near the trail.</summary>
        private bool TryWoodlandFloor(System.Random rng, out Vector3 p)
        {
            var centres = new List<(Vector3 c, float rMin, float rMax)>();
            if (_fernGlade.HasValue) centres.Add((_fernGlade.Value, 1.5f, 5f));
            var grove = _sites.Find(x => x.kind == DestinationKind.OldGrowth);
            if (grove != null) centres.Add((grove.centre, 6f, 18f));
            for (int attempt = 0; attempt < 60; attempt++)
            {
                if (centres.Count > 0)
                {
                    var (c, rMin, rMax) = centres[rng.Next(centres.Count)];
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = rMin + (float)rng.NextDouble() * (rMax - rMin);
                    p = TerrainUtil.Ground(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                }
                else
                {
                    float d = (float)rng.NextDouble() * _trail.Length;
                    Vector3 side = Vector3.Cross(Vector3.up, _trail.GetDirection(d)) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                    p = TerrainUtil.Ground(_trail.GetPoint(d) + side * (4f + (float)rng.NextDouble() * 8f));
                    if (Forest(p) < 0.5f) continue;
                }
                if (PathDistance(p) < 1.5f || InKeepOut(p) || CreekDistance(p) < 2.5f || TerrainUtil.SlopeDegrees(p) > 30f) continue;
                return true;
            }
            p = default;
            return false;
        }

        /// <summary>Water hotspots that belong to the creek (ford bar, plunge pool, alcove): the pond is handled by its own ring.</summary>
        private List<(Vector3 centre, float radius)> FindCreekWaterSpots()
        {
            var spots = new List<(Vector3, float)>();
            var found = new List<HabitatHotspot>(Object.FindObjectsOfType<HabitatHotspot>());
            found.RemoveAll(h => h == null || h.type != HotspotType.Water || h.name == "Pond Shallows");
            found.Sort((a, b) =>
            {
                int n = string.CompareOrdinal(a.name, b.name);
                if (n != 0) return n;
                Vector3 pa = a.transform.position, pb = b.transform.position;
                return pa.x != pb.x ? pa.x.CompareTo(pb.x) : pa.z.CompareTo(pb.z);
            });
            foreach (var h in found) spots.Add((h.transform.position, h.radius));
            return spots;
        }

        /// <summary>
        /// 0.3 to 1.5 m from the water on gentle ground: around each pond (its own shoreline, PondRadius), by the creek's water hotspots
        /// (ford bar, plunge pool, alcove: within the hotspot radius, 1.7 to 3.4 m from the creek line, the channel being about 1.2 m flat
        /// each side), along the creek banks at large, and (1.9) by any other water hotspot that is not on the creek (a pool someone else
        /// built: a ring at 0.6 to 1.0 of its radius on ground no lower than its centre). <paramref name="mode"/>: 0 anywhere, 1 the creek
        /// only (a jewelwing keeps to shaded streams), 2 ponds only (cattails).
        /// </summary>
        private bool TryWaterEdge(System.Random rng, List<(Vector3 centre, float radius)> spots, int mode, out Vector3 p)
        {
            var ponds = mode == 1 ? new List<Site>() : _sites.FindAll(x => x.kind == DestinationKind.Wetland);
            int spotCount = mode == 2 ? 0 : spots.Count;
            int creekOption = mode == 2 ? 0 : (_creek.Count > 1 ? 1 : 0);
            int options = ponds.Count + spotCount + creekOption;
            for (int attempt = 0; attempt < 40 && options > 0; attempt++)
            {
                int o = rng.Next(options);
                if (o < ponds.Count)
                {
                    var pond = ponds[o];
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float r = PondRadius(pond, pond.centre + dir * pond.water) + 0.3f + (float)rng.NextDouble() * 1.2f;
                    p = TerrainUtil.Ground(pond.centre + dir * r);
                    if (p.y < pond.waterLevel - 0.02f) continue; // still under water
                }
                else
                {
                    o -= ponds.Count;
                    if (o < spotCount)
                    {
                        bool standalone = CreekDistance(spots[o].centre) > 15f; // a pool that is not part of the creek
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                        if (standalone)
                        {
                            float rr = spots[o].radius * (0.6f + (float)rng.NextDouble() * 0.4f);
                            p = TerrainUtil.Ground(spots[o].centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr);
                            if (p.y < TerrainUtil.SampleHeight(spots[o].centre) + 0.02f) continue; // on the bank, not in the bed
                        }
                        else
                        {
                            float r = Mathf.Sqrt((float)rng.NextDouble()) * spots[o].radius;
                            p = TerrainUtil.Ground(spots[o].centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                            float cd0 = CreekDistance(p);
                            if (cd0 < 1.7f || cd0 > 3.4f) continue;
                        }
                    }
                    else
                    {
                        Vector3 c = _creek[rng.Next(_creek.Count)];
                        Vector3 side = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f).normalized;
                        p = TerrainUtil.Ground(c + side * (1.8f + (float)rng.NextDouble() * 1.4f));
                        float cd1 = CreekDistance(p);
                        if (cd1 < 1.7f || cd1 > 3.4f) continue;
                    }
                }
                if (TerrainUtil.SlopeDegrees(p) > 20f || PathDistance(p) < 1f || InKeepOut(p)) continue;
                return true;
            }
            p = default;
            return false;
        }

        // ---- 1.9: trunks, boulders and firefly clusters

        /// <summary>Trees with a trunk collider near a path, in hierarchy order (so the build repeats).</summary>
        private List<Transform> FindTrunks(Transform env)
        {
            var list = new List<Transform>();
            foreach (var f in env.GetComponentsInChildren<EnvironmentFeature>())
            {
                if (f == null || f.kind != FeatureKind.Tree || f.radius < 0.12f || f.radius > 1.2f) continue;
                if (f.GetComponent<Collider>() == null) continue;
                if (PathDistance(f.transform.position) > 30f) continue; // trunks the player will actually pass
                list.Add(f.transform);
            }
            return list;
        }

        /// <summary>
        /// On the lower trunk of a tree (0.4 to 1.6 m up), on the side facing the nearest path, found by a ray toward the trunk's axis; the
        /// normal is the bark's. At most two subjects of a species on one tree. False when there is no trunk (the caller falls back to dead wood).
        /// </summary>
        private bool TryTrunk(System.Random rng, List<Transform> trunks, Dictionary<Transform, int> used, out Vector3 p, out Vector3 normal)
        {
            p = default;
            normal = Vector3.up;
            if (trunks == null || trunks.Count == 0) return false;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var tree = trunks[rng.Next(trunks.Count)];
                if (tree == null || (used.TryGetValue(tree, out int n) && n >= MaxPerLog)) continue;
                var feature = tree.GetComponent<EnvironmentFeature>();
                float radius = feature != null ? feature.radius : 0.3f;
                // Of eight sides, the one whose bark is nearest a path (so it is seen), with a little randomness among the first few.
                float bestScore = float.MaxValue;
                Vector3 bestDir = Vector3.forward;
                float a0 = (float)rng.NextDouble() * Mathf.PI * 2f;
                for (int k = 0; k < 8; k++)
                {
                    float a = a0 + k * Mathf.PI * 0.25f;
                    Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float score = PathDistance(tree.position + d * (radius + 1f)) + (float)rng.NextDouble() * 2f;
                    if (score < bestScore) { bestScore = score; bestDir = d; }
                }
                float height = 0.4f + (float)rng.NextDouble() * 1.2f;
                Vector3 origin = tree.position + bestDir * (radius + 0.6f) + Vector3.up * height;
                Vector3 pos = tree.position + bestDir * radius + Vector3.up * height; // the fallback: the trunk's nominal surface
                Vector3 nrm = bestDir;
                var hits = Physics.RaycastAll(origin, -bestDir, radius + 0.9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float bestD = float.MaxValue;
                foreach (var h in hits)
                {
                    if (h.collider == null || !h.collider.transform.IsChildOf(tree) || h.distance >= bestD) continue;
                    bestD = h.distance;
                    pos = h.point;
                    nrm = Mathf.Abs(h.normal.y) < 0.6f ? new Vector3(h.normal.x, 0f, h.normal.z).normalized : bestDir;
                }
                p = pos + nrm * 0.002f;
                normal = nrm;
                used.TryGetValue(tree, out int c);
                used[tree] = c + 1;
                return true;
            }
            return false;
        }

        /// <summary>Boulders and rocks big enough to carry moss (0.4 to 4 m across), in hierarchy order.</summary>
        private static List<Transform> FindRocks(Transform env)
        {
            var list = new List<Transform>();
            foreach (var f in env.GetComponentsInChildren<EnvironmentFeature>())
            {
                if (f == null || f.kind != FeatureKind.Rock) continue;
                var rs = f.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                float across = Mathf.Max(b.size.x, b.size.z);
                if (across < 0.4f || across > 4f || b.size.y < 0.15f) continue;
                list.Add(f.transform);
            }
            return list;
        }

        /// <summary>Cluster centres for fireflies: three on the Meadow Loop's verge and, when there is a pond, one at its shore.</summary>
        private List<Vector3> FireflyCentres(System.Random rng)
        {
            var centres = new List<Vector3>();
            bool pond = _sites.Exists(x => x.kind == DestinationKind.Wetland);
            int verge = pond ? 3 : 4;
            for (int k = 0; k < verge; k++) if (TryVerge(rng, out var v)) centres.Add(v);
            if (pond)
            {
                var none = new List<(Vector3 centre, float radius)>();
                if (TryWaterEdge(rng, none, 2, out var shore)) centres.Add(shore);
            }
            return centres;
        }

        private bool TryFireflySpot(System.Random rng, List<Vector3> centres, int index, out Vector3 p)
        {
            p = default;
            if (centres == null || centres.Count == 0) return false;
            Vector3 c = centres[index % centres.Count];
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.5f + (float)rng.NextDouble() * 3f;
                Vector3 q = TerrainUtil.Ground(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                if (TerrainUtil.SlopeDegrees(q) > 25f || q.y < c.y - 1.2f) continue; // not down a bank into the water
                p = q;
                return true;
            }
            p = c;
            return true;
        }

        /// <summary>Fallen logs and stumps the builder placed (the Logs group, "Mossy Log", "Fallen Log", "Stump"), in hierarchy order.</summary>
        private static List<Transform> FindDeadWood(Transform env)
        {
            var list = new List<Transform>();
            var seen = new HashSet<Transform>();
            var logs = env.Find("Logs");
            if (logs != null)
                foreach (Transform t in logs) if (seen.Add(t) && t.GetComponentInChildren<Renderer>() != null) list.Add(t);
            foreach (var t in env.GetComponentsInChildren<Transform>())
            {
                string n = t.name;
                bool wood = n == "Mossy Log" || n == "Stump" || n == "Fallen Log";
                if (wood && seen.Add(t) && t.GetComponentInChildren<Renderer>() != null) list.Add(t);
            }
            return list;
        }

        private const int MaxPerLog = 2;

        /// <summary>
        /// On top of a log or stump: a spot along its long axis, found by a ray onto its collider (else the top centre). A log
        /// that already carries <see cref="MaxPerLog"/> subjects of this species (counted in <paramref name="used"/>) is skipped;
        /// with every log full this returns false and the caller sends the subject to the woodland floor.
        /// </summary>
        private static bool TryDeadWood(System.Random rng, List<Transform> wood, Dictionary<Transform, int> used, out Vector3 p)
        {
            p = default;
            if (wood == null || wood.Count == 0) return false;
            var open = new List<Transform>();
            foreach (var w in wood)
                if (w != null && (!used.TryGetValue(w, out int n) || n < MaxPerLog)) open.Add(w);
            if (open.Count == 0) return false;
            Transform firstPick = null;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var log = open[rng.Next(open.Count)];
                if (firstPick == null) firstPick = log;
                var rs = log.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                bool alongX = b.size.x >= b.size.z;
                float along = ((float)rng.NextDouble() - 0.5f) * 0.7f * (alongX ? b.size.x : b.size.z);
                Vector3 xz = new Vector3(b.center.x + (alongX ? along : 0f), 0f, b.center.z + (alongX ? 0f : along));
                var origin = new Vector3(xz.x, b.max.y + 0.5f, xz.z);
                var hits = Physics.RaycastAll(origin, Vector3.down, b.size.y + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                bool found = false;
                float bestY = float.MinValue;
                foreach (var h in hits)
                    if (h.collider != null && h.collider.transform.IsChildOf(log) && h.point.y > bestY) { bestY = h.point.y; found = true; }
                if (found)
                {
                    p = new Vector3(xz.x, bestY, xz.z);
                    used.TryGetValue(log, out int c);
                    used[log] = c + 1;
                    return true;
                }
            }
            // No collider answered (an art log without one): the top centre of the first log tried.
            var first = firstPick ?? open[0];
            var frs = first.GetComponentsInChildren<Renderer>();
            if (frs.Length == 0) return false;
            Bounds fb = frs[0].bounds;
            foreach (var r in frs) fb.Encapsulate(r.bounds);
            p = new Vector3(fb.center.x, fb.max.y, fb.center.z);
            used.TryGetValue(first, out int fc);
            used[first] = fc + 1;
            return true;
        }

        // ---- one instance

        private MacroSubject PlaceModel(Transform group, NaturalistSubject s, Vector3 surface, System.Random rng, Dictionary<string, RuntimeAnimatorController> controllers)
        {
            GameObject go = null;
            // Which model: the subject's own, or one of its variants (the subject's seeded generator, so the same build repeats).
            var pool = new List<GameObject> { s.prefab };
            if (s.variants != null) foreach (var v in s.variants) if (v != null) pool.Add(v);
            int variantIndex = pool.Count > 1 ? rng.Next(pool.Count) : 0;
            GameObject source = pool[variantIndex];
            // Mushrooms are often modelled as a cluster on a base: vary their size so a wood is not a row of clones.
            float sizeFactor = s.group == SubjectGroup.Fungus ? 0.8f + (float)rng.NextDouble() * 0.8f : 1f;
            try
            {
                go = EditorUtility.IsPersistent(source) ? PrefabUtility.InstantiatePrefab(source, group) as GameObject : null;
                if (go == null) go = Object.Instantiate(source, group);
                if (go == null) return null;
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                    PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(go), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                StripPackComponents(go);
                if (!HasVisibleMesh(go))
                {
                    if (_noMeshWarned.Add(s.subjectId))
                        Debug.LogWarning($"[WildLens] Small life: '{source.name}' has no visible mesh once its scripts and effects are removed; '{s.displayName}' is left out.");
                    Object.DestroyImmediate(go);
                    return null;
                }
                ConvertSmallLifeMaterials(go, source, s, variantIndex);
                go.transform.rotation = Quaternion.identity;
                go.transform.position = surface;

                // Scale so the largest renderer dimension equals the subject's size (measured unrotated: no AABB inflation).
                if (!TryBounds(go, out Bounds b) || Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) < 1e-5f)
                {
                    Object.DestroyImmediate(go);
                    return null;
                }
                float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                go.transform.localScale = go.transform.localScale * (s.size * sizeFactor / largest);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                TryBounds(go, out b);
                go.transform.position += new Vector3(surface.x - b.center.x, surface.y + s.hoverHeight - b.min.y, surface.z - b.center.z);
                go.name = s.displayName;

                var animator = go.GetComponentInChildren<Animator>();
                bool animated = animator != null || go.GetComponentInChildren<SkinnedMeshRenderer>() != null;
                var controller = EnsureLoopingController(s, animator, controllers);
                if (controller != null)
                {
                    if (animator == null) animator = go.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animated = true;
                }
                if (animator != null) { animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.CullCompletely; }

                var m = go.AddComponent<MacroSubject>();
                m.subject = s;
                TryBounds(go, out b);
                var focus = new GameObject("Focus").transform;
                focus.SetParent(go.transform, false);
                focus.position = b.center;
                m.focusPoint = focus;
                m.focusSize = s.size * 1.4f;
                m.hideWhenNotShowing = HidesWhenAway(s);
                if (s.behaviour != SubjectBehaviour.Still) go.AddComponent<MacroMotion>().behaviour = s.behaviour;
                if (s.behaviour == SubjectBehaviour.Still && !animated) MakeStatic(go);
                MacroSubjectsPlaced++;
                return m;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WildLens] Small life: could not place '{s.displayName}' from '{source.name}': {e.Message}");
                if (go != null) Object.DestroyImmediate(go);
                return null;
            }
        }

        // Converted materials per source model, so every instance of a model shares one set of URP copies (converting again would
        // delete and recreate the same assets under the instances already placed).
        private readonly Dictionary<GameObject, Material[][]> _smallLifeMats = new Dictionary<GameObject, Material[][]>();
        private readonly HashSet<string> _matWarned = new HashSet<string>();

        /// <summary>
        /// Under URP, swaps the pack's built-in-shader materials on one placed instance for converted copies (the pack's own are
        /// never touched), via <see cref="AnimalPrefabFactory.ConvertMaterialsForPipeline"/>. Never throws: a material problem is a
        /// Console warning, not a failed build.
        /// </summary>
        private void ConvertSmallLifeMaterials(GameObject go, GameObject source, NaturalistSubject s, int variantIndex)
        {
            try
            {
                if (go == null || !AnimalPrefabFactory.UrpActive()) return;
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                if (_smallLifeMats.TryGetValue(source, out var saved))
                {
                    if (saved.Length == renderers.Length)
                        for (int i = 0; i < renderers.Length; i++)
                            if (renderers[i] != null && saved[i] != null) renderers[i].sharedMaterials = saved[i];
                    return;
                }
                string folder = GeneratedAssets.Folder + "/SmallLife/Materials";
                string note;
                AnimalPrefabFactory.ConvertMaterialsForPipeline(go, $"{s.subjectId}_{variantIndex}", folder, out note);
                var record = new Material[renderers.Length][];
                var leftover = new SortedSet<string>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    record[i] = renderers[i].sharedMaterials;
                    foreach (var m in record[i])
                    {
                        string sn = m == null ? "(no material)" : m.shader != null ? m.shader.name : "(missing shader)";
                        if (AnimalPrefabFactory.PipelineIssue(sn, true, false) != null) leftover.Add(sn);
                    }
                }
                _smallLifeMats[source] = record;
                if (!string.IsNullOrEmpty(note) && _matWarned.Add(s.subjectId + "/note"))
                    Debug.LogWarning($"[WildLens] Small life: materials of '{source.name}' ({s.displayName}) were not fully converted for URP: {note}");
                if (leftover.Count > 0 && _matWarned.Add(s.subjectId + "/" + source.name))
                    Debug.LogWarning($"[WildLens] Small life: '{source.name}' ({s.displayName}) still uses shaders URP cannot draw (it may look pink): {string.Join(", ", leftover)}");
            }
            catch (System.Exception e)
            {
                if (_matWarned.Add(s.subjectId + "/ex"))
                    Debug.LogWarning($"[WildLens] Small life: could not convert materials for '{s.displayName}' ('{source.name}'): {e.Message}");
            }
        }

        /// <summary>Animals vanish when it is the wrong hour or weather; plants and fungi stay where they grow (a flower may be closed, not gone).</summary>
        private static bool HidesWhenAway(NaturalistSubject s)
        {
            if (s.IsPlantOrFungus) return false;
            bool gated = s.hidesInRain || s.afterRain || s.condition != SubjectCondition.None;
            if (s.activePeriods != null) foreach (bool on in s.activePeriods) if (!on) gated = true;
            return gated;
        }

        // ------------------------------------------------------------------ 1.9: one generated instance

        /// <summary>
        /// Places one instance of a generated model (or the bundled fern scan) with its pivot at <paramref name="surface"/>, standing on the ground
        /// or, for a trunk, lying against the bark (its up is the bark's normal, its head up the trunk). Sized from the subject's size relative to
        /// the model's authoring size, a little varied. Carries a <see cref="MacroSubject"/> (focus at the animal or flower head) and, when the
        /// behaviour calls for it, a <see cref="MacroMotion"/> that knows the model has hinged wings.
        /// </summary>
        private MacroSubject PlaceGenerated(Transform group, NaturalistSubject s, Vector3 surface, Vector3 normal, System.Random rng)
        {
            GameObject go = null;
            try
            {
                bool scan = s.generatedModel == SmallLifeModel.FernScan;
                GameObject source = scan ? FindFernScan() : SmallLifeModels.Prefab(s.generatedModel.ToString());
                if (source == null)
                {
                    if (_noMeshWarned.Add(s.subjectId)) Debug.LogWarning($"[WildLens] Small life: no model for '{s.displayName}' ({s.generatedModel}): left out.");
                    return null;
                }
                go = EditorUtility.IsPersistent(source) ? PrefabUtility.InstantiatePrefab(source, group) as GameObject : null;
                if (go == null) go = Object.Instantiate(source, group);
                if (go == null) return null;
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                    PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(go), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                float vary = s.group == SubjectGroup.Fungus ? 0.8f + (float)rng.NextDouble() * 0.45f : 0.9f + (float)rng.NextDouble() * 0.2f;
                float hover = s.hoverHeight;
                if (s.behaviour == SubjectBehaviour.Glows) hover *= 0.6f + (float)rng.NextDouble() * 0.8f; // fireflies at all heights
                float yaw = (float)rng.NextDouble() * 360f;
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                if (normal.y < 0.7f) // on a trunk: the head up the bark, the back away from it
                    rot = Quaternion.LookRotation(Vector3.up, normal) * Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() - 0.5) * 50f);
                go.transform.rotation = rot;
                go.transform.position = surface + Vector3.up * hover;

                if (scan)
                {
                    // The bundled scan has no authoring size: scale its renderers' largest dimension to the subject's size.
                    go.transform.rotation = Quaternion.identity;
                    if (TryBounds(go, out Bounds sb) && Mathf.Max(sb.size.x, sb.size.y, sb.size.z) > 1e-5f)
                        go.transform.localScale = go.transform.localScale * (s.size * vary / Mathf.Max(sb.size.x, sb.size.y, sb.size.z));
                    go.transform.rotation = rot;
                    if (TryBounds(go, out sb)) go.transform.position += new Vector3(surface.x - sb.center.x, surface.y - sb.min.y, surface.z - sb.center.z);
                }
                else
                {
                    float native = SmallLifeModels.NativeSize(s.generatedModel.ToString());
                    go.transform.localScale = Vector3.one * (native > 1e-6f ? s.size * vary / native : vary);
                    // A dragonfly's perch: the rider (the animal) sits at a height of its own on a stem of that height.
                    var rider = go.transform.Find("Rider");
                    var perch = go.transform.Find("Perch");
                    if (rider != null && perch != null)
                    {
                        float f = 0.75f + (float)rng.NextDouble() * 0.6f;
                        rider.localPosition = new Vector3(rider.localPosition.x, rider.localPosition.y * f, rider.localPosition.z);
                        perch.localScale = new Vector3(1f, f, 1f);
                    }
                }
                go.name = s.displayName;

                var m = go.AddComponent<MacroSubject>();
                m.subject = s;
                var focus = FindDeep(go.transform, "Focus");
                if (focus == null)
                {
                    focus = new GameObject("Focus").transform;
                    focus.SetParent(go.transform, false);
                    if (TryBounds(go, out Bounds fb)) focus.position = fb.center;
                }
                m.focusPoint = focus;
                // The focus box is 1.4 times the subject; a firefly is judged on its flash and halo, so its box is 4 cm.
                m.focusSize = s.behaviour == SubjectBehaviour.Glows ? Mathf.Max(0.04f, s.size * 1.4f) : s.size * 1.4f;
                m.hideWhenNotShowing = HidesWhenAway(s);
                if (s.behaviour != SubjectBehaviour.Still)
                {
                    var motion = go.AddComponent<MacroMotion>();
                    motion.behaviour = s.behaviour;
                    motion.hingedWings = !scan;
                }
                else MakeStatic(go); // it never moves: batched with the other static scenery
                MacroSubjectsPlaced++;
                return m;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WildLens] Small life: could not place the generated '{s.displayName}': {e.Message}");
                if (go != null) Object.DestroyImmediate(go);
                return null;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t != root && t.name == name) return t;
            return null;
        }

        /// <summary>The bundled CC0 fern scan (fern_02), the woodland fern's model.</summary>
        private GameObject FindFernScan()
        {
            if (!_art) return null;
            var ferns = ArtLibrary.Prefabs(ArtKind.Fern);
            return ferns.Find(f => f != null && f.name.Contains("fern_02"));
        }

        private static bool TryBounds(GameObject go, out Bounds b)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            b = default;
            bool any = false;
            foreach (var r in rs)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        // Components a pack may ship that a macro subject must not carry (matched by type name, so no module reference is
        // needed): navigation, particles (renderer first: it requires the system), lights, listeners and cameras.
        private static readonly string[] StripByTypeName =
            { "NavMeshAgent", "NavMeshObstacle", "ParticleSystemRenderer", "ParticleSystem", "Light", "AudioListener", "Camera" };

        private static void TryDestroy(Object o)
        {
            if (o == null) return;
            try { Object.DestroyImmediate(o); } catch (System.Exception) { /* a RequireComponent partner goes first on a later pass */ }
        }

        /// <summary>Packs ship physics, audio, effects and scripts of their own; a macro subject needs none (the Animator stays).</summary>
        private static void StripPackComponents(GameObject go)
        {
            // Scripts whose class is gone leave a "missing" component behind that nothing else can remove.
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                try { GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject); } catch (System.Exception) { }
            }
            // Scripts: up to three passes, so one that another RequireComponent-s is removed after the one needing it.
            for (int pass = 0; pass < 3; pass++)
            {
                var scripts = go.GetComponentsInChildren<MonoBehaviour>(true);
                int left = 0;
                foreach (var c in scripts) if (c != null) { TryDestroy(c); left++; }
                if (left == 0) break;
            }
            foreach (var c in go.GetComponentsInChildren<Joint>(true)) TryDestroy(c);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) TryDestroy(c);
            foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) TryDestroy(c);
            foreach (var c in go.GetComponentsInChildren<AudioSource>(true)) TryDestroy(c);
            foreach (string typeName in StripByTypeName)
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                    if (c != null && c.GetType().Name == typeName) TryDestroy(c);
        }

        /// <summary>Whether the instance still has an enabled renderer with a mesh (a pack prefab can be all rig, effects and scripts).</summary>
        private static bool HasVisibleMesh(GameObject go)
        {
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != null && smr.enabled && smr.sharedMesh != null) return true;
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null || !mr.enabled) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) return true;
            }
            return false;
        }

        private readonly HashSet<string> _noMeshWarned = new HashSet<string>();

        /// <summary>
        /// A one-state, looping controller when the model has clips but nothing to play them (no Animator, or an Animator with
        /// no controller). Clips that do not loop are copied into the controller asset with loop time on: the pack's import
        /// settings are not touched. Null when the model brings its own controller or has no clips.
        /// </summary>
        private static RuntimeAnimatorController EnsureLoopingController(NaturalistSubject s, Animator animator, Dictionary<string, RuntimeAnimatorController> cache)
        {
            if (animator != null && animator.runtimeAnimatorController != null) return null;
            if (cache.TryGetValue(s.subjectId, out var cached)) return cached;
            RuntimeAnimatorController result = null;
            try
            {
                var clips = AnimalPrefabFactory.ClipsIn(new Object[] { s.prefab });
                if (clips.Count > 0)
                {
                    AnimationClip pick = clips[0];
                    foreach (var key in new[] { "idle", "fly", "flap", "hover", "wing" })
                    {
                        var hit = clips.Find(c => c.name.ToLowerInvariant().Contains(key));
                        if (hit != null) { pick = hit; break; }
                    }
                    string folder = GeneratedAssets.Folder + "/SmallLife";
                    GeneratedAssets.EnsureFolder(folder);
                    string path = $"{folder}/{s.subjectId}_Animator.controller";
                    AssetDatabase.DeleteAsset(path);
                    var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
                    AnimationClip motion = pick;
                    if (!pick.isLooping)
                    {
                        var copy = Object.Instantiate(pick);
                        copy.name = pick.name + " (looped)";
                        var settings = AnimationUtility.GetAnimationClipSettings(copy);
                        settings.loopTime = true;
                        AnimationUtility.SetAnimationClipSettings(copy, settings);
                        AssetDatabase.AddObjectToAsset(copy, ctrl);
                        motion = copy;
                    }
                    var sm = ctrl.layers[0].stateMachine;
                    var state = sm.AddState(pick.name);
                    state.motion = motion;
                    sm.defaultState = state;
                    EditorUtility.SetDirty(ctrl);
                    result = ctrl;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WildLens] Small life: no looping animation for '{s.displayName}': {e.Message}");
            }
            cache[s.subjectId] = result;
            return result;
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
