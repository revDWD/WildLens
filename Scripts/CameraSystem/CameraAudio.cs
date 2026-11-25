using UnityEngine;
using WildLens.Photography;

namespace WildLens.CameraSystem
{
    /// <summary>
    /// Camera sounds: shutter (on the shutter press, so it feels instant), a soft detent tick per zoom step and a
    /// quiet handling sound when the camera is raised. Clips are synthesised at startup so no audio assets are
    /// needed; assign authored clips to override them.
    /// </summary>
    public class CameraAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip shutterClip;
        [SerializeField] private AudioClip zoomTickClip;
        [SerializeField] private AudioClip raiseClip;
        [SerializeField, Range(0f, 1f)] private float shutterVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float zoomTickVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float raiseVolume = 0.2f;

        private AudioSource _source;
        private PhotoCamera _photo;
        private ZoomLens _lens;
        private CameraModeController _mode;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            if (shutterClip == null) shutterClip = ProceduralSounds.Shutter();
            if (zoomTickClip == null) zoomTickClip = ProceduralSounds.ZoomTick();
            if (raiseClip == null) raiseClip = ProceduralSounds.Handling();
        }

        private void OnEnable()
        {
            _photo = GetComponent<PhotoCamera>();
            _lens = GetComponent<ZoomLens>();
            _mode = GetComponent<CameraModeController>();
            if (_photo != null) _photo.CaptureStarting += OnShutter;
            if (_lens != null) _lens.LevelChanged += OnZoom;
            if (_mode != null) _mode.Entered += OnRaise;
        }

        private void OnDisable()
        {
            if (_photo != null) _photo.CaptureStarting -= OnShutter;
            if (_lens != null) _lens.LevelChanged -= OnZoom;
            if (_mode != null) _mode.Entered -= OnRaise;
        }

        private void OnShutter() => _source.PlayOneShot(shutterClip, shutterVolume);
        private void OnZoom(int level) => _source.PlayOneShot(zoomTickClip, zoomTickVolume);
        private void OnRaise() => _source.PlayOneShot(raiseClip, raiseVolume);
    }

    /// <summary>Tiny synthesiser for placeholder camera sounds.</summary>
    public static class ProceduralSounds
    {
        private const int Rate = 44100;

        /// <summary>Mechanical SLR shutter: mirror-up clack, short whirr, curtain/mirror-return clack.</summary>
        public static AudioClip Shutter()
        {
            var rng = new System.Random(7);
            int n = (int)(Rate * 0.17f);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.12f; // low-passed noise for the mechanism body
                float v = Clack(t, 0f, 1f, noise) + Clack(t, 0.07f, 0.75f, noise);
                if (t > 0.008f && t < 0.07f) v += lp * 0.12f * Mathf.Sin(Mathf.PI * (t - 0.008f) / 0.062f);
                data[i] = v;
            }
            return Build("Shutter", data);
        }

        private static float Clack(float t, float start, float amp, float noise)
        {
            float d = t - start;
            if (d < 0f) return 0f;
            float burst = noise * Mathf.Exp(-d * 260f) * 0.7f;
            float thump = Mathf.Sin(2f * Mathf.PI * 140f * d) * Mathf.Exp(-d * 60f) * 0.6f;
            float tick = Mathf.Sin(2f * Mathf.PI * 3400f * d) * Mathf.Exp(-d * 700f) * 0.4f;
            return amp * (burst + thump + tick);
        }

        /// <summary>Lens ring detent.</summary>
        public static AudioClip ZoomTick()
        {
            var rng = new System.Random(3);
            int n = (int)(Rate * 0.03f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                data[i] = noise * Mathf.Exp(-t * 1500f) * 0.6f + Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-t * 500f) * 0.5f;
            }
            return Build("ZoomTick", data);
        }

        /// <summary>Soft fabric/strap rustle when the camera comes up.</summary>
        public static AudioClip Handling()
        {
            var rng = new System.Random(11);
            int n = (int)(Rate * 0.14f);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.08f;
                data[i] = lp * Mathf.Sin(Mathf.PI * t) * 0.9f;
            }
            return Build("Handling", data);
        }

        private static AudioClip Build(string name, float[] data)
        {
            float peak = 0.0001f;
            foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < data.Length; i++) data[i] *= 0.9f / peak;
            var clip = AudioClip.Create("WL_" + name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
