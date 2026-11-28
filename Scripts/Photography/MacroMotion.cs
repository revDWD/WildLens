using UnityEngine;
using WildLens.Data;

namespace WildLens.Photography
{
    /// <summary>
    /// Small, cheap, purely cosmetic movement for the macro subjects (1.7, extended in 1.9 for the generated models): butterflies and
    /// bees sit on their flower and now and then flutter a few tens of centimetres; a dragonfly keeps a perch and darts out on a short loop;
    /// a frog sits until the camera comes within 0.9 m (measured along the ground, not from eye height) and then makes one small hop away;
    /// a firefly rises from the grass at dusk, drifts and flashes. Nothing here chases the player or leaves its patch, and nothing allocates:
    /// a subject farther than 25 m from the camera does nothing but a once-a-second distance check, and a hidden one (out of season)
    /// does nothing at all. The subject's bounds and Focus child follow the transform, so <see cref="MacroAnalyzer"/> needs no changes.
    ///
    /// Wings (1.9): children named "Wing L", "Wing R" (and "Wing FL", "Wing FR", "Wing HL", "Wing HR") are hinged at the body and rolled
    /// about their own Z axis (positive lifts a right-hand wing; the left one gets the opposite sign), on top of the rotation they were
    /// saved with. A butterfly at rest sits with its wings closed up over its back and, now and then, slowly opens them flat and closes
    /// them again (the photograph worth waiting for); in flight they beat. A bee's wings buzz in flight and tremble on the flower, a
    /// dragonfly's shimmer a little, a damselfly (jewelwing) opens and shuts its black wings like a butterfly, a firefly's whirr when it
    /// flies. A model with a child called "Rider" (a dragonfly on its perch) moves only the Rider.
    /// </summary>
    [RequireComponent(typeof(MacroSubject))]
    public class MacroMotion : MonoBehaviour
    {
        public SubjectBehaviour behaviour = SubjectBehaviour.Flits;
        [Tooltip("Set by the builder for the generated models: their children named 'Wing L', 'Wing R' (and FL, FR, HL, HR) are hinged wings to move. " +
                 "Never set for a pack model, whose own animation owns its parts.")]
        public bool hingedWings;

        private const float ActiveRange = 25f;
        private const float HopTrigger = 0.9f, HopLeash = 1.2f, HopLength = 0.4f, HopHeight = 0.1f, HopTime = 0.35f;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TintId = Shader.PropertyToID("_TintColor");
        private static readonly Color Glow = new Color(0.72f, 1f, 0.25f);

        private MacroSubject _macro;
        private Transform _move;                 // what moves: the Rider of a perched dragonfly, else the whole model
        private Vector3 _home, _from, _to, _spawn;
        private Quaternion _homeRot;
        private float _nextPoll, _phase, _t, _dur, _arc, _nextEvent, _flashEnd, _nextFlash, _hopReady;
        private bool _near, _moving, _wasVisible;
        private Renderer[] _glowRenderers;
        private Color[] _baseColors;
        private int[] _colorIds;
        private MaterialPropertyBlock _block;
        private bool _lit;
        private float _loopRadius, _loopSign;
        private Vector3 _loopDir;
        private System.Random _rng;
        private SubjectGroup _group;
        private Transform _camT;

        // Wings
        private Transform[] _wings;
        private float[] _wingSign;
        private Quaternion[] _wingBase;
        private float _wingAngle, _wingFrom, _wingTo, _wingT, _wingDur, _wingNext, _wingClosed = 68f, _wingOpen = 2f;
        private int _wingState; // 0 closed, 1 opening, 2 open, 3 closing

        // Fireflies
        private bool _hasLantern;
        private Transform _halo;
        private Renderer _haloRenderer;
        private float _riseStart = -1e9f, _riseDur = 1f, _riseDrop;

