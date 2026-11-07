using UnityEngine;

namespace TreasureRun.UI
{
    /// <summary>
    /// Placeholder treasure icon: a white faceted gem generated at runtime, tinted per rarity by the Image color
    /// (vanilla card art works the same way: white silhouettes tinted by the Image).
    /// </summary>
    internal static class TreasureIcons
    {
        private static Sprite gem;

        public static Sprite Gem => gem != null ? gem : (gem = CreateGem(64));

        private static Sprite CreateGem(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "TreasureRun_Gem",
            };

            float c = (size - 1) / 2f;
            float crownY = size * 0.68f; // y of the girdle (widest line); crown above, pavilion below
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - c) / c;
                    float fy = y / (float)(size - 1);
                    bool inside;
                    float shade;
                    if (y >= crownY)
                    {
                        // Crown: trapezoid, narrower towards the top.
                        float t = (y - crownY) / (size - 1 - crownY);
                        inside = dx <= Mathf.Lerp(0.95f, 0.55f, t) && fy <= 0.93f;
                        shade = dx < 0.3f ? 1f : 0.85f;
                    }
                    else
                    {
                        // Pavilion: triangle down to the culet.
                        float t = y / crownY;
                        inside = dx <= Mathf.Lerp(0.02f, 0.95f, t) && fy >= 0.05f;
                        shade = x < c ? 0.7f : 0.55f;
                    }
                    tex.SetPixel(x, y, inside ? new Color(shade, shade, shade, 1f) : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
