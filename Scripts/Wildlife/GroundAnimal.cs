using UnityEngine;
using WildLens.Core;
using WildLens.Data;

namespace WildLens.Wildlife
{
    /// <summary>
    /// Brain + locomotion for ground animals (deer, rabbit, fox, squirrel).
    ///
    /// Modes:
    ///   Moving    wander legs, shown as Walking / Hopping, or an occasional Running leg (deer bounding)
    ///   Behaving  species behaviours: Grazing, Sitting, LookingAround, Grooming, Sniffing, Resting, Foraging…
    ///   Watching  reaction to <see cref="WildlifeAwareness"/>: a brief glance when Aware, a fixed stare when Alert
    ///   Fleeing   run away from the threat (tree species climb the nearest trunk instead)
    ///   Climbing  squirrels up/down trunks
    ///
    /// Perception lives in <see cref="WildlifeAwareness"/>; this class only reacts to its state changes.
    /// Movement is steered rather than tweened: heading is smoothed, speed accelerates, paths meander with noise,
    /// trunks are avoided and the body tilts with the terrain, which keeps it from looking robotic.
    /// </summary>
    public class GroundAnimal : AnimalAgent
    {
        private enum Mode { Moving, Behaving, Watching, Fleeing, Climbing }
        private enum ClimbPhase { Approach, Up, Hold, Down }

        private Mode _mode;
        private float _modeTimer;

        // Steering
        private Vector3 _target;
        private bool _hasTarget;
        private Vector3? _faceTarget;
        private float _desiredSpeed;
        private float _heading;
        private float _headingVelocity;
        private float _speed;

        // Gait variation
        private bool _burstPaused;
        private float _burstTimer;
        private float _grazeStepTimer;
        private float _grazeStepRemaining;

        // Awareness reactions
        private float _nextGlance;

        // Climbing
        private FeaturePoint _tree;
        private ClimbPhase _climbPhase;
        private float _climbHeight;
        private float _climbTargetHeight;
        private float _trunkAngle;
        private bool _climbIsEscape;

        public bool IsOnTrunk => _mode == Mode.Climbing && _climbPhase != ClimbPhase.Approach;

        private AwarenessState AwarenessNow => Awareness != null ? Awareness.State : AwarenessState.Calm;

        public override void Initialize(SpeciesDefinition species, Vector3 home, WildlifeWorld world, int seed)
        {
            base.Initialize(species, home, world, seed);
            _heading = Range(0f, 360f);
            transform.position = TerrainUtil.Ground(transform.position);
            transform.rotation = Quaternion.Euler(0f, _heading, 0f);
            if (Awareness != null) Awareness.StateChanged += OnAwarenessChanged;

            // Start mid-behaviour so a group doesn't all set off walking on frame one.
            if (Chance(0.65f)) StartBehavior(species.PickBehavior(Rng));
            else StartMove(PickWanderTarget(), species.walkSpeed, false);
        }

        private void OnDestroy()
        {
            if (Awareness != null) Awareness.StateChanged -= OnAwarenessChanged;
        }

        protected override void Tick(float dt)
        {
            if (Awareness != null) Awareness.Sheltered = IsOnTrunk;
            _modeTimer -= dt;

            switch (_mode)
            {
                case Mode.Moving: TickMoving(dt); break;
                case Mode.Behaving: TickBehaving(dt); break;
                case Mode.Watching: TickWatching(); break;
                case Mode.Fleeing: TickFleeing(); break;
                case Mode.Climbing:
                    if (TickClimbing(dt)) return; // on the trunk: climbing places the body itself
                    break;
            }

            Locomote(dt);
        }

        // ------------------------------------------------------------------ awareness reactions

        private void OnAwarenessChanged(AwarenessState previous, AwarenessState next)
        {
            if (IsOnTrunk)
            {
                // Already safe: a close approach just sends it higher.
                if (next == AwarenessState.Fleeing && _climbPhase == ClimbPhase.Hold)
                {
                    _climbTargetHeight = Mathf.Min(_tree.height * 0.75f, _climbHeight + 2f);
                    _climbPhase = ClimbPhase.Up;
                    SetActivity(AnimalActivity.Climbing);
                }
                return;
            }

            switch (next)
            {
                case AwarenessState.Fleeing:
                    if (_mode != Mode.Fleeing && !(_mode == Mode.Climbing && _climbIsEscape)) StartFlee(Awareness.ThreatPosition);
                    break;
                case AwarenessState.Alert:
                    if (_mode != Mode.Fleeing) StartWatching(true);
                    break;
                case AwarenessState.Aware:
                    if (previous == AwarenessState.Calm && _mode != Mode.Fleeing) StartWatching(false);
                    else if (previous == AwarenessState.Alert && _mode == Mode.Watching) ResumeAfterWatching();
                    _nextGlance = Range(3f, 7f);
                    break;
                case AwarenessState.Calm:
                    if (_mode == Mode.Watching) ResumeAfterWatching();
                    break;
            }
        }

