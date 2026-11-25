using UnityEngine;
#if WILDLENS_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace WildLens.CameraSystem
{
    /// <summary>
    /// Optical character of the lens in camera mode (URP only; a no-op in other pipelines):
    ///   • depth of field that follows <see cref="FocusController"/>: physically based (bokeh) with the lens's
    ///     equivalent focal length, so at 1× almost everything is sharp and at 8–16× the background melts away
    ///     behind a focused animal, the classic telephoto wildlife look. Focus pulls are visible.
    ///   • a slightly stronger vignette while the camera is at the eye.
    /// Implemented as a runtime global Volume whose weight follows the camera-mode blend, layered over the scene's
    /// grading volume. Photos include the effect.
    /// </summary>
    public class LensEffects : MonoBehaviour
    {
        [SerializeField] private bool depthOfField = true;
        [Tooltip("f-number. Lower = shallower depth of field.")]
        [SerializeField, Range(1.4f, 16f)] private float aperture = 4f;
        [SerializeField, Range(0f, 1f)] private float cameraModeVignette = 0.3f;
        [SerializeField] private float volumePriority = 100f;

#if WILDLENS_URP
        private CameraModeController _mode;
        private ZoomLens _lens;
        private FocusController _focus;
        private Volume _volume;
        private DepthOfField _dof;

        private void Awake()
        {
            _mode = GetComponent<CameraModeController>();
            _lens = GetComponent<ZoomLens>();
            _focus = GetComponent<FocusController>();

            var cameraData = GetComponent<Camera>().GetUniversalAdditionalCameraData();
            if (cameraData != null) cameraData.renderPostProcessing = true;

            var go = new GameObject("Lens Volume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = volumePriority;
            _volume.weight = 0f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "WildLens Lens (runtime)";
            if (depthOfField)
            {
                _dof = profile.Add<DepthOfField>();
                _dof.mode.Override(DepthOfFieldMode.Bokeh);
                _dof.focusDistance.Override(10f);
                _dof.aperture.Override(aperture);
                _dof.focalLength.Override(50f);
                _dof.bladeCount.Override(7);
            }
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(cameraModeVignette);
            vignette.smoothness.Override(0.45f);
            _volume.sharedProfile = profile;
            go.SetActive(false);
        }

        private void LateUpdate()
        {
            float blend = _mode != null ? _mode.Blend : 0f;
            bool on = blend > 0.01f;
            if (_volume.gameObject.activeSelf != on) _volume.gameObject.SetActive(on);
            if (!on) return;
            _volume.weight = blend;

            if (_dof != null)
            {
                if (_focus != null) _dof.focusDistance.Override(_focus.FocusDistance);
                // URP caps focal length at 300 mm; the 16× step (~410 mm) uses the cap.
                float focal = _lens != null ? Mathf.Clamp(_lens.EquivalentFocalLength, 20f, 300f) : 50f;
                _dof.focalLength.Override(focal);
            }
        }
#endif
    }
}
