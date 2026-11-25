using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>
    /// A composed view the builder created and cleared (overlook platforms, lookouts). Used by the editor's
    /// comparison-capture tool and as a natural spot for players to stop and photograph.
    /// The transform is the eye point; its forward is the view direction.
    /// </summary>
    public class ScenicViewpoint : MonoBehaviour
    {
        public string title = "Viewpoint";
        [Tooltip("Vertical field of view used when capturing this view.")]
        public float captureFov = 50f;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawSphere(transform.position, 0.25f);
            Gizmos.DrawRay(transform.position, transform.forward * 12f);
        }
    }
}
