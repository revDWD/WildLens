using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using WildLens.CameraSystem;
using WildLens.Core;
using WildLens.Journal;
using WildLens.Wildlife;

namespace WildLens.Photography
{
    public class PhotoResult
    {
        public PhotoRecord record;
        public List<SpeciesIdentification> identifications;
        public Texture2D thumbnail;
    }

    /// <summary>
    /// The camera body's shutter. Works only in camera mode (<see cref="CameraModeController.IsReady"/>).
    ///
    /// On a press it: analyses the frame → raises <see cref="CaptureStarting"/> (shutter sound, HUD hides) →
    /// grabs the screen at end of frame → raises <see cref="CaptureFinished"/> (shutter blackout) → saves the
    /// image, registers it in the journal and raises <see cref="PhotoTaken"/> (preview card).
    /// The shutter is audible: nearby wildlife gets a little more suspicious.
    ///
    /// While the camera is raised it also runs a cheap focus check (<see cref="SubjectInFocus"/>) for the viewfinder.
    /// </summary>
    public class PhotoCamera : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("Colliders under this root (the player) never occlude subjects.")]
        [SerializeField] private Transform ignoreRoot;
        [SerializeField] private float cooldown = 0.45f;
        [SerializeField] private float maxDistance = 200f;
        [SerializeField, Range(30, 100)] private int jpgQuality = 88;
        [SerializeField] private int thumbnailWidth = 384;
        [Header("Shutter noise heard by wildlife")]
        [SerializeField] private float shutterNoiseRadius = 12f;
        [SerializeField, Range(0f, 1f)] private float shutterNoiseStrength = 0.2f;

        /// <summary>Raised on the shutter frame; HUD hides itself so it isn't in the picture.</summary>
        public event Action CaptureStarting;
        /// <summary>Raised once the image has been grabbed.</summary>
        public event Action CaptureFinished;
        public event Action<PhotoResult> PhotoTaken;

        public bool IsCapturing { get; private set; }
        public bool SubjectInFocus { get; private set; }
        public Camera ViewCamera => viewCamera;

        private CameraModeController _mode;
        private float _nextAllowedTime;
        private float _focusTimer;

        /// <summary>Set by the player controller when a click was used to capture the mouse cursor.</summary>
        public static int ClickConsumedFrame = -1;

        public void Configure(Camera cam, Transform playerRoot)
        {
            viewCamera = cam;
            ignoreRoot = playerRoot;
        }

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            if (ignoreRoot == null) ignoreRoot = transform.root;
        }

        private void Start() => _mode = GetComponent<CameraModeController>();

        private void Update()
        {
            var game = WildLensGame.Instance;
            bool ready = _mode != null && _mode.IsReady && (game == null || !game.IsUiOpen);

            UpdateFocus(ready);

            if (!ready || Cursor.lockState != CursorLockMode.Locked || ClickConsumedFrame == Time.frameCount) return;
            if (!GameInput.ShutterPressed || IsCapturing || Time.unscaledTime < _nextAllowedTime) return;
            StartCoroutine(Capture());
        }

        private void UpdateFocus(bool active)
        {
            if (!active) { SubjectInFocus = false; return; }
            _focusTimer -= Time.unscaledDeltaTime;
            if (_focusTimer > 0f) return;
            _focusTimer = 0.1f;
            SubjectInFocus = PhotoAnalyzer.HasSubjectInFocus(viewCamera, maxDistance, ignoreRoot);
        }

        private IEnumerator Capture()
        {
            IsCapturing = true;
            var subjects = PhotoAnalyzer.Analyze(viewCamera, maxDistance, ignoreRoot);
            Vector3 cameraPosition = viewCamera.transform.position;

            CaptureStarting?.Invoke();
            WildlifeAwareness.EmitSound(cameraPosition, shutterNoiseRadius, shutterNoiseStrength);
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            CaptureFinished?.Invoke();

            DateTime now = DateTime.Now;
            string id = "WL_" + now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            Texture2D thumbnail = PhotoStorage.Downscale(shot, thumbnailWidth);
            string path = PhotoStorage.Save(shot, id, jpgQuality);
            if (thumbnail != shot) Destroy(shot);
            PhotoStorage.CacheThumbnail(id, thumbnail);

            var trail = WildlifeWorld.Instance != null ? WildlifeWorld.Instance.Trail : null;
            var record = new PhotoRecord
            {
                photoId = id,
                takenAtUtc = now.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                imagePath = path,
                cameraPosition = cameraPosition,
                locationLabel = trail != null ? trail.DescribeLocation(cameraPosition) : "",
                subjects = subjects,
            };

            var game = WildLensGame.Instance;
            var ids = game != null ? game.Journal.RegisterPhoto(record) : new List<SpeciesIdentification>();
            PhotoTaken?.Invoke(new PhotoResult { record = record, identifications = ids, thumbnail = thumbnail });

            _nextAllowedTime = Time.unscaledTime + cooldown;
            IsCapturing = false;
        }
    }
}
