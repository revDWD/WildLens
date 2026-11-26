using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WildLens.Core;
using WildLens.Environment;

namespace WildLens.EditorTools
{
    /// <summary>
    /// Environment Kit placement (1.2; part of <see cref="VegetationBuilder"/>). Free assets the user imported are put
    /// where their ecological role belongs, through the same placement rules as the generated scenery (forest field,
    /// clearances, occupancy), and each instance is given what gameplay needs:
    ///   trees: a solid trunk collider (blocks the player), a trigger crown collider (blocks photo line of sight, as
    ///          generated crowns do), an EnvironmentFeature (squirrels climb, birds perch) and distance culling unless
    ///          the prefab brings its own LODs. Kit trees are never static-batched (it breaks shader wind and LODs).
    ///   props: a collider (solid rocks and logs, trigger shrubs) and an EnvironmentFeature, sized from the mesh.
    /// With an empty kit nothing here runs and the random sequence is untouched, so reserves rebuild identically.
    /// </summary>
    public partial class VegetationBuilder
    {
        private enum TreeRole { Conifer, Broadleaf, Riparian, OldGrowth, Young }

        private readonly HashSet<GameObject> _kitInstances = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, FeatureKind?> _kitProps = new Dictionary<GameObject, FeatureKind?>();
        public int KitTreesPlaced { get; private set; }
        public int KitPropsPlaced { get; private set; }

        private EnvironmentKit Kit => _s.environmentKit != null && !_s.environmentKit.IsEmpty ? _s.environmentKit : null;
        private bool HasKitTrees => Kit != null && Kit.HasTrees;
        private bool IsKit(GameObject go) => go != null && _kitInstances.Contains(go);

        private static List<GameObject> Valid(GameObject[] list)
        {
            var result = new List<GameObject>();
            if (list != null) foreach (var g in list) if (g != null) result.Add(g);
            return result;
        }

        // ------------------------------------------------------------------ trees

        private List<GameObject> KitTreeOptions(TreeRole role)
        {
            var k = Kit;
            if (k == null) return new List<GameObject>();
            // Natural substitutes first; then (replaceAllGeneratedTrees) any kit tree, so fidelities never mix.
            GameObject[][] chain;
            switch (role)
            {
                case TreeRole.Conifer: chain = new[] { k.conifers }; break;
                case TreeRole.Riparian: chain = new[] { k.riparian, k.broadleaf }; break;
                case TreeRole.OldGrowth: chain = new[] { k.oldGrowth, k.broadleaf }; break;
                case TreeRole.Young: chain = new[] { k.young }; break;
                default: chain = new[] { k.broadleaf, k.oldGrowth, k.riparian }; break;
            }
            foreach (var list in chain)
            {
                var v = Valid(list);
                if (v.Count > 0) return v;
            }
            if (role == TreeRole.Young || !k.replaceAllGeneratedTrees) return new List<GameObject>();
            foreach (var list in new[] { k.broadleaf, k.conifers, k.riparian, k.oldGrowth })
            {
                var v = Valid(list);
                if (v.Count > 0) return v;
            }
            return new List<GameObject>();
        }

        /// <summary>A kit tree for <paramref name="role"/> at <paramref name="p"/>, or null if the kit has none for it.</summary>
        private GameObject KitTree(TreeRole role, Transform parent, Vector3 p)
        {
            var options = KitTreeOptions(role);
            if (options.Count == 0) return null;
            var prefab = options[_rng.Next(options.Count)];
            var go = InstantiatePrefab(prefab, parent, p, Kit.treeScale * R(0.85f, 1.2f));
            go.name = (role == TreeRole.Young ? "Young " : "") + prefab.name;
            EnsureTreeGameplay(go);
            _kitInstances.Add(go);
            KitTreesPlaced++;
            return go;
        }

