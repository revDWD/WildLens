using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using WildLens.CameraSystem;
using WildLens.Core;
using WildLens.Photography;
#if ENABLE_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM_1_9
using UnityEngine.InputSystem;
#endif

namespace WildLens.Debugging
{
    /// <summary>
    /// Camera and zoom diagnostics.
    ///
    /// F3: overlay. Input backend, raw scroll and how it's interpreted, zoom inputs received / applied / ignored (and why),
    /// zoom level, applied and target FOV, focal length, camera mode, and, from a render-time probe, which camera(s)
    /// actually drew to the screen this frame together with the FOV encoded in the projection they rendered with.
    /// Mismatches (another camera on screen, projection ≠ fieldOfView) are shown in red and logged once.
    ///
    /// F4: zoom self-test. Raises the camera, steps through every zoom level, and at each one records the camera that
    /// rendered the frame, its projection FOV, the measured magnification relative to 1×, and how tall the animal
    /// nearest the centre of view appears on screen; saves a screenshot per level to
    /// persistentDataPath/WildLens/ZoomTest/ and prints PASS/FAIL with the reason.
    /// </summary>
    public class CameraDiagnostics : MonoBehaviour
    {
        [SerializeField] private bool visibleOnStart;

        private struct RenderRecord
        {
            public string name;
            public bool isPlayer;
            public float fieldOfView;
            public float projectionFov;
        }

        private Camera _camera;
        private ZoomLens _lens;
        private CameraModeController _mode;
        private bool _visible;
        private bool _warnedCamera, _warnedProjection;
        private GUIStyle _style;
        private readonly StringBuilder _sb = new StringBuilder(2048);

        private readonly List<RenderRecord> _thisFrame = new List<RenderRecord>();
        private readonly List<RenderRecord> _lastFrame = new List<RenderRecord>();
        private int _recordFrame = -1;

        private bool _testRunning;
        private string _testReport = "";

        private ZoomLens Lens => _lens != null ? _lens : (_lens = GetComponent<ZoomLens>());
        private CameraModeController Mode => _mode != null ? _mode : (_mode = GetComponent<CameraModeController>());

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _visible = visibleOnStart;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Camera.onPreRender += OnPreRenderCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Camera.onPreRender -= OnPreRenderCamera;
        }

        // ------------------------------------------------------------------ render probe

