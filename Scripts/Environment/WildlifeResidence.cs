using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>
    /// A place where a particular species reliably lives, without being a named destination: deer that drink at the
    /// creek, deer whose range straddles a track (so they cross it), cottontails along a meadow's brushy edge.
    /// <c>WildlifeSpawner</c> settles one group of each listed species here. Ecological placement, not new mechanics.
    /// </summary>
    public class WildlifeResidence : MonoBehaviour
    {
        public string label = "Residence";
        public string[] species = new string[0];
        public float radius = 15f;

        private static readonly List<WildlifeResidence> Active = new List<WildlifeResidence>();
        public static IReadOnlyList<WildlifeResidence> All => Active;

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.9f, 0.5f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