        private void EnsureTreeGameplay(GameObject go)
        {
            float h = RenderedHeight(go);
            float s = Mathf.Max(0.01f, go.transform.lossyScale.y);
            float trunk = 0.12f + h * 0.012f;
            bool solid = false, trigger = false;
            foreach (var c in go.GetComponentsInChildren<Collider>()) { if (c.isTrigger) trigger = true; else solid = true; }
            if (!solid)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, 1.5f / s, 0f);
                cap.height = 3f / s;
                cap.radius = trunk / s;
            }
            if (!trigger)
            {
                var crown = go.AddComponent<CapsuleCollider>();
                crown.isTrigger = true; // blocks photo line of sight, not the player
                crown.center = new Vector3(0f, h * 0.6f / s, 0f);
                crown.height = h * 0.75f / s;
                crown.radius = h * 0.17f / s;
            }
            if (go.GetComponent<EnvironmentFeature>() == null) AddFeature(go, FeatureKind.Tree, trunk, h * 0.95f);
            AddDistanceCull(go, 0.012f); // skipped when the prefab has its own LODs
        }

        /// <summary>Old-growth giant for the grove: a kit giant if there is one, else a generated broadleaf.</summary>
        private GameObject GiantTree(Transform parent, Vector3 p) => KitTree(TreeRole.OldGrowth, parent, p) ?? Deciduous(parent, p);

        // ------------------------------------------------------------------ props

        /// <summary>Adds kit shrubs, ferns, rocks, boulders, logs and stumps to the scanned-art lists every builder draws from.</summary>
        private void MergeKitProps()
        {
            var k = Kit;
            if (k == null) return;
            Merge(_artShrubs, k.shrubs, FeatureKind.Bush, k.mixWithBundledArt);
            Merge(_artFerns, k.ferns, null, k.mixWithBundledArt);
            Merge(_artRocks, k.rocks, FeatureKind.Rock, k.mixWithBundledArt);
            Merge(_artBoulders, k.boulders, FeatureKind.Rock, k.mixWithBundledArt);
            Merge(_artLogs, k.logs, FeatureKind.Log, k.mixWithBundledArt);
            Merge(_artStumps, k.stumps, FeatureKind.Log, k.mixWithBundledArt);
        }

        private void Merge(List<GameObject> list, GameObject[] kit, FeatureKind? kind, bool mix)
        {
            var v = Valid(kit);
            if (v.Count == 0) return;
            if (!mix) list.Clear();
            foreach (var g in v)
            {
                list.Add(g);
                _kitProps[g] = kind;
            }
        }

        private bool HasKitShrubs
        {
            get
            {
                foreach (var g in _artShrubs) if (_kitProps.ContainsKey(g)) return true;
                return false;
            }
        }

        /// <summary>
        /// Gives a placed kit prop what the bundled scans carry: a collider and EnvironmentFeature sized from its mesh
        /// (in its own unscaled, unrotated space, like the bundled prefabs). Ground cover (ferns) gets neither.
        /// </summary>
        private void EnsurePropGameplay(GameObject go, FeatureKind? kind)
        {
            KitPropsPlaced++;
            _kitInstances.Add(go);
            if (kind == null || go.GetComponent<EnvironmentFeature>() != null) return;
            var rot = go.transform.rotation;
            go.transform.rotation = Quaternion.identity;
            var renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.one);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            go.transform.rotation = rot;
            Vector3 scale = go.transform.lossyScale;
            Vector3 size = new Vector3(b.size.x / Mathf.Max(0.01f, scale.x), b.size.y / Mathf.Max(0.01f, scale.y), b.size.z / Mathf.Max(0.01f, scale.z));
            Vector3 centre = b.center - go.transform.position;
            centre = new Vector3(centre.x / Mathf.Max(0.01f, scale.x), centre.y / Mathf.Max(0.01f, scale.y), centre.z / Mathf.Max(0.01f, scale.z));
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = centre;
                box.size = size * 0.9f;
                box.isTrigger = kind == FeatureKind.Bush; // shrubs hide and block photos, not movement
            }
            AddFeature(go, kind.Value, Mathf.Max(size.x, size.z) * 0.5f, size.y);
        }

        /// <summary>Kit vegetation is not static-batched (shader wind and LOD crossfades need their own instances).</summary>
        private bool KeepDynamic(GameObject go)
        {
            if (!IsKit(go)) return false;
            var f = go.GetComponent<EnvironmentFeature>();
            return f == null || f.kind == FeatureKind.Tree || f.kind == FeatureKind.Bush;
        }

        // ------------------------------------------------------------------ terrain

        private TerrainLayer[] ApplyKitLayers(TerrainLayer[] layers)
        {
            var k = Kit;
            if (k == null) return layers;
            var kit = k.Layers;
            for (int i = 0; i < layers.Length && i < kit.Length; i++)
                if (kit[i] != null) layers[i] = kit[i];
            return layers;
        }

        /// <summary>Replaces generated grass/flower prototypes with kit ones; fills per-slot density multipliers.</summary>
        private void ApplyKitDetails(DetailPrototype[] protos, float[] density)
        {
            var k = Kit;
            if (k == null) return;
            var kit = k.Details;
            for (int i = 0; i < protos.Length && i < kit.Length; i++)
            {
                var d = kit[i];
                if (d == null || !d.IsSet) continue;
                density[i] = d.density;
                protos[i] = d.mesh != null ? MeshDetail(d) : Modern(Detail(d.texture, protos[i].healthyColor, protos[i].dryColor,
                    protos[i].minWidth * d.scale.x, protos[i].maxWidth * d.scale.y, protos[i].minHeight * d.scale.x, protos[i].maxHeight * d.scale.y));
            }
        }

        private static DetailPrototype MeshDetail(KitDetail d)
        {
            var proto = new DetailPrototype
            {
                prototype = d.mesh,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                healthyColor = Color.white,
                dryColor = new Color(0.92f, 0.9f, 0.82f),
                minWidth = d.scale.x, maxWidth = d.scale.y,
                minHeight = d.scale.x, maxHeight = d.scale.y,
                noiseSpread = 0.35f,
            };
            // Unity 2022.2+: draw the mesh with its own material (Shader Graph vegetation in the free sample packs).
            var prop = typeof(DetailPrototype).GetProperty("useInstancing");
            if (prop != null && prop.CanWrite) prop.SetValue(proto, true);
            return Modern(proto);
        }
    }
}
