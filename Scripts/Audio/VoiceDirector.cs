using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildLens.Core;
using WildLens.Data;
using WildLens.Environment;
using WildLens.Photography;
using WildLens.Wildlife;

namespace WildLens.Sound
{
    /// <summary>
    /// The voices of the reserve (2.0 "Sound"). ONE manager, polling four times a second over the spawner's animals and the frogs, that
    /// lets the same conditions that move the animals decide when they are heard: a dawn chorus, songbirds bursting out in the first sun after rain,
    /// frogs on wet evenings, the owl on dry dusks, crows at the roost, crickets on warm dry nights, near-silence in rain. A call comes from the
    /// animal that made it (3D), so hearing a cardinal means a cardinal is there.
    ///
    /// Rules (WildLens "events are conditions, never scripts"): an animal calls because of the hour, the weather, its own state and its own
    /// timer, never because of where the player is. The only exception is natural animal behaviour: a short ALARM (a deer's snort, a squirrel's
    /// scolding) the first time it becomes Alert, rare, with a cooldown. An Alert or Fleeing animal is otherwise silent. Nothing is shown on screen.
    ///
    /// Calm by design: calls are a Poisson process per individual (VoiceRules.Rate, wide jitter), same-species neighbours inhibit each other for a few
    /// seconds and sometimes answer, at most six animal voices and four frog voices sound at once, nothing is heard beyond its range, and the clock
    /// running fast (the night, T held) thins the animals to a fifth and leaves the beds (crickets, frogs).
    ///
    /// Sound is generated in code once per call (VoiceSynth, cached here, about 4 MB in all) and played from a small pool of AudioSources, never one per animal.
    /// Created at runtime (so it also works in scenes built by older versions); a VoiceDirector placed in a scene by hand wins and keeps its settings.
    /// </summary>
    public class VoiceDirector : MonoBehaviour
    {
        [Tooltip("Wildlife voices on or off.")]
        public bool wildlifeVoices = true;
        [Range(0f, 1f)] [Tooltip("One master volume for every voice and bed.")]
        public float masterVolume = 0.8f;
        [Range(0f, 1f)] [Tooltip("Level of the cricket bed at its fullest.")]
        public float cricketLevel = 0.16f;

        public static VoiceDirector Instance { get; private set; }

        private const float PollInterval = 0.25f;
        private const float AlarmBoost = 1.15f;
        private const int PoolSize = VoiceRules.MaxAnimalVoices + VoiceRules.MaxFrogVoices;

        private sealed class Singer
        {
            public float nextAllowed, inhibitUntil, answerAt = -1f, alarmReady;
            public AwarenessState lastAware = AwarenessState.Calm;
            public bool answerIsReply;
        }

        private sealed class Slot
        {
            public AudioSource source;
            public Transform follow;
            public Vector3 pos;
            public float volume, range;
            public bool frog, busy;
        }

        private static readonly Dictionary<VoiceCall, AudioClip> Clips = new Dictionary<VoiceCall, AudioClip>();
        private static AudioClip _crickets, _buzz;
        private static long _clipBytes;
        /// <summary>Memory held by the generated clips (bytes), for diagnostics.</summary>
        public static long ClipBytes => _clipBytes;

        private readonly Slot[] _slots = new Slot[PoolSize];
        private readonly Dictionary<Component, Singer> _singers = new Dictionary<Component, Singer>();
        private readonly Dictionary<SpeciesDefinition, VoiceRecipe> _resolved = new Dictionary<SpeciesDefinition, VoiceRecipe>();
        private readonly System.Random _rng = new System.Random(System.Environment.TickCount); // private: nothing deterministic depends on it
        private AudioSource _cricketSource, _beeSource;
        private Transform _listener;
        private WildlifeSpawner _spawner;
        private float _nextPoll, _nextClean, _nextListenerSearch, _lastPoll = -1f, _alarmGlobalReady;
        private float _cricketTarget, _cricketNow, _beeTarget, _beeNow;
        private Transform _beeFollow;
        private VoiceConditions _cond;
        private int _animalVoices, _frogVoices;

