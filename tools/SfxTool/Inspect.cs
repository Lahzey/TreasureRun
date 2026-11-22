using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using VoxTool;

namespace SfxTool
{
    /// <summary>
    /// inspect &lt;file&gt; [out-prefix] [t0 t1]: the fine structure of a sound, what band energies can't show
    /// (docs/sound-workflow.md, "Matching an example"). Levelled and onset-trimmed like <c>compare</c>.
    ///  - transients: every click or knock (time, level, how sudden, how fast it decays, its brightness);
    ///  - impulsiveness every 25 ms: crest factor (peak / RMS: noise ≈ 3-4, clicks and knocks 6-20) of the full band and of
    ///    the highs, and how many transients fall in it;
    ///  - resonances: spectral lines with their frequency, strength, start and decay rate (dB/s), from a long FFT;
    ///  - pictures: the waveform of the window at high zoom, and a short-window spectrogram (256-point FFT, 0.36 ms hop)
    ///    that shows individual clicks as vertical lines.
    /// </summary>
    public static class Inspect
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public sealed class Transient
        {
            public float Time, Level, Jump, Decay, Centroid;
        }

        /// <summary>envelope &lt;file&gt; t0 t1 [step ms]: level per step in four bands (onset-trimmed, levelled), to read attacks and decays.</summary>
        public static int Envelope(string[] args)
        {
            Sound s = Compare.Prepare(AudioIO.Read(args[0]));
            float t0 = float.Parse(args[1], Inv), t1 = float.Parse(args[2], Inv), step = args.Length > 3 ? float.Parse(args[3], Inv) / 1000f : 0.002f;
            var bands = new (string name, Sound s)[]
            {
                ("full", s),
                ("<1k", s.HighPass(150f).LowPass(1000f).LowPass(1000f)),
                ("1-4k", s.HighPass(1000f).HighPass(1000f).LowPass(4000f).LowPass(4000f)),
                ("4-16k", s.HighPass(4000f).HighPass(4000f)),
            };
            Console.WriteLine("  time  " + string.Join(" ", bands.Select(b => $"{b.name,6}")) + "   (dB, RMS over the step)");
            int w = Math.Max(1, (int)(step * Sound.Rate));
            for (float t = t0; t < t1; t += step)
            {
                int i = (int)(t * Sound.Rate);
                if (i + w > s.Length) break;
                Console.WriteLine($"{t,6:0.000} " + string.Join(" ", bands.Select(b => $"{Compare.Db(Compare.Rms(b.s.S, i, w)),6:0}")) +
                                  "  " + new string('#', Math.Max(0, (int)((Compare.Db(Compare.Rms(s.S, i, w)) + 60) / 2))));
            }
            return 0;
        }