        private void Awake()
        {
            _macro = GetComponent<MacroSubject>();
            _group = _macro.subject != null ? _macro.subject.group : SubjectGroup.Butterfly;
            _move = FindByName("Rider");
            if (_move == null) _move = transform;
            _home = _move.position;
            _spawn = _home;
            _homeRot = _move.rotation;
            _phase = (Mathf.Abs(_home.x * 12.9898f + _home.z * 78.233f) % 6.2831f);
            _rng = new System.Random(unchecked((int)(_home.x * 1000f) * 31 + (int)(_home.z * 1000f)));
            _nextPoll = Random.value; // per-instance phase for the distance check
            _nextEvent = Time.time + Range(2f, 8f); // the first flutter is not at the same moment everywhere
            _nextFlash = Time.time + Range(0.5f, 5f);
            _wasVisible = _macro.Visible;
            if (hingedWings) CollectWings();
            if (_wings != null)
            {
                if (_group == SubjectGroup.Damselfly) { _wingClosed = 0f; _wingOpen = -44f; } // saved with its wings up: the angle is added to that
                else { _wingClosed = 68f; _wingOpen = 2f; }
                _wingAngle = HasRestPose ? 0f : _wingClosed;
                _wingNext = Time.time + Range(1f, 6f);
                ApplyWings(_wingAngle);
            }
            if (behaviour == SubjectBehaviour.Glows)
            {
                _block = new MaterialPropertyBlock();
                var all = GetComponentsInChildren<Renderer>();
                // A generated firefly has a "Lantern" and a "Halo"; only those glow. A pack model glows all over, as before.
                int lanterns = 0;
                foreach (var r in all) if (r != null && r.name.Contains("Lantern")) lanterns++;
                _hasLantern = lanterns > 0;
                var list = new System.Collections.Generic.List<Renderer>();
                foreach (var r in all)
                {
                    if (r == null) continue;
                    if (r.name == "Halo") { _halo = r.transform; _haloRenderer = r; continue; }
                    if (_hasLantern && !r.name.Contains("Lantern")) continue;
                    list.Add(r);
                }
                _glowRenderers = list.ToArray();
                // Emission only shows when the material's keyword is on; switch it on once per instance (not per frame).
                _baseColors = new Color[_glowRenderers.Length];
                _colorIds = new int[_glowRenderers.Length];
                for (int i = 0; i < _glowRenderers.Length; i++)
                {
                    var r = _glowRenderers[i];
                    var m = r != null ? r.sharedMaterial : null;
                    _colorIds[i] = -1;
                    if (m == null) continue;
                    if (!_hasLantern)
                    {
                        if (m.HasProperty(BaseColorId)) { _colorIds[i] = BaseColorId; _baseColors[i] = m.GetColor(BaseColorId); }
                        else if (m.HasProperty(ColorId)) { _colorIds[i] = ColorId; _baseColors[i] = m.GetColor(ColorId); }
                    }
                    if (!m.IsKeywordEnabled("_EMISSION")) r.material.EnableKeyword("_EMISSION");
                }
                // How far below its hover point the ground is: it lifts off the grass from there.
                var terrain = Terrain.activeTerrain;
                float ground = terrain != null ? terrain.SampleHeight(_home) + terrain.transform.position.y : _home.y - 0.6f;
                _riseDrop = Mathf.Clamp(_home.y - ground - 0.02f, 0.1f, 2f);
                _riseStart = -1e9f; // already risen unless it appears (OnAppear) while we are watching
                if (_haloRenderer != null) _haloRenderer.enabled = true;
                ApplyGlow(0f);
            }
        }

        private bool HasRestPose
        {
            get
            {
                // A wing saved with a rest roll (a bee's, a jewelwing's) already sits at its closed pose: the angle added on top starts at 0.
                if (_wings == null) return false;
                return _group == SubjectGroup.Bee || _group == SubjectGroup.Damselfly || _group == SubjectGroup.Dragonfly || _group == SubjectGroup.Firefly;
            }
        }

