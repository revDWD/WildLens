using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WildLens.Core;
using WildLens.Photography;
using WildLens.Player;

namespace WildLens.UI
{
    /// <summary>
    /// In-world HUD: crosshair, camera viewfinder (when raised), shutter flash, post-photo identification
    /// panel, new-species toast, journal counters and control hints. Hidden for the capture frame so it never
    /// appears in photographs.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private PhotoCamera photoCamera;
        [SerializeField] private PlayerCameraRig cameraRig;
        [SerializeField] private float resultDisplaySeconds = 5f;

        private Canvas _canvas;
        private Image _crosshair;
        private CanvasGroup _viewfinder;
        private Text _zoomText;
        private Image _flash;
        private CanvasGroup _resultGroup;
        private RawImage _resultThumb;
        private Text _resultText;
        private CanvasGroup _toastGroup;
        private Text _toastText;
        private Text _statusText;
        private Text _hintsText;
        private Text _pausedText;

        private float _resultTimer, _toastTimer, _flashAlpha;

        public void Configure(PhotoCamera camera, PlayerCameraRig rig)
        {
            photoCamera = camera;
            cameraRig = rig;
        }

        private void Awake()
        {
            if (photoCamera == null) photoCamera = SceneQuery.FindOne<PhotoCamera>();
            if (cameraRig == null) cameraRig = SceneQuery.FindOne<PlayerCameraRig>();
            Build();
        }

        private void OnEnable()
        {
            if (photoCamera != null)
            {
                photoCamera.CaptureStarting += OnCaptureStarting;
                photoCamera.CaptureFinished += OnCaptureFinished;
                photoCamera.PhotoTaken += OnPhotoTaken;
            }
        }

        private void Start()
        {
            var game = WildLensGame.Instance;
            if (game != null) game.Journal.Changed += RefreshStatus;
            RefreshStatus();
        }

        private void OnDisable()
        {
            if (photoCamera != null)
            {
                photoCamera.CaptureStarting -= OnCaptureStarting;
                photoCamera.CaptureFinished -= OnCaptureFinished;
                photoCamera.PhotoTaken -= OnPhotoTaken;
            }
        }

        private void OnDestroy()
        {
            var game = WildLensGame.Instance;
            if (game != null && game.Journal != null) game.Journal.Changed -= RefreshStatus;
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            _canvas = UIFactory.CreateCanvas("HUD Canvas", 0, transform);
            var root = (RectTransform)_canvas.transform;

            _crosshair = UIFactory.CreateImage("Crosshair", root, new Color(1f, 1f, 1f, 0.55f));
            UIFactory.Place(_crosshair.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f));

            BuildViewfinder(root);

            _flash = UIFactory.CreateImage("Flash", root, new Color(1f, 1f, 1f, 0f));
            UIFactory.Stretch(_flash.rectTransform);