        /// <summary>Aware: a brief glance at the observer. Alert: stop, turn and stare until awareness changes.</summary>
        private void StartWatching(bool alert)
        {
            _mode = Mode.Watching;
            _modeTimer = alert ? float.MaxValue : Range(1.5f, 3.5f);
            _hasTarget = false;
            _desiredSpeed = 0f;
            _faceTarget = alert ? Awareness.ThreatPosition : (Vector3?)null;
            AttentionPoint = Awareness.ThreatPosition;
            SetActivity(alert ? AnimalActivity.Alert : AnimalActivity.LookingAround);
        }

        private void TickWatching()
        {
            if (Awareness == null) { BeginNextAction(false); return; }
            AttentionPoint = Awareness.ThreatPosition;
            if (Activity == AnimalActivity.Alert) _faceTarget = Awareness.ThreatPosition;
            if (_modeTimer <= 0f) ResumeAfterWatching();
        }

        private void ResumeAfterWatching()
        {
            _faceTarget = null;
            AttentionPoint = null;
            BeginNextAction(false);
        }

        /// <summary>While Aware, feeding or wandering animals keep glancing up at the observer.</summary>
        private void TickGlances(float dt)
        {
            if (AwarenessNow != AwarenessState.Aware) return;
            _nextGlance -= dt;
            if (_nextGlance > 0f) return;
            _nextGlance = Range(3f, 7f);
            StartWatching(false);
        }

        // ------------------------------------------------------------------ modes

        private void BeginNextAction(bool afterMove)
        {
            if (afterMove && Chance(Species.behaviorChance)) { StartBehavior(Species.PickBehavior(Rng)); return; }
            // Calm animals occasionally break into a short run (deer bounding); suspicious ones never do.
            bool run = AwarenessNow == AwarenessState.Calm && Chance(Species.runLegChance);
            float speed = run ? Species.runSpeed * Range(0.45f, 0.65f) : Species.walkSpeed * Range(0.85f, 1.15f);
            StartMove(PickWanderTarget(), speed, run);
        }

        private void StartMove(Vector3 target, float speed, bool run)
        {
            _mode = Mode.Moving;
            _modeTimer = 30f;
            _target = target;
            _hasTarget = true;
            _faceTarget = null;
            _desiredSpeed = speed;
            _burstPaused = false;
            _burstTimer = Range(0.4f, 1.4f);
            AttentionPoint = null;
            SetActivity(run ? AnimalActivity.Running : Species.gait == GaitStyle.Hop ? AnimalActivity.Hopping : AnimalActivity.Walking);
        }

        private void StartBehavior(BehaviorOption option)
        {
            if (option == null) option = new BehaviorOption(AnimalActivity.LookingAround, 1f, 2f, 4f);
            if (option.activity == AnimalActivity.Climbing)
            {
                if (TryStartClimb(12f, false)) return;
                option = new BehaviorOption(AnimalActivity.Foraging, 1f, 2f, 4f);
            }
            // A suspicious animal doesn't lie down to rest.
            if (option.activity == AnimalActivity.Resting && AwarenessNow != AwarenessState.Calm)
                option = new BehaviorOption(AnimalActivity.LookingAround, 1f, 2f, 4f);

            _mode = Mode.Behaving;
            _modeTimer = Range(option.duration) * Temperament;
            _hasTarget = false;
            _faceTarget = null;
            _desiredSpeed = 0f;
            _grazeStepTimer = Range(2f, 5f);
            _grazeStepRemaining = 0f;
            AttentionPoint = null;
            SetActivity(option.activity);
        }

        private void StartFlee(Vector3 threat)
        {
            if (Species.habitat == HabitatPreference.Trees && TryStartClimb(10f, true)) return;

            Vector3 pos = transform.position;
            Vector3 away = TerrainUtil.Flat(pos - threat);
            if (away.sqrMagnitude < 0.01f) away = Dir(_heading);
            float baseAngle = Angle(away);
            float distance = Range(Species.fleeDistanceRange);

            Vector3 target = pos + Dir(baseAngle) * distance;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = TerrainUtil.Ground(pos + Dir(baseAngle + Gaussianish() * 50f) * distance);
                if (IsWalkable(c)) { target = c; break; }
            }

