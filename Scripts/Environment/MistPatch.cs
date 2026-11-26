using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>
    /// Low mist lying over water at dawn (and a faint haze at dusk): slow, soft particles in a flat box whose opacity
    /// follows the <see cref="DayCycle"/>. Thickest just after first light, gone by mid-morning. The material is made by
    /// the builder (so it is included in builds); the particle system is configured here.
    /// </summary>
    public class MistPatch : MonoBehaviour
    {
        public Vector3 size = new Vector3(30f, 1.6f, 30f);
        public Material material;
        [Range(0f, 1f)] public float strength = 1f;

        private ParticleSystem _ps;
        private Material _mat;
        private float _shown = -1f;

        private void Start()
        {
            if (material == null) { enabled = false; return; }
            _mat = new Material(material);
            _ps = gameObject.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _ps.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 26f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Clamp(size.x * 0.25f, 4f, 9f), Mathf.Clamp(size.x * 0.45f, 6f, 14f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.prewarm = true;
            var emission = _ps.emission;
            emission.rateOverTime = Mathf.Clamp(size.x * size.z / 90f, 4f, 30f);
            var shape = _ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var fade = _ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var noise = _ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.08f;
            var r = GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = _mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 10f;
            _ps.Play();
        }

        private void OnDestroy() { if (_mat != null) Destroy(_mat); }

        /// <summary>Mist intensity for an hour of the day: dense at dawn, burning off by 09:00, a light haze at dusk.</summary>
        public static float Intensity(float hour)
        {
            if (hour >= 4.6f && hour < 9f) return hour < 6.4f ? Mathf.InverseLerp(4.6f, 5.6f, hour) : 1f - Mathf.InverseLerp(6.4f, 9f, hour);
            if (hour >= 18.8f && hour < 21.5f) return 0.35f * (hour < 20f ? Mathf.InverseLerp(18.8f, 20f, hour) : 1f - Mathf.InverseLerp(20.5f, 21.5f, hour));
            return 0f;
        }

        private void Update()
        {
            if (_ps == null) return;
            float hour = DayCycle.Instance != null ? DayCycle.Instance.Hour : 6f;
            float a = Intensity(hour) * strength;
            if (Mathf.Abs(a - _shown) < 0.01f) return;
            _shown = a;
            foreach (var prop in new[] { "_BaseColor", "_TintColor", "_Color" })
                if (_mat.HasProperty(prop))
                {
                    Color c = _mat.GetColor(prop);
                    c.a = a * (prop == "_TintColor" ? 0.5f : 1f);
                    _mat.SetColor(prop, c);
                }
            var emission = _ps.emission;
            emission.enabled = a > 0.02f;
        }
    }
}
