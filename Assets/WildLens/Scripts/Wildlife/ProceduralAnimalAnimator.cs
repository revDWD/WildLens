using UnityEngine;
using WildLens.Data;

namespace WildLens.Wildlife
{
    /// <summary>
    /// Placeholder animation driven entirely by code: gait-synced leg swing, body bob, hops, breathing,
    /// activity-specific head/neck poses (grazing, sniffing, scanning, grooming, alert gaze), ear twitches,
    /// tail flicks, and for birds flapping, gliding, banking and wing folding.
    /// All poses blend smoothly so state changes never snap. Replace with authored clips later.
    /// </summary>
    [RequireComponent(typeof(AnimalAgent))]
    public class ProceduralAnimalAnimator : MonoBehaviour
    {
        private AnimalAgent _agent;
        private FlyingAnimal _flyer;
        private AnimalRig _rig;

        private Vector3 _bodyRestPos, _bodyRestScale;
        private Quaternion _bodyRestRot, _neckRest, _headRest, _tailRest;
        private Quaternion[] _legRest, _earRest, _wingRest;
        private Vector3[] _wingRestScale;

        private Vector3 _neckOffset, _headOffset, _tailOffset;
        private float _moveBlend, _restBlend, _flapBlend, _foldBlend;
        private float _seed;
        private float _earTimer, _earAmount;
        private int _earIndex;
        private float _groomSide = 1f;

        private void Start()
        {
            _agent = GetComponent<AnimalAgent>();
            _flyer = _agent as FlyingAnimal;
            _rig = GetComponent<AnimalRig>();
            if (_rig == null || _rig.body == null) { enabled = false; return; }

            _seed = Random.Range(0f, 100f);
            _bodyRestPos = _rig.body.localPosition;
            _bodyRestRot = _rig.body.localRotation;
            _bodyRestScale = _rig.body.localScale;
            if (_rig.neck) _neckRest = _rig.neck.localRotation;
            if (_rig.head) _headRest = _rig.head.localRotation;
            if (_rig.tail) _tailRest = _rig.tail.localRotation;
            _legRest = CacheRotations(_rig.legs);
            _earRest = CacheRotations(_rig.ears);
            _wingRest = CacheRotations(_rig.wings);
            _wingRestScale = new Vector3[_rig.wings.Length];
            for (int i = 0; i < _rig.wings.Length; i++) _wingRestScale[i] = _rig.wings[i] ? _rig.wings[i].localScale : Vector3.one;
            _earTimer = Random.Range(1f, 4f);
        }

        private static Quaternion[] CacheRotations(Transform[] t)
        {
            var r = new Quaternion[t.Length];
            for (int i = 0; i < t.Length; i++) r[i] = t[i] ? t[i].localRotation : Quaternion.identity;
            return r;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _agent.Species == null) return;
            if (_flyer != null) AnimateBird(dt);
            else AnimateQuadruped(dt);
        }

        private static float Blend(float dt, float rate) => 1f - Mathf.Exp(-rate * dt);

        private float Perlin(float speed, float offset) => Mathf.PerlinNoise(_seed + offset, Time.time * speed) * 2f - 1f;

        // ------------------------------------------------------------------ quadrupeds

