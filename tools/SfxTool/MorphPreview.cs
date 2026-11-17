using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SfxTool
{
    /// <summary>
    /// The Particle Accelerator's transformation as one sound file per direction, mixed the way the game plays it
    /// (<c>MorphPlayer</c>): the step timing, one-shots and loops come from the morph file (VoxTool), the pump fades in
    /// under its start-up, strains while a piece moves (pitch and volume follow the step's load) and stops at the end.
    /// Core → capacitor starts with the barrel turning back (accel_servo). No spatial falloff, no random pitch.
    /// <c>morphpreview &lt;modelsDir&gt; &lt;soundsDir&gt; &lt;outDir&gt;</c> writes accelerator_to_core.wav and
    /// accelerator_to_capacitor.wav at the file's speed, and *_slow.wav at the slow-motion review speed (TimeScale 2).
    /// </summary>
    public static class MorphPreview
    {
        private sealed class Step
        {
            public float Seconds, Load;
            public readonly List<(float a, float b)> Tweens = new List<(float, float)>();
            public readonly List<(bool forward, float at, string name, float vol)> Sounds = new List<(bool, float, string, float)>();
            public readonly List<(string name, float a, float b, float vol)> Loops = new List<(string, float, float, float)>();
        }

        private const string Folder = "ascensions/particle_accelerator/";
        private const float ControlRate = 200f;

        public static int Run(string[] args)
        {
            if (args.Length < 3) { Console.WriteLine("SfxTool morphpreview <modelsDir> <soundsDir> <outDir>"); return 1; }
            string morph = Path.Combine(args[0], "ascensions", "particle", "accelerator_morph.txt");
            var (steps, timeScale) = Parse(File.ReadAllLines(morph));
            Directory.CreateDirectory(args[2]);
            foreach (bool slow in new[] { false, true })
                foreach (bool toCore in new[] { true, false })
                {
                    float stretch = slow ? 2f / timeScale : 1f;
                    Sound s = Render(steps, timeScale * stretch, stretch, toCore, args[1]);
                    string name = $"accelerator_to_{(toCore ? "core" : "capacitor")}{(slow ? "_slow" : "")}";
                    string wav = Path.Combine(args[2], name + ".wav");
                    AudioIO.WriteWav(wav, s);
                    Analysis a = Analysis.Of(s, name);
                    a.Render(s).SavePng(Path.ChangeExtension(wav, ".png"));
                    File.WriteAllText(Path.ChangeExtension(wav, ".txt"), a.Report());
                    Console.Write(a.Report());
                }
            Console.WriteLine($"previews (file speed: TimeScale {timeScale.ToString(CultureInfo.InvariantCulture)}; *_slow: TimeScale 2) -> {Path.GetFullPath(args[2])}");
            return 0;
        }

        private static (List<Step>, float) Parse(string[] lines)
        {
            var steps = new List<Step>();
            float timeScale = 1f;
            Step step = null;
            int skip = 0;
            float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);
            foreach (string raw in lines)
            {
                string line = raw.Contains("#") ? raw.Substring(0, raw.IndexOf('#')).Trim() : raw.Trim();
                if (line.Length == 0) continue;
                if (skip > 0) { skip--; continue; }
                string[] t = line.Split(' ');
                switch (t[0])
                {
                    case "timescale": timeScale = F(t[1]); break;
                    case "piece": skip = int.Parse(t[2], CultureInfo.InvariantCulture); break;
                    case "wave": skip = int.Parse(t[2], CultureInfo.InvariantCulture); break;
                    case "step": step = new Step { Seconds = F(t[2]), Load = F(t[3]) }; steps.Add(step); break;
                    case "tween": step.Tweens.Add((F(t[2]), F(t[3]))); break;
                    case "snd": step.Sounds.Add((t[1] == "f", F(t[2]), t[3], F(t[4]))); break;
                    case "loop": step.Loops.Add((t[1], F(t[2]), F(t[3]), F(t[4]))); break;
                }
            }
            return (steps, timeScale);
        }

        /// <summary>A sound by name; a random one of its variants (name_1, name_2, ...) if there are several.</summary>
        private static Sound Load(string soundsDir, string name, Random rng)
        {
            string path = Path.Combine(soundsDir, name.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path + ".wav")) return AudioIO.ReadWav(path + ".wav");
            var variants = new List<string>();
            for (int i = 1; File.Exists($"{path}_{i}.wav"); i++) variants.Add($"{path}_{i}.wav");
            if (variants.Count == 0) throw new FileNotFoundException("no sound " + name);
            return AudioIO.ReadWav(variants[rng.Next(variants.Count)]);
        }

        private static Sound Render(List<Step> steps, float timeScale, float stretch, bool toCore, string soundsDir)
        {
            var rng = new Random(toCore ? 1 : 2);
            float align = toCore ? 0f : 0.25f * timeScale;
            float total = align + steps.Sum(s => s.Seconds) * stretch;
            var mix = new float[Sound.Samples(total + 2.5f)];
            void Shot(string name, float at, float vol)
            {
                Sound s = Load(soundsDir, name, rng);
                int o = Sound.Samples(at);
                for (int i = 0; i < s.Length && o + i < mix.Length; i++) mix[o + i] += s.S[i] * vol;
            }
            if (!toCore) Shot(Folder + "accel_servo", 0f, 0.6f);

            // The pump: a loop read at a varying rate (pitch) and volume, updated at the control rate like the game's frames.
            Sound pumpLoop = Load(soundsDir, Folder + "accel_pump_loop", rng);
            Shot(Folder + "accel_pump_start", align, 0.9f);
            double pumpPos = 0;
            float pitch = 1f, vol = 0f, age = 0f, dt = 1f / ControlRate;
            int perTick = Sound.Samples(dt);

            float t = align;
            var order = toCore ? steps : Enumerable.Reverse(steps).ToList();
            foreach (Step st in order)
            {
                float len = st.Seconds * stretch;
                // One-shots at their time in the playing direction.
                foreach (var (forward, at, name, v) in st.Sounds)
                    if (forward == toCore) Shot(name, t + at * len, v);
                // Loops while their window (in the step's forward time) lasts.
                foreach (var (name, a, b, v) in st.Loops)
                {
                    float from = toCore ? a : 1f - b, to = toCore ? b : 1f - a;
                    Sound l = Load(soundsDir, name, rng);
                    int o = Sound.Samples(t + from * len), n = Sound.Samples((to - from) * len);
                    for (int i = 0; i < n && o + i < mix.Length; i++)
                    {
                        float fade = Math.Min(1f, Math.Min(i, n - i) / (0.01f * Sound.Rate));
                        mix[o + i] += l.S[i % l.Length] * v * fade;
                    }
                }
                // The pump through this step.
                for (float u = 0f; u < 1f; u += dt / len)
                {
                    float tau = toCore ? u : 1f - u;
                    bool moving = st.Tweens.Any(tw => tw.b > tw.a && tau > tw.a && tau < tw.b);
                    float load = moving ? st.Load : 0f;
                    age += dt;
                    float k = 1f - (float)Math.Exp(-dt * 6f), fadeIn = Math.Clamp((age - 0.45f) / 0.35f, 0f, 1f);
                    pitch += (1f + 0.16f * load - pitch) * k;
                    vol += (fadeIn * (0.55f + 0.35f * load) - vol) * k;
                    int o = Sound.Samples(t + u * len);
                    for (int i = 0; i < perTick && o + i < mix.Length; i++)
                    {
                        int j = (int)pumpPos;
                        float frac = (float)(pumpPos - j);
                        float s = pumpLoop.S[j % pumpLoop.Length] * (1f - frac) + pumpLoop.S[(j + 1) % pumpLoop.Length] * frac;
                        mix[o + i] += s * vol;
                        pumpPos += pitch;
                    }
                }
                t += len;
            }
            Shot(Folder + "accel_pump_stop", t, 0.9f);
            Shot(Folder + "accel_vent", t, 0.8f);
            return new Sound(mix).Normalize(-1f);
        }
    }
}
