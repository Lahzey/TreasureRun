using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SfxTool
{
    /// <summary>
    /// Sound effects as code, the audio counterpart of VoxTool (docs/ascension-plan.md §1.5).
    /// <list type="bullet">
    /// <item><c>build &lt;soundsDir&gt; [name|all]</c>: renders recipes to WAV, each with an analysis .png and .txt</item>
    /// <item><c>analyze &lt;file.wav|ogg&gt;...</c>: analysis .png next to the file, report on stdout</item>
    /// <item><c>reference &lt;audioDir&gt; &lt;outDir&gt;</c>: analyzes every clip (e.g. vanilla .ogg) into outDir + reference.md</item>
    /// <item><c>play &lt;file&gt;</c>: opens the file in the default player</item>
    /// <item><c>list</c>: lists recipes</item>
    /// </list>
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0) return Usage();
            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "build": return Build(args.Skip(1).ToArray());
                    case "analyze": return Analyze(args.Skip(1).ToArray());
                    case "reference": return Reference(args.Skip(1).ToArray());
                    case "play": Process.Start(new ProcessStartInfo(Path.GetFullPath(args[1])) { UseShellExecute = true }); return 0;
                    case "list": foreach (string n in Recipes.All.Keys.OrderBy(k => k)) Console.WriteLine(n); return 0;
                    default: return Usage();
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return 1;
            }
        }

        private static int Usage()
        {
            Console.WriteLine("SfxTool build <soundsDir> [name|all] | analyze <file>... | reference <audioDir> <outDir> | play <file> | list");
            return 1;
        }

        private static int Build(string[] args)
        {
            if (args.Length < 1) return Usage();
            string dir = args[0], which = args.Length > 1 ? args[1] : "all";
            int n = 0;
            foreach (var kv in Recipes.All.OrderBy(k => k.Key))
            {
                if (which != "all" && !kv.Key.StartsWith(which, StringComparison.OrdinalIgnoreCase)) continue;
                Sound s = kv.Value();
                string wav = Path.Combine(dir, kv.Key.Replace('/', Path.DirectorySeparatorChar) + ".wav");
                AudioIO.WriteWav(wav, s);
                Analysis a = Analysis.Of(s, kv.Key);
                a.Render(s).SavePng(Path.ChangeExtension(wav, ".png"));
                File.WriteAllText(Path.ChangeExtension(wav, ".txt"), a.Report());
                Console.Write(a.Report());
                n++;
            }
            Console.WriteLine($"{n} sound(s) written to {Path.GetFullPath(dir)}");
            return n > 0 ? 0 : 1;
        }

        private static int Analyze(string[] files)
        {
            foreach (string f in files)
            {
                Sound s = AudioIO.Read(f);
                Analysis a = Analysis.Of(s, Path.GetFileName(f));
                string png = Path.ChangeExtension(f, ".png");
                a.Render(s).SavePng(png);
                Console.Write(a.Report());
                Console.WriteLine($"  image: {Path.GetFullPath(png)}");
            }
            return 0;
        }

        private static int Reference(string[] args)
        {
            if (args.Length < 2) return Usage();
            string src = args[0], outDir = args[1];
            Directory.CreateDirectory(outDir);
            var rows = new List<Analysis>();
            foreach (string f in Directory.EnumerateFiles(src).Where(f => f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                                                                         || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f))
            {
                Sound s = AudioIO.Read(f);
                Analysis a = Analysis.Of(s, Path.GetFileNameWithoutExtension(f));
                a.Render(s).SavePng(Path.Combine(outDir, a.Name + ".png"));
                File.WriteAllText(Path.Combine(outDir, a.Name + ".txt"), a.Report());
                rows.Add(a);
            }
            var sb = new StringBuilder();
            sb.AppendLine("# Reference clips").AppendLine().AppendLine(Analysis.TableHeader);
            foreach (Analysis a in rows) sb.AppendLine(a.TableRow());
            File.WriteAllText(Path.Combine(outDir, "reference.md"), sb.ToString());
            Console.Write(sb.ToString());
            return 0;
        }
    }
}
