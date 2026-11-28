using UnityEngine;
using WildLens.Data;

namespace WildLens.Photography
{
    /// <summary>
    /// Small, cheap, purely cosmetic movement for the macro subjects that come from the user's packs (1.7):
    /// butterflies and bees sit on their flower and now and then flutter a few tens of centimetres; a dragonfly keeps a perch
    /// and darts out on a short loop; a frog sits until the camera comes within 0.7 m and then makes one small hop away;
    /// a firefly drifts and flashes. Nothing here chases the player or leaves its patch, and nothing allocates: a subject
    /// farther than 25 m from the camera does nothing but a once-a-second distance check. The subject's bounds and Focus
    /// child follow the transform, so <see cref="MacroAnalyzer"/> needs no changes.
    /// </summary>
    [RequireComponent(typeof(MacroSubject))]
    public class MacroMotion : MonoBehaviour
    {
        public SubjectBehaviour behaviour = SubjectBehaviour.Flits;

        private const float ActiveRange = 25f;
        private const float HopTrigger = 0.7f, HopLength = 0.4f, HopHeight = 0.1f, HopTime = 0.35f;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color Glow = new Color(0.72f, 1f, 0.25f);

        private MacroSubject _macro;
        private Vector3 _home, _from, _to;
        private Quaternion _homeRot;
        private float _nextPoll, _phase, _t, _dur, _arc, _nextEvent, _flashEnd, _nextFlash, _hopReady;
        private bool _near, _moving;
        private Renderer[] _glowRenderers;
        private Color[] _baseColors;
        private int[] _colorIds;
        private MaterialPropertyBlock _block;
        private bool _lit;
        private float _loopRadius, _loopSign;
        private Vector3 _loopDir;
        private System.Random _rng;

        private void Awake()
        {
            _macro = GetComponent<MacroSubject>();
            _home = transform.position;
            _homeRot = transform.rotation;
            _phase = (Mathf.Abs(_home.x * 12.9898f + _home.z * 78.233f) % 6.2831f);
            _rng = new System.Random(unchecked((int)(_home.x * 1000f) * 31 + (int)(_home.z * 1000f)));
            _nextPoll = Random.value; // per-instance phase for the distance check
            _nextEvent = Time.time + Range(2f, 8f); // the first flutter is not at the same moment everywhere
            _nextFlash = Time.time + Range(0.5f, 5f);
            if (behaviour == SubjectBehaviour.Glows)
            {
                _block = new MaterialPropertyBlock();
                _glowRenderers = GetComponentsInChildren<Renderer>();
                // Emission only shows when the material's keyword is on; switch it on once per instance (not per frame).
                _baseColors = new Color[_glowRenderers.Length];
                _colorIds = new int[_glowRenderers.Length];
                for (int i = 0; i < _glowRenderers.Length; i++)
                {
                    var r = _glowRenderers[i];
                    var m = r != null ? r.sharedMaterial : null;
                    _colorIds[i] = -1;
                    if (m == null) continue;
                    if (m.HasProperty(BaseColorId)) { _colorIds[i] = BaseColorId; _baseColors[i] = m.GetColor(BaseColorId); }
                    else if (m.HasProperty(ColorId)) { _colorIds[i] = ColorId; _baseColors[i] = m.GetColor(ColorId); }
                    if (!m.IsKeywordEnabled("_EMISSION")) r.material.EnableKeyword("_EMISSION");
                }
            }
        }

