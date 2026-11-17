using System;
using System.Collections.Generic;
using TreasureRun.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>
    /// Many camera-facing quads in one dynamic mesh (one draw call, no GameObject per quad): charge bunches, energy
    /// packets, accretion motes. <see cref="AddAt"/> takes a ready world position; quads always face the camera.
    /// </summary>
    internal sealed class QuadBatch
    {
        private readonly GameObject go;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> tris = new List<int>();
        private Vector3 right, up;
        private readonly bool additive;

        /// <param name="additive">Glowing light (added to what is behind) instead of blended paint.</param>
        public QuadBatch(string name, Texture texture, int queue = 3000, int layer = 0, bool additive = false)
        {
            go = new GameObject(name) { layer = layer };
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            this.additive = additive;
            material = additive ? AccelFx.Additive(name, texture, Color.white, queue)
                                : new Material(Fx.GlowMaterial) { mainTexture = texture, name = name, renderQueue = queue };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public int Count => verts.Count / 4;

        public void Begin()
        {
            verts.Clear(); colors.Clear(); uvs.Clear(); tris.Clear();
            Camera cam = Camera.main;
            right = cam != null ? cam.transform.right : Vector3.right;
            up = cam != null ? cam.transform.up : Vector3.up;
        }

        public void Add(Vector3 at, float size, Color color)
        {
            int i = verts.Count;
            Vector3 r = right * size * 0.5f, u = up * size * 0.5f;
            verts.Add(at - r - u); verts.Add(at - r + u); verts.Add(at + r + u); verts.Add(at + r - u);
            if (additive) color = new Color(color.r * color.a, color.g * color.a, color.b * color.a, 1f);
            for (int k = 0; k < 4; k++) colors.Add(color);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        public void End()
        {
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        public void Clear() { Begin(); End(); }

        public void Destroy()
        {
            if (go != null) Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
        }
    }

    /// <summary>Generated textures, meshes and small objects for the accelerator's effects.</summary>
    internal static class AccelFx
    {
        /// <summary>
        /// Layer of the black hole's own visuals: the space warp's camera leaves it out, so the hole doesn't lens itself.
        /// (An unnamed layer; the main camera is made to draw it.)
        /// </summary>
        public const int HoleLayer = 31;

        private static Texture2D dot, ring, disk, dotGlow, ringGlow;
        private static Material black;

        /// <summary>A soft round dot (white, alpha falling off like a Gaussian).</summary>
        public static Texture2D Dot => dot != null ? dot : dot = Radial("TR_AccelDot", 64, r => Mathf.Exp(-r * r * 5f) * Mathf.Clamp01((1f - r) * 4f));

        /// <summary>A bright thin ring at 47% of the radius with a soft glow outside it (the photon ring round the shadow).</summary>
        public static Texture2D RingTex => ring != null ? ring : ring = Radial("TR_AccelRing", 128, r =>
        {
            float line = Mathf.Exp(-Mathf.Pow((r - 0.47f) / 0.05f, 2f));
            float glow = r > 0.47f ? Mathf.Exp(-(r - 0.47f) * 8f) * 0.4f : 0f;
            return Mathf.Clamp01(line + glow) * Mathf.Clamp01((1f - r) * 6f);
        });

        /// <summary>The dot and ring as light for additive materials: the shape is in the colour (premultiplied), not only alpha.</summary>
        public static Texture2D DotGlow => dotGlow != null ? dotGlow : dotGlow = Premultiplied(Dot, "TR_AccelDotGlow");
        public static Texture2D RingGlow => ringGlow != null ? ringGlow : ringGlow = Premultiplied(RingTex, "TR_AccelRingGlow");

        private static Texture2D Premultiplied(Texture2D src, string name)
        {
            Color[] px = src.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = new Color(px[i].r * px[i].a, px[i].g * px[i].a, px[i].b * px[i].a, px[i].a);
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>A solid disc with a short soft edge (the black hole's shadow).</summary>
        public static Texture2D DiskTex => disk != null ? disk : disk = Radial("TR_AccelDisk", 128, r => Mathf.Clamp01((1f - r) / 0.06f));

        private static Texture2D Radial(string name, int size, Func<float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float h = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - h) * (x - h) + (y - h) * (y - h)) / h;
                    px[y * size + x] = new Color(1f, 1f, 1f, r >= 1f ? 0f : alpha(r));
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>An opaque, unlit-looking black sphere (Standard, black, no gloss): it hides what lies behind it.</summary>
        public static GameObject Sphere(string name, Transform parent, float diameter)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "TR_" + name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * diameter;
            if (black == null)
            {
                Shader standard = Shader.Find("Standard");
                black = new Material(standard != null ? standard : Fx.GlowMaterial.shader) { name = "TR_Void", color = Color.black };
                if (black.HasProperty("_Glossiness")) black.SetFloat("_Glossiness", 0f);
                if (black.HasProperty("_Metallic")) black.SetFloat("_Metallic", 0f);
            }
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = black;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static Shader particles;
        private static bool particlesLooked;

        /// <summary>
        /// An additive material (light added to what is behind it): the build's "Particles/Standard Unlit" with its blend
        /// factors set to One/One. Vertex colours and <paramref name="color"/> are the light added (premultiply any
        /// alpha into them). Falls back to the alpha-blended glow material if the shader is missing.
        /// </summary>
        public static Material Additive(string name, Texture tex, Color color, int queue = 3000)
        {
            if (!particlesLooked) { particlesLooked = true; particles = Shader.Find("Particles/Standard Unlit"); }
            if (particles == null) return Glow(name, tex, color, queue);
            var m = new Material(particles) { name = "TR_" + name, renderQueue = queue };
            if (tex != null) m.mainTexture = tex;
            m.SetFloat("_Mode", 4f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite", 0);
            if (m.HasProperty("_Cull")) m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            m.color = color;
            return m;
        }

        /// <summary>A glow material (unlit, alpha-blended, vertex colours) of its own.</summary>
        public static Material Glow(string name, Texture tex, Color color, int queue = 3000) =>
            new Material(Fx.GlowMaterial) { mainTexture = tex, color = color, name = "TR_" + name, renderQueue = queue };

        /// <summary>A camera-facing textured quad with its own material (so its colour can change).</summary>
        public static GameObject Billboard(string name, Transform parent, Texture tex, Color color, float size, out Material material,
                                           int queue = 3000, int layer = 0, bool additive = false)
        {
            material = additive ? Additive(name, tex, color, queue) : Glow(name, tex, color, queue);
            GameObject go = MeshObject(name, parent, Quad(), material, layer);
            go.transform.localScale = Vector3.one * size;
            Face(go.transform);
            return go;
        }

        public static void Face(Transform t)
        {
            Camera cam = Camera.main;
            if (t != null && cam != null) t.rotation = cam.transform.rotation;
        }

        private static Mesh quad;

        private static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "TR_AccelQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateBounds();
            return quad;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material, int layer = 0)
        {
            var go = new GameObject("TR_" + name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>A flat mesh object with vertex colours, drawn with its own glow material.</summary>
        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, out Material material, int queue = 3000, int layer = 0,
                                            bool additive = false)
        {
            material = additive ? Additive(name, null, Color.white, queue) : Glow(name, null, Color.white, queue);
            return MeshObject(name, parent, mesh, material, layer);
        }

        /// <summary>
        /// An annulus in the x/z plane from <paramref name="inner"/> to <paramref name="outer"/>, coloured per vertex by
        /// <paramref name="color"/>(r 0..1 from inner to outer, angle). Two-sided (Sprites/Default does not cull).
        /// </summary>
        public static Mesh Disc(float inner, float outer, int segments, int rings, Func<float, float, Color> color)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int ri = 0; ri <= rings; ri++)
            {
                float r01 = ri / (float)rings, r = Mathf.Lerp(inner, outer, r01);
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    cols.Add(color(r01, a));
                }
            }
            GridTriangles(tris, rings, segments);
            var m = new Mesh { name = "TR_AccelDisc" };
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Triangles of a (rows + 1) x (columns + 1) vertex grid laid out row by row.</summary>
        public static void GridTriangles(List<int> tris, int rows, int columns, int offset = 0)
        {
            int row = columns + 1;
            for (int ri = 0; ri < rows; ri++)
                for (int s = 0; s < columns; s++)
                {
                    int i = offset + ri * row + s;
                    tris.Add(i); tris.Add(i + row); tris.Add(i + 1);
                    tris.Add(i + 1); tris.Add(i + row); tris.Add(i + row + 1);
                }
        }
    }

    /// <summary>
    /// Gravitational lensing of the scene around black holes. A second camera (a copy of the main one, at half
    /// resolution) renders the scene without the holes' own visuals into a texture; around each hole a camera-facing
    /// annulus shows that texture with every point sampled from where its light would come from: radius ρ maps to
    /// β = ρ − E²/ρ (E = the Einstein radius), so the background is pushed outwards and pulled into a ring round the
    /// shadow. After the burst, the lens turns into a ripple running outwards. Only active while a hole exists.
    /// </summary>
    internal sealed class SpaceWarp : MonoBehaviour
    {
        private static SpaceWarp instance;
        private static readonly List<WarpLens> lenses = new List<WarpLens>();
        private Camera rtCamera;
        private RenderTexture rt;

        public static void Add(WarpLens lens)
        {
            if (!lenses.Contains(lens)) lenses.Add(lens);
            if (instance == null) instance = new GameObject("TR_SpaceWarp").AddComponent<SpaceWarp>();
        }

        public static void Remove(WarpLens lens) => lenses.Remove(lens);

        public static Texture Texture => instance != null ? instance.rt : null;

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            Release();
        }

        private void Release()
        {
            if (rtCamera != null) rtCamera.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        }

        private void LateUpdate()
        {
            lenses.RemoveAll(l => l == null || l.Dead);
            Camera main = Camera.main;
            if (lenses.Count == 0 || main == null) { Release(); return; }
            main.cullingMask |= 1 << AccelFx.HoleLayer;
            int w = Mathf.Max(64, Screen.width / 2), h = Mathf.Max(64, Screen.height / 2);
            if (rt == null || rt.width != w || rt.height != h)
            {
                Release();
                // No alpha channel: the sampled alpha is always 1, whatever the scene's shaders wrote.
                RenderTextureFormat f = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)
                    ? RenderTextureFormat.RGB111110Float : RenderTextureFormat.RGB565;
                rt = new RenderTexture(w, h, 16, f) { name = "TR_SpaceWarp" };
            }
            if (rtCamera == null)
            {
                rtCamera = new GameObject("TR_SpaceWarpCamera").AddComponent<Camera>();
                rtCamera.transform.SetParent(transform, false);
            }
            rtCamera.CopyFrom(main);
            rtCamera.transform.position = main.transform.position + ScreenShake.Current();
            rtCamera.transform.rotation = main.transform.rotation;
            // Without the holes' own visuals and without world-space UI (health bars, damage numbers): those stay crisp.
            rtCamera.cullingMask = main.cullingMask & ~(1 << AccelFx.HoleLayer) & ~(1 << LayerMask.NameToLayer("UI"));
            rtCamera.targetTexture = rt;
            rtCamera.enabled = false;
            rtCamera.Render();
            foreach (WarpLens lens in lenses) lens.Draw(main, rt);
        }
    }

    /// <summary>One lens of the <see cref="SpaceWarp"/> (owned by a black hole).</summary>
    internal sealed class WarpLens
    {
        public Vector3 Centre;
        /// <summary>Einstein radius (0 = no lensing), outer radius of the warped area.</summary>
        public float Einstein, Outer = 3f;
        /// <summary>After the burst: a compression ring at this radius, this strong (radius &lt; 0 = none).</summary>
        public float RippleRadius = -1f, Ripple;
        /// <summary>The warp fades out this far below the centre (on screen), before it would cut into the ground in front.</summary>
        public float Floor = 1f;
        public bool Dead { get; private set; }

        private const int Rings = 18, Segments = 64;
        private readonly GameObject go;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Vector3[] verts = new Vector3[(Rings + 1) * (Segments + 1)];
        private readonly Vector2[] uvs = new Vector2[(Rings + 1) * (Segments + 1)];
        private readonly Color[] colors = new Color[(Rings + 1) * (Segments + 1)];

        public WarpLens()
        {
            mesh = new Mesh { name = "TR_WarpLens" };
            mesh.MarkDynamic();
            var tris = new List<int>();
            AccelFx.GridTriangles(tris, Rings, Segments);
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.SetTriangles(tris, 0);
            material = AccelFx.Glow("WarpLens", null, Color.white, 2990);
            go = AccelFx.MeshObject("WarpLens", null, mesh, material, AccelFx.HoleLayer);
            go.SetActive(false);
            SpaceWarp.Add(this);
        }

        public void Destroy()
        {
            Dead = true;
            SpaceWarp.Remove(this);
            if (go != null) Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
        }

        private float Lens(float rho)
        {
            float b = rho;
            if (Einstein > 0f) b -= Einstein * Einstein / Mathf.Max(rho, 0.05f);
            if (RippleRadius >= 0f) b -= Ripple * Mathf.Exp(-Mathf.Pow((rho - RippleRadius) / 0.45f, 2f));
            return b;
        }

        public void Draw(Camera cam, Texture rt)
        {
            if (Dead) return;
            go.SetActive(true);
            material.mainTexture = rt;
            Vector3 right = cam.transform.right, up = cam.transform.up;
            float outer = RippleRadius >= 0f ? RippleRadius + 1.2f : Outer;
            int v = 0;
            for (int ri = 0; ri <= Rings; ri++)
            {
                float rho = Mathf.Lerp(0.15f, outer, ri / (float)Rings);
                float beta = Lens(rho);
                float alpha = RippleRadius >= 0f
                    ? Mathf.Exp(-Mathf.Pow((rho - RippleRadius) / 0.7f, 2f))
                    : Mathf.Clamp01((outer - rho) / (outer * 0.4f));
                for (int s = 0; s <= Segments; s++)
                {
                    float a = s / (float)Segments * Mathf.PI * 2f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    verts[v] = Centre + right * (ca * rho) + up * (sa * rho);
                    Vector3 vp = cam.WorldToViewportPoint(Centre + right * (ca * beta) + up * (sa * beta));
                    uvs[v] = new Vector2(vp.x, vp.y);
                    float floor = Mathf.Clamp01((sa * rho + Floor) / 0.5f);
                    colors[v] = new Color(1f, 1f, 1f, alpha * floor);
                    v++;
                }
            }
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }
    }

    /// <summary>
    /// Camera shake from black holes. Each open hole gives an amplitude (a share of the screen height); it is weaker the
    /// further the hole is from the middle of the screen and stronger zoomed in. The offset is added to the main camera
    /// just for rendering (onPreCull) and taken off again afterwards, so the game's camera controller never sees it.
    /// </summary>
    internal static class ScreenShake
    {
        private static int frame = -1;
        private static Vector3 offset;
        private static bool hooked, applied;
        private static Vector3 saved;

        /// <summary>This frame's offset (world units), computed once per frame.</summary>
        public static Vector3 Current()
        {
            if (frame == Time.frameCount) return offset;
            frame = Time.frameCount;
            offset = Vector3.zero;
            Camera cam = Camera.main;
            if (cam == null || BlackHole.Active.Count == 0) return offset;
            if (!hooked)
            {
                hooked = true;
                Camera.onPreCull += PreCull;
                Camera.onPostRender += PostRender;
            }
            float size = cam.orthographic ? cam.orthographicSize : 10f;
            float zoom = Mathf.Clamp(10f / Mathf.Max(1f, size), 0.6f, 1.7f);
            float amp = 0f;
            foreach (BlackHole h in BlackHole.Active)
            {
                if (h == null) continue;
                Vector3 vp = cam.WorldToViewportPoint(h.transform.position);
                float d = new Vector2(vp.x - 0.5f, vp.y - 0.5f).magnitude;
                amp += h.ShakeAmplitude * Mathf.Clamp01(1.25f - 1.5f * d);
            }
            if (amp <= 0f) return offset;
            float t = Time.unscaledTime * 24f;
            float nx = Mathf.PerlinNoise(t, 3.7f) * 2f - 1f, ny = Mathf.PerlinNoise(11.3f, t) * 2f - 1f;
            offset = (cam.transform.right * nx + cam.transform.up * ny) * (amp * zoom * 2f * size);
            return offset;
        }

        private static void PreCull(Camera cam)
        {
            if (cam != Camera.main || applied) return;
            Vector3 o = Current();
            if (o == Vector3.zero) return;
            saved = cam.transform.position;
            cam.transform.position = saved + o;
            applied = true;
        }

        private static void PostRender(Camera cam)
        {
            if (!applied || cam != Camera.main) return;
            cam.transform.position = saved;
            applied = false;
        }
    }
}
