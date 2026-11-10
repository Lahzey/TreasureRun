using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using VoxTool;

namespace SfxTool
{
    /// <summary>
    /// Makes a sound readable without listening: numbers (loudness, attack, decay, brightness, dominant pitch over
    /// time) and a picture (waveform with loudness envelope above a log-frequency spectrogram).
    /// </summary>
    public sealed class Analysis
    {
        public string Name;
        public float Duration, PeakDb, RmsDb, LoudestDb, AttackMs, DecayTo30Db, CentroidHz;
        public float[] EnvelopeDb;     // per 50 ms
        public float[] CentroidOverTime; // per 100 ms (Hz)
        public float[] PitchOverTime;    // per 100 ms (Hz, strongest spectral peak 50 Hz - 5 kHz)
        private float[][] spectrogram;   // [frame][bin] magnitude dB
        private int hop;

        private const int Fft = 2048;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static Analysis Of(Sound s, string name)
        {
            var a = new Analysis { Name = name, Duration = s.Duration };
            float peak = s.S.Length == 0 ? 0f : s.S.Max(x => Math.Abs(x));
            a.PeakDb = Db(peak);
            a.RmsDb = Db(Rms(s.S, 0, s.Length));

            // Loudness envelope (RMS per 50 ms window).
            int win = Sound.Samples(0.05f);
            var env = new List<float>();
            for (int i = 0; i < s.Length; i += win) env.Add(Rms(s.S, i, Math.Min(win, s.Length - i)));
            a.EnvelopeDb = env.Select(Db).ToArray();
            a.LoudestDb = a.EnvelopeDb.Length > 0 ? a.EnvelopeDb.Max() : -120f;

            // Attack: time until the 5 ms envelope reaches 90% of its maximum; decay: until it stays below max -30 dB.
            int fine = Sound.Samples(0.005f);
            var fineEnv = new List<float>();
            for (int i = 0; i < s.Length; i += fine) fineEnv.Add(Rms(s.S, i, Math.Min(fine, s.Length - i)));
            float max = fineEnv.Count > 0 ? fineEnv.Max() : 0f;
            int attack = fineEnv.FindIndex(v => v >= 0.9f * max);
            a.AttackMs = Math.Max(0, attack) * 5f;
            int peakIdx = fineEnv.IndexOf(max);
            float floor = max * (float)Math.Pow(10, -30 / 20.0);
            int lastAbove = fineEnv.FindLastIndex(v => v > floor);
            a.DecayTo30Db = (Math.Max(lastAbove, peakIdx) + 1) * 0.005f;

            // Spectrogram.
            a.hop = 256;
            var window = new double[Fft];
            for (int i = 0; i < Fft; i++) window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Fft - 1));
            var frames = new List<float[]>();
            var centroids = new List<(float t, float c, float w)>();
            var pitches = new List<(float t, float f)>();
            for (int start = -Fft / 2; start < s.Length; start += a.hop)
            {
                var re = new double[Fft];
                var im = new double[Fft];
                for (int i = 0; i < Fft; i++)
                {
                    int k = start + i;
                    re[i] = k >= 0 && k < s.Length ? s.S[k] * window[i] : 0;
                }
                FFT(re, im);
                var mag = new float[Fft / 2];
                double num = 0, den = 0;
                int best = 0;
                double bestMag = 0;
                for (int b = 1; b < Fft / 2; b++)
                {
                    double m = Math.Sqrt(re[b] * re[b] + im[b] * im[b]);
                    mag[b] = Db((float)(m / (Fft / 4)));
                    double f = b * (double)Sound.Rate / Fft;
                    num += f * m; den += m;
                    if (f >= 50 && f <= 5000 && m > bestMag) { bestMag = m; best = b; }
                }
                frames.Add(mag);
                float t = (start + Fft / 2) / (float)Sound.Rate;
                if (den > 1e-6) centroids.Add((t, (float)(num / den), (float)den));
                if (best > 0) pitches.Add((t, best * (float)Sound.Rate / Fft));
            }
            a.spectrogram = frames.ToArray();
            float totalW = centroids.Sum(c => c.w);
            a.CentroidHz = totalW > 0 ? centroids.Sum(c => c.c * c.w) / totalW : 0f;
            int buckets = Math.Max(1, (int)Math.Ceiling(s.Duration / 0.1f));
            a.CentroidOverTime = new float[buckets];
            a.PitchOverTime = new float[buckets];
            for (int bkt = 0; bkt < buckets; bkt++)
            {
                var cs = centroids.Where(c => c.t >= bkt * 0.1f && c.t < (bkt + 1) * 0.1f && c.w > 1e-3).ToList();
                float w = cs.Sum(c => c.w);
                a.CentroidOverTime[bkt] = w > 0 ? cs.Sum(c => c.c * c.w) / w : 0f;
                var ps = pitches.Where(p => p.t >= bkt * 0.1f && p.t < (bkt + 1) * 0.1f).Select(p => p.f).OrderBy(f => f).ToList();
                a.PitchOverTime[bkt] = ps.Count > 0 ? ps[ps.Count / 2] : 0f;
            }
            return a;
        }

        public string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Name}");
            sb.AppendLine($"  duration {F(Duration, "0.00")} s | peak {F(PeakDb, "0.0")} dBFS | RMS {F(RmsDb, "0.0")} dBFS | loudest 50 ms {F(LoudestDb, "0.0")} dBFS");
            sb.AppendLine($"  attack {F(AttackMs, "0")} ms | decays to -30 dB at {F(DecayTo30Db, "0.00")} s | brightness (spectral centroid) {Hz(CentroidHz)}");
            sb.AppendLine($"  envelope per 50 ms (dBFS): {string.Join(" ", EnvelopeDb.Select(d => F(Math.Max(d, -60), "0")))}");
            sb.AppendLine($"  brightness per 100 ms:    {string.Join(" ", CentroidOverTime.Select(Hz))}");
            sb.AppendLine($"  dominant pitch per 100 ms: {string.Join(" ", PitchOverTime.Select(Hz))}");
            return sb.ToString();
        }

        public string TableRow() =>
            $"| {Name} | {F(Duration, "0.00")} | {F(PeakDb, "0.0")} | {F(RmsDb, "0.0")} | {F(LoudestDb, "0.0")} | {F(AttackMs, "0")} | {F(DecayTo30Db, "0.00")} | {Hz(CentroidHz)} |";

        public const string TableHeader =
            "| Sound | Duration (s) | Peak (dBFS) | RMS (dBFS) | Loudest 50 ms | Attack (ms) | -30 dB at (s) | Brightness |\n" +
            "|---|---|---|---|---|---|---|---|";

        /// <summary>Waveform + envelope (top) and log-frequency spectrogram 40 Hz - 16 kHz (bottom); grid every 100 ms.</summary>
        public Image Render(int width = 1200)
        {
            const int waveH = 180, specH = 420, gap = 6;
            var img = new Image(width, waveH + gap + specH, Rgb(20, 20, 24));
            int n = Math.Max(1, spectrogram.Length);
            float secondsPerPx = Duration / width;

            // Time grid every 100 ms (every second brighter).
            for (float t = 0.1f; t < Duration; t += 0.1f)
            {
                int x = (int)(t / secondsPerPx);
                bool second = Math.Abs(t - Math.Round(t)) < 0.001f;
                for (int y = 0; y < img.Height; y++) img.Set(x, y, second ? Rgb(90, 90, 100) : Rgb(45, 45, 52));
            }
            return img;
        }

        public Image Render(Sound s, int width = 1200)
        {
            Image img = Render(width);
            const int waveH = 180, gap = 6, specH = 420;
            int mid = waveH / 2;
            // Waveform (min/max per column) and RMS envelope.
            int perPx = Math.Max(1, s.Length / width);
            for (int x = 0; x < width; x++)
            {
                int a = (int)((long)x * s.Length / width), b = Math.Min(s.Length, a + perPx);
                if (a >= s.Length) break;
                float lo = 0, hi = 0, sq = 0;
                for (int i = a; i < b; i++) { lo = Math.Min(lo, s.S[i]); hi = Math.Max(hi, s.S[i]); sq += s.S[i] * s.S[i]; }
                for (int y = (int)(mid - hi * mid); y <= (int)(mid - lo * mid); y++) img.Set(x, y, Rgb(90, 170, 230));
                float rms = (float)Math.Sqrt(sq / Math.Max(1, b - a));
                img.Set(x, (int)(mid - rms * mid), Rgb(255, 200, 80));
                img.Set(x, (int)(mid + rms * mid), Rgb(255, 200, 80));
            }
            for (int x = 0; x < width; x++) img.Set(x, mid, Rgb(70, 70, 80));

            // Spectrogram, log frequency.
            double fMin = 40, fMax = 16000;
            int top = waveH + gap;
            for (int x = 0; x < width; x++)
            {
                int frame = Math.Min(spectrogram.Length - 1, (int)((x * Duration / width) * Sound.Rate / hop));
                if (frame < 0) continue;
                float[] mag = spectrogram[frame];
                for (int y = 0; y < specH; y++)
                {
                    double f = fMin * Math.Pow(fMax / fMin, (specH - 1 - y) / (double)(specH - 1));
                    int bin = Math.Clamp((int)Math.Round(f * Fft / Sound.Rate), 1, mag.Length - 1);
                    img.Set(x, top + y, Heat((mag[bin] + 90f) / 90f));
                }
            }
            foreach (double f in new[] { 100.0, 1000.0, 10000.0 })
            {
                int y = top + (int)((specH - 1) * (1 - Math.Log(f / fMin) / Math.Log(fMax / fMin)));
                for (int x = 0; x < width; x += 4) img.Set(x, y, Rgb(200, 200, 200));
            }
            return img;
        }

        // ------------------------------------------------------------------ helpers

        private static float Rms(float[] s, int start, int count)
        {
            if (count <= 0) return 0f;
            double sum = 0;
            for (int i = start; i < start + count; i++) sum += s[i] * s[i];
            return (float)Math.Sqrt(sum / count);
        }

        private static float Db(float v) => v <= 1e-6f ? -120f : 20f * (float)Math.Log10(v);
        private static string F(float v, string fmt) => v.ToString(fmt, Inv);
        private static string Hz(float f) => f <= 0 ? "-" : f >= 1000 ? (f / 1000f).ToString("0.0", Inv) + "k" : f.ToString("0", Inv);
        private static uint Rgb(int r, int g, int b) => (uint)(r | g << 8 | b << 16 | 255 << 24);

        /// <summary>Black - purple - red - orange - yellow - white.</summary>
        private static uint Heat(float v)
        {
            v = Math.Clamp(v, 0f, 1f);
            (float p, int r, int g, int b)[] stops = { (0f, 0, 0, 0), (0.3f, 70, 20, 110), (0.55f, 200, 40, 60), (0.75f, 250, 140, 30), (0.9f, 255, 230, 80), (1f, 255, 255, 255) };
            for (int i = 1; i < stops.Length; i++)
            {
                if (v > stops[i].p) continue;
                float t = (v - stops[i - 1].p) / (stops[i].p - stops[i - 1].p);
                return Rgb((int)(stops[i - 1].r + (stops[i].r - stops[i - 1].r) * t), (int)(stops[i - 1].g + (stops[i].g - stops[i - 1].g) * t),
                           (int)(stops[i - 1].b + (stops[i].b - stops[i - 1].b) * t));
            }
            return Rgb(255, 255, 255);
        }

        private static void FFT(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                double wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = a + len / 2;
                        double tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti;
                        re[a] += tr; im[a] += ti;
                        double ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }
    }
}
