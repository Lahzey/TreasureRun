using System;

namespace SfxTool
{
    public enum Wave { Sine, Triangle, Saw, Square, Pulse25 }
    public enum NoiseColor { White, Pink, Brown }

    /// <summary>
    /// A mono sound buffer (44.1 kHz, floats in -1..1) with a small, chainable set of synthesis and effect operations.
    /// Every operation returns a new buffer, so recipes read like a signal chain:
    /// <code>Sound.Noise(NoiseColor.White, 0.4f, seed).LowPass(t => 4000 - 3000 * t).Envelope(Env.Exp(8)).Normalize(-3)</code>
    /// Functions of time (<c>Func&lt;float, float&gt;</c>) take seconds from the start of the buffer.
    /// </summary>
    public sealed class Sound
    {
        public const int Rate = 44100;
        public readonly float[] S;

        public Sound(int length) { S = new float[Math.Max(0, length)]; }
        public Sound(float[] samples) { S = samples; }

        public int Length => S.Length;
        public float Duration => S.Length / (float)Rate;
        public static int Samples(float seconds) => (int)Math.Round(seconds * Rate);

        // ------------------------------------------------------------------ sources

        public static Sound Silence(float seconds) => new Sound(Samples(seconds));

        /// <summary>Oscillator with a time-varying frequency (Hz) and optional amplitude.</summary>
        public static Sound Tone(Wave wave, float seconds, Func<float, float> freq, Func<float, float> amp = null, float phase = 0f)
        {
            var s = new Sound(Samples(seconds));
            double ph = phase;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                ph += freq(t) / Rate;
                ph -= Math.Floor(ph);
                s.S[i] = Osc(wave, ph) * (amp?.Invoke(t) ?? 1f);
            }
            return s;
        }

        public static Sound Tone(Wave wave, float seconds, float freq, Func<float, float> amp = null) => Tone(wave, seconds, _ => freq, amp);

