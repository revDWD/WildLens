using UnityEngine;
using WildLens.Core;

namespace WildLens.Environment
{
    /// <summary>
    /// A wooden hide. Inside its box the player's silhouette is mostly hidden from wildlife and footsteps are
    /// muffled (<see cref="PlayerPresence.IsConcealed"/>), so animals in front of it behave naturally. The reward is
    /// patience, not a mechanic: sit, wait, photograph through the viewing slot.
    /// </summary>
    public class ObservationBlind : MonoBehaviour
    {
        [Tooltip("Interior volume in local space (centre and size).")]
        public Vector3 interiorCentre = new Vector3(0f, 1f, 0f);
        public Vector3 interiorSize = new Vector3(3f, 2f, 2.2f);

        private bool _inside;
        public bool PlayerInside => _inside;

        private void Update()
        {
            bool inside = PlayerPresence.Exists && Contains(PlayerPresence.Position + Vector3.up * 0.5f);
            if (inside == _inside) return;
            _inside = inside;
            PlayerPresence.SetConcealed(inside);
        }

        private void OnDisable()
        {
            if (_inside) PlayerPresence.SetConcealed(false);
            _inside = false;
        }

        private bool Contains(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world) - interiorCentre;
            Vector3 h = interiorSize * 0.5f;
            return Mathf.Abs(local.x) <= h.x && Mathf.Abs(local.y) <= h.y && Mathf.Abs(local.z) <= h.z;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.8f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(interiorCentre, interiorSize);
        }
    }
}
