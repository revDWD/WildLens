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
        [Tooltip("Also scroll the main texture (the falls curtain's streaks), not only the normal map.")]
        public bool scrollBase;

        private Material _material;
        private Vector2 _offset;
        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        private void Start()
        {
            var r = GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) { enabled = false; return; }
            bool bump = r.sharedMaterial.HasProperty(BumpMap);
            if (!bump && !scrollBase) { enabled = false; return; }
            _material = r.material; // instance: the generated asset stays untouched
            if (bump) _offset = _material.GetTextureOffset(BumpMap);
        }

        private void Update()
        {
            if (_material == null) return;
            float t = Time.time;
            _offset += speed * Time.deltaTime;
            Vector2 sway = new Vector2(Mathf.Sin(t * 0.37f), Mathf.Cos(t * 0.29f)) * 0.01f * wobble;
            if (_material.HasProperty(BumpMap)) _material.SetTextureOffset(BumpMap, _offset + sway);
            if (scrollBase)
            {
                if (_material.HasProperty(BaseMap)) _material.SetTextureOffset(BaseMap, _offset);
                if (_material.HasProperty(MainTex)) _material.SetTextureOffset(MainTex, _offset);
            }
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
