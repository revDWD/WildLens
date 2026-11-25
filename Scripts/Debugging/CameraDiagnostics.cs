using System.Text;
using UnityEngine;
using WildLens.CameraSystem;
using WildLens.Core;
#if ENABLE_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM_1_9
using UnityEngine.InputSystem;
#endif

namespace WildLens.Debugging
{
    /// <summary>
    /// Optional debug overlay for the camera and zoom (toggle with F3). Shows the input backend, raw scroll values and
    /// how they're interpreted, zoom inputs received / applied / ignored (with the reason), zoom level, live and target
    /// FOV, focal length and camera-mode state, and warns about conditions that would make zoom invisible:
    /// another script writing the FOV, or another camera rendering over the player camera.
    /// While visible, each scroll event is also logged to the Console.
    /// </summary>
    public class CameraDiagnostics : MonoBehaviour
    {
        [SerializeField] private bool visibleOnStart;

        private Camera _camera;
        private ZoomLens _lens;
        private CameraModeController _mode;
        private bool _visible;
        private bool _warnedCamera, _warnedFov;
        private string _cameraWarning = "";
        private float _cameraCheckTimer;
        private GUIStyle _style;
        private readonly StringBuilder _sb = new StringBuilder(1024);

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _lens = GetComponent<ZoomLens>();
            _mode = GetComponent<CameraModeController>();
            _visible = visibleOnStart;
        }

        private void Update()
        {
            if (GameInput.DiagnosticsTogglePressed) _visible = !_visible;

            _cameraCheckTimer -= Time.unscaledDeltaTime;
            if (_cameraCheckTimer <= 0f)
            {
                _cameraCheckTimer = 1f;
                _cameraWarning = CheckCompetingCameras();
                if (!string.IsNullOrEmpty(_cameraWarning) && !_warnedCamera)
                {
                    _warnedCamera = true;
                    Debug.LogWarning("[WildLens] " + _cameraWarning);
                }
            }
            if (_lens != null && _lens.ExternalFovOverrideDetected && !_warnedFov)
            {
                _warnedFov = true;
                Debug.LogWarning("[WildLens] Another component changed the player camera's field of view. ZoomLens must be the only FOV writer, or zoom will appear not to work.");
            }

            if (_visible && _lens != null && Mathf.Abs(_lens.LastFrameScrollRaw) > 0.0001f)
                Debug.Log($"[WildLens Zoom] raw scroll {_lens.LastFrameScrollRaw:0.###} → level {_lens.LevelIndex} ({_lens.TargetMagnification:0.#}×), " +
                          $"FOV {_camera.fieldOfView:0.0}° → {_lens.TargetFov:0.0}°, camera mode {(_mode != null && _mode.IsActive ? "on" : "off")}" +
                          (string.IsNullOrEmpty(_lens.LastIgnoreReason) ? "" : $", ignored: {_lens.LastIgnoreReason}"));
        }

        private string CheckCompetingCameras()
        {
            if (_camera == null) return "";
            if (Camera.main != null && Camera.main != _camera)
                return $"Camera.main is '{Camera.main.name}', not the player camera. Another MainCamera-tagged camera is active.";
            foreach (var c in Camera.allCameras)
            {
                if (c == _camera || c.targetTexture != null || !c.isActiveAndEnabled) continue;
                if (c.depth >= _camera.depth)
                    return $"Camera '{c.name}' (depth {c.depth}) renders after the player camera (depth {_camera.depth}) and may cover it, hiding zoom. Disable it.";
            }
            return "";
        }

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
            if (!_visible || _lens == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _style.normal.textColor = Color.white;
            }

            _sb.Clear();
            _sb.Append("<b>WildLens camera diagnostics</b>  (F3)\n");
            _sb.Append("Input backend: ").Append(GameInput.Backend).Append('\n');
#if ENABLE_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM && WILDLENS_INPUT_SYSTEM_1_9
            _sb.Append("Scroll delta behaviour: ").Append(InputSystem.settings.scrollDeltaBehavior).Append('\n');
#endif
            _sb.Append($"Scroll raw this frame: {_lens.LastFrameScrollRaw:0.###}\n");
            float age = _lens.LastScrollTime < 0f ? -1f : Time.unscaledTime - _lens.LastScrollTime;
            _sb.Append($"Last scroll: {_lens.LastNonZeroScrollRaw:0.###}  {(age < 0f ? "" : $"({age:0.0}s ago)")}  {Interpret(_lens.LastNonZeroScrollRaw)}\n");
            _sb.Append($"Zoom inputs: {_lens.ZoomInputEvents} received, {_lens.ZoomStepsApplied} applied, {_lens.ZoomInputsIgnored} ignored");
            if (!string.IsNullOrEmpty(_lens.LastIgnoreReason)) _sb.Append($"  (last: {_lens.LastIgnoreReason})");
            _sb.Append('\n');
            _sb.Append($"Zoom level: {_lens.LevelIndex + 1}/{_lens.Levels.Count} = {_lens.TargetMagnification:0.#}×   live {_lens.CurrentMagnification:0.00}×\n");
            _sb.Append($"FOV: {_camera.fieldOfView:0.00}°  target {_lens.TargetFov:0.00}°   ≈{_lens.EquivalentFocalLength:0} mm\n");
            _sb.Append($"Camera mode: {(_mode != null && _mode.IsActive ? "ACTIVE" : "off")}  blend {(_mode != null ? _mode.Blend : 0f):0.00}  ready {(_mode != null && _mode.IsReady)}\n");
            _sb.Append($"Cursor: {Cursor.lockState}\n");
            if (_lens.ExternalFovOverrideDetected) _sb.Append("<color=#FF8080>WARNING: another component is changing the FOV</color>\n");
            if (!string.IsNullOrEmpty(_cameraWarning)) _sb.Append("<color=#FF8080>WARNING: ").Append(_cameraWarning).Append("</color>\n");

            var content = new GUIContent(_sb.ToString());
            Vector2 size = _style.CalcSize(content);
            var rect = new Rect(12f, 12f, Mathf.Min(size.x + 16f, Screen.width - 24f), size.y + 12f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), content, _style);
        }
    }
}