            // Identification panel
            var panel = UIFactory.CreateImage("Result Panel", root, UIFactory.Overlay);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(820f, 168f));
            _resultGroup = panel.gameObject.AddComponent<CanvasGroup>();
            _resultGroup.alpha = 0f;
            _resultThumb = UIFactory.CreateRawImage("Thumbnail", panel.transform);
            UIFactory.Place(_resultThumb.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(256f, 144f));
            _resultText = UIFactory.CreateText("Result Text", panel.transform, "", 24, Color.white);
            UIFactory.Stretch(_resultText.rectTransform, 288f, 12f, 16f, 12f);

            // New species toast
            var toastBg = UIFactory.CreateImage("Toast", root, new Color(UIFactory.Paper.r, UIFactory.Paper.g, UIFactory.Paper.b, 0.95f));
            UIFactory.Place(toastBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(640f, 96f));
            _toastGroup = toastBg.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastText = UIFactory.CreateText("Toast Text", toastBg.transform, "", 28, UIFactory.Ink, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_toastText.rectTransform, 12f, 8f, 12f, 8f);

            _statusText = UIFactory.CreateText("Status", root, "", 24, Color.white, TextAnchor.UpperRight);
            UIFactory.Place(_statusText.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(520f, 40f));
            UIFactory.AddShadow(_statusText);

            _hintsText = UIFactory.CreateText("Hints", root,
                "<b>WASD</b> walk   <b>Shift</b> sprint\n<b>Hold RMB</b> raise camera   <b>Scroll</b> zoom\n" +
                "<b>LMB / F</b> take photo   <b>J / Tab</b> journal\n<b>H</b> hide hints   <b>Esc</b> release mouse",
                20, new Color(1f, 1f, 1f, 0.8f), TextAnchor.LowerLeft);
            UIFactory.Place(_hintsText.rectTransform, new Vector2(0f, 0f), new Vector2(28f, 24f), new Vector2(560f, 120f));
            UIFactory.AddShadow(_hintsText);

            _pausedText = UIFactory.CreateText("Paused", root, "Click to resume", 30, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(_pausedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(600f, 50f));
            UIFactory.AddShadow(_pausedText);
        }

        private void BuildViewfinder(RectTransform root)
        {
            var vf = UIFactory.CreateRect("Viewfinder", root);
            UIFactory.Stretch(vf);
            _viewfinder = vf.gameObject.AddComponent<CanvasGroup>();
            _viewfinder.alpha = 0f;

            Color line = new Color(1f, 1f, 1f, 0.9f);
            const float inset = 70f, len = 70f, thick = 3f;
            var corners = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            foreach (var c in corners)
            {
                Vector2 dir = new Vector2(c.x < 0.5f ? 1f : -1f, c.y < 0.5f ? 1f : -1f);
                var h = UIFactory.CreateImage("Corner H", vf, line);
                UIFactory.Place(h.rectTransform, c, dir * inset, new Vector2(len, thick));
                var v = UIFactory.CreateImage("Corner V", vf, line);
                UIFactory.Place(v.rectTransform, c, dir * inset, new Vector2(thick, len));
            }

            // Rule-of-thirds guides
            Color guide = new Color(1f, 1f, 1f, 0.14f);
            for (int i = 1; i <= 2; i++)
            {
                var vline = UIFactory.CreateImage("Third V", vf, guide);
                vline.rectTransform.anchorMin = new Vector2(i / 3f, 0f);
                vline.rectTransform.anchorMax = new Vector2(i / 3f, 1f);
                vline.rectTransform.sizeDelta = new Vector2(1.5f, 0f);
                var hline = UIFactory.CreateImage("Third H", vf, guide);
                hline.rectTransform.anchorMin = new Vector2(0f, i / 3f);
                hline.rectTransform.anchorMax = new Vector2(1f, i / 3f);
                hline.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
            }

            // Focus brackets around centre
            const float f = 36f;
            var ticks = new[] { new Vector2(-f, 0f), new Vector2(f, 0f), new Vector2(0f, -f), new Vector2(0f, f) };
            foreach (var t in ticks)
            {
                var tick = UIFactory.CreateImage("Focus Tick", vf, line);
                bool horizontal = Mathf.Abs(t.x) > 0f;
                UIFactory.Place(tick.rectTransform, new Vector2(0.5f, 0.5f), t, horizontal ? new Vector2(16f, 2f) : new Vector2(2f, 16f));
            }

            _zoomText = UIFactory.CreateText("Zoom", vf, "1.0×", 22, line, TextAnchor.LowerRight);
            UIFactory.Place(_zoomText.rectTransform, new Vector2(1f, 0f), new Vector2(-90f, 84f), new Vector2(200f, 30f));
            UIFactory.AddShadow(_zoomText);
        }

        // ------------------------------------------------------------------ runtime

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var game = WildLensGame.Instance;
            bool uiOpen = game != null && game.IsUiOpen;
            _canvas.gameObject.SetActive(!uiOpen);
            if (uiOpen) return;

            bool raised = cameraRig != null && cameraRig.IsRaised;
            _viewfinder.alpha = Mathf.MoveTowards(_viewfinder.alpha, raised ? 1f : 0f, dt * 6f);
            _crosshair.enabled = !raised;
            if (raised) _zoomText.text = cameraRig.Magnification.ToString("0.0") + "×";

            _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, dt * 3f);
            _flash.color = new Color(1f, 1f, 1f, _flashAlpha * 0.85f);

            _resultTimer -= dt;
            _resultGroup.alpha = Mathf.Clamp01(_resultTimer / 0.5f);
            _toastTimer -= dt;
            _toastGroup.alpha = Mathf.Clamp01(_toastTimer / 0.6f);

            if (GameInput.ToggleHintsPressed) _hintsText.enabled = !_hintsText.enabled;
            _pausedText.enabled = Cursor.lockState != CursorLockMode.Locked;
        }

        private void OnCaptureStarting() => _canvas.enabled = false;

        private void OnCaptureFinished()
        {
            _canvas.enabled = true;
            _flashAlpha = 1f;
        }

        private void OnPhotoTaken(PhotoResult result)
        {
            _resultThumb.texture = result.thumbnail;
            var sb = new StringBuilder();
            if (result.identifications == null || result.identifications.Count == 0)
            {
                sb.Append("<b>No wildlife identified</b>\n<size=20>Get closer, use the zoom, and keep the animal clear of cover.</size>");
            }
            else
            {
                int shown = 0;
                foreach (var id in result.identifications)
                {
                    if (shown++ >= 3) break;
                    sb.Append("<b>").Append(id.species.displayName).Append("</b>  <color=#E8C77A>")
                        .Append(id.subject.quality).Append(" · ").Append(id.subject.score).Append("</color>");
                    if (id.isNewSpecies) sb.Append("  <color=#9FE07A>NEW</color>");
                    else if (id.isNewBest) sb.Append("  <color=#9FD0FF>best shot</color>");
                    sb.Append("\n<size=19><i>").Append(PhotoAnalyzer.DescribeActivity(id.subject.activity)).Append("</i></size>\n");

                    if (id.isNewSpecies) ShowToast(id.species.displayName);
                }
            }
            _resultText.text = sb.ToString().TrimEnd('\n');
            _resultTimer = resultDisplaySeconds;
        }

        private void ShowToast(string speciesName)
        {
            _toastText.text = "<size=20>New species recorded in your journal</size>\n<b>" + speciesName + "</b>";
            _toastTimer = 4f;
        }

        private void RefreshStatus()
        {
            var game = WildLensGame.Instance;
            if (game == null || _statusText == null) return;
            _statusText.text = $"Species {game.Journal.DiscoveredCount}/{game.Journal.TotalSpecies}    Photos {game.Journal.TotalPhotos}";
        }
    }
}
