using System;
using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;

namespace WildLens.Environment
{
    public enum DestinationKind { RidgeOverlook, Wetland, OldGrowth, ObservationBlind }

    /// <summary>
    /// A named place in the reserve worth travelling to: a ridge overlook, a pond, an old-growth grove, a blind
    /// over a wildflower meadow. Each has a purpose (a view, water, big trees, concealment) and resident wildlife
    /// that the spawner settles there, so a trip is rewarded. Arriving shows a short field note.
    /// Placed by the scene builder; add your own anywhere.
    /// </summary>
    public class ReserveDestination : MonoBehaviour
    {
        public int number = 1;
        public string title = "Destination";
        [TextArea(2, 4)] public string fieldNote = "";
        [Tooltip("When to come (shown on the trailhead board), e.g. \"dawn mist · deer at dusk\".")]
        public string bestTimes = "";
        public DestinationKind kind;
        [Tooltip("Species ids that live here (one group each).")]
        public string[] residentSpecies = new string[0];
        [Tooltip("Where resident wildlife settles (world space). Defaults to this transform.")]
        public Vector3 wildlifeCentre;
        public float wildlifeRadius = 20f;
        public float arrivalRadius = 18f;
        [Tooltip("Open water radius around the transform (ponds); 0 for none. Drawn on the trailhead map.")]
        public float waterRadius;

        private static readonly List<ReserveDestination> Active = new List<ReserveDestination>();
        public static IReadOnlyList<ReserveDestination> All => Active;
        public static event Action<ReserveDestination> Arrived;

        public bool Visited { get; private set; }
        public Vector3 WildlifeCentre => wildlifeCentre == Vector3.zero ? transform.position : wildlifeCentre;

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
            Arrived = null;
        }

        private void Update()
        {
            if (Visited || !PlayerPresence.Exists) return;
            if (TerrainUtil.FlatDistance(PlayerPresence.Position, transform.position) > arrivalRadius) return;
            Visited = true;
            Arrived?.Invoke(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, arrivalRadius);
            Gizmos.color = new Color(0.4f, 0.9f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(WildlifeCentre, wildlifeRadius);
        }
    }
}
