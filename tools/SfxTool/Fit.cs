using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SfxTool
{
    /// <summary>
    /// Named parameters of a fitted sound model. The model reads each one with <see cref="P"/> (name, default, step,
    /// range); the first render registers them, so the fitter knows what it may move. Values come from a generated
    /// table (Recipes/Fitted/*.cs); missing ones fall back to the default written in the model.
    /// </summary>
    public sealed class Params
    {
        public sealed class Info { public string Name; public double Default, Step, Min, Max; }

        public readonly Dictionary<string, double> Values;
        public readonly List<Info> Infos = new List<Info>();
        private readonly HashSet<string> seen = new HashSet<string>();

        public Params(IDictionary<string, double> values = null) =>
            Values = values != null ? new Dictionary<string, double>(values) : new Dictionary<string, double>();

        public float P(string name, double def, double step, double min, double max)
        {
            if (seen.Add(name)) Infos.Add(new Info { Name = name, Default = def, Step = step, Min = min, Max = max });
            return (float)Math.Clamp(Values.TryGetValue(name, out double v) ? v : def, min, max);
        }

        public double Get(Info i) => Math.Clamp(Values.TryGetValue(i.Name, out double v) ? v : i.Default, i.Min, i.Max);
    }

    /// <summary>
    /// How close a candidate is to a reference recording, for fitting: both levelled to the same loudest 50 ms (the
    /// reference also trimmed to its onset, the candidate's time base is the reference's), then the mean absolute
    /// difference of (a) 1/3-octave band levels every 10 ms (63 Hz – 16 kHz) and (b) the level every 2 ms in three bands
    /// (below 1 kHz, 1–4 kHz, above 4 kHz), which catches the sharpness of clicks and knocks, and (c) the fine spectrum
    /// (11 Hz bins, 200 Hz – 15.5 kHz) averaged over each segment, which puts partials and resonances on their exact
    /// frequencies (two partials in one 1/3-octave band look the same to (a)). Each term is the RMS of the dB differences
    /// (a few large local errors weigh more than many small ones). In dB; lower is closer.
    /// </summary>
    public sealed class Similarity
    {
        public readonly float Duration;
        private readonly float[][] refBands, refEnv, refFine;
        private readonly (float a, float b)[] segments;
        private static readonly float[] Centres = Enumerable.Range(0, 24).Select(i => 63f * (float)Math.Pow(2, i / 3.0)).Concat(new[] { 15000f }).ToArray();
        public const float EnvWeight = 0.5f, FineWeight = 0.5f;

        public Similarity(Sound reference, float duration, (float a, float b)[] segments, bool trim = true)
        {
            Duration = duration;
            this.segments = segments;
            Sound r = trim ? Compare.Prepare(reference) : Level(reference);
            refBands = Bands(r, duration);
            refEnv = Envelope(r, duration);
            refFine = Fine(r, segments);
        }

        /// <summary>Scales a sound so its loudest 50 ms sits at -15 dBFS (the level <c>compare</c> uses).</summary>
        public static Sound Level(Sound s)
        {
            int w50 = Sound.Rate / 20, w10 = Sound.Rate / 100;
            float loud = 0;
            for (int i = 0; i + w50 < s.Length; i += w10) loud = Math.Max(loud, Compare.Rms(s.S, i, w50));
            return loud > 0 ? s.Gain(0.1778f / loud) : s;
        }

        public double Loss(Sound candidate) => Detail(candidate).total;

        public (double total, double bands, double env, double fine) Detail(Sound candidate)
        {
            Sound c = Level(candidate);
            float[][] cb = Bands(c, Duration), ce = Envelope(c, Duration), cf = Fine(c, segments);
            double fs = 0; int nf = 0;
            for (int g = 0; g < cf.Length; g++)
                for (int k = 0; k < cf[g].Length; k++) { double d = refFine[g][k] - cf[g][k]; fs += d * d; nf++; }
            fs = Math.Sqrt(fs / Math.Max(1, nf));
            double b = 0; int nb = 0;
            for (int f = 0; f < refBands.Length; f++)
                for (int k = 0; k < Centres.Length; k++) { double d = refBands[f][k] - cb[f][k]; b += d * d; nb++; }
            double e = 0; int ne = 0;
            for (int f = 0; f < refEnv.Length; f++)
                for (int k = 0; k < 3; k++) { double d = refEnv[f][k] - ce[f][k]; e += d * d; ne++; }
            // RMS rather than mean: a short, large error at an important moment (a missing click) must outweigh many
            // small differences of the noise texture.
            b = Math.Sqrt(b / Math.Max(1, nb)); e = Math.Sqrt(e / Math.Max(1, ne));
            return (b + EnvWeight * e + FineWeight * fs, b, e, fs);
        }

        /// <summary>Candidate minus reference per 1/3-octave band, every 10 ms frame from t0 to t1 (for reading a residual).</summary>
        public void PrintDiff(Sound candidate, float t0, float t1)
        {
            float[][] cb = Bands(Level(candidate), Duration);
            Console.WriteLine("  time  " + string.Join("", Centres.Select(c => $"{(c >= 1000 ? (c / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "k" : c.ToString("0")),5}")));
            for (int f = (int)(t0 * 100); f < (int)(t1 * 100) && f < cb.Length; f++)
                Console.WriteLine($"{f / 100f,6:0.00} " + string.Join("", cb[f].Select((v, k) => $"{v - refBands[f][k],5:0}")) + $"   ref max {refBands[f].Max():0}");
        }

        /// <summary>A part's own band levels per 10 ms frame, levelled by the full candidate's gain.</summary>
        public void PrintLevels(Sound part, Sound full, float t0, float t1)
        {
            float g = Level(full).S.Zip(full.S, (a, b) => Math.Abs(b) > 1e-6f ? a / b : 0f).FirstOrDefault(x => x != 0f);
            float[][] pb = Bands(part.Gain(g), Duration);
            Console.WriteLine("  time  " + string.Join("", Centres.Select(c => $"{(c >= 1000 ? (c / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "k" : c.ToString("0")),5}")));
            for (int f = (int)(t0 * 100); f < (int)(t1 * 100) && f < pb.Length; f++)
                Console.WriteLine($"{f / 100f,6:0.00} " + string.Join("", pb[f].Select(v => $"{v,5:0}")));
            Console.WriteLine("  ref   " + string.Join("", refBands[(int)(t0 * 100)].Select(v => $"{v,5:0}")) + "   (reference at t0)");
        }

        /// <summary>RMS of the band and envelope differences per 50 ms window: where a candidate departs from the reference.</summary>
        public List<(float t, double bands, double env)> Profile(Sound candidate)
        {
            Sound c = Level(candidate);
            float[][] cb = Bands(c, Duration), ce = Envelope(c, Duration);
            var list = new List<(float, double, double)>();
            for (float t = 0; t + 0.05f <= Duration; t += 0.05f)
            {
                double b = 0, e = 0; int nb = 0, ne = 0;
                for (int f = (int)(t * 100); f < (int)((t + 0.05f) * 100) && f < cb.Length; f++)
                    for (int k = 0; k < Centres.Length; k++) { double d = refBands[f][k] - cb[f][k]; b += d * d; nb++; }
                for (int f = (int)(t * 500); f < (int)((t + 0.05f) * 500) && f < ce.Length; f++)
                    for (int k = 0; k < 3; k++) { double d = refEnv[f][k] - ce[f][k]; e += d * d; ne++; }
                list.Add((t, Math.Sqrt(b / Math.Max(1, nb)), Math.Sqrt(e / Math.Max(1, ne))));
            }
            return list;
        }

        /// <summary>Average power spectrum per segment (4096-point FFT, 1024 hop), smoothed over ±1 bin, 200 Hz – 15.5 kHz, in dB.</summary>
        private static float[][] Fine(Sound s, (float a, float b)[] segments)
        {
            const int fft = 4096, hop = 1024;
            int b0 = 200 * fft / Sound.Rate, b1 = 15500 * fft / Sound.Rate;
            var win = new double[fft];
            for (int i = 0; i < fft; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (fft - 1));
            var result = new float[segments.Length][];
            for (int g = 0; g < segments.Length; g++)
            {
                var pow = new double[fft / 2];
                int frames = 0;
                for (int start = (int)(segments[g].a * Sound.Rate); start + fft <= (int)(segments[g].b * Sound.Rate) + fft / 2; start += hop)
                {
                    var re = new double[fft]; var im = new double[fft];
                    for (int i = 0; i < fft; i++) { int j = start - fft / 4 + i; re[i] = j >= 0 && j < s.Length ? s.S[j] * win[i] : 0; }
                    Dsp.Fft(re, im);
                    for (int k = 0; k < fft / 2; k++) pow[k] += re[k] * re[k] + im[k] * im[k];
                    frames++;
                }
                var row = new float[b1 - b0];
                for (int k = b0; k < b1; k++)
                    row[k - b0] = Math.Max(-95f, 10f * (float)Math.Log10((pow[k - 1] + pow[k] + pow[k + 1]) / (3.0 * Math.Max(1, frames) * fft * fft / 16.0) + 1e-20));
                result[g] = row;
            }
            return result;
        }

        private static float[][] Bands(Sound s, float duration)
        {
            const int fft = 1024, hop = 441;
            int frames = (int)(duration * Sound.Rate / hop);
            var win = new double[fft];
            for (int i = 0; i < fft; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (fft - 1));
            var result = new float[frames][];
            for (int f = 0; f < frames; f++)
            {
                var re = new double[fft]; var im = new double[fft];
                for (int i = 0; i < fft; i++) { int j = f * hop - fft / 2 + i; re[i] = j >= 0 && j < s.Length ? s.S[j] * win[i] : 0; }
                Dsp.Fft(re, im);
                var row = new float[Centres.Length];
                for (int k = 0; k < Centres.Length; k++)
                {
                    float lo = k == Centres.Length - 1 ? 14100f : Centres[k] / 1.122f, hi = k == Centres.Length - 1 ? 16000f : Centres[k] * 1.122f;
                    int b0 = Math.Max(1, (int)(lo * fft / Sound.Rate)), b1 = Math.Max(b0, (int)(hi * fft / Sound.Rate));
                    double sum = 0;
                    for (int bin = b0; bin <= b1; bin++) sum += re[bin] * re[bin] + im[bin] * im[bin];
                    row[k] = Math.Max(-85f, 10f * (float)Math.Log10(sum / (fft * fft / 16.0) + 1e-20));
                }
                result[f] = row;
            }
            return result;
        }

        private static float[][] Envelope(Sound s, float duration)
        {
            Sound[] bands = { s.HighPass(150f).LowPass(1000f).LowPass(1000f), s.HighPass(1000f).HighPass(1000f).LowPass(4000f).LowPass(4000f), s.HighPass(4000f).HighPass(4000f) };
            int w = Sound.Rate / 500, frames = (int)(duration * Sound.Rate / w);
            var result = new float[frames][];
            for (int f = 0; f < frames; f++)
            {
                result[f] = new float[3];
                for (int k = 0; k < 3; k++)
                    result[f][k] = f * w + w <= bands[k].Length ? Math.Max(-75f, Compare.Db(Compare.Rms(bands[k].S, f * w, w))) : -75f;
            }
            return result;
        }
    }

    /// <summary>
    /// fit &lt;model&gt; &lt;reference&gt; [rounds] [name-prefix]: coordinate descent of a model's parameters against a recording
    /// (docs/sound-workflow.md, "Fitting a model"). Every parameter is nudged up and down by its step; changes that lower
    /// the <see cref="Similarity"/> loss are kept (and pushed further), and the steps halve when a round finds nothing.
    /// The values are written to Recipes/Fitted/&lt;Model&gt;Fit.cs after every round; rebuild SfxTool to render with them.
    /// </summary>
    public static class Fitter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public sealed class Model
        {
            public string Name, ClassName;
            public float Duration;
            public (float a, float b)[] Segments;
            public Func<Params, Sound> Render;
            public IDictionary<string, double> Fitted;
        }

        public static readonly Dictionary<string, Model> Models = new Dictionary<string, Model>
        {
            ["register"] = new Model { Name = "register", ClassName = "Register", Duration = 1.33f,
                Segments = new[] { (0.02f, 0.3f), (0.3f, 0.45f), (0.45f, 0.7f), (0.7f, 1.3f) }, Render = p => Recipes.RegisterModel(p, false), Fitted = Fitted.Register },
            ["door"] = new Model { Name = "door", ClassName = "Door", Duration = 1.0f,
                Segments = new[] { (0.015f, 0.21f), (0.21f, 0.34f), (0.34f, 1.0f) }, Render = p => Recipes.DoorModel(p, false), Fitted = Fitted.Door },
            ["cloth"] = new Model { Name = "cloth", ClassName = "Cloth", Duration = 1.63f,
                Segments = new[] { (0.03f, 0.25f), (0.25f, 0.95f), (0.95f, 1.63f) }, Render = p => Recipes.ClothModel(p, false), Fitted = Fitted.Cloth },
        };

        public static int Run(string[] args)
        {
            if (args.Length < 2 || !Models.TryGetValue(args[0], out Model model))
            {
                Console.WriteLine("fit <" + string.Join("|", Models.Keys) + "> <reference> [rounds] [name-prefix]");
                return 2;
            }
            if (args.Length > 2 && args[2] == "floor")
            {
                // The texture floor: the fitted model against a second take of itself with other noise. A fit whose loss
                // against the recording approaches this is as close as noise allows.
                var pf = new Params(model.Fitted);
                Sound take1 = model.Render(pf);
                Recipes.SeedOffset = 1000;
                Sound take2 = model.Render(pf);
                Recipes.SeedOffset = 0;
                var selfSim = new Similarity(take1, model.Duration, model.Segments, trim: false);
                var recSim = new Similarity(AudioIO.Read(args[1]), model.Duration, model.Segments);
                var f = selfSim.Detail(take2); var r = recSim.Detail(take1);
                Console.WriteLine($"against the recording: {r.total:0.000} (bands {r.bands:0.00}, envelope {r.env:0.00}, fine {r.fine:0.00})");
                Console.WriteLine($"take 1 vs take 2 (floor): {f.total:0.000} (bands {f.bands:0.00}, envelope {f.env:0.00}, fine {f.fine:0.00})");
                Console.WriteLine("per 50 ms: time | recording: bands env | floor: bands env | excess");
                var pr = recSim.Profile(take1); var pf2 = selfSim.Profile(take2);
                for (int i = 0; i < pr.Count; i++)
                {
                    double excess = (pr[i].bands + 0.5 * pr[i].env) - (pf2[i].bands + 0.5 * pf2[i].env);
                    Console.WriteLine($"  {pr[i].t,5:0.00} | {pr[i].bands,5:0.0} {pr[i].env,5:0.0} | {pf2[i].bands,5:0.0} {pf2[i].env,5:0.0} | {excess,5:0.0} {new string('#', Math.Max(0, (int)(excess * 2)))}");
                }
                return 0;
            }
            if (args.Length > 5 && args[2] == "solo")
            {
                // One part alone: every level (".l<n>") and mode level (".db") outside the prefix is muted.
                var ps = new Params(model.Fitted);
                model.Render(ps);
                foreach (Params.Info i in ps.Infos)
                {
                    string last = i.Name.Substring(i.Name.LastIndexOf('.') + 1);
                    bool level = last == "db" || (last.Length > 1 && last[0] == 'l' && char.IsDigit(last[1]));
                    if (level && !i.Name.StartsWith(args[3])) ps.Values[i.Name] = i.Min;
                }
                Sound solo = model.Render(ps);
                var soloSim = new Similarity(AudioIO.Read(args[1]), model.Duration, model.Segments);
                Console.WriteLine($"solo {args[3]} (levels relative to the full model's loudest 50 ms):");
                soloSim.PrintLevels(solo, model.Render(new Params(model.Fitted)), float.Parse(args[4], Inv), float.Parse(args[5], Inv));
                return 0;
            }
            if (args.Length > 3 && args[2] == "render")
            {
                // The fitted model alone, for listening and for compare/inspect against the recording.
                AudioIO.WriteWav(args[3], Similarity.Level(model.Render(new Params(model.Fitted))));
                Console.WriteLine($"{model.Name} written to {Path.GetFullPath(args[3])}");
                return 0;
            }
            if (args.Length > 4 && args[2] == "diff")
            {
                new Similarity(AudioIO.Read(args[1]), model.Duration, model.Segments).PrintDiff(model.Render(new Params(model.Fitted)), float.Parse(args[3], Inv), float.Parse(args[4], Inv));
                return 0;
            }
            int rounds = args.Length > 2 ? int.Parse(args[2], Inv) : 6;
            string only = args.Length > 3 ? args[3] : "";
            var sim = new Similarity(AudioIO.Read(args[1]), model.Duration, model.Segments);
            var p = new Params(model.Fitted);
            double best = sim.Loss(model.Render(p));
            var steps = p.Infos.ToDictionary(i => i.Name, i => i.Step);
            var rng = new Random(1);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var d0 = sim.Detail(model.Render(p));
            Console.WriteLine($"{model.Name}: {p.Infos.Count} parameters, loss {best:0.000} (bands {d0.bands:0.00}, envelope {d0.env:0.00}, fine {d0.fine:0.00})");
            for (int round = 1; round <= rounds; round++)
            {
                int accepted = 0;
                foreach (Params.Info info in p.Infos.Where(i => i.Name.StartsWith(only)).OrderBy(_ => rng.Next()).ToList())
                {
                    double step = steps[info.Name];
                    foreach (int dir in new[] { 1, -1 })
                    {
                        bool moved = false;
                        for (int push = 0; push < 4; push++)
                        {
                            double old = p.Get(info), v = Math.Clamp(old + dir * step, info.Min, info.Max);
                            if (Math.Abs(v - old) < 1e-9) break;
                            p.Values[info.Name] = v;
                            double loss = sim.Loss(model.Render(p));
                            if (loss < best - 1e-4) { best = loss; moved = true; accepted++; }
                            else { p.Values[info.Name] = old; break; }
                        }
                        if (moved) break;
                    }
                }
                var d = sim.Detail(model.Render(p));
                Console.WriteLine($"round {round}: loss {best:0.000} (bands {d.bands:0.00}, envelope {d.env:0.00}, fine {d.fine:0.00}), {accepted} changes, {watch.Elapsed.TotalSeconds:0}s");
                Write(model, p, best);
                if (accepted == 0)
                    foreach (string k in steps.Keys.ToList()) steps[k] *= 0.5;
            }
            return 0;
        }

        private static void Write(Model model, Params p, double loss)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace SfxTool");
            sb.AppendLine("{");
            sb.AppendLine("    public static partial class Fitted");
            sb.AppendLine("    {");
            sb.AppendLine($"        /// <summary>Generated by <c>SfxTool fit {model.Name}</c> (loss {loss.ToString("0.000", Inv)} dB). Re-run the fitter instead of editing.</summary>");
            sb.AppendLine($"        public static readonly Dictionary<string, double> {model.ClassName} = new Dictionary<string, double>");
            sb.AppendLine("        {");
            foreach (Params.Info i in p.Infos)
                sb.AppendLine($"            [\"{i.Name}\"] = {p.Get(i).ToString("0.####", Inv)},");
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            Directory.CreateDirectory(Path.Combine("Recipes", "Fitted"));
            File.WriteAllText(Path.Combine("Recipes", "Fitted", model.ClassName + "Fit.cs"), sb.ToString());
        }
    }
}
