using UnityEngine;
using WildLens.CameraSystem;
using WildLens.Core;
using WildLens.Photography;

namespace WildLens.Player
{
    /// <summary>
    /// First-person controller: mouse look, walk / sprint / crouch with smoothed acceleration, gravity and
    /// ground snapping. Reports itself to wildlife through <see cref="PlayerPresence"/>.
    ///
    /// Crouch (C toggles, hold Left Ctrl) is the stalking tool: half speed, half the footfall noise, a smaller
    /// silhouette, and a lower head that bushes and rocks can hide. The capsule and camera lower smoothly; the
    /// player won't stand up under something solid. Sprinting cancels crouch.
    ///
    /// In camera mode (<see cref="CameraModeController"/>) speed is further reduced, sprint is unavailable, and
    /// look input is damped and lightly smoothed so the camera feels like it has weight.
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
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float gravity = -20f;

        [Header("Crouch")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.1f;
        [Tooltip("Camera pivot sits this far below the top of the capsule.")]
        [SerializeField] private float eyeOffsetFromTop = 0.15f;
        [SerializeField] private float crouchTransitionTime = 0.18f;

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

        private bool _crouchToggled;
        private float _height, _heightVelocity;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        /// <summary>0 standing … 1 fully crouched (animated).</summary>
        public float CrouchBlend => Mathf.InverseLerp(standingHeight, crouchHeight, _height);
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
            _height = standingHeight;
            ApplyHeight();
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

            bool wantSprint = canControl && GameInput.SprintHeld && !raised;
            if (canControl) UpdateCrouch(wantSprint);
            if (canControl) Look();
            Move(canControl ? GameInput.Move : Vector2.zero, wantSprint && !IsCrouching, raised);
            AnimateHeight();

            float eye = _height - eyeOffsetFromTop;
            PlayerPresence.Report(transform, transform.position, CurrentSpeed, IsSprinting, IsCrouching, raised, eye);
        }

        // ------------------------------------------------------------------ crouch

        private void UpdateCrouch(bool wantSprint)
        {
            if (GameInput.CrouchTogglePressed) _crouchToggled = !_crouchToggled;
            if (wantSprint && GameInput.Move.y > 0.1f) _crouchToggled = false; // sprinting stands you up

            bool want = _crouchToggled || GameInput.CrouchHeld;
            if (!want && IsCrouching && !HasHeadroom()) want = true; // can't stand under a log or branch
            IsCrouching = want;
        }

        private bool HasHeadroom()
        {
            float radius = _controller.radius * 0.9f;
            Vector3 bottom = transform.position + Vector3.up * (_height - radius);
            float distance = standingHeight - _height + 0.05f;
            return !Physics.SphereCast(bottom, radius, Vector3.up, out _, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private void AnimateHeight()
        {
            float target = IsCrouching ? crouchHeight : standingHeight;
            _height = Mathf.SmoothDamp(_height, target, ref _heightVelocity, crouchTransitionTime);
            ApplyHeight();
        }

        private void ApplyHeight()
        {
            _controller.height = _height;
            _controller.center = new Vector3(0f, _height * 0.5f, 0f);
            if (cameraPivot != null)
            {
                Vector3 p = cameraPivot.localPosition;
                cameraPivot.localPosition = new Vector3(p.x, _height - eyeOffsetFromTop, p.z);
            }
        }

        // ------------------------------------------------------------------ cursor & look

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

        // ------------------------------------------------------------------ movement

        private void Move(Vector2 input, bool sprint, bool raised)
        {
            float dt = Time.deltaTime;
            IsSprinting = sprint && input.y > 0.1f;
            float baseSpeed = IsCrouching ? crouchSpeed : walkSpeed;
            float speed = raised && _mode != null ? baseSpeed * _mode.MovementSpeedMultiplier : (IsSprinting ? sprintSpeed : baseSpeed);

            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, wish, acceleration * Mathf.Max(speed, walkSpeed) * dt);

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f; // keep snapped to slopes
            _verticalVelocity += gravity * dt;

            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalVelocity) * dt;
            _controller.Move(motion);
        }

        private static float NormalizePitch(float x) => x > 180f ? x - 360f : x;
    }
}
