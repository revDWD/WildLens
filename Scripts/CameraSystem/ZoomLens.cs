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
        [SerializeField] private float zoomSmoothTime = 0.16f;
        [SerializeField] private float raiseSmoothTime = 0.2f;
        [Tooltip("Minimum seconds between zoom steps, so one flick of a free-spinning wheel doesn't skip levels.")]
        [SerializeField] private float stepCooldown = 0.08f;

        private Camera _camera;
        private CameraModeController _mode;
        private int _level;
        private float _logTan, _logVelocity;
        private float _scroll, _stepTimer;

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
            bool active = _mode != null && _mode.IsActive;
            var game = WildLensGame.Instance;
            bool canUse = (game == null || !game.IsUiOpen) && Cursor.lockState == CursorLockMode.Locked;
            if (canUse) HandleScroll(dt); // also preselects while lowered
            else _scroll = 0f;

            float targetTan = active ? Tan(cameraModeFov) / zoomSteps[_level] : Tan(walkingFov);
            float smooth = active ? zoomSmoothTime : raiseSmoothTime;
            _logTan = Mathf.SmoothDamp(_logTan, Mathf.Log(targetTan), ref _logVelocity, smooth, Mathf.Infinity, dt);
            _camera.fieldOfView = 2f * Mathf.Atan(Mathf.Exp(_logTan)) * Mathf.Rad2Deg;
        }

        private void HandleScroll(float dt)
        {
            _stepTimer -= dt;
            _scroll = Mathf.Clamp(_scroll + GameInput.Zoom, -1f, 1f);
            if (Mathf.Abs(_scroll) < 0.5f || _stepTimer > 0f) return;
            SetLevel(_level + (_scroll > 0f ? 1 : -1));
            _scroll = 0f;
            _stepTimer = stepCooldown;
        }
    }
}
