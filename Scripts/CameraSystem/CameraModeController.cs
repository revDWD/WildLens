using System;
using UnityEngine;
using WildLens.Core;

namespace WildLens.CameraSystem
{
    /// <summary>
    /// Camera Mode: hold the right mouse button to raise the camera to your eye, release to lower it.
    /// Scrolling (or E/Q) while lowered also raises it and keeps it up ("latched") until you click RMB, sprint,
    /// zoom out past 1×, or open a menu, so zooming always changes what you see.
    ///
    /// While active:
    ///   • the player moves at <see cref="MovementSpeedMultiplier"/> of walking or crouching speed and cannot sprint,
    ///   • mouse look is slightly damped (<see cref="LookSensitivityScale"/>; further reduced with zoom),
    ///   • <see cref="ZoomLens"/> narrows the field of view and accepts zoom input,
    ///   • the HUD shows the viewfinder, and photos can be taken (<see cref="IsReady"/>).
    ///
    /// <see cref="Blend"/> eases 0→1 over <c>raiseDuration</c> so the UI, FOV and handling all transition together,
    /// like physically lifting a camera. Other systems read this component; it reads only input.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class CameraModeController : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] private float movementSpeedMultiplier = 0.5f;
        [SerializeField, Range(0.3f, 1f)] private float lookSensitivityMultiplier = 0.8f;
        [SerializeField] private float raiseDuration = 0.22f;
        [Tooltip("How far into the raise (0-1) the shutter becomes usable.")]
        [SerializeField, Range(0f, 1f)] private float readyThreshold = 0.75f;

        /// <summary>Set while something makes raising the camera impossible (riding the trail bike).</summary>
        public static bool RaiseBlocked { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => RaiseBlocked = false;

        private ZoomLens _lens;
        private float _raw;
        private bool _latched;
        private bool _heldLastFrame;

        public bool IsActive { get; private set; }
        /// <summary>Raised by zoom input rather than by holding RMB.</summary>
        public bool IsLatched => _latched;
        /// <summary>Eased 0 (lowered) → 1 (at the eye).</summary>
        public float Blend { get; private set; }
        /// <summary>Raised far enough to take a photograph.</summary>
        public bool IsReady => IsActive && _raw >= readyThreshold;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;

        /// <summary>Look sensitivity multiplier: slightly reduced in camera mode, then scaled by zoom so framing stays controllable.</summary>
        public float LookSensitivityScale =>
            Mathf.Lerp(1f, lookSensitivityMultiplier, Blend) * (Lens != null ? Lens.FovRatio : 1f);

        public event Action Entered;
        public event Action Exited;

        // Resolved lazily: when components are added at runtime, Awake order can't be relied on.
        private ZoomLens Lens => _lens != null ? _lens : (_lens = GetComponent<ZoomLens>());

        /// <summary>Raise the camera without RMB held (zoom input while lowered).</summary>
        public void Latch() => _latched = true;

        /// <summary>Lower a latched camera.</summary>
        public void Unlatch() => _latched = false;

        private void Update()
        {
            var game = WildLensGame.Instance;
            bool canUse = (game == null || !game.IsUiOpen) && Cursor.lockState == CursorLockMode.Locked && !RaiseBlocked;
            bool held = GameInput.CameraRaiseHeld;
            // Pressing RMB hands control back to hold-to-raise; sprinting or a menu lowers a latched camera.
            if (!canUse || (held && !_heldLastFrame) || GameInput.SprintHeld) _latched = false;
            _heldLastFrame = held;
            bool want = canUse && (held || _latched);

            if (want != IsActive)
            {
                IsActive = want;
                if (want) Entered?.Invoke(); else Exited?.Invoke();
            }

            _raw = Mathf.MoveTowards(_raw, IsActive ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, raiseDuration));
            Blend = Mathf.SmoothStep(0f, 1f, _raw);
        }
    }
}
