using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions.Phase2;
using TreasureRun.Audio;
using TreasureRun.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>The black hole's damage and the core's effects, taken when it fires (the core may be gone by the end).</summary>
    internal struct HoleStats
    {
        public PooledDamage Pool;
        public float Slow, Bleed, Burn, Poison, Crit, Stun;
        /// <summary>The core's attack speed (1 = normal): the hole ticks that much faster.</summary>
        public float Speed;
    }

    /// <summary>
    /// A black hole: opens where the singularity lands, catches every monster within reach (on the ground plane) while it
    /// is open, lifts each one into its own orbit slot in the accretion disc, ticks damage, then collapses and bursts: a
    /// final hit and a throw that sends every caught monster 1 to 3 units away. Caught monsters' pathfinding is held
    /// (<see cref="Held"/>, a Pathfinder.FixedUpdate prefix); afterwards they walk straight back to their waypoint.
    /// <para>
    /// Look (seen from the game's tilted camera, after the classic renderings of a lensed disc): a black shadow with a
    /// thin photon ring; the near half of the accretion disc crosses in front of it, while light from the far half is
    /// bent up over the top of the shadow into a wide arc (and a thin secondary image under it). The side turning
    /// towards the camera is brighter and whiter (Doppler beaming), the other dimmer and redder. Hot white-yellow inside,
    /// orange to red outside, with violet streaks. Around it the scene itself is lensed (<see cref="SpaceWarp"/>).
    /// </para>
    /// </summary>
    internal sealed class BlackHole : MonoBehaviour
    {
        internal static readonly HashSet<Pathfinder> Held = new HashSet<Pathfinder>();
        internal static readonly List<BlackHole> Active = new List<BlackHole>();
        /// <summary>Monsters caught by black holes so far, and tick and burst hits (scenarios).</summary>
        internal static int TotalCaught, TotalBursts, TotalTicks;

        /// <summary>Shadow radius; the disc runs from <see cref="DiscInner"/> to <see cref="DiscOuter"/> (world units).</summary>
        public const float Shadow = 0.6f, DiscInner = 0.64f, DiscOuter = 2.1f;

        private sealed class Caught
        {
            public Enemy Enemy;
            public Pathfinder Path;
            public OrbitSlot Slot;
            public Vector3 ThrowFrom, ThrowOffset;
        }

        private readonly List<Caught> caught = new List<Caught>();
        public int CaughtCount => caught.Count;
        /// <summary>Closest distance between two caught monsters right now (scenarios: they must not stack).</summary>
        public float MinSpacing()
        {
            float best = float.MaxValue;
            for (int i = 0; i < caught.Count; i++)
                for (int j = i + 1; j < caught.Count; j++)
                    if (caught[i].Enemy != null && caught[j].Enemy != null)
                        best = Mathf.Min(best, Vector3.Distance(caught[i].Enemy.transform.position, caught[j].Enemy.transform.position));
            return best;
        }

        /// <summary>Debug: what the disc meshes hold right now.</summary>
        internal string DiscInfo()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (name, mesh) in new[] { ("far", farMesh), ("near", nearMesh), ("farGlow", farGlowMesh) })
            {
                MeshFilter f = GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x => x.sharedMesh == mesh);
                MeshRenderer r = f != null ? f.GetComponent<MeshRenderer>() : null;
                sb.Append($"{name}: {mesh.vertexCount} verts, {mesh.triangles.Length / 3} tris, bounds {mesh.bounds.center} {mesh.bounds.size}, " +
                          $"go {(f != null ? f.transform.position.ToString() : "none")} active {(f != null && f.gameObject.activeInHierarchy)} " +
                          $"visible {(r != null && r.isVisible)} layer {(f != null ? f.gameObject.layer : -1)} queue {(r != null ? r.sharedMaterial.renderQueue : -1)}; ");
            }
            if (farColors.Count > 0) sb.Append($"far colours {farColors[5 * (DiscSegments + 1) + 10]} {farColors[2 * (DiscSegments + 1) + 40]}; near {nearColors[3 * (DiscSegments + 1) + 24]}");
            return sb.ToString();
        }

        /// <summary>Camera shake (share of the screen height) this hole asks for right now (<see cref="ScreenShake"/>).</summary>
        public float ShakeAmplitude { get; private set; }
        /// <summary>The hole's middle (in the disc plane, <see cref="ParticleAcceleratorLogic.Lift"/> above the ground).</summary>
        public Vector3 Middle => centre + Vector3.up * ParticleAcceleratorLogic.Lift;

        private HoleStats stats;
        private Vector3 centre;
        private float age, tickTimer, scanTimer, spin;
        private int slots;
        private bool burst, released;
        private SoundLoop rumble;

        // Visuals: lensed far side + secondary image (dynamic mesh), shadow, photon ring, near half of the disc (dynamic
        // mesh), motes, glow, ground well, light, warp; after the burst a spark, halo and shock ring.
        // Each half of the disc twice: a dark smoky underlay (blended) and the same disc as light (additive) on top, so its
        // colours glow whatever the ground behind it looks like.
        private Mesh farMesh, nearMesh, farGlowMesh, nearGlowMesh;
        private readonly List<Color> glowColors = new List<Color>();
        private readonly List<Vector3> farVerts = new List<Vector3>(), nearVerts = new List<Vector3>();
        private readonly List<Color> farColors = new List<Color>(), nearColors = new List<Color>();
        private Transform shadow, photon, glow, spark, halo, well, shock;
        private Material farMat, nearMat, farGlowMat, nearGlowMat, shadowMat, photonMat, glowMat, sparkMat, haloMat, wellMat, shockMat;
        private Light light_;
        private QuadBatch motes;
        private readonly List<(float r, float a, float size, int color)> moteState = new List<(float, float, float, int)>();
        private WarpLens lens;

        private const int DiscRings = 16, DiscSegments = 96, SecondRings = 3;
        private static readonly Color[] MoteColors =
        {
            new Color(1f, 0.97f, 0.8f), new Color(1f, 0.78f, 0.3f), new Color(1f, 0.45f, 0.15f), new Color(0.95f, 0.2f, 0.2f),
            new Color(0.8f, 0.4f, 1f), new Color(1f, 1f, 1f),
        };

        /// <summary>The core's attack speed when it fired (the hole ticks that much faster).</summary>
        public float Speed => stats.Speed > 0f ? stats.Speed : 1f;

        public static BlackHole Open(Vector3 at, HoleStats stats)
        {
            var go = new GameObject("TR_BlackHole");
            go.transform.position = at;
            var h = go.AddComponent<BlackHole>();
            h.centre = at;
            h.stats = stats;
            h.Build();
            Active.Add(h);
            return h;
        }

        private void Build()
        {
            const int L = AccelFx.HoleLayer;
            well = AccelFx.MeshObject("Well", transform, AccelFx.Disc(0f, 3.3f, 48, 6, (r, a) =>
                new Color(0.05f, 0f, 0.1f, 0.6f * (1f - r) * (1f - r))), out wellMat, 2995, L).transform;
            well.localPosition = Vector3.up * 0.03f;

            glow = AccelFx.Billboard("HoleGlow", transform, AccelFx.Dot, new Color(0.45f, 0.1f, 0.6f, 0.16f), 4.2f, out glowMat, 2999, L).transform;
            farMesh = DynamicMesh("Far", out farMat, 3001, false);
            farGlowMesh = DynamicMesh("FarGlow", out farGlowMat, 3002, true);
            shadow = AccelFx.Billboard("Shadow", transform, AccelFx.DiskTex, Color.black, 2f * Shadow, out shadowMat, 3003, L).transform;
            photon = AccelFx.Billboard("Photon", transform, AccelFx.RingGlow, new Color(1f, 0.9f, 0.75f, 1f), 2f * 1.07f * Shadow / 0.47f, out photonMat, 3004, L, true).transform;
            nearMesh = DynamicMesh("Near", out nearMat, 3005, false);
            nearGlowMesh = DynamicMesh("NearGlow", out nearGlowMat, 3006, true);
            farMat.color = nearMat.color = new Color(0.1f, 0.015f, 0.04f, 1f); // the underlay: dark, smoky
            spark = AccelFx.Billboard("Spark", transform, AccelFx.Dot, Color.white, 1f, out sparkMat, 3008, L).transform;
            spark.localScale = Vector3.zero;
            halo = AccelFx.Billboard("Halo", transform, AccelFx.RingTex, new Color(1f, 0.85f, 0.7f, 0f), 1f, out haloMat, 3008, L).transform;
            foreach (Transform t in new[] { glow, shadow, photon, spark, halo }) t.localPosition = Vector3.up * ParticleAcceleratorLogic.Lift;

            var lg = new GameObject("TR_HoleLight");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = Vector3.up * (ParticleAcceleratorLogic.Lift + 0.4f);
            light_ = lg.AddComponent<Light>();
            light_.type = LightType.Point;
            light_.color = new Color(1f, 0.6f, 0.35f);
            light_.range = 5f;
            light_.intensity = 0f;

            motes = new QuadBatch("TR_HoleMotes", AccelFx.DotGlow, 3007, L, additive: true);
            for (int i = 0; i < 150; i++) moteState.Add(NewMote(Random.Range(DiscInner, DiscOuter + 0.6f)));
            lens = new WarpLens { Centre = Middle, Einstein = 0f, Outer = 3f, Floor = ParticleAcceleratorLogic.Lift + 0.15f };

            // The sound plays at the hole, loud (full volume out to 30 units, so the camera's height doesn't fade it).
            // Four identical sources in sync: up to +12 dB over one (the rumble asked for +10 dB after playtest).
            rumble = SoundLoop.Start("ascensions/particle_accelerator/blackhole_rumble", transform, 3.2f, 30f, 4);
        }

        private Mesh DynamicMesh(string name, out Material material, int queue, bool additive)
        {
            var mesh = new Mesh { name = "TR_Hole" + name };
            mesh.MarkDynamic();
            // World-space vertices: the object sits at the origin (a child only so it goes with the hole).
            Transform t = AccelFx.MeshObject("Hole" + name, null, mesh, out material, queue, AccelFx.HoleLayer, additive).transform;
            t.SetParent(transform, true);
            return mesh;
        }

        private static (float, float, float, int) NewMote(float r) =>
            (r, Random.Range(0f, Mathf.PI * 2f), Random.Range(0.03f, 0.065f), Random.Range(0, MoteColors.Length));

        private void OnDestroy()
        {
            Release();
            Active.Remove(this);
            motes?.Destroy();
            lens?.Destroy();
            foreach (Material m in new[] { farMat, nearMat, farGlowMat, nearGlowMat, shadowMat, photonMat, glowMat, sparkMat, haloMat, wellMat, shockMat })
                if (m != null) Destroy(m);
            if (farMesh != null) Destroy(farMesh);
            if (nearMesh != null) Destroy(nearMesh);
            if (farGlowMesh != null) Destroy(farGlowMesh);
            if (nearGlowMesh != null) Destroy(nearGlowMesh);
            if (rumble != null) Destroy(rumble.gameObject);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            const float Hole = ParticleAcceleratorLogic.HoleSeconds;
            if (!burst)
            {
                Catch(dt);
                Hold();
                Tick(dt);
                Animate(dt);
                if (age >= Hole) Burst();
                return;
            }
            float since = age - Hole, t = since / ParticleAcceleratorLogic.ThrowSeconds;
            if (!released)
            {
                foreach (Caught c in caught)
                    if (c.Enemy != null) c.Enemy.transform.position = ParticleAcceleratorLogic.ThrowPoint(c.ThrowFrom, c.ThrowOffset, t);
                if (t >= 1f) Release();
            }
            Afterglow(since);
            if (age >= Hole + 1.3f) Destroy(gameObject);
        }

        // ------------------------------------------------------------------ mechanics

        /// <summary>Catches every living monster in reach that no other hole holds (every 0.1 s).</summary>
        private void Catch(float dt)
        {
            scanTimer -= dt;
            if (scanTimer > 0f || SpawnManager.instance == null) return;
            scanTimer = 0.1f;
            foreach (Enemy e in SpawnManager.instance.currentEnemies)
            {
                if (!Combat.IsAlive(e) || !ParticleAcceleratorLogic.InReach(centre, e.transform.position)) continue;
                Pathfinder p = e.GetComponent<Pathfinder>();
                if (p == null || Held.Contains(p)) continue;
                Held.Add(p);
                caught.Add(new Caught { Enemy = e, Path = p, Slot = ParticleAcceleratorLogic.Slot(slots++, GameRng.Instance) });
                TotalCaught++;
            }
        }

        /// <summary>Pulls every caught monster up into its slot in the disc.</summary>
        private void Hold()
        {
            float dt = Time.deltaTime;
            for (int i = caught.Count - 1; i >= 0; i--)
            {
                Caught c = caught[i];
                if (!Combat.IsAlive(c.Enemy))
                {
                    if (c.Path != null) Held.Remove(c.Path);
                    caught.RemoveAt(i);
                    continue;
                }
                Vector3 target = ParticleAcceleratorLogic.OrbitPoint(centre, c.Slot, age) + Vector3.up * ParticleAcceleratorLogic.Lift;
                c.Enemy.transform.position = ParticleAcceleratorLogic.Pull(c.Enemy.transform.position, target, dt);
            }
        }

        private void Tick(float dt)
        {
            int ticks = FireClock.Due(ref tickTimer, dt, ParticleAcceleratorLogic.TickInterval(stats.Speed > 0f ? stats.Speed : 1f), 8);
            int dmg = ParticleAcceleratorLogic.TickDamage(stats.Pool.Base);
            for (int k = 0; k < ticks; k++)
                foreach (Caught c in caught.ToList())
                {
                    Hit(c.Enemy, dmg);
                    TotalTicks++;
                }
        }

        private void Hit(Enemy e, int dmg)
        {
            if (!Combat.IsAlive(e) || dmg <= 0) return;
            PooledDamage p = stats.Pool;
            e.TakeDamage(TowerType.ParticleCannon, dmg, p.Health, p.Armor, p.Shield, stats.Slow, stats.Bleed, stats.Burn, stats.Poison, stats.Crit, stats.Stun);
        }

        /// <summary>The supernova: the last hit, then every caught monster is thrown away (over 0.4 s).</summary>
        private void Burst()
        {
            burst = true;
            int dmg = ParticleAcceleratorLogic.BurstDamage(stats.Pool.Base);
            foreach (Caught c in caught.ToList())
            {
                Hit(c.Enemy, dmg);
                TotalBursts++;
            }
            for (int i = caught.Count - 1; i >= 0; i--)
            {
                Caught c = caught[i];
                if (!Combat.IsAlive(c.Enemy)) { if (c.Path != null) Held.Remove(c.Path); caught.RemoveAt(i); continue; }
                c.ThrowFrom = c.Enemy.transform.position;
                c.ThrowOffset = LandingOffset(c.ThrowFrom);
            }
            if (rumble != null) { Destroy(rumble.gameObject); rumble = null; }
            // The rumble stops dead at its loudest; only a quiet smoky puff is left.
            SoundLibrary.PlayLoud("ascensions/particle_accelerator/blackhole_collapse", Middle, 1f, 30f);

            // The flash: the hole goes, a shock ring runs over the ground, sparks fly, space ripples.
            foreach (Transform t in new[] { shadow, photon, glow })
                t.gameObject.SetActive(false);
            farMesh.Clear();
            nearMesh.Clear();
            farGlowMesh.Clear();
            nearGlowMesh.Clear();
            motes.Clear();
            Vector3 mid = Middle;
            Fx.Burst(mid, new Color(1f, 0.65f, 0.3f), 60, 0.16f, 6f);
            Fx.Burst(mid, new Color(0.85f, 0.55f, 1f), 50, 0.14f, 5f);
            Fx.Burst(mid, Color.white, 30, 0.1f, 3.5f);
            shock = AccelFx.MeshObject("Shock", transform, AccelFx.Disc(0.84f, 1f, 96, 2, (r, a) =>
                new Color(1f, 0.9f, 0.8f, Mathf.Sin(r * Mathf.PI))), out shockMat, 3006, AccelFx.HoleLayer).transform;
            shock.localPosition = Vector3.up * 0.06f;
            lens.Einstein = 0f;
            lens.RippleRadius = 0f;
        }

        /// <summary>
        /// A random throw (1 to 3 units) that lands on the map: path preferred, else grass at about the same height; never
        /// over the edge, into water or onto a tower. Up to 12 tries; without a landing the monster drops straight down.
        /// </summary>
        private Vector3 LandingOffset(Vector3 from)
        {
            int grass = LayerMask.NameToLayer("Grass"), path = LayerMask.NameToLayer("Path");
            int mask = ~((1 << LayerMask.NameToLayer("Enemy")) | (1 << AccelFx.HoleLayer));
            Vector3? onGrass = null;
            for (int i = 0; i < 12; i++)
            {
                Vector3 o = ParticleAcceleratorLogic.ThrowOffset(GameRng.Instance);
                Vector3 at = from + o;
                if (!Physics.Raycast(new Vector3(at.x, from.y + 20f, at.z), Vector3.down, out RaycastHit hit, 40f, mask, QueryTriggerInteraction.Ignore))
                    continue;
                int layer = hit.collider.gameObject.layer;
                if ((layer != path && layer != grass) || Mathf.Abs(hit.point.y - centre.y) > 0.7f) continue;
                o.y = hit.point.y - from.y;
                if (layer == path) return o;
                if (onGrass == null) onGrass = o;
            }
            return onGrass ?? new Vector3(0f, centre.y - from.y, 0f);
        }

        /// <summary>Gives the caught monsters back to their pathfinding, with the distance to the end recomputed.</summary>
        private void Release()
        {
            if (released) return;
            released = true;
            foreach (Caught c in caught)
            {
                if (c.Path == null) continue;
                Held.Remove(c.Path);
                if (c.Enemy == null || c.Path.currentWaypoint == null) continue;
                c.Path.distanceFromEnd = c.Path.currentWaypoint.distanceFromEnd +
                                         Vector3.Distance(c.Enemy.transform.position, c.Path.currentWaypoint.transform.position);
            }
        }

        // ------------------------------------------------------------------ look

        /// <summary>Angular speed of the disc's pattern at radius r: faster inside, like a real disc.</summary>
        private static float Omega(float r) => 2.2f * Mathf.Pow(DiscInner / Mathf.Max(0.3f, r), 1.5f);

        /// <summary>
        /// Colour of the disc at r01 (0 inner edge .. 1 outer edge) and angle θ (0 = screen right, π/2 = far, 3π/2 = near):
        /// temperature from white-yellow to red, turbulent streaks winding with the rotation, violet streaks, and
        /// Doppler beaming (the side moving towards the camera, on the left, brighter and whiter).
        /// </summary>
        private Color DiscColor(float r01, float r, float theta, float fade)
        {
            float phi = theta - Omega(r) * spin;
            // Thin bright filaments with darker lanes between them, winding with the differential rotation.
            float fil = Mathf.Clamp01(0.5f + 0.5f * (0.5f * Mathf.Sin(9f * phi + 14f * r) + 0.3f * Mathf.Sin(5f * phi - 9f * r + 1.7f)
                                                     + 0.2f * Mathf.Sin(15f * phi + 26f * r + 0.6f)));
            fil *= fil;
            Color hot = r01 < 0.15f ? Color.Lerp(new Color(1f, 0.98f, 0.85f), new Color(1f, 0.84f, 0.32f), r01 / 0.15f)
                      : r01 < 0.4f ? Color.Lerp(new Color(1f, 0.84f, 0.32f), new Color(1f, 0.5f, 0.1f), (r01 - 0.15f) / 0.25f)
                      : r01 < 0.7f ? Color.Lerp(new Color(1f, 0.5f, 0.1f), new Color(0.88f, 0.18f, 0.08f), (r01 - 0.4f) / 0.3f)
                      : Color.Lerp(new Color(0.88f, 0.18f, 0.08f), new Color(0.5f, 0.04f, 0.14f), (r01 - 0.7f) / 0.3f);
            float violet = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(4f * phi + 11f * r + 2.1f)), 4f);
            Color lane = Color.Lerp(new Color(0.55f, 0.06f, 0.12f), new Color(0.48f, 0.14f, 0.75f), violet);
            Color c = Color.Lerp(lane, hot, 0.25f + 0.75f * fil);
            // Doppler beaming: the side turning towards the camera (left) hot and white, the other deep red.
            float doppler = 1f + 0.7f * -Mathf.Cos(theta);
            c = Color.Lerp(c, new Color(1f, 0.97f, 0.85f), Mathf.Clamp01(doppler - 1f) * 0.5f);
            c = Color.Lerp(c, new Color(0.5f, 0.03f, 0.08f), Mathf.Clamp01(1f - doppler) * 0.6f);
            float inner = Mathf.Clamp01(r01 / 0.03f), outer = Mathf.Clamp01((1f - r01) / 0.18f);
            c.a = Mathf.Clamp01(inner * outer * (0.55f + 0.45f * fil) * Mathf.Clamp(0.6f + 0.5f * doppler, 0f, 1.2f) * fade);
            return c;
        }

        /// <summary>The camera's basis: right and up on screen, and the ground directions under them.</summary>
        private static bool CameraBasis(out Vector3 right, out Vector3 up, out Vector3 rightFlat, out Vector3 awayFlat, out float tilt)
        {
            Camera cam = Camera.main;
            right = up = rightFlat = awayFlat = Vector3.zero;
            tilt = 0.6f;
            if (cam == null) return false;
            right = cam.transform.right;
            up = cam.transform.up;
            rightFlat = new Vector3(right.x, 0f, right.z).normalized;
            Vector3 f = cam.transform.forward;
            awayFlat = new Vector3(f.x, 0f, f.z).normalized;
            tilt = Mathf.Clamp(Vector3.Dot(awayFlat, up), 0.2f, 1f); // how much "away" shows as "up" on screen
            return true;
        }

        /// <summary>
        /// Rebuilds the disc for this frame. The near half (θ in π..2π) lies flat in the world, in the plane of the caught
        /// monsters. The far half is drawn where its light arrives: bent up over the shadow into an arc whose inner edge
        /// hugs the photon ring (height H(r) above the middle instead of r·tilt), plus a thin dim secondary image under
        /// the shadow. Both halves meet at the disc's left and right ends.
        /// </summary>
        private void DrawDisc(float scale, float fade)
        {
            if (!CameraBasis(out Vector3 right, out Vector3 up, out Vector3 rightFlat, out Vector3 awayFlat, out float tilt)) return;
            Vector3 mid = Middle;
            float rs = Shadow * scale;
            farVerts.Clear(); farColors.Clear(); nearVerts.Clear(); nearColors.Clear();
            for (int ri = 0; ri <= DiscRings; ri++)
            {
                float r01 = ri / (float)DiscRings, r = Mathf.Lerp(DiscInner, DiscOuter, r01) * scale;
                float lifted = Mathf.Max(r * tilt, rs * 1.12f + (r - DiscInner * scale) * 0.5f);
                for (int s = 0; s <= DiscSegments; s++)
                {
                    float u = s / (float)DiscSegments;
                    // Far half, lensed up over the shadow.
                    float th = u * Mathf.PI, sn = Mathf.Sin(th);
                    farVerts.Add(mid + right * (Mathf.Cos(th) * r) + up * (Mathf.Pow(Mathf.Max(0f, sn), 0.8f) * lifted));
                    farColors.Add(DiscColor(r01, r / scale, th, fade));
                    // Near half, flat in the world.
                    float tn = Mathf.PI + u * Mathf.PI;
                    nearVerts.Add(mid + rightFlat * (Mathf.Cos(tn) * r) + awayFlat * (Mathf.Sin(tn) * r));
                    nearColors.Add(DiscColor(r01, r / scale, tn, fade));
                }
            }
            // Secondary image: the far half's inner edge, seen under the shadow (thin and dim).
            for (int ri = 0; ri <= SecondRings; ri++)
            {
                float r01 = ri / (float)SecondRings * 0.25f, rho = rs * (1.06f + r01 * 0.5f);
                for (int s = 0; s <= DiscSegments; s++)
                {
                    float th = s / (float)DiscSegments * Mathf.PI;
                    farVerts.Add(mid + right * (Mathf.Cos(th) * rho * 1.02f) - up * (Mathf.Pow(Mathf.Max(0f, Mathf.Sin(th)), 0.8f) * rho));
                    Color c = DiscColor(r01, DiscInner, th, fade);
                    c.a *= 0.55f * (1f - ri / (float)SecondRings);
                    farColors.Add(c);
                }
            }
            var farGrids = new[] { (DiscRings, 0), (SecondRings, (DiscRings + 1) * (DiscSegments + 1)) };
            var nearGrids = new[] { (DiscRings, 0) };
            Fill(farMesh, farVerts, farColors, farGrids);
            Fill(farGlowMesh, farVerts, Glowing(farColors), farGrids);
            Fill(nearMesh, nearVerts, nearColors, nearGrids);
            Fill(nearGlowMesh, nearVerts, Glowing(nearColors), nearGrids);
        }

        /// <summary>The disc's colours as light for the additive pass (premultiplied by their alpha).</summary>
        private List<Color> Glowing(List<Color> colors)
        {
            glowColors.Clear();
            foreach (Color c in colors) glowColors.Add(new Color(c.r * c.a, c.g * c.a, c.b * c.a, 1f));
            return glowColors;
        }

        private static readonly List<int> tris = new List<int>();

        private static void Fill(Mesh mesh, List<Vector3> verts, List<Color> colors, (int rows, int offset)[] grids)
        {
            bool first = mesh.vertexCount != verts.Count;
            if (first) mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            if (first)
            {
                tris.Clear();
                foreach (var (rows, offset) in grids) AccelFx.GridTriangles(tris, rows, DiscSegments, offset);
                mesh.SetTriangles(tris, 0);
            }
            mesh.RecalculateBounds();
        }

        private void Animate(float dt)
        {
            float form = Mathf.Clamp01(age / ParticleAcceleratorLogic.FormSeconds);
            float grow = 1f - (1f - form) * (1f - form) * (1f - form);
            float k = ParticleAcceleratorLogic.Collapse(age), kk = k * k;
            float scale = grow * (1f - 0.7f * kk);
            spin += dt * (1f + 4f * kk);
            DrawDisc(scale, Mathf.Clamp01(form * 1.5f));

            shadow.localScale = Vector3.one * 2f * Shadow * grow * (1f - 0.85f * kk);
            photon.localScale = Vector3.one * (2f * 1.07f * Shadow / 0.47f) * grow * (1f - 0.8f * kk);
            photonMat.color = Color.Lerp(new Color(1f, 0.9f, 0.75f, 1f), Color.white, k) * grow;
            glow.localScale = Vector3.one * 4.2f * grow * (1f - 0.5f * kk) * (1f + 0.05f * Mathf.Sin(age * 6f));
            spark.localScale = Vector3.one * 1.3f * kk;
            light_.intensity = Mathf.Lerp(1.5f, 6f, kk) * grow;
            wellMat.color = new Color(1f, 1f, 1f, grow * (1f - 0.5f * k));
            foreach (Transform t in new[] { shadow, photon, glow, spark }) AccelFx.Face(t);
            lens.Centre = Middle;
            lens.Einstein = 0.8f * grow * (1f - 0.6f * kk);

            DrawMotes(dt, scale, grow, kk);

            // Shake: a rumble as it opens, a low tremor while it holds, building in the collapse.
            ShakeAmplitude = 0.006f * Mathf.Exp(-age * 5f) + 0.0012f * grow + 0.005f * kk;
            if (rumble != null)
            {
                // Entry: higher and loud, falling within half a second to the deep rumble. The collapse swells sharply in
                // volume and drops in pitch right up to the burst, where it is cut off.
                float fall = Mathf.Clamp01(age / 0.55f), ease = 1f - (1f - fall) * (1f - fall);
                rumble.Pitch = Mathf.Lerp(1.5f, 0.72f, ease) - 0.34f * kk;
                rumble.Volume = (Mathf.Lerp(1f, 0.7f, ease) + 0.3f * k * kk) * 3.2f;
            }
        }

        /// <summary>
        /// Small bright motes spiralling in along the disc (reds, yellows, violets, white). Like the disc, a mote on the far
        /// half shows where its light arrives (on the lensed arc), one on the near half where it is.
        /// </summary>
        private void DrawMotes(float dt, float scale, float grow, float kk)
        {
            if (!CameraBasis(out Vector3 right, out Vector3 up, out Vector3 rightFlat, out Vector3 awayFlat, out float tilt)) return;
            Vector3 mid = Middle;
            float rs = Shadow * scale;
            motes.Begin();
            for (int i = 0; i < moteState.Count; i++)
            {
                var (r, a, size, ci) = moteState[i];
                r -= (0.25f + 0.45f / Mathf.Max(0.4f, r)) * dt * (1f + 3f * kk);
                a += Omega(r) * 1.3f * dt * (1f + 3f * kk);
                if (r < DiscInner * 0.9f) (r, a, size, ci) = NewMote(Random.Range(DiscOuter, DiscOuter + 0.6f));
                moteState[i] = (r, a, size, ci);
                float rr = r * scale, th = Mathf.Repeat(a, Mathf.PI * 2f);
                Vector3 p;
                if (th < Mathf.PI)
                {
                    float lifted = Mathf.Max(rr * tilt, rs * 1.12f + (rr - DiscInner * scale) * 0.5f);
                    p = mid + right * (Mathf.Cos(th) * rr) + up * (Mathf.Pow(Mathf.Max(0f, Mathf.Sin(th)), 0.8f) * lifted);
                }
                else p = mid + rightFlat * (Mathf.Cos(th) * rr) + awayFlat * (Mathf.Sin(th) * rr);
                Color c = MoteColors[ci];
                c.a = grow * Mathf.Clamp01((DiscOuter + 0.6f - r) * 2f) * (0.75f + 0.25f * -Mathf.Cos(th));
                motes.Add(p, size * (0.8f + 0.4f * Mathf.Clamp01(1f - (r - DiscInner) / (DiscOuter - DiscInner))), c);
            }
            motes.End();
        }

        /// <summary>After the burst: a white spark fading, a halo and a shock ring running out, the light flaring, space rippling.</summary>
        private void Afterglow(float since)
        {
            float k = Mathf.Clamp01(since / 1.3f);
            spark.localScale = Vector3.one * Mathf.Lerp(2.4f, 0f, Mathf.Sqrt(k));
            sparkMat.color = new Color(1f, 0.95f, 0.9f, 1f - k);
            halo.localScale = Vector3.one * Mathf.Lerp(1.8f, 5.5f, k);
            haloMat.color = new Color(1f, 0.8f, 0.65f, (1f - k) * 0.85f);
            AccelFx.Face(halo);
            AccelFx.Face(spark);
            light_.intensity = Mathf.Lerp(8f, 0f, Mathf.Sqrt(k));
            light_.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.3f), k);
            wellMat.color = new Color(1f, 1f, 1f, 0.5f * (1f - k));
            float s = Mathf.Clamp01(since / 0.6f), eased = 1f - (1f - s) * (1f - s);
            shock.localScale = Vector3.one * Mathf.Lerp(0.3f, ParticleAcceleratorLogic.CaptureRadius + 0.6f, eased);
            shockMat.color = new Color(1f, 1f, 1f, 1f - s);
            lens.RippleRadius = Mathf.Lerp(0.3f, 4.5f, eased);
            lens.Ripple = 0.35f * (1f - s);
            // The burst: a short, harder shake.
            ShakeAmplitude = 0.014f * Mathf.Exp(-since * 9f);
        }
    }

    /// <summary>The singularity: a black bead with a violet trail flying from the core's muzzle to where the hole opens.</summary>
    internal sealed class SingularityShot : MonoBehaviour
    {
        private Vector3 from, to;
        private float flight, t;
        private System.Action arrive;
        private Transform halo;
        private Material haloMat;

        public static void Launch(Vector3 from, Vector3 to, float flight, System.Action arrive)
        {
            var go = new GameObject("TR_Singularity");
            go.transform.position = from;
            var s = go.AddComponent<SingularityShot>();
            s.from = from;
            s.to = to + Vector3.up * ParticleAcceleratorLogic.Lift;
            s.flight = flight;
            s.arrive = arrive;
            AccelFx.Sphere("Bead", go.transform, 0.24f);
            s.halo = AccelFx.Billboard("BeadHalo", go.transform, AccelFx.Dot, new Color(0.8f, 0.5f, 1f, 0.9f), 0.75f, out s.haloMat).transform;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Fx.GlowMaterial;
            trail.time = 0.3f;
            trail.startWidth = 0.22f;
            trail.endWidth = 0f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.95f, 0.8f, 1f), 0f), new GradientColorKey(new Color(0.45f, 0.15f, 0.9f), 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void OnDestroy()
        {
            if (haloMat != null) Destroy(haloMat);
        }

        private void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flight);
            transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (0.8f * 4f * k * (1f - k));
            AccelFx.Face(halo);
            if (k < 1f) return;
            arrive?.Invoke();
            arrive = null;
            Destroy(gameObject);
        }
    }
}