            _mode = Mode.Fleeing;
            _modeTimer = 10f;
            _target = target;
            _hasTarget = true;
            _faceTarget = null;
            _desiredSpeed = Species.runSpeed * Temperament;
            AttentionPoint = null;
            SetActivity(AnimalActivity.Fleeing);
        }

        private void TickMoving(float dt)
        {
            if (ArrivedOrTimedOut()) { BeginNextAction(true); return; }
            TickGlances(dt);
            if (_mode != Mode.Moving) return;

            if (Species.gait == GaitStyle.Bursty && Activity != AnimalActivity.Running)
            {
                // Dash-and-freeze foraging pattern.
                _burstTimer -= dt;
                if (_burstTimer <= 0f)
                {
                    _burstPaused = !_burstPaused;
                    _burstTimer = _burstPaused ? Range(0.3f, 1.1f) : Range(0.4f, 1.4f);
                }
                _desiredSpeed = _burstPaused ? 0f : Species.walkSpeed * Temperament;
            }
        }

        private void TickBehaving(float dt)
        {
            if (_modeTimer <= 0f) { BeginNextAction(false); return; }
            TickGlances(dt);
            if (_mode != Mode.Behaving) return;

            // Grazers and foragers shuffle a step or two while feeding.
            if (Activity != AnimalActivity.Grazing && Activity != AnimalActivity.Foraging) return;
            if (_grazeStepRemaining > 0f)
            {
                _grazeStepRemaining -= dt;
                if (_grazeStepRemaining <= 0f) { _hasTarget = false; _desiredSpeed = 0f; }
                return;
            }
            _grazeStepTimer -= dt;
            if (_grazeStepTimer <= 0f)
            {
                _grazeStepTimer = Range(3f, 7f);
                _grazeStepRemaining = Range(0.6f, 1.4f);
                Vector3 c = TerrainUtil.Ground(transform.position + Dir(_heading + Gaussianish() * 60f) * Range(0.6f, 1.6f));
                if (IsWalkable(c))
                {
                    _target = c;
                    _hasTarget = true;
                    _desiredSpeed = Species.walkSpeed * 0.4f;
                }
            }
        }

        private void TickFleeing()
        {
            if (!ArrivedOrTimedOut()) return;
            if (Awareness != null) Awareness.EndFlee();
            // After bolting, animals typically stop and look back: a second chance for a photo.
            StartBehavior(new BehaviorOption(AnimalActivity.LookingAround, 1f, 4f, 8f));
            if (Awareness != null && Awareness.State != AwarenessState.Calm) AttentionPoint = Awareness.ThreatPosition;
        }

        private bool ArrivedOrTimedOut()
        {
            float arrive = 0.4f + _speed * 0.25f;
            return _modeTimer <= 0f || TerrainUtil.FlatDistance(transform.position, _target) < arrive;
        }

        // ------------------------------------------------------------------ climbing (tree species)

        private bool TryStartClimb(float searchRadius, bool escape)
        {
            if (World == null || !World.TryGetNearestTree(transform.position, searchRadius, out var tree)) return false;
            if (tree.height < 3f) return false;

            _tree = tree;
            _climbIsEscape = escape;
            _climbPhase = ClimbPhase.Approach;
            _trunkAngle = Angle(TerrainUtil.Flat(transform.position - tree.basePosition));
            _climbTargetHeight = Mathf.Min(escape ? Range(3.5f, 6f) : Range(1.8f, 4.5f), tree.height * 0.55f);

            _mode = Mode.Climbing;
            _modeTimer = 15f;
            _target = TerrainUtil.Ground(tree.basePosition + Dir(_trunkAngle) * (tree.radius + 0.12f));
            _hasTarget = true;
            _faceTarget = null;
            _desiredSpeed = escape ? Species.runSpeed : Species.walkSpeed * 1.2f;
            AttentionPoint = null;
            SetActivity(escape ? AnimalActivity.Fleeing : AnimalActivity.Wandering);
            return true;
        }

