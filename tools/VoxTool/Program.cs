using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TreasureRun.Vox;

namespace VoxTool
{
    /// <summary>
    /// Command line voxel tooling for Treasure Run.
    ///   extract &lt;rippedAssetsDir&gt; &lt;outDir&gt; [--filter text] [--scene SceneName] [--no-preview]
    ///       Converts vanilla models (prefabs, optionally a scene) to .vox + .json sidecar + preview .png.
    ///   preview &lt;file.vox&gt; [out.png]     Renders an isometric front/back preview.
    ///   info &lt;file.vox&gt;                  Prints size, voxel count and colours.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0) return Usage();
                switch (args[0].ToLowerInvariant())
                {
                    case "extract": return Extract(args.Skip(1).ToArray());
                    case "preview": return PreviewCmd(args.Skip(1).ToArray());
                    case "info": return Info(args.Skip(1).ToArray());
                    case "build": return Build(args.Skip(1).ToArray());
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
            Console.WriteLine("VoxTool extract <rippedAssetsDir> <outDir> [--filter text] [--scene SceneName] [--no-preview]");
            Console.WriteLine("VoxTool preview <file.vox> [out.png]");
            Console.WriteLine("VoxTool info <file.vox>");
            return 2;
        }

        private static string Option(string[] args, string name) =>
            Array.IndexOf(args, name) is int i && i >= 0 && i + 1 < args.Length ? args[i + 1] : null;

        private static int Extract(string[] args)
        {
            if (args.Length < 2) return Usage();
            string assetsDir = args[0], outDir = args[1];
            string filter = Option(args, "--filter");
            string scene = Option(args, "--scene");
            bool preview = !args.Contains("--no-preview");

            var assets = new UnityAssets(assetsDir);
            var sources = new List<(string label, string file)>();
            if (scene != null) sources.Add((scene, Path.Combine(assetsDir, "Scenes", scene + ".unity")));
            else
                foreach (string prefab in Directory.EnumerateFiles(Path.Combine(assetsDir, "GameObject"), "*.prefab"))
                    sources.Add((Path.GetFileNameWithoutExtension(prefab), prefab));

            int written = 0, failed = 0;
            var meshCache = new Dictionary<string, MeshData>();
            foreach (var (label, file) in sources)
            {
                List<RendererRef> renderers = assets.ReadRenderers(file);
                foreach (RendererRef r in renderers)
                {
                    string fullPath = scene != null ? $"{label}:{r.Path}" : r.Path;
                    if (filter != null && fullPath.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    string meshPath = assets.PathOf(r.MeshGuid);
                    if (meshPath == null || !meshPath.EndsWith(".asset")) continue; // built-in meshes (cube, quad...)
                    try
                    {
                        if (!meshCache.TryGetValue(meshPath, out MeshData mesh)) meshCache[meshPath] = mesh = UnityAssets.ReadMesh(meshPath);
                        string texPath = assets.MainTexturePath(r.MaterialGuid);
                        Image palette = texPath != null && texPath.EndsWith(".png") ? Image.LoadPng(texPath) : null;

                        string name = Sanitize(r.Path.Replace('/', '_'));
                        var result = Voxelizer.Convert(mesh, palette, name);
                        string dir = Path.Combine(outDir, Sanitize(label));
                        Directory.CreateDirectory(dir);
                        string basePath = Path.Combine(dir, name);

                        VoxFile.Write(basePath + ".vox", result.Model);
                        File.WriteAllText(basePath + ".json", Json(result.VoxelSize, result.Origin, fullPath));
                        if (preview) Preview.Render(result.Model).SavePng(basePath + ".png");

                        Console.WriteLine($"{fullPath,-70} {result.Model.SizeX}x{result.Model.SizeY}x{result.Model.SizeZ} @ {result.VoxelSize}" +
                                          $"{(palette == null ? "  [no texture]" : "")}{(result.Conflicts > 0 ? $"  [{result.Conflicts} colour conflicts]" : "")}");
                        written++;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"{fullPath,-70} FAILED: {e.Message}");
                        failed++;
                    }
                }
            }
            Console.WriteLine($"Done: {written} models written, {failed} failed.");
            return failed > 0 && written == 0 ? 1 : 0;
        }

        private static int PreviewCmd(string[] args)
        {
            if (args.Length < 1) return Usage();
            List<VoxModel> models = VoxFile.Read(args[0]);
            string outPath = args.Length > 1 ? args[1] : Path.ChangeExtension(args[0], ".png");
            Preview.Render(models[0]).SavePng(outPath);
            Console.WriteLine($"Preview written: {outPath}{(models.Count > 1 ? $" (first of {models.Count} models)" : "")}");
            return 0;
        }

