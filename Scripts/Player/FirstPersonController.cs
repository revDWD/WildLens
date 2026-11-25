using UnityEngine;
using WildLens.Core;
using WildLens.Photography;

namespace WildLens.Player
{
    /// <summary>
    /// First-person walking controller: mouse look, walk/sprint with smoothed acceleration, gravity and
    /// ground snapping. Slows to a careful pace while the camera is raised. Reports itself to wildlife
    /// through <see cref="PlayerPresence"/>.
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
        [SerializeField] private float cameraRaisedSpeed = 1.4f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float gravity = -20f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float maxPitch = 85f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _yaw, _pitch;

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
            LockCursor(true);
        }

        private void OnDisable() => PlayerPresence.Clear();

        private void Update()
        {
            var game = WildLensGame.Instance;
            bool uiOpen = game != null && game.IsUiOpen;
            bool raised = cameraRig != null && cameraRig.IsRaised;

            if (!uiOpen && (game == null || game.UiClosedFrame != Time.frameCount)) HandleCursor();
            bool canControl = !uiOpen && Cursor.lockState == CursorLockMode.Locked;

            if (canControl) Look();
            Move(canControl ? GameInput.Move : Vector2.zero, canControl && GameInput.SprintHeld && !raised, raised);

            PlayerPresence.Report(transform.position, CurrentSpeed, IsSprinting, raised);
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
            float scale = lookSensitivity * (cameraRig != null ? cameraRig.LookSensitivityScale : 1f);
            Vector2 look = GameInput.Look * scale;
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move(Vector2 input, bool sprint, bool raised)
        {
            float dt = Time.deltaTime;
            IsSprinting = sprint && input.y > 0.1f;
            float speed = raised ? cameraRaisedSpeed : (IsSprinting ? sprintSpeed : walkSpeed);

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
