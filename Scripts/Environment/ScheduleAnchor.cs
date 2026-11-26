using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Environment
{
    public enum ScheduleRole { Water, Woodland, Ridge }

    /// <summary>
    /// A place wildlife schedules can send animals to that isn't already a habitat hotspot: water to drink at dusk,
    /// woodland cover to bed down in at midday, the ridge where hawks soar in daylight. Placed by the builder.
    /// </summary>
    public class ScheduleAnchor : MonoBehaviour
    {
        public ScheduleRole role;
        public float radius = 12f;

        private static readonly List<ScheduleAnchor> Active = new List<ScheduleAnchor>();
        public static IReadOnlyList<ScheduleAnchor> All => Active;

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        private void OnDrawGizmos()
        {
            Gizmos.color = role == ScheduleRole.Water ? new Color(0.3f, 0.6f, 1f, 0.6f) : role == ScheduleRole.Ridge ? new Color(1f, 0.8f, 0.3f, 0.6f) : new Color(0.2f, 0.6f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