        /// <summary>
        /// levels &lt;file&gt; t0 t1 [step ms]: the octave-band levels (31 Hz – 16 kHz) per step, the spectral shape over time
        /// at a glance (a door's low body, where a rustle's energy sits). FFT of 2048 samples centred on each step.
        /// </summary>
        public static int Levels(string[] args)
        {
            Sound s = Compare.Prepare(AudioIO.Read(args[0]));
            float t0 = float.Parse(args[1], Inv), t1 = float.Parse(args[2], Inv), step = args.Length > 3 ? float.Parse(args[3], Inv) / 1000f : 0.02f;
            float[] centres = { 31, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
            const int fft = 2048;
            var win = new double[fft];
            for (int i = 0; i < fft; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (fft - 1));
            Console.WriteLine("  time " + string.Join("", centres.Select(c => $"{(c >= 1000 ? (c / 1000) + "k" : c.ToString()),6}")) + "   (dB per octave band)");
            for (float t = t0; t < t1; t += step)
            {
                var re = new double[fft]; var im = new double[fft];
                int c0 = (int)(t * Sound.Rate) - fft / 2;
                for (int i = 0; i < fft; i++) { int j = c0 + i; re[i] = j >= 0 && j < s.Length ? s.S[j] * win[i] : 0; }
                Dsp.Fft(re, im);
                var row = centres.Select(c =>
                {
                    int b0 = Math.Max(1, (int)(c / 1.414f * fft / Sound.Rate)), b1 = Math.Min(fft / 2 - 1, Math.Max(b0, (int)(c * 1.414f * fft / Sound.Rate)));
                    double sum = 0;
                    for (int b = b0; b <= b1; b++) sum += re[b] * re[b] + im[b] * im[b];
                    return Math.Max(-90.0, 10 * Math.Log10(sum / (fft * fft / 16.0) + 1e-20));
                });
                Console.WriteLine($"{t,6:0.000}" + string.Join("", row.Select(v => $"{v,6:0}")));
            }
            return 0;
        }

        public static int Run(string[] args)
        {
            if (args.Length < 1) { Console.WriteLine("inspect <file> [out-prefix] [t0 t1]"); return 2; }
            Sound s = Compare.Prepare(AudioIO.Read(args[0]));
            string prefix = args.Length > 1 ? args[1] : Path.ChangeExtension(args[0], null);
            float t0 = args.Length > 3 ? float.Parse(args[2], Inv) : 0f, t1 = args.Length > 3 ? float.Parse(args[3], Inv) : Math.Min(s.Duration, 2f);
            var sb = new StringBuilder();
            sb.AppendLine($"{Path.GetFileName(args[0])}: {s.Duration:0.00} s after the onset (levelled: loudest 50 ms = -15 dBFS)");

            List<Transient> tr = Transients(s);
            sb.AppendLine($"transients ({tr.Count}): time  level(dB)  jump(dB)  decay to -15 dB (ms)  centroid");
            foreach (Transient t in tr)
                sb.AppendLine($"  {t.Time,6:0.000}  {t.Level,6:0}  {t.Jump,5:0}  {t.Decay,6:0.0}  {Compare.Hz(t.Centroid)}");

            sb.AppendLine("impulsiveness per 25 ms: time  rms(dB)  crest(full)  crest(>2 kHz)  transients");
            Sound hi = s.HighPass(2000f).HighPass(2000f);
            int w = Sound.Rate / 40;
            for (int i = 0; i + w <= s.Length && i / (float)Sound.Rate < 2f; i += w)
            {
                float rms = Compare.Rms(s.S, i, w), rmsH = Compare.Rms(hi.S, i, w);
                if (Compare.Db(rms) < -60) continue;
                float peak = 0, peakH = 0;
                for (int k = i; k < i + w; k++) { peak = Math.Max(peak, Math.Abs(s.S[k])); peakH = Math.Max(peakH, Math.Abs(hi.S[k])); }
                float time = i / (float)Sound.Rate;
                int n = tr.Count(t => t.Time >= time && t.Time < time + 0.025f);
                sb.AppendLine($"  {time,5:0.000}  {Compare.Db(rms),5:0}  {peak / Math.Max(1e-9f, rms),5:0.0}  {peakH / Math.Max(1e-9f, rmsH),5:0.0}  {new string('|', n)}");
            }

            sb.AppendLine("periodicity per 50 ms (envelope above 1 kHz): time  best period (ms)  strength (0-1)  2nd period  strength");
            foreach (var p in Periodicity(s, 0.05f))
                sb.AppendLine($"  {p.t,5:0.00}  {p.lag1,5:0.00} ms {p.c1,5:0.00}   {p.lag2,5:0.00} ms {p.c2,5:0.00}");

            sb.AppendLine("resonances (freq, strength above its neighbourhood, from, decay dB/s, lasts):");
            foreach (var r in Resonances(s, 24))
                sb.AppendLine($"  {Compare.Hz(r.f),7}  +{r.prom,3:0}  @{r.start:0.00}s  {r.decay,6:0} dB/s  {r.lasts:0.00}s");
            if (args.Length > 3)
            {
                // Resonances of the window alone (shorter FFT: the window may be short), times relative to the window.
                int i0 = Math.Max(0, (int)(t0 * Sound.Rate)), i1 = Math.Min(s.Length, (int)(t1 * Sound.Rate));
                var seg = new Sound(s.S.Skip(i0).Take(i1 - i0).ToArray());
                sb.AppendLine($"resonances in {t0:0.000}-{t1:0.000} s (2048-point FFT, times from the window start):");
                foreach (var r in Resonances(seg, 16, 2048, 128))
                    sb.AppendLine($"  {Compare.Hz(r.f),7}  +{r.prom,3:0}  @{r.start:0.000}s  {r.decay,6:0} dB/s  {r.lasts:0.000}s");
            }
            Console.Write(sb.ToString());

            ZoomWave(s, t0, t1).SavePng(prefix + ".wave.png");
            ShortSpectrogram(s, t0, t1).SavePng(prefix + ".clicks.png");
            Console.WriteLine($"images: {Path.GetFullPath(prefix + ".wave.png")}, {Path.GetFullPath(prefix + ".clicks.png")}");
            return 0;
        }

        /// <summary>
        /// Clicks and knocks: 1 ms energy frames (0.25 ms hop) of the signal above 1 kHz that jump at least 9 dB over the
        /// previous 10 ms and lie within 50 dB of the loudest frame; at least 4 ms apart.
        /// </summary>
        public static List<Transient> Transients(Sound s)
        {
            Sound hp = s.HighPass(1000f).HighPass(1000f);
            int win = Sound.Rate / 1000, hop = win / 4;
            int n = (hp.Length - win) / hop;
            var e = new float[Math.Max(0, n)];
            for (int f = 0; f < n; f++) e[f] = Compare.Rms(hp.S, f * hop, win);
            float max = e.Length > 0 ? e.Max() : 1f;
            var list = new List<Transient>();
            float last = -1f;
            for (int f = 44; f < n - 4; f++)
            {
                double prev = 0;
                for (int k = f - 44; k < f - 4; k++) prev += e[k] * e[k];
                prev = Math.Sqrt(prev / 40);
                if (e[f] < e[f - 1] || e[f] < e[f + 1] || e[f] < e[f + 2]) continue;
                float jump = Compare.Db(e[f]) - Compare.Db((float)prev);
                float level = Compare.Db(e[f]) - Compare.Db(max);
                float time = f * hop / (float)Sound.Rate;
                if (jump < 9 || level < -50 || time - last < 0.004f) continue;
                int g = f;
                while (g + 1 < n && Compare.Db(e[g + 1]) > Compare.Db(e[f]) - 15) g++;
                list.Add(new Transient { Time = time, Level = level, Jump = jump, Decay = (g - f) * hop * 1000f / Sound.Rate, Centroid = Centroid(s, f * hop) });
                last = time;
            }
            return list;
        }

        /// <summary>
        /// Repetition in the fine structure (ratchets, gear teeth, buzzing): the envelope of the signal above 1 kHz (rectified,
        /// smoothed over 0.25 ms, sampled at 8 kHz) is autocorrelated over lags of 1-25 ms in each window; the two strongest
        /// separate peaks. Noise gives strengths below about 0.2; a ratchet or buzz 0.4 and more.
        /// </summary>
        public static List<(float t, float lag1, float c1, float lag2, float c2)> Periodicity(Sound s, float window)
        {
            Sound hp = s.HighPass(1000f).HighPass(1000f);
            const int rate = 8000;
            int step = Sound.Rate / rate;
            int n = hp.Length / step;
            var env = new float[n];
            float a = 0f;
            for (int i = 0; i < n; i++)
            {
                float m = 0;
                for (int k = 0; k < step; k++) m = Math.Max(m, Math.Abs(hp.S[i * step + k]));
                a += (m - a) * 0.6f;
                env[i] = a;
            }
            // Detrend: subtract a 6 ms moving average, so only the fast fluctuation (clicks, teeth) remains.
            int half = 24;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                double m = 0; int c = 0;
                for (int k = Math.Max(0, i - half); k <= Math.Min(n - 1, i + half); k++) { m += env[k]; c++; }
                d[i] = env[i] - (float)(m / c);
            }
            var result = new List<(float, float, float, float, float)>();
            int w = (int)(window * rate);
            for (int start = 0; start + w + 200 < n && start / (float)rate < 2f; start += w)
            {
                double level = 0;
                for (int i = start; i < start + w; i++) level += env[i];
                if (Compare.Db((float)(level / w)) < -55) continue;
                var corr = new double[202];
                for (int lag = 4; lag <= 201; lag++)
                {
                    double c = 0, e0 = 0, e1 = 0;
                    for (int i = start; i < start + w; i++) { c += d[i] * d[i + lag]; e0 += d[i] * d[i]; e1 += d[i + lag] * d[i + lag]; }
                    corr[lag] = e0 > 0 && e1 > 0 ? c / Math.Sqrt(e0 * e1) : 0;
                }
                int best = 0, second = 0;
                for (int lag = 8; lag <= 200; lag++)
                {
                    if (corr[lag] < corr[lag - 1] || corr[lag] < corr[lag + 1] || corr[lag] <= 0) continue;
                    if (best == 0 || corr[lag] > corr[best]) { if (best != 0 && Math.Abs(lag - best) > 4) second = best; best = lag; }
                    else if ((second == 0 || corr[lag] > corr[second]) && Math.Abs(lag - best) > 4) second = lag;
                }
                result.Add((start / (float)rate, best * 1000f / rate, (float)(best > 0 ? corr[best] : 0), second * 1000f / rate, (float)(second > 0 ? corr[second] : 0)));
            }
            return result;
        }