        /// <summary>Returns true while the animal is on the trunk (normal locomotion skipped).</summary>
        private bool TickClimbing(float dt)
        {
            switch (_climbPhase)
            {
                case ClimbPhase.Approach:
                    if (TerrainUtil.FlatDistance(transform.position, _target) < (_climbIsEscape ? 0.5f : 0.3f) || _modeTimer <= 0f)
                    {
                        _climbPhase = ClimbPhase.Up;
                        _climbHeight = 0f;
                        _speed = 0f;
                        SetActivity(AnimalActivity.Climbing);
                        return true;
                    }
                    return false;

                case ClimbPhase.Up:
                {
                    float rate = _climbIsEscape ? 3f : 1.4f;
                    // Short bursts up the trunk.
                    float burst = Mathf.Max(0f, Mathf.Sin(Time.time * 5f + NoiseSeed)) * 1.6f + 0.2f;
                    _climbHeight += rate * burst * dt;
                    if (_climbHeight >= _climbTargetHeight)
                    {
                        _climbHeight = _climbTargetHeight;
                        _climbPhase = ClimbPhase.Hold;
                        _modeTimer = _climbIsEscape ? Range(6f, 12f) : Range(3f, 8f);
                        SetActivity(AnimalActivity.Clinging);
                        if (_climbIsEscape && Awareness != null) Awareness.EndFlee();
                    }
                    PlaceOnTrunk(dt, true, rate * burst);
                    return true;
                }

                case ClimbPhase.Hold:
                    _trunkAngle += Noise(0.3f, 5f) * 25f * dt; // shuffles around the trunk
                    if (_modeTimer <= 0f)
                    {
                        _climbPhase = ClimbPhase.Down;
                        SetActivity(AnimalActivity.Climbing);
                    }
                    PlaceOnTrunk(dt, true, 0f);
                    return true;

                case ClimbPhase.Down:
                    _climbHeight -= 1.1f * dt;
                    if (_climbHeight <= 0.02f)
                    {
                        _climbHeight = 0f;
                        _heading = _trunkAngle;
                        _headingVelocity = 0f;
                        transform.position = TerrainUtil.Ground(transform.position + Dir(_trunkAngle) * 0.15f);
                        transform.rotation = Quaternion.Euler(0f, _heading, 0f);
                        BeginNextAction(true);
                        return true;
                    }
                    // Tree squirrels descend head-first.
                    PlaceOnTrunk(dt, false, 1.1f);
                    return true;
            }
            return false;
        }

        private void PlaceOnTrunk(float dt, bool headUp, float verticalSpeed)
        {
            Vector3 outward = Dir(_trunkAngle);
            Vector3 basePos = _tree.basePosition + outward * (_tree.radius + 0.05f);
            Vector3 pos = new Vector3(basePos.x, TerrainUtil.SampleHeight(basePos) + _climbHeight, basePos.z);

            Vector3 previous = transform.position;
            transform.position = pos;
            Quaternion targetRot = Quaternion.LookRotation(headUp ? Vector3.up : Vector3.down, outward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-12f * dt));

