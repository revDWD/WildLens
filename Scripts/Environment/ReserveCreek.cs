using System.Collections.Generic;
using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>The creek's centre-line from source to mouth (drawn on the trailhead map; used for ecology queries).</summary>
    public class ReserveCreek : MonoBehaviour
    {
        public string title = "Willow Creek";
        [SerializeField] private List<Vector3> points = new List<Vector3>();

        private static readonly List<ReserveCreek> Active = new List<ReserveCreek>();
        public static IReadOnlyList<ReserveCreek> All => Active;
        public IReadOnlyList<Vector3> Points => points;

        public float Length
        {
            get
            {
                float l = 0f;
                for (int i = 1; i < points.Count; i++) l += Vector3.Distance(points[i - 1], points[i]);
                return l;
            }
        }

        public void SetPath(List<Vector3> p) => points = new List<Vector3>(p);

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f);
            for (int i = 1; i < points.Count; i++) Gizmos.DrawLine(points[i - 1] + Vector3.up * 0.3f, points[i] + Vector3.up * 0.3f);
        }
    }
}