        /// <summary>Two-operator FM: carrier frequency, modulator frequency and modulation index over time.</summary>
        public static Sound FM(float seconds, Func<float, float> carrier, Func<float, float> modulator, Func<float, float> index, Func<float, float> amp = null)
        {
            var s = new Sound(Samples(seconds));
            double pc = 0, pm = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                pm += modulator(t) / Rate;
                double mod = Math.Sin(2 * Math.PI * pm) * index(t);
                pc += carrier(t) / Rate;
                s.S[i] = (float)Math.Sin(2 * Math.PI * pc + mod) * (amp?.Invoke(t) ?? 1f);
            }
            return s;
        }

        public static Sound Noise(NoiseColor color, float seconds, int seed = 1, Func<float, float> amp = null)
        {
            var s = new Sound(Samples(seconds));
            var rng = new Random(seed);
            double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0, brown = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double white = rng.NextDouble() * 2 - 1;
                double v;
                switch (color)
                {
                    case NoiseColor.Pink: // Paul Kellet's filter
                        b0 = 0.99886 * b0 + white * 0.0555179; b1 = 0.99332 * b1 + white * 0.0750759;
                        b2 = 0.96900 * b2 + white * 0.1538520; b3 = 0.86650 * b3 + white * 0.3104856;
                        b4 = 0.55000 * b4 + white * 0.5329522; b5 = -0.7616 * b5 - white * 0.0168980;
                        v = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362) * 0.11;
                        b6 = white * 0.115926;
                        break;
                    case NoiseColor.Brown:
                        brown = (brown + 0.02 * white) / 1.02;
                        v = brown * 3.5;
                        break;
                    default: v = white; break;
                }
                s.S[i] = (float)v * (amp?.Invoke(i / (float)Rate) ?? 1f);
            }
            return s;
        }

        private static float Osc(Wave w, double ph)
        {
            switch (w)
            {
                case Wave.Sine: return (float)Math.Sin(2 * Math.PI * ph);
                case Wave.Triangle: return (float)(ph < 0.5 ? 4 * ph - 1 : 3 - 4 * ph);
                case Wave.Saw: return (float)(2 * ph - 1);
                case Wave.Square: return ph < 0.5 ? 1f : -1f;
                case Wave.Pulse25: return ph < 0.25 ? 1f : -1f;
                default: return 0f;
            }
        }

        // ------------------------------------------------------------------ amplitude

        public Sound Gain(float g) => Map((x, _) => x * g);
        public Sound Envelope(Func<float, float> env) => Map((x, t) => x * env(t));

        /// <summary>Scales so the peak is at <paramref name="peakDb"/> dBFS.</summary>
        public Sound Normalize(float peakDb = -1f)
        {
            float peak = 0f;
            foreach (float x in S) peak = Math.Max(peak, Math.Abs(x));
            return peak <= 0f ? Copy() : Gain((float)Math.Pow(10, peakDb / 20.0) / peak);
        }

        public Sound Fade(float fadeIn, float fadeOut) => Map((x, t) =>
        {
            float g = 1f;
            if (fadeIn > 0 && t < fadeIn) g *= t / fadeIn;
            if (fadeOut > 0 && t > Duration - fadeOut) g *= Math.Max(0f, (Duration - t) / fadeOut);
            return x * g;
        });

        // ------------------------------------------------------------------ filters and shaping

        public Sound LowPass(Func<float, float> cutoff, float q = 0.707f) => Biquad(cutoff, q, FilterType.LowPass);
        public Sound HighPass(Func<float, float> cutoff, float q = 0.707f) => Biquad(cutoff, q, FilterType.HighPass);
        public Sound BandPass(Func<float, float> center, float q = 1f) => Biquad(center, q, FilterType.BandPass);
        public Sound LowPass(float cutoff, float q = 0.707f) => LowPass(_ => cutoff, q);
        public Sound HighPass(float cutoff, float q = 0.707f) => HighPass(_ => cutoff, q);
        public Sound BandPass(float center, float q = 1f) => BandPass(_ => center, q);

        private enum FilterType { LowPass, HighPass, BandPass }

        /// <summary>RBJ biquad with per-sample coefficients (time-varying cutoff).</summary>
        private Sound Biquad(Func<float, float> freq, float q, FilterType type)
        {
            var o = new Sound(Length);
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < Length; i++)
            {
                float f = Math.Clamp(freq(i / (float)Rate), 10f, Rate * 0.45f);
                double w0 = 2 * Math.PI * f / Rate, cos = Math.Cos(w0), alpha = Math.Sin(w0) / (2 * q);
                double b0, b1, b2, a0 = 1 + alpha, a1 = -2 * cos, a2 = 1 - alpha;
                switch (type)
                {
                    case FilterType.LowPass: b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = (1 - cos) / 2; break;
                    case FilterType.HighPass: b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = (1 + cos) / 2; break;
                    default: b0 = alpha; b1 = 0; b2 = -alpha; break;
                }
                double x = S[i];
                double y = (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                o.S[i] = (float)y;
            }
            return o;
        }

        /// <summary>
        /// A brickwall low-pass (windowed-sinc FIR, Blackman window): almost nothing passes above <paramref name="cutoff"/>.
        /// Biquads roll off at 12 dB per octave; this matches hard cuts such as an MP3's 16 kHz edge.
        /// </summary>
        public Sound BrickWall(float cutoff, int taps = 255)
        {
            int half = taps / 2;
            var h = new double[taps];
            double fc = cutoff / Rate, sum = 0;
            for (int i = 0; i < taps; i++)
            {
                int n = i - half;
                double sinc = n == 0 ? 2 * fc : Math.Sin(2 * Math.PI * fc * n) / (Math.PI * n);
                double w = 0.42 - 0.5 * Math.Cos(2 * Math.PI * i / (taps - 1)) + 0.08 * Math.Cos(4 * Math.PI * i / (taps - 1));
                h[i] = sinc * w;
                sum += h[i];
            }
            var o = new float[Length];
            for (int i = 0; i < Length; i++)
            {
                double acc = 0;
                for (int k = 0; k < taps; k++)
                {
                    int j = i + k - half;
                    if (j >= 0 && j < Length) acc += S[j] * h[k];
                }
                o[i] = (float)(acc / sum);
            }
            return new Sound(o);
        }

        /// <summary>
        /// Zero-phase FFT equaliser from (frequency, dB) points (linear in dB over log frequency, flat outside), e.g.
        /// <c>Eq((125, -20), (1000, 0), (4000, 6), (16000, -10))</c>: any measured spectral shape in one step.
        /// </summary>
        public Sound Eq(params (float hz, float db)[] points) => new Sound(Dsp.Eq(S, points));

        /// <summary>A resonator (Dsp.Resonate): this signal excites a mode at <paramref name="hz"/> decaying by the given dB per second.</summary>
        public Sound Resonate(float hz, float decayDbPerSecond) => new Sound(Dsp.Resonate(S, hz, decayDbPerSecond));

        /// <summary>A single-sample impulse of the given height at <paramref name="at"/> seconds in a buffer of <paramref name="seconds"/>.</summary>
        public static Sound Impulse(float seconds, float at = 0f, float height = 1f)
        {
            var s = new Sound(Samples(seconds));
            int i = (int)(at * Rate);
            if (i >= 0 && i < s.Length) s.S[i] = height;
            return s;
        }

        /// <summary>Soft saturation (tanh). 1 = mild, 5+ = heavy.</summary>
        public Sound Drive(float amount) => Map((x, _) => (float)(Math.Tanh(x * amount) / Math.Tanh(amount)));

        /// <summary>Bit depth and sample-rate reduction (lo-fi crunch).</summary>
        public Sound Crush(int bits, int hold = 1)
        {
            var o = new Sound(Length);
            float steps = (float)Math.Pow(2, bits - 1), held = 0f;
            for (int i = 0; i < Length; i++)
            {
                if (i % Math.Max(1, hold) == 0) held = (float)Math.Round(S[i] * steps) / steps;
                o.S[i] = held;
            }
            return o;
        }

        public Sound RingMod(Func<float, float> freq)
        {
            var o = new Sound(Length);
            double ph = 0;
            for (int i = 0; i < Length; i++)
            {
                ph += freq(i / (float)Rate) / Rate;
                o.S[i] = S[i] * (float)Math.Sin(2 * Math.PI * ph);
            }
            return o;
        }

        // ------------------------------------------------------------------ time and space

        /// <summary>Feedback echo; the result is longer by the echo tail.</summary>
        public Sound Delay(float seconds, float feedback, float mix, int repeats = 6)
        {
            int d = Samples(seconds);
            var o = new Sound(Length + d * repeats);
            Array.Copy(S, o.S, Length);
            for (int i = d; i < o.Length; i++) o.S[i] += o.S[i - d] * feedback;
            var dry = Pad(o.Length);
            return dry.Gain(1f - mix).Add(o.Gain(mix));
        }

        /// <summary>Small Freeverb-style reverb (4 combs + 2 allpasses); adds a tail of <paramref name="tail"/> seconds.</summary>
        public Sound Reverb(float room = 0.7f, float damp = 0.4f, float mix = 0.25f, float tail = 0.6f)
        {
            Sound input = Pad(Length + Samples(tail));
            var wet = new Sound(input.Length);
            int[] combs = { 1116, 1188, 1277, 1356 };
            foreach (int len in combs)
            {
                var buf = new float[len];
                int idx = 0;
                float store = 0f;
                for (int i = 0; i < input.Length; i++)
                {
                    float y = buf[idx];
                    store = y * (1 - damp) + store * damp;
                    buf[idx] = input.S[i] + store * (0.7f + 0.28f * room);
                    idx = (idx + 1) % len;
                    wet.S[i] += y * 0.25f;
                }
            }
            foreach (int len in new[] { 556, 441 })
            {
                var buf = new float[len];
                int idx = 0;
                for (int i = 0; i < wet.Length; i++)
                {
                    float b = buf[idx];
                    float y = -wet.S[i] + b;
                    buf[idx] = wet.S[i] + b * 0.5f;
                    idx = (idx + 1) % len;
                    wet.S[i] = y;
                }
            }
            return input.Gain(1f - mix).Add(wet.Gain(mix));
        }

        /// <summary>Resamples (pitch and speed together); 2 = an octave up and half as long.</summary>
        public Sound Pitch(float factor)
        {
            var o = new Sound((int)(Length / factor));
            for (int i = 0; i < o.Length; i++)
            {
                double p = i * factor;
                int a = (int)p;
                float f = (float)(p - a);
                float x0 = a < Length ? S[a] : 0f, x1 = a + 1 < Length ? S[a + 1] : 0f;
                o.S[i] = x0 + (x1 - x0) * f;
            }
            return o;
        }

        public Sound Reverse()
        {
            var o = Copy();
            Array.Reverse(o.S);
            return o;
        }

        public Sound Trim(float seconds) => new Sound(S.AsSpan(0, Math.Min(Length, Samples(seconds))).ToArray());

        // ------------------------------------------------------------------ combining

        /// <summary>Sample-wise sum (result as long as the longer one).</summary>
        public Sound Add(Sound other) => Mix(other, 0f, 1f);

        /// <summary>Mixes <paramref name="other"/> in at <paramref name="at"/> seconds with a gain.</summary>
        public Sound Mix(Sound other, float at, float gain = 1f)
        {
            int offset = Samples(at);
            var o = Pad(Math.Max(Length, offset + other.Length));
            for (int i = 0; i < other.Length; i++) o.S[offset + i] += other.S[i] * gain;
            return o;
        }

        public static Sound Layer(params Sound[] parts)
        {
            Sound o = new Sound(0);
            foreach (Sound p in parts) o = o.Add(p);
            return o;
        }

        public Sound Then(Sound next, float gap = 0f) => Mix(next, Duration + gap);

        public Sound Pad(int length)
        {
            var o = new Sound(Math.Max(length, Length));
            Array.Copy(S, o.S, Length);
            return o;
        }

        public Sound Copy() => new Sound((float[])S.Clone());

        public Sound Map(Func<float, float, float> f)
        {
            var o = new Sound(Length);
            for (int i = 0; i < Length; i++) o.S[i] = f(S[i], i / (float)Rate);
            return o;
        }
    }

    /// <summary>Envelope shapes (functions of time in seconds, returning 0..1).</summary>
    public static class Env
    {
        /// <summary>Exponential decay from 1 with the given rate (1/s); 8 ≈ -70 dB after 1 s.</summary>
        public static Func<float, float> Exp(float rate, float attack = 0.002f) =>
            t => (attack > 0 && t < attack ? t / attack : 1f) * (float)Math.Exp(-rate * Math.Max(0, t - attack));

        /// <summary>Attack / decay / sustain level / release, over a note of <paramref name="hold"/> seconds.</summary>
        public static Func<float, float> Adsr(float a, float d, float s, float r, float hold) => t =>
        {
            if (t < a) return t / a;
            if (t < a + d) return 1f - (1f - s) * (t - a) / d;
            if (t < hold) return s;
            return Math.Max(0f, s * (1f - (t - hold) / r));
        };

        /// <summary>Piecewise-linear curve through (time, value) pairs.</summary>
        public static Func<float, float> Points(params float[] tv) => t =>
        {
            if (t <= tv[0]) return tv[1];
            for (int i = 2; i < tv.Length; i += 2)
                if (t <= tv[i]) return tv[i - 1] + (tv[i + 1] - tv[i - 1]) * (t - tv[i - 2]) / (tv[i] - tv[i - 2]);
            return tv[tv.Length - 1];
        };

        /// <summary>Exponential sweep from a to b over <paramref name="seconds"/> (frequencies sound even).</summary>
        public static Func<float, float> Sweep(float a, float b, float seconds) =>
            t => a * (float)Math.Pow(b / a, Math.Clamp(t / seconds, 0f, 1f));
    }
}
