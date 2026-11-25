using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;
using WildLens.Data;
using WildLens.Photography;

namespace WildLens.Wildlife
{
    /// <summary>
    /// Populates the reserve at start: finds habitat-appropriate home ranges near the trail for each species,
    /// spawns groups, and assembles each animal (body, brain, animation, photo target, trigger collider).
    /// </summary>
    public class WildlifeSpawner : MonoBehaviour
    {
        [SerializeField] private SpeciesDatabase database;
        [SerializeField] private WildlifeWorld world;
        [SerializeField] private int seed = 20261125;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField, Range(0f, 3f)] private float populationMultiplier = 1f;
        [Tooltip("Keep ground animals at least this far from the player's start so they aren't spooked instantly.")]
        [SerializeField] private float playerStartClearance = 25f;

        private readonly List<AnimalAgent> _spawned = new List<AnimalAgent>();
        private Transform _container;

        public IReadOnlyList<AnimalAgent> Spawned => _spawned;

        public void Configure(SpeciesDatabase db, WildlifeWorld w)
        {
            database = db;
            world = w;
        }

        private void Start()
        {
            if (spawnOnStart) SpawnAll();
        }

        public void SpawnAll()
        {
            if (world == null) world = WildlifeWorld.Instance;
            var db = database != null ? database : (WildLensGame.Instance != null ? WildLensGame.Instance.Species : null);
            if (db == null)
            {
                Debug.LogWarning("[WildLens] WildlifeSpawner has no species database.");
                return;
            }

            if (_container == null) _container = new GameObject("Wildlife").transform;
            var rng = new System.Random(seed);
            Vector3? avoid = Camera.main != null ? Camera.main.transform.position : (Vector3?)null;

            int index = 0;
            foreach (var species in db.species)
            {
                if (species == null) continue;
                int groups = Mathf.RoundToInt(species.groupCount * populationMultiplier);
                for (int g = 0; g < groups; g++)
                {
                    Vector3 home = FindHome(species, rng, avoid);
                    int size = rng.Next(species.groupSize.x, species.groupSize.y + 1);
                    for (int i = 0; i < size; i++)
                    {
                        Vector3 pos = home;
                        if (i > 0)
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                            float r = 1.5f + (float)rng.NextDouble() * 3.5f;
                            pos += new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                        }
                        Spawn(species, pos, home, seed + (++index) * 7919);
                    }
                }
            }
            Debug.Log($"[WildLens] Spawned {_spawned.Count} animals across {db.Count} species.");
        }

        private Vector3 FindHome(SpeciesDefinition species, System.Random rng, Vector3? avoid)
        {
            var trail = world != null ? world.Trail : null;
            Vector3 candidate = transform.position;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 p, dir;
                if (trail != null && trail.Points.Count > 1)
                {
                    float along = (float)rng.NextDouble() * trail.Length;
                    p = trail.GetPoint(along);
                    dir = trail.GetDirection(along);
                }
                else
                {
                    p = transform.position;
                    float a = (float)rng.NextDouble() * 360f;
                    dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                }

                Vector3 side = Vector3.Cross(Vector3.up, dir) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                float dist = Mathf.Lerp(species.spawnDistanceFromTrail.x, species.spawnDistanceFromTrail.y, (float)rng.NextDouble());
                candidate = TerrainUtil.Ground(p + side * dist);

                if (species.habitat == HabitatPreference.Sky) return p;
                if (avoid.HasValue && TerrainUtil.FlatDistance(candidate, avoid.Value) < playerStartClearance) continue;
                if (TerrainUtil.SlopeDegrees(candidate) > species.maxSlope * 0.8f) continue;
                if (trail != null && trail.DistanceToTrail(candidate) < trail.Width + 1f) continue;
                if (world == null) return candidate;

                switch (species.habitat)
                {
                    case HabitatPreference.OpenMeadow:
                        if (world.DistanceToNearestTree(candidate, 8f) >= 7f) return candidate;
                        break;
                    case HabitatPreference.ForestEdge:
                        float d = world.DistanceToNearestTree(candidate, 16f);
                        if (d >= 3f && d <= 15f) return candidate;
                        break;
                    case HabitatPreference.Trees:
                        if (world.TryGetNearestTree(candidate, 12f, out var tree))
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                            return TerrainUtil.Ground(tree.basePosition + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (tree.radius + 1.2f));
                        }
                        break;
                }
            }
            return candidate; // habitat not found: accept the last candidate rather than skip the species
        }

        private void Spawn(SpeciesDefinition species, Vector3 position, Vector3 home, int individualSeed)
        {
            GameObject go;
            if (species.prefab != null)
            {
                go = Instantiate(species.prefab, position, Quaternion.identity, _container);
            }
            else
            {
                go = new GameObject();
                go.transform.SetParent(_container, false);
                go.transform.position = position;
                PlaceholderAnimalBuilder.Build(species, go.transform);
            }
            go.name = $"{species.displayName} #{_spawned.Count + 1}";

            EnsureTriggerCollider(go);
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var agent = go.GetComponent<AnimalAgent>();
            if (agent == null)
                agent = species.locomotion == LocomotionType.Flying
                    ? (AnimalAgent)go.AddComponent<FlyingAnimal>()
                    : go.AddComponent<GroundAnimal>();

            if (go.GetComponent<AnimalRig>() != null && go.GetComponent<ProceduralAnimalAnimator>() == null)
                go.AddComponent<ProceduralAnimalAnimator>();
            if (go.GetComponentInChildren<Animator>() != null && go.GetComponent<AnimalAnimatorBridge>() == null)
                go.AddComponent<AnimalAnimatorBridge>();

            var photo = go.GetComponent<Photographable>();
            if (photo == null) photo = go.AddComponent<Photographable>();
            photo.Initialize(species, agent);

            agent.Initialize(species, species.locomotion == LocomotionType.Flying ? home : position, world, individualSeed);
            _spawned.Add(agent);
        }

        /// <summary>One trigger box around the visuals: used for photo occlusion tests, never blocks the player.</summary>
        private static void EnsureTriggerCollider(GameObject go)
        {
            var existing = go.GetComponentInChildren<Collider>();
            if (existing != null) return;

            var renderers = go.GetComponentsInChildren<Renderer>();
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            // Spawned with identity rotation, so world AABB maps directly into local space.
            box.center = go.transform.InverseTransformPoint(b.center);
            Vector3 s = go.transform.lossyScale;
            box.size = new Vector3(b.size.x / Mathf.Max(0.0001f, s.x), b.size.y / Mathf.Max(0.0001f, s.y), b.size.z / Mathf.Max(0.0001f, s.z));
        }
    }
}
