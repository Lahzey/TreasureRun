using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions.Phase2;
using TreasureRun.Audio;
using TreasureRun.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>
    /// A closed ring of Sun Obelisks and the sun's answer. When it closes, a pulse of light runs round the ring and each
    /// prism it reaches turns to the sky and sends a beam straight up; the beams' light gathers into a sun over the ring's
    /// centre, and the sun strikes down: a column of light (<see cref="RitualLogic.Radius"/>, drawn exactly that wide)
    /// that reaches down to the lowest ground under it and envelops whatever stands inside. It hits every monster inside
    /// with the ring's average damage (<see cref="RitualLogic.Average"/>), at the ring's average rate, with 100% burn.
    /// <para>
    /// Easter egg: when the centre falls on a mana crystal's cell, the beam comes down on the crystal and powers it: the
    /// crystal turns gold and a ritual circle <see cref="RitualLogic.CrystalCircleFactor"/> times the beam's radius
    /// glows round it on the ground; the circle is the damage area.
    /// </para>
    /// When the ring breaks, the column draws back up into the sun and the sun fades.
    /// </summary>
    internal sealed class SunRing : MonoBehaviour
    {
        // Timeline after the ring closes (seconds).
        /// <summary>The pulse of light runs once round the ring; each prism turns up as it passes.</summary>
        public const float PulseTime = 0.6f;
        private const float UpBeamGrow = 0.28f, SunStart = 0.55f, SunGrow = 0.45f, StrikeStart = 1.0f;
        /// <summary>The column hits the ground: from here on it deals damage.</summary>
        public const float StrikeTime = 1.12f;
        private const float BreakTime = 0.65f;
        /// <summary>The sun's disc is this many times the column's width.</summary>
        public const float SunToBeam = 1.1f;

        public string Key { get; private set; }
        public readonly List<SunObelisk> Members = new List<SunObelisk>();
        public bool Breaking { get; private set; }
        /// <summary>The column's axis at its foot: the lowest ground under the beam (path level).</summary>
        public Vector3 Centre { get; private set; }
        /// <summary>The column's radius (= the damage radius unless it powers a crystal).</summary>
        public float Radius { get; private set; }
        public float SunHeight { get; private set; }
        /// <summary>The mana crystal the beam comes down on (null: none).</summary>
        public ManaCrystal Crystal { get; private set; }
        /// <summary>The beam powers its crystal: the ritual circle is the damage area.</summary>
        public bool Powered => Crystal != null && struck && !Breaking;
        public float DamageRadius => Powered ? Radius * RitualLogic.CrystalCircleFactor : Radius;
        public bool Active => !Breaking && age >= StrikeTime;
        public RitualBeamStats Stats => stats;
        /// <summary>Damage ticks with at least one monster inside, and hits on monsters (scenarios).</summary>
        public long Ticks { get; private set; }
        public long Hits { get; private set; }
        /// <summary>The last tick had no mana to pay.</summary>
        public bool Starved { get; private set; }
        public HeightField Field => heights;

        private float brokenAt;
        private float age, breakAge, statsTimer, tickTimer, dmgCarry, manaCarry, hitGlow, roarLevel;
        private RitualBeamStats stats;
        private readonly Dictionary<Enemy, float> timeIn = new Dictionary<Enemy, float>();
        private readonly List<Enemy> inside = new List<Enemy>();

        private HeightField heights;
        /// <summary>Where the beam meets whatever is under it (the tops of grass, path, crystals, towers inside it).</summary>
        private List<Vector3> impacts = new List<Vector3>();
        private float impactCheck;
        private LightBeam[] upBeams, skyBeams;
        private Shaft glowShaft, bodyShaft, streakShaft, coreShaft;
        private GameObject sun, raysA, raysB;
        private Material sunMat, raysAMat, raysBMat;
        private CrystalPower power;
        private Light lamp;
        private SoundLoop roar;
        private bool struck;
        private float spin;

        public static string KeyOf(List<SunObelisk> members) =>
            string.Join(",", members.Select(s => s.GetInstanceID()).OrderBy(i => i));

        public int IndexOf(SunObelisk s) => Members.IndexOf(s);

        /// <summary>Starts a ring (members in counter-clockwise order).</summary>
        public static SunRing Ignite(List<SunObelisk> members)
        {
            var poly = members.Select(m => new Vector2(m.transform.position.x, m.transform.position.z)).ToList();
            Vector2 c = RitualLogic.Centre(poly);
            // On a mana crystal's cell the beam comes down on the crystal itself.
            ManaCrystal crystal = Object.FindObjectsOfType<ManaCrystal>()
                .FirstOrDefault(m => m.isActiveAndEnabled && RitualLogic.OnCell(c, new Vector2(m.transform.position.x, m.transform.position.z)));
            if (crystal != null) c = new Vector2(crystal.transform.position.x, crystal.transform.position.z);
            float radius = RitualLogic.Radius(members.Count);
            float reach = Mathf.Max(radius * 2.6f, crystal != null ? radius * RitualLogic.CrystalCircleFactor : 0f) + 0.3f;
            var heights = new HeightField(c, reach, crystal != null ? 0.125f : 0.2f);
            float baseY = heights.LowestGround(c, radius, members.Average(m => m.transform.position.y));

            var go = new GameObject("TR_SunRing");
            go.transform.position = new Vector3(c.x, baseY, c.y);
            SunRing r = go.AddComponent<SunRing>();
            r.Members.AddRange(members);
            r.Key = KeyOf(members);
            r.Centre = go.transform.position;
            r.Radius = radius;
            r.Crystal = crystal;
            r.heights = heights;
            r.SunHeight = 5.2f + 0.5f * radius;
            r.Build();
            SoundLibrary.PlayLoud("ascensions/ritual_of_the_sun/ritual_close", r.Centre, 0.9f, 14f);
            return r;
        }

        public void Break()
        {
            if (Breaking) return;
            Breaking = true;
            breakAge = 0f;
            brokenAt = age;
            if (struck) SoundLibrary.Play("ascensions/ritual_of_the_sun/ritual_break", Centre + Vector3.up, 0.85f, 0.03f, 2);
        }

        /// <summary>The pulse has reached member i: its prism points at the sky.</summary>
        public bool PrismUp(int i) => !Breaking && age >= MemberTime(i);

        private float MemberTime(int i) => PulseTime * i / Mathf.Max(1, Members.Count);

        /// <summary>A flash on member i's prism as the pulse passes it, and on all of them at the strike.</summary>
        public float Flash(int i)
        {
            float d = (age - MemberTime(i)) / 0.09f;
            float s = (age - StrikeTime) / 0.15f;
            return Mathf.Exp(-d * d) + (age >= StrikeTime ? 0.6f * Mathf.Exp(-s * s) : 0f);
        }

        /// <summary>Extra light on a ring link while the pulse runs along it (fading after it passed).</summary>
        public float LinkIgnite(LinkBeam b)
        {
            int n = Members.Count, ia = Members.IndexOf(b.A), ib = Members.IndexOf(b.B);
            if (ia < 0 || ib < 0) return 0f;
            int seg = (ia + 1) % n == ib ? ia : (ib + 1) % n == ia ? ib : -1;
            if (seg < 0) return 0f;
            float t0 = MemberTime(seg), t1 = t0 + PulseTime / n;
            if (age < t0) return 0f;
            return age < t1 ? 1f : Mathf.Exp(-(age - t1) * 3f);
        }

        // ------------------------------------------------------------------ building the visuals

        private void Build()
        {
            int n = Members.Count;
            upBeams = new LightBeam[n];
            skyBeams = new LightBeam[n];
            for (int i = 0; i < n; i++)
            {
                upBeams[i] = new LightBeam("SunUp", transform, SunFx.UpCore, SunFx.UpGlow, 0.06f, 0.26f);
                skyBeams[i] = new LightBeam("SunSky", transform, SunFx.SkyCore, SunFx.SkyGlow, 0.03f, 0.14f);
                upBeams[i].SetVisible(false);
                skyBeams[i].SetVisible(false);
            }
            // The column: nested half-cylinders turned to the camera, each fading towards its silhouette like a volume of light.
            glowShaft = new Shaft("SunShaftGlow", transform, null, 3001, true, 2.5f, 0f);
            bodyShaft = new Shaft("SunShaftBody", transform, null, 3002, false, 0.6f, 0.55f);
            streakShaft = new Shaft("SunShaftStreaks", transform, SunFx.Streaks, 3003, true, 1.2f, 0f);
            coreShaft = new Shaft("SunShaftCore", transform, null, 3004, true, 1.6f, 0f);
            impacts = heights.SurfacePoints(new Vector2(Centre.x, Centre.z), Radius, 60);

            sun = AccelFx.Billboard("SunDisc", transform, SunFx.SunDisc, Color.black, 1f, out sunMat, 3010, 0, true);
            raysA = AccelFx.Billboard("SunRaysA", transform, SunFx.Rays, Color.black, 1f, out raysAMat, 3009, 0, true);
            raysB = AccelFx.Billboard("SunRaysB", transform, SunFx.Rays, Color.black, 1f, out raysBMat, 3009, 0, true);
            foreach (GameObject g in new[] { sun, raysA, raysB }) g.SetActive(false);

            var lampGo = new GameObject("TR_SunLamp");
            lampGo.transform.SetParent(transform, false);
            lampGo.transform.localPosition = Vector3.up * 1.6f;
            lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.78f, 0.45f);
            lamp.range = Radius + 3f;
            lamp.intensity = 0f;
            lamp.shadows = LightShadows.None;
        }

        private void OnDestroy()
        {
            if (upBeams != null) foreach (LightBeam b in upBeams) b.Destroy();
            if (skyBeams != null) foreach (LightBeam b in skyBeams) b.Destroy();
            glowShaft?.Destroy(); bodyShaft?.Destroy(); streakShaft?.Destroy(); coreShaft?.Destroy();
            foreach (Material m in new[] { sunMat, raysAMat, raysBMat }) if (m != null) Destroy(m);
            power?.Destroy();
            if (roar != null) Destroy(roar.gameObject);
        }

        // ------------------------------------------------------------------ every frame

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        /// <summary>Grows past 1 and settles back (a pop).</summary>
        private static float Pop(float t) { t = Mathf.Clamp01(t); return 1f + 2.2f * (t - 1f) * (t - 1f) * (t - 1f) + 1.2f * (t - 1f) * (t - 1f); }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (Breaking)
            {
                breakAge += dt;
                if (breakAge >= BreakTime) { Destroy(gameObject); return; }
            }
            if (!struck && age >= StrikeTime && !Breaking) Strike();
            // A crystal can be removed (a new tile's layout, a scenario): the beam then simply comes down where it is.
            if (Crystal == null && power != null) { power.Destroy(); power = null; }
            // Something built or removed under the beam: where it meets the ground changes.
            impactCheck -= dt;
            if (impactCheck <= 0f && struck)
            {
                impactCheck = 2f;
                heights.Refresh(new Vector2(Centre.x, Centre.z), Radius + 0.2f);
                impacts = heights.SurfacePoints(new Vector2(Centre.x, Centre.z), Radius, 60);
            }
            bool combat = SpawnManager.instance != null && SpawnManager.instance.combat;
            if (Active && combat) Damage(dt);
            else { inside.Clear(); timeIn.Clear(); }
            hitGlow = Mathf.MoveTowards(hitGlow, 0f, dt * 4f);
            Draw(combat);
        }

        private void Strike()
        {
            struck = true;
            Vector3 hit = new Vector3(Centre.x, heights.Surface(Centre.x, Centre.z) ?? Centre.y, Centre.z);
            SoundLibrary.PlayLoud("ascensions/ritual_of_the_sun/sun_strike", hit, 1f, 16f);
            roar = SoundLoop.Start("ascensions/ritual_of_the_sun/sun_beam", transform, 0f, 12f, 2);
            Fx.Burst(hit + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.4f), 40, 0.16f, 4.5f);
            Fx.VoxelBurst(hit + Vector3.up * 0.1f, SunFx.EmberColors, 26, 0.07f, 4f, 1.1f, 6f);
            ScreenShake.Kick(hit, 0.006f + 0.0015f * Members.Count, 0.45f);
            if (Crystal != null)
            {
                power = new CrystalPower(Crystal, transform, heights, Radius * RitualLogic.CrystalCircleFactor);
                SoundLibrary.PlayLoud("ascensions/ritual_of_the_sun/crystal_power", hit, 0.9f, 14f);
            }
        }

        // ------------------------------------------------------------------ damage

        private void Damage(float dt)
        {
            statsTimer -= dt;
            if (statsTimer <= 0f)
            {
                statsTimer = 0.5f;
                stats = RitualLogic.Average(Members.Where(m => m != null && m.Tower != null).Select(m => m.Stats()).ToList());
            }
            // Monsters inside the column, or inside the ritual circle round a powered crystal (their body may reach in).
            float reach = DamageRadius;
            inside.Clear();
            if (SpawnManager.instance.currentEnemies != null)
                foreach (Enemy e in SpawnManager.instance.currentEnemies)
                {
                    if (!Combat.IsAlive(e)) continue;
                    Vector3 d = e.transform.position - Centre;
                    d.y = 0f;
                    if (d.magnitude <= reach + Phase2Kit.Radius(e)) inside.Add(e);
                }
            foreach (Enemy e in timeIn.Keys.ToList()) if (!inside.Contains(e)) timeIn.Remove(e);
            foreach (Enemy e in inside) timeIn[e] = (timeIn.TryGetValue(e, out float t) ? t : 0f) + dt;

            int due = FireClock.Due(ref tickTimer, dt, stats.Rate > 0f ? 1f / stats.Rate : 0f, 4);
            float focus = TowerManager.instance != null ? TowerManager.instance.obeliskTimeOnTargetMultiplier : 0f;
            for (int k = 0; k < due; k++)
            {
                if (inside.Count == 0) break;
                if (stats.Mana > 0f)
                {
                    int mana = RitualLogic.Take(ref manaCarry, stats.Mana);
                    if (mana > 0 && !ResourceManager.instance.CheckMana(mana)) { manaCarry += mana; Starved = true; break; }
                    if (mana > 0) ResourceManager.instance.SpendMana(mana);
                }
                Starved = false;
                int dmg = RitualLogic.Take(ref dmgCarry, stats.Damage);
                Ticks++;
                int sparks = 0;
                foreach (Enemy e in inside.ToList())
                {
                    if (!Combat.IsAlive(e)) continue;
                    int bonus = RitualLogic.FocusBonus(timeIn.TryGetValue(e, out float t) ? t : 0f, focus, dmg);
                    e.TakeDamage(TowerType.Obelisk, dmg + bonus, stats.HealthMult, stats.ArmorMult, stats.ShieldMult, stats.Slow,
                                 stats.Bleed, stats.Burn, stats.Poison, stats.Crit, stats.Stun);
                    Hits++;
                    if (sparks++ < 3 && UnityEngine.Random.value < 0.35f)
                        Fx.Burst(e.transform.position + Vector3.up * 0.3f, new Color(1f, 0.6f, 0.2f), 3, 0.05f, 1.4f);
                }
                hitGlow = 1f;
                power?.Hit();
            }
        }

        // ------------------------------------------------------------------ drawing

        private void Draw(bool combat)
        {
            int n = Members.Count;
            float k = Breaking ? 1f - Smooth(breakAge / BreakTime) : 1f;
            float sunY = Centre.y + SunHeight;
            Vector3 sunPos = new Vector3(Centre.x, sunY, Centre.z);
            spin += Time.deltaTime;

            // Beams straight up from each prism, then on to the sun.
            for (int i = 0; i < n; i++)
            {
                SunObelisk m = Members[i];
                if (m == null) { upBeams[i].SetVisible(false); skyBeams[i].SetVisible(false); continue; }
                Vector3 p = m.Prism;
                float g = EaseOut(((Breaking ? brokenAt : age) - MemberTime(i)) / UpBeamGrow) * k;
                bool on = g > 0.001f;
                upBeams[i].SetVisible(on);
                if (on)
                {
                    Vector3 top = new Vector3(p.x, Mathf.Lerp(p.y, sunY, g), p.z);
                    float w = 1f + 0.1f * Mathf.Sin(spin * 7f + i) + Flash(i) * 0.8f;
                    upBeams[i].Set(p, top, w, k * (1f + Flash(i)), 0.25f);
                }
                float sg = EaseOut(((Breaking ? brokenAt : age) - SunStart) / 0.3f) * k;
                skyBeams[i].SetVisible(sg > 0.001f);
                if (sg > 0.001f)
                {
                    Vector3 top = new Vector3(p.x, sunY, p.z);
                    skyBeams[i].Set(top, Vector3.Lerp(top, sunPos, sg), 1f, 0.55f * k);
                }
            }

            // The sun: its disc a little wider than the column (SunToBeam), its rays reaching out round it.
            float sunGrow = Pop(((Breaking ? brokenAt : age) - SunStart) / SunGrow);
            if ((Breaking ? brokenAt : age) < SunStart) sunGrow = 0f;
            if (Breaking) sunGrow *= Smooth(1f - (breakAge - 0.2f) / (BreakTime - 0.2f));
            bool sunOn = sunGrow > 0.001f;
            sun.SetActive(sunOn); raysA.SetActive(sunOn); raysB.SetActive(sunOn);
            float sunSize = SunToBeam * 2f * Radius / SunFx.SunDiscShare * sunGrow;
            float throb = 1f + 0.04f * Mathf.Sin(spin * 2.3f) + 0.1f * hitGlow;
            if (sunOn)
            {
                Camera cam = Camera.main;
                Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
                sun.transform.position = raysA.transform.position = raysB.transform.position = sunPos;
                sun.transform.rotation = face;
                raysA.transform.rotation = face * Quaternion.Euler(0f, 0f, spin * 11f);
                raysB.transform.rotation = face * Quaternion.Euler(0f, 0f, -spin * 17f + 7.5f);
                sun.transform.localScale = Vector3.one * sunSize * throb;
                raysA.transform.localScale = Vector3.one * sunSize * 1.7f;
                raysB.transform.localScale = Vector3.one * sunSize * 1.35f * throb;
                sunMat.color = SunFx.SunColor;
                raysAMat.color = SunFx.RayColor * 0.8f;
                raysBMat.color = SunFx.RayColor * 0.55f;
            }

            // The column: it falls from the sun to the ground, then widens to its full radius; it draws back up when the
            // ring breaks. It always keeps its width up into the sun.
            float bottomY, width;
            if (Breaking)
            {
                float b = Smooth(breakAge / (BreakTime * 0.7f));
                bottomY = Mathf.Lerp(struck ? Centre.y : sunY, sunY, b);
                width = (struck ? 1f : 0.3f) * (1f - b);
            }
            else
            {
                float fall = (age - StrikeStart) / (StrikeTime - StrikeStart);
                bottomY = Mathf.Lerp(sunY, Centre.y, Mathf.Clamp01(fall * fall));
                width = age < StrikeStart ? 0f : age < StrikeTime ? 0.3f : Mathf.Lerp(0.3f, 1f, EaseOut((age - StrikeTime) / 0.3f));
            }
            bool colOn = width > 0.001f && bottomY < sunY - 0.05f;
            float flare = struck && !Breaking ? Mathf.Exp(-(age - StrikeTime) * 5f) : 0f;
            float pulse = 1f + 0.07f * Mathf.Sin(spin * 5.3f) + 0.05f * Mathf.Sin(spin * 13.7f) + 0.18f * hitGlow + 1.2f * flare;
            float R = Radius * width;
            float idle = combat ? 1f : 0.8f;
            Vector3 axis = Centre;
            glowShaft.Draw(axis, bottomY, sunY, R * 1.3f + 0.1f, SunFx.ShaftGlow * (pulse * idle * k), 0f, 0f, colOn);
            Color body = SunFx.ShaftBody;
            body.a *= Mathf.Min(1.5f, pulse) * idle * k;
            bodyShaft.Draw(axis, bottomY, sunY, R, body, 0f, 0f, colOn);
            streakShaft.Draw(axis, bottomY, sunY, R * 0.92f, SunFx.ShaftStreak * (idle * k), -spin * 1.6f, 2.2f, colOn);
            coreShaft.Draw(axis, bottomY, sunY, R * 0.4f, SunFx.ShaftCore * (pulse * k), 0f, 0f, colOn);

            // A powered crystal: gold, glowing, with the ritual circle round it.
            power?.Draw(Powered ? 1f : Breaking ? k : 0f, spin);

            // Light on the surroundings, from where the beam meets the ground.
            lamp.intensity = struck ? (1.1f * pulse + 2.5f * flare) * k * idle : 0f;

            if (roar != null)
            {
                roarLevel = Mathf.MoveTowards(roarLevel, Breaking ? 0f : inside.Count > 0 ? 1f : combat ? 0.65f : 0.45f, Time.deltaTime * 1.5f);
                roar.Volume = 1.3f * roarLevel;
                roar.Pitch = 1f + 0.03f * hitGlow;
            }
        }

        /// <summary>
        /// Light that moves: the ignition pulse round the ring; once the sun has struck, a glow and splashing sparks
        /// wherever the beam meets something (on each surface at its own height), motes rising in the column, the strike's
        /// shock wave running out over the ground, and the powered crystal's pulses.
        /// </summary>
        public void DrawLight(QuadBatch q)
        {
            int n = Members.Count;
            if (!Breaking && age < PulseTime + 0.1f && n > 0)
            {
                float p = Mathf.Clamp01(age / PulseTime) * n;
                for (int trail = 0; trail < 4; trail++)
                {
                    float pt = p - trail * 0.06f * n / 3f;
                    if (pt < 0f) continue;
                    int seg = Mathf.Min(n - 1, Mathf.FloorToInt(pt));
                    SunObelisk a = Members[seg], b = Members[(seg + 1) % n];
                    if (a == null || b == null) continue;
                    Vector3 at = Vector3.Lerp(a.Prism, b.Prism, pt - seg);
                    float f = 1f - trail * 0.22f;
                    q.Add(at, 0.75f * f, SunFx.Packet * (1.6f * f));
                    if (trail == 0) q.Add(at, 0.3f, Color.white * 1.5f);
                }
            }
            if (!struck) return;
            float k = Breaking ? 1f - Smooth(breakAge / BreakTime) : Smooth((age - StrikeTime) / 0.3f);
            if (k <= 0.001f) return;
            float t = Time.time;
            int count = impacts.Count;
            if (count > 0)
            {
                // Glow where the light lands, flickering.
                for (int i = 0; i < count; i += 2)
                {
                    float flick = 0.75f + 0.25f * Mathf.Sin(t * (7f + i % 5) + i * 1.7f);
                    q.Add(impacts[i] + Vector3.up * 0.03f, 0.34f, SunFx.Impact * (flick * k));
                }
                // Sparks splashing up from the surfaces.
                int sparks = 14 + 4 * n;
                for (int i = 0; i < sparks; i++)
                {
                    float seed = i * 0.7548776f + 0.3f;
                    float cyc = t * 1.6f + seed;
                    float ph = Frac(cyc);
                    float pick = Frac(Mathf.Sin(Mathf.Floor(cyc) * 12.9898f + i * 78.233f) * 43758.55f);
                    Vector3 from = impacts[Mathf.Min(count - 1, (int)(pick * count))];
                    float ang = pick * 40f;
                    Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 at = from + dir * (0.28f * ph) + Vector3.up * (Mathf.Sin(ph * Mathf.PI) * 0.32f + 0.03f);
                    q.Add(at, 0.07f, SunFx.Spark * ((1f - ph) * k));
                }
                // Motes rising from where it lands.
                int motes = 14 + 5 * n;
                for (int i = 0; i < motes; i++)
                {
                    float seed = i * 0.6180339f;
                    float ph = Frac(t * (0.18f + 0.1f * Frac(seed * 7.3f)) + seed);
                    Vector3 from = impacts[(int)(Frac(seed * 13.7f) * count) % count];
                    float ang = seed * 37.1f + t * (0.4f + Frac(seed * 3.1f));
                    Vector3 at = from + new Vector3(Mathf.Cos(ang) * 0.15f, ph * SunHeight * 0.7f, Mathf.Sin(ang) * 0.15f);
                    q.Add(at, 0.08f + 0.06f * Frac(seed * 5.3f), SunFx.Mote * (Mathf.Sin(ph * Mathf.PI) * k));
                }
            }
            // The strike's shock wave: a ring of light running out over the ground, at the ground's own height.
            float sa = age - StrikeTime;
            if (!Breaking && sa < 0.7f)
            {
                float s = EaseOut(sa / 0.7f), r = Mathf.Lerp(Radius * 0.6f, Mathf.Max(Radius * 2.5f, DamageRadius), s);
                int dots = 56;
                for (int i = 0; i < dots; i++)
                {
                    float a = i / (float)dots * Mathf.PI * 2f;
                    float x = Centre.x + Mathf.Cos(a) * r, z = Centre.z + Mathf.Sin(a) * r;
                    float? y = heights.Surface(x, z);
                    if (y == null) continue;
                    q.Add(new Vector3(x, y.Value + 0.05f, z), 0.3f, SunFx.Shock * (1f - s));
                }
            }
            power?.DrawLight(q, k, t);
        }

        private static float Frac(float x) => x - Mathf.Floor(x);
    }

    /// <summary>
    /// A straight beam of light: a bright thin core and a soft wide glow (two additive LineRenderers facing the camera,
    /// with a soft edge across their width).
    /// </summary>
    internal sealed class LightBeam
    {
        private readonly LineRenderer core, glow;
        private readonly Color coreColor, glowColor;
        private readonly float coreWidth, glowWidth;
        private static Material material;

        public LightBeam(string name, Transform parent, Color coreColor, Color glowColor, float coreWidth, float glowWidth)
        {
            if (material == null) material = AccelFx.Additive("SunBeam", SunFx.BeamProfile, Color.white, 3005);
            this.coreColor = coreColor; this.glowColor = glowColor;
            this.coreWidth = coreWidth; this.glowWidth = glowWidth;
            glow = Line(name + "Glow", parent);
            core = Line(name + "Core", parent);
        }

        private static LineRenderer Line(string name, Transform parent)
        {
            var go = new GameObject("TR_" + name);
            if (parent != null) go.transform.SetParent(parent, false);
            var l = go.AddComponent<LineRenderer>();
            l.sharedMaterial = material;
            l.useWorldSpace = true;
            l.positionCount = 2;
            l.textureMode = LineTextureMode.Stretch;
            l.alignment = LineAlignment.View;
            l.numCapVertices = 3;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            return l;
        }

        /// <param name="width">Width factor.</param>
        /// <param name="level">Brightness (premultiplied into the colours: the material adds light).</param>
        /// <param name="endLevel">Brightness at the far end, as a share of <paramref name="level"/> (a beam fading out).</param>
        public void Set(Vector3 from, Vector3 to, float width, float level, float endLevel = 1f)
        {
            core.SetPosition(0, from); core.SetPosition(1, to);
            glow.SetPosition(0, from); glow.SetPosition(1, to);
            core.startWidth = core.endWidth = coreWidth * width;
            glow.startWidth = glow.endWidth = glowWidth * width;
            core.startColor = coreColor * level;
            core.endColor = coreColor * (level * endLevel);
            glow.startColor = glowColor * level;
            glow.endColor = glowColor * (level * endLevel);
        }

        public void SetVisible(bool on)
        {
            if (core != null && core.enabled != on) core.enabled = on;
            if (glow != null && glow.enabled != on) glow.enabled = on;
        }

        public void Destroy()
        {
            if (core != null) Object.Destroy(core.gameObject);
            if (glow != null) Object.Destroy(glow.gameObject);
        }
    }

    /// <summary>
    /// A column of light seen as a volume: the half of a cylinder that faces the camera, turned with it every frame,
    /// fading from its middle towards its silhouette (the light a ray picks up is longer through the middle of a
    /// column than near its edge). Everything standing inside the cylinder is behind that surface and so lies in the
    /// light; things in front of it still hide it. Texture u runs across, v along the column (scrolled for streaks).
    /// </summary>
    internal sealed class Shaft
    {
        private const int Segs = 24, Rows = 8;
        private readonly GameObject go;
        private readonly Mesh mesh;
        private readonly Material mat;
        private readonly Vector3[] verts = new Vector3[(Rows + 1) * (Segs + 1)];
        private readonly Color[] colors = new Color[(Rows + 1) * (Segs + 1)];
        private readonly Vector2[] uvs = new Vector2[(Rows + 1) * (Segs + 1)];
        private readonly bool additive;
        private readonly float power, floor;

        /// <param name="additive">Light added to the scene (else a tinted volume, alpha-blended: keeps its colour on bright ground).</param>
        /// <param name="power">How quickly it fades towards the silhouette (higher: a softer, rounder look).</param>
        /// <param name="floor">What is left at the silhouette (a share of the middle: a visible edge).</param>
        public Shaft(string name, Transform parent, Texture tex, int queue, bool additive, float power, float floor)
        {
            this.additive = additive;
            this.power = power;
            this.floor = floor;
            mesh = new Mesh { name = "TR_" + name };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            var tris = new List<int>();
            AccelFx.GridTriangles(tris, Rows, Segs);
            mesh.SetTriangles(tris, 0);
            mat = additive ? AccelFx.Additive(name, tex, Color.white, queue) : AccelFx.Glow(name, tex, Color.white, queue);
            go = AccelFx.MeshObject(name, parent, mesh, mat);
        }

        /// <param name="axis">A point on the column's axis (only x and z count).</param>
        /// <param name="vRepeat">World units per texture repeat along the column (0 = once over its length).</param>
        public void Draw(Vector3 axis, float y0, float y1, float radius, Color color, float vScroll, float vRepeat, bool visible)
        {
            go.SetActive(visible);
            if (!visible) return;
            Camera cam = Camera.main;
            Vector3 toCam = cam != null ? -cam.transform.forward : Vector3.back;
            float facing = Mathf.Atan2(toCam.z, toCam.x);
            if (new Vector2(toCam.x, toCam.z).sqrMagnitude < 1e-4f) facing = -Mathf.PI / 2f;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            int v = 0;
            for (int r = 0; r <= Rows; r++)
            {
                float t = r / (float)Rows, y = Mathf.Lerp(y0, y1, t);
                // Full strength down to the ground; it fades only into the sun over the last row.
                float along = Mathf.Clamp01((1f - t) * Rows);
                float vv = (vRepeat > 0f ? (y - y0) / vRepeat : t) + vScroll;
                for (int s = 0; s <= Segs; s++)
                {
                    float u = s / (float)Segs, off = (u - 0.5f) * Mathf.PI, a = facing + off;
                    float nv = Mathf.Cos(off);
                    float e = (floor + (1f - floor) * Mathf.Pow(Mathf.Max(0f, nv), power)) * along;
                    verts[v] = new Vector3(axis.x + Mathf.Cos(a) * radius, y, axis.z + Mathf.Sin(a) * radius);
                    uvs[v] = new Vector2(u, vv);
                    colors[v] = additive ? color * e : new Color(color.r, color.g, color.b, color.a * e);
                    v++;
                }
            }
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        public void Destroy()
        {
            if (go != null) Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
            if (mat != null) Object.Destroy(mat);
        }
    }

    /// <summary>
    /// Heights of the map round a point, sampled straight down on a grid (cells offset by half a step, so tile edges fall
    /// between samples): the <b>surface</b> (the top of whatever is there: grass, path, crystals, trees, towers) and the
    /// <b>ground</b> (map tiles only). Monsters are ignored. Used for where the sun's beam lands, its foot and the
    /// ritual circle round a powered crystal.
    /// </summary>
    internal sealed class HeightField
    {
        private readonly Vector2 centre;
        public readonly float Step;
        private readonly int half;
        public readonly int Size;
        private readonly float[] surface, ground;
        private static readonly RaycastHit[] hits = new RaycastHit[32];
        /// <summary>Collider → 0 ignored (monsters), 1 ground (map tiles), 2 something standing on them.</summary>
        private static readonly Dictionary<int, byte> kinds = new Dictionary<int, byte>();

        public HeightField(Vector2 centre, float radius, float step)
        {
            this.centre = centre;
            Step = step;
            half = Mathf.CeilToInt(radius / step);
            Size = 2 * half;
            surface = new float[Size * Size];
            ground = new float[Size * Size];
            for (int i = 0; i < surface.Length; i++) surface[i] = ground[i] = float.NaN;
            if (kinds.Count > 20000) kinds.Clear();
            Refresh(centre, radius);
        }

        public Vector2 Pos(int i, int j) => new Vector2(centre.x + (i - half + 0.5f) * Step, centre.y + (j - half + 0.5f) * Step);
        public float GroundAt(int i, int j) => ground[j * Size + i];

        /// <summary>Samples again within <paramref name="radius"/> of a point (something was built or removed there).</summary>
        public void Refresh(Vector2 around, float radius)
        {
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    Vector2 p = Pos(i, j);
                    if ((p - around).sqrMagnitude > (radius + Step) * (radius + Step)) continue;
                    int n = Physics.RaycastNonAlloc(new Vector3(p.x, 40f, p.y), Vector3.down, hits, 80f, ~0, QueryTriggerInteraction.Ignore);
                    float s = float.NaN, g = float.NaN;
                    for (int k = 0; k < n; k++)
                    {
                        byte kind = Kind(hits[k].collider);
                        if (kind == 0) continue;
                        float y = hits[k].point.y;
                        if (float.IsNaN(s) || y > s) s = y;
                        if (kind == 1 && (float.IsNaN(g) || y > g)) g = y;
                    }
                    surface[j * Size + i] = s;
                    ground[j * Size + i] = g;
                }
        }

        private static byte Kind(Collider c)
        {
            int id = c.GetInstanceID();
            if (kinds.TryGetValue(id, out byte k)) return k;
            if (c.GetComponentInParent<Enemy>() != null) k = 0;
            else if (c.GetComponentInParent<TerrainTile>() != null && c.GetComponentInParent<SpawnableObject>() == null &&
                     c.GetComponentInParent<Tower>() == null) k = 1;
            else k = 2;
            kinds[id] = k;
            return k;
        }

        private int Index(float x, float z)
        {
            int i = Mathf.FloorToInt((x - centre.x) / Step + half), j = Mathf.FloorToInt((z - centre.y) / Step + half);
            return i < 0 || j < 0 || i >= Size || j >= Size ? -1 : j * Size + i;
        }

        public float? Surface(float x, float z) { int i = Index(x, z); return i < 0 || float.IsNaN(surface[i]) ? (float?)null : surface[i]; }
        public float? Ground(float x, float z) { int i = Index(x, z); return i < 0 || float.IsNaN(ground[i]) ? (float?)null : ground[i]; }

        /// <summary>The lowest ground within a radius (where the beam's foot goes: path level).</summary>
        public float LowestGround(Vector2 c, float radius, float fallback)
        {
            float low = float.NaN;
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    float g = ground[j * Size + i];
                    if (float.IsNaN(g) || (Pos(i, j) - c).sqrMagnitude > radius * radius) continue;
                    if (float.IsNaN(low) || g < low) low = g;
                }
            return float.IsNaN(low) ? fallback : low;
        }

        /// <summary>Points on the surface within a radius, at most <paramref name="max"/> (spread evenly).</summary>
        public List<Vector3> SurfacePoints(Vector2 c, float radius, int max)
        {
            var all = new List<Vector3>();
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    float s = surface[j * Size + i];
                    Vector2 p = Pos(i, j);
                    if (float.IsNaN(s) || (p - c).sqrMagnitude > radius * radius) continue;
                    all.Add(new Vector3(p.x, s, p.y));
                }
            if (all.Count <= max) return all;
            var some = new List<Vector3>(max);
            for (int k = 0; k < max; k++) some.Add(all[k * all.Count / max]);
            return some;
        }
    }

    /// <summary>
    /// A mana crystal powered by the sun's beam (the easter egg): it turns gold and glows, and a ritual circle (the sun
    /// sigil, laid over the ground so it follows every step of the terrain) turns slowly round it; pulses of light run
    /// out over it. Undone when the ring breaks.
    /// </summary>
    internal sealed class CrystalPower
    {
        private readonly ManaCrystal crystal;
        private readonly List<(Renderer Renderer, Material[] Original)> recoloured = new List<(Renderer, Material[])>();
        private readonly List<Material> made = new List<Material>();
        /// <summary>Gold copies of the crystal drawn over the column of light (it would wash the crystal out).</summary>
        private readonly List<GameObject> overlays = new List<GameObject>();
        private Material overlayMat;
        private readonly HeightField heights;
        private readonly Vector3 centre;
        private readonly float radius;
        private readonly GameObject circle;
        private readonly Mesh mesh;
        private readonly Material circleMat;
        private readonly Vector2[] rel;
        private readonly Vector2[] uvs;
        private float hitFlash;
        private static readonly Color GoldDark = new Color(0.55f, 0.3f, 0.04f), GoldLight = new Color(1f, 0.92f, 0.55f);

        public CrystalPower(ManaCrystal crystal, Transform parent, HeightField heights, float radius)
        {
            this.crystal = crystal;
            this.heights = heights;
            this.radius = radius;
            Bounds b = new Bounds(crystal.transform.position, Vector3.zero);
            foreach (Renderer r in crystal.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is LineRenderer) continue;
                b.Encapsulate(r.bounds);
                Material[] original = r.sharedMaterials;
                var gold = new Material[original.Length];
                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;
                    var m = new Material(original[i]) { name = original[i].name + "_Sun" };
                    if (m.mainTexture != null)
                    {
                        m.mainTexture = Fx.RampTexture(original[i].mainTexture, GoldDark, GoldLight);
                        if (m.HasProperty("_Color")) m.color = Color.white;
                    }
                    else if (m.HasProperty("_Color")) m.color = new Color(1f, 0.78f, 0.25f);
                    if (m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.12f) * 0.45f);
                    }
                    gold[i] = m;
                    made.Add(m);
                }
                recoloured.Add((r, original));
                r.sharedMaterials = gold;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && r is MeshRenderer)
                {
                    Texture tex = original.Length > 0 && original[0] != null && original[0].mainTexture != null
                        ? Fx.RampTexture(original[0].mainTexture, GoldDark, GoldLight) : null;
                    if (overlayMat == null) overlayMat = AccelFx.Glow("SunCrystalOverlay", tex, new Color(1f, 0.82f, 0.35f, 0f), 3011);
                    GameObject o = AccelFx.MeshObject("SunCrystalOverlay", r.transform, mf.sharedMesh, overlayMat);
                    overlays.Add(o);
                }
            }
            centre = b.center;

            // The circle: one quad per pair of neighbouring ground samples on the same level (steps leave a hairline gap
            // instead of a stretched slope), at the ground's height, inside the circle.
            Vector2 c = new Vector2(crystal.transform.position.x, crystal.transform.position.z);
            int n = heights.Size;
            var index = new int[n * n];
            var verts = new List<Vector3>();
            var relList = new List<Vector2>();
            Vector3 origin = parent.position;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    index[j * n + i] = -1;
                    Vector2 p = heights.Pos(i, j);
                    float g = heights.GroundAt(i, j);
                    if (float.IsNaN(g) || (p - c).magnitude > radius + heights.Step * 1.5f) continue;
                    index[j * n + i] = verts.Count;
                    verts.Add(new Vector3(p.x, g + 0.03f, p.y) - origin);
                    relList.Add((p - c) / (2f * radius / 0.95f));
                }
            var tris = new List<int>();
            for (int j = 0; j + 1 < n; j++)
                for (int i = 0; i + 1 < n; i++)
                {
                    int a = index[j * n + i], b1 = index[j * n + i + 1], c1 = index[(j + 1) * n + i], d = index[(j + 1) * n + i + 1];
                    if (a < 0 || b1 < 0 || c1 < 0 || d < 0) continue;
                    float lo = Mathf.Min(verts[a].y, verts[b1].y, verts[c1].y, verts[d].y);
                    float hi = Mathf.Max(verts[a].y, verts[b1].y, verts[c1].y, verts[d].y);
                    if (hi - lo > 0.06f) continue;
                    tris.Add(a); tris.Add(c1); tris.Add(b1);
                    tris.Add(b1); tris.Add(c1); tris.Add(d);
                }
            rel = relList.ToArray();
            uvs = new Vector2[rel.Length];
            mesh = new Mesh { name = "TR_SunCircle" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            circleMat = AccelFx.Additive("SunCircle", SunFx.Sigil, Color.black, 3000);
            circle = AccelFx.MeshObject("SunCircle", parent, mesh, circleMat);
        }

        public void Hit() => hitFlash = 1f;

        /// <param name="level">0..1: how strongly it is powered (fades in and out).</param>
        public void Draw(float level, float spin)
        {
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 4f);
            bool on = level > 0.001f && crystal != null;
            circle.SetActive(on);
            foreach (GameObject o in overlays) if (o != null) o.SetActive(on);
            if (!on) return;
            float a = spin * 0.1f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            for (int i = 0; i < rel.Length; i++)
                uvs[i] = new Vector2(0.5f + rel[i].x * ca - rel[i].y * sa, 0.5f + rel[i].x * sa + rel[i].y * ca);
            mesh.uv = uvs;
            circleMat.color = SunFx.SigilColor * (level * (0.8f + 0.2f * Mathf.Sin(spin * 2f) + 0.25f * hitFlash));
            if (overlayMat != null)
                overlayMat.color = new Color(1f, 0.8f + 0.1f * Mathf.Sin(spin * 3.3f), 0.32f, level * (0.88f + 0.08f * Mathf.Sin(spin * 3.3f) + 0.04f * hitFlash));
        }

        /// <summary>The crystal's glow and pulses of light running out over the circle at the ground's height.</summary>
        public void DrawLight(QuadBatch q, float k, float t)
        {
            if (crystal == null || k <= 0.001f) return;
            float throb = 0.85f + 0.15f * Mathf.Sin(t * 3.3f) + 0.3f * hitFlash;
            q.Add(centre, 1.3f * throb, SunFx.CrystalGlow * k);
            q.Add(centre, 0.5f, Color.white * (0.7f * k * throb));
            for (int w = 0; w < 2; w++)
            {
                float ph = (t / 1.4f + w * 0.5f) % 1f, r = Mathf.Lerp(0.3f, radius, ph);
                int dots = Mathf.Clamp((int)(r * 14f), 12, 90);
                for (int i = 0; i < dots; i++)
                {
                    float a = i / (float)dots * Mathf.PI * 2f + w;
                    float x = crystal.transform.position.x + Mathf.Cos(a) * r, z = crystal.transform.position.z + Mathf.Sin(a) * r;
                    float? y = heights.Ground(x, z);
                    if (y == null) continue;
                    q.Add(new Vector3(x, y.Value + 0.05f, z), 0.18f, SunFx.Shock * (0.55f * (1f - ph) * k));
                }
            }
        }

        public void Destroy()
        {
            foreach (var (r, original) in recoloured) if (r != null) r.sharedMaterials = original;
            recoloured.Clear();
            foreach (Material m in made) if (m != null) Object.Destroy(m);
            made.Clear();
            if (circle != null) Object.Destroy(circle);
            if (mesh != null) Object.Destroy(mesh);
            if (circleMat != null) Object.Destroy(circleMat);
            foreach (GameObject o in overlays) if (o != null) Object.Destroy(o);
            overlays.Clear();
            if (overlayMat != null) Object.Destroy(overlayMat);
        }
    }

    /// <summary>Colours and generated textures of the sun's light (all textures premultiplied: light in the colour).</summary>
    internal static class SunFx
    {
        public static readonly Color FireCore = new Color(1f, 0.88f, 0.6f), FireGlow = new Color(1f, 0.45f, 0.08f) * 0.8f;
        public static readonly Color LinkCore = new Color(1f, 0.82f, 0.4f) * 0.9f, LinkGlow = new Color(1f, 0.66f, 0.16f) * 0.6f;
        public static readonly Color UpCore = new Color(1f, 0.97f, 0.85f), UpGlow = new Color(1f, 0.72f, 0.28f) * 0.55f;
        public static readonly Color SkyCore = new Color(1f, 0.92f, 0.7f) * 0.7f, SkyGlow = new Color(1f, 0.65f, 0.25f) * 0.35f;
        public static readonly Color Packet = new Color(1f, 0.88f, 0.55f), PrismGlow = new Color(1f, 0.84f, 0.5f) * 0.65f;
        public static readonly Color ShaftGlow = new Color(1f, 0.5f, 0.12f) * 0.22f, ShaftStreak = new Color(1f, 0.88f, 0.55f) * 0.3f,
                                     ShaftCore = new Color(1f, 0.96f, 0.85f) * 0.55f;
        /// <summary>The column's body: a volume of golden light (alpha-blended, so it stays gold over the bright path).</summary>
        public static readonly Color ShaftBody = new Color(1f, 0.76f, 0.28f, 0.5f);
        public static readonly Color SunColor = new Color(1f, 0.86f, 0.55f), RayColor = new Color(1f, 0.7f, 0.3f);
        public static readonly Color SigilColor = new Color(1f, 0.78f, 0.3f) * 1.3f, Shock = new Color(1f, 0.85f, 0.55f),
                                     Impact = new Color(1f, 0.72f, 0.3f) * 0.55f, CrystalGlow = new Color(1f, 0.7f, 0.25f) * 0.8f;
        public static readonly Color Mote = new Color(1f, 0.85f, 0.5f) * 0.9f, Spark = new Color(1f, 0.7f, 0.3f);
        public static readonly Color[] EmberColors =
            { new Color(1f, 0.95f, 0.7f), new Color(1f, 0.8f, 0.3f), new Color(1f, 0.55f, 0.15f), new Color(0.95f, 0.35f, 0.1f) };

        private static Texture2D beamProfile, streaks, glint, sunDisc, rays, sigil;

        /// <summary>Across a LineRenderer (v): a bright middle with soft edges.</summary>
        public static Texture2D BeamProfile => beamProfile != null ? beamProfile : beamProfile = Make("TR_SunBeamProfile", 4, 64, (u, v) =>
        {
            float d = (v - 0.5f) * 2f;
            return Mathf.Exp(-d * d * 3.2f) * Mathf.Clamp01((1f - Mathf.Abs(d)) * 6f);
        });

        /// <summary>Vertical streaks of light (u across, v along, repeating along v).</summary>
        public static Texture2D Streaks
        {
            get
            {
                if (streaks != null) return streaks;
                const int w = 64, h = 128;
                var px = new Color[w * h];
                var rnd = new System.Random(11);
                var lanes = new float[w];
                for (int x = 0; x < w; x++) lanes[x] = (float)rnd.NextDouble();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = x / (w - 1f), edge = Mathf.Clamp01((1f - Mathf.Abs(u - 0.5f) * 2f) * 2.5f);
                        float lane = Mathf.Pow(lanes[x], 3f);
                        // Each lane carries a few dashes along v.
                        float ph = (y / (float)h + lanes[(x * 7) % w]) % 1f;
                        float dash = Mathf.Pow(Mathf.Abs(Mathf.Sin(ph * Mathf.PI * (2 + (x % 3)))), 6f);
                        float b = lane * dash * edge;
                        px[y * w + x] = new Color(b, b, b, b);
                    }
                streaks = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "TR_SunStreaks", wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Repeat };
                streaks.SetPixels(px);
                streaks.Apply();
                return streaks;
            }
        }

        /// <summary>A four-pointed glint with a soft heart (prisms, the fire beam's hit).</summary>
        public static Texture2D Glint => glint != null ? glint : glint = Make("TR_SunGlint", 64, 64, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f, r = Mathf.Sqrt(x * x + y * y);
            float star = Mathf.Exp(-Mathf.Abs(x) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(y)) + Mathf.Exp(-Mathf.Abs(y) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(x));
            float heart = Mathf.Exp(-r * r * 9f);
            return Mathf.Clamp01(star * 0.8f + heart) * Mathf.Clamp01((1f - r) * 3f);
        });

        /// <summary>The sun's disc spans this share of its quad (the rest is corona).</summary>
        public const float SunDiscShare = 0.6f;

        /// <summary>The sun: a bright disc (a little brighter in the middle) in a soft corona.</summary>
        public static Texture2D SunDisc => sunDisc != null ? sunDisc : sunDisc = Make("TR_SunDisc", 128, 128, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f, r = Mathf.Sqrt(x * x + y * y), d = SunDiscShare;
            float disc = Mathf.Clamp01((d - r) / 0.03f) * (0.86f + 0.14f * (1f - r / d));
            float corona = r > d - 0.04f ? Mathf.Exp(-(r - d + 0.02f) * 7f) * 0.55f : 0f;
            return Mathf.Clamp01(Mathf.Max(disc, corona)) * Mathf.Clamp01((1f - r) * 6f);
        });

        /// <summary>Sixteen tapering rays, long and short in turn.</summary>
        public static Texture2D Rays => rays != null ? rays : rays = Make("TR_SunRays", 256, 256, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f, r = Mathf.Sqrt(x * x + y * y);
            if (r < 0.12f || r >= 1f) return 0f;
            float a = Mathf.Atan2(y, x);
            float k = Mathf.Abs(Mathf.Cos(a * 8f));
            bool longRay = Mathf.Cos(a * 16f) > 0f;
            float len = longRay ? 1f : 0.62f;
            float sharp = Mathf.Pow(k, Mathf.Lerp(6f, 40f, r / len));
            return sharp * Mathf.Clamp01((len - r) / (len * 0.6f)) * Mathf.Clamp01((r - 0.12f) / 0.1f);
        });

        /// <summary>
        /// The sun sigil on the ground (its outer ring at 95% of the radius = the beam's reach): two rings round a band
        /// of rune marks, twelve rays pointing in, an inner ring and a faint warm fill.
        /// </summary>
        public static Texture2D Sigil
        {
            get
            {
                if (sigil != null) return sigil;
                const int n = 512;
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float fx = (x - (n - 1) / 2f) / ((n - 1) / 2f), fy = (y - (n - 1) / 2f) / ((n - 1) / 2f);
                        float r = Mathf.Sqrt(fx * fx + fy * fy), a = Mathf.Atan2(fy, fx);
                        if (r >= 1f) { px[y * n + x] = Color.clear; continue; }
                        float b = 0f;
                        b += Line(r, 0.95f, 0.012f) + 0.6f * Line(r, 0.885f, 0.006f) + 0.7f * Line(r, 0.76f, 0.008f) + 0.5f * Line(r, 0.4f, 0.006f);
                        // Rune band: 24 cells, each with one of four marks.
                        if (r > 0.79f && r < 0.865f)
                        {
                            float cells = 24f, cell = (a / (2f * Mathf.PI) + 1f) * cells;
                            int id = Mathf.FloorToInt(cell) % 24;
                            float cu = cell - Mathf.Floor(cell), cv = (r - 0.79f) / 0.075f;
                            float m = 0f;
                            switch ((id * 7 + 3) % 4)
                            {
                                case 0: m = Bar(cu, 0.5f, 0.07f) * Bar(cv, 0.5f, 0.38f); break;                       // |
                                case 1: m = Dot(cu - 0.5f, cv - 0.5f, 0.2f); break;                                     // o
                                case 2: m = Bar(cv, 0.5f, 0.07f) * Bar(cu, 0.5f, 0.3f) + Bar(cu, 0.5f, 0.07f) * Bar(cv, 0.7f, 0.2f); break; // ⊥
                                default: m = Bar(Mathf.Abs(cu - 0.5f) * 2f - cv * 0.8f, 0.1f, 0.08f) * Bar(cv, 0.5f, 0.4f); break; // ^
                            }
                            b += m * 0.9f;
                        }
                        // Twelve rays pointing in from the inner ring, long and short in turn.
                        if (r < 0.75f && r > 0.42f)
                        {
                            float seg = a / (2f * Mathf.PI) * 12f;
                            float off = Mathf.Abs(seg - Mathf.Round(seg));
                            bool longRay = ((int)Mathf.Round(seg) + 24) % 2 == 0;
                            float inner = longRay ? 0.44f : 0.56f;
                            float t = Mathf.Clamp01((r - inner) / (0.75f - inner));
                            float half = 0.035f + 0.11f * t;
                            if (r > inner) b += Mathf.Clamp01((half - off) / 0.02f) * (0.35f + 0.45f * t);
                        }
                        b += 0.12f * Mathf.Clamp01(1f - r / 0.95f);
                        b = Mathf.Clamp01(b) * Mathf.Clamp01((1f - r) * 30f);
                        px[y * n + x] = new Color(b, b, b, b);
                    }
                sigil = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "TR_SunSigil", wrapMode = TextureWrapMode.Clamp };
                sigil.SetPixels(px);
                sigil.Apply();
                return sigil;
            }
        }

        private static float Line(float r, float at, float w) => Mathf.Exp(-Mathf.Pow((r - at) / w, 2f));
        private static float Bar(float x, float at, float half) => Mathf.Clamp01((half - Mathf.Abs(x - at)) / 0.06f);
        private static float Dot(float x, float y, float rad) => Mathf.Clamp01((rad - Mathf.Sqrt(x * x + y * y)) / 0.06f) *
                                                                Mathf.Clamp01((Mathf.Sqrt(x * x + y * y) - rad * 0.45f) / 0.06f);

        /// <param name="straight">White with the shape in alpha (for blended materials) instead of premultiplied light.</param>
        private static Texture2D Make(string name, int w, int h, Func<float, float, float> brightness, TextureWrapMode wrap = TextureWrapMode.Clamp,
                                      bool straight = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = wrap };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float b = Mathf.Clamp01(brightness(w > 1 ? x / (w - 1f) : 0.5f, h > 1 ? y / (h - 1f) : 0.5f));
                    px[y * w + x] = straight ? new Color(1f, 1f, 1f, b) : new Color(b, b, b, b);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