        private Transform FindByName(string wanted)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != null && t != transform && t.name == wanted) return t;
            return null;
        }

        private void CollectWings()
        {
            int count = 0;
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all) if (t != null && t != transform && t.name.StartsWith("Wing ")) count++;
            if (count == 0) return;
            _wings = new Transform[count];
            _wingSign = new float[count];
            _wingBase = new Quaternion[count];
            int i = 0;
            foreach (var t in all)
            {
                if (t == null || t == transform || !t.name.StartsWith("Wing ")) continue;
                _wings[i] = t;
                _wingSign[i] = t.name.EndsWith("L") ? -1f : 1f; // L at -X, R at +X; positive roll lifts a right-hand wing
                _wingBase[i] = t.localRotation;
                i++;
            }
        }

        private void ApplyWings(float angle)
        {
            if (_wings == null) return;
            _wingAngle = angle;
            for (int i = 0; i < _wings.Length; i++)
                if (_wings[i] != null) _wings[i].localRotation = _wingBase[i] * Quaternion.Euler(0f, 0f, _wingSign[i] * angle);
        }

        private float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        private void Update()
        {
            float now = Time.time;
            if (now >= _nextPoll)
            {
                _nextPoll = now + 1f;
                var cam = Camera.main;
                _camT = cam != null ? cam.transform : null;
                _near = _camT != null && (_camT.position - _home).sqrMagnitude < ActiveRange * ActiveRange;
            }
            bool visible = _macro.Visible;
            if (visible != _wasVisible)
            {
                _wasVisible = visible;
                if (visible) OnAppear(now);
            }
            if (!_near || !visible) return;
            switch (behaviour)
            {
                case SubjectBehaviour.Flits: UpdateFlits(now); break;
                case SubjectBehaviour.Perches: UpdatePerches(now); break;
                case SubjectBehaviour.Hops: UpdateHops(now); break;
                case SubjectBehaviour.Glows: UpdateGlows(now); break;
            }
        }

        /// <summary>The subject has just come out (the right hour, the right weather): fireflies lift off the grass.</summary>
        private void OnAppear(float now)
        {
            if (behaviour != SubjectBehaviour.Glows) return;
            _riseStart = now;
            _riseDur = Range(8f, 20f);
            _nextFlash = now + Range(1f, 6f);
            _flashEnd = 0f;
            _lit = false;
        }

        // ------------------------------------------------------------ wing behaviours

        /// <summary>The slow open-and-close of a butterfly (and a jewelwing) at rest: mostly closed, now and then opened flat for a few seconds.</summary>
        private void UpdateRestingWings(float now, float dt)
        {
            if (_wings == null) return;
            switch (_wingState)
            {
                case 0: // closed, waiting
                    if (now >= _wingNext)
                    {
                        _wingState = 1; _wingT = 0f; _wingDur = Range(1.1f, 1.8f); _wingFrom = _wingAngle; _wingTo = _wingOpen + Range(0f, 8f);
                    }
                    break;
                case 1: // opening
                case 3: // closing
                {
                    _wingT += dt / _wingDur;
                    float k = Mathf.Clamp01(_wingT);
                    ApplyWings(Mathf.Lerp(_wingFrom, _wingTo, k * k * (3f - 2f * k)));
                    if (k >= 1f)
                    {
                        if (_wingState == 1) { _wingState = 2; _wingNext = now + Range(1.6f, 4.2f); }
                        else { _wingState = 0; _wingNext = now + Range(3f, 8f); }
                    }
                    break;
                }
                default: // open: a faint pulse, then close
                    ApplyWings(_wingTo + Mathf.Sin(now * 2.2f + _phase) * 2.5f);
                    if (now >= _wingNext)
                    {
                        _wingState = 3; _wingT = 0f; _wingDur = Range(1.4f, 2.2f); _wingFrom = _wingAngle; _wingTo = _wingClosed - Range(0f, 8f);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------ Flits: sits, settles its wings, flutters now and then

        private void UpdateFlits(float now)
        {
            if (_moving)
            {
                _t += Time.deltaTime / _dur;
                float k = Mathf.Clamp01(_t);
                float s = k * k * (3f - 2f * k);
                _move.position = Vector3.Lerp(_from, _to, s) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * _arc);
                _move.rotation = _homeRot * Quaternion.Euler(Mathf.Sin(k * Mathf.PI * 6f) * 14f, 0f, 0f); // bobbing as the wings beat
                if (_group == SubjectGroup.Bee) ApplyWings(Mathf.Sin(now * 164f) * 36f);                       // a buzz
                else if (_group == SubjectGroup.Butterfly || _group == SubjectGroup.Moth) ApplyWings(25f + Mathf.Sin(now * 56f + _phase) * 40f);
                if (k >= 1f)
                {
                    _moving = false;
                    _nextEvent = now + Range(6f, 14f);
                    if (_group == SubjectGroup.Butterfly || _group == SubjectGroup.Moth) { _wingState = 3; _wingT = 0f; _wingDur = Range(0.8f, 1.4f); _wingFrom = _wingAngle; _wingTo = _wingClosed; }
                    else ApplyWings(0f);
                }
                return;
            }
            // At rest: a slow, small roll, and the wings doing what that animal's wings do.
            float roll = Mathf.Sin(now * 1.6f + _phase) * (_group == SubjectGroup.Butterfly ? 2f : 4f);
            _move.rotation = _homeRot * Quaternion.Euler(0f, 0f, roll);
            if (_group == SubjectGroup.Butterfly || _group == SubjectGroup.Moth) UpdateRestingWings(now, Time.deltaTime);
            else if (_group == SubjectGroup.Bee) ApplyWings(Mathf.Sin(now * 19f + _phase) * 3f); // a worker on the flower: wings tremble
            if (now < _nextEvent) return;
            _from = _move.position;
            // Half the time it goes back to its flower, otherwise settles somewhere within half a metre of it.
            if (_rng.NextDouble() < 0.5) _to = _home;
            else
            {
                float a = Range(0f, Mathf.PI * 2f), r = Range(0.15f, 0.5f);
                _to = _home + new Vector3(Mathf.Cos(a) * r, Range(-0.03f, 0.05f), Mathf.Sin(a) * r); // same height band
            }
            Vector3 heading = _to - _from;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.0025f) _homeRot = Quaternion.LookRotation(heading); // it faces where it is going
            _dur = Range(1.2f, 2f);
            _arc = Range(0.08f, 0.16f);
            _t = 0f;
            _moving = true;
        }

        // ------------------------------------------------------------ Perches: still, then a short dart and back

        private void UpdatePerches(float now)
        {
            if (_moving)
            {
                _t += Time.deltaTime / _dur;
                float k = Mathf.Clamp01(_t);
                float th = k * Mathf.PI * 2f;
                // A circle that starts and ends at the perch: out along _loopDir, round, and back.
                Vector3 side = new Vector3(-_loopDir.z, 0f, _loopDir.x) * _loopSign;
                Vector3 pos = _home + _loopDir * (_loopRadius * (1f - Mathf.Cos(th))) + side * (_loopRadius * Mathf.Sin(th));
                pos.y += Mathf.Sin(k * Mathf.PI) * 0.15f;
                Vector3 tangent = _loopDir * Mathf.Sin(th) + side * Mathf.Cos(th);
                _move.position = pos;
                if (tangent.sqrMagnitude > 0.0001f) _move.rotation = Quaternion.LookRotation(tangent);
                if (_wings != null) ApplyWings(Mathf.Sin(now * 110f) * (_group == SubjectGroup.Damselfly ? 22f : 16f)); // a blur of wings in flight
                if (k >= 1f)
                {
                    _moving = false;
                    _move.position = _home;
                    _move.rotation = _homeRot;
                    ApplyWings(0f);
                    if (_group == SubjectGroup.Damselfly) { _wingState = 3; _wingT = 0f; _wingDur = 0.6f; _wingFrom = _wingAngle; _wingTo = _wingClosed; }
                    _nextEvent = now + Range(8f, 20f);
                }
                return;
            }
            if (_wings != null)
            {
                if (_group == SubjectGroup.Damselfly) UpdateRestingWings(now, Time.deltaTime);
                else ApplyWings(Mathf.Sin(now * 7f + _phase) * 2.5f); // the dragonfly's wings shimmer a little as it perches
            }
            if (now < _nextEvent) return;
            float a = Range(0f, Mathf.PI * 2f);
            _loopDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            _loopRadius = Range(0.16f, 0.32f); // a loop 1 to 2 m long
            _loopSign = _rng.NextDouble() < 0.5 ? -1f : 1f;
            _dur = Range(1.2f, 2f);
            _t = 0f;
            _moving = true;
        }

        // ------------------------------------------------------------ Hops: still until you come within 0.9 m (horizontal)

        private void UpdateHops(float now)
        {
            if (_moving)
            {
                _t += Time.deltaTime / HopTime;
                float k = Mathf.Clamp01(_t);
                Vector3 pos = Vector3.Lerp(_from, _to, k);
                pos.y += 4f * HopHeight * k * (1f - k); // parabola
                _move.position = pos;
                if (k >= 1f) { _moving = false; _hopReady = now + 6f; _home = _move.position; }
                return;
            }
            if (now < _hopReady) return;
            var cam = _camT;
            if (cam == null) return;
            Vector3 away = _move.position - cam.position;
            away.y = 0f; // horizontal distance: the camera is at eye height, the frog is on the ground
            if (away.sqrMagnitude > HopTrigger * HopTrigger) return;
            if (away.sqrMagnitude < 0.0001f) away = -Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (away.sqrMagnitude < 0.0001f) { away = _homeRot * Vector3.forward; away.y = 0f; }
            if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
            away.Normalize();
            // The leash: never land farther than HopLeash from the spot it started on; hop back toward it instead.
            Vector3 land = _move.position + away * HopLength;
            Vector3 fromSpawn = land - _spawn; fromSpawn.y = 0f;
            if (fromSpawn.sqrMagnitude > HopLeash * HopLeash)
            {
                Vector3 back = _spawn - _move.position; back.y = 0f;
                if (back.sqrMagnitude > 0.0001f) away = back.normalized;
            }
            _from = _move.position;
            _to = _from + away * HopLength;
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                // Keep the same height above the ground at the landing spot.
                Vector3 tp = terrain.transform.position;
                float offset = _from.y - (terrain.SampleHeight(_from) + tp.y);
                _to.y = terrain.SampleHeight(_to) + tp.y + offset;
            }
            _move.rotation = Quaternion.LookRotation(away);
            _homeRot = _move.rotation;
            _t = 0f;
            _moving = true;
        }

        // ------------------------------------------------------------ Glows: rise from the grass, drift, flash

        private void UpdateGlows(float now)
        {
            float t = now + _phase;
            float rise = _riseDur > 0f ? Mathf.Clamp01((now - _riseStart) / _riseDur) : 1f;
            rise = rise * rise * (3f - 2f * rise);
            Vector3 pos = _home + new Vector3(Mathf.Sin(t * 0.30f) * 0.25f, Mathf.Sin(t * 0.45f) * 0.08f, Mathf.Cos(t * 0.27f) * 0.25f);
            pos.y -= (1f - rise) * _riseDrop;          // lifting off the grass over eight to twenty seconds
            if (now >= _nextFlash)
            {
                _flashEnd = now + 0.5f;
                _nextFlash = now + 0.5f + Range(2f, 5f);
            }
            bool on = now < _flashEnd;
            float prog = on ? Mathf.Clamp01(1f - (_flashEnd - now) / 0.5f) : 0f;
            float k = on ? Mathf.Sin(prog * Mathf.PI) : 0f;
            pos.y += k * 0.12f;                        // the J-shaped swoop: it rises as it flashes
            _move.position = pos;
            if (_wings != null) ApplyWings(Mathf.Sin(now * 190f) * 32f); // whirring: only while it is out and near
            if (_halo != null)
            {
                if (_camT != null) _halo.rotation = _camT.rotation;
                _halo.localScale = Vector3.one * (0.55f + 0.9f * k);
            }
            if (!on && !_lit) return;
            ApplyGlow(k);
            _lit = on;
        }

        private void ApplyGlow(float k)
        {
            if (_glowRenderers == null) return;
            for (int i = 0; i < _glowRenderers.Length; i++)
            {
                var r = _glowRenderers[i];
                if (r == null) continue;
                _block.Clear();
                _block.SetColor(EmissionId, Glow * (_hasLantern ? 0.25f + k * 4f : k * 3f));
                if (_colorIds[i] >= 0) _block.SetColor(_colorIds[i], Color.Lerp(_baseColors[i], Glow, k * 0.6f)); // the model's own colour, warmed
                r.SetPropertyBlock(_block);
            }
            if (_haloRenderer != null)
            {
                float a = 0.04f + k * 0.96f;
                _block.Clear();
                _block.SetColor(TintId, new Color(Glow.r * 0.5f, Glow.g * 0.5f, Glow.b * 0.5f, 0.5f * a));
                _block.SetColor(ColorId, new Color(Glow.r, Glow.g, Glow.b, a));
                _block.SetColor(BaseColorId, new Color(Glow.r, Glow.g, Glow.b, a));
                _haloRenderer.SetPropertyBlock(_block);
            }
        }
    }
}
