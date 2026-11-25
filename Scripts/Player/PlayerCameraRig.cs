using UnityEngine;
using WildLens.CameraSystem;
using WildLens.Observation;
using WildLens.Photography;

namespace WildLens.Player
{
    /// <summary>
    /// Physical motion of the view camera: walking head-bob, a small dip-and-settle when the camera is raised, and
    /// very subtle handheld sway in camera mode that steadies when the player stands still.
    /// Field of view belongs to <see cref="ZoomLens"/>; camera-mode state to <see cref="CameraModeController"/>.
    ///
    /// Also adds any missing camera-system components on start-up, so scenes built with an earlier version
    /// pick up new systems without a rebuild.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] private FirstPersonController controller;

        [Header("Head bob")]
        [SerializeField] private float bobAmplitude = 0.035f;
        [SerializeField] private float bobFrequency = 1.8f;

        [Header("Camera mode handling")]
        [SerializeField] private float raiseDip = 0.025f;
        [Tooltip("Handheld sway in degrees at 1×; grows gently with zoom, settles when still or crouched.")]
        [SerializeField] private float handheldSway = 0.035f;

        private CameraModeController _mode;
        private ZoomLens _lens;
        private float _bobPhase, _bobWeight;
        private float _dip, _dipVelocity;
        private float _stillTime;
        private Vector3 _restLocalPos;
        private float _seed;

        public CameraModeController CameraMode => _mode;
        public ZoomLens Lens => _lens;

        public void Configure(FirstPersonController owner) => controller = owner;

        private void Awake()
        {
            EnsureComponents();
            _restLocalPos = transform.localPosition;
            _seed = Random.Range(0f, 100f);
        }

        private void EnsureComponents()
        {
            _lens = GetComponent<ZoomLens>();
            if (_lens == null) _lens = gameObject.AddComponent<ZoomLens>();
            _mode = GetComponent<CameraModeController>();
            if (_mode == null) _mode = gameObject.AddComponent<CameraModeController>();
            if (GetComponent<PhotoCamera>() == null) gameObject.AddComponent<PhotoCamera>();
            if (GetComponent<FramingAssistant>() == null) gameObject.AddComponent<FramingAssistant>();
            if (GetComponent<ObservationTracker>() == null) gameObject.AddComponent<ObservationTracker>();
            if (GetComponent<CameraAudio>() == null) gameObject.AddComponent<CameraAudio>();
        }

        private void OnEnable() => _mode.Entered += OnRaise;
        private void OnDisable() => _mode.Entered -= OnRaise;

        private void OnRaise() => _dipVelocity = -raiseDip * 12f; // kick downward, spring back

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float speed = controller != null && controller.IsGrounded ? controller.CurrentSpeed : 0f;
            float cameraBlend = _mode.Blend;

            // Head bob, mostly suppressed while holding the camera to the eye.
            float crouch = controller != null ? controller.CrouchBlend : 0f;
            float targetWeight = Mathf.Clamp01(speed / 3f) * Mathf.Lerp(1f, 0.2f, cameraBlend) * Mathf.Lerp(1f, 0.6f, crouch);
            _bobWeight = Mathf.Lerp(_bobWeight, targetWeight, 1f - Mathf.Exp(-6f * dt));
            _bobPhase += dt * bobFrequency * Mathf.Lerp(1f, 1.5f, Mathf.Clamp01((speed - 3f) / 3f)) * Mathf.PI * 2f;
            Vector3 bob = new Vector3(Mathf.Cos(_bobPhase * 0.5f) * bobAmplitude * 0.5f,
                Mathf.Abs(Mathf.Sin(_bobPhase * 0.5f)) * bobAmplitude, 0f) * _bobWeight;

            // Critically-damped spring for the raise dip.
            float accel = -_dip * 180f - _dipVelocity * 2f * Mathf.Sqrt(180f);
            _dipVelocity += accel * dt;
            _dip += _dipVelocity * dt;

            transform.localPosition = _restLocalPos + bob + Vector3.up * _dip;

            // Handheld sway: noticeable only zoomed in, and settles after standing still for a moment.
            _stillTime = speed < 0.1f ? _stillTime + dt : 0f;
            float steadiness = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(_stillTime / 1.5f));
            // Grows gently per zoom doubling; crouching braces the camera a little.
            float amp = handheldSway * cameraBlend * steadiness * Mathf.Lerp(1f, 0.8f, crouch)
                        * (1f + Mathf.Log(Mathf.Max(1f, _lens.CurrentMagnification), 2f) * 0.25f);
            float t = Time.time * 0.6f;
            transform.localRotation = Quaternion.Euler(
                (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * amp,
                (Mathf.PerlinNoise(_seed + 17f, t) * 2f - 1f) * amp, 0f);
        }
    }
}
