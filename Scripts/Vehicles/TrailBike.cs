using UnityEngine;
using WildLens.CameraSystem;
using WildLens.Core;
using WildLens.Environment;
using WildLens.Player;

namespace WildLens.Vehicles
{
    /// <summary>
    /// A small, quiet-ish trail bike for getting around the reserve. Deliberately gentle: modest top speed, soft
    /// acceleration, forgiving steering, no damage, fuel or stunts. The loop it supports is
    /// ride → spot something → stop → park (R) → camera → explore on foot → back to the bike (R).
    ///
    /// Design points:
    ///   • R mounts when you're next to it, and parks when riding (it brakes to a stop first, puts the stand down
    ///     and switches off). You step off on the left, ready to walk.
    ///   • Fastest on the trail loop and reserve tracks; slower and bumpier across open ground; won't climb
    ///     very steep slopes; stops softly against trees and rocks.
    ///   • The engine is louder than any footstep (<see cref="PlayerPresence.EngineNoise"/>): wildlife notices a
    ///     running bike from far off, so the natural thing is to park and walk the last stretch.
    ///   • Stopped in the saddle you can still raise the camera; moving, you can't.
    ///
    /// Kinematic: it follows the terrain with two wheel samples rather than using wheel colliders, which keeps it
    /// predictable and cheap. A purchased model can replace the placeholder: put it under a child named "Model"
    /// before Play and the placeholder isn't built (wheel/steer animation then needs the named pivots).
    /// </summary>
    public class TrailBike : MonoBehaviour
    {
        [Header("Handling")]
        [SerializeField] private float maxSpeed = 11f;          // ≈40 km/h on tracks
        [SerializeField] private float offTrackMaxSpeed = 6.5f;  // ≈23 km/h across country
        [SerializeField] private float acceleration = 2.6f;
        [SerializeField] private float braking = 6f;
        [SerializeField] private float reverseSpeed = 1f;        // paddling backwards
        [SerializeField] private float steerRateSlow = 85f;      // deg/s at walking pace
        [SerializeField] private float steerRateFast = 34f;      // deg/s at top speed
        [SerializeField] private float maxClimbDegrees = 30f;

        [Header("Rider")]
        [SerializeField] private float mountDistance = 2.6f;
        [SerializeField] private float seatedEyeHeight = 1.5f;
        [SerializeField] private float lookSensitivity = 2f;

        public static TrailBike Ridden { get; private set; }
        /// <summary>A parked bike the player is standing next to (for the HUD prompt).</summary>
        public static TrailBike InReach { get; private set; }

        public float Speed => _speed;
        public float MaxSpeed => maxSpeed;
        public bool IsRidden => _player != null && Ridden == this;
        public bool IsParking => _parkRequested;
        public bool OnTrack { get; private set; }

        private TrailBikeModel.Parts _parts;
        private Quaternion _steerBase;
        private Collider _collider;
        private TrailBikeEngine _engine;

        private FirstPersonController _player;
        private CharacterController _cc;
        private Transform _pivot, _playerParent;
        private CameraModeController _mode;

        private float _speed, _heading, _steerInput, _steerVelocity, _lean, _wheelAngle;
        private float _lookYaw, _lookPitch;
        private bool _parkRequested;
        private float _kick = 1f; // 1 = stand down
        private float _bump;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Ridden = null; InReach = null; }

