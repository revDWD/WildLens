using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>
    /// Wind motion for a plant. Parts (canopy lobes, conifer tiers, bush lobes) bend about the plant's base, with
    /// higher parts moving more, which reads as a trunk flexing rather than parts wobbling in place. A slower
    /// sway follows the gusts, with a quicker leaf flutter on top. Driven by <see cref="WildLensWind"/>.
    /// The trunk and colliders don't move, so gameplay is unaffected.
    /// </summary>
    public class FoliageSway : MonoBehaviour
    {
        [SerializeField] private Transform[] parts = new Transform[0];
        [Tooltip("Lean at the top in a strong gust (degrees).")]
        [SerializeField] private float amplitude = 1.6f;
        [SerializeField] private float frequency = 0.35f;
        [SerializeField] private float flutter = 0.25f;
        [Tooltip("Height of the plant; parts bend in proportion to their height.")]
        [SerializeField] private float height = 8f;

        public Transform[] Parts => parts;

        private Vector3[] _restPos;
        private Quaternion[] _restRot;
        private float _phase;

        public void Configure(Transform[] swayingParts, float plantHeight, float amplitudeDegrees, float swayFrequency)
        {
            parts = swayingParts;
            height = Mathf.Max(0.5f, plantHeight);
            amplitude = amplitudeDegrees;
            frequency = swayFrequency;
        }

        private void Awake()
        {
            _restPos = new Vector3[parts.Length];
            _restRot = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                _restPos[i] = parts[i].localPosition;
                _restRot[i] = parts[i].localRotation;
            }
            _phase = Random.value * 10f;
        }

        private void OnEnable() => WildLensWind.Register(this);
        private void OnDisable() => WildLensWind.Unregister(this);

        internal void Apply(float t, float gust, Vector3 windWorld)
        {
            // Bend axis: horizontal, perpendicular to the wind, in this plant's local space.
            Vector3 axis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, windWorld));
            float sway = Mathf.Sin(t * frequency * Mathf.PI * 2f + _phase) * 0.45f + 0.55f; // mostly leaning downwind
            float lean = amplitude * gust * sway;

            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p == null) continue;
                float h = Mathf.Clamp01(_restPos[i].y / height);
                float f = Mathf.Sin(t * 2.3f + _phase + i * 1.7f) * flutter * gust;
                Quaternion bend = Quaternion.AngleAxis(lean * h * h + f, axis);
                p.localPosition = bend * _restPos[i];
                p.localRotation = bend * _restRot[i];
            }
        }
    }
}
