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
        private Text _statusText, _hintsText, _pausedText, _crouchText, _promptText, _rideText, _hiddenText;
        private bool _wasConcealed;
        private float _clockTimer;
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
            WildLens.Environment.ReserveDiscovery.Found += OnDiscovered;
            if (WildLens.Environment.DayCycle.Instance != null) WildLens.Environment.DayCycle.Instance.PeriodChanged += OnPeriodChanged;
            if (WildLens.Environment.WeatherCycle.Instance != null) WildLens.Environment.WeatherCycle.Instance.Changed += OnWeatherChanged;

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
            WildLens.Environment.ReserveDiscovery.Found -= OnDiscovered;
            if (WildLens.Environment.DayCycle.Instance != null) WildLens.Environment.DayCycle.Instance.PeriodChanged -= OnPeriodChanged;
            if (WildLens.Environment.WeatherCycle.Instance != null) WildLens.Environment.WeatherCycle.Instance.Changed -= OnWeatherChanged;
        }

        /// <summary>A hidden place (1.5): its name, quietly, the first time you walk in. Nothing is counted.</summary>
        private void OnDiscovered(WildLens.Environment.ReserveDiscovery d)
        {
            if (_notice == null) return;
            _notice.Show($"<i>{d.title}</i>\n<size=20>{d.note}</size>", 7f);
        }

        private bool _hadRain;

        /// <summary>A field note when the weather turns (1.4): what it means for the light and the wildlife.</summary>
        private void OnWeatherChanged(WildLens.Environment.WeatherCondition c)
        {
            if (_notice == null) return;
            string title, note;
            switch (c)
            {
                case WildLens.Environment.WeatherCondition.LightRain:
                    _hadRain = true;
                    title = "Light rain"; note = "Deer and foxes shelter in the woods. Herons don't mind the wet."; break;
                case WildLens.Environment.WeatherCondition.Overcast:
                    title = "Overcast"; note = "Soft, even light: no harsh shadows on fur and feathers."; break;
                default:
                    if (!_hadRain) return; // clouds coming and going: let the sky say it
                    _hadRain = false;
                    title = "Clearing"; note = "The rain has passed. Wildlife comes back out into the open."; break;
            }
            _notice.Show($"<b>{title}</b>\n<size=20>{note}</size>", 6f);
        }

        private float _lastPeriodNotice = -100f;

        /// <summary>A field note at each change of light: what the reserve is doing now (0.9).</summary>
        private void OnPeriodChanged(WildLens.Environment.DayPeriod p)
        {
            if (_notice == null || Time.time - _lastPeriodNotice < 3f) return;
            _lastPeriodNotice = Time.time;
            string note;
            switch (p)
            {
                case WildLens.Environment.DayPeriod.Dawn: note = "Mist on the pond and creek. Cottontails venture onto the meadow edges."; break;
                case WildLens.Environment.DayPeriod.Morning: note = "Deer graze the meadows. Hawks start to soar over the ridge."; break;
                case WildLens.Environment.DayPeriod.Midday: note = "Deer bed down in woodland shade. A quiet time: try the grove."; break;
                case WildLens.Environment.DayPeriod.GoldenHour: note = "Warm, low light. Head for the ridge; deer return to the meadows."; break;
                case WildLens.Environment.DayPeriod.Dusk: note = "Deer come down to the water. Foxes hunt the meadow edges."; break;
                default: note = "Night falls over the reserve. Time passes quickly until dawn."; break;
            }
            _notice.Show($"<b>{WildLens.Environment.DayCycle.PeriodName(p)}</b>\n<size=20>{note}</size>", 6f);
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
            UIFactory.Place(_statusText.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(760f, 40f));
            UIFactory.AddShadow(_statusText);

            _walkHints =
                "<b>WASD</b> walk   <b>Shift</b> sprint   <b>C</b> crouch   <b>J</b> journal   <b>R</b> ride (at the bike)\n" +
                "<b>Hold RMB</b> or <b>Scroll</b> camera + zoom   <b>LMB</b> shutter\n" +
                "Hold an animal in view to identify it   <b>M</b> macro lens   <b>Hold T</b> let time pass   <b>H</b> hide hints";
            _hintsText = UIFactory.CreateText("Hints", root, _walkHints, 19, new Color(1f, 1f, 1f, 0.75f), TextAnchor.LowerLeft);
            UIFactory.Place(_hintsText.rectTransform, new Vector2(0f, 0f), new Vector2(28f, 24f), new Vector2(900f, 90f));
            UIFactory.AddShadow(_hintsText);

            _crouchText = UIFactory.CreateText("Crouch", root, "CROUCHED", 18, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
            UIFactory.Place(_crouchText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(200f, 26f));
            UIFactory.AddShadow(_crouchText);

            _promptText = UIFactory.CreateText("Prompt", root, "", 24, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Place(_promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(600f, 34f));
            UIFactory.AddShadow(_promptText);
            _hiddenText = UIFactory.CreateText("Hidden", root, "HIDDEN  <size=15>wildlife can't see you</size>", 18, new Color(0.75f, 1f, 0.75f, 0.85f), TextAnchor.MiddleCenter);
            UIFactory.Place(_hiddenText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 68f), new Vector2(420f, 26f));
            UIFactory.AddShadow(_hiddenText);
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
            bool concealed = PlayerPresence.IsConcealed;
            _hiddenText.enabled = concealed;
            _hiddenText.color = new Color(0.75f, 1f, 0.75f, 0.85f * (1f - blend * 0.7f));
            if (concealed && !_wasConcealed)
                _notice.Show("<b>Inside the blind</b>\n<size=20>Wildlife can't see you here. Sit tight at the window; they'll come closer.</size>", 6f);
            _wasConcealed = concealed;
            _crosshair.color = new Color(1f, 1f, 1f, 0.55f * (1f - blend));
            // Hints and counters step back while the camera is up.
            _hintsText.color = new Color(1f, 1f, 1f, 0.75f * (1f - blend)); // gone in camera mode: it overlapped the viewfinder corner
            _statusText.color = new Color(1f, 1f, 1f, 0.85f * (1f - blend * 0.6f));

            _shutter.Update(dt);
            _preview.Update(dt);
            _notice.Update(dt);

            if (GameInput.ToggleHintsPressed) _hintsText.enabled = !_hintsText.enabled;
            UpdateBike(blend);
            _clockTimer -= dt;
            if (_clockTimer <= 0f) { _clockTimer = 0.5f; RefreshStatus(); }
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
                string where = bike.Surface == WildLens.Vehicles.TrailBike.RideSurface.Road ? "Reserve Road"
                    : bike.Surface == WildLens.Vehicles.TrailBike.RideSurface.Footpath ? "footpath · ride gently" : "across country";
                _rideText.text = bike.IsParking ? "Parking…" : $"{kmh:0} km/h   <size=16>{where}</size>";
                _rideText.color = new Color(1f, 1f, 1f, 0.85f * (1f - blend));
                // 0.8: pull-offs suggest themselves as you roll past.
                var pull = WildLens.Environment.ReserveParking.At(bike.transform.position);
                _promptText.text = pull != null && !bike.IsParking && Mathf.Abs(bike.Speed) < 7f
                    ? $"<b>R</b>  Park here  <size=18>· {pull.title}{(string.IsNullOrEmpty(pull.hint) ? "" : " · " + pull.hint)}</size>" : "";
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
                macro = _lens != null && _lens.MacroMode,
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

        private void OnPhotoTaken(PhotoResult result)
        {
            _preview.Show(result);
            // Macro (1.5): name the small subject and how the photo did; the print shows as usual.
            if (result.macro != null && result.macro.Count > 0 && _notice != null)
            {
                var m = result.macro[0];
                string extra = string.IsNullOrEmpty(m.hint) ? "" : $" · {m.hint}";
                _notice.Show($"<b>{m.subject.displayName}</b>{(result.macroIsNew ? "  <i>new in your field notebook</i>" : "")}\n" +
                             $"<size=20>Macro · {m.quality}{extra}</size>", 5f);
            }
        }

        private void RefreshStatus()
        {
            var game = WildLensGame.Instance;
            if (game == null || _statusText == null) return;
            var clock = WildLens.Environment.DayCycle.Instance;
            string time = clock != null ? $"{WildLens.Environment.DayCycle.PeriodName(clock.Period)} · {clock.ClockText}{(clock.IsWaiting ? "  <i>time passing…</i>" : "")}      " : "";
            _statusText.text = $"{time}Species {game.Journal.DiscoveredCount}/{game.Journal.TotalSpecies}    Photos {game.Journal.TotalPhotos}";
        }
    }
}
