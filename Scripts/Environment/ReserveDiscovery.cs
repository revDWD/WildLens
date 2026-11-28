using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>
    /// A hidden place (1.5): a quiet overlook, a fern glade, a creek alcove. Not on the trailhead map, not signed, not
    /// counted: no collectibles. When you first walk into one in a session, a field note names it, and that's all.
    /// Places, not points: each was built with a reason to linger (a seat, a view, a subject).
    /// </summary>
    public class ReserveDiscovery : MonoBehaviour
    {
        public string title = "Hidden place";
        [TextArea(2, 3)] public string note = "";
        public float radius = 10f;

        private static readonly List<ReserveDiscovery> Active = new List<ReserveDiscovery>();
        public static IReadOnlyList<ReserveDiscovery> All => Active;
        public static event Action<ReserveDiscovery> Found;

        public bool FoundThisSession { get; private set; }
        private float _check;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active.Clear(); Found = null; }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            if (FoundThisSession) return;
            _check -= Time.deltaTime;
            if (_check > 0f) return;
            _check = 0.5f;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 d = cam.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > radius * radius) return;
            FoundThisSession = true;
            Found?.Invoke(this);
        }
    }
}
