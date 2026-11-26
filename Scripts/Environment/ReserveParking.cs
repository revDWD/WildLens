using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;

namespace WildLens.Environment
{
    /// <summary>
    /// A gravel pull-off beside a reserve track where the trail bike belongs: short of a destination (so the engine
    /// doesn't scatter the wildlife) or at a scenic spot. While riding near one, the HUD suggests parking.
    /// </summary>
    public class ReserveParking : MonoBehaviour
    {
        public string title = "Parking";
        [Tooltip("Shown in the HUD prompt, e.g. \"walk 40 m to the blind\".")]
        public string hint = "";
        public float radius = 4f;

        private static readonly List<ReserveParking> Active = new List<ReserveParking>();
        public static IReadOnlyList<ReserveParking> All => Active;

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        /// <summary>The pull-off containing <paramref name="p"/> (with a little slack), or null.</summary>
        public static ReserveParking At(Vector3 p, float slack = 2f)
        {
            foreach (var a in Active)
                if (TerrainUtil.FlatDistance(p, a.transform.position) < a.radius + slack) return a;
            return null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
