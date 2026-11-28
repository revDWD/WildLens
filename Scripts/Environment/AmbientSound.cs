using UnityEngine;

namespace WildLens.Environment
{
    public enum AmbientKind { Wind, Brook, Falls } // append only

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
        private static AudioClip _wind, _brook, _falls;

        private void Start()
        {
            var src = GetComponent<AudioSource>();
            src.clip = kind == AmbientKind.Wind ? Wind() : kind == AmbientKind.Falls ? Falls() : Brook();
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

        /// <summary>A waterfall: a deep, steady roar under a bright hiss, with slow swells (1.5). Loops over 10 s.</summary>
        private static AudioClip Falls()
        {
            if (_falls != null) return _falls;
            int n = Rate * 10;
            var d = new float[n];
            var rng = new System.Random(41);
            float deep = 0f, mid = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                deep += (white - deep) * 0.03f;  // the roar
                mid += (white - mid) * 0.3f;
                float hiss = mid - prev; prev = mid; // spray hiss
                float swell = 0.85f + 0.15f * Mathf.Sin(t / 10f * Mathf.PI * 2f) + 0.05f * Mathf.Sin(t / 10f * Mathf.PI * 8f + 0.7f);
                d[i] = (deep * 3.4f + mid * 0.35f + hiss * 0.5f) * swell * 0.55f;
            }
            _falls = AudioClip.Create("AmbientFalls", n, 1, Rate, false);
            _falls.SetData(d, 0);
            return _falls;
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
