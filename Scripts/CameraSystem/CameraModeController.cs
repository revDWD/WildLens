using System;
using UnityEngine;
using WildLens.Core;

namespace WildLens.CameraSystem
{
    /// <summary>
    /// Camera Mode: hold the right mouse button to raise the camera to your eye, release to lower it.
    ///
    /// While active:
    ///   • the player moves at <see cref="MovementSpeedMultiplier"/> of walking speed and cannot sprint,
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
        [SerializeField, Range(0.1f, 1f)] private float movementSpeedMultiplier = 0.45f;
        [SerializeField, Range(0.3f, 1f)] private float lookSensitivityMultiplier = 0.8f;
        [SerializeField] private float raiseDuration = 0.22f;
        [Tooltip("How far into the raise (0-1) the shutter becomes usable.")]
        [SerializeField, Range(0f, 1f)] private float readyThreshold = 0.75f;

        private ZoomLens _lens;
        private float _raw;

        public bool IsActive { get; private set; }
        /// <summary>Eased 0 (lowered) → 1 (at the eye).</summary>
        public float Blend { get; private set; }
        /// <summary>Raised far enough to take a photograph.</summary>
        public bool IsReady => IsActive && _raw >= readyThreshold;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;

        /// <summary>Look sensitivity multiplier: slightly reduced in camera mode, then scaled by zoom so framing stays controllable.</summary>
        public float LookSensitivityScale =>
            Mathf.Lerp(1f, lookSensitivityMultiplier, Blend) * (_lens != null ? _lens.FovRatio : 1f);

        public event Action Entered;
        public event Action Exited;

        private void Awake() => _lens = GetComponent<ZoomLens>();

        private void Update()
        {
            var game = WildLensGame.Instance;
            bool canUse = (game == null || !game.IsUiOpen) && Cursor.lockState == CursorLockMode.Locked;
            bool want = canUse && GameInput.CameraRaiseHeld;

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
