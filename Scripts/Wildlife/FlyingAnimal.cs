using UnityEngine;
using WildLens.Core;
using WildLens.Data;

namespace WildLens.Wildlife
{
    /// <summary>
    /// Brain + flight model for soaring birds (Red-tailed Hawk).
    /// Soars in drifting circles on "thermals", glides to new thermals, and drops onto tall trees or rocks
    /// to perch, taking off again when disturbed. Velocity is steered with limited turn rate and the body banks
    /// into turns, so paths are smooth arcs rather than straight lines.
    /// </summary>
    public class FlyingAnimal : AnimalAgent
    {
        private enum Mode { Soaring, Transit, Descending, Perched, TakingOff }

        [SerializeField] private float minAltitude = 20f;
        [SerializeField] private float maxAltitude = 38f;
        [SerializeField] private float perchSearchRadius = 70f;

        private Mode _mode;
        private float _timer;
        private Vector3 _vel;

        private Vector3 _circleCenter;
        private float _circleRadius;
        private float _circleAngle;
        private float _circleDir;
        private float _altitude;

        private Vector3 _transitTarget;
        private Vector3 _perchPoint;
        private float _perchYaw;
        private float _perchShiftTimer;
        private float _bank;

        public bool IsFlapping { get; private set; }
        public bool IsPerched => _mode == Mode.Perched;
        /// <summary>Roll angle in degrees about the body's forward axis (negative = right wing down).</summary>
        public float Bank => _bank;

        private float Cruise => Species.walkSpeed * Temperament;

        public override void Initialize(SpeciesDefinition species, Vector3 home, WildlifeWorld world, int seed)
        {
            base.Initialize(species, home, world, seed);
            _altitude = Range(minAltitude, maxAltitude);
            _circleDir = Chance(0.5f) ? 1f : -1f;
            BeginCircle(home, Range(0f, Mathf.PI * 2f));
            Vector3 start = CirclePoint(_circleAngle);
            transform.position = start;
            _vel = CircleTangent(_circleAngle) * Cruise;
            transform.rotation = Quaternion.LookRotation(_vel);
        }

        protected override void Tick(float dt)
        {
            _timer -= dt;
            IsFlapping = false;
            AttentionPoint = null;

            switch (_mode)
            {
                case Mode.Soaring: TickSoaring(dt); break;
                case Mode.Transit: TickTransit(dt); break;
                case Mode.Descending: TickDescending(dt); break;
                case Mode.Perched: TickPerched(dt); return;
                case Mode.TakingOff: TickTakeOff(dt); break;
            }

            Integrate(dt);
        }

        // ------------------------------------------------------------------ modes

        private void BeginCircle(Vector3 center, float angle)
        {
            _mode = Mode.Soaring;
            _circleCenter = TerrainUtil.Flat(center);
            _circleRadius = Range(16f, 34f);
            _circleAngle = angle;
            _timer = Range(DurationFor(AnimalActivity.Soaring, new Vector2(20f, 40f)));
            SetActivity(AnimalActivity.Soaring);
        }

        private Vector3 CirclePoint(float angle)
        {
            Vector3 p = _circleCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _circleRadius;
            p.y = TerrainUtil.SampleHeight(_circleCenter) + _altitude + Noise(0.07f, 3f) * 4f;
            return p;
        }

        private Vector3 CircleTangent(float angle) =>
            new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _circleDir;

        private void TickSoaring(float dt)
        {
            _circleAngle += _circleDir * (Cruise / _circleRadius) * dt;
            // Thermals drift with the wind.
            _circleCenter += new Vector3(Noise(0.05f, 1f), 0f, Noise(0.05f, 2f)) * (0.8f * dt);

            Vector3 desired = CirclePoint(_circleAngle + _circleDir * 0.35f);
            Steer(desired, Cruise, 0.9f, dt);
            IsFlapping = desired.y - transform.position.y > 3f || Noise(0.15f, 7f) > 0.8f;

            if (_timer <= 0f) ChooseNextFlight();
        }

        private void ChooseNextFlight()
        {
            var option = Species.PickBehavior(Rng);
            var activity = option != null ? option.activity : AnimalActivity.Soaring;

            if (activity == AnimalActivity.Perched && TryStartPerch()) return;

            if (activity == AnimalActivity.Gliding)
            {
                // Glide to a new thermal within the territory.
                Vector2 r = Random.insideUnitCircle * Species.wanderRadius;
                _transitTarget = HomePosition + new Vector3(r.x, 0f, r.y);
                _mode = Mode.Transit;
                _timer = 40f;
                SetActivity(AnimalActivity.Gliding);
                return;
            }

            _altitude = Mathf.Clamp(_altitude + Range(-6f, 6f), minAltitude, maxAltitude);
            BeginCircle(_circleCenter, _circleAngle);
        }

        private void TickTransit(float dt)
        {
            Vector3 target = _transitTarget;
            target.y = TerrainUtil.SampleHeight(target) + _altitude;
            Steer(target, Cruise * 1.2f, 0.6f, dt);
            if (TerrainUtil.FlatDistance(transform.position, target) < 15f || _timer <= 0f)
            {
                Vector3 pos = transform.position;
                float angle = Mathf.Atan2(pos.z - target.z, pos.x - target.x);
                BeginCircle(target, angle);
            }
        }