        private void AnimateQuadruped(float dt)
        {
            var s = _agent.Species;
            float speed = _agent.Speed;
            float t = Time.time + _seed;
            float phase = _agent.GaitPhase;
            AnimalActivity a = _agent.Activity;
            float legLen = Mathf.Max(0.05f, _rig.legLength);

            float run = Mathf.InverseLerp(s.walkSpeed * 1.3f, s.runSpeed, speed);
            _moveBlend = Mathf.Lerp(_moveBlend, speed > 0.05f ? 1f : 0f, Blend(dt, 8f));
            _restBlend = Mathf.Lerp(_restBlend, a == AnimalActivity.Resting ? 1f : 0f, Blend(dt, 1.5f));
            bool onTrunk = a == AnimalActivity.Climbing || a == AnimalActivity.Clinging;

            // Legs ----------------------------------------------------------
            float swing = Mathf.Lerp(22f, 42f, run) * _moveBlend;
            for (int i = 0; i < _rig.legs.Length && i < 4; i++)
            {
                if (_rig.legs[i] == null) continue;
                bool front = i < 2, left = i % 2 == 0;
                float angle;
                if (s.gait == GaitStyle.Hop)
                    angle = front ? Mathf.Sin(phase + 0.6f) * swing : -Mathf.Sin(phase) * swing * 1.3f;
                else if (run > 0.5f || onTrunk)
                    angle = Mathf.Sin(phase + (front ? 0f : Mathf.PI * 0.8f) + (left ? 0f : 0.25f)) * swing; // gallop / bound
                else
                    angle = Mathf.Sin(phase + ((front == left) ? 0f : Mathf.PI)) * swing; // diagonal walk / trot
                float fold = (front ? -80f : 80f) * _restBlend;
                _rig.legs[i].localRotation = _legRest[i] * Quaternion.Euler(angle + fold, 0f, 0f);
            }

            // Body ----------------------------------------------------------
            float bob, pitch = 0f;
            if (s.gait == GaitStyle.Hop)
            {
                float air = Mathf.Max(0f, Mathf.Sin(phase));
                bob = air * legLen * 0.9f * _moveBlend;
                pitch = -Mathf.Cos(phase) * 12f * _moveBlend;
            }
            else
            {
                bob = Mathf.Abs(Mathf.Cos(phase)) * legLen * 0.04f * (1f + run * 2f) * _moveBlend;
                pitch = Mathf.Sin(phase) * 4f * run;
            }
            float lower = _restBlend * legLen * 0.55f;
            _rig.body.localPosition = _bodyRestPos + Vector3.up * (bob - lower);
            _rig.body.localRotation = _bodyRestRot * Quaternion.Euler(pitch, 0f, 0f);

            float breathRate = Mathf.Lerp(7f, 2f, Mathf.Clamp01(legLen / 0.9f)) * (run > 0.3f ? 1.8f : 1f);
            _rig.body.localScale = _bodyRestScale * (1f + Mathf.Sin(t * breathRate) * 0.012f);

            // Neck & head ---------------------------------------------------
            float reach = legLen > 0.5f ? 100f : 40f; // long-necked browsers bend further to reach the ground
            Vector3 neckT = Vector3.zero, headT = Vector3.zero;
            switch (a)
            {
                case AnimalActivity.Grazing:
                    neckT.x = reach + Mathf.Sin(t * 5f) * 3f;
                    headT.x = 10f + Mathf.Sin(t * 7f) * 4f;
                    break;
                case AnimalActivity.Foraging:
                    neckT.x = reach * 0.8f + Perlin(1.5f, 1f) * 8f;
                    headT.y = Mathf.Sin(t * 2.3f) * 20f;
                    break;
                case AnimalActivity.Sniffing:
                    neckT.x = reach * 0.55f + Mathf.Sin(t * 11f) * 4f;
                    headT.y = Perlin(0.8f, 2f) * 30f;
                    break;
                case AnimalActivity.LookingAround:
                    neckT.x = -10f;
                    headT.y = Perlin(0.35f, 3f) * 70f;
                    headT.x = Perlin(0.4f, 4f) * 10f;
                    break;
                case AnimalActivity.Alert:
                    neckT.x = -18f;
                    LookAt(ref neckT, ref headT);
                    break;
                case AnimalActivity.Grooming:
                    if (Perlin(0.2f, 9f) > 0.7f) _groomSide = -_groomSide;
                    neckT.x = reach * 0.45f;
                    neckT.y = 70f * _groomSide;
                    headT.x = 20f + Mathf.Sin(t * 6f) * 8f;
                    break;
                case AnimalActivity.Resting:
                    neckT.x = -5f;
                    headT.y = Perlin(0.15f, 5f) * 35f;
                    break;
                case AnimalActivity.Fleeing:
                    neckT.x = -8f;
                    break;
                case AnimalActivity.Clinging:
                    headT.x = -25f;
                    headT.y = Perlin(0.4f, 6f) * 50f;
                    break;
                default: // Wandering / Climbing
                    if (speed < 0.05f && a == AnimalActivity.Wandering)
                    {
                        // Paused mid-dash: head up, glancing about.
                        neckT.x = -12f;
                        headT.y = Perlin(0.6f, 7f) * 40f;
                    }
                    else
                    {
                        neckT.x = 8f + Mathf.Sin(phase * 2f) * 3f * _moveBlend;
                        headT.y = Perlin(0.2f, 8f) * 15f;
                    }
                    break;
            }

            _neckOffset = Vector3.Lerp(_neckOffset, neckT, Blend(dt, a == AnimalActivity.Alert ? 7f : 3.5f));
            _headOffset = Vector3.Lerp(_headOffset, headT, Blend(dt, 4f));
            ApplyNeckHead();

            // Ears ----------------------------------------------------------
            AnimateEars(dt, a == AnimalActivity.Alert);

            // Tail ----------------------------------------------------------
            Vector3 tailT = Vector3.zero;
            switch (s.placeholderBody)
            {
                case PlaceholderBody.Deer:
                    // White-tail "flag" when alarmed, otherwise the occasional flick.
                    if (a == AnimalActivity.Fleeing || a == AnimalActivity.Alert) tailT.x = 75f;
                    else if (Perlin(0.5f, 10f) > 0.55f) tailT.y = Mathf.Sin(t * 20f) * 20f;
                    break;
                case PlaceholderBody.Fox:
                    tailT.y = Mathf.Sin(t * 1.3f) * 12f * (1f - run);
                    tailT.x = -10f * run;
                    break;
                case PlaceholderBody.Squirrel:
                    tailT.x = Mathf.Sin(t * 2f) * 8f;
                    if (a == AnimalActivity.Alert || a == AnimalActivity.LookingAround) tailT.x += Mathf.Sin(t * 25f) * 18f * Mathf.Max(0f, Perlin(0.7f, 11f));
                    break;
                case PlaceholderBody.Rabbit:
                    tailT.x = Mathf.Sin(t * 3f) * 5f;
                    break;
            }
            _tailOffset = Vector3.Lerp(_tailOffset, tailT, Blend(dt, 10f));
            if (_rig.tail) _rig.tail.localRotation = _tailRest * Quaternion.Euler(0f, _tailOffset.y, 0f) * Quaternion.Euler(_tailOffset.x, 0f, 0f);
        }

