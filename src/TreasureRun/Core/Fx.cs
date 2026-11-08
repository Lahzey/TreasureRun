using System.Linq;
using UnityEngine;

namespace TreasureRun.Core
{
    /// <summary>Small procedural visual effects.</summary>
    public static class Fx
    {
        private static Material particleMaterial;
        private static bool lookedUp;
        private static Material lineMaterial;

        private static Material glowMaterial;
        private static Mesh cube;

        private static Mesh Cube()
        {
            if (cube != null) return cube;
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
            return cube;
        }

        /// <summary>
        /// Voxel-style burst: small cubes in the given colours that fly out, tumble, fall (gravity) and shrink away.
        /// Explosions, debris, smoke puffs. Unlit (Sprites/Default), cheap: one short-lived particle system.
        /// </summary>
        public static void VoxelBurst(Vector3 position, Color[] colors, int count, float size, float speed, float lifetime, float gravity)
        {
            Material mat = GlowMaterial;
            if (mat == null || colors == null || colors.Length == 0) return;
            var go = new GameObject("TR_VoxelBurst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity / 9.81f;
            main.maxParticles = count * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var gradient = new Gradient();
            int n = Mathf.Min(colors.Length, 8);
            var keys = new GradientColorKey[n];
            for (int i = 0; i < n; i++) keys[i] = new GradientColorKey(colors[i], n == 1 ? 0f : i / (n - 1f));
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = size;
            shape.rotation = new Vector3(-90f, 0f, 0f); // hemisphere opening upwards

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = rot.y = rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Cube();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            Object.Destroy(go, lifetime + 0.5f);
        }

        /// <summary>
        /// Unlit alpha-blended material (built-in "Sprites/Default", always included): colour and alpha come from
        /// vertex colours / LineRenderer colours. For glows, veins and other semi-transparent effects.
        /// </summary>
        public static Material GlowMaterial
        {
            get
            {
                if (glowMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader != null) glowMaterial = new Material(shader) { name = "TR_Glow" };
                }
                return glowMaterial ?? LineMaterial;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, Material> ghostMaterials =
            new System.Collections.Generic.Dictionary<string, Material>();

        /// <summary>
        /// Ghostly version of a (voxel) material: unlit, alpha-blended, tinted. Works for any model using a palette
        /// texture, so every monster turns into a ghost without per-model work. Cached per texture.
        /// </summary>
        public static Material GhostMaterial(Texture texture, Color tint)
        {
            string key = (texture != null ? texture.GetInstanceID().ToString() : "none") + "/" + tint;
            if (ghostMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material baseMat = GlowMaterial;
            if (baseMat == null) return null;
            var m = new Material(baseMat) { name = "TR_Ghost", mainTexture = texture, color = tint };
            ghostMaterials[key] = m;
            return m;
        }

        private static readonly System.Collections.Generic.Dictionary<int, Texture2D> rampTextures =
            new System.Collections.Generic.Dictionary<int, Texture2D>();

        /// <summary>
        /// Recoloured copy of a texture: each pixel's brightness is mapped onto a colour ramp (dark → light), e.g. to
        /// turn any monster's palette into shades of ghostly blue. Works on non-readable textures (copied via a
        /// render texture). Cached per source texture; meant for small palette textures.
        /// </summary>
        public static Texture2D RampTexture(Texture source, Color dark, Color light)
        {
            if (source == null) return null;
            int key = source.GetInstanceID() ^ dark.GetHashCode() * 31 ^ light.GetHashCode();
            if (rampTextures.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            int w = Mathf.Clamp(source.width, 1, 512), h = Mathf.Clamp(source.height, 1, 512);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "TR_Ramp" };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color[] px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                float lum = Mathf.Clamp01(0.299f * px[i].r + 0.587f * px[i].g + 0.114f * px[i].b);
                lum = Mathf.Pow(lum, 0.7f); // lift the darks a little
                Color c = Color.Lerp(dark, light, lum);
                c.a = 1f;
                px[i] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            rampTextures[key] = tex;
            return tex;
        }

        /// <summary>
        /// Copy of a Standard-shader material switched to "Fade" (alpha-blended, still lit). Returns the copy; if the
        /// build stripped the Fade variant, the copy simply renders opaque.
        /// </summary>
        public static Material FadeCopy(Material standard, float alpha)
        {
            var m = new Material(standard) { name = standard.name + "_Fade" };
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            Color c = m.HasProperty("_Color") ? m.color : Color.white;
            m.color = new Color(c.r, c.g, c.b, alpha);
            return m;
        }

        /// <summary>Material of a vanilla LineRenderer (null if none is loaded). Vertex colours incl. alpha apply.</summary>
        public static Material LineMaterial
        {
            get
            {
                if (lineMaterial == null)
                {
                    LineRenderer template = Resources.FindObjectsOfTypeAll<LineRenderer>().FirstOrDefault(l => l != null && l.sharedMaterial != null);
                    lineMaterial = template != null ? template.sharedMaterial : null;
                }
                return lineMaterial;
            }
        }

        /// <summary>
        /// One-shot particle burst (sparks drifting upwards). Uses a vanilla particle material so it renders with the
        /// game's shaders; silently does nothing if none is loaded.
        /// </summary>
        public static void Burst(Vector3 position, Color color, int count = 28, float size = 0.12f, float speed = 2.2f)
        {
            if (!lookedUp)
            {
                lookedUp = true;
                // Renderer references only (never read textures of arbitrary materials).
                ParticleSystemRenderer template = Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>()
                    .FirstOrDefault(r => r != null && r.sharedMaterial != null);
                particleMaterial = template != null ? template.sharedMaterial : null;
            }
            if (particleMaterial == null) return;

            var go = new GameObject("TR_Burst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.6f));
            main.gravityModifier = -0.25f;
            main.maxParticles = count * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
            ps.Play();
            Object.Destroy(go, 2f);
        }
    }
}
