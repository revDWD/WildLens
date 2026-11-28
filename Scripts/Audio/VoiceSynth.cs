using System;

// PURE C# (no UnityEngine): float[] in, float[] out. Renders a VoiceCall (notes) or a bed (crickets, a bee) to mono PCM in [-1, 1].
// Everything is generated once, cached by the VoiceDirector, and never touched per frame. A harness outside Unity compiles this file to
// write WAVs and spectrograms. Loudness: every call is normalised to the same RMS over its sounding parts, then limited by its peak,
// so a species' loudness is only its recipe's volume (see VoiceRecipe.volume).
namespace WildLens.Sound
{
    public static class VoiceSynth
    {
        public const float TargetRms = 0.14f, PeakLimit = 0.9f;
        private const double TwoPi = Math.PI * 2.0;

        /// <summary>Renders the notes of a call. The result starts and ends silent (at least 5 ms faded) and has no DC offset.</summary>
        public static float[] RenderCall(VoiceCall call, int seed)
        {
            if (call == null || call.notes == null || call.notes.Length == 0) return new float[0];
            int rate = call.rate > 4000 ? call.rate : 22050;
            var notes = call.notes;
            var starts = new float[notes.Length];
            float t = 0f, end = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                starts[i] = Math.Max(0f, t);
                end = Math.Max(end, starts[i] + notes[i].dur);
                t = starts[i] + notes[i].dur + notes[i].gap;
            }
            int n = (int)Math.Ceiling((end + 0.04f) * rate);
            var buf = new float[n];
            for (int i = 0; i < notes.Length; i++)
                RenderNote(buf, rate, notes[i], (int)(starts[i] * rate), new Random(seed * 7919 + i * 31 + 1));
            Normalise(buf, rate);
            return buf;
        }

        private static float Smooth(float x) { x = x < 0f ? 0f : x > 1f ? 1f : x; return x * x * (3f - 2f * x); }

        /// <summary>The pitch of a note at u (0..1) of its length.</summary>
        public static float Pitch(VoiceNote nt, float u)
        {
            if (nt.fm > 0f)
            {
                float m = Math.Min(0.95f, Math.Max(0.05f, nt.mAt));
                return u < m ? nt.f0 + (nt.fm - nt.f0) * Smooth(u / m) : nt.fm + (nt.f1 - nt.fm) * Smooth((u - m) / (1f - m));
            }
            float c = nt.curve <= 0f ? 1f : nt.curve;
            return nt.f0 + (nt.f1 - nt.f0) * (float)Math.Pow(u, c);
        }

        private static void RenderNote(float[] buf, int rate, VoiceNote nt, int start, Random rng)
        {
            int len = (int)(nt.dur * rate);
            if (len < 8) return;
            double phase = 0.0, amPhase = 0.0;
            float ns = 0f;
            float bw = Math.Min(0.9f, Math.Max(0.02f, nt.noiseBw));
            float nGain = 1.2247f * (float)Math.Sqrt((2f - bw) / bw);
            float h2 = 0.55f * nt.buzz, h3 = 0.4f * nt.buzz, h4 = 0.22f * nt.buzz, h5 = 0.12f * nt.buzz;
            float atk = Math.Max(0.005f, nt.atk);
            int fadeIn = Math.Max(1, (int)(0.006f * rate));
            // The end of a note is rounded off over up to 30 ms (a quarter of its length at most): whistles taper, they are not cut.
            int fade = Math.Max(fadeIn, (int)(Math.Min(0.03f, 0.25f * nt.dur) * rate));
            float nyq = 0.45f * rate;
            for (int k = 0; k < len && start + k < buf.Length; k++)
            {
                float time = k / (float)rate, u = k / (float)len;
                float f = Pitch(nt, u);
                if (nt.vib > 0f && nt.vibDepth > 0f) f *= 1f + nt.vibDepth * (float)Math.Sin(TwoPi * nt.vib * time);
                phase += TwoPi * f / rate;
                if (phase > 1e5) phase -= 1e5 * Math.Floor(phase / 1e5);
                float s = (float)Math.Sin(phase);
                if (h2 > 0f && 2f * f < nyq) s += h2 * (float)Math.Sin(2.0 * phase);
                if (h3 > 0f && 3f * f < nyq) s += h3 * (float)Math.Sin(3.0 * phase + 0.7);
                if (h4 > 0f && 4f * f < nyq) s += h4 * (float)Math.Sin(4.0 * phase + 1.9);
                if (h5 > 0f && 5f * f < nyq) s += h5 * (float)Math.Sin(5.0 * phase + 2.6);
                if (h2 > 0f) s /= 1f + 0.5f * nt.buzz; // keep the tone near unit level whatever the harmonics
                if (nt.noise > 0f)
                {
                    ns += ((float)rng.NextDouble() * 2f - 1f - ns) * bw;
                    float nz = ns * nGain * (float)Math.Sin(phase);
                    s = (1f - nt.noise) * s + nt.noise * nz;
                }
                if (nt.am > 0f && nt.amDepth > 0f)
                {
                    amPhase += nt.am / (double)rate;
                    float p = 0.5f * (1f - (float)Math.Cos(TwoPi * amPhase));
                    p *= (float)Math.Sqrt(p);
                    s *= 1f - nt.amDepth + nt.amDepth * p;
                }
                float env = Smooth(time / atk) * (float)Math.Pow(1f - u, Math.Max(0f, nt.decay));
                if (k < fadeIn) env *= k / (float)fadeIn;                // at least 6 ms in: never a click
                if (len - 1 - k < fade) { float q = (len - 1 - k) / (float)fade; env *= q * q * (3f - 2f * q); }
                buf[start + k] += s * env * nt.amp * 0.5f;
            }
        }