        // ------------------------------------------------------------------ creation

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Clips.Clear();
            _crickets = null; _buzz = null; _clipBytes = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        /// <summary>Creates the director when a reserve scene (one with a DayCycle) is loaded and none exists yet.</summary>
        private static void Ensure()
        {
            if (Instance != null || DayCycle.Instance == null) return;
            var go = new GameObject("WildLens Voices");
            go.AddComponent<VoiceDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            try
            {
                if (PlayerPrefs.HasKey("WildLens.Voices")) wildlifeVoices = PlayerPrefs.GetInt("WildLens.Voices", 1) != 0;
                if (PlayerPrefs.HasKey("WildLens.VoiceVolume")) masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("WildLens.VoiceVolume", masterVolume));
            }
            catch { /* settings are a convenience */ }
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Voice " + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                Configure3D(src);
                _slots[i] = new Slot { source = src, frog = i >= VoiceRules.MaxAnimalVoices };
            }
            var cgo = new GameObject("Crickets");
            cgo.transform.SetParent(transform, false);
            _cricketSource = cgo.AddComponent<AudioSource>();
            _cricketSource.playOnAwake = false; _cricketSource.loop = true; _cricketSource.spatialBlend = 0f; _cricketSource.volume = 0f;
            var bgo = new GameObject("Bee");
            bgo.transform.SetParent(transform, false);
            _beeSource = bgo.AddComponent<AudioSource>();
            Configure3D(_beeSource);
            _beeSource.loop = true; _beeSource.minDistance = 0.3f; _beeSource.maxDistance = 3f; _beeSource.volume = 0f;
        }

        private static void Configure3D(AudioSource src)
        {
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.dopplerLevel = 0f;
            src.spread = 30f;
            src.volume = 0f;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Turns the wildlife voices on or off (kept between sessions).</summary>
        public void SetVoices(bool on)
        {
            wildlifeVoices = on;
            try { PlayerPrefs.SetInt("WildLens.Voices", on ? 1 : 0); } catch { }
        }

        // ------------------------------------------------------------------ the loop

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!wildlifeVoices)
            {
                StopAll();
                return;
            }
            Follow(dt);
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + PollInterval;
            Poll();
        }

        private void StopAll()
        {
            for (int i = 0; i < _slots.Length; i++) if (_slots[i].busy) Release(_slots[i]);
            _cricketTarget = 0f; _beeTarget = 0f;
            if (_cricketSource.isPlaying) { _cricketSource.Stop(); _cricketNow = 0f; }
            if (_beeSource.isPlaying) { _beeSource.Stop(); _beeNow = 0f; }
        }

        private bool FindListener()
        {
            if (_listener != null) return true;
            if (Time.unscaledTime < _nextListenerSearch) return false;
            _nextListenerSearch = Time.unscaledTime + 2f;
            var l = SceneQuery.FindOne<AudioListener>();
            if (l != null) _listener = l.transform;
            return _listener != null;
        }

        private Vector3 ListenerPos => _listener != null ? _listener.position : PlayerPresence.Position;

        /// <summary>Every frame, for the few sounding voices: follow the caller, fade toward the limit of the range, free finished sources, ease the beds.</summary>
        private void Follow(float dt)
        {
            Vector3 lp = ListenerPos;
            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (!s.busy) continue;
                if (!s.source.isPlaying) { Release(s); continue; }
                if (s.follow != null) s.pos = s.follow.position + Vector3.up * 0.3f;
                s.source.transform.position = s.pos;
                s.source.volume = s.volume * VoiceRules.Edge(Vector3.Distance(lp, s.pos), s.range);
            }

            _cricketNow = Mathf.MoveTowards(_cricketNow, _cricketTarget, dt * 0.25f);
            if (_cricketNow > 0.003f)
            {
                if (!_cricketSource.isPlaying) { _cricketSource.clip = Crickets(); _cricketSource.time = (float)_rng.NextDouble() * 5f; _cricketSource.Play(); }
                _cricketSource.volume = _cricketNow * cricketLevel * masterVolume * BedCover();
            }
            else if (_cricketSource.isPlaying) _cricketSource.Stop();

            _beeNow = Mathf.MoveTowards(_beeNow, _beeFollow != null ? _beeTarget : 0f, dt * 1.5f);
            if (_beeNow > 0.003f && _beeFollow != null)
            {
                if (!_beeSource.isPlaying) { _beeSource.clip = Buzz(); _beeSource.Play(); }
                _beeSource.transform.position = _beeFollow.position;
                _beeSource.volume = _beeNow * 0.07f * masterVolume;
            }
            else if (_beeSource.isPlaying) _beeSource.Stop();
        }

        /// <summary>
        /// RAIN-SHELTER HOOK: a future cover factor (0 = outdoors, 1 = fully under a roof) can muffle the beds here, e.g.
        /// <c>return 1f - 0.6f * RainShelter.Covered;</c>. Deliberately not referenced yet, so this compiles without it.
        /// </summary>
        private static float BedCover() => 1f;

        private void Release(Slot s)
        {
            if (s.source.isPlaying) s.source.Stop();
            s.source.clip = null;
            s.busy = false; s.follow = null;
            if (s.frog) _frogVoices = Mathf.Max(0, _frogVoices - 1); else _animalVoices = Mathf.Max(0, _animalVoices - 1);
        }

        private void Poll()
        {
            var clock = DayCycle.Instance;
            if (clock == null || !FindListener()) return;
            float dtPoll = _lastPoll < 0f ? PollInterval : Mathf.Min(Time.time - _lastPoll, 1f);
            _lastPoll = Time.time;
            var weather = WeatherCycle.Instance;
            _cond = new VoiceConditions
            {
                period = (int)clock.Period,
                hour = clock.Hour,
                raining = weather != null && weather.IsRaining,
                fair = weather == null || weather.IsFair,
                sunAfterRain = weather != null && weather.SunAfterRain,
                hoursSinceRain = weather != null ? weather.HoursSinceRain : 1e9f,
                wetNight = NaturalConditions.WetNight,
                accelerated = clock.IsWaiting || (clock.Period == DayPeriod.Night && clock.nightSpeed > 1.5f),
            };
            _cricketTarget = VoiceRules.CricketLevel(_cond.hour, _cond.raining, _cond.hoursSinceRain);

            if (Time.time >= _nextClean) { _nextClean = Time.time + 20f; Clean(); }
            var game = WildLensGame.Instance;
            if (game != null && game.IsUiOpen) return; // paused: nothing new starts

            Vector3 lp = ListenerPos;
            PollAnimals(lp, dtPoll);
            PollSmallLife(lp, dtPoll);
        }

        private Singer Get(Component c)
        {
            Singer s;
            if (!_singers.TryGetValue(c, out s)) _singers[c] = s = new Singer();
            return s;
        }

        private void Clean()
        {
            List<Component> dead = null;
            foreach (var kv in _singers) if (kv.Key == null) { if (dead == null) dead = new List<Component>(); dead.Add(kv.Key); }
            if (dead != null) foreach (var d in dead) _singers.Remove(d);
            List<SpeciesDefinition> gone = null;
            foreach (var kv in _resolved) if (kv.Key == null) { if (gone == null) gone = new List<SpeciesDefinition>(); gone.Add(kv.Key); }
            if (gone != null) foreach (var d in gone) _resolved.Remove(d);
        }

        private VoiceRecipe Resolve(SpeciesDefinition sp)
        {
            VoiceRecipe r;
            if (_resolved.TryGetValue(sp, out r)) return r;
            r = sp.voice != null && sp.voice.Has ? sp.voice : VoiceBank.For(sp.speciesId);
            _resolved[sp] = r;
            return r;
        }

        // ------------------------------------------------------------------ animals

        private static bool IsFlying(AnimalActivity a) =>
            a == AnimalActivity.Flying || a == AnimalActivity.Soaring || a == AnimalActivity.Gliding || a == AnimalActivity.TakingOff || a == AnimalActivity.Landing;

        private void PollAnimals(Vector3 lp, float dt)
        {
            if (_spawner == null) _spawner = SceneQuery.FindOne<WildlifeSpawner>();
            if (_spawner == null) return;
            var list = _spawner.Spawned;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null || a.Species == null) continue;
                Vector3 pos = a.transform.position;
                float d2 = (pos - lp).sqrMagnitude;
                if (d2 > 125f * 125f) continue; // no voice reaches farther: skip before anything else
                var r = Resolve(a.Species);
                if (r == null || d2 > r.range * r.range) continue;
                var st = Get(a);
                var aware = a.Awareness != null ? a.Awareness.State : AwarenessState.Calm;
                float now = Time.time;

                // Natural alarm: the first moment it notices something enough to go Alert.
                if (aware != st.lastAware)
                {
                    if (aware == AwarenessState.Alert && st.lastAware != AwarenessState.Fleeing) TryAlarm(a, r, st, pos, now);
                    st.lastAware = aware;
                }
                if (aware == AwarenessState.Alert || aware == AwarenessState.Fleeing) continue;
                if (aware == AwarenessState.Aware && !r.awareOk) continue;
                if (!r.HasCalm) continue;
                bool flying = IsFlying(a.Activity);
                if (flying ? !r.inFlight : !r.atRest) continue;

                bool reply = st.answerAt >= 0f && now >= st.answerAt;
                bool fire = false;
                float rate = VoiceRules.Rate(r, _cond, false);
                if (reply)
                {
                    st.answerAt = -1f;
                    fire = rate > 0f && now >= st.nextAllowed;
                }
                else if (rate > 0f && now >= st.nextAllowed && now >= st.inhibitUntil)
                    fire = _rng.NextDouble() < VoiceRules.Chance(rate, dt);
                if (!fire) continue;
                if (_animalVoices >= VoiceRules.MaxAnimalVoices) continue;
                if (_animalVoices >= VoiceRules.MaxAnimalVoices - 2 && d2 > 0.25f * r.range * r.range) continue; // the last places go to the nearer voices

                bool song = r.song != null && r.song.Has && (r.call == null || !r.call.Has || _rng.NextDouble() < r.songShare);
                var call = song ? r.song : r.call;
                if (call == null || !call.Has) continue;
                float len;
                if (!Play(call, r, a.transform, pos, false, 1f, out len)) continue;
                st.nextAllowed = now + len + r.minGap * (0.7f + (float)_rng.NextDouble() * 0.8f);
                Neighbours(a, r, st, pos, now, len, reply);
            }
        }

        private void TryAlarm(AnimalAgent a, VoiceRecipe r, Singer st, Vector3 pos, float now)
        {
            if (r.alarm == null || !r.alarm.Has || r.alarmChance <= 0f) return;
            if (now < st.alarmReady || now < _alarmGlobalReady) return;
            if (_animalVoices >= VoiceRules.MaxAnimalVoices) return;
            if (_rng.NextDouble() > r.alarmChance) return;
            float len;
            if (!Play(r.alarm, r, a.transform, pos, false, AlarmBoost, out len)) return;
            st.alarmReady = now + r.alarmCooldown * (0.8f + (float)_rng.NextDouble() * 0.5f);
            st.nextAllowed = now + len + 4f;
            _alarmGlobalReady = now + 3f;
        }

        /// <summary>After a call: same-species neighbours stay quiet a few seconds, and one of them may answer a moment after it ends.</summary>
        private void Neighbours(AnimalAgent caller, VoiceRecipe r, Singer cs, Vector3 pos, float now, float len, bool wasReply)
        {
            var list = _spawner.Spawned;
            string id = caller.Species.speciesId;
            AnimalAgent nearest = null;
            float best = VoiceRules.AnswerRadius * VoiceRules.AnswerRadius;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o == null || o == caller || o.Species == null || o.Species.speciesId != id) continue;
                float d2 = (o.transform.position - pos).sqrMagnitude;
                if (d2 > best && d2 > VoiceRules.InhibitRadius * VoiceRules.InhibitRadius) continue;
                var os = Get(o);
                if (d2 <= VoiceRules.InhibitRadius * VoiceRules.InhibitRadius)
                    os.inhibitUntil = Mathf.Max(os.inhibitUntil, now + len + Range(VoiceRules.InhibitMin, VoiceRules.InhibitMax));
                if (d2 < best && os.answerAt < 0f) { best = d2; nearest = o; }
            }
            if (wasReply || nearest == null || r.answer <= 0f || _rng.NextDouble() > r.answer) return;
            var ns = Get(nearest);
            ns.answerAt = now + len + Range(VoiceRules.AnswerDelayMin, VoiceRules.AnswerDelayMax);
            ns.inhibitUntil = 0f; // it is about to answer
        }

        private float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // ------------------------------------------------------------------ small life: frogs and bees

        private void PollSmallLife(Vector3 lp, float dt)
        {
            var active = MacroSubject.Active;
            float now = Time.time;
            Transform bee = null;
            float beeBest = 3f * 3f;
            for (int i = 0; i < active.Count; i++)
            {
                var ms = active[i];
                if (ms == null || ms.subject == null || !ms.Visible) continue;
                var grp = ms.subject.group;
                if (grp == SubjectGroup.Bee)
                {
                    float bd = (ms.transform.position - lp).sqrMagnitude;
                    if (bd < beeBest && !_cond.raining) { beeBest = bd; bee = ms.transform; }
                    continue;
                }
                if (grp != SubjectGroup.Frog) continue;
                var r = VoiceBank.ForSubject(ms.subject.subjectId);
                if (r == null) continue;
                Vector3 pos = ms.transform.position;
                float d2 = (pos - lp).sqrMagnitude;
                if (d2 > r.range * r.range) continue;
                var st = Get(ms);
                bool reply = st.answerAt >= 0f && now >= st.answerAt;
                float rate = VoiceRules.Rate(r, _cond, true);
                if (rate <= 0f) { st.answerAt = -1f; continue; }
                bool fire;
                if (reply) { st.answerAt = -1f; fire = now >= st.nextAllowed; }
                else fire = now >= st.nextAllowed && _rng.NextDouble() < VoiceRules.Chance(rate, dt);
                if (!fire || _frogVoices >= VoiceRules.MaxFrogVoices) continue;
                var call = r.call;
                float len;
                if (call == null || !call.Has || !Play(call, r, ms.transform, pos, true, 1f, out len)) continue;
                st.nextAllowed = now + len + r.minGap * (0.7f + (float)_rng.NextDouble() * 0.8f);
                Chorus(active, ms, r, pos, now, len, reply);
            }
            _beeFollow = bee;
            _beeTarget = bee != null ? Mathf.Clamp01(1f - Mathf.Sqrt(beeBest) / 3f) : 0f;
        }

        /// <summary>A frog's call invites up to two neighbours of its kind to answer (the chorus); an answer does not start another.</summary>
        private void Chorus(IReadOnlyList<MacroSubject> active, MacroSubject caller, VoiceRecipe r, Vector3 pos, float now, float len, bool wasReply)
        {
            if (wasReply || r.answer <= 0f) return;
            int invited = 0;
            for (int i = 0; i < active.Count && invited < 2; i++)
            {
                var o = active[i];
                if (o == null || o == caller || o.subject == null || o.subject.subjectId != caller.subject.subjectId || !o.Visible) continue;
                if ((o.transform.position - pos).sqrMagnitude > 20f * 20f) continue;
                var os = Get(o);
                if (os.answerAt >= 0f || _rng.NextDouble() > r.answer) continue;
                os.answerAt = now + len * 0.4f + Range(0.4f, 1.8f);
                invited++;
            }
        }

        // ------------------------------------------------------------------ playing

        private bool Play(VoiceCall call, VoiceRecipe r, Transform follow, Vector3 pos, bool frog, float boost, out float length)
        {
            length = 0f;
            Slot slot = null;
            for (int i = 0; i < _slots.Length; i++)
                if (!_slots[i].busy && _slots[i].frog == frog) { slot = _slots[i]; break; }
            if (slot == null) return false;
            var clip = ClipFor(call);
            if (clip == null) return false;
            float pitch = 1f + ((float)_rng.NextDouble() * 2f - 1f) * 0.03f;
            var src = slot.source;
            src.clip = clip;
            src.pitch = pitch;
            src.minDistance = Mathf.Max(0.5f, r.near);
            src.maxDistance = Mathf.Max(r.near + 1f, r.range);
            slot.volume = Mathf.Clamp01(r.volume * masterVolume * boost * (0.85f + (float)_rng.NextDouble() * 0.3f));
            slot.range = r.range;
            slot.follow = follow;
            slot.pos = pos + Vector3.up * 0.3f;
            src.transform.position = slot.pos;
            src.volume = slot.volume * VoiceRules.Edge(Vector3.Distance(ListenerPos, slot.pos), r.range);
            src.Play();
            slot.busy = true;
            if (frog) _frogVoices++; else _animalVoices++;
            length = clip.length / pitch;
            return true;
        }

        private static AudioClip ClipFor(VoiceCall call)
        {
            AudioClip clip;
            if (Clips.TryGetValue(call, out clip) && clip != null) return clip;
            float[] data = VoiceSynth.RenderCall(call, call.name != null ? call.name.GetHashCode() & 0xffff : 1);
            if (data.Length == 0) return null;
            int rate = call.rate > 4000 ? call.rate : 22050;
            clip = AudioClip.Create("Voice_" + call.name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            Clips[call] = clip;
            _clipBytes += data.Length * 4L;
            return clip;
        }

        private static AudioClip Crickets()
        {
            if (_crickets != null) return _crickets;
            var d = VoiceSynth.RenderCrickets(22050, 6f, 3);
            _crickets = AudioClip.Create("Crickets", d.Length, 1, 22050, false);
            _crickets.SetData(d, 0);
            _clipBytes += d.Length * 4L;
            return _crickets;
        }

        private static AudioClip Buzz()
        {
            if (_buzz != null) return _buzz;
            var d = VoiceSynth.RenderBuzz(22050);
            _buzz = AudioClip.Create("BeeBuzz", d.Length, 1, 22050, false);
            _buzz.SetData(d, 0);
            _clipBytes += d.Length * 4L;
            return _buzz;
        }
    }
}
