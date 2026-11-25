using UnityEngine;
using WildLens.Core;

namespace WildLens.Player
{
    /// <summary>
    /// Owns the view camera's feel: walking head-bob, and the "camera raised" mode used for photography
    /// (hold RMB): steadier view, telephoto zoom on the scroll wheel, and reduced look sensitivity at zoom.
    /// Photography reads <see cref="IsRaised"/> and <see cref="Magnification"/>.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] private FirstPersonController controller;

        [Header("Field of view")]
        [SerializeField] private float normalFov = 65f;
        [SerializeField] private float raisedWideFov = 45f;
        [SerializeField] private float raisedTeleFov = 9f;
        [SerializeField] private float fovSmoothing = 10f;
        [SerializeField] private float zoomStep = 0.12f;

        [Header("Head bob")]
        [SerializeField] private float bobAmplitude = 0.035f;
        [SerializeField] private float bobFrequency = 1.8f;

        private Camera _camera;
        private float _zoom01;          // 0 = wide, 1 = full telephoto
        private float _bobPhase;
        private float _bobWeight;
        private Vector3 _restLocalPos;

        public bool IsRaised { get; private set; }
        public float Zoom01 => _zoom01;
        public float CurrentFov => _camera != null ? _camera.fieldOfView : normalFov;
        public Camera Camera => _camera;

        /// <summary>Optical magnification relative to the normal walking view.</summary>
        public float Magnification =>
            Mathf.Tan(normalFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(CurrentFov * 0.5f * Mathf.Deg2Rad);

        /// <summary>Look sensitivity multiplier: slower when zoomed in so framing is controllable.</summary>
        public float LookSensitivityScale => Mathf.Clamp(CurrentFov / normalFov, 0.15f, 1f);

        public void Configure(FirstPersonController owner) => controller = owner;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.fieldOfView = normalFov;
            _restLocalPos = transform.localPosition;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            var game = WildLensGame.Instance;
            bool canUse = (game == null || !game.IsUiOpen) && Cursor.lockState == CursorLockMode.Locked;

            IsRaised = canUse && GameInput.CameraRaiseHeld;
            if (IsRaised) _zoom01 = Mathf.Clamp01(_zoom01 + GameInput.Zoom * zoomStep);

            // Zoom is perceptually linear in log(focal length), so interpolate tan(fov/2) geometrically.
            float targetFov = IsRaised ? FovForZoom(_zoom01) : normalFov;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSmoothing * dt));

            UpdateHeadBob(dt);
        }

        private float FovForZoom(float t)
        {
            float wide = Mathf.Tan(raisedWideFov * 0.5f * Mathf.Deg2Rad);
            float tele = Mathf.Tan(raisedTeleFov * 0.5f * Mathf.Deg2Rad);
            float tan = wide * Mathf.Pow(tele / wide, t);
            return 2f * Mathf.Atan(tan) * Mathf.Rad2Deg;
        }

        private void UpdateHeadBob(float dt)
        {
            float speed = controller != null && controller.IsGrounded ? controller.CurrentSpeed : 0f;
            float targetWeight = Mathf.Clamp01(speed / 3f) * (IsRaised ? 0.2f : 1f);
            _bobWeight = Mathf.Lerp(_bobWeight, targetWeight, 1f - Mathf.Exp(-6f * dt));
            _bobPhase += dt * bobFrequency * Mathf.Lerp(1f, 1.5f, Mathf.Clamp01((speed - 3f) / 3f)) * Mathf.PI * 2f;

            Vector3 offset = new Vector3(Mathf.Cos(_bobPhase * 0.5f) * bobAmplitude * 0.5f,
                Mathf.Abs(Mathf.Sin(_bobPhase * 0.5f)) * bobAmplitude, 0f) * _bobWeight;
            transform.localPosition = _restLocalPos + offset;
        }
    }
}
