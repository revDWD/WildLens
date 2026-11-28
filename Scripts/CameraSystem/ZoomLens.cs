using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WildLens.Core;

namespace WildLens.CameraSystem
{
    /// <summary>
    /// The lens: sole owner of the view camera's field of view.
    ///
    /// Walking uses <c>walkingFov</c>. In camera mode the lens starts at <c>cameraModeFov</c> (1×, ≈26 mm equivalent)
    /// and zoom input steps through discrete levels (default 1×, 2×, 4×, 8×, 16×; 16× ≈ 410 mm). Magnification is
    /// optical: 2× halves tan(FOV/2) relative to 1×, so 16× is a 3.6° view and a distant deer fills ~16× more of the
    /// frame height than at 1×. Transitions are smoothed in log space, so every step feels equally quick.
    ///
    /// Zoom input while the camera is lowered raises it (CameraModeController latch), so a zoom step always
    /// changes the view. (0.3–0.4 only "preselected" while lowered, so the readouts changed but the view didn't.)
    ///
    /// Authority over the projection: the FOV is applied late every frame and re-applied right before this camera
    /// renders, so a script that writes the FOV later in the frame cannot win. Physical-camera mode, a custom
    /// projection matrix and a Cinemachine Brain on this camera would all stop the FOV from controlling the image;
    /// each is detected, corrected and logged once.
    /// </summary>
    [DefaultExecutionOrder(10000)]
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
        [Header("Macro (1.5)")]
        [Tooltip("Field of view of the macro lens at its first step (a short macro, for subjects a hand's length away).")]
        [SerializeField] private float macroFov = 24f;
        [SerializeField] private float[] macroSteps = { 1f, 1.7f };

        private Camera _camera;
        private CameraModeController _mode;
        private int _level;
        private float _logTan, _logVelocity;
        private float _stepTimer;
        private float _appliedFov = -1f;

        public IReadOnlyList<float> Levels => zoomSteps;
        public int LevelIndex => _level;
        public float TargetMagnification => zoomSteps[_level];
        public Camera Camera => _camera;
        /// <summary>Live magnification relative to camera-mode 1× (animates during transitions).</summary>
        public float CurrentMagnification => Tan(cameraModeFov) / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        /// <summary>35 mm-equivalent focal length of the current view (for the viewfinder readout).</summary>
        public float EquivalentFocalLength => 12f / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        /// <summary>Equivalent focal length of the selected level when the camera is raised.</summary>
        public float TargetFocalLength => 12f / (Tan(cameraModeFov) / zoomSteps[_level]);
        /// <summary>Current FOV relative to walking FOV (used to scale look sensitivity).</summary>
        public float FovRatio => _camera != null ? _camera.fieldOfView / walkingFov : 1f;
        /// <summary>FOV at 1× in camera mode (diagnostics and self-test).</summary>
        public float CameraModeFov => cameraModeFov;
        /// <summary>FOV the lens is heading for.</summary>
        public float TargetFov { get; private set; }
        /// <summary>FOV the lens applied this frame.</summary>
        public float AppliedFov => _appliedFov;

        // ---- diagnostics (read by CameraDiagnostics) ----
        public float LastFrameScrollRaw { get; private set; }
        public float LastNonZeroScrollRaw { get; private set; }
        public float LastScrollTime { get; private set; } = -1f;
        public int ZoomInputEvents { get; private set; }
        public int ZoomStepsApplied { get; private set; }
        public int ZoomInputsIgnored { get; private set; }
        public string LastIgnoreReason { get; private set; } = "";
        /// <summary>Something other than this lens changed the camera's FOV between our writes.</summary>
        public bool ExternalFovOverrideDetected { get; private set; }
        /// <summary>Times the FOV had to be re-applied just before rendering (another writer ran late in the frame).</summary>
        public int RenderTimeCorrections { get; private set; }
        /// <summary>Human-readable list of projection problems found and corrected.</summary>
        public string Corrections { get; private set; } = "";

        /// <summary>Raised with the new level index.</summary>
        public event Action<int> LevelChanged;

        /// <summary>Macro mode (1.5): a close-focus lens for small subjects; M toggles it with the camera raised.</summary>
        public bool MacroMode { get; private set; }
        public int MacroLevel => _macroLevel;
        public event Action<bool> MacroChanged;
        private int _macroLevel;

        public void SetMacro(bool on)
        {
            if (MacroMode == on) return;
            MacroMode = on;
            _macroLevel = 0;
            MacroChanged?.Invoke(on);
        }

