using System;

// PURE C# (no UnityEngine): the data model of a voice (2.0 "Sound"). A harness outside Unity compiles VoiceRecipe.cs, VoiceSynth.cs,
// VoiceRules.cs and VoiceBank.cs together, renders every call to a WAV and a spectrogram, and simulates the scheduler.
namespace WildLens.Sound
{
    /// <summary>
    /// One note of a call: a pitch sweep with an envelope. Frequencies are in Hz, times in seconds.
    /// <c>f0</c> to <c>f1</c> is the sweep (optionally through <c>fm</c> at <c>mAt</c> of the way, a rounded peak or dip).
    /// <c>gap</c> is the silence after the note (negative overlaps the next note, to join slurs).
    /// </summary>
    [Serializable]
    public class VoiceNote
    {
        public float f0 = 3000f, f1 = 3000f;
        /// <summary>Middle pitch of a three-point sweep; 0 = a straight sweep f0 to f1.</summary>
        public float fm;
        public float mAt = 0.5f;
        public float dur = 0.2f, gap = 0.08f, amp = 1f;
        /// <summary>Vibrato rate (Hz) and depth (fraction of the pitch).</summary>
        public float vib, vibDepth;
        /// <summary>0 pure whistle .. 1 rich in harmonics (buzzy, reedy).</summary>
        public float buzz;
        /// <summary>0 tone .. 1 band-limited noise around the pitch (harsh, breathy).</summary>
        public float noise;
        /// <summary>Noise bandwidth: smoothing 0.02 (narrow) .. 0.9 (wide).</summary>
        public float noiseBw = 0.15f;
        /// <summary>Pulse (amplitude) rate in Hz and depth 0..1: trills, rattles, roughness.</summary>
        public float am, amDepth;
        /// <summary>Attack seconds; decay exponent: 0 sustained, 0.35 whistle, 3 pluck.</summary>
        public float atk = 0.01f, decay = 0.35f;
        /// <summary>Sweep shape: 1 linear, above 1 stays near f0 first, below 1 moves first.</summary>
        public float curve = 1f;
    }

    /// <summary>A named call (song, call, alarm): a sequence of notes rendered once to a clip at <see cref="rate"/> Hz.</summary>
    [Serializable]
    public class VoiceCall
    {
        public string name = "call";
        public int rate = 22050;
        public VoiceNote[] notes = new VoiceNote[0];
        public bool Has => notes != null && notes.Length > 0;
    }

    /// <summary>
    /// A species' voice: up to three calls and when, how often and how far it is heard. Pure data (like WeatherHabits); the rules are
    /// <see cref="VoiceRules"/>, the player of it is the VoiceDirector. Tuning one species is one line in <see cref="VoiceBank"/>.
    /// </summary>
    [Serializable]
    public class VoiceRecipe
    {
        public bool enabled;
        public VoiceCall song = new VoiceCall { name = "song" };
        public VoiceCall call = new VoiceCall { name = "call" };
        public VoiceCall alarm = new VoiceCall { name = "alarm" };

        /// <summary>Chance that a calm call is the song rather than the call.</summary>

        public float songShare = 0.6f;
        /// <summary>Average calls per minute for ONE individual in ordinary fair daylight (before the multipliers below).</summary>
        public float perMinute = 1f;
        /// <summary>Multiplier by day period: night, dawn, morning, midday, golden hour, dusk (DayPeriod order). Dawn is the chorus.</summary>
        public float[] period = { 0f, 1f, 1f, 1f, 1f, 1f };
        /// <summary>Multiplier while it rains (0 silent).</summary>
        public float rain = 0.05f;
        /// <summary>Multiplier in the first sun after rain (WeatherCycle.SunAfterRain).</summary>
        public float afterRain = 1f;
        /// <summary>Multiplier within six game hours of rain (frogs).</summary>
        public float wet = 1f;
        public bool fairOnly;
        public bool needsWetNight;
        public bool atRest = true, inFlight;
        /// <summary>Still calls while Aware of the player (otherwise only while Calm).</summary>
        public bool awareOk;
        /// <summary>Chance of the alarm when it first becomes Alert, and the seconds before it can sound again.</summary>
        public float alarmChance, alarmCooldown = 40f;
        /// <summary>Chance that a neighbour of the same species answers (counter-singing; frogs: chorus).</summary>
        public float answer = 0.3f;
        /// <summary>Seconds an individual stays quiet after calling.</summary>
        public float minGap = 6f;
        /// <summary>Heard no farther than this (metres); the full-volume distance 'near' sets how it carries.</summary>
        public float range = 70f, near = 4f;
        /// <summary>Source volume 0..1 before the master volume.</summary>
        public float volume = 0.5f;

        public bool Has => enabled && ((song != null && song.Has) || (call != null && call.Has) || (alarm != null && alarm.Has));
        public bool HasCalm => (song != null && song.Has) || (call != null && call.Has);
    }

}
