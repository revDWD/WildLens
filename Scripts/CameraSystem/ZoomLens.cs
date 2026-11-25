using System;
using System.Collections.Generic;
using UnityEngine;
using WildLens.Core;

namespace WildLens.CameraSystem
{
    /// <summary>
    /// The lens: sole owner of the view camera's field of view.
    ///
    /// Walking uses <c>walkingFov</c>. In camera mode the lens starts at <c>cameraModeFov</c> (1×) and the mouse
    /// wheel steps through discrete zoom levels (default 1×, 2×, 4×, 8×). Magnification is defined optically:
    /// 2× halves tan(FOV/2) relative to 1×. Transitions are smoothed in log space, so every step feels the same
    /// speed whether it's 1→2× or 4→8×. The zoom level is remembered between raises, like a real lens.
    /// </summary>
    [DefaultExecutionOrder(-25)]
    [RequireComponent(typeof(Camera))]
    public class ZoomLens : MonoBehaviour
    {
        [SerializeField] private float walkingFov = 65f;
        [Tooltip("Field of view at 1× in camera mode (narrower than walking, so raising the camera visibly tightens the view).")]
        [SerializeField] private float cameraModeFov = 50f;
        [SerializeField] private float[] zoomLevels = { 1f, 2f, 4f, 8f };
        [SerializeField] private float zoomSmoothTime = 0.16f;
        [SerializeField] private float raiseSmoothTime = 0.2f;
        [Tooltip("Minimum seconds between zoom steps, so one flick of a free-spinning wheel doesn't skip levels.")]
        [SerializeField] private float stepCooldown = 0.08f;

        private Camera _camera;
        private CameraModeController _mode;
        private int _level;
        private float _logTan, _logVelocity;
        private float _scroll, _stepTimer;

        public IReadOnlyList<float> Levels => zoomLevels;
        public int LevelIndex => _level;
        public float TargetMagnification => zoomLevels[_level];
        /// <summary>Live magnification relative to camera-mode 1× (animates during transitions).</summary>
        public float CurrentMagnification => Tan(cameraModeFov) / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        /// <summary>Current FOV relative to walking FOV (used to scale look sensitivity).</summary>
        public float FovRatio => _camera != null ? _camera.fieldOfView / walkingFov : 1f;

        /// <summary>Raised with the new level index.</summary>
        public event Action<int> LevelChanged;

        private static float Tan(float fovDegrees) => Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _mode = GetComponent<CameraModeController>();
            if (zoomLevels == null || zoomLevels.Length == 0) zoomLevels = new[] { 1f };
            _camera.fieldOfView = walkingFov;
            _logTan = Mathf.Log(Tan(walkingFov));
        }

        private void OnValidate()
        {
            if (zoomLevels == null || zoomLevels.Length == 0) zoomLevels = new[] { 1f };
            for (int i = 0; i < zoomLevels.Length; i++) zoomLevels[i] = Mathf.Max(1f, zoomLevels[i]);
            Array.Sort(zoomLevels);
        }

        public void SetLevel(int index)
        {
            index = Mathf.Clamp(index, 0, zoomLevels.Length - 1);
            if (index == _level) return;
            _level = index;
            LevelChanged?.Invoke(index);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool active = _mode != null && _mode.IsActive;
            if (active) HandleScroll(dt);
            else _scroll = 0f;

            float targetTan = active ? Tan(cameraModeFov) / zoomLevels[_level] : Tan(walkingFov);
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
