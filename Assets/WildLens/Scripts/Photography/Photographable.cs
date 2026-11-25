using System.Collections.Generic;
using UnityEngine;
using WildLens.Data;
using WildLens.Wildlife;

namespace WildLens.Photography
{
    /// <summary>
    /// Marks something the camera can identify. Lives on each animal root; future subjects
    /// (plants, tracks, landmarks) can reuse it with a different definition type.
    /// </summary>
    public class Photographable : MonoBehaviour
    {
        private static readonly List<Photographable> ActiveList = new List<Photographable>();
        public static IReadOnlyList<Photographable> Active => ActiveList;

        [SerializeField] private SpeciesDefinition species;
        private AnimalAgent _agent;
        private Renderer[] _renderers;

        public SpeciesDefinition Species => species;
        public AnimalActivity CurrentActivity => _agent != null ? _agent.Activity : AnimalActivity.Wandering;

        public void Initialize(SpeciesDefinition definition, AnimalAgent agent)
        {
            species = definition;
            _agent = agent;
            _renderers = null;
        }

        private void OnEnable() => ActiveList.Add(this);
        private void OnDisable() => ActiveList.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveList.Clear();

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>();
            bounds = default;
            bool any = false;
            foreach (var r in _renderers)
            {
                if (r == null || !r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        public bool Owns(Collider c) => c != null && c.transform.IsChildOf(transform);
    }
}
