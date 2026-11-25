using UnityEngine;

namespace WildLens.Vehicles
{
    /// <summary>
    /// Procedural single-cylinder engine sound: a soft, low "putt-putt" that rises in pitch with speed. Synthesised
    /// at startup (no audio files), looped, and faded in on start and out on switch-off.
    /// </summary>
    public class TrailBikeEngine : MonoBehaviour
    {
        private const int Rate = 22050;
        private static AudioClip _clip;

        private AudioSource _source;
        private float _load, _throttle, _volume, _targetVolume, _startPitch;
        private bool _running;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = Clip();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.minDistance = 3f;
            _source.maxDistance = 120f;
            _source.rolloffMode = AudioRolloffMode.Logarithmic;
            _source.dopplerLevel = 0.2f;
            _source.volume = 0f;
        }

        public void StartEngine(bool fromCold)
        {
            _running = true;
            _targetVolume = 0.32f;
            _startPitch = fromCold ? 0.45f : 0.9f;
            if (!_source.isPlaying) _source.Play();
        }

        public void StopEngine()
        {
            _running = false;
            _targetVolume = 0f;
        }

        public void SetLoad(float load, float throttle)
        {
            if (!_running) return;
            _load = load;
            _throttle = throttle;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _startPitch = Mathf.MoveTowards(_startPitch, 1f, dt * 1.2f);
            float target = _running ? _targetVolume + _load * 0.2f + _throttle * 0.06f : 0f;
            _volume = Mathf.MoveTowards(_volume, target, dt * (_running ? 0.8f : 0.5f));
            _source.volume = _volume;
            float pitch = (0.85f + _load * 1.25f + _throttle * 0.12f) * _startPitch;
            _source.pitch = _running ? pitch : Mathf.MoveTowards(_source.pitch, 0.4f, dt * 0.6f);
            if (!_running && _volume <= 0.001f && _source.isPlaying) _source.Stop();
        }

        private static AudioClip Clip()
        {
            if (_clip != null) return _clip;
            int n = Rate; // one second, 12 firing pulses: loops seamlessly
            var data = new float[n];
            var rng = new System.Random(5);
            const float pulses = 12f;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float phase = t * pulses % 1f;
                float env = Mathf.Exp(-phase * 7f);
                float body = Mathf.Sin(2f * Mathf.PI * 62f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 124f * t) * 0.25f;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.08f; // low-passed exhaust hiss
                data[i] = (body * env * 0.8f + lp * env * 0.9f + body * 0.06f) * 0.6f;
            }
            _clip = AudioClip.Create("TrailBikeEngine", n, 1, Rate, false);
            _clip.SetData(data, 0);
            return _clip;
        }
    }
}
