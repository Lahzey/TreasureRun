using System;
using System.Collections;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Effects of the Soul Urn (docs/poi-plan.md §6): souls flying into the urn, the soul fire in its mouth, its burst, and
    /// the textures of the soulfire rune. Everything is unlit and alpha-blended (Sprites/Default) on procedural textures.
    /// </summary>
    public static class SoulFx
    {
        public static readonly Color Soul = new Color(0.55f, 1f, 0.9f, 1f), SoulCore = new Color(0.9f, 1f, 0.98f, 1f);

        private static Texture2D softTex, ringTex, sigilTex, pillarTex;
        private static Material softMat, ringMat, sigilMat, pillarMat;
        private static Mesh flatQuad, billboardQuad;

        // ------------------------------------------------------------------ materials and meshes

        /// <summary>A soft round glow (radial falloff), for wisp heads, motes and halos.</summary>
        public static Material SoftGlow => softMat != null ? softMat : softMat = Make("TR_SoulSoft", softTex = Radial(64));
        /// <summary>The soulfire rune's outer circle: two rings with rune ticks between them.</summary>
        public static Material RuneRing => ringMat != null ? ringMat : ringMat = Make("TR_RuneRing", ringTex = RingTexture(128));
        /// <summary>The rune's inner sigil: three curved blades turning (speed).</summary>
        public static Material RuneSigil => sigilMat != null ? sigilMat : sigilMat = Make("TR_RuneSigil", sigilTex = SigilTexture(128));
        /// <summary>A vertical beam of light, bright at the bottom, for the burst.</summary>
        public static Material Pillar => pillarMat != null ? pillarMat : pillarMat = Make("TR_SoulPillar", pillarTex = PillarTexture(32, 64));

        private static Material Make(string name, Texture2D tex)
        {
            Shader shader = Shader.Find("Sprites/Default");
            var m = shader != null ? new Material(shader) : new Material(Fx.GlowMaterial);
            m.name = name;
            m.mainTexture = tex;
            return m;
        }

        private static Texture2D NewTex(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };

        private static Texture2D Radial(int size)
        {
            Texture2D t = NewTex(size, size, "TR_SoulSoftTex");
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a * (0.6f + 0.4f * a));
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Soft-edged band between radii r0 and r1 (0..1 of the half size).</summary>
        private static float Band(float d, float r0, float r1, float soft) =>
            Mathf.Clamp01((d - r0) / soft + 1f) * Mathf.Clamp01((r1 - d) / soft + 1f);

        private static Texture2D RingTexture(int size)
        {
            Texture2D t = NewTex(size, size, "TR_RuneRingTex");
            var px = new Color[size * size];
            const float soft = 0.025f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
                    float a = Band(d, 0.9f, 0.97f, soft) + 0.8f * Band(d, 0.66f, 0.7f, soft);
                    // Twelve runes between the rings: short bars and dots, alternating.
                    float sector = (ang / (2f * Mathf.PI) + 1f) * 12f;
                    float local = sector - Mathf.Floor(sector) - 0.5f; // -0.5..0.5 inside the sector
                    bool bar = ((int)Mathf.Floor(sector)) % 2 == 0;
                    float width = Mathf.Abs(local) * d * 2f * Mathf.PI / 12f; // arc distance from the sector centre
                    if (bar) a += Band(d, 0.74f, 0.86f, soft) * Mathf.Clamp01((0.035f - width) / soft + 1f);
                    else a += Mathf.Clamp01((0.045f - Mathf.Sqrt(width * width + (d - 0.8f) * (d - 0.8f))) / soft + 1f);
                    // A faint fill so the circle reads as one disc of light.
                    a += d < 0.97f ? 0.12f : 0f;
                    px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D SigilTexture(int size)
        {
            Texture2D t = NewTex(size, size, "TR_RuneSigilTex");
            var px = new Color[size * size];
            const float soft = 0.03f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
                    // Three blades: spirals that sweep a third of a turn from the hub to the rim, thinning outwards.
                    float a = 0f;
                    for (int k = 0; k < 3; k++)
                    {
                        float centre = k * 2f * Mathf.PI / 3f + d * 2.2f;
                        float da = Mathf.DeltaAngle(ang * Mathf.Rad2Deg, centre * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                        float halfWidth = 0.32f * (1f - d) + 0.04f;
                        a = Mathf.Max(a, Mathf.Clamp01((halfWidth - Mathf.Abs(da) * d) / soft + 1f) * Band(d, 0.18f, 0.95f, soft));
                    }
                    a = Mathf.Max(a, Band(d, 0f, 0.12f, soft));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D PillarTexture(int w, int h)
        {
            Texture2D t = NewTex(w, h, "TR_SoulPillarTex");
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(u)) * Mathf.Pow(1f - v, 1.5f);
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>A 1 x 1 quad lying flat (in x/z, facing up), centred.</summary>
        public static Mesh FlatQuad => flatQuad != null ? flatQuad : flatQuad = Quad(true);
        /// <summary>A 1 x 1 quad standing in x/y, centred (turned to the camera by <see cref="Billboard"/>).</summary>
        public static Mesh StandingQuad => billboardQuad != null ? billboardQuad : billboardQuad = Quad(false);

        private static Mesh Quad(bool flat)
        {
            var m = new Mesh { name = flat ? "TR_FlatQuad" : "TR_StandingQuad" };
            m.vertices = flat
                ? new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) }
                : new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A quad with a material and colour (vertex colours drive Sprites/Default, so colours go through a property block).</summary>
        public static MeshRenderer QuadObject(string name, Transform parent, Mesh mesh, Material mat, Color color, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetColor(r, color);
            return r;
        }

        private static MaterialPropertyBlock block;

        public static void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }

        /// <summary>A soft glow that always faces the camera.</summary>
        public static MeshRenderer GlowBillboard(string name, Transform parent, Color color, float size)
        {
            MeshRenderer r = QuadObject(name, parent, StandingQuad, SoftGlow, color, size);
            r.gameObject.AddComponent<Billboard>();
            return r;
        }

        // ------------------------------------------------------------------ particles

        /// <summary>A looping particle system of soft glows (local space unless <paramref name="world"/>).</summary>
        public static ParticleSystem Particles(string name, Transform parent, Color color, bool world)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startColor = color;
            main.maxParticles = 200;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SoftGlow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>The soul fire in the urn's mouth: soft flames rising; <see cref="SetFire"/> scales it with the fill.</summary>
        public static ParticleSystem SoulFire(Transform parent, Vector3 mouth)
        {
            ParticleSystem ps = Particles("TR_SoulFire", parent, Soul, false);
            ps.transform.position = mouth;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // upwards
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            SetFire(ps, 0f);
            ps.Play();
            return ps;
        }

        public static void SetFire(ParticleSystem ps, float fill)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTime = 3f + 40f * fill;
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f + 0.08f * fill, 0.16f + 0.16f * fill);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f + 0.3f * fill, 0.45f + 0.5f * fill);
        }

        /// <summary>A one-shot burst of soft glows from a point.</summary>
        public static void GlowBurst(Vector3 at, Color color, int count, float speed, float size, float lifetime, float lift)
        {
            var holder = new GameObject("TR_SoulBurst");
            holder.transform.position = at;
            ParticleSystem ps = Particles("Burst", holder.transform, color, true);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.gravityModifier = -lift;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.08f;
            limit.limit = speed * 0.3f;
            ps.Play();
            UnityEngine.Object.Destroy(holder, lifetime + 0.5f);
        }

        // ------------------------------------------------------------------ souls

        /// <summary>Sends a soul from a slain monster into the urn's mouth; <paramref name="onArrive"/> runs as it enters.</summary>
        public static void LaunchSoul(Vector3 from, Vector3 mouth, Action onArrive)
        {
            var go = new GameObject("TR_Soul");
            go.transform.position = from + Vector3.up * 0.3f;
            var w = go.AddComponent<SoulWisp>();
            Vector3 flat = mouth - from;
            flat.y = 0f;
            w.Init(from, mouth, SoulUrnLogic.FlightTime(flat.magnitude) * UnityEngine.Random.Range(0.9f, 1.15f), UnityEngine.Random.Range(0f, 6.28f), onArrive);
            GlowBurst(from + Vector3.up * 0.3f, Soul, 5, 0.6f, 0.12f, 0.6f, 0.15f);
        }

        /// <summary>A soul entering the mouth: a small flash of light.</summary>
        public static void Absorb(Vector3 mouth) => GlowBurst(mouth, SoulCore, 6, 1.1f, 0.12f, 0.45f, 0.3f);

        /// <summary>
        /// The urn bursting: glaze and bronze shards fly, a burst of soul light rises, a shock ring runs over the ground and
        /// a beam of light shoots up.
        /// </summary>
        public static void UrnBurst(MonoBehaviour host, Vector3 ground, Vector3 mouth)
        {
            Fx.VoxelBurst(mouth - Vector3.up * 0.45f, new[]
            {
                new Color(0.23f, 0.29f, 0.43f), new Color(0.33f, 0.42f, 0.58f), new Color(0.77f, 0.61f, 0.28f), Soul,
            }, 40, 0.09f, 3.6f, 1.3f, 9.81f);
            GlowBurst(mouth, Soul, 60, 4.5f, 0.22f, 1.5f, 0.6f);
            GlowBurst(mouth, SoulCore, 25, 2f, 0.3f, 0.8f, 1f);
            host.StartCoroutine(ShockRing(ground + Vector3.up * 0.06f));
            host.StartCoroutine(Beam(ground));
        }

        private static IEnumerator ShockRing(Vector3 centre)
        {
            var go = new GameObject("TR_SoulRing");
            go.transform.position = centre;
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Fx.GlowMaterial;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 48;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            const float length = 0.7f;
            for (float t = 0f; t < length && go != null; t += Time.deltaTime)
            {
                float u = t / length, r = Mathf.Lerp(0.3f, 3f, 1f - (1f - u) * (1f - u));
                for (int i = 0; i < line.positionCount; i++)
                {
                    float a = i * Mathf.PI * 2f / line.positionCount;
                    line.SetPosition(i, centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
                line.startWidth = line.endWidth = Mathf.Lerp(0.16f, 0.03f, u);
                Color c = Soul;
                c.a = 0.9f * (1f - u);
                line.startColor = line.endColor = c;
                yield return null;
            }
            if (go != null) UnityEngine.Object.Destroy(go);
        }

        private static IEnumerator Beam(Vector3 ground)
        {
            var root = new GameObject("TR_SoulBeam").transform;
            root.position = ground;
            var quads = new MeshRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                quads[i] = QuadObject("Beam", root, StandingQuad, Pillar, Soul, 1f);
                quads[i].transform.localRotation = Quaternion.Euler(0f, 45f + 90f * i, 0f);
            }
            const float length = 0.8f, height = 4f;
            for (float t = 0f; t < length && root != null; t += Time.deltaTime)
            {
                float u = t / length;
                float w = Mathf.Lerp(0.9f, 0.15f, u), h = height * Mathf.Min(1f, u * 5f);
                foreach (MeshRenderer q in quads)
                {
                    q.transform.localScale = new Vector3(w, h, 1f);
                    q.transform.localPosition = Vector3.up * (h / 2f);
                    Color c = SoulCore;
                    c.a = 0.85f * (1f - u);
                    SetColor(q, c);
                }
                yield return null;
            }
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
        }

        /// <summary>The souls whirling into the urn just before it bursts: glows spiralling in round the mouth.</summary>
        public static void Swirl(Vector3 mouth, float seconds)
        {
            var holder = new GameObject("TR_SoulSwirl");
            holder.transform.position = mouth;
            ParticleSystem ps = Particles("Swirl", holder.transform, Soul, false);
            var main = ps.main;
            main.loop = false;
            main.duration = seconds;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            var em = ps.emission;
            em.rateOverTime = 60f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.9f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f); // flat
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(6f);
            vel.radial = new ParticleSystem.MinMaxCurve(-1.6f);
            vel.y = new ParticleSystem.MinMaxCurve(0.4f);
            vel.x = vel.z = new ParticleSystem.MinMaxCurve(0f);
            ps.Play();
            UnityEngine.Object.Destroy(holder, seconds + 1f);
        }

        /// <summary>Gold popping out of the ground where the urn stood.</summary>
        public static void Coins(Vector3 at) =>
            Fx.VoxelBurst(at, new[] { new Color(1f, 0.84f, 0.3f), new Color(0.95f, 0.72f, 0.2f), new Color(1f, 0.95f, 0.6f) }, 30, 0.08f, 3.2f, 1.2f, 9.81f);
    }

    /// <summary>Turns a quad to face the camera.</summary>
    internal sealed class Billboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }

    /// <summary>
    /// A soul in flight (<see cref="SoulUrnLogic.SoulPoint"/>): a soft glowing head with a bright core and a fading trail.
    /// At the mouth it calls back, hides the head and lets the trail fade out.
    /// </summary>
    internal sealed class SoulWisp : MonoBehaviour
    {
        private Vector3 from, mouth;
        private float duration, time, phase;
        private Action onArrive;
        private TrailRenderer trail;
        private Transform head;
        private bool arrived;

        public void Init(Vector3 from, Vector3 mouth, float duration, float phase, Action onArrive)
        {
            this.from = from;
            this.mouth = mouth;
            this.duration = Mathf.Max(0.2f, duration);
            this.phase = phase;
            this.onArrive = onArrive;
            head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            SoulFx.GlowBillboard("Halo", head, new Color(SoulFx.Soul.r, SoulFx.Soul.g, SoulFx.Soul.b, 0.8f), 0.5f);
            SoulFx.GlowBillboard("Core", head, SoulFx.SoulCore, 0.2f);
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Fx.GlowMaterial;
            trail.time = 0.35f;
            trail.minVertexDistance = 0.04f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(SoulFx.SoulCore, 0f), new GradientColorKey(SoulFx.Soul, 1f) },
                      new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        private void Update()
        {
            if (arrived) return;
            time += Time.deltaTime;
            float t = time / duration;
            transform.position = SoulUrnLogic.SoulPoint(from, mouth, t, phase);
            if (head != null) head.localScale = Vector3.one * (0.85f + 0.15f * Mathf.Sin(time * 18f + phase));
            if (t < 1f) return;
            arrived = true;
            if (head != null) Destroy(head.gameObject);
            trail.emitting = false;
            Destroy(gameObject, trail.time + 0.05f);
            try { onArrive?.Invoke(); }
            catch (Exception e) { Plugin.Log.LogError($"Soul arrival failed: {e}"); }
        }
    }
}