        private float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        private void Update()
        {
            float now = Time.time;
            if (now >= _nextPoll)
            {
                _nextPoll = now + 1f;
                var cam = Camera.main;
                _near = cam != null && (cam.transform.position - _home).sqrMagnitude < ActiveRange * ActiveRange;
            }
            if (!_near || !_macro.Visible) return;
            switch (behaviour)
            {
                case SubjectBehaviour.Flits: UpdateFlits(now); break;
                case SubjectBehaviour.Perches: UpdatePerches(now); break;
                case SubjectBehaviour.Hops: UpdateHops(now); break;
                case SubjectBehaviour.Glows: UpdateGlows(now); break;
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
                transform.position = Vector3.Lerp(_from, _to, s) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * _arc);
                transform.rotation = _homeRot * Quaternion.Euler(Mathf.Sin(k * Mathf.PI * 6f) * 14f, 0f, 0f); // wings beating
                if (k >= 1f) { _moving = false; _nextEvent = now + Range(6f, 14f); }
                return;
            }
            // The wing-settle wobble: a slow, small roll while it sits.
            float roll = Mathf.Sin(now * 1.6f + _phase) * 4f;
            transform.rotation = _homeRot * Quaternion.Euler(0f, 0f, roll);
            if (now < _nextEvent) return;
            _from = transform.position;
            // Half the time it goes back to its flower, otherwise settles somewhere within half a metre of it.
            if (_rng.NextDouble() < 0.5) _to = _home;
            else
            {
                float a = Range(0f, Mathf.PI * 2f), r = Range(0.15f, 0.5f);
                _to = _home + new Vector3(Mathf.Cos(a) * r, Range(-0.03f, 0.05f), Mathf.Sin(a) * r); // same height band
            }
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
                transform.position = pos;
                if (tangent.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(tangent);
                if (k >= 1f)
                {
                    _moving = false;
                    transform.position = _home;
                    transform.rotation = _homeRot;
                    _nextEvent = now + Range(8f, 20f);
                }
                return;
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

        // ------------------------------------------------------------ Hops: still until you come within 0.7 m

        private void UpdateHops(float now)
        {
            if (_moving)
            {
                _t += Time.deltaTime / HopTime;
                float k = Mathf.Clamp01(_t);
                Vector3 pos = Vector3.Lerp(_from, _to, k);
                pos.y += 4f * HopHeight * k * (1f - k); // parabola
                transform.position = pos;
                if (k >= 1f) { _moving = false; _hopReady = now + 6f; _home = transform.position; }
                return;
            }
            if (now < _hopReady) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 away = transform.position - cam.transform.position;
            if (away.sqrMagnitude > HopTrigger * HopTrigger) return;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -cam.transform.forward;
            away.y = 0f;
            away.Normalize();
            _from = transform.position;
            _to = _from + away * HopLength;
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                // Keep the same height above the ground at the landing spot.
                Vector3 tp = terrain.transform.position;
                float offset = _from.y - (terrain.SampleHeight(_from) + tp.y);
                _to.y = terrain.SampleHeight(_to) + tp.y + offset;
            }
            transform.rotation = Quaternion.LookRotation(away);
            _homeRot = transform.rotation;
            _t = 0f;
            _moving = true;
        }

        // ------------------------------------------------------------ Glows: a slow drift and a soft flash

        private void UpdateGlows(float now)
        {
            float t = now + _phase;
            transform.position = _home + new Vector3(Mathf.Sin(t * 0.30f) * 0.25f, Mathf.Sin(t * 0.45f) * 0.08f, Mathf.Cos(t * 0.27f) * 0.25f);
            if (now >= _nextFlash)
            {
                _flashEnd = now + 0.5f;
                _nextFlash = now + 0.5f + Range(2f, 5f);
            }
            bool on = now < _flashEnd;
            if (!on && !_lit) return;
            float k = on ? Mathf.Sin(Mathf.Clamp01(1f - (_flashEnd - now) / 0.5f) * Mathf.PI) : 0f;
            for (int i = 0; i < _glowRenderers.Length; i++)
            {
                var r = _glowRenderers[i];
                if (r == null) continue;
                _block.Clear();
                _block.SetColor(EmissionId, Glow * (k * 3f));
                if (_colorIds[i] >= 0) _block.SetColor(_colorIds[i], Color.Lerp(_baseColors[i], Glow, k * 0.6f)); // the model's own colour, warmed
                r.SetPropertyBlock(_block);
            }
            _lit = on;
        }
    }
}
