using UnityEngine;

namespace WildLens.Wildlife
{
    /// <summary>
    /// Named pivots of an animal body, consumed by <see cref="ProceduralAnimalAnimator"/>.
    /// Filled automatically for placeholder bodies; for an authored prefab, assign bones here to get
    /// procedural motion, or leave it off and drive an Animator via <see cref="AnimalAnimatorBridge"/>.
    /// </summary>
    public class AnimalRig : MonoBehaviour
    {
        [Tooltip("Root of all visuals; bobbed/lowered as a whole.")]
        public Transform body;
        public Transform neck;
        public Transform head;
        public Transform tail;
        [Tooltip("Order: front-left, front-right, back-left, back-right.")]
        public Transform[] legs = new Transform[0];
        [Tooltip("Order: left, right.")]
        public Transform[] ears = new Transform[0];
        [Tooltip("Order: left, right.")]
        public Transform[] wings = new Transform[0];
        [Tooltip("Typical leg length, used to scale bob and resting height.")]
        public float legLength = 0.5f;
    }
}
