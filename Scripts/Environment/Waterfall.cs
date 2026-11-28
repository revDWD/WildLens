using UnityEngine;

namespace WildLens.Environment
{
    /// <summary>
    /// Willow Falls at runtime (1.5): spray at the foot of the falls, and a flow that follows the weather. Rain swells
    /// the creek quickly and it settles back slowly over a few in-game hours, so after the afternoon shower the falls run
    /// faster, throw more spray and roar louder: a learnable reason to go there after rain. The spray particle system is
    /// configured here; its material is made by the builder (so it is included in builds).
    /// </summary>
    public class Waterfall : MonoBehaviour
    {
        [Header("Set by the builder")]
        public WaterFlow curtain;
        public AudioSource roar;
        public Material sprayMaterial;
        public Vector3 sprayCentre;
        public Vector3 spraySize = new Vector3(4f, 0.6f, 3f);
        public Quaternion sprayRotation = Quaternion.identity;

        [Header("Flow")]
        [Tooltip("Curtain scroll speed at normal flow (UV/s, negative = downward).")]
        public float baseScroll = -1.4f;
        [Tooltip("How much stronger the falls get when the creek is in spate after rain.")]
        public float spateBoost = 0.9f;

        /// <summary>0 = normal flow, 1 = in spate after rain.</summary>
        public float Spate { get; private set; }
        public float Flow => 1f + Spate * spateBoost;

        private ParticleSystem _spray;
        private Material _sprayMat;
        private float _baseVolume = -1f;

        private void Start()
        {
            if (sprayMaterial != null) BuildSpray();
            if (roar != null) _baseVolume = roar.volume;
        }

        private void OnDestroy()
        {
            if (_sprayMat != null) Destroy(_sprayMat);
        }

        private void BuildSpray()
        {
            _sprayMat = new Material(sprayMaterial);
            var go = new GameObject("Falls Spray");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(sprayCentre, sprayRotation);
            _spray = go.AddComponent<ParticleSystem>();
            _spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _spray.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.32f);
            main.gravityModifier = -0.04f; // drifts up and away
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            main.prewarm = true;
            var shape = _spray.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = spraySize;
            var vel = _spray.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            vel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            vel.z = new ParticleSystem.MinMaxCurve(0.2f, 0.9f); // carried downstream
            var col = _spray.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = _sprayMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _spray.Play();
        }

        private void Update()
        {
            // Spate: rises quickly while it rains, falls back over a few in-game hours.
            var weather = WeatherCycle.Instance;
            float rain = weather != null ? weather.Rain : 0f;
            var day = DayCycle.Instance;
            float hoursPerSecond = day != null ? 1f / Mathf.Max(1f, day.minutesPerHour * 60f) : 1f / 60f;
            if (rain > Spate) Spate = Mathf.MoveTowards(Spate, rain, Time.deltaTime * 0.25f);
            else Spate = Mathf.MoveTowards(Spate, rain, Time.deltaTime * hoursPerSecond * 0.3f); // ~3 game hours to settle

            float flow = Flow;
            if (curtain != null) curtain.speed = new Vector2(0f, baseScroll * flow);
            if (_spray != null)
            {
                var emission = _spray.emission;
                emission.rateOverTime = 16f * flow;
            }
            if (roar != null && _baseVolume >= 0f) roar.volume = Mathf.Clamp01(_baseVolume * (0.8f + 0.3f * flow));
        }
    }
}