        /// <summary>Removes DC, then scales so the sounding parts have <see cref="TargetRms"/> unless that would pass <see cref="PeakLimit"/>; fades the ends.</summary>
        public static void Normalise(float[] buf, int rate, bool fadeEnds = true)
        {
            int n = buf.Length;
            if (n == 0) return;
            double mean = 0;
            for (int i = 0; i < n; i++) mean += buf[i];
            mean /= n;
            float peak = 0f;
            for (int i = 0; i < n; i++) { buf[i] -= (float)mean; peak = Math.Max(peak, Math.Abs(buf[i])); }
            if (peak < 1e-6f) return;
            double sum = 0; int cnt = 0;
            float thr = peak * 0.1f;
            for (int i = 0; i < n; i++) if (Math.Abs(buf[i]) > thr) { sum += buf[i] * buf[i]; cnt++; }
            float rms = cnt > 0 ? (float)Math.Sqrt(sum / cnt) : peak;
            float gain = TargetRms / rms;
            if (peak * gain > PeakLimit) gain = PeakLimit / peak;
            int fade = fadeEnds ? Math.Max(1, (int)(0.006f * rate)) : 0;
            for (int i = 0; i < n; i++)
            {
                float g = gain;
                if (i < fade) g *= i / (float)fade;
                if (fade > 0 && n - 1 - i < fade) g *= (n - 1 - i) / (float)fade;
                buf[i] *= g;
            }
        }

        // ------------------------------------------------------------------ beds

        /// <summary>
        /// A soft bed of field crickets: seven insects, each its own pitch (4.3 to 4.9 kHz) and rhythm, chirping in short pulse trains.
        /// Loops seamlessly over <paramref name="seconds"/> (pulses wrap around the end). Quiet by design: the director adds the level.
        /// </summary>
        public static float[] RenderCrickets(int rate, float seconds, int seed)
        {
            int n = (int)(rate * seconds);
            var buf = new float[n];
            var rng = new Random(seed);
            int insects = 7;
            for (int c = 0; c < insects; c++)
            {
                float f = 4300f + 600f * (float)rng.NextDouble();
                float period = 0.42f + 0.5f * (float)rng.NextDouble();       // seconds between chirps
                period = seconds / (float)Math.Max(1, Math.Round(seconds / period)); // a whole number of chirps per loop: it repeats seamlessly
                float offset = (float)rng.NextDouble() * period;
                int pulses = 3 + rng.Next(3);
                float pulseGap = 0.028f + 0.01f * (float)rng.NextDouble(), pulseLen = 0.014f;
                float amp = 0.5f + 0.5f * (float)rng.NextDouble();
                for (float t0 = offset; t0 < seconds; t0 += period)
                    for (int p = 0; p < pulses; p++)
                    {
                        int s0 = (int)((t0 + p * pulseGap) * rate), l = (int)(pulseLen * rate);
                        for (int k = 0; k < l; k++)
                        {
                            float env = (float)Math.Sin(Math.PI * k / l);
                            env *= env;
                            buf[(s0 + k) % n] += (float)Math.Sin(TwoPi * f * k / rate) * env * amp * 0.5f;
                        }
                    }
            }
            Normalise(buf, rate, false); // a loop: no fade at the seam
            // Soften: this is a bed, and Normalise gave it the loudness of a call.
            for (int i = 0; i < n; i++) buf[i] *= 0.6f;
            return buf;
        }

        /// <summary>A bee's buzz: a 190 Hz hum with harmonics and a slow wobble, exactly one second so it loops without a seam.</summary>
        public static float[] RenderBuzz(int rate)
        {
            var buf = new float[rate];
            for (int i = 0; i < rate; i++)
            {
                double t = i / (double)rate;
                double wob = 1.0 + 0.012 * Math.Sin(TwoPi * 4.0 * t); // keeps whole cycles over the second only for the carrier below
                float s = 0f;
                for (int h = 1; h <= 7; h++) s += (float)(Math.Sin(TwoPi * 190.0 * h * t + 0.5 * Math.Sin(TwoPi * 4.0 * t) * 0.15 * h) / (1.0 + 0.55 * h));
                s *= (float)(0.8 + 0.2 * Math.Sin(TwoPi * 4.0 * t)) * (float)wob;
                buf[i] = s;
            }
            Normalise(buf, rate, false);
            return buf;
        }
    }
}