        private void Awake()
        {
            // The scene builder pre-builds the placeholder under "Model" (so it shows in the editor); a purchased model
            // can go there instead, using the same pivot names for wheel and steering animation.
            var model = transform.Find("Model");
            if (model == null) _parts = TrailBikeModel.Build(transform);
            else
            {
                _parts.lean = FindDeep(model, "Lean") ?? model;
                _parts.steer = FindDeep(model, "Steer");
                _parts.frontWheel = FindDeep(model, "Front Wheel");
                _parts.rearWheel = FindDeep(model, "Rear Wheel");
                _parts.kickstand = FindDeep(model, "Kickstand");
                _parts.seat = FindDeep(model, "Seat Anchor");
                if (_parts.seat == null) _parts.seat = PrimitiveMeshes.CreatePivot("Seat Anchor", transform, new Vector3(0f, 0f, -0.3f));
            }
            if (_parts.steer != null) _steerBase = _parts.steer.localRotation;

            _collider = GetComponent<Collider>();
            if (_collider == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.6f, -0.05f);
                box.size = new Vector3(0.6f, 1.2f, 2.0f);
                _collider = box;
            }
            _engine = gameObject.AddComponent<TrailBikeEngine>();
            _heading = transform.eulerAngles.y;
            SnapToGround();
            ApplyPose(0f);
        }

        private void OnDisable()
        {
            if (Ridden == this) Ridden = null;
            if (InReach == this) InReach = null;
            PlayerPresence.EngineNoise = 0f;
            CameraModeController.RaiseBlocked = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (IsRidden) Ride(dt);
            else Parked();
            ApplyPose(dt);
        }

        // ------------------------------------------------------------------ parked

        private void Parked()
        {
            if (_player == null) _player = SceneQuery.FindOne<FirstPersonController>();
            bool near = _player != null && _player.enabled && Ridden == null &&
                        Vector3.Distance(_player.transform.position, transform.position) < mountDistance &&
                        Cursor.lockState == CursorLockMode.Locked && !UiOpen();
            if (near) InReach = this;
            else if (InReach == this) InReach = null;

            if (near && GameInput.InteractPressed) Mount();
            _kick = Mathf.MoveTowards(_kick, 1f, Time.deltaTime * 3f);
        }

        private static bool UiOpen()
        {
            var game = WildLensGame.Instance;
            return game != null && game.IsUiOpen;
        }

        private void Mount()
        {
            _cc = _player.GetComponent<CharacterController>();
            _pivot = _player.CameraPivot;
            _mode = _player.GetComponentInChildren<CameraModeController>();
            _playerParent = _player.transform.parent;

            // Look direction carries over: the view doesn't snap when you climb on.
            _lookYaw = Mathf.DeltaAngle(_heading, _player.transform.eulerAngles.y);
            _lookPitch = _pivot != null ? Mathf.DeltaAngle(0f, _pivot.localEulerAngles.x) : 0f;

            _player.enabled = false;
            if (_cc != null) _cc.enabled = false;
            _player.transform.SetParent(_parts.seat, false);
            float eye = _player.StandingEyeHeight;
            _player.transform.localPosition = new Vector3(0f, seatedEyeHeight - eye, 0f);
            if (_pivot != null) _pivot.localPosition = new Vector3(_pivot.localPosition.x, eye, _pivot.localPosition.z);
            if (_collider != null) _collider.enabled = false; // animals' sight lines must reach the rider

            Ridden = this;
            InReach = null;
            _parkRequested = false;
            _speed = 0f;
            _engine.StartEngine(true);
        }

        // ------------------------------------------------------------------ riding

        private void Ride(float dt)
        {
            FirstPersonController.UpdateCursorLock();
            bool control = Cursor.lockState == CursorLockMode.Locked && !UiOpen();
            bool cameraUp = _mode != null && _mode.IsActive;

            if (control && GameInput.InteractPressed) _parkRequested = true;

            Vector2 move = control && !cameraUp ? GameInput.Move : Vector2.zero;
            float throttle = _parkRequested ? 0f : move.y;
            _steerInput = Mathf.SmoothDamp(_steerInput, _parkRequested ? 0f : move.x, ref _steerVelocity, 0.18f);

            OnTrack = ReserveTrack.DistanceToAnyPath(transform.position, WildlifeWorldTrail()) < 2.6f;
            float top = OnTrack ? maxSpeed : offTrackMaxSpeed;
            float grade = Grade(); // degrees, + uphill
            float climb = Mathf.Sin(grade * Mathf.Deg2Rad) * 9.81f * 0.55f;

            if (_parkRequested) _speed = Mathf.MoveTowards(_speed, 0f, braking * dt);
            else if (throttle > 0.05f)
            {
                if (_speed < -0.05f) _speed = Mathf.MoveTowards(_speed, 0f, braking * dt);
                else _speed = Mathf.Min(top, _speed + (acceleration * throttle - climb) * dt);
            }
            else if (throttle < -0.05f)
            {
                if (_speed > 0.2f) _speed = Mathf.MoveTowards(_speed, 0f, braking * -throttle * dt);
                else _speed = Mathf.MoveTowards(_speed, -reverseSpeed, 1.5f * dt);
            }
            else _speed = Mathf.MoveTowards(_speed, 0f, ((OnTrack ? 0.6f : 1.2f) + Mathf.Max(0f, climb)) * dt);
            if (_speed > top) _speed = Mathf.MoveTowards(_speed, top, braking * 0.5f * dt); // left the track
            if (grade > maxClimbDegrees && _speed > 0f) _speed = Mathf.MoveTowards(_speed, 0f, braking * dt);

            // Steering: quick at walking pace, gentle at speed; no turning on the spot.
            float steerRate = Mathf.Lerp(steerRateSlow, steerRateFast, Mathf.Abs(_speed) / maxSpeed);
            _heading += _steerInput * steerRate * Mathf.Clamp01(Mathf.Abs(_speed) / 1.2f) * Mathf.Sign(_speed) * dt;

            Vector3 forward = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;
            Vector3 step = forward * (_speed * dt);
            if (step.sqrMagnitude > 0f && Blocked(step))
            {
                _bump = Mathf.Clamp01(Mathf.Abs(_speed) / 4f);
                _speed = 0f;
                step = Vector3.zero;
            }
            Vector3 next = transform.position + step;
            if (!TerrainUtil.FindTerrainAt(next)) { _speed = 0f; next = transform.position; } // edge of the world
            transform.position = next;
            SnapToGround();

            Look(dt, control);

            if (_parkRequested && Mathf.Abs(_speed) < 0.15f) Dismount();

            // Presence: a running engine is loud; a stopped bike idles.
            float load = Mathf.Abs(_speed) / maxSpeed;
            PlayerPresence.Report(_player.transform, transform.position, Mathf.Abs(_speed), false, false, cameraUp, seatedEyeHeight);
            PlayerPresence.EngineNoise = IsRidden ? Mathf.Lerp(1.3f, 3f, load) : 0f;
            CameraModeController.RaiseBlocked = IsRidden && Mathf.Abs(_speed) > 0.4f;
            _engine.SetLoad(load, Mathf.Clamp01(throttle));
            _kick = Mathf.MoveTowards(_kick, 0f, dt * 3f);
        }

        private TrailPath WildlifeWorldTrail()
        {
            var world = Wildlife.WildlifeWorld.Instance;
            return world != null ? world.Trail : null;
        }

        private void Look(float dt, bool control)
        {
            if (control)
            {
                float scale = lookSensitivity * (_mode != null ? _mode.LookSensitivityScale : 1f);
                Vector2 look = GameInput.Look * scale;
                _lookYaw = Mathf.Clamp(_lookYaw + look.x, -120f, 120f);
                _lookPitch = Mathf.Clamp(_lookPitch - look.y, -70f, 75f);
                // At speed, the head drifts back toward the road when you're not looking around.
                if (Mathf.Abs(_speed) > 3f && look.sqrMagnitude < 0.0001f)
                    _lookYaw = Mathf.MoveTowards(_lookYaw, 0f, 25f * dt);
            }
            _player.transform.localRotation = Quaternion.Euler(0f, _lookYaw, 0f);
            if (_pivot != null) _pivot.localRotation = Quaternion.Euler(_lookPitch, 0f, 0f);
        }

        private bool Blocked(Vector3 step)
        {
            Vector3 origin = transform.position + Vector3.up * 0.65f;
            float dist = step.magnitude + 0.9f; // front wheel reaches ~0.95 m ahead of centre
            var hits = Physics.SphereCastAll(origin, 0.28f, step.normalized, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider == _collider || h.collider is TerrainCollider) continue;
                if (_player != null && h.collider.transform.IsChildOf(_player.transform)) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue; // overlap at start: let it drive out
                return true;
            }
            return false;
        }

        private void Dismount()
        {
            _speed = 0f;
            _parkRequested = false;
            Vector3 right = transform.right;
            Vector3 spot = transform.position;
            foreach (var offset in new[] { -right * 0.95f, right * 0.95f, -transform.forward * 1.6f, transform.forward * 1.6f })
            {
                Vector3 c = TerrainUtil.Ground(transform.position + offset);
                if (!Physics.CheckCapsule(c + Vector3.up * 0.45f, c + Vector3.up * 1.5f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    spot = c;
                    break;
                }
            }

            float worldYaw = _heading + _lookYaw;
            _player.transform.SetParent(_playerParent, true);
            _player.transform.SetPositionAndRotation(spot + Vector3.up * 0.05f, Quaternion.Euler(0f, worldYaw, 0f));
            if (_cc != null) _cc.enabled = true;
            _player.enabled = true;
            _player.SyncLookFromTransform();
            if (_collider != null) _collider.enabled = true;

            Ridden = null;
            PlayerPresence.EngineNoise = 0f;
            CameraModeController.RaiseBlocked = false;
            _engine.StopEngine();
        }

        // ------------------------------------------------------------------ pose

        private float Grade()
        {
            float front = GroundHeight(transform.position + Flat(transform.forward) * TrailBikeModel.FrontAxleZ);
            float rear = GroundHeight(transform.position + Flat(transform.forward) * TrailBikeModel.RearAxleZ);
            float g = Mathf.Atan2(front - rear, TrailBikeModel.FrontAxleZ - TrailBikeModel.RearAxleZ) * Mathf.Rad2Deg;
            return _speed < 0f ? -g : g;
        }

        /// <summary>Terrain height, or the top of a bridge deck (<see cref="RideableSurface"/>) when riding over one.</summary>
        private float GroundHeight(Vector3 p)
        {
            float terrain = TerrainUtil.SampleHeight(p);
            var origin = new Vector3(p.x, Mathf.Max(terrain, transform.position.y) + 1.2f, p.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = terrain;
            foreach (var h in hits)
                if (h.point.y > best && h.collider.GetComponent<RideableSurface>() != null) best = h.point.y;
            return best;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0f ? v.normalized : Vector3.forward; }

        private void SnapToGround()
        {
            Vector3 fwd = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;
            float front = GroundHeight(transform.position + fwd * TrailBikeModel.FrontAxleZ);
            float rear = GroundHeight(transform.position + fwd * TrailBikeModel.RearAxleZ);
            float pitch = -Mathf.Atan2(front - rear, TrailBikeModel.FrontAxleZ - TrailBikeModel.RearAxleZ) * Mathf.Rad2Deg;
            Vector3 p = transform.position;
            p.y = (front + rear) * 0.5f;
            transform.SetPositionAndRotation(p, Quaternion.Euler(pitch, _heading, 0f));
        }

        private void ApplyPose(float dt)
        {
            // Corner lean when riding; resting on the stand (left) when parked; a small jolt after a bump.
            float targetLean = IsRidden
                ? -_steerInput * Mathf.Clamp01(Mathf.Abs(_speed) / 4f) * 18f
                : 7f * _kick;
            _lean = dt > 0f ? Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-6f * dt)) : targetLean;
            _bump = Mathf.MoveTowards(_bump, 0f, dt * 3f);
            if (_parts.lean != null)
                _parts.lean.localRotation = Quaternion.Euler(-_bump * 4f, 0f, _lean);

            if (_parts.steer != null)
                _parts.steer.localRotation = _steerBase * Quaternion.Euler(0f, _steerInput * Mathf.Lerp(28f, 8f, Mathf.Abs(_speed) / maxSpeed), 0f);
            _wheelAngle += _speed / TrailBikeModel.WheelRadius * Mathf.Rad2Deg * dt;
            var spin = Quaternion.Euler(_wheelAngle, 0f, 0f);
            if (_parts.frontWheel != null) _parts.frontWheel.localRotation = spin;
            if (_parts.rearWheel != null) _parts.rearWheel.localRotation = spin;
            if (_parts.kickstand != null) _parts.kickstand.localRotation = Quaternion.Euler(Mathf.Lerp(-80f, 18f, _kick), 0f, Mathf.Lerp(0f, -28f, _kick));
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
