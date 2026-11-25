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
        private Text _statusText, _hintsText, _pausedText, _crouchText, _promptText, _rideText;
        private string _walkHints;
        private bool _riding;
        private const string RideHints =
            "<b>W</b> throttle   <b>S</b> brake   <b>A / D</b> steer   <b>R</b> park & step off\n" +
            "Stop to raise the camera   Engines carry: park short of wildlife   <b>H</b> hide hints";
        private ViewfinderView _viewfinder;
        private ShutterEffectView _shutter;
        private PhotoPreviewView _preview;
        private NoticeView _notice;
        private CameraModeController _mode;
        private ZoomLens _lens;
        private FramingAssistant _framing;
        private ObservationTracker _observation;
        private FocusController _focus;

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
                _focus = photoCamera.GetComponent<FocusController>();
                if (_focus != null) _focus.FocusLocked += OnFocusLocked;
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
            WildLens.Environment.ReserveDestination.Arrived += OnArrived;

            // A scene built by an older package hides every art improvement since. Say so instead of letting it
            // pass for the current look (0.6: a stale scene was mistaken for the 0.5 art).
            string stale = WildLens.Environment.SliceInfo.StaleReason();
            if (stale != null && _notice != null)
                _notice.Show($"<size=22>This scene was {stale}. Rebuild it for the {WildLensVersion.Package} art:\n" +
                             "<b>WildLens ▸ Vertical Slice Builder ▸ Rebuild</b></size>", 12f);
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
            if (_focus != null) _focus.FocusLocked -= OnFocusLocked;
            var game = WildLensGame.Instance;
            if (game != null && game.Journal != null) game.Journal.Changed -= RefreshStatus;
            WildLens.Environment.ReserveDestination.Arrived -= OnArrived;
        }

        private void OnArrived(WildLens.Environment.ReserveDestination d)
        {
            if (_notice == null) return;
            _notice.Show($"<b>{d.title}</b>\n<size=20>{d.fieldNote}</size>", 6f);
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

            _walkHints =
                "<b>WASD</b> walk   <b>Shift</b> sprint   <b>C</b> crouch   <b>J</b> journal   <b>R</b> ride (at the bike)\n" +
                "<b>Hold RMB</b> or <b>Scroll</b> camera + zoom   <b>LMB</b> shutter\n" +
                "Hold an animal in view to identify it   <b>H</b> hide hints";
            _hintsText = UIFactory.CreateText("Hints", root, _walkHints, 19, new Color(1f, 1f, 1f, 0.75f), TextAnchor.LowerLeft);
            UIFactory.Place(_hintsText.rectTransform, new Vector2(0f, 0f), new Vector2(28f, 24f), new Vector2(900f, 90f));
            UIFactory.AddShadow(_hintsText);

            _crouchText = UIFactory.CreateText("Crouch", root, "CROUCHED", 18, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
            UIFactory.Place(_crouchText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(200f, 26f));
            UIFactory.AddShadow(_crouchText);

            _promptText = UIFactory.CreateText("Prompt", root, "", 24, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(_promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(600f, 34f));
            UIFactory.AddShadow(_promptText);
            _rideText = UIFactory.CreateText("Ride", root, "", 20, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
            UIFactory.Place(_rideText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(420f, 28f));
            UIFactory.AddShadow(_rideText);

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
            _hintsText.color = new Color(1f, 1f, 1f, 0.75f * (1f - blend)); // gone in camera mode: it overlapped the viewfinder corner
            _statusText.color = new Color(1f, 1f, 1f, 0.85f * (1f - blend * 0.6f));

            _shutter.Update(dt);
            _preview.Update(dt);
            _notice.Update(dt);

            if (GameInput.ToggleHintsPressed) _hintsText.enabled = !_hintsText.enabled;
            UpdateBike(blend);
            _pausedText.enabled = Cursor.lockState != CursorLockMode.Locked;
        }

        private void UpdateBike(float blend)
        {
            var bike = WildLens.Vehicles.TrailBike.Ridden;
            bool riding = bike != null;
            if (riding != _riding)
            {
                _riding = riding;
                _hintsText.text = riding ? RideHints : _walkHints;
            }
            if (riding)
            {
                float kmh = Mathf.Abs(bike.Speed) * 3.6f;
                string where = bike.OnTrack ? "on track" : "across country";
                _rideText.text = bike.IsParking ? "Parking…" : $"{kmh:0} km/h   <size=16>{where}</size>";
                _rideText.color = new Color(1f, 1f, 1f, 0.85f * (1f - blend));
                _promptText.text = "";
            }
            else
            {
                _rideText.text = "";
                // Hidden while the camera is up: it sat on top of the viewfinder's zoom scale.
                _promptText.text = WildLens.Vehicles.TrailBike.InReach != null && blend < 0.05f ? "<b>R</b>  Ride the trail bike" : "";
            }
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
                focusLocked = _focus != null && _focus.IsLocked,
                focusHunting = _focus != null && Mathf.Abs(_focus.FocusDistance - _focus.TargetDistance) / Mathf.Max(0.5f, _focus.TargetDistance) > 0.06f,
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
            // Zoom input always raises the camera now, so the readout belongs in the viewfinder.
            _viewfinder.ShowZoomPop(_lens.Levels[level]);
        }

        private void OnFocusLocked() => _viewfinder.PulseFocus();

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
