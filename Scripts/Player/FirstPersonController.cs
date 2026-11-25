using UnityEngine;
using WildLens.CameraSystem;
using WildLens.Core;
using WildLens.Photography;

namespace WildLens.Player
{
    /// <summary>
    /// First-person walking controller: mouse look, walk/sprint with smoothed acceleration, gravity and
    /// ground snapping. In camera mode (<see cref="CameraModeController"/>) it creeps at a reduced speed, can't
    /// sprint, and look input is damped and lightly smoothed so the camera feels like it has weight.
    /// Reports itself to wildlife through <see cref="PlayerPresence"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private PlayerCameraRig cameraRig;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.0f;
        [SerializeField] private float sprintSpeed = 6.0f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float gravity = -20f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float maxPitch = 85f;
        [Tooltip("Look smoothing time while in camera mode (seconds).")]
        [SerializeField] private float cameraModeLookSmoothing = 0.04f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _yaw, _pitch;
        private Vector2 _smoothedLook, _lookVelocity;
        private CameraModeController _mode;

        public bool IsSprinting { get; private set; }
        public float CurrentSpeed => _horizontalVelocity.magnitude;
        public bool IsGrounded => _controller.isGrounded;
        public float SprintSpeed => sprintSpeed;

        public void Configure(Transform pivot, PlayerCameraRig rig)
        {
            cameraPivot = pivot;
            cameraRig = rig;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
            if (cameraPivot != null) _pitch = NormalizePitch(cameraPivot.localEulerAngles.x);
        }

        private void Start()
        {
            // Resolved in Start: the rig may add camera components during its own Awake.
            if (cameraRig != null) _mode = cameraRig.GetComponent<CameraModeController>();
            LockCursor(true);
        }

        private void OnDisable() => PlayerPresence.Clear();

        private void Update()
        {
            var game = WildLensGame.Instance;
            bool uiOpen = game != null && game.IsUiOpen;
            bool raised = _mode != null && _mode.IsActive;

            if (!uiOpen && (game == null || game.UiClosedFrame != Time.frameCount)) HandleCursor();
            bool canControl = !uiOpen && Cursor.lockState == CursorLockMode.Locked;

            if (canControl) Look();
            Move(canControl ? GameInput.Move : Vector2.zero, canControl && GameInput.SprintHeld && !raised, raised);

            PlayerPresence.Report(transform, transform.position, CurrentSpeed, IsSprinting, raised);
        }

        private void HandleCursor()
        {
            if (GameInput.CancelPressed && Cursor.lockState == CursorLockMode.Locked)
                LockCursor(false);
            else if (GameInput.PrimaryClickPressed && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor(true);
                PhotoCamera.ClickConsumedFrame = Time.frameCount; // this click doesn't also take a photo
            }
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Look()
        {
            float blend = _mode != null ? _mode.Blend : 0f;
            float scale = lookSensitivity * (_mode != null ? _mode.LookSensitivityScale : 1f);
            Vector2 raw = GameInput.Look * scale;
            // Raw look when walking; a touch of smoothing when holding the camera.
            _smoothedLook = Vector2.SmoothDamp(_smoothedLook, raw, ref _lookVelocity, cameraModeLookSmoothing * blend + 0.0001f);
            Vector2 look = Vector2.Lerp(raw, _smoothedLook, blend);
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move(Vector2 input, bool sprint, bool raised)
        {
            float dt = Time.deltaTime;
            IsSprinting = sprint && input.y > 0.1f;
            float speed = raised ? walkSpeed * _mode.MovementSpeedMultiplier : (IsSprinting ? sprintSpeed : walkSpeed);

            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, wish, acceleration * speed * dt);

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f; // keep snapped to slopes
            _verticalVelocity += gravity * dt;

            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalVelocity) * dt;
            _controller.Move(motion);
        }

        private static float NormalizePitch(float x) => x > 180f ? x - 360f : x;
    }
}
