using UnityEngine;

namespace WildLens.Environment
{
    public enum AmbientKind { Wind, Brook }

    /// <summary>
    /// Procedural ambience that gives places a voice: wind gusting over the ridge, a brook babbling along the creek.
    /// Synthesised once per kind (no audio files), looped, 3D, randomly offset so neighbouring sources don't phase.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientSound : MonoBehaviour
    {
        public AmbientKind kind = AmbientKind.Brook;
        [Range(0f, 1f)] public float volume = 0.4f;
        public float audibleRadius = 35f;

        private const int Rate = 22050;
        private static AudioClip _wind, _brook;

        private void Start()
        {
            var src = GetComponent<AudioSource>();
            src.clip = kind == AmbientKind.Wind ? Wind() : Brook();
            src.loop = true;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = audibleRadius * 0.25f;
            src.maxDistance = audibleRadius;
            src.volume = volume;
            src.dopplerLevel = 0f;
            src.time = Random.value * src.clip.length * 0.95f;
            src.Play();
        }

        private static AudioClip Wind()
        {
            if (_wind != null) return _wind;
            int n = Rate * 12;
            var d = new float[n];
            var rng = new System.Random(11);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * 0.02f;   // deep rush
                lp2 += (white - lp2) * 0.12f; // airy hiss
                // Gusts: slow swells that loop seamlessly over 12 s.
                float gust = 0.55f + 0.3f * Mathf.Sin(t / 12f * Mathf.PI * 2f) + 0.15f * Mathf.Sin(t / 12f * Mathf.PI * 6f + 1.3f);
                d[i] = (lp * 3.2f + lp2 * 0.35f * gust) * gust * 0.7f;
            }
            _wind = AudioClip.Create("AmbientWind", n, 1, Rate, false);
            _wind.SetData(d, 0);
            return _wind;
        }

        private static AudioClip Brook()
        {
            if (_brook != null) return _brook;
            int n = Rate * 8;
            var d = new float[n];
            var rng = new System.Random(23);
            float lp = 0f, hp = 0f, prev = 0f;
            // Babble: band-limited noise plus many short "bloops" (resonant chirps) at random times.
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * 0.25f;
                hp = lp - prev; prev = lp;
                d[i] = (lp * 0.25f + hp * 0.6f) * 0.5f;
            }
            for (int b = 0; b < 260; b++)
            {
                int start = rng.Next(n - Rate / 8);
                float f0 = 300f + (float)rng.NextDouble() * 900f, len = 0.03f + (float)rng.NextDouble() * 0.05f;
                float amp = 0.08f + (float)rng.NextDouble() * 0.12f;
                int count = (int)(len * Rate);
                for (int k = 0; k < count; k++)
                {
                    float t = k / (float)Rate;
                    float env = Mathf.Sin(Mathf.PI * k / count);
                    d[start + k] += Mathf.Sin(2f * Mathf.PI * (f0 + 2500f * t) * t) * env * amp;
                }
            }
            _brook = AudioClip.Create("AmbientBrook", n, 1, Rate, false);
            _brook.SetData(d, 0);
            return _brook;
        }
    }
}
