using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TreasureRun.UI
{
    /// <summary>
    /// Loads PNG icons from <c>BepInEx/plugins/TreasureRun/icons</c> (rendered from SVG by tools/IconTool). Names are the
    /// path inside the icons folder without extension, e.g. "poi/lightning_trap".
    /// </summary>
    public static class IconLibrary
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        public static string IconsDirectory =>
            Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "icons");

        /// <summary>The icon as a sprite (cached); null when missing (logged once).</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (cache.TryGetValue(name, out Sprite cached)) return cached;
            Sprite sprite = null;
            string file = Path.Combine(IconsDirectory, name.Replace('/', Path.DirectorySeparatorChar) + ".png");
            try
            {
                if (File.Exists(file))
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "TR_Icon_" + name, wrapMode = TextureWrapMode.Clamp };
                    if (tex.LoadImage(File.ReadAllBytes(file)))
                    {
                        tex.filterMode = FilterMode.Trilinear;
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        sprite.name = tex.name;
                    }
                }
                if (sprite == null) Plugin.Log.LogWarning($"Icon not found or unreadable: {name} ({file})");
            }
            catch (Exception e) { Plugin.Log.LogError($"Failed to load icon '{name}': {e.Message}"); }
            cache[name] = sprite;
            return sprite;
        }

        public static void ClearCache() => cache.Clear();
    }
}