        private static float Centroid(Sound s, int start)
        {
            const int n = 512;
            var re = new double[n]; var im = new double[n];
            for (int i = 0; i < n; i++) { int j = start + i - 32; re[i] = j >= 0 && j < s.Length ? s.S[j] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (n - 1))) : 0; }
            Compare.FFT(re, im);
            double num = 0, den = 0;
            for (int k = 1; k < n / 2; k++) { double m = re[k] * re[k] + im[k] * im[k]; num += m * k; den += m; }
            return den > 0 ? (float)(num / den * Sound.Rate / n) : 0f;
        }

        /// <summary>
        /// Spectral lines from an 8192-point STFT (hop 512): bins well above the median of their ±60-bin neighbourhood at
        /// their loudest frame; their decay is a line fitted to the dB values after that frame while they stay above the floor.
        /// </summary>
        public static List<(float f, float prom, float start, float decay, float lasts)> Resonances(Sound s, int count, int fft = 8192, int hop = 512)
        {
            int frames = Math.Max(1, (s.Length - fft / 2) / hop);
            var win = new double[fft];
            for (int i = 0; i < fft; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (fft - 1));
            var spec = new float[frames][];
            for (int f = 0; f < frames; f++)
            {
                var re = new double[fft]; var im = new double[fft];
                for (int i = 0; i < fft; i++) { int j = f * hop - fft / 2 + i; re[i] = j >= 0 && j < s.Length ? s.S[j] * win[i] : 0; }
                Compare.FFT(re, im);
                spec[f] = new float[fft / 2];
                for (int k = 0; k < fft / 2; k++) spec[f][k] = Compare.Db((float)(Math.Sqrt(re[k] * re[k] + im[k] * im[k]) / (fft / 4)));
            }
            int bins = fft / 2, nbh = Math.Max(8, 60 * fft / 8192);
            var prom = new float[bins]; var at = new int[bins];
            for (int f = 0; f < frames; f++)
                for (int k = nbh; k < bins - nbh; k++)
                {
                    float[] m = spec[f];
                    if (m[k] < -80 || m[k] < m[k - 1] || m[k] < m[k + 1]) continue;
                    var nb = new float[2 * nbh + 1];
                    Array.Copy(m, k - nbh, nb, 0, nb.Length);
                    Array.Sort(nb);
                    float p = m[k] - nb[nbh];
                    if (p > prom[k]) { prom[k] = p; at[k] = f; }
                }
            var result = new List<(float, float, float, float, float)>();
            foreach (int k in Enumerable.Range(0, bins).Where(k => prom[k] > 12).OrderByDescending(k => prom[k]))
            {
                float freq = k * (float)Sound.Rate / fft;
                if (result.Any(r => Math.Abs(r.Item1 - freq) / freq < 0.01f)) continue;
                int f0 = at[k];
                float peak = spec[f0][k];
                var xs = new List<double>(); var ys = new List<double>();
                for (int f = f0; f < frames && spec[f][k] > peak - 40 && spec[f][k] > -95; f++) { xs.Add(f * hop / (double)Sound.Rate); ys.Add(spec[f][k]); }
                double slope = 0;
                if (xs.Count >= 3)
                {
                    double mx = xs.Average(), my = ys.Average(), num = 0, den = 0;
                    for (int i = 0; i < xs.Count; i++) { num += (xs[i] - mx) * (ys[i] - my); den += (xs[i] - mx) * (xs[i] - mx); }
                    slope = den > 0 ? num / den : 0;
                }
                result.Add((freq, prom[k], f0 * hop / (float)Sound.Rate, (float)slope, xs.Count * hop / (float)Sound.Rate));
                if (result.Count >= count) break;
            }
            return result;
        }

        private static Image ZoomWave(Sound s, float t0, float t1)
        {
            const int width = 2400, height = 300;
            var img = new Image(width, height, Compare.Rgb(20, 20, 24));
            int mid = height / 2;
            for (int x = 0; x < width; x++)
            {
                int i0 = (int)((t0 + (t1 - t0) * x / width) * Sound.Rate), i1 = (int)((t0 + (t1 - t0) * (x + 1) / width) * Sound.Rate);
                float lo = 0, hi = 0;
                for (int i = i0; i <= i1 && i < s.Length; i++) { if (i < 0) continue; lo = Math.Min(lo, s.S[i]); hi = Math.Max(hi, s.S[i]); }
                for (int y = (int)(mid - hi * mid * 1.4f); y <= (int)(mid - lo * mid * 1.4f); y++) img.Set(x, y, Compare.Rgb(90, 170, 230));
                img.Set(x, mid, Compare.Rgb(70, 70, 80));
            }
            for (float t = (float)Math.Ceiling(t0 * 100) / 100f; t < t1; t += 0.01f)
            {
                int x = (int)((t - t0) / (t1 - t0) * width);
                bool tenth = Math.Abs(t * 10 - Math.Round(t * 10)) < 0.001f;
                for (int y = 0; y < height; y += tenth ? 1 : 4) img.Set(x, y, tenth ? Compare.Rgb(120, 120, 130) : Compare.Rgb(55, 55, 62));
            }
            return img;
        }

        private static Image ShortSpectrogram(Sound s, float t0, float t1)
        {
            const int fft = 256, width = 2400, height = 400;
            var img = new Image(width, height, Compare.Rgb(20, 20, 24));
            var win = new double[fft];
            for (int i = 0; i < fft; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (fft - 1));
            for (int x = 0; x < width; x++)
            {
                int c = (int)((t0 + (t1 - t0) * x / width) * Sound.Rate);
                var re = new double[fft]; var im = new double[fft];
                for (int i = 0; i < fft; i++) { int j = c - fft / 2 + i; re[i] = j >= 0 && j < s.Length ? s.S[j] * win[i] : 0; }
                Compare.FFT(re, im);
                for (int y = 0; y < height; y++)
                {
                    double f = 200 * Math.Pow(20000 / 200.0, (height - 1 - y) / (double)(height - 1));
                    int k = Math.Clamp((int)Math.Round(f * fft / Sound.Rate), 1, fft / 2 - 1);
                    float db = Compare.Db((float)(Math.Sqrt(re[k] * re[k] + im[k] * im[k]) / (fft / 4)));
                    img.Set(x, y, Compare.Heat((db + 80f) / 80f));
                }
            }
            for (float t = (float)Math.Ceiling(t0 * 100) / 100f; t < t1; t += 0.01f)
            {
                int x = (int)((t - t0) / (t1 - t0) * width);
                for (int y = 0; y < height; y += 6) img.Set(x, y, Compare.Rgb(90, 90, 100));
            }
            return img;
        }
    }
}