        private void ApplyNeckHead()
        {
            Quaternion neckRot = Quaternion.Euler(0f, _neckOffset.y, 0f) * Quaternion.Euler(_neckOffset.x, 0f, 0f);
            Quaternion headRot = Quaternion.Euler(0f, _headOffset.y, _headOffset.z) * Quaternion.Euler(_headOffset.x, 0f, 0f);
            if (_rig.neck && _rig.head)
            {
                _rig.neck.localRotation = _neckRest * neckRot;
                _rig.head.localRotation = _headRest * headRot;
            }
            else if (_rig.head)
            {
                _rig.head.localRotation = _headRest * neckRot * headRot;
            }
            else if (_rig.neck)
            {
                _rig.neck.localRotation = _neckRest * neckRot * headRot;
            }
        }

        /// <summary>Splits a gaze toward the agent's attention point across neck (60%) and head (40%).</summary>
        private void LookAt(ref Vector3 neckT, ref Vector3 headT)
        {
            if (!_agent.AttentionPoint.HasValue) return;
            Transform head = _rig.head ? _rig.head : _rig.neck;
            if (head == null) return;
            Vector3 dir = transform.InverseTransformDirection(_agent.AttentionPoint.Value - head.position);
            float yaw = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -85f, 85f);
            float flat = new Vector2(dir.x, dir.z).magnitude;
            float pitchAngle = Mathf.Clamp(-Mathf.Atan2(dir.y, flat) * Mathf.Rad2Deg, -30f, 30f);
            if (_rig.neck && _rig.head)
            {
                neckT.y = yaw * 0.6f;
                headT.y = yaw * 0.4f;
                headT.x = pitchAngle;
            }
            else
            {
                neckT.y = yaw;
                neckT.x += pitchAngle;
            }
        }

        private void AnimateEars(float dt, bool alert)
        {
            if (_rig.ears.Length == 0) return;
            _earTimer -= dt;
            if (_earTimer <= 0f)
            {
                _earTimer = Random.Range(1.5f, 5f);
                _earIndex = Random.Range(0, _rig.ears.Length);
                _earAmount = Random.Range(18f, 30f);
            }
            _earAmount = Mathf.MoveTowards(_earAmount, 0f, dt * 90f);
            for (int i = 0; i < _rig.ears.Length; i++)
            {
                if (_rig.ears[i] == null) continue;
                float x = alert ? -12f : (i == _earIndex ? _earAmount : 0f);
                _rig.ears[i].localRotation = Quaternion.Slerp(_rig.ears[i].localRotation, _earRest[i] * Quaternion.Euler(x, 0f, 0f), Blend(dt, 18f));
            }
        }

        // ------------------------------------------------------------------ birds

        private void AnimateBird(float dt)
        {
            float t = Time.time + _seed;
            _flapBlend = Mathf.Lerp(_flapBlend, _flyer.IsFlapping ? 1f : 0f, Blend(dt, 6f));
            _foldBlend = Mathf.Lerp(_foldBlend, _flyer.IsPerched ? 1f : 0f, Blend(dt, 5f));

            float flap = Mathf.Sin(_agent.GaitPhase) * 45f * _flapBlend;
            float glide = (6f + Perlin(0.5f, 1f) * 4f) * (1f - _flapBlend);
            float raise = glide + flap;

            for (int i = 0; i < _rig.wings.Length; i++)
            {
                var w = _rig.wings[i];
                if (w == null) continue;
                float side = i == 0 ? -1f : 1f;
                Quaternion spread = Quaternion.Euler(0f, 0f, raise * side);
                Quaternion folded = Quaternion.Euler(8f, 85f * side, -10f * side);
                w.localRotation = _wingRest[i] * Quaternion.Slerp(spread, folded, _foldBlend);
                w.localScale = Vector3.Scale(_wingRestScale[i], new Vector3(Mathf.Lerp(1f, 0.5f, _foldBlend), 1f, 1f));
            }

            // Legs tucked in flight, down when perched.
            for (int i = 0; i < _rig.legs.Length; i++)
                if (_rig.legs[i]) _rig.legs[i].localRotation = _legRest[i] * Quaternion.Euler(70f * (1f - _foldBlend), 0f, 0f);

            // Head stays level while the body banks; scans when perched.
            Vector3 headT = new Vector3(0f, 0f, -_flyer.Bank * 0.8f * (1f - _foldBlend));
            Vector3 neckT = Vector3.zero;
            if (_flyer.IsPerched)
            {
                if (_agent.AttentionPoint.HasValue) LookAt(ref neckT, ref headT);
                else headT.y = Perlin(0.3f, 2f) * 80f;
            }
            else headT.x = 10f; // looking down at the ground
            _neckOffset = Vector3.Lerp(_neckOffset, neckT, Blend(dt, 4f));
            _headOffset = Vector3.Lerp(_headOffset, headT, Blend(dt, 5f));
            ApplyNeckHead();

            // Tail fans and twists into turns.
            Vector3 tailT = new Vector3(Mathf.Sin(_agent.GaitPhase) * 6f * _flapBlend, -_flyer.Bank * 0.3f, 0f);
            _tailOffset = Vector3.Lerp(_tailOffset, tailT, Blend(dt, 6f));
            if (_rig.tail) _rig.tail.localRotation = _tailRest * Quaternion.Euler(_tailOffset.x, _tailOffset.y, 0f);

            _rig.body.localScale = _bodyRestScale * (1f + Mathf.Sin(t * 4f) * 0.01f * _foldBlend);
        }
    }
}