            Velocity = (pos - previous) / dt;
            if (verticalSpeed > 0.01f) GaitPhase += dt * Species.gaitFrequency * 2f * Mathf.PI;
        }

        // ------------------------------------------------------------------ locomotion

        private void Locomote(float dt)
        {
            Vector3 pos = transform.position;
            float desiredHeading = _heading;
            bool fleeing = _mode == Mode.Fleeing || (_mode == Mode.Climbing && _climbIsEscape);

            if (_hasTarget && _desiredSpeed > 0.01f)
            {
                Vector3 to = TerrainUtil.Flat(_target - pos);
                if (to.sqrMagnitude > 0.0025f) desiredHeading = Angle(to);

                // Meandering: gentle drift when relaxed, zig-zag when a rabbit bolts.
                float amp = fleeing ? (Species.gait == GaitStyle.Hop ? 40f : 8f) : 14f;
                float freq = fleeing ? 1.3f : 0.25f;
                desiredHeading += Noise(freq, 11f) * amp;

                // (Not while heading for a trunk to climb — the trunk is the destination.)
                if (_mode != Mode.Climbing) desiredHeading += ObstacleAvoidance(pos);
            }
            else if (_faceTarget.HasValue)
            {
                // Turn the body only when the head alone can't reach.
                float a = Angle(TerrainUtil.Flat(_faceTarget.Value - pos));
                if (Mathf.Abs(Mathf.DeltaAngle(_heading, a)) > 45f) desiredHeading = a;
            }

            float maxTurn = Species.turnSpeed * (fleeing ? 1.6f : 1f);
            _heading = Mathf.SmoothDampAngle(_heading, desiredHeading, ref _headingVelocity, fleeing ? 0.15f : 0.45f, maxTurn, dt);

            float turnError = Mathf.Abs(Mathf.DeltaAngle(_heading, desiredHeading));
            float targetSpeed = _hasTarget ? _desiredSpeed * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(turnError / 120f)) : 0f;
            if (_hasTarget && !fleeing)
                targetSpeed = Mathf.Min(targetSpeed, Mathf.Max(0.25f, TerrainUtil.FlatDistance(pos, _target) * 1.2f));

            float accel = targetSpeed > _speed ? Species.runSpeed * 0.9f : Species.runSpeed * 1.6f;
            _speed = Mathf.MoveTowards(_speed, targetSpeed, accel * dt);

            if (_speed > 0.02f)
            {
                float rel = _speed / Mathf.Max(0.1f, Species.walkSpeed);
                GaitPhase = Mathf.Repeat(GaitPhase + dt * Species.gaitFrequency * Mathf.Pow(rel, 0.55f) * 2f * Mathf.PI, Mathf.PI * 2f);
            }

            float moveSpeed = _speed;
            if (Species.gait == GaitStyle.Hop)
                moveSpeed *= Mathf.Max(0f, Mathf.Sin(GaitPhase)) * Mathf.PI; // forward motion only while airborne

            Vector3 next = pos + Dir(_heading) * (moveSpeed * dt);
            next.y = TerrainUtil.SampleHeight(next);

            if (moveSpeed > 0.01f && TerrainUtil.SlopeDegrees(next) > Species.maxSlope)
            {
                next = new Vector3(pos.x, TerrainUtil.SampleHeight(pos), pos.z);
                _speed *= 0.5f;
                if (_mode == Mode.Moving) _target = PickWanderTarget();
            }

            Velocity = (next - pos) / dt;
            transform.position = next;

            Vector3 up = Vector3.Slerp(Vector3.up, TerrainUtil.SampleNormal(next), 0.65f);
            Vector3 fwd = Vector3.ProjectOnPlane(Dir(_heading), up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Dir(_heading);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd.normalized, up), 1f - Mathf.Exp(-10f * dt));
        }

        private float ObstacleAvoidance(Vector3 pos)
        {
            if (World == null) return 0f;
            Vector3 forward = Dir(_heading);
            Vector3 probe = pos + forward * 1.2f;
            if (!World.TryGetNearestObstacle(probe, 3f, out var ob)) return 0f;
            Vector3 away = TerrainUtil.Flat(probe - ob.basePosition);
            float clearance = ob.radius + 1.2f;
            float d = away.magnitude;
            if (d >= clearance) return 0f;
            float side = Vector3.Cross(forward, away).y >= 0f ? 1f : -1f;
            return side * 60f * (1f - d / clearance);
        }

        private Vector3 PickWanderTarget()
        {
            Vector3 pos = transform.position;
            float radius = Species.wanderRadius;
            for (int i = 0; i < 10; i++)
            {
                Vector3 toHome = TerrainUtil.Flat(HomePosition - pos);
                float homePull = Mathf.InverseLerp(radius * 0.4f, radius, toHome.magnitude);
                float wanderAngle = _heading + Gaussianish() * 110f;
                float homeAngle = toHome.sqrMagnitude > 0.01f ? Angle(toHome) + Gaussianish() * 40f : wanderAngle;
                float angle = Mathf.LerpAngle(wanderAngle, homeAngle, homePull);
                if (AwarenessNow != AwarenessState.Calm && Awareness != null)
                {
                    // A wary animal drifts away from the observer rather than toward them.
                    Vector3 away = TerrainUtil.Flat(pos - Awareness.ThreatPosition);
                    if (away.sqrMagnitude > 0.01f) angle = Mathf.LerpAngle(angle, Angle(away) + Gaussianish() * 50f, 0.6f);
                }
                Vector3 c = TerrainUtil.Ground(pos + Dir(angle) * Range(radius * 0.15f, radius * 0.5f));
                if (IsWalkable(c) && TerrainUtil.FlatDistance(c, HomePosition) <= radius) return c;
            }
            Vector2 r = Random.insideUnitCircle * radius * 0.3f;
            return TerrainUtil.Ground(HomePosition + new Vector3(r.x, 0f, r.y));
        }

        private bool IsWalkable(Vector3 p)
        {
            if (TerrainUtil.SlopeDegrees(p) > Species.maxSlope) return false;
            if (World != null && World.TryGetNearestObstacle(p, 3f, out var ob) &&
                TerrainUtil.FlatDistance(p, ob.basePosition) < ob.radius + 0.4f) return false;
            return true;
        }

        private static Vector3 Dir(float headingDegrees) => Quaternion.Euler(0f, headingDegrees, 0f) * Vector3.forward;
        private static float Angle(Vector3 flatDirection) => Mathf.Atan2(flatDirection.x, flatDirection.z) * Mathf.Rad2Deg;
    }
}
