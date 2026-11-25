using UnityEngine;

namespace WildLens.Environment
{
    public enum FeatureKind { Tree, Rock, Bush, Log }

    /// <summary>
    /// Tags a scenery object so wildlife can use it (squirrels climb trees, hawks perch on tall trees and rocks,
    /// ground animals steer around trunks). Add this to any authored prefab to make it wildlife-aware.
    /// </summary>
    public class EnvironmentFeature : MonoBehaviour
    {
        public FeatureKind kind = FeatureKind.Tree;
        [Tooltip("Trunk radius for trees, footprint radius for rocks/bushes.")]
        public float radius = 0.3f;
        [Tooltip("Height of the top (perch point) above the base.")]
        public float height = 8f;

        public Vector3 BasePosition => transform.position;
        public Vector3 TopPosition => transform.position + Vector3.up * height;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(TopPosition, 0.3f);
            Gizmos.DrawLine(BasePosition, TopPosition);
        }
    }
}
