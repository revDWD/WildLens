using System;
using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;

namespace WildLens.CameraSystem
{
    /// <summary>
    /// The lens: sole owner of the view camera's field of view.
    ///
    /// Walking uses <c>walkingFov</c>. In camera mode the lens starts at <c>cameraModeFov</c> (1×, ≈26 mm equivalent)
    /// and the mouse wheel steps through discrete zoom levels (default 1×, 2×, 4×, 8×, 16×; 16× ≈ 410 mm, a real
    /// wildlife focal length). Magnification is optical: 2× halves tan(FOV/2) relative to 1×. Transitions are
    /// smoothed in log space, so every step feels equally quick.
    ///
    /// The level is remembered between raises, and the wheel works while the camera is lowered too: it
    /// preselects the level (HUD shows it), so raising goes straight to the zoom you chose.
    /// </summary>
    [DefaultExecutionOrder(-25)]
    [RequireComponent(typeof(Camera))]
    public class ZoomLens : MonoBehaviour
    {
        [SerializeField] private float walkingFov = 65f;
        [Tooltip("Field of view at 1× in camera mode (narrower than walking, so raising the camera visibly tightens the view).")]
        [SerializeField] private float cameraModeFov = 50f;
        // Renamed from "zoomLevels" so scenes saved with the old 1/2/4/8 set pick up the new default.
        [SerializeField] private float[] zoomSteps = { 1f, 2f, 4f, 8f, 16f };
        [SerializeField] private float zoomSmoothTime = 0.12f;
        [SerializeField] private float raiseSmoothTime = 0.2f;
        [Tooltip("Minimum seconds between zoom steps, so one flick of a free-spinning wheel or a trackpad swipe doesn't skip every level.")]
        [SerializeField] private float stepCooldown = 0.09f;

        private Camera _camera;
        private CameraModeController _mode;
        private int _level;
        private float _logTan, _logVelocity;
        private float _stepTimer;
        private float _lastWrittenFov = -1f;

        public IReadOnlyList<float> Levels => zoomSteps;
        public int LevelIndex => _level;
        public float TargetMagnification => zoomSteps[_level];
        /// <summary>Live magnification relative to camera-mode 1× (animates during transitions).</summary>
        public float CurrentMagnification => Tan(cameraModeFov) / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        /// <summary>35 mm-equivalent focal length of the current view (for the viewfinder readout).</summary>
        public float EquivalentFocalLength => 12f / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        /// <summary>Equivalent focal length of the selected level when the camera is raised.</summary>
        public float TargetFocalLength => 12f / (Tan(cameraModeFov) / zoomSteps[_level]);
        /// <summary>Current FOV relative to walking FOV (used to scale look sensitivity).</summary>
        public float FovRatio => _camera != null ? _camera.fieldOfView / walkingFov : 1f;

        /// <summary>FOV the lens is heading for (diagnostics).</summary>
        public float TargetFov { get; private set; }

        // ---- diagnostics (read by CameraDiagnostics) ----
        /// <summary>Raw scroll value seen this frame.</summary>
        public float LastFrameScrollRaw { get; private set; }
        /// <summary>Most recent non-zero raw scroll value and when it arrived (unscaled time).</summary>
        public float LastNonZeroScrollRaw { get; private set; }
        public float LastScrollTime { get; private set; } = -1f;
        /// <summary>Frames with zoom input / steps actually applied / inputs ignored (cooldown, cursor, UI).</summary>
        public int ZoomInputEvents { get; private set; }
        public int ZoomStepsApplied { get; private set; }
        public int ZoomInputsIgnored { get; private set; }
        /// <summary>Why the last zoom input was ignored, if it was.</summary>
        public string LastIgnoreReason { get; private set; } = "";
        /// <summary>Something other than this lens changed the camera's FOV since the last frame.</summary>
        public bool ExternalFovOverrideDetected { get; private set; }

        /// <summary>Raised with the new level index.</summary>
        public event Action<int> LevelChanged;

        private static float Tan(float fovDegrees) => Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _mode = GetComponent<CameraModeController>();
            if (zoomSteps == null || zoomSteps.Length == 0) zoomSteps = new[] { 1f };
            _camera.fieldOfView = walkingFov;
            _logTan = Mathf.Log(Tan(walkingFov));
        }

        private void OnValidate()
        {
            if (zoomSteps == null || zoomSteps.Length == 0) zoomSteps = new[] { 1f };
            for (int i = 0; i < zoomSteps.Length; i++) zoomSteps[i] = Mathf.Max(1f, zoomSteps[i]);
            Array.Sort(zoomSteps);
        }

        public void SetLevel(int index)
        {
            index = Mathf.Clamp(index, 0, zoomSteps.Length - 1);
            if (index == _level) return;
            _level = index;
            LevelChanged?.Invoke(index);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            // Another script writing the FOV would make zoom invisible: detect it instead of fighting silently.
            if (_lastWrittenFov > 0f && Mathf.Abs(_camera.fieldOfView - _lastWrittenFov) > 0.01f) ExternalFovOverrideDetected = true;

            bool active = _mode != null && _mode.IsActive;
            HandleZoomInput(dt); // works while lowered too (preselect)

            float targetTan = active ? Tan(cameraModeFov) / zoomSteps[_level] : Tan(walkingFov);
            TargetFov = 2f * Mathf.Atan(targetTan) * Mathf.Rad2Deg;
            float smooth = active ? zoomSmoothTime : raiseSmoothTime;
            _logTan = Mathf.SmoothDamp(_logTan, Mathf.Log(targetTan), ref _logVelocity, smooth, Mathf.Infinity, dt);
            _camera.fieldOfView = 2f * Mathf.Atan(Mathf.Exp(_logTan)) * Mathf.Rad2Deg;
            _lastWrittenFov = _camera.fieldOfView;
        }

        private void HandleZoomInput(float dt)
        {
            _stepTimer -= Time.unscaledDeltaTime;
            float raw = GameInput.ScrollRaw;
            LastFrameScrollRaw = raw;
            if (Mathf.Abs(raw) > 0.0001f) { LastNonZeroScrollRaw = raw; LastScrollTime = Time.unscaledTime; }

            int direction = GameInput.ZoomDirection;
            if (direction == 0) return;
            ZoomInputEvents++;

            var game = WildLensGame.Instance;
            if (game != null && game.IsUiOpen) { Ignore("journal open"); return; }
            if (Cursor.lockState != CursorLockMode.Locked) { Ignore("mouse not captured (click the Game view)"); return; }
            if (_stepTimer > 0f) { Ignore("step cooldown"); return; }

            int before = _level;
            SetLevel(_level + direction);
            if (_level == before) { Ignore(direction > 0 ? "already at max zoom" : "already at 1×"); return; }
            ZoomStepsApplied++;
            LastIgnoreReason = "";
            _stepTimer = stepCooldown;
        }

        private void Ignore(string reason)
        {
            ZoomInputsIgnored++;
            LastIgnoreReason = reason;
        }
    }
}
