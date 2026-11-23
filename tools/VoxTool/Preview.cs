using System;
using System.Collections.Generic;
using TreasureRun.Vox;

namespace VoxTool
{
    /// <summary>Isometric software renderer for .vox models: front view and back view side by side.</summary>
    public static class Preview
    {
        private const uint Background = 0xFF2B2B2Bu;

        public static Image Render(VoxModel m, int viewSize = 420)
        {
            var img = new Image(viewSize * 2, viewSize, Background);
            DrawView(img, m, 0, viewSize, back: false);
            DrawView(img, m, viewSize, viewSize, back: true);
            return img;
        }

        private static void DrawView(Image img, VoxModel m, int offsetX, int size, bool back)
        {
            int sx = m.SizeX, sy = m.SizeY, sz = m.SizeZ;
            // Projected extent of the bounding box: width = (sx + sy) * a, height = ((sx + sy) / 2 + sz) * a.
            float a = Math.Min((size - 20f) / (sx + sy), (size - 20f) / ((sx + sy) / 2f + sz));
            float cx = offsetX + size / 2f - (sx - sy) * a / 2f;
            float cy = (size - ((sx + sy) / 2f + sz) * a) / 2f + sz * a;

            (float, float) P(float x, float y, float z) => (cx + (x - y) * a, cy + (x + y) * a * 0.5f - z * a);

            var order = new List<(int x, int y, int z)>();
            for (int x = 0; x < sx; x++)
                for (int y = 0; y < sy; y++)
                    for (int z = 0; z < sz; z++)
                        if (Voxel(m, x, y, z, back) != 0) order.Add((x, y, z));
            order.Sort((p, q) => (p.x + p.y + p.z).CompareTo(q.x + q.y + q.z));

            foreach (var (x, y, z) in order)
            {
                uint col = m.Palette[Voxel(m, x, y, z, back)];
                if (Voxel(m, x, y, z + 1, back) == 0)
                    Quad(img, P(x, y, z + 1), P(x + 1, y, z + 1), P(x + 1, y + 1, z + 1), P(x, y + 1, z + 1), Shade(col, 1f));
                if (Voxel(m, x + 1, y, z, back) == 0)
                    Quad(img, P(x + 1, y, z), P(x + 1, y + 1, z), P(x + 1, y + 1, z + 1), P(x + 1, y, z + 1), Shade(col, 0.78f));
                if (Voxel(m, x, y + 1, z, back) == 0)
                    Quad(img, P(x, y + 1, z), P(x + 1, y + 1, z), P(x + 1, y + 1, z + 1), P(x, y + 1, z + 1), Shade(col, 0.6f));
            }
        }

        /// <summary>
        /// The in-game camera's view (it looks along world +x, -z, so it sees the model's -x and +y faces), drawn larger.
        /// </summary>
        public static Image RenderGame(VoxModel m, int viewSize = 800)
        {
            // A quarter turn: the model's +y becomes +x and its -x becomes +y, the two faces this view draws.
            var turned = new VoxModel(m.SizeY, m.SizeX, m.SizeZ) { Name = m.Name, Palette = m.Palette };
            for (int x = 0; x < m.SizeX; x++)
                for (int y = 0; y < m.SizeY; y++)
                    for (int z = 0; z < m.SizeZ; z++) turned.Voxels[y, m.SizeX - 1 - x, z] = m.Voxels[x, y, z];
            var img = new Image(viewSize, viewSize, Background);
            DrawView(img, turned, 0, viewSize, back: false);
            return img;
        }

        /// <summary>Back view = model rotated 180° around the vertical axis.</summary>
        private static byte Voxel(VoxModel m, int x, int y, int z, bool back) =>
            back ? m.Get(m.SizeX - 1 - x, m.SizeY - 1 - y, z) : m.Get(x, y, z);

        private static uint Shade(uint c, float f)
        {
            VoxModel.Unpack(c, out byte r, out byte g, out byte b, out _);
            return VoxModel.Rgba((byte)(r * f), (byte)(g * f), (byte)(b * f));
        }

        private static void Quad(Image img, (float, float) p0, (float, float) p1, (float, float) p2, (float, float) p3, uint color)
        {
            var pts = new[] { p0, p1, p2, p3 };
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { minY = Math.Min(minY, p.Item2); maxY = Math.Max(maxY, p.Item2); }
            for (int y = (int)Math.Floor(minY); y <= (int)Math.Ceiling(maxY); y++)
            {
                float yc = y + 0.5f, xMin = float.MaxValue, xMax = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    var (ax, ay) = pts[i];
                    var (bx, by) = pts[(i + 1) % 4];
                    if ((yc < ay) == (yc < by)) continue;
                    float x = ax + (yc - ay) / (by - ay) * (bx - ax);
                    xMin = Math.Min(xMin, x); xMax = Math.Max(xMax, x);
                }
                if (xMin > xMax) continue;
                for (int x = (int)Math.Round(xMin); x < (int)Math.Round(xMax); x++) img.Set(x, y, color);
            }
        }
    }
}
