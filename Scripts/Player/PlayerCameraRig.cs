using UnityEngine;
using WildLens.CameraSystem;
using WildLens.Debugging;
using WildLens.Observation;
using WildLens.Photography;

namespace WildLens.Player
{
    /// <summary>
    /// Physical motion of the view camera. Walking: head-bob. Camera raised: a dip and roll that settle as it
    /// comes to the eye, slow breathing, rotational inertia (the camera lags a fraction behind fast pans and
    /// springs back, so it feels like it has mass), and subtle handheld sway that steadies when you keep still.
    /// All of it is tiny in degrees but scales visibly with zoom, as it does with a real long lens.
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
        [Tooltip("Breathing amplitude in degrees (camera raised).")]
        [SerializeField] private float breathing = 0.02f;
        [Tooltip("Seconds of rotational lag behind fast pans (camera raised).")]
        [SerializeField] private float panLag = 0.012f;
        [SerializeField] private float raiseRoll = 0.8f;

        private CameraModeController _mode;
        private ZoomLens _lens;
        private float _bobPhase, _bobWeight;
        private float _dip, _dipVelocity;
        private float _roll, _rollVelocity;
        private Vector2 _lag, _lagVelocity;
        private Quaternion _lastParentRotation;
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
            _lastParentRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
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
            if (GetComponent<FocusController>() == null) gameObject.AddComponent<FocusController>();
            if (GetComponent<LensEffects>() == null) gameObject.AddComponent<LensEffects>();
            if (GetComponent<CameraAudio>() == null) gameObject.AddComponent<CameraAudio>();
            if (GetComponent<CameraDiagnostics>() == null) gameObject.AddComponent<CameraDiagnostics>();
        }

        private void OnEnable() => _mode.Entered += OnRaise;
        private void OnDisable() => _mode.Entered -= OnRaise;

        private void OnRaise()
        {
            _dipVelocity = -raiseDip * 12f;                          // kick downward, spring back
            _rollVelocity = (Random.value < 0.5f ? -1f : 1f) * raiseRoll * 10f; // and a small roll that settles
        }

        private static float Spring(ref float x, ref float v, float target, float stiffness, float dt)
        {
            float a = -(x - target) * stiffness - v * 2f * Mathf.Sqrt(stiffness);
            v += a * dt;
            x += v * dt;
            return x;
        }

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

            // Rotational inertia: lag behind the pivot's angular velocity, springing back.
            Quaternion parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Quaternion delta = parentRot * Quaternion.Inverse(_lastParentRotation);
            _lastParentRotation = parentRot;
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 angular = float.IsNaN(axis.x) ? Vector3.zero : axis * (angle / dt);
            Vector3 local = transform.parent != null ? transform.parent.InverseTransformDirection(angular) : angular;
            Vector2 lagTarget = new Vector2(Mathf.Clamp(-local.x * panLag, -0.6f, 0.6f), Mathf.Clamp(-local.y * panLag, -0.6f, 0.6f)) * cameraBlend;
            _lag = Vector2.SmoothDamp(_lag, lagTarget, ref _lagVelocity, 0.08f);

            Spring(ref _roll, ref _rollVelocity, 0f, 120f, dt);
            float breath = Mathf.Sin(Time.time * Mathf.PI * 2f * 0.22f) * breathing * cameraBlend;

            // Handheld sway: noticeable only zoomed in, and settles after standing still for a moment.
            _stillTime = speed < 0.1f ? _stillTime + dt : 0f;
            float steadiness = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(_stillTime / 1.5f));
            // Grows gently per zoom doubling; crouching braces the camera a little.
            float amp = handheldSway * cameraBlend * steadiness * Mathf.Lerp(1f, 0.8f, crouch)
                        * (1f + Mathf.Log(Mathf.Max(1f, _lens.CurrentMagnification), 2f) * 0.25f);
            float t = Time.time * 0.6f;
            transform.localRotation = Quaternion.Euler(
                (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * amp + breath + _lag.x,
                (Mathf.PerlinNoise(_seed + 17f, t) * 2f - 1f) * amp + _lag.y,
                _roll * cameraBlend);
        }
    }
}
