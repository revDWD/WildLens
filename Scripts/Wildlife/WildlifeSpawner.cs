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
        private int _ownStreamIndex;

        /// <summary>
        /// Species added after 1.7 draw their groups from their own random stream and their own individual seeds, so adding
        /// them to a reserve never moves an animal that was already there (the shared stream below is consumed in species
        /// order, then by residences). Append new ids here when a species is appended to SpeciesDefaults.CreateAll().
        /// </summary>
        private static readonly HashSet<string> OwnStreamSpecies = new HashSet<string>
        {
            "american_crow", "american_robin", "black_capped_chickadee", "american_goldfinch", "song_sparrow", "american_black_bear",
            "eastern_bluebird", "tufted_titmouse", "louisiana_waterthrush", // 1.9
        };

        private static bool HasOwnStream(SpeciesDefinition s) => s != null && OwnStreamSpecies.Contains(s.speciesId);

        private System.Random OwnStream(SpeciesDefinition s, int salt)
        {
            int h = 17;
            foreach (char c in s.speciesId) h = h * 31 + c;
            return new System.Random(unchecked(seed ^ h) + salt * 7919);
        }

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
            _ownStreamIndex = 0;
            foreach (var species in db.species)
            {
                if (species == null) continue;
                bool own = HasOwnStream(species);
                var srng = own ? OwnStream(species, 0) : rng;
                int groups = Mathf.RoundToInt(species.groupCount * populationMultiplier);
                for (int g = 0; g < groups; g++)
                {
                    Vector3 home = FindHome(species, srng, avoid);
                    int size = srng.Next(species.groupSize.x, species.groupSize.y + 1);
                    for (int i = 0; i < size; i++)
                    {
                        Vector3 pos = home;
                        if (i > 0)
                        {
                            float a = (float)srng.NextDouble() * Mathf.PI * 2f;
                            float r = 1.5f + (float)srng.NextDouble() * 3.5f;
                            pos += new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                        }
                        Spawn(species, pos, home, own ? seed + (100000 + (++_ownStreamIndex)) * 7919 : seed + (++index) * 7919);
                    }
                }
            }
            index = SpawnResidents(db, rng, avoid, index);
            Debug.Log($"[WildLens] Spawned {_spawned.Count} animals across {db.Count} species.");
        }

        /// <summary>
        /// Reserve destinations name their resident species (deer at the pond, squirrels in the old-growth grove…):
        /// one group of each settles there, so travelling somewhere is rewarded with the wildlife it promises.
        /// </summary>
        private int SpawnResidents(SpeciesDatabase db, System.Random sharedRng, Vector3? avoid, int index)
        {
            var places = new List<(string[] ids, Vector3 centre, float radius, Vector3 water, float waterRadius)>();
            foreach (var dest in WildLens.Environment.ReserveDestination.All)
                if (dest != null && dest.residentSpecies != null)
                    places.Add((dest.residentSpecies, dest.WildlifeCentre, dest.wildlifeRadius, dest.transform.position, dest.waterRadius));
            // 0.8: residences that aren't destinations (deer at the creek, a deer crossing, cottontails on a meadow edge).
            foreach (var res in WildLens.Environment.WildlifeResidence.All)
                if (res != null && res.species != null)
                    places.Add((res.species, res.transform.position, res.radius, Vector3.zero, 0f));

            int placeNumber = 0;
            foreach (var place in places)
            {
                placeNumber++;
                foreach (string id in place.ids)
                {
                    var species = db.Get(id);
                    if (species == null) continue;
                    bool own = HasOwnStream(species);
                    var rng = own ? OwnStream(species, placeNumber) : sharedRng;
                    Vector3 centre = place.centre;
                    Vector3 home = TerrainUtil.Ground(centre);
                    if (species.habitat != HabitatPreference.Sky)
                    {
                        for (int attempt = 0; attempt < 25; attempt++)
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                            float r = Mathf.Sqrt((float)rng.NextDouble()) * place.radius;
                            Vector3 c = TerrainUtil.Ground(centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                            if (TerrainUtil.SlopeDegrees(c) > species.maxSlope * 0.8f) continue;
                            if (place.waterRadius > 0f && TerrainUtil.FlatDistance(c, place.water) < place.waterRadius + 1.5f) continue;
                            if (avoid.HasValue && TerrainUtil.FlatDistance(c, avoid.Value) < playerStartClearance) continue;
                            home = c;
                            break;
                        }
                    }
                    int size = Mathf.Max(1, rng.Next(species.groupSize.x, species.groupSize.y + 1));
                    for (int i = 0; i < size; i++)
                    {
                        Vector3 pos = home;
                        if (i > 0 && species.habitat != HabitatPreference.Sky)
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                            pos = TerrainUtil.Ground(home + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (1.5f + (float)rng.NextDouble() * 3f));
                        }
                        Spawn(species, pos, home, own ? seed + (100000 + (++_ownStreamIndex)) * 7919 : seed + (++index) * 7919);
                    }
                }
            }
            return index;
        }

        private Vector3 FindHome(SpeciesDefinition species, System.Random rng, Vector3? avoid)
        {
            var trail = world != null ? world.Trail : null;
            Vector3 candidate = transform.position;

            // Most groups live around a preferred habitat hotspot, so the landscape becomes learnable.
            if (world != null && species.preferredHotspots != HotspotType.None && rng.NextDouble() < species.hotspotAffinity &&
                world.TryPickHotspot(species.preferredHotspots, rng, out var hotspot))
            {
                if (species.habitat == HabitatPreference.Sky) return hotspot.position;
                for (int attempt = 0; attempt < 15; attempt++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float r = hotspot.type == HotspotType.MatureTree
                        ? 1.2f + (float)rng.NextDouble() * 2f
                        : Mathf.Sqrt((float)rng.NextDouble()) * hotspot.radius * 0.7f;
                    Vector3 c = TerrainUtil.Ground(hotspot.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
                    if (TerrainUtil.SlopeDegrees(c) > species.maxSlope * 0.8f) continue;
                    if (trail != null && trail.DistanceToTrail(c) < trail.Width + 1f) continue;
                    if (avoid.HasValue && TerrainUtil.FlatDistance(c, avoid.Value) < playerStartClearance) continue;
                    return c;
                }
            }

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

            // Perception must exist before the brain initialises so the brain can subscribe to it.
            var awareness = go.GetComponent<WildlifeAwareness>();
            if (awareness == null) awareness = go.AddComponent<WildlifeAwareness>();
            var rig = go.GetComponent<AnimalRig>();
            awareness.Initialize(species, rig != null && rig.head != null ? rig.head : null);

            var agent = go.GetComponent<AnimalAgent>();
            if (agent == null)
                agent = species.locomotion == LocomotionType.Flying
                    ? (AnimalAgent)go.AddComponent<FlyingAnimal>()
                    : go.AddComponent<GroundAnimal>();

            if (rig != null && go.GetComponent<ProceduralAnimalAnimator>() == null)
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
