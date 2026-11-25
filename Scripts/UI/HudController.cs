using UnityEngine;
using UnityEngine.UI;
using WildLens.CameraSystem;
using WildLens.Core;
using WildLens.Data;
using WildLens.Observation;
using WildLens.Photography;

namespace WildLens.UI
{
    /// <summary>
    /// Owns the HUD canvas and composes its views:
    ///   <see cref="ViewfinderView"/>    camera-mode overlay, zoom, subject tracking and observation progress
    ///   <see cref="ShutterEffectView"/> shutter blackout after each capture
    ///   <see cref="PhotoPreviewView"/>  preview card with species and quality
    ///   <see cref="NoticeView"/>        one-line notices (species identified, lens preselected)
    /// plus the always-on minimum: crosshair (walking only), crouch indicator, journal counters, control hints.
    /// The whole canvas is hidden for the capture frame so it never appears in photographs.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private PhotoCamera photoCamera;

        private Canvas _canvas;
        private Image _crosshair;
        private Text _statusText, _hintsText, _pausedText, _crouchText;
        private ViewfinderView _viewfinder;
        private ShutterEffectView _shutter;
        private PhotoPreviewView _preview;
        private NoticeView _notice;
        private CameraModeController _mode;
        private ZoomLens _lens;
        private FramingAssistant _framing;
        private ObservationTracker _observation;

        public void Configure(PhotoCamera camera) => photoCamera = camera;

        private void Start()
        {
            if (photoCamera == null) photoCamera = SceneQuery.FindOne<PhotoCamera>();
            if (photoCamera != null)
            {
                _mode = photoCamera.GetComponent<CameraModeController>();
                _lens = photoCamera.GetComponent<ZoomLens>();
                _framing = photoCamera.GetComponent<FramingAssistant>();
                _observation = photoCamera.GetComponent<ObservationTracker>();
                if (_lens != null) _lens.LevelChanged += OnLensLevelChanged;
                if (_observation != null) _observation.SpeciesIdentified += OnSpeciesIdentified;
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
            if (_lens != null) _lens.LevelChanged -= OnLensLevelChanged;
            if (_observation != null) _observation.SpeciesIdentified -= OnSpeciesIdentified;
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
                "<b>WASD</b> walk   <b>Shift</b> sprint   <b>C</b> crouch   <b>J</b> journal\n" +
                "<b>Hold RMB</b> camera   <b>Scroll</b> zoom   <b>LMB</b> shutter\n" +
                "Hold an animal in view to identify it   <b>H</b> hide hints",
                19, new Color(1f, 1f, 1f, 0.75f), TextAnchor.LowerLeft);
            UIFactory.Place(_hintsText.rectTransform, new Vector2(0f, 0f), new Vector2(28f, 24f), new Vector2(720f, 90f));
            UIFactory.AddShadow(_hintsText);

            _crouchText = UIFactory.CreateText("Crouch", root, "CROUCHED", 18, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
            UIFactory.Place(_crouchText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(200f, 26f));
            UIFactory.AddShadow(_crouchText);

            _pausedText = UIFactory.CreateText("Paused", root, "Click to resume", 30, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(_pausedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(600f, 50f));
            UIFactory.AddShadow(_pausedText);

            _preview = new PhotoPreviewView(root);
            _notice = new NoticeView(root);
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
            _viewfinder.Update(BuildViewfinderState(blend), dt);
            _crouchText.enabled = PlayerPresence.IsCrouching;
            _crosshair.color = new Color(1f, 1f, 1f, 0.55f * (1f - blend));
            // Hints and counters step back while the camera is up.
            _hintsText.color = new Color(1f, 1f, 1f, 0.75f * (1f - blend * 0.8f));
            _statusText.color = new Color(1f, 1f, 1f, 0.85f * (1f - blend * 0.6f));

            _shutter.Update(dt);
            _preview.Update(dt);
            _notice.Update(dt);

            if (GameInput.ToggleHintsPressed) _hintsText.enabled = !_hintsText.enabled;
            _pausedText.enabled = Cursor.lockState != CursorLockMode.Locked;
        }

        private ViewfinderState BuildViewfinderState(float blend)
        {
            var s = new ViewfinderState
            {
                blend = blend,
                zoomLevel = _lens != null ? _lens.LevelIndex : 0,
                focalLength = _lens != null ? _lens.EquivalentFocalLength : 26f,
                hasSubject = _framing != null && _framing.HasSubject,
                subjectCentred = _framing != null && _framing.SubjectCentred,
            };
            if (!s.hasSubject) return s;

            s.subject = _framing.Subject;
            SpeciesDefinition species = s.subject.target != null ? s.subject.target.Species : null;
            var game = WildLensGame.Instance;
            bool known = species != null && game != null && game.Journal.IsDiscovered(species.speciesId);
            s.subjectName = known ? species.displayName : null;
            s.observationProgress = _observation != null && _observation.CurrentSpecies == species ? _observation.Progress : 0f;
            return s;
        }

        private void OnLensLevelChanged(int level)
        {
            // While lowered, the wheel preselects the lens: confirm it, since there's no viewfinder to show it.
            if (_mode != null && _mode.IsActive) return;
            _notice.Show($"Lens  <b>{_lens.Levels[level]:0.#}×</b>  <size=20>{Mathf.RoundToInt(_lens.TargetFocalLength)} mm</size>", 1.2f);
        }

        private void OnSpeciesIdentified(SpeciesDefinition species)
        {
            _notice.Show($"<size=20>Identified by observation</size>\n<b>{species.displayName}</b>  <size=20><i>{species.scientificName}</i></size>", 3.5f);
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