        private static float Tan(float fovDegrees) => Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);

        /// <summary>Vertical FOV actually encoded in a camera's projection matrix.</summary>
        public static float ProjectionFov(Camera cam)
        {
            float m11 = cam.projectionMatrix.m11;
            return m11 > 0f ? 2f * Mathf.Atan(1f / m11) * Mathf.Rad2Deg : cam.fieldOfView;
        }

        // Lazy: components added at runtime run Awake inside AddComponent, before later siblings exist.
        // 0.2–0.4 cached CameraModeController in Awake; in scenes upgraded at runtime it was always null,
        // so camera mode never counted as active here and the FOV never left the walking value.
        private CameraModeController Mode => _mode != null ? _mode : (_mode = GetComponent<CameraModeController>());

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (zoomSteps == null || zoomSteps.Length == 0) zoomSteps = new[] { 1f };
            _camera.fieldOfView = walkingFov;
            _logTan = Mathf.Log(Tan(walkingFov));
            TargetFov = walkingFov;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Camera.onPreCull += OnPreCullCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Camera.onPreCull -= OnPreCullCamera;
        }

        private void Start()
        {
            GuardProjection(true);
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
            // M: macro on/off. Like zooming, it raises the camera; lowering the camera leaves macro.
            var mode = Mode;
            var game = WildLensGame.Instance;
            if (GameInput.MacroPressed && Cursor.lockState == CursorLockMode.Locked && (game == null || !game.IsUiOpen))
            {
                if (mode != null && !mode.IsActive) mode.Latch();
                SetMacro(!MacroMode);
            }
            if (MacroMode && mode != null && !mode.IsActive && !mode.IsLatched) SetMacro(false);
            HandleZoomInput();
        }

        // Late in the frame (execution order 10000): anything that ran earlier this frame is overwritten.
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (_appliedFov > 0f && Mathf.Abs(_camera.fieldOfView - _appliedFov) > 0.01f) ExternalFovOverrideDetected = true;

            bool active = Mode != null && Mode.IsActive;
            float targetTan = active ? Tan(cameraModeFov) / zoomSteps[_level] : Tan(walkingFov);
            if (active && MacroMode) targetTan = Tan(macroFov) / macroSteps[Mathf.Clamp(_macroLevel, 0, macroSteps.Length - 1)];
            TargetFov = 2f * Mathf.Atan(targetTan) * Mathf.Rad2Deg;
            float smooth = active ? zoomSmoothTime : raiseSmoothTime;
            _logTan = Mathf.SmoothDamp(_logTan, Mathf.Log(targetTan), ref _logVelocity, smooth, Mathf.Infinity, dt);
            _appliedFov = 2f * Mathf.Atan(Mathf.Exp(_logTan)) * Mathf.Rad2Deg;
            _camera.fieldOfView = _appliedFov;
            GuardProjection(false);
        }

        // Final say right before rendering (SRP and Built-in).
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam) => EnforceAtRender(cam);
        private void OnPreCullCamera(Camera cam) => EnforceAtRender(cam);

        private void EnforceAtRender(Camera cam)
        {
            if (cam != _camera || _appliedFov <= 0f) return;
            if (Mathf.Abs(cam.fieldOfView - _appliedFov) > 0.01f)
            {
                cam.fieldOfView = _appliedFov;
                RenderTimeCorrections++;
                ExternalFovOverrideDetected = true;
            }
        }

        /// <summary>Makes sure fieldOfView actually drives this camera's projection.</summary>
        private void GuardProjection(bool includeComponents)
        {
            if (_camera.usePhysicalProperties)
            {
                // Physical cameras derive the projection from focal length and sensor size.
                _camera.usePhysicalProperties = false;
                Report("Physical Camera mode was on (it overrides Field of View); turned it off.");
            }
            if (Mathf.Abs(ProjectionFov(_camera) - _camera.fieldOfView) > 0.5f && !_camera.orthographic)
            {
                // A script assigned camera.projectionMatrix; while set, fieldOfView is ignored.
                _camera.ResetProjectionMatrix();
                Report("A custom projection matrix was overriding Field of View; reset it.");
            }
            if (_camera.orthographic)
            {
                _camera.orthographic = false;
                Report("Camera was orthographic (no FOV); switched to perspective.");
            }
            if (!includeComponents) return;
            foreach (var behaviour in GetComponents<Behaviour>())
            {
                if (behaviour == null || !behaviour.enabled) continue;
                string type = behaviour.GetType().Name;
                if (type == "CinemachineBrain")
                {
                    // Cinemachine would write the lens every frame from a virtual camera.
                    behaviour.enabled = false;
                    Report("A CinemachineBrain on the player camera was driving its lens; disabled it (WildLens owns this camera).");
                }
            }
        }

        private void Report(string message)
        {
            if (Corrections.Contains(message)) return;
            Corrections += (Corrections.Length > 0 ? "\n" : "") + message;
            Debug.LogWarning("[WildLens Zoom] " + message);
        }

        private void HandleZoomInput()
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
            _stepTimer = stepCooldown;

            var mode = Mode;
            if (mode != null && !mode.IsActive)
            {
                // Zooming implies looking through the camera: raise it, then apply the step.
                mode.Latch();
            }
            else if (mode != null && mode.IsLatched && direction < 0 && _level == 0)
            {
                // Zooming out past 1× lowers a camera that was raised by zooming.
                mode.Unlatch();
                LastIgnoreReason = "";
                return;
            }

            if (MacroMode)
            {
                // The macro lens has its own short range; zooming out past its first step goes back to the telephoto.
                if (direction < 0 && _macroLevel == 0) { SetMacro(false); LastIgnoreReason = ""; return; }
                int was = _macroLevel;
                _macroLevel = Mathf.Clamp(_macroLevel + direction, 0, macroSteps.Length - 1);
                if (_macroLevel == was) { Ignore("macro lens at its closest"); return; }
                ZoomStepsApplied++;
                LastIgnoreReason = "";
                return;
            }

            int before = _level;
            SetLevel(_level + direction);
            if (_level == before) { Ignore(direction > 0 ? "already at max zoom" : "already at 1×"); return; }
            ZoomStepsApplied++;
            LastIgnoreReason = "";
        }

        private void Ignore(string reason)
        {
            ZoomInputsIgnored++;
            LastIgnoreReason = reason;
        }
    }
}
