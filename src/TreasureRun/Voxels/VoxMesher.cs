using System.Collections.Generic;
using TreasureRun.Vox;
using UnityEngine;
using UnityEngine.Rendering;

namespace TreasureRun.Voxels
{
    /// <summary>
    /// Builds a Unity mesh from a <see cref="VoxModel"/> with greedy meshing (same-colour faces merged into quads),
    /// coloured through a 256x1 palette texture, exactly like vanilla's MagicaVoxel exports.
    /// </summary>
    internal static class VoxMesher
    {
        /// <param name="voxelSize">World units per voxel.</param>
        /// <param name="origin">Unity-space position of the grid's minimum corner.</param>
        public static Mesh Build(VoxModel m, float voxelSize, Vector3 origin, string name)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int[] size = { m.SizeX, m.SizeY, m.SizeZ };

            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3, v = (d + 2) % 3;
                var mask = new byte[size[u] * size[v]];
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    for (int slice = 0; slice < size[d]; slice++)
                    {
                        // Mask of visible faces on this slice.
                        int n = 0;
                        var p = new int[3];
                        for (int j = 0; j < size[v]; j++)
                            for (int i = 0; i < size[u]; i++, n++)
                            {
                                p[d] = slice; p[u] = i; p[v] = j;
                                byte c = m.Get(p[0], p[1], p[2]);
                                p[d] = slice + sign;
                                mask[n] = c != 0 && m.Get(p[0], p[1], p[2]) == 0 ? c : (byte)0;
                            }

                        // Greedy merge.
                        n = 0;
                        for (int j = 0; j < size[v]; j++)
                            for (int i = 0; i < size[u];)
                            {
                                byte c = mask[n];
                                if (c == 0) { i++; n++; continue; }
                                int w = 1;
                                while (i + w < size[u] && mask[n + w] == c) w++;
                                int h = 1;
                                bool done = false;
                                while (j + h < size[v] && !done)
                                {
                                    for (int k = 0; k < w; k++)
                                        if (mask[n + k + h * size[u]] != c) { done = true; break; }
                                    if (!done) h++;
                                }

                                float plane = sign > 0 ? slice + 1 : slice;
                                var a = new float[3]; var du = new float[3]; var dv = new float[3];
                                a[d] = plane; a[u] = i; a[v] = j;
                                du[u] = w; dv[v] = h;
                                var normal = new float[3]; normal[d] = sign;
                                AddQuad(verts, normals, uvs, tris, a, du, dv, normal, c, voxelSize, origin);

                                for (int l = 0; l < h; l++)
                                    for (int k = 0; k < w; k++) mask[n + k + l * size[u]] = 0;
                                i += w; n += w;
                            }
                    }
                }
            }

            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Vox (x, y, z) to Unity (x, z, y).</summary>
        private static Vector3 ToUnity(float x, float y, float z, float s, Vector3 origin) => origin + new Vector3(x, z, y) * s;

        private static void AddQuad(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris,
                                    float[] a, float[] du, float[] dv, float[] n, byte color, float s, Vector3 origin)
        {
            Vector3 p0 = ToUnity(a[0], a[1], a[2], s, origin);
            Vector3 p1 = ToUnity(a[0] + du[0], a[1] + du[1], a[2] + du[2], s, origin);
            Vector3 p2 = ToUnity(a[0] + du[0] + dv[0], a[1] + du[1] + dv[1], a[2] + du[2] + dv[2], s, origin);
            Vector3 p3 = ToUnity(a[0] + dv[0], a[1] + dv[1], a[2] + dv[2], s, origin);
            Vector3 normal = new Vector3(n[0], n[2], n[1]);

            int b = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
            for (int k = 0; k < 4; k++) normals.Add(normal);
            var uv = new Vector2((color + 0.5f) / 256f, 0.5f);
            for (int k = 0; k < 4; k++) uvs.Add(uv);

            // Unity front faces: Cross(b - a, c - a) points outwards.
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) >= 0f)
            {
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            else
            {
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
        }

        public static Texture2D PaletteTexture(uint[] palette, string name)
        {
            var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[256];
            for (int i = 0; i < 256; i++)
            {
                VoxModel.Unpack(palette[i], out byte r, out byte g, out byte b, out byte al);
                px[i] = new Color32(r, g, b, 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
