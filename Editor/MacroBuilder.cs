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
                else Object.DestroyImmediate(def); // keep the user's tuning
                result[asset.subjectId] = asset;
            }
            return result;
        }

        public void BuildMacroSubjects(Transform parent)
        {
            var prefabs = _art ? ArtLibrary.Prefabs(ArtKind.MacroPlant) : new List<GameObject>();
            var dandelions = prefabs.FindAll(p => p != null && p.name.Contains("dandelion"));
            var periwinkles = prefabs.FindAll(p => p != null && p.name.Contains("periwinkle"));
            // 1.7: with no bundled plants and no pack models there is nothing to place (and nothing is created, as before).
            if (dandelions.Count == 0 && periwinkles.Count == 0 && !AnyPackSubjects()) return;
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

            // 1.7: small life from the user's packs. Everything below uses its own generators; the sequence above is untouched.
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

        private void BuildSmallLife(Transform group, Transform envParent, Dictionary<string, NaturalistSubject> subjects)
        {
            var list = new List<NaturalistSubject>();
            foreach (var s in subjects.Values)
                if (s != null && s.prefab != null && s.count > 0 && !IsBundledPlant(s.subjectId)) list.Add(s);
            if (list.Count == 0) return;
            list.Sort((a, b) => string.CompareOrdinal(a.subjectId, b.subjectId));

            // The dandelions just placed: the flowers that butterflies and bees visit.
            var flowers = new List<Vector3>();
            foreach (var m in group.GetComponentsInChildren<MacroSubject>())
                if (m != null && m.subject != null && m.subject.subjectId == "common_dandelion")
                    flowers.Add(m.focusPoint != null ? m.focusPoint.position : m.transform.position);
            var flowerUsed = new bool[flowers.Count];

            List<Transform> wood = null;
            List<(Vector3 centre, float radius)> creekSpots = null;
            var controllers = new Dictionary<string, RuntimeAnimatorController>();
            int total = 0;
            try { Physics.SyncTransforms(); } catch (System.Exception) { }

            foreach (var s in list)
            {
                var rng = new System.Random(unchecked(_s.seed * 613 + 29 + StableHash(s.subjectId)));
                int placed = 0;
                for (int i = 0; i < s.count; i++)
                {
                    Vector3 surface;
                    bool onGround = true;
                    var habitat = s.where;
                    bool ok = false;
                    surface = default;
                    if (habitat == SubjectHabitat.Flowers)
                    {
                        ok = TryFlower(rng, flowers, flowerUsed, out surface);
                        onGround = false;
                        if (!ok) { habitat = SubjectHabitat.MeadowVerge; onGround = true; }
                    }
                    if (!ok && habitat == SubjectHabitat.DeadWood)
                    {
                        if (wood == null) wood = FindDeadWood(envParent);
                        ok = TryDeadWood(rng, wood, out surface);
                        onGround = false;
                        if (!ok) { habitat = SubjectHabitat.WoodlandFloor; onGround = true; }
                    }
                    if (!ok)
                    {
                        switch (habitat)
                        {
                            case SubjectHabitat.WaterEdge:
                                if (creekSpots == null) creekSpots = FindCreekWaterSpots();
                                ok = TryWaterEdge(rng, creekSpots, out surface);
                                break;
                            case SubjectHabitat.WoodlandFloor: ok = TryWoodlandFloor(rng, out surface); break;
                            default: ok = TryVerge(rng, out surface); break;
                        }
                    }
                    if (!ok) continue;
                    if (PlaceModel(group, s, surface, rng, controllers))
                    {
                        if (onGround) AddKeepOut(surface, 0.7f); // no tall terrain grass over a small subject
                        placed++;
                    }
                }
                total += placed;
            }
            if (total > 0) Debug.Log($"[WildLens] Small life: {total} macro subjects placed from your packs.");
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

        /// <summary>The head of an unused dandelion, a few centimetres off-centre.</summary>
        private static bool TryFlower(System.Random rng, List<Vector3> flowers, bool[] used, out Vector3 p)
        {
            p = default;
            if (flowers.Count == 0) return false;
            int start = rng.Next(flowers.Count);
            for (int k = 0; k < flowers.Count; k++)
            {
                int i = (start + k) % flowers.Count;
                if (used[i]) continue;
                used[i] = true;
                p = flowers[i] + new Vector3((float)(rng.NextDouble() - 0.5) * 0.03f, 0.01f, (float)(rng.NextDouble() - 0.5) * 0.03f);
                return true;
            }
            return false;
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
        /// 0.3 to 1.5 m from the water on gentle ground: around the pond (its own shoreline, PondRadius), by the creek's
        /// water hotspots (ford bar, plunge pool, alcove: within the hotspot radius, 1.7 to 3.4 m from the creek line, the
        /// channel being about 1.2 m flat each side), and along the creek banks at large.
        /// </summary>
        private bool TryWaterEdge(System.Random rng, List<(Vector3 centre, float radius)> spots, out Vector3 p)
        {
            var pond = _sites.Find(x => x.kind == DestinationKind.Wetland);
            int options = (pond != null ? 1 : 0) + spots.Count + (_creek.Count > 1 ? 1 : 0);
            for (int attempt = 0; attempt < 40 && options > 0; attempt++)
            {
                int o = rng.Next(options);
                if (pond != null && o == 0)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float r = PondRadius(pond, pond.centre + dir * pond.water) + 0.3f + (float)rng.NextDouble() * 1.2f;
                    p = TerrainUtil.Ground(pond.centre + dir * r);
                    if (p.y < pond.waterLevel - 0.02f) continue; // still under water
                }
                else
                {
                    if (pond != null) o--;
                    if (o < spots.Count)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble()) * spots[o].radius;
                        p = TerrainUtil.Ground(spots[o].centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                    }
                    else
                    {
                        Vector3 c = _creek[rng.Next(_creek.Count)];
                        Vector3 side = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f).normalized;
                        p = TerrainUtil.Ground(c + side * (1.8f + (float)rng.NextDouble() * 1.4f));
                    }
                    float cd = CreekDistance(p);
                    if (cd < 1.7f || cd > 3.4f) continue;
                }
                if (TerrainUtil.SlopeDegrees(p) > 20f || PathDistance(p) < 1f || InKeepOut(p)) continue;
                return true;
            }
            p = default;
            return false;
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

        /// <summary>On top of a log or stump: a spot along its long axis, found by a ray onto its collider (else the top centre).</summary>
        private static bool TryDeadWood(System.Random rng, List<Transform> wood, out Vector3 p)
        {
            p = default;
            if (wood == null || wood.Count == 0) return false;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var log = wood[rng.Next(wood.Count)];
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
                if (found) { p = new Vector3(xz.x, bestY, xz.z); return true; }
            }
            // No collider answered (an art log without one): the top centre of the first log's bounds.
            var first = wood[rng.Next(wood.Count)];
            var frs = first.GetComponentsInChildren<Renderer>();
            if (frs.Length == 0) return false;
            Bounds fb = frs[0].bounds;
            foreach (var r in frs) fb.Encapsulate(r.bounds);
            p = new Vector3(fb.center.x, fb.max.y, fb.center.z);
            return true;
        }

        // ---- one instance

        private bool PlaceModel(Transform group, NaturalistSubject s, Vector3 surface, System.Random rng, Dictionary<string, RuntimeAnimatorController> controllers)
        {
            GameObject go = null;
            try
            {
                go = EditorUtility.IsPersistent(s.prefab) ? PrefabUtility.InstantiatePrefab(s.prefab, group) as GameObject : null;
                if (go == null) go = Object.Instantiate(s.prefab, group);
                if (go == null) return false;
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                    PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(go), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                StripPackComponents(go);
                if (!HasVisibleMesh(go))
                {
                    if (_noMeshWarned.Add(s.subjectId))
                        Debug.LogWarning($"[WildLens] Small life: '{s.prefab.name}' has no visible mesh once its scripts and effects are removed; '{s.displayName}' is left out.");
                    Object.DestroyImmediate(go);
                    return false;
                }
                go.transform.rotation = Quaternion.identity;
                go.transform.position = surface;

                // Scale so the largest renderer dimension equals the subject's size (measured unrotated: no AABB inflation).
                if (!TryBounds(go, out Bounds b) || Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) < 1e-5f)
                {
                    Object.DestroyImmediate(go);
                    return false;
                }
                float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                go.transform.localScale = go.transform.localScale * (s.size / largest);
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
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WildLens] Small life: could not place '{s.displayName}' from '{s.prefab.name}': {e.Message}");
                if (go != null) Object.DestroyImmediate(go);
                return false;
            }
        }

        /// <summary>Animals vanish when it is the wrong hour or weather; plants and fungi stay where they grow.</summary>
        private static bool HidesWhenAway(NaturalistSubject s)
        {
            if (s.group == SubjectGroup.Wildflower || s.group == SubjectGroup.Fungus) return false;
            bool gated = s.hidesInRain || s.afterRain;
            if (s.activePeriods != null) foreach (bool on in s.activePeriods) if (!on) gated = true;
            return gated;
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
