using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>
    /// Moves the ripples on a water surface (1.2): scrolls the normal map so Willow Creek visibly flows downstream (its UVs
    /// run along the channel) and the pond's surface drifts in the breeze. Uses its own material instance, so the shared
    /// generated material asset is never written in Play mode.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class WaterFlow : MonoBehaviour
    {
        [Tooltip("Normal-map scroll in UV units per second (creek: along V, its downstream direction).")]
        public Vector2 speed = new Vector2(0f, 0.06f);
        [Tooltip("A second, slower layer of the same ripples on the detail normal slot, if the shader has one.")]
        public float wobble = 0.35f;

        private Material _material;
        private Vector2 _offset;
        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

        private void Start()
        {
            var r = GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(BumpMap)) { enabled = false; return; }
            _material = r.material; // instance: the generated asset stays untouched
            _offset = _material.GetTextureOffset(BumpMap);
        }

        private void Update()
        {
            if (_material == null) return;
            float t = Time.time;
            _offset += speed * Time.deltaTime;
            Vector2 sway = new Vector2(Mathf.Sin(t * 0.37f), Mathf.Cos(t * 0.29f)) * 0.01f * wobble;
            _material.SetTextureOffset(BumpMap, _offset + sway);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
