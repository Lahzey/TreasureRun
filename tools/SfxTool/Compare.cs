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
    /// compare &lt;reference&gt; &lt;candidate&gt; [out.png]: lines a recipe up against a recorded example (docs/sound-workflow.md,
    /// "Matching an example"). Both are trimmed to their onset and levelled to the same loudest 50 ms, then:
    ///  - a picture: per sound a waveform and a fine log-frequency spectrogram (40 Hz – 20 kHz, 4096-point FFT, 5.8 ms
    ///    hop), and below them a difference map (red: the candidate is louder there, blue: quieter, full colour = 20 dB);
    ///  - a table: energy per frequency band every 50 ms, reference | candidate;
    ///  - the tonal peaks of each (frequency, level, when, how long it rings);
    ///  - a score: mean absolute band difference in dB (lower is closer).
    /// </summary>
    public static class Compare
    {
        private const int Fft = 4096, Hop = 256;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly (float lo, float hi)[] Bands =
            { (40, 200), (200, 500), (500, 1000), (1000, 2000), (2000, 4000), (4000, 8000), (8000, 16000), (16000, 22000) };

        public static int Run(string[] args)
        {
            if (args.Length < 2) { Console.WriteLine("compare <reference> <candidate> [out.png] [t0 t1 ...: segments for the spectrum shape]"); return 2; }
            Sound a = Prepare(AudioIO.Read(args[0])), b = Prepare(AudioIO.Read(args[1]));
            float dur = Math.Min(2.5f, Math.Max(a.Duration, b.Duration));
            float[][] sa = Spectrogram(a, dur), sb = Spectrogram(b, dur);
            string png = args.Length > 2 ? args[2] : Path.ChangeExtension(args[1], ".compare.png");
            Render(a, b, sa, sb, dur).SavePng(png);

            var sb2 = new StringBuilder();
            sb2.AppendLine($"reference {Path.GetFileName(args[0])}  |  candidate {Path.GetFileName(args[1])}  (onset-aligned, loudest 50 ms = -15 dBFS)");
            sb2.AppendLine("band energy (dB) per 50 ms; bands " + string.Join(" ", Bands.Select(x => $"{Hz(x.lo)}-{Hz(x.hi)}")));
            sb2.AppendLine("  time | reference                               | candidate");
            double diff = 0; int cells = 0;
            for (float t = 0; t < dur; t += 0.05f)
            {
                float[] ea = BandEnergy(sa, t), eb = BandEnergy(sb, t);
                if (ea.Max() < -75 && eb.Max() < -75) continue;
                sb2.AppendLine($"{t,6:0.00} | {string.Join(" ", ea.Select(v => $"{v,4:0}"))} | {string.Join(" ", eb.Select(v => $"{v,4:0}"))}");
                for (int k = 0; k < Bands.Length; k++) { diff += Math.Abs(ea[k] - eb[k]); cells++; }
            }
            // Spectrum shape per segment, 1/3 octave: where the candidate's colour (EQ, resonances) differs.
            var segments = new List<(float a, float b)>();
            for (int i = 3; i + 1 < args.Length; i += 2)
                segments.Add((float.Parse(args[i], Inv), float.Parse(args[i + 1], Inv)));
            if (segments.Count == 0) segments.Add((0f, dur));
            var centres = new List<float>();
            for (float f = 50; f < 16000; f *= 1.2599f) centres.Add(f);
            sb2.AppendLine("1/3-octave spectrum, candidate minus reference (dB; both levelled to the segment's total):");
            sb2.AppendLine("        Hz " + string.Join(" ", centres.Select(f => $"{Hz(f),5}")));
            foreach (var (t0, t1) in segments)
            {
                float[] ra = ThirdOctaves(sa, t0, t1, centres), rb = ThirdOctaves(sb, t0, t1, centres);
                float ta = 10f * (float)Math.Log10(ra.Sum(v => Math.Pow(10, v / 10)) + 1e-12), tb = 10f * (float)Math.Log10(rb.Sum(v => Math.Pow(10, v / 10)) + 1e-12);
                sb2.AppendLine($"{t0,4:0.00}-{t1,4:0.00} " + string.Join(" ", ra.Select((v, i) => $"{(rb[i] - tb) - (v - ta),5:0}")));
                sb2.AppendLine($"   ref shape " + string.Join(" ", ra.Select(v => $"{v - ta,5:0}")));
            }
            sb2.AppendLine("tonal peaks (freq, peak dB above its neighbourhood, at, rings for):");
            sb2.AppendLine("  reference: " + string.Join("; ", Peaks(sa).Select(p => $"{Hz(p.f)} +{p.prom:0} @{p.at:0.00}s {p.ring:0.00}s")));
            sb2.AppendLine("  candidate: " + string.Join("; ", Peaks(sb).Select(p => $"{Hz(p.f)} +{p.prom:0} @{p.at:0.00}s {p.ring:0.00}s")));
            sb2.AppendLine($"score (mean |band difference|): {diff / Math.Max(1, cells):0.0} dB");
            sb2.AppendLine($"image: {Path.GetFullPath(png)}");
            Console.Write(sb2.ToString());
            return 0;
        }

        /// <summary>Trims to 20 ms before the onset (10 ms RMS within 35 dB of the loudest) and levels the loudest 50 ms to -15 dBFS.</summary>
        internal static Sound Prepare(Sound s)
        {
            int w50 = Sound.Rate / 20, w10 = Sound.Rate / 100;
            float loud = 0;
            for (int i = 0; i + w50 < s.Length; i += w10) loud = Math.Max(loud, Rms(s.S, i, w50));
            int onset = 0;
            for (int i = 0; i + w10 < s.Length; i += w10 / 2)
                if (Rms(s.S, i, w10) > loud * 0.0178f) { onset = Math.Max(0, i - Sound.Rate / 50); break; }
            float g = loud > 0 ? 0.1778f / loud : 1f;
            var o = new float[s.Length - onset];
            for (int i = 0; i < o.Length; i++) o[i] = s.S[i + onset] * g;
            return new Sound(o);
        }

        private static float[][] Spectrogram(Sound s, float dur)
        {
            int frames = (int)(dur * Sound.Rate / Hop);
            var win = new double[Fft];
            for (int i = 0; i < Fft; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Fft - 1));
            var result = new float[frames][];
            for (int f = 0; f < frames; f++)
            {
                int start = f * Hop - Fft / 2;
                var re = new double[Fft]; var im = new double[Fft];
                for (int i = 0; i < Fft; i++) { int j = start + i; re[i] = j >= 0 && j < s.Length ? s.S[j] * win[i] : 0; }
                FFT(re, im);
                var mag = new float[Fft / 2];
                for (int k = 0; k < Fft / 2; k++) mag[k] = Db((float)(Math.Sqrt(re[k] * re[k] + im[k] * im[k]) / (Fft / 4)));
                result[f] = mag;
            }
            return result;
        }

        private static float[] BandEnergy(float[][] spec, float t)
        {
            var e = new float[Bands.Length];
            int f0 = (int)(t * Sound.Rate / Hop), f1 = Math.Min(spec.Length, (int)((t + 0.05f) * Sound.Rate / Hop));
            for (int k = 0; k < Bands.Length; k++)
            {
                int b0 = (int)(Bands[k].lo * Fft / Sound.Rate), b1 = Math.Min(Fft / 2 - 1, (int)(Bands[k].hi * Fft / Sound.Rate));
                double sum = 0; int n = 0;
                for (int f = f0; f < f1; f++)
                    for (int bin = b0; bin <= b1; bin++) { sum += Math.Pow(10, spec[f][bin] / 10); n++; }
                e[k] = n == 0 ? -90 : Math.Max(-90, 10f * (float)Math.Log10(sum / n + 1e-12) + 10f * (float)Math.Log10(b1 - b0 + 1));
            }
            return e;
        }

        private static float[] ThirdOctaves(float[][] spec, float t0, float t1, List<float> centres)
        {
            int f0 = (int)(t0 * Sound.Rate / Hop), f1 = Math.Min(spec.Length, (int)(t1 * Sound.Rate / Hop));
            var r = new float[centres.Count];
            for (int c = 0; c < centres.Count; c++)
            {
                int b0 = Math.Max(1, (int)(centres[c] / 1.122f * Fft / Sound.Rate)), b1 = Math.Max(b0, (int)(centres[c] * 1.122f * Fft / Sound.Rate));
                double sum = 0;
                for (int f = f0; f < f1; f++) for (int bin = b0; bin <= b1; bin++) sum += Math.Pow(10, spec[f][bin] / 10);
                r[c] = 10f * (float)Math.Log10(sum / Math.Max(1, f1 - f0) + 1e-12);
            }
            return r;
        }

        /// <summary>Spectral lines: bins far above the median of their ±40-bin neighbourhood, merged, strongest first.</summary>
        private static List<(float f, float prom, float at, float ring)> Peaks(float[][] spec)
        {
            int bins = Fft / 2;
            var prom = new float[bins]; var at = new int[bins];
            for (int f = 0; f < spec.Length; f++)
            {
                float[] m = spec[f];
                for (int k = 40; k < bins - 40; k += 1)
                {
                    if (m[k] < -70 || m[k] < m[k - 1] || m[k] < m[k + 1]) continue;
                    float med = Median(m, k - 40, k + 40);
                    float p = m[k] - med;
                    if (p > prom[k]) { prom[k] = p; at[k] = f; }
                }
            }
            var list = new List<(float f, float prom, float at, float ring)>();
            foreach (int k in Enumerable.Range(0, bins).Where(k => prom[k] > 18).OrderByDescending(k => prom[k]))
            {
                float freq = k * (float)Sound.Rate / Fft;
                if (list.Any(p => Math.Abs(p.f - freq) / freq < 0.03f)) continue;
                float peakDb = spec[at[k]][k];
                int end = at[k];
                while (end + 1 < spec.Length && spec[end + 1][k] > peakDb - 25) end++;
                list.Add((freq, prom[k], at[k] * Hop / (float)Sound.Rate, (end - at[k]) * Hop / (float)Sound.Rate));
                if (list.Count >= 8) break;
            }
            return list;
        }

        private static float Median(float[] m, int a, int b)
        {
            var v = new float[b - a + 1];
            Array.Copy(m, a, v, 0, v.Length);
            Array.Sort(v);
            return v[v.Length / 2];
        }

        private static Image Render(Sound a, Sound b, float[][] sa, float[][] sb, float dur)
        {
            const int width = 1400, waveH = 110, specH = 460, gap = 8;
            int h = 2 * (waveH + specH + gap) + specH;
            var img = new Image(width, h, Rgb(20, 20, 24));
            int y = 0;
            foreach (var (s, spec) in new[] { (a, sa), (b, sb) })
            {
                Wave(img, s, y, waveH, dur, width);
                Spec(img, spec, y + waveH, specH, width, (x, f) => Heat((f + 90f) / 90f));
                y += waveH + specH + gap;
            }
            // Difference map: candidate minus reference.
            Spec(img, sb, y, specH, width, (x, f) => 0, (frame, bin) =>
            {
                float d = sb[frame][bin] - sa[frame][bin];
                float strength = Math.Clamp(Math.Max(sb[frame][bin], sa[frame][bin]) + 80f, 0f, 60f) / 60f;
                float v = Math.Clamp(d / 20f, -1f, 1f) * strength;
                return v >= 0 ? Rgb((int)(30 + 225 * v), 30, 30) : Rgb(30, 30, (int)(30 - 225 * v));
            });
            for (float t = 0.1f; t < dur; t += 0.1f)
            {
                int x = (int)(t / dur * width);
                bool second = Math.Abs(t - Math.Round(t)) < 0.001f;
                for (int yy = 0; yy < h; yy += second ? 1 : 3) img.Set(x, yy, second ? Rgb(110, 110, 120) : Rgb(60, 60, 70));
            }
            return img;
        }

        private static void Wave(Image img, Sound s, int top, int height, float dur, int width)
        {
            int mid = top + height / 2;
            for (int x = 0; x < width; x++)
            {
                int i0 = (int)(x * dur / width * Sound.Rate), i1 = (int)((x + 1) * dur / width * Sound.Rate);
                float lo = 0, hi = 0;
                for (int i = i0; i < i1 && i < s.Length; i++) { lo = Math.Min(lo, s.S[i]); hi = Math.Max(hi, s.S[i]); }
                for (int yy = (int)(mid - hi * height / 2); yy <= (int)(mid - lo * height / 2); yy++) img.Set(x, yy, Rgb(90, 170, 230));
                img.Set(x, mid, Rgb(70, 70, 80));
            }
        }

        private static void Spec(Image img, float[][] spec, int top, int height, int width, Func<int, float, uint> heat, Func<int, int, uint> custom = null)
        {
            const double fMin = 40, fMax = 20000;
            for (int x = 0; x < width; x++)
            {
                int frame = Math.Min(spec.Length - 1, x * spec.Length / width);
                for (int yy = 0; yy < height; yy++)
                {
                    double f = fMin * Math.Pow(fMax / fMin, (height - 1 - yy) / (double)(height - 1));
                    int bin = Math.Clamp((int)Math.Round(f * Fft / Sound.Rate), 1, Fft / 2 - 1);
                    img.Set(x, top + yy, custom != null ? custom(frame, bin) : heat(x, spec[frame][bin]));
                }
            }
            foreach (double f in new[] { 100.0, 1000.0, 10000.0 })
            {
                int yy = top + (int)((height - 1) * (1 - Math.Log(f / fMin) / Math.Log(fMax / fMin)));
                for (int x = 0; x < width; x += 4) img.Set(x, yy, Rgb(200, 200, 200));
            }
            foreach (double f in new[] { 500.0, 2000.0, 5000.0 })
            {
                int yy = top + (int)((height - 1) * (1 - Math.Log(f / fMin) / Math.Log(fMax / fMin)));
                for (int x = 0; x < width; x += 12) img.Set(x, yy, Rgb(120, 120, 120));
            }
        }

        internal static float Rms(float[] s, int start, int count)
        {
            double sum = 0;
            for (int i = start; i < start + count && i < s.Length; i++) sum += s[i] * s[i];
            return (float)Math.Sqrt(sum / Math.Max(1, count));
        }

        internal static float Db(float v) => v <= 1e-6f ? -120f : 20f * (float)Math.Log10(v);
        internal static string Hz(float f) => f >= 1000 ? (f / 1000f).ToString("0.0#", Inv) + "k" : f.ToString("0", Inv);
        internal static uint Rgb(int r, int g, int b) => (uint)(r | g << 8 | b << 16 | 255 << 24);

        internal static uint Heat(float v)
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

        internal static void FFT(double[] re, double[] im)
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
                double ang = -2 * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int p = i + k, q = i + k + len / 2;
                        double tr = re[q] * cr - im[q] * ci, ti = re[q] * ci + im[q] * cr;
                        re[q] = re[p] - tr; im[q] = im[p] - ti; re[p] += tr; im[p] += ti;
                        double ncr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = ncr;
                    }
                }
            }
        }
    }
}