        private bool TryStartPerch()
        {
            if (World == null || !World.TryGetRandomPerch(transform.position, perchSearchRadius, out var perch)) return false;
            if (TryGetPlayer(out Vector3 player, out _) && TerrainUtil.FlatDistance(player, perch.basePosition) < Species.alertDistance)
                return false;

            _perchPoint = perch.Top + Vector3.up * 0.02f;
            _mode = Mode.Descending;
            _timer = 30f;
            SetActivity(AnimalActivity.Gliding);
            return true;
        }

        private void TickDescending(float dt)
        {
            Vector3 to = _perchPoint - transform.position;
            float dist = to.magnitude;
            float speed = Mathf.Lerp(2.5f, Cruise, Mathf.Clamp01(dist / 30f));
            Steer(_perchPoint, speed, dist < 15f ? 3f : 1.2f, dt);
            IsFlapping = dist < 8f; // braking flaps on approach

            if (dist < 0.6f)
            {
                transform.position = _perchPoint;
                Vector3 flatVel = TerrainUtil.Flat(_vel);
                _perchYaw = flatVel.sqrMagnitude > 0.01f ? Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
                _vel = Vector3.zero;
                Velocity = Vector3.zero;
                _bank = 0f;
                _mode = Mode.Perched;
                _timer = Range(DurationFor(AnimalActivity.Perched, new Vector2(10f, 20f)));
                _perchShiftTimer = Range(3f, 7f);
                SetActivity(AnimalActivity.Perched);
            }
            else if (_timer <= 0f)
            {
                BeginCircle(transform.position, 0f);
            }
        }

        private void TickPerched(float dt)
        {
            Velocity = Vector3.zero;
            bool disturbed = false;
            if (TryGetPlayer(out Vector3 player, out float dist))
            {
                float scale = PlayerPresence.DisturbanceScale;
                if (dist < Species.alertDistance * scale) AttentionPoint = player;
                disturbed = dist < Species.fleeDistance * scale;
            }

            _perchShiftTimer -= dt;
            if (_perchShiftTimer <= 0f)
            {
                _perchShiftTimer = Range(4f, 9f);
                _perchYaw += Range(-70f, 70f);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _perchYaw, 0f), 1f - Mathf.Exp(-3f * dt));

            if (disturbed || _timer <= 0f)
            {
                _mode = Mode.TakingOff;
                _timer = 12f;
                _vel = transform.forward * 3f + Vector3.up * 2.5f;
                SetActivity(disturbed ? AnimalActivity.Fleeing : AnimalActivity.Soaring);
            }
        }

        private void TickTakeOff(float dt)
        {
            Vector3 fwd = TerrainUtil.Flat(_vel);
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            Vector3 target = transform.position + fwd.normalized * 20f + Vector3.up * 12f;
            Steer(target, Cruise, 1.5f, dt);
            IsFlapping = true;

            float height = transform.position.y - TerrainUtil.SampleHeight(transform.position);
            if (height > _altitude * 0.7f || _timer <= 0f)
            {
                // Join a circle that passes through the current position.
                Vector3 right = Vector3.Cross(Vector3.up, fwd.normalized) * _circleDir;
                float radius = Range(16f, 34f);
                Vector3 center = transform.position + right * radius;
                BeginCircle(center, Mathf.Atan2(transform.position.z - center.z, transform.position.x - center.x));
                _circleRadius = radius;
            }
        }

        // ------------------------------------------------------------------ flight model

        private void Steer(Vector3 target, float speed, float turnRateRadians, float dt)
        {
            Vector3 desired = target - transform.position;
            if (desired.sqrMagnitude < 0.0001f) return;
            desired = desired.normalized * speed;
            if (_vel.sqrMagnitude < 0.01f) _vel = desired.normalized * 0.5f;
            _vel = Vector3.RotateTowards(_vel, desired, turnRateRadians * dt, 4f * dt);
        }

        private void Integrate(float dt)
        {
            Vector3 prevFlat = TerrainUtil.Flat(transform.forward);
            Vector3 pos = transform.position + _vel * dt;

            if (_mode != Mode.Descending)
            {
                float minY = TerrainUtil.SampleHeight(pos) + 3f;
                if (pos.y < minY) { pos.y = minY; if (_vel.y < 0f) _vel.y = 0f; }
            }
            transform.position = pos;
            Velocity = _vel;

            if (_vel.sqrMagnitude > 0.01f)
            {
                Vector3 newFlat = TerrainUtil.Flat(_vel);
                float yawRate = Vector3.SignedAngle(prevFlat, newFlat, Vector3.up) / dt;
                float targetBank = Mathf.Clamp(-yawRate * 0.9f, -35f, 35f);
                _bank = Mathf.Lerp(_bank, targetBank, 1f - Mathf.Exp(-3f * dt));
                Quaternion look = Quaternion.LookRotation(_vel.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look * Quaternion.Euler(0f, 0f, _bank), 1f - Mathf.Exp(-6f * dt));
            }

            if (IsFlapping) GaitPhase = Mathf.Repeat(GaitPhase + dt * Species.gaitFrequency * 2f * Mathf.PI, Mathf.PI * 2f);
        }

        private Vector2 DurationFor(AnimalActivity activity, Vector2 fallback)
        {
            if (Species.behaviors != null)
                foreach (var b in Species.behaviors)
                    if (b.activity == activity) return b.duration;
            return fallback;
        }
    }
}
