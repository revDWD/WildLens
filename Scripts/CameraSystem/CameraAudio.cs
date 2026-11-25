using UnityEngine;
using WildLens.Data;
using WildLens.Observation;
using WildLens.Photography;

namespace WildLens.CameraSystem
{
    /// <summary>
    /// Camera sounds: shutter (on the shutter press, so it feels instant), a soft detent tick per zoom step, a
    /// quiet handling sound when the camera is raised, and a soft two-note chime when a species is identified
    /// by observation. Clips are synthesised at startup so no audio assets are
    /// needed; assign authored clips to override them.
    /// </summary>
    public class CameraAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip shutterClip;
        [SerializeField] private AudioClip zoomTickClip;
        [SerializeField] private AudioClip raiseClip;
        [SerializeField] private AudioClip identifyClip;
        [SerializeField] private AudioClip focusClip;
        [SerializeField] private AudioClip keeperClip;
        [SerializeField, Range(0f, 1f)] private float shutterVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float zoomTickVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float raiseVolume = 0.2f;
        [SerializeField, Range(0f, 1f)] private float identifyVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float focusVolume = 0.12f;
        [SerializeField, Range(0f, 1f)] private float keeperVolume = 0.3f;

        private AudioSource _source;
        private PhotoCamera _photo;
        private ZoomLens _lens;
        private CameraModeController _mode;
        private ObservationTracker _observation;
        private FocusController _focus;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            if (shutterClip == null) shutterClip = ProceduralSounds.Shutter();
            if (zoomTickClip == null) zoomTickClip = ProceduralSounds.ZoomTick();
            if (raiseClip == null) raiseClip = ProceduralSounds.Handling();
            if (identifyClip == null) identifyClip = ProceduralSounds.Chime();
            if (focusClip == null) focusClip = ProceduralSounds.FocusBeep();
            if (keeperClip == null) keeperClip = ProceduralSounds.Keeper();
        }

        private void OnEnable()
        {
            _photo = GetComponent<PhotoCamera>();
            _lens = GetComponent<ZoomLens>();
            _mode = GetComponent<CameraModeController>();
            if (_photo != null) _photo.CaptureStarting += OnShutter;
            if (_photo != null) _photo.PhotoTaken += OnPhotoTaken;
            if (_lens != null) _lens.LevelChanged += OnZoom;
            if (_mode != null) _mode.Entered += OnRaise;
            _observation = GetComponent<ObservationTracker>();
            if (_observation != null) _observation.SpeciesIdentified += OnIdentified;
            _focus = GetComponent<FocusController>();
            if (_focus != null) _focus.FocusLocked += OnFocusLocked;
        }

        private void OnDisable()
        {
            if (_photo != null) _photo.CaptureStarting -= OnShutter;
            if (_photo != null) _photo.PhotoTaken -= OnPhotoTaken;
            if (_lens != null) _lens.LevelChanged -= OnZoom;
            if (_mode != null) _mode.Entered -= OnRaise;
            if (_observation != null) _observation.SpeciesIdentified -= OnIdentified;
            if (_focus != null) _focus.FocusLocked -= OnFocusLocked;
        }

        private void OnShutter() => _source.PlayOneShot(shutterClip, shutterVolume);
        private void OnZoom(int level) => _source.PlayOneShot(zoomTickClip, zoomTickVolume);
        private void OnRaise() => _source.PlayOneShot(raiseClip, raiseVolume);
        private void OnIdentified(SpeciesDefinition species) => _source.PlayOneShot(identifyClip, identifyVolume);
        private void OnFocusLocked() => _source.PlayOneShot(focusClip, focusVolume);

        private void OnPhotoTaken(PhotoResult result)
        {
            if (result.identifications == null) return;
            foreach (var id in result.identifications)
                if (PhotoQualityEvaluator.Parse(id.subject.quality) == PhotoQuality.Excellent)
                {
                    _source.PlayOneShot(keeperClip, keeperVolume); // a keeper
                    return;
                }
        }
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

        /// <summary>Autofocus confirmation: two short, soft high blips.</summary>
        public static AudioClip FocusBeep()
        {
            int n = (int)(Rate * 0.12f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = (t < 0.035f ? Mathf.Sin(Mathf.PI * t / 0.035f) : 0f) +
                            (t > 0.06f && t < 0.095f ? Mathf.Sin(Mathf.PI * (t - 0.06f) / 0.035f) : 0f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 2700f * t) * env;
            }
            return Build("FocusBeep", data);
        }

        /// <summary>Warm three-note rising arpeggio for an Excellent photograph.</summary>
        public static AudioClip Keeper()
        {
            int n = (int)(Rate * 1.1f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = Note(t, 0f, 523.25f) * 0.5f + Note(t, 0.09f, 659.25f) * 0.45f + Note(t, 0.18f, 783.99f) * 0.5f; // C5 E5 G5
            }
            return Build("Keeper", data);
        }

        /// <summary>Two soft rising sine notes with a gentle decay.</summary>
        public static AudioClip Chime()
        {
            int n = (int)(Rate * 0.6f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float a = Note(t, 0f, 1318.5f);     // E6
                float b = Note(t, 0.11f, 1975.5f);  // B6
                data[i] = a * 0.6f + b * 0.5f;
            }
            return Build("Chime", data);
        }

        private static float Note(float t, float start, float freq)
        {
            float d = t - start;
            if (d < 0f) return 0f;
            float env = Mathf.Min(1f, d * 200f) * Mathf.Exp(-d * 7f);
            return (Mathf.Sin(2f * Mathf.PI * freq * d) + 0.25f * Mathf.Sin(4f * Mathf.PI * freq * d)) * env;
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