        private void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam) => Record(cam);
        private void OnPreRenderCamera(Camera cam) => Record(cam);

        private void Record(Camera cam)
        {
            if (cam == null || cam.cameraType != CameraType.Game || cam.targetTexture != null) return;
            if (Time.frameCount != _recordFrame)
            {
                _lastFrame.Clear();
                _lastFrame.AddRange(_thisFrame);
                _thisFrame.Clear();
                _recordFrame = Time.frameCount;
            }
            _thisFrame.Add(new RenderRecord
            {
                name = cam.name,
                isPlayer = cam == _camera,
                fieldOfView = cam.fieldOfView,
                projectionFov = ZoomLens.ProjectionFov(cam),
            });
        }

        /// <summary>Screen cameras from the most recent complete frame.</summary>
        private List<RenderRecord> ScreenCameras => _lastFrame.Count > 0 ? _lastFrame : _thisFrame;

        private string ProbeProblem()
        {
            var cams = ScreenCameras;
            if (cams.Count == 0) return "";
            var last = cams[cams.Count - 1]; // rendered last = on top
            if (!last.isPlayer)
                return $"'{last.name}' renders to the screen after the player camera and covers it: zoom happens on a camera you can't see. Disable '{last.name}'.";
            if (Mathf.Abs(last.projectionFov - last.fieldOfView) > 0.5f)
                return $"Player camera renders with a {last.projectionFov:0.0}° projection although its FOV is {last.fieldOfView:0.0}°: something overrides the projection.";
            return "";
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            if (GameInput.DiagnosticsTogglePressed) _visible = !_visible;
            if (GameInput.ZoomSelfTestPressed && !_testRunning) StartCoroutine(SelfTest());

            string problem = ProbeProblem();
            if (!string.IsNullOrEmpty(problem))
            {
                bool cameraIssue = problem.Contains("covers it");
                if (cameraIssue && !_warnedCamera) { _warnedCamera = true; Debug.LogWarning("[WildLens Zoom] " + problem); }
                if (!cameraIssue && !_warnedProjection) { _warnedProjection = true; Debug.LogWarning("[WildLens Zoom] " + problem); }
            }

            if (_visible && Lens != null && Mathf.Abs(Lens.LastFrameScrollRaw) > 0.0001f)
                Debug.Log($"[WildLens Zoom] raw scroll {Lens.LastFrameScrollRaw:0.###} → level {Lens.LevelIndex} ({Lens.TargetMagnification:0.#}×), " +
                          $"FOV {_camera.fieldOfView:0.0}° → {Lens.TargetFov:0.0}°, camera mode {(Mode != null && Mode.IsActive ? "on" : "off")}" +
                          (string.IsNullOrEmpty(Lens.LastIgnoreReason) ? "" : $", ignored: {Lens.LastIgnoreReason}"));
        }

        // ------------------------------------------------------------------ self-test

        private IEnumerator SelfTest()
        {
            _testRunning = true;
            _visible = true;
            var lens = Lens;
            var mode = Mode;
            if (lens == null || mode == null) { _testReport = "Self-test: ZoomLens / CameraModeController missing."; _testRunning = false; yield break; }

            int startLevel = lens.LevelIndex;
            bool wasLatched = mode.IsLatched;
            mode.Latch();
            string folder = Path.Combine(Application.persistentDataPath, "WildLens", "ZoomTest");
            Directory.CreateDirectory(folder);

            var rows = new List<string>();
            float fov1 = 0f;
            float fill1 = -1f;
            bool pass = true;
            string failure = "";
            Photographable subject = PickSubject();

            for (int i = 0; i < lens.Levels.Count; i++)
            {
                lens.SetLevel(i);
                float wait = Time.realtimeSinceStartup + 0.7f; // let the lens settle (0.12 s smoothing)
                while (Time.realtimeSinceStartup < wait) yield return null;
                yield return new WaitForEndOfFrame();

                var cams = ScreenCameras;
                var screen = cams.Count > 0 ? cams[cams.Count - 1] : default;
                float projFov = cams.Count > 0 ? screen.projectionFov : ZoomLens.ProjectionFov(_camera);
                if (i == 0) fov1 = projFov;
                float magnification = Mathf.Tan(fov1 * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(projFov * 0.5f * Mathf.Deg2Rad);
                float expectedFov = 2f * Mathf.Atan(Mathf.Tan(lens.CameraModeFov * 0.5f * Mathf.Deg2Rad) / lens.Levels[i]) * Mathf.Rad2Deg;

                float fill = SubjectHeightFraction(subject);
                if (i == 0) fill1 = fill;
                string fillText = fill < 0f ? "no animal in view" : $"animal {fill * 100f:0.0}% of frame height" + (fill1 > 0f && i > 0 ? $" ({fill / fill1:0.0}× vs 1×)" : "");

                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, $"zoom_{lens.Levels[i]:0.#}x.png"), shot.EncodeToPNG());
                Destroy(shot);

                rows.Add($"{lens.Levels[i],4:0.#}×  rendered by '{(cams.Count > 0 ? screen.name : "?")}'  projection {projFov,6:0.00}° (expected {expectedFov:0.00}°)  magnification {magnification,5:0.0}×  {fillText}");
                if (cams.Count > 0 && !screen.isPlayer) { pass = false; failure = $"screen is rendered by '{screen.name}', not the player camera"; }
                else if (Mathf.Abs(projFov - expectedFov) > 0.5f) { pass = false; failure = $"at {lens.Levels[i]:0.#}× the rendered projection is {projFov:0.0}°, expected {expectedFov:0.0}°"; }
            }

            lens.SetLevel(startLevel);
            if (!wasLatched) mode.Unlatch();

            _testReport = $"<b>Zoom self-test: {(pass ? "<color=#80FF80>PASS</color>" : "<color=#FF8080>FAIL</color>")}</b>" +
                          (pass ? "" : $"  ({failure})") + "\n" + string.Join("\n", rows) +
                          $"\nScreenshots: {folder}";
            Debug.Log("[WildLens Zoom] " + _testReport.Replace("<b>", "").Replace("</b>", "").Replace("<color=#80FF80>", "").Replace("<color=#FF8080>", "").Replace("</color>", ""));
            _testRunning = false;
        }

        /// <summary>The visible animal closest to the view direction (within 25°), if any.</summary>
        private Photographable PickSubject()
        {
            Photographable best = null;
            float bestAngle = 25f;
            foreach (var p in Photographable.Active)
            {
                if (p == null || !p.TryGetWorldBounds(out var b)) continue;
                Vector3 to = b.center - _camera.transform.position;
                if (to.magnitude > 250f) continue;
                float angle = Vector3.Angle(_camera.transform.forward, to);
                if (angle < bestAngle) { bestAngle = angle; best = p; }
            }
            return best;
        }

        private float SubjectHeightFraction(Photographable p)
        {
            if (p == null || !p.TryGetWorldBounds(out var b)) return -1f;
            Vector3 top = _camera.WorldToViewportPoint(b.center + Vector3.up * b.extents.y);
            Vector3 bottom = _camera.WorldToViewportPoint(b.center - Vector3.up * b.extents.y);
            if (top.z <= 0f) return -1f;
            return Mathf.Abs(top.y - bottom.y);
        }

        // ------------------------------------------------------------------ overlay

        private static string Interpret(float raw)
        {
            float a = Mathf.Abs(raw);
            if (a < 0.0001f) return "none yet";
            if (a >= 60f) return "≈±120 per notch (platform range)";
            if (a >= 0.9f && a <= 1.1f) return "≈±1 per notch (normalised)";
            return "fractional (trackpad or high-resolution wheel)";
        }

        private void OnGUI()
        {
            if (!_visible || Lens == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _style.normal.textColor = Color.white;
            }
            var lens = Lens;
            var mode = Mode;

            _sb.Clear();
            _sb.Append("<b>WildLens camera diagnostics</b>  (F3 hide · F4 zoom self-test)\n");
            string stale = WildLens.Environment.SliceInfo.StaleReason();
            _sb.Append($"WildLens {WildLensVersion.Package} · scene: {(stale == null ? "current" : "<color=#ffb347>STALE, " + stale + " (rebuild)</color>")}\n");
            _sb.Append("Input backend: ").Append(GameInput.Backend).Append('\n');
#if ENABLE_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM_1_9
            _sb.Append("Scroll delta behaviour: ").Append(InputSystem.settings.scrollDeltaBehavior).Append('\n');
#endif
            float age = lens.LastScrollTime < 0f ? -1f : Time.unscaledTime - lens.LastScrollTime;
            _sb.Append($"Last scroll: {lens.LastNonZeroScrollRaw:0.###} {(age < 0f ? "" : $"({age:0.0}s ago)")}  {Interpret(lens.LastNonZeroScrollRaw)}\n");
            _sb.Append($"Zoom inputs: {lens.ZoomInputEvents} received, {lens.ZoomStepsApplied} applied, {lens.ZoomInputsIgnored} ignored");
            if (!string.IsNullOrEmpty(lens.LastIgnoreReason)) _sb.Append($" (last: {lens.LastIgnoreReason})");
            _sb.Append('\n');
            _sb.Append($"Camera mode: {(mode != null && mode.IsActive ? (mode.IsLatched ? "ACTIVE (raised by zoom)" : "ACTIVE (RMB)") : "off")}  blend {(mode != null ? mode.Blend : 0f):0.00}\n");
            _sb.Append($"Zoom level: {lens.LevelIndex + 1}/{lens.Levels.Count} = {lens.TargetMagnification:0.#}×   live {lens.CurrentMagnification:0.00}×   ≈{lens.EquivalentFocalLength:0} mm\n");
            _sb.Append($"Lens FOV: applied {lens.AppliedFov:0.00}°  target {lens.TargetFov:0.00}°  camera.fieldOfView {_camera.fieldOfView:0.00}°\n");

            _sb.Append("<b>Rendered to screen</b> (render-time probe):\n");
            var cams = ScreenCameras;
            if (cams.Count == 0) _sb.Append("  (no frames recorded yet)\n");
            for (int i = 0; i < cams.Count; i++)
            {
                var c = cams[i];
                bool bad = !c.isPlayer || Mathf.Abs(c.projectionFov - c.fieldOfView) > 0.5f;
                _sb.Append(bad ? "  <color=#FF8080>" : "  ")
                   .Append($"{i + 1}. '{c.name}'{(c.isPlayer ? " (player)" : "")}: FOV {c.fieldOfView:0.00}°, projection {c.projectionFov:0.00}°")
                   .Append(bad ? "</color>\n" : "\n");
            }
            if (lens.RenderTimeCorrections > 0) _sb.Append($"<color=#FFD080>FOV re-applied at render time {lens.RenderTimeCorrections}×: another script writes it late in the frame</color>\n");
            if (!string.IsNullOrEmpty(lens.Corrections)) _sb.Append("<color=#FFD080>Corrected: ").Append(lens.Corrections.Replace("\n", "\n  ")).Append("</color>\n");
            string problem = ProbeProblem();
            if (!string.IsNullOrEmpty(problem)) _sb.Append("<color=#FF8080>PROBLEM: ").Append(problem).Append("</color>\n");
            if (_testRunning) _sb.Append("<b>Zoom self-test running…</b>\n");
            else if (!string.IsNullOrEmpty(_testReport)) _sb.Append(_testReport).Append('\n');

            var content = new GUIContent(_sb.ToString());
            float width = Mathf.Min(980f, Screen.width - 24f);
            float height = _style.CalcHeight(content, width - 16f);
            var rect = new Rect(12f, 12f, width, height + 12f);
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), content, _style);
        }
    }
}
