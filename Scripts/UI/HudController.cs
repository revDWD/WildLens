using UnityEngine;
using UnityEngine.UI;
using WildLens.CameraSystem;
using WildLens.Core;
using WildLens.Photography;

namespace WildLens.UI
{
    /// <summary>
    /// Owns the HUD canvas and composes its views:
    ///   <see cref="ViewfinderView"/>    camera-mode overlay and zoom level
    ///   <see cref="ShutterEffectView"/> shutter blackout after each capture
    ///   <see cref="PhotoPreviewView"/>  preview card with species and quality
    /// plus the always-on minimum: crosshair (walking only), journal counters, control hints, "click to resume".
    /// The whole canvas is hidden for the capture frame so it never appears in photographs.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private PhotoCamera photoCamera;

        private Canvas _canvas;
        private Image _crosshair;
        private Text _statusText, _hintsText, _pausedText;
        private ViewfinderView _viewfinder;
        private ShutterEffectView _shutter;
        private PhotoPreviewView _preview;
        private CameraModeController _mode;
        private ZoomLens _lens;

        public void Configure(PhotoCamera camera) => photoCamera = camera;

        private void Start()
        {
            if (photoCamera == null) photoCamera = SceneQuery.FindOne<PhotoCamera>();
            if (photoCamera != null)
            {
                _mode = photoCamera.GetComponent<CameraModeController>();
                _lens = photoCamera.GetComponent<ZoomLens>();
                photoCamera.CaptureStarting += OnCaptureStarting;
                photoCamera.CaptureFinished += OnCaptureFinished;
                photoCamera.PhotoTaken += OnPhotoTaken;
            }
            Build();

            var game = WildLensGame.Instance;
            if (game != null) game.Journal.Changed += RefreshStatus;
            RefreshStatus();
        }

        private void OnDestroy()
        {
            if (photoCamera != null)
            {
                photoCamera.CaptureStarting -= OnCaptureStarting;
                photoCamera.CaptureFinished -= OnCaptureFinished;
                photoCamera.PhotoTaken -= OnPhotoTaken;
            }
            var game = WildLensGame.Instance;
            if (game != null && game.Journal != null) game.Journal.Changed -= RefreshStatus;
        }

        private void Build()
        {
            _canvas = UIFactory.CreateCanvas("HUD Canvas", 0, transform);
            var root = (RectTransform)_canvas.transform;

            _crosshair = UIFactory.CreateImage("Crosshair", root, new Color(1f, 1f, 1f, 0.55f));
            UIFactory.Place(_crosshair.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f));

            _viewfinder = new ViewfinderView(root, _lens != null ? _lens.Levels : new[] { 1f });

            _statusText = UIFactory.CreateText("Status", root, "", 22, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperRight);
            UIFactory.Place(_statusText.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(520f, 40f));
            UIFactory.AddShadow(_statusText);

            _hintsText = UIFactory.CreateText("Hints", root,
                "<b>WASD</b> walk   <b>Shift</b> sprint   <b>J</b> journal\n" +
                "<b>Hold RMB</b> camera   <b>Scroll</b> zoom   <b>LMB</b> shutter\n<b>H</b> hide hints",
                19, new Color(1f, 1f, 1f, 0.75f), TextAnchor.LowerLeft);
            UIFactory.Place(_hintsText.rectTransform, new Vector2(0f, 0f), new Vector2(28f, 24f), new Vector2(620f, 90f));
            UIFactory.AddShadow(_hintsText);

            _pausedText = UIFactory.CreateText("Paused", root, "Click to resume", 30, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(_pausedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(600f, 50f));
            UIFactory.AddShadow(_pausedText);

            _preview = new PhotoPreviewView(root);
            _shutter = new ShutterEffectView(root); // last: draws over everything
        }

        private void Update()
        {
            if (_canvas == null) return;
            float dt = Time.unscaledDeltaTime;
            var game = WildLensGame.Instance;
            bool uiOpen = game != null && game.IsUiOpen;
            _canvas.gameObject.SetActive(!uiOpen);
            if (uiOpen) return;

            float blend = _mode != null ? _mode.Blend : 0f;
            _viewfinder.Update(blend, _lens != null ? _lens.LevelIndex : 0, photoCamera != null && photoCamera.SubjectInFocus, dt);
            _crosshair.color = new Color(1f, 1f, 1f, 0.55f * (1f - blend));
            // Hints and counters step back while the camera is up.
            _hintsText.color = new Color(1f, 1f, 1f, 0.75f * (1f - blend * 0.8f));
            _statusText.color = new Color(1f, 1f, 1f, 0.85f * (1f - blend * 0.6f));

            _shutter.Update(dt);
            _preview.Update(dt);

            if (GameInput.ToggleHintsPressed) _hintsText.enabled = !_hintsText.enabled;
            _pausedText.enabled = Cursor.lockState != CursorLockMode.Locked;
        }

        private void OnCaptureStarting() => _canvas.enabled = false;

        private void OnCaptureFinished()
        {
            _canvas.enabled = true;
            _shutter.Trigger();
        }

        private void OnPhotoTaken(PhotoResult result) => _preview.Show(result);

        private void RefreshStatus()
        {
            var game = WildLensGame.Instance;
            if (game == null || _statusText == null) return;
            _statusText.text = $"Species {game.Journal.DiscoveredCount}/{game.Journal.TotalSpecies}    Photos {game.Journal.TotalPhotos}";
        }
    }
}
