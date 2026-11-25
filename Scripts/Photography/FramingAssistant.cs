using UnityEngine;
using WildLens.CameraSystem;

namespace WildLens.Photography
{
    /// <summary>The subject the camera is currently tracking.</summary>
    public struct TrackedSubject
    {
        public Photographable target;
        /// <summary>Viewport-space rectangle (0-1) around the subject, clipped to the frame.</summary>
        public Rect screenRect;
        public SubjectMeasurements measurements;
        /// <summary>Large and visible enough to identify / photograph.</summary>
        public bool identifiable;
        /// <summary>The grade a photo taken right now would get.</summary>
        public PhotoQuality previewQuality;
    }

    /// <summary>
    /// Camera subject tracking (like eye-detection autofocus). While camera mode is active it picks the subject
    /// nearest the frame centre (sticking with the current one when it's still a candidate), measures it with
    /// the same <see cref="PhotoAnalyzer"/> code a photo uses, and previews its quality class.
    ///
    /// Selection and line-of-sight run at <c>updateInterval</c>; the on-screen rectangle is re-projected every
    /// frame so the viewfinder brackets track smoothly. Consumers: the viewfinder (brackets and grade colour)
    /// and <c>ObservationTracker</c> (identifying species by watching).
    /// </summary>
    public class FramingAssistant : MonoBehaviour
    {
        [SerializeField] private float updateInterval = 0.1f;
        [SerializeField] private float maxDistance = 200f;
        [Tooltip("Half-size (viewport units) of the central area in which subjects are picked up.")]
        [SerializeField] private float selectionRadius = 0.3f;
        [Tooltip("The tracked subject counts as centred when its centre is within this of frame centre.")]
        [SerializeField] private float centredTolerance = 0.12f;

        private Camera _camera;
        private CameraModeController _mode;
        private Transform _ignoreRoot;
        private float _timer;
        private TrackedSubject _subject;

        public bool HasSubject { get; private set; }
        public TrackedSubject Subject => _subject;
        /// <summary>The tracked subject sits under the centre focus marks.</summary>
        public bool SubjectCentred { get; private set; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _mode = GetComponent<CameraModeController>();
            // Ignore only the player's own body (not transform.root, which in a built scene is the whole slice).
            var body = GetComponentInParent<CharacterController>();
            _ignoreRoot = body != null ? body.transform : transform.parent;
        }

        private void Update()
        {
            if (_mode == null || !_mode.IsActive || _camera == null)
            {
                HasSubject = false;
                SubjectCentred = false;
                return;
            }

            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                _timer = updateInterval;
                Select();
            }
            else if (HasSubject)
            {
                // Cheap per-frame re-projection; keep the last visibility measurement.
                float visibility = _subject.measurements.visibility;
                if (PhotoAnalyzer.TryMeasure(_camera, _subject.target, maxDistance, _ignoreRoot, false, out var m, out var rect))
                {
                    m.visibility = visibility;
                    Store(_subject.target, m, rect);
                }
                else HasSubject = false;
            }

            SubjectCentred = HasSubject && _subject.identifiable &&
                             Mathf.Abs(_subject.screenRect.center.x - 0.5f) <= centredTolerance &&
                             Mathf.Abs(_subject.screenRect.center.y - 0.5f) <= centredTolerance;
        }

        private void Select()
        {
            Photographable best = null;
            float bestScore = float.MaxValue;
            var previous = HasSubject ? _subject.target : null;

            foreach (var target in Photographable.Active)
            {
                if (!PhotoAnalyzer.TryMeasure(_camera, target, maxDistance, _ignoreRoot, false, out var m, out var rect)) continue;
                // Distance from frame centre to the subject's rectangle (0 if the centre is inside it).
                float dx = Mathf.Max(0f, Mathf.Max(rect.xMin - 0.5f, 0.5f - rect.xMax));
                float dy = Mathf.Max(0f, Mathf.Max(rect.yMin - 0.5f, 0.5f - rect.yMax));
                float d = Mathf.Max(dx, dy);
                if (d > selectionRadius) continue;
                // Prefer the current subject, then bigger subjects, so the frame doesn't jump around.
                float score = d - Mathf.Min(m.NormalizedFill, 0.5f) * 0.2f - (target == previous ? 0.08f : 0f);
                if (score < bestScore) { bestScore = score; best = target; }
            }

            if (best != null && PhotoAnalyzer.TryMeasure(_camera, best, maxDistance, _ignoreRoot, true, out var measured, out var r))
                Store(best, measured, r);
            else
                HasSubject = false;
        }

        private void Store(Photographable target, SubjectMeasurements m, Rect rect)
        {
            _subject = new TrackedSubject
            {
                target = target,
                screenRect = rect,
                measurements = m,
                identifiable = PhotoQualityEvaluator.IsIdentifiable(m),
                previewQuality = PhotoQualityEvaluator.Classify(m),
            };
            HasSubject = true;
        }
    }
}
