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
    ///   <see cref="PhotoPreviewView"/>  the print that comes out after each photograph
    ///   <see cref="NoticeView"/>        quiet notices (two at most) and the name of a hidden place
    ///   <see cref="PromptView"/>        context-sensitive prompts, at most two, that fade when ignored or learnt
    ///   <see cref="ControlsCardView"/>  the Field Guide controls card (H)
    /// With the camera lowered and nothing happening the screen is empty apart from a faint centre dot (1.9): no clock, no
    /// counters, no hint block. Speed shows while riding; "Crouched", "Hidden" and the clock while waiting are small, quiet
    /// lines at the bottom-left. The whole canvas is hidden for the capture frame so it never appears in photographs.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private PhotoCamera photoCamera;

        private Canvas _canvas;
        private Image _crosshair;
        private Text _pausedText, _rideText;
        private StateLine _hiddenLine, _crouchLine, _clockLine;
        private readonly StateLine[] _stateLines = new StateLine[3];
        private bool _wasConcealed, _wasCrouching;
        private float _crouchShown;
        private bool _riding;
        private ViewfinderView _viewfinder;
        private ShutterEffectView _shutter;
        private PhotoPreviewView _preview;
        private NoticeView _notice;
        private PromptView _prompts;
        private ControlsCardView _card;
        private CameraModeController _mode;
        private ZoomLens _lens;
        private FramingAssistant _framing;
        private ObservationTracker _observation;
        private FocusController _focus;
        private Camera _cam;

        // Session memory for the prompts (nothing is saved).
        private bool _prevUiOpen;
        private float _prevBlend;
        private float _hudClock;
        private int _photosThisSession;
        private float _lastPhotoClock = -1000f;
        private float _pollTimer;
        private bool _subjectAhead, _macroNear;

        // Ride readout: rebuilt only when what it shows changes.
        private int _rideKey = -1;
        private int _lastSurface = -1;
        private float _surfaceShownUntil;
        private string _clockLineText = "";
        private float _clockTimer;

        private sealed class StateLine
        {
            public Text text;
            public float alpha;
            public bool want;
            public float y = float.NaN;
        }

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
                _cam = _lens != null ? _lens.Camera : photoCamera.GetComponent<Camera>();
                if (_focus != null) _focus.FocusLocked += OnFocusLocked;
                if (_lens != null) { _lens.LevelChanged += OnLensLevelChanged; _lens.MacroChanged += OnMacroChanged; }
                if (_observation != null) _observation.SpeciesIdentified += OnSpeciesIdentified;
                photoCamera.CaptureStarting += OnCaptureStarting;
                photoCamera.CaptureFinished += OnCaptureFinished;
                photoCamera.PhotoTaken += OnPhotoTaken;
            }
            Build();

            WildLens.Environment.ReserveDestination.Arrived += OnArrived;
            WildLens.Environment.ReserveDiscovery.Found += OnDiscovered;
            if (WildLens.Environment.DayCycle.Instance != null) WildLens.Environment.DayCycle.Instance.PeriodChanged += OnPeriodChanged;
            if (WildLens.Environment.WeatherCycle.Instance != null) WildLens.Environment.WeatherCycle.Instance.Changed += OnWeatherChanged;

            // A scene built by an older package hides every art improvement since. Say so instead of letting it
            // pass for the current look (0.6: a stale scene was mistaken for the 0.5 art). Stays noticeable: amber, priority.
            string stale = WildLens.Environment.SliceInfo.StaleReason();
            if (stale != null && _notice != null)
                _notice.Show($"This scene was {stale}",
                             $"Rebuild it for the {WildLensVersion.Package} art:  WildLens \u25b8 Vertical Slice Builder \u25b8 Rebuild", 12f, priority: true, alert: true);
        }

        private void OnDestroy()
        {
            if (photoCamera != null)
            {
                photoCamera.CaptureStarting -= OnCaptureStarting;
                photoCamera.CaptureFinished -= OnCaptureFinished;
                photoCamera.PhotoTaken -= OnPhotoTaken;
            }
            if (_lens != null) { _lens.LevelChanged -= OnLensLevelChanged; _lens.MacroChanged -= OnMacroChanged; }
            if (_observation != null) _observation.SpeciesIdentified -= OnSpeciesIdentified;
            if (_focus != null) _focus.FocusLocked -= OnFocusLocked;
            WildLens.Environment.ReserveDestination.Arrived -= OnArrived;
            WildLens.Environment.ReserveDiscovery.Found -= OnDiscovered;
            if (WildLens.Environment.DayCycle.Instance != null) WildLens.Environment.DayCycle.Instance.PeriodChanged -= OnPeriodChanged;
            if (WildLens.Environment.WeatherCycle.Instance != null) WildLens.Environment.WeatherCycle.Instance.Changed -= OnWeatherChanged;
        }

        /// <summary>A hidden place (1.5): its name, once, large and calm, centre-top. Nothing is counted.</summary>
        private void OnDiscovered(WildLens.Environment.ReserveDiscovery d)
        {
            if (_notice == null) return;
            _notice.ShowPlace(d.title, d.note);
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
            _notice.Show(title, note, 6f);
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
            _notice.Show(WildLens.Environment.DayCycle.PeriodName(p), note, 6f);
        }

        private void OnArrived(WildLens.Environment.ReserveDestination d)
        {
            if (_notice == null) return;
            _notice.Show(d.title, d.fieldNote, 6f);
        }

        private void Build()
        {
            _canvas = UIFactory.CreateCanvas("HUD Canvas", 0, transform);
            var root = (RectTransform)_canvas.transform;

            // The one thing always on screen with the camera lowered: a faint centre dot.
            _crosshair = UIFactory.CreateImage("Crosshair", root, UiStyle.WithAlpha(UiStyle.Text, 0.3f));
            UIFactory.Place(_crosshair.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 4f));

            _viewfinder = new ViewfinderView(root, _lens != null ? _lens.Levels : new[] { 1f });

            // Quiet state lines, bottom-left, stacked upward from the margin (only the ones that apply take a place).
            _hiddenLine = MakeStateLine(root, "Hidden", "Hidden", 0.6f);
            _crouchLine = MakeStateLine(root, "Crouched", "Crouched", 0.7f);
            _clockLine = MakeStateLine(root, "Clock", "", 0.75f);
            _stateLines[0] = _crouchLine; _stateLines[1] = _hiddenLine; _stateLines[2] = _clockLine;

            // Speed, bottom-right, only while riding.
            _rideText = UIFactory.CreateText("Ride", root, "", UiStyle.SizeTitle, UiStyle.Text, TextAnchor.LowerRight);
            UIFactory.Place(_rideText.rectTransform, new Vector2(1f, 0f), new Vector2(-UiStyle.Margin, UiStyle.Margin), new Vector2(420f, 70f));
            UiStyle.SoftShadow(_rideText);
            _rideText.enabled = false;

            _prompts = new PromptView(root);

            _pausedText = UIFactory.CreateText("Paused", root,
                "Click to resume\n<size=" + UiStyle.SizeSmall + ">" + PromptView.Prompt(GameInput.Labels.Controls, "Controls") + "</size>",
                28, UiStyle.Text, TextAnchor.MiddleCenter);
            UIFactory.Place(_pausedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(600f, 90f));
            UiStyle.SoftShadow(_pausedText, 0.7f);

            _preview = new PhotoPreviewView(root);
            _notice = new NoticeView(root);
            _shutter = new ShutterEffectView(root); // last: draws over everything
            _card = new ControlsCardView(transform, WildLensGame.Instance);
        }

        private StateLine MakeStateLine(RectTransform root, string name, string text, float strength)
        {
            var l = new StateLine();
            l.text = UIFactory.CreateText(name, root, text, UiStyle.SizeBody, UiStyle.WithAlpha(UiStyle.Text, strength), TextAnchor.LowerLeft);
            UIFactory.Place(l.text.rectTransform, new Vector2(0f, 0f), new Vector2(UiStyle.Margin, UiStyle.Margin), new Vector2(560f, 30f));
            UiStyle.SoftShadow(l.text);
            l.text.enabled = false;
            return l;
        }

        private void Update()
        {
            if (_canvas == null) return;
            float dt = Time.unscaledDeltaTime;
            var game = WildLensGame.Instance;

            // The controls card works whether or not the HUD is showing; opening it retires the "H Controls" line.
            if (_card != null && _card.Update()) _prompts.Retire(PromptId.Intro);

            bool uiOpen = game != null && game.IsUiOpen;
            if (uiOpen && !_prevUiOpen && (_card == null || !_card.IsOpen)) _prompts.Acted(PromptId.Journal); // the journal was opened
            _prevUiOpen = uiOpen;
            _canvas.gameObject.SetActive(!uiOpen);
            if (uiOpen) return;

            _hudClock += dt;
            float blend = _mode != null ? _mode.Blend : 0f;
            if (_prevBlend < 0.5f && blend >= 0.5f && !_riding) _prompts.Acted(PromptId.Raise); // the camera went up
            _prevBlend = blend;

            _viewfinder.Update(BuildViewfinderState(blend), dt);
            _crosshair.enabled = !_riding;
            _crosshair.color = UiStyle.WithAlpha(UiStyle.Text, 0.3f * (1f - blend));

            // Quiet state lines
            bool concealed = PlayerPresence.IsConcealed;
            if (concealed && !_wasConcealed)
                _notice.Show("Inside the blind", "Wildlife can't see you here. Sit tight at the window; they'll come closer.", 6f);
            _wasConcealed = concealed;
            bool crouching = PlayerPresence.IsCrouching;
            if (crouching && !_wasCrouching) _crouchShown = 2.4f;
            _wasCrouching = crouching;
            _crouchShown = Mathf.Max(0f, _crouchShown - dt);
            var clock = WildLens.Environment.DayCycle.Instance;
            bool waiting = clock != null && clock.IsWaiting;
            if (waiting) UpdateClockText(clock, dt);
            _hiddenLine.want = concealed;
            _crouchLine.want = crouching && _crouchShown > 0f;
            _clockLine.want = waiting;
            UpdateStateLines(dt, blend);

            _shutter.Update(dt);
            _preview.Update(dt);
            _notice.Update(dt);

            bool locked = Cursor.lockState == CursorLockMode.Locked;
            PollNearby(dt);
            var ctx = new PromptContext
            {
                hidden = !locked,
                riding = _riding,
                bikeInReach = WildLens.Vehicles.TrailBike.InReach != null,
                cameraLowered = blend < 0.05f,
                cameraRaised = blend > 0.85f,
                raiseBlend = blend,
                macroMode = _lens != null && _lens.MacroMode,
                hasSubject = _framing != null && _framing.HasSubject,
                macroNear = _macroNear,
                subjectAhead = _subjectAhead || _macroNear,
                photosThisSession = _photosThisSession,
                secondsSincePhoto = _hudClock - _lastPhotoClock,
            };
            UpdateBike(blend, ref ctx);
            _prompts.Update(ctx, dt);
            _pausedText.enabled = !locked;
        }

        /// <summary>Bottom-left: the lines that apply, stacked up from the margin, fading with the viewfinder.</summary>
        private void UpdateStateLines(float dt, float blend)
        {
            float y = UiStyle.Margin;
            for (int i = 0; i < _stateLines.Length; i++)
            {
                var l = _stateLines[i];
                l.alpha = Mathf.MoveTowards(l.alpha, l.want ? 1f : 0f, dt / UiStyle.Fade);
                bool on = l.alpha > 0.001f;
                if (l.text.enabled != on) l.text.enabled = on;
                if (!on) continue;
                var c = l.text.color;
                l.text.color = new Color(c.r, c.g, c.b, (i == 2 ? 0.75f : i == 1 ? 0.6f : 0.7f) * l.alpha * (1f - blend)); // out of the viewfinder's way
                if (l.y != y) { l.y = y; l.text.rectTransform.anchoredPosition = new Vector2(UiStyle.Margin, y); }
                y += 30f;
            }
        }

        private void UpdateClockText(WildLens.Environment.DayCycle clock, float dt)
        {
            _clockTimer -= dt;
            if (_clockTimer > 0f && _clockLineText.Length > 0) return;
            _clockTimer = 0.5f;
            string t = $"{WildLens.Environment.DayCycle.PeriodName(clock.Period)}  \u00b7  {clock.ClockText}   <i>time passing\u2026</i>";
            if (t == _clockLineText) return;
            _clockLineText = t;
            _clockLine.text.text = t;
        }

        /// <summary>Four times a second: is an animal worth raising the camera for, is a small subject within 3 m?</summary>
        private void PollNearby(float dt)
        {
            _pollTimer -= dt;
            if (_pollTimer > 0f) return;
            _pollTimer = 0.25f;
            _subjectAhead = _macroNear = false;
            if (_cam == null) return;
            Vector3 p = _cam.transform.position, f = _cam.transform.forward;

            var animals = Photographable.Active;
            for (int i = 0; i < animals.Count && !_subjectAhead; i++)
            {
                var a = animals[i];
                if (a == null) continue;
                Vector3 to = a.transform.position - p;
                float d2 = to.sqrMagnitude;
                if (d2 > 45f * 45f || d2 < 1f) continue;
                if (Vector3.Dot(to, f) > 0.82f * Mathf.Sqrt(d2)) _subjectAhead = true; // within about 35 degrees of where you look
            }

            var small = MacroSubject.Active;
            for (int i = 0; i < small.Count; i++)
            {
                var m = small[i];
                if (m == null || m.subject == null || !m.Visible) continue;
                if ((m.transform.position - p).sqrMagnitude > 9f) continue;
                if (m.IsShowing) { _macroNear = true; break; }
            }
        }

        private void UpdateBike(float blend, ref PromptContext ctx)
        {
            var bike = WildLens.Vehicles.TrailBike.Ridden;
            bool riding = bike != null;
            if (riding != _riding)
            {
                if (riding) _prompts.Acted(PromptId.Mount); else _prompts.Acted(PromptId.Park);
                _riding = riding;
                ctx.riding = riding;
                _rideKey = -1;
                _lastSurface = -1;
            }
            if (!riding)
            {
                if (_rideText.enabled) _rideText.enabled = false;
                return;
            }

            float speed = Mathf.Abs(bike.Speed);
            int surface = (int)bike.Surface;
            if (surface != _lastSurface) { _lastSurface = surface; _surfaceShownUntil = _hudClock + 4f; }
            bool showSurface = _hudClock < _surfaceShownUntil;
            int kmh = Mathf.RoundToInt(speed * 3.6f);
            int key = (kmh * 8 + surface * 2 + (showSurface ? 1 : 0)) * 2 + (bike.IsParking ? 1 : 0);
            if (key != _rideKey)
            {
                _rideKey = key;
                string where = surface == (int)WildLens.Vehicles.TrailBike.RideSurface.Road ? "Reserve Road"
                    : surface == (int)WildLens.Vehicles.TrailBike.RideSurface.Footpath ? "Footpath \u00b7 ride gently" : "Across country";
                _rideText.text = bike.IsParking ? "Parking\u2026" : kmh + " km/h" + (showSurface ? "\n<size=" + UiStyle.SizeSmall + ">" + where + "</size>" : "");
            }
            _rideText.enabled = true;
            _rideText.color = UiStyle.WithAlpha(UiStyle.Text, 0.85f * (1f - blend));

            // 0.8: pull-offs suggest themselves as you roll past; a stopped bike offers to park anywhere.
            var pull = WildLens.Environment.ReserveParking.At(bike.transform.position);
            ctx.parkPlace = pull != null && !string.IsNullOrEmpty(pull.title) ? pull.title : null;
            ctx.canPark = !bike.IsParking && ((pull != null && speed < 7f) || speed < 0.6f);
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
            _prompts?.Acted(PromptId.Zoom);
        }

        private void OnMacroChanged(bool on) { if (on) _prompts?.Acted(PromptId.Macro); }

        private void OnFocusLocked() => _viewfinder.PulseFocus();

        private void OnSpeciesIdentified(SpeciesDefinition species)
        {
            _notice.Show(species.displayName, $"<i>{species.scientificName}</i>  \u00b7  identified by observation", 4f);
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
            _photosThisSession++;
            _lastPhotoClock = _hudClock;
            _prompts.Acted(PromptId.Shutter);
            // Macro (1.5): name the small subject and how the photo did; the print shows as usual.
            if (result.macro != null && result.macro.Count > 0 && _notice != null)
            {
                var m = result.macro[0];
                string extra = string.IsNullOrEmpty(m.hint) ? "" : $" \u00b7 {m.hint}";
                _notice.Show(m.subject.displayName,
                             (result.macroIsNew ? "New in your field notebook  \u00b7  " : "") + $"Macro \u00b7 {m.quality}{extra}", 5f);
            }
        }
    }
}
