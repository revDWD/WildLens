using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
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
    /// The player's camera body: on shutter, analyses the frame, captures the screen without HUD,
    /// saves the image, registers the photo in the journal and announces the result.
    /// </summary>
    public class PhotoCamera : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("Colliders under this root (the player) never occlude subjects.")]
        [SerializeField] private Transform ignoreRoot;
        [SerializeField] private float cooldown = 0.7f;
        [SerializeField] private float maxDistance = 150f;
        [SerializeField, Range(30, 100)] private int jpgQuality = 88;
        [SerializeField] private int thumbnailWidth = 384;

        /// <summary>Raised on the shutter frame; HUD hides itself so it isn't in the picture.</summary>
        public event Action CaptureStarting;
        /// <summary>Raised once the image has been grabbed (HUD can show the flash).</summary>
        public event Action CaptureFinished;
        public event Action<PhotoResult> PhotoTaken;

        public bool IsCapturing { get; private set; }
        public Camera ViewCamera => viewCamera;

        private float _nextAllowedTime;

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

        private void Update()
        {
            var game = WildLensGame.Instance;
            if (game != null && game.IsUiOpen) return;
            if (Cursor.lockState != CursorLockMode.Locked || ClickConsumedFrame == Time.frameCount) return;
            if (!GameInput.ShutterPressed || IsCapturing || Time.unscaledTime < _nextAllowedTime) return;
            StartCoroutine(Capture());
        }

        private IEnumerator Capture()
        {
            IsCapturing = true;
            var subjects = PhotoAnalyzer.Analyze(viewCamera, maxDistance, ignoreRoot);
            Vector3 cameraPosition = viewCamera.transform.position;

            CaptureStarting?.Invoke();
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