        /// <summary>Procedural models: recipe name -> generator (see Recipes/).</summary>
        private static readonly Dictionary<string, Func<VoxModel>> RecipeTable = new Dictionary<string, Func<VoxModel>>(StringComparer.OrdinalIgnoreCase)
        {
            { "castles/castle_town", VoxTool.Recipes.CastleRecipes.CastleTown },
            { "castles/arcane_spire", VoxTool.Recipes.CastleRecipes.ArcaneSpire },
            { "castles/arcane_spire_crystal", VoxTool.Recipes.CastleRecipes.ArcaneCrystal },
            { "castles/gilded_vault", VoxTool.Recipes.CastleRecipes2.GildedVault },
            { "castles/siege_workshop", VoxTool.Recipes.CastleRecipes2.SiegeWorkshop },
            { "castles/siege_cannon", VoxTool.Recipes.CastleRecipes2.SiegeCannon },
            { "castles/siege_ball", VoxTool.Recipes.CastleRecipes2.SiegeBall },
            { "castles/siege_wall_cannon", VoxTool.Recipes.CastleRecipes2.SiegeWallCannon },
            { "castles/siege_catapult", VoxTool.Recipes.CastleRecipes2.SiegeCatapult },
            { "castles/siege_catapult_arm", VoxTool.Recipes.CastleRecipes2.SiegeCatapultArm },
            { "castles/necropolis", VoxTool.Recipes.CastleRecipes2.Necropolis },
            { "castles/druid_grove", VoxTool.Recipes.CastleRecipes2.DruidGrove },
            { "castles/colosseum", VoxTool.Recipes.CastleRecipes2.Colosseum },
            { "castles/crows_nest", VoxTool.Recipes.CastleRecipes2.CrowsNest },
            { "castles/glass_fortress", VoxTool.Recipes.CastleRecipes2.GlassFortress },
            { "planes/strike_fighter", VoxTool.Recipes.PlaneRecipes.StrikeFighter },
            { "planes/carpet_bomber", VoxTool.Recipes.PlaneRecipes.CarpetBomber },
            { "planes/propeller", VoxTool.Recipes.PlaneRecipes.Propeller },
            { "planes/bomb", VoxTool.Recipes.PlaneRecipes.Bomb },
            { "planes/war_blimp", VoxTool.Recipes.PlaneRecipes.WarBlimp },
            { "planes/gatling_mount", VoxTool.Recipes.PlaneRecipes.GatlingMount },
            { "planes/gatling_barrels", VoxTool.Recipes.PlaneRecipes.GatlingBarrels },
        };

        /// <summary>build &lt;modelsDir&gt; [recipe|all]: writes &lt;modelsDir&gt;/&lt;recipe&gt;.vox and a preview .png next to it.</summary>
        private static int Build(string[] args)
        {
            if (args.Length < 1) return Usage();
            string modelsDir = args[0];
            string which = args.Length > 1 ? args[1] : "all";
            foreach (var kv in RecipeTable)
            {
                if (which != "all" && !string.Equals(which, kv.Key, StringComparison.OrdinalIgnoreCase)) continue;
                VoxModel m = kv.Value();
                string path = Path.Combine(modelsDir, kv.Key.Replace('/', Path.DirectorySeparatorChar) + ".vox");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                VoxFile.Write(path, m);
                Preview.Render(m).SavePng(Path.ChangeExtension(path, ".png"));
                Console.WriteLine($"{kv.Key,-30} {m.SizeX}x{m.SizeY}x{m.SizeZ}, {m.CountFilled()} voxels -> {path}");
            }
            return 0;
        }

        private static int Info(string[] args)
        {
            if (args.Length < 1) return Usage();
            foreach (VoxModel m in VoxFile.Read(args[0]))
            {
                var used = new HashSet<byte>();
                foreach (byte b in m.Voxels) if (b != 0) used.Add(b);
                Console.WriteLine($"{m.Name}: {m.SizeX}x{m.SizeY}x{m.SizeZ}, {m.CountFilled()} voxels, {used.Count} colours");
            }
            return 0;
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? '_' : c);
            return sb.ToString();
        }

        private static string Json(float voxelSize, float[] origin, string source)
        {
            string F(float f) => f.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);
            return "{\n" +
                   $"  \"VoxelSize\": {F(voxelSize)},\n" +
                   $"  \"Origin\": [{F(origin[0])}, {F(origin[1])}, {F(origin[2])}],\n" +
                   $"  \"Source\": \"{source.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"\n" +
                   "}\n";
        }
    }
}
