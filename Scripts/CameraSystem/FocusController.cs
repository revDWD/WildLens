using System;
using UnityEngine;
using WildLens.Photography;

namespace WildLens.CameraSystem
{
    /// <summary>
    /// The lens's focus: where the camera is focused and whether it has "locked" on a subject.
    ///
    /// In camera mode the target is the tracked subject (<see cref="FramingAssistant"/>), or whatever is under the
    /// centre of the frame. Focus pulls toward it smoothly in log-distance space, so refocusing from 8 m to 80 m
    /// feels like a lens racking rather than a jump. When a centred subject comes into focus and holds,
    /// <see cref="FocusLocked"/> fires once (beep and viewfinder pulse) until the lock is lost.
    /// <see cref="LensEffects"/> drives depth of field from <see cref="FocusDistance"/>.
    /// </summary>
    public class FocusController : MonoBehaviour
    {
        [SerializeField] private float focusPullTime = 0.14f;
        [SerializeField] private float maxFocusDistance = 400f;
        [Tooltip("Seconds a centred, in-focus subject must hold before the lock confirms.")]
        [SerializeField] private float lockDelay = 0.12f;

        private Camera _camera;
        private CameraModeController _mode;
        private FramingAssistant _framing;
        private ZoomLens _lens;
        private float _logDistance, _logVelocity;
        private float _lockTimer, _unlockTimer;

        public float FocusDistance => Mathf.Exp(_logDistance);
        public float TargetDistance { get; private set; } = 10f;
        public bool IsLocked { get; private set; }
        public event Action FocusLocked;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _mode = GetComponent<CameraModeController>();
            _framing = GetComponent<FramingAssistant>();
            _lens = GetComponent<ZoomLens>();
            _logDistance = Mathf.Log(TargetDistance);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool active = _mode != null && _mode.IsActive;
            bool subject = active && _framing != null && _framing.HasSubject && _framing.Subject.identifiable;
            bool macro = active && _lens != null && _lens.MacroMode;

            if (macro)
            {
                // 1.5: the macro lens focuses down to 8 cm, on the small subject nearest the centre (else what's there).
                var small = MacroAnalyzer.Centred(_camera, out float d);
                TargetDistance = small != null ? Mathf.Max(0.08f, d) : CentreDistance(0.08f);
                subject = small != null;
            }
            else TargetDistance = subject ? Mathf.Max(0.5f, _framing.Subject.measurements.distance) : CentreDistance();
            _logDistance = Mathf.SmoothDamp(_logDistance, Mathf.Log(TargetDistance), ref _logVelocity, focusPullTime, Mathf.Infinity, dt);

            bool sharp = Mathf.Abs(FocusDistance - TargetDistance) / TargetDistance < 0.06f;
            bool candidate = subject && (macro || _framing.SubjectCentred) && sharp;
            if (candidate)
            {
                _unlockTimer = 0f;
                _lockTimer += dt;
                if (!IsLocked && _lockTimer >= lockDelay)
                {
                    IsLocked = true;
                    FocusLocked?.Invoke();
                }
            }
            else
            {
                _lockTimer = 0f;
                _unlockTimer += dt;
                if (_unlockTimer > 0.3f) IsLocked = false; // brief wobbles don't re-trigger the beep
            }
        }

        private float CentreDistance(float minimum = 0.5f)
        {
            if (_camera == null) return 10f;
            var ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return Physics.Raycast(ray, out var hit, maxFocusDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? Mathf.Max(minimum, hit.distance)
                : maxFocusDistance * 0.5f;
        }
    }
}
