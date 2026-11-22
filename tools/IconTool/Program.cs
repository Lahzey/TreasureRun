using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using Svg.Skia;

namespace IconTool
{
    /// <summary>
    /// Icons as code. SVG files are written as text (tools/IconTool/Icons/**.svg) and rendered to PNGs that the mod
    /// loads at runtime (icons/**.png, deployed by the mod build to plugins/TreasureRun/icons).
    ///   build &lt;svgDir&gt; &lt;pngDir&gt; [prefix] [--size N]   render every SVG (default 128 px, square, transparent)
    ///   sheet &lt;pngDir&gt; &lt;out.png&gt; [prefix]               contact sheet on light and dark backgrounds, for review
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
                    case "sheet": return Sheet(args.Skip(1).ToArray());
                    default: return Usage();
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
        }

        private static int Usage()
        {
            Console.WriteLine("IconTool build <svgDir> <pngDir> [prefix] [--size N]");
            Console.WriteLine("IconTool sheet <pngDir> <out.png> [prefix]");
            return 1;
        }

        private static (List<string> positional, int size) Parse(string[] args)
        {
            var pos = new List<string>();
            int size = 128;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--size" && i + 1 < args.Length) size = int.Parse(args[++i]);
                else pos.Add(args[i]);
            }
            return (pos, size);
        }

        private static string Rel(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

        private static int Build(string[] args)
        {
            var (pos, size) = Parse(args);
            if (pos.Count < 2) return Usage();
            string svgDir = Path.GetFullPath(pos[0]), pngDir = Path.GetFullPath(pos[1]);
            string prefix = pos.Count > 2 ? pos[2].Replace('\\', '/') : "";
            int written = 0, failed = 0;
            foreach (string file in Directory.EnumerateFiles(svgDir, "*.svg", SearchOption.AllDirectories).OrderBy(f => f))
            {
                string rel = Rel(svgDir, file);
                if (!rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string outPath = Path.Combine(pngDir, Path.ChangeExtension(rel, ".png"));
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    Render(file, outPath, size);
                    Console.WriteLine($"{rel,-50} -> {Rel(pngDir, outPath)} ({size}x{size})");
                    written++;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{rel,-50} FAILED: {e.Message}");
                    failed++;
                }
            }
            Console.WriteLine($"Done: {written} icons written, {failed} failed.");
            return failed > 0 ? 1 : 0;
        }

        /// <summary>Renders an SVG into a transparent square PNG, scaled to fit and centred.</summary>
        public static void Render(string svgPath, string pngPath, int size)
        {
            using var svg = new SKSvg();
            if (svg.Load(svgPath) == null || svg.Picture == null) throw new InvalidDataException("not a valid SVG");
            SKRect r = svg.Picture.CullRect;
            float scale = size / Math.Max(r.Width, r.Height);
            using var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Translate((size - r.Width * scale) / 2f, (size - r.Height * scale) / 2f);
                canvas.Scale(scale);
                canvas.Translate(-r.Left, -r.Top);
                canvas.DrawPicture(svg.Picture);
            }
            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            using FileStream fs = File.Create(pngPath);
            data.SaveTo(fs);
        }

        /// <summary>All PNGs side by side, each on a light and a dark tile, with the name underneath.</summary>
        private static int Sheet(string[] args)
        {
            var (pos, _) = Parse(args);
            if (pos.Count < 2) return Usage();
            string pngDir = Path.GetFullPath(pos[0]);
            string prefix = pos.Count > 2 ? pos[2].Replace('\\', '/') : "";
            var files = Directory.EnumerateFiles(pngDir, "*.png", SearchOption.AllDirectories)
                .Where(f => Rel(pngDir, f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(f => f).ToList();
            if (files.Count == 0) { Console.WriteLine("No icons."); return 1; }
            const int cell = 160, pad = 12, label = 22;
            int w = cell * 2 + pad * 3, h = (cell + label + pad) * files.Count + pad;
            using var bitmap = new SKBitmap(w, h);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(new SKColor(128, 128, 140));
                using var font = new SKFont(SKTypeface.Default, 14);
                using var text = new SKPaint { Color = SKColors.White, IsAntialias = true };
                for (int i = 0; i < files.Count; i++)
                {
                    int y = pad + i * (cell + label + pad);
                    using SKBitmap icon = SKBitmap.Decode(files[i]);
                    for (int k = 0; k < 2; k++)
                    {
                        int x = pad + k * (cell + pad);
                        using var bg = new SKPaint { Color = k == 0 ? new SKColor(240, 228, 196) : new SKColor(40, 44, 60) };
                        canvas.DrawRect(x, y, cell, cell, bg);
                        canvas.DrawBitmap(icon, new SKRect(x + 16, y + 16, x + cell - 16, y + cell - 16));
                    }
                    canvas.DrawText(Rel(pngDir, files[i]), pad, y + cell + 16, font, text);
                }
            }
            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pos[1])));
            using FileStream fs = File.Create(pos[1]);
            data.SaveTo(fs);
            Console.WriteLine($"Sheet written: {pos[1]} ({files.Count} icons)");
            return 0;
        }
    }
}
