using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TreasureRun.Vox;
using UnityEngine;

namespace TreasureRun.Voxels
{
    /// <summary>A loaded .vox model ready to render.</summary>
    public sealed class VoxAsset
    {
        public string Name;
        public string FilePath;
        public VoxModel Model;
        public VoxSidecar Sidecar;
        public Mesh Mesh;
        public Material Material;
    }

    /// <summary>
    /// Loads MagicaVoxel models from <c>BepInEx/plugins/TreasureRun/models</c> (subfolders allowed) and turns them into
    /// meshes with vanilla-style materials. Names are the path relative to the models folder without extension,
    /// using '/' (e.g. "towers/fighter_jet"). An optional same-named .json sidecar sets voxel size and origin.
    /// </summary>
    public static class VoxelLibrary
    {
        private static readonly Dictionary<string, VoxAsset> cache = new Dictionary<string, VoxAsset>(StringComparer.OrdinalIgnoreCase);
        private static Material templateMaterial;

        public static string ModelsDirectory =>
            Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "models");

        /// <summary>All model names currently on disk.</summary>
        public static IEnumerable<string> AvailableNames()
        {
            if (!Directory.Exists(ModelsDirectory)) return Enumerable.Empty<string>();
            return Directory.EnumerateFiles(ModelsDirectory, "*.vox", SearchOption.AllDirectories)
                .Select(NameOf)
                .OrderBy(n => n);
        }

        private static string NameOf(string file)
        {
            string rel = file.Substring(ModelsDirectory.Length).TrimStart('/', '\\');
            return Path.ChangeExtension(rel, null).Replace('\\', '/');
        }

        /// <summary>Loads (or returns cached) model; null if missing or invalid (logged).</summary>
        public static VoxAsset Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (cache.TryGetValue(name, out VoxAsset cached)) return cached;

            string file = Path.Combine(ModelsDirectory, name.Replace('/', Path.DirectorySeparatorChar) + ".vox");
            if (!File.Exists(file))
            {
                Plugin.Log.LogWarning($"Voxel model not found: {name} ({file})");
                return null;
            }

            try
            {
                VoxModel model = VoxFile.Read(file)[0];
                VoxSidecar sidecar = ReadSidecar(Path.ChangeExtension(file, ".json"));
                float s = sidecar.VoxelSize > 0f ? sidecar.VoxelSize : 0.1f;
                Vector3 origin = sidecar.Origin != null && sidecar.Origin.Length == 3
                    ? new Vector3(sidecar.Origin[0], sidecar.Origin[1], sidecar.Origin[2])
                    : new Vector3(-model.SizeX * s / 2f, 0f, -model.SizeY * s / 2f); // centred, standing on y = 0

                var asset = new VoxAsset
                {
                    Name = name,
                    FilePath = file,
                    Model = model,
                    Sidecar = sidecar,
                    Mesh = VoxMesher.Build(model, s, origin, "TR_" + name),
                    Material = CreateMaterial(VoxMesher.PaletteTexture(model.Palette, "TR_" + name + "_palette")),
                };
                cache[name] = asset;
                Plugin.Log.LogInfo($"Loaded voxel model '{name}': {model.SizeX}x{model.SizeY}x{model.SizeZ}, " +
                                   $"{model.CountFilled()} voxels, {asset.Mesh.vertexCount} vertices.");
                return asset;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to load voxel model '{name}': {e.Message}");
                return null;
            }
        }

        /// <summary>Spawns a model as a new GameObject (MeshFilter + MeshRenderer).</summary>
        public static GameObject Spawn(string name, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            VoxAsset asset = Get(name);
            if (asset == null) return null;
            var go = new GameObject("TR_Vox_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = asset.Material;
            return go;
        }

        /// <summary>Drops all cached models; the next <see cref="Get"/> reads the files again.</summary>
        public static void ClearCache()
        {
            foreach (VoxAsset a in cache.Values)
            {
                if (a.Mesh != null) UnityEngine.Object.Destroy(a.Mesh);
                if (a.Material != null)
                {
                    if (a.Material.mainTexture != null) UnityEngine.Object.Destroy(a.Material.mainTexture);
                    UnityEngine.Object.Destroy(a.Material);
                }
            }
            cache.Clear();
        }

        private static VoxSidecar ReadSidecar(string path)
        {
            if (!File.Exists(path)) return new VoxSidecar();
            try { return JsonConvert.DeserializeObject<VoxSidecar>(File.ReadAllText(path)) ?? new VoxSidecar(); }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Invalid voxel sidecar {path}: {e.Message}");
                return new VoxSidecar();
            }
        }

        /// <summary>Clone of a vanilla model material (same shader/lighting), with our palette as main texture.</summary>
        private static Material CreateMaterial(Texture2D palette)
        {
            if (templateMaterial == null) templateMaterial = FindTemplateMaterial();
            var mat = new Material(templateMaterial) { name = palette.name.Replace("_palette", ""), mainTexture = palette };
            return mat;
        }

        /// <summary>
        /// A vanilla model material (palette texture 256x1). Only looks at renderers of live scene objects, starting with
        /// the castle. Never scan Resources.FindObjectsOfTypeAll&lt;Material&gt;(): reading mainTexture on Unity's hidden
        /// internal materials crashes the player natively.
        /// </summary>
        private static Material FindTemplateMaterial()
        {
            var renderers = new List<MeshRenderer>();
            if (TileManager.instance != null && TileManager.instance.startTile != null)
                renderers.AddRange(TileManager.instance.startTile.GetComponentsInChildren<MeshRenderer>(true));
            renderers.AddRange(UnityEngine.Object.FindObjectsOfType<MeshRenderer>());

            foreach (MeshRenderer r in renderers)
            {
                Material m = r != null ? r.sharedMaterial : null;
                if (m == null || !m.HasProperty("_MainTex")) continue;
                if (m.GetTexture("_MainTex") is Texture2D t && t.width == 256 && t.height == 1)
                {
                    Plugin.Log.LogInfo($"Voxel template material: '{m.name}' ({m.shader?.name}) from '{r.name}'.");
                    return m;
                }
            }

            Plugin.Log.LogWarning("No vanilla model material found; using the Standard shader for voxel models.");
            Shader standard = Shader.Find("Standard");
            return new Material(standard != null ? standard : Shader.Find("Diffuse"));
        }
    }
}
