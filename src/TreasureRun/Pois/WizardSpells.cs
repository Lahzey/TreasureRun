using System;
using System.Collections.Generic;
using TreasureRun.Ascensions.Phase1;
using TreasureRun.Audio;
using TreasureRun.Castles;
using TreasureRun.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TreasureRun.Pois
{
    /// <summary>
    /// The Wizard's Tower's spells (docs/poi-plan.md §7), their effects and sounds. Damage goes through
    /// <see cref="WizardTower.Hit"/> (fixed: Combat.FixedHit), numbers from <see cref="WizardTowerLogic"/>.
    /// </summary>
    internal static class WizardSpells
    {
        public static readonly Color Fire = new Color(1f, 0.5f, 0.12f, 1f), FireCore = new Color(1f, 0.88f, 0.45f, 1f);
        public static readonly Color Spark = new Color(0.72f, 0.9f, 1f, 1f), SparkCore = new Color(0.95f, 0.99f, 1f, 1f);
        public static readonly Color Frost = new Color(0.62f, 0.9f, 1f, 1f), FrostCore = new Color(0.92f, 0.99f, 1f, 1f);
        public static readonly Color Wind = new Color(0.86f, 0.9f, 0.94f, 0.55f);
        public static readonly Color Ghoul = new Color(0.42f, 1f, 0.55f, 1f), GhoulCore = new Color(0.8f, 1f, 0.85f, 1f);
        public static readonly Color GoldLight = new Color(1f, 0.86f, 0.35f, 1f), GoldCore = new Color(1f, 0.97f, 0.75f, 1f);

        public const string SoundFireCast = "ascensions/magma_lobber/magma_lob", SoundFireHit = "ascensions/magma_lobber/magma_impact";
        /// <summary>The fireball is the Magma Lobber's ball (no lava pool where it lands), a little wider than the chimney (0.5).</summary>
        public const string FireballModel = "ascensions/flame/magma_ball";
        public const float FireballScale = 0.85f;
        public const string SoundIceCast = "ascensions/icicle_cannon/icicle_launch", SoundIceHit = "ascensions/icicle_cannon/icicle_shatter";
        public const string SoundThunder = "pois/wizard_tower/thunder", SoundTornado = "pois/wizard_tower/tornado",
                            SoundNecro = "pois/wizard_tower/necro_aura", SoundGoldify = "pois/wizard_tower/goldify";

        private static readonly HashSet<Enemy> goldified = new HashSet<Enemy>();
        private static bool hooked;

        /// <summary>Is this monster being turned into gold (no other spell, minion or tower should take it)?</summary>
        public static bool IsGoldified(Enemy e) => e != null && goldified.Contains(e);

        public static void Cast(WizardTower t, WizardSpell s, List<Enemy> enemies, List<WizardTarget> targets)
        {
            int wave = Mathf.Max(1, GameHelpers.Level);
            switch (s)
            {
                case WizardSpell.Fireball:
                {
                    Enemy e = enemies[WizardTowerLogic.FireballTarget(targets)];
                    Vector3 from = t.Emitter(s);
                    float flight = 0.55f + 0.05f * Vector3.Distance(from, e.transform.position);
                    Vector3 aim = Combat.IsAlive(e) && e.pathfinder != null ? e.GetFuturePosition(flight) : e.transform.position;
                    Flash(from, Fire, FireCore);
                    SoundLibrary.Play(SoundFireCast, from, 0.7f, 0.08f, 3);
                    WizardMissile.Launch(from, null, aim, flight, 1.6f, Fire, FireCore, 1f, false, p => Explode(t, p, wave),
                                         model: FireballModel, modelScale: FireballScale);
                    break;
                }
                case WizardSpell.Lightning:
                    Strike(t, enemies[WizardTowerLogic.LightningTarget(targets)], wave);
                    break;
                case WizardSpell.IceSpikes:
                {
                    Vector3 from = t.Emitter(s);
                    Flash(from, Frost, FrostCore);
                    SoundLibrary.Play(SoundIceCast, from, 0.8f, 0.08f, 3);
                    List<int> picks = WizardTowerLogic.IceTargets(targets, WizardTowerLogic.IceSpikeCount);
                    for (int k = 0; k < picks.Count; k++)
                    {
                        Enemy e = enemies[picks[k]];
                        float flight = 0.35f + 0.04f * Vector3.Distance(from, e.transform.position) + 0.08f * k;
                        Vector3 start = from + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0f, 0.2f), Random.Range(-0.15f, 0.15f));
                        WizardMissile.Launch(start, e, e.transform.position, flight, 0.5f + 0.3f * k, Frost, FrostCore, 0.5f, false,
                                             p => Spike(t, e, p, wave), shard: true);
                    }
                    break;
                }
                case WizardSpell.Tornado:
                {
                    int i = WizardTowerLogic.TornadoTarget(targets);
                    if (i < 0) break;
                    Enemy e = enemies[i];
                    Vector3 from = t.Emitter(s);
                    Flash(from, Wind, Color.white);
                    Waypoint ahead = e.pathfinder != null ? e.pathfinder.currentWaypoint : null;
                    Vector3 at = e.transform.position;
                    WizardMissile.Launch(from, null, at, 0.45f, 0.4f, Wind, Color.white, 0.7f, false,
                                         p => WizardTornado.Spawn(t, new Vector3(at.x, p.y - 0.3f, at.z), ahead, wave));
                    break;
                }
                case WizardSpell.Necromancy:
                    EnsureHooked();
                    t.AuraUntil = Time.time + WizardTowerLogic.AuraSeconds;
                    Flash(t.Emitter(s), Ghoul, GhoulCore);
                    WizardAura.Show(t);
                    SoundLibrary.PlayLoud(SoundNecro, t.TowerBase + Vector3.up, 0.8f, 10f);
                    break;
                case WizardSpell.Goldify:
                {
                    int i = WizardTowerLogic.GoldifyTarget(targets);
                    if (i < 0) break;
                    Enemy e = enemies[i];
                    Vector3 from = t.Emitter(s);
                    Flash(from, GoldLight, GoldCore);
                    goldified.Add(e); // claimed now: nothing else takes it while the spell flies
                    WizardMissile.Launch(from, e, e.transform.position, 0.4f, 0.6f, GoldLight, GoldCore, 0.65f, false, p =>
                    {
                        Bloom.Spawn(p, GoldLight, 1.4f, 0.3f);
                        Goldify(t, e);
                    });
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ fire

        private static void Explode(WizardTower t, Vector3 at, int wave)
        {
            Vector3 ground = new Vector3(at.x, Mathf.Max(0.02f, at.y - 0.3f), at.z);
            int damage = WizardTowerLogic.FireballDamage(wave), tick = WizardTowerLogic.BurnTick(wave);
            foreach (Enemy e in Combat.EnemiesInRadius(ground, WizardTowerLogic.FireballRadius))
            {
                if (IsGoldified(e)) continue;
                if (t != null) t.Hit(e, damage, WizardTowerLogic.FireballHealth, WizardTowerLogic.FireballArmor, WizardTowerLogic.FireballShield);
                if (Combat.IsAlive(e)) WizardBurn.Apply(e, t, tick);
            }
            Bloom.Spawn(ground + Vector3.up * 0.35f, FireCore, 2.6f, 0.3f);
            Bloom.Spawn(ground + Vector3.up * 0.3f, Fire, 3.4f, 0.5f);
            Fx.Burst(ground + Vector3.up * 0.2f, Fire, 45, 0.18f, 4f);
            SoulFx.GlowBurst(ground + Vector3.up * 0.25f, Fire, 34, 3.6f, 0.6f, 0.8f, 0.6f);
            SoulFx.GlowBurst(ground + Vector3.up * 0.25f, FireCore, 12, 1.8f, 0.7f, 0.4f, 0.4f);
            SoulFx.GlowBurst(ground + Vector3.up * 0.4f, new Color(0.3f, 0.27f, 0.26f, 0.7f), 10, 0.8f, 0.7f, 1.4f, 0.6f);
            Fx.VoxelBurst(ground + Vector3.up * 0.15f, new[] { Fire, FireCore, new Color(0.35f, 0.3f, 0.28f) }, 22, 0.08f, 3.4f, 0.9f, 9.81f);
            GroundFlash.Spawn(ground, Fire, WizardTowerLogic.FireballRadius * 2.4f, 0.6f);
            SoundLibrary.Play(SoundFireHit, ground, 0.9f, 0.08f, 3);
        }

        // ------------------------------------------------------------------ lightning

        private static void Strike(WizardTower t, Enemy e, int wave)
        {
            Vector3 from = t.Emitter(WizardSpell.Lightning);
            Vector3 hit = e.transform.position + Vector3.up * 0.3f;
            Flash(from, Spark, SparkCore);
            Phase1Kit.Bolt(from, hit, SparkCore, 0.2f, 0.35f, 14);
            Phase1Kit.Bolt(from, hit, Spark, 0.11f, 0.6f, 12);
            Bloom.Spawn(from, SparkCore, 1.2f, 0.25f);
            Bloom.Spawn(hit, Spark, 2f, 0.3f);
            // A few forks that die out beside the main bolt.
            for (int i = 0; i < 3; i++)
            {
                Vector3 mid = Vector3.Lerp(from, hit, Random.Range(0.3f, 0.8f));
                Phase1Kit.Bolt(mid, mid + Random.insideUnitSphere * 0.9f + Vector3.down * 0.4f, Spark, 0.06f, 0.2f, 5);
            }
            XRayFlash.Flash(e, 0.16f);
            Fx.Burst(hit, Spark, 22, 0.09f, 3f);
            GroundFlash.Spawn(new Vector3(hit.x, 0.03f, hit.z), Spark, 1.6f, 0.3f);
            SoundLibrary.PlayLoud(SoundThunder, hit, 0.9f, 9f);
            t.Hit(e, WizardTowerLogic.LightningDamage(wave), WizardTowerLogic.LightningHealth, WizardTowerLogic.LightningArmor, WizardTowerLogic.LightningShield);
        }

        // ------------------------------------------------------------------ ice

        private static void Spike(WizardTower t, Enemy e, Vector3 at, int wave)
        {
            Bloom.Spawn(at, FrostCore, 1f, 0.22f);
            Fx.VoxelBurst(at, new[] { Frost, FrostCore, new Color(0.45f, 0.75f, 0.95f) }, 16, 0.07f, 2.6f, 0.6f, 9.81f);
            SoulFx.GlowBurst(at, Frost, 10, 1.6f, 0.3f, 0.45f, 0.2f);
            SoundLibrary.Play(SoundIceHit, at, 0.65f, 0.1f, 4);
            if (!Combat.IsAlive(e) || IsGoldified(e)) return;
            if (t != null) t.Hit(e, WizardTowerLogic.IceDamage(wave), WizardTowerLogic.IceMult, WizardTowerLogic.IceMult, WizardTowerLogic.IceMult);
            if (Combat.IsAlive(e)) e.AddSlow(WizardTowerLogic.IceSlow);
        }

        // ------------------------------------------------------------------ necromancy

        private static void EnsureHooked()
        {
            if (hooked) return;
            hooked = true;
            Combat.EnemyKilled += OnKilled;
        }

        /// <summary>A monster slain in an active aura's reach rises as one of the Necropolis's minions (fixed damage).</summary>
        private static void OnKilled(KillInfo k)
        {
            if (!TreasureRunMode.Active || k.Enemy == null || IsGoldified(k.Enemy)) return;
            foreach (WizardTower t in WizardTower.All)
            {
                if (t == null || !t.AuraActive || !WizardTowerLogic.InRange(k.Position - t.TowerBase)) continue;
                if (RisenMinion.Active.Count >= NecropolisBehaviour.MaxMinions) return;
                int hp = Mathf.Max(1, Mathf.RoundToInt((float)(k.MaxHp * WizardTowerLogic.MinionShare)));
                RisenMinion m = RisenMinion.Raise(k.Enemy, hp, null, isFixed: true);
                if (m == null) return;
                WizardTower owner = t;
                m.OnDetonate = d => { if (owner != null) owner.Count("minion_damage", d); };
                t.Count("raised");
                SoulFx.GlowBurst(k.Position + Vector3.up * 0.3f, Ghoul, 10, 1.2f, 0.18f, 0.6f, 0.4f);
                return;
            }
        }

        // ------------------------------------------------------------------ goldify

        private static void Goldify(WizardTower t, Enemy e)
        {
            if (!Combat.IsAlive(e)) { goldified.Remove(e); return; }
            GoldStatue statue = GoldStatue.Create(e);
            int before = ResourceManager.instance != null ? ResourceManager.instance.gold : 0;
            // Pure gold doesn't split or spawn anything; the wizard counts as one more tower type, like any hit.
            e.deathObjectSpawn = null;
            e.typesOfTowers.Add(TowerType.Global);
            e.Die();
            goldified.Remove(e);
            int gained = ResourceManager.instance != null ? Mathf.Max(0, ResourceManager.instance.gold - before) : 0;
            int bonus = gained * (WizardTowerLogic.GoldifyFactor - 1);
            if (t != null) t.Count("kills");
            SoundLibrary.PlayLoud(SoundGoldify, e.transform.position + Vector3.up * 0.3f, 0.9f, 9f);
            if (statue != null) statue.Init(t, bonus, gained + bonus);
            else Pay(t, e.transform.position, bonus, gained + bonus);
        }

        /// <summary>The goldify bonus (the kill already paid its own gold): no gold multiplier applies to it again.</summary>
        internal static void Pay(WizardTower t, Vector3 at, int bonus, int total)
        {
            if (bonus > 0) ResourceManager.instance?.AddMoney(bonus);
            if (t != null) t.Count("gold", total);
            SoulFx.Coins(at + Vector3.up * 0.2f);
            SoulFx.GlowBurst(at + Vector3.up * 0.3f, GoldLight, 24, 2.4f, 0.22f, 0.8f, 0.5f);
            SoundLibrary.PlayVanilla(Sound.CoinLong, at, 1f, 1f);
            GameHelpers.FloatingText(at + Vector3.up * 1.2f, $"+{total} gold", "Yellow", 1.4f, 1.5f);
        }

        // ------------------------------------------------------------------ shared effects

        /// <summary>A burst of light at an apparatus as it casts.</summary>
        private static void Flash(Vector3 at, Color color, Color core)
        {
            SoulFx.GlowBurst(at, color, 12, 1.4f, 0.22f, 0.5f, 0.3f);
            SoulFx.GlowBurst(at, core, 4, 0.5f, 0.35f, 0.3f, 0.1f);
        }

    }

    /// <summary>A soft glow that breathes (the tower's idle lights).</summary>
    internal sealed class Pulse : MonoBehaviour
    {
        private Renderer r;
        private Color color;
        private float alpha, size, phase;
        /// <summary>Swells the glow (a gadget winding up): size and brightness times this.</summary>
        public float Boost = 1f;

        public void Init(Color c, float a, float s)
        {
            r = GetComponent<Renderer>();
            color = c; alpha = a; size = s;
            phase = Random.Range(0f, 6.28f);
        }

        private void Update()
        {
            float k = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.3f + phase) + 0.08f * Mathf.Sin(Time.time * 7.1f + phase * 2f);
            SoulFx.SetColor(r, new Color(color.r, color.g, color.b, Mathf.Min(1f, alpha * k * Boost)));
            transform.localScale = Vector3.one * size * (0.9f + 0.15f * k) * Boost;
        }
    }

    /// <summary>
    /// A spell in flight: a glowing head with a trail (and flames for a fireball, a crystal shard for an ice spike) on an
    /// arc from <c>from</c> to its target (homing while the target lives) or to a fixed point.
    /// </summary>
    internal sealed class WizardMissile : MonoBehaviour
    {
        private Vector3 from, point;
        private Enemy target;
        private float duration, time, arc;
        private Action<Vector3> onArrive;
        private Transform head, shard;
        private TrailRenderer trail;
        private ParticleSystem flames;
        private bool done, smoke;
        private float smokeTimer;

        /// <summary>
        /// A spell in flight. With <paramref name="model"/> the head is that voxel model, tumbling, trailing embers and smoke
        /// like the Magma Lobber's ball (no glow, no trail line).
        /// </summary>
        public static WizardMissile Launch(Vector3 from, Enemy target, Vector3 point, float duration, float arc, Color color, Color core, float size,
                                           bool fire, Action<Vector3> onArrive, bool shard = false, string model = null, float modelScale = 1f)
        {
            var go = new GameObject("TR_WizardSpell");
            go.transform.position = from;
            var m = go.AddComponent<WizardMissile>();
            m.from = from; m.target = target; m.point = point; m.duration = Mathf.Max(0.1f, duration); m.arc = arc; m.onArrive = onArrive;
            if (model != null)
            {
                m.head = Ascensions.Phase1.Phase1Kit.Model(model, modelScale, go.transform).transform;
                m.smoke = true;
                return m;
            }
            m.head = new GameObject("Head").transform;
            m.head.SetParent(go.transform, false);
            SoulFx.GlowBillboard("Halo", m.head, new Color(color.r, color.g, color.b, 0.8f), size);
            SoulFx.GlowBillboard("Core", m.head, core, size * 0.4f);
            m.trail = go.AddComponent<TrailRenderer>();
            m.trail.sharedMaterial = Fx.GlowMaterial;
            m.trail.time = fire ? 0.3f : 0.18f;
            m.trail.minVertexDistance = 0.04f;
            m.trail.widthCurve = new AnimationCurve(new Keyframe(0f, size * 0.45f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(core, 0f), new GradientColorKey(color, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            m.trail.colorGradient = g;
            m.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m.trail.receiveShadows = false;
            if (fire)
            {
                m.flames = SoulFx.Particles("Flames", go.transform, color, true);
                var main = m.flames.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.35f, size * 0.7f);
                main.gravityModifier = -0.15f;
                var shape = m.flames.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = size * 0.15f;
                var em = m.flames.emission;
                em.rateOverTime = 90f;
                m.flames.Play();
            }
            if (shard)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(go.transform, false);
                s.transform.localScale = new Vector3(0.08f, 0.08f, 0.45f);
                var r = s.GetComponent<MeshRenderer>();
                r.sharedMaterial = Fx.GlowMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                SoulFx.SetColor(r, core);
                m.shard = s.transform;
            }
            return m;
        }

        private void Update()
        {
            if (done) return;
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            if (target != null && !target.dead) point = target.transform.position + Vector3.up * 0.3f;
            Vector3 p = Vector3.Lerp(from, point, t) + Vector3.up * (arc * 4f * t * (1f - t));
            Vector3 step = p - transform.position;
            transform.position = p;
            if (shard != null && step.sqrMagnitude > 1e-6f) shard.rotation = Quaternion.LookRotation(step.normalized);
            if (smoke)
            {
                if (head != null) head.localRotation = Quaternion.Euler(time * 160f, time * 90f, 0f);
                if ((smokeTimer -= Time.deltaTime) <= 0f)
                {
                    smokeTimer = 0.05f;
                    Fx.VoxelBurst(p, Ascensions.Phase2.Phase2Kit.FireColors, 2, 0.07f, 0.3f, 0.5f, -0.5f);
                }
            }
            else if (head != null) head.localScale = Vector3.one * (0.9f + 0.12f * Mathf.Sin(time * 25f));
            if (t < 1f) return;
            done = true;
            if (head != null) Destroy(head.gameObject);
            if (shard != null) Destroy(shard.gameObject);
            if (flames != null) { var em = flames.emission; em.rateOverTime = 0f; }
            if (trail != null) trail.emitting = false;
            Destroy(gameObject, 0.6f);
            try { onArrive?.Invoke(point); }
            catch (Exception e) { Plugin.Log.LogError($"Wizard spell failed: {e}"); }
        }
    }

    /// <summary>A ball of light facing the camera that swells and fades (impacts, casts).</summary>
    internal sealed class Bloom : MonoBehaviour
    {
        private MeshRenderer r;
        private Color color;
        private float size, life, time;

        public static void Spawn(Vector3 at, Color c, float size, float life)
        {
            var go = new GameObject("TR_Bloom");
            go.transform.position = at;
            var b = go.AddComponent<Bloom>();
            b.r = SoulFx.GlowBillboard("Glow", go.transform, c, size * 0.4f);
            b.color = c; b.size = size; b.life = life;
        }

        private void Update()
        {
            time += Time.deltaTime;
            float t = time / life;
            if (t >= 1f) { Destroy(gameObject); return; }
            r.transform.localScale = Vector3.one * size * (0.4f + 0.6f * Mathf.Sqrt(t));
            SoulFx.SetColor(r, new Color(color.r, color.g, color.b, (1f - t) * (1f - t)));
        }
    }

    /// <summary>A flash of light on the ground that spreads and fades (blasts, strikes).</summary>
    internal sealed class GroundFlash : MonoBehaviour
    {
        private MeshRenderer r;
        private Color color;
        private float size, life, time;

        public static void Spawn(Vector3 at, Color c, float size, float life)
        {
            var go = new GameObject("TR_GroundFlash");
            go.transform.position = at + Vector3.up * 0.04f;
            var f = go.AddComponent<GroundFlash>();
            f.r = SoulFx.QuadObject("Disc", go.transform, SoulFx.FlatQuad, SoulFx.SoftGlow, c, size * 0.5f);
            f.color = c; f.size = size; f.life = life;
        }

        private void Update()
        {
            time += Time.deltaTime;
            float t = time / life;
            if (t >= 1f) { Destroy(gameObject); return; }
            r.transform.localScale = Vector3.one * size * (0.5f + 0.5f * Mathf.Sqrt(t));
            SoulFx.SetColor(r, new Color(color.r, color.g, color.b, 0.9f * (1f - t)));
        }
    }

    /// <summary>
    /// The fireball's burn: <see cref="WizardTowerLogic.BurnTicks"/> fixed ticks over <see cref="WizardTowerLogic.BurnSeconds"/>
    /// (not the vanilla burn, so no card or treasure changes it), with flames licking off the monster.
    /// </summary>
    internal sealed class WizardBurn : MonoBehaviour
    {
        private Enemy enemy;
        private WizardTower owner;
        private int tick, left;
        private float timer;
        private ParticleSystem flames;

        public static void Apply(Enemy e, WizardTower owner, int tick)
        {
            WizardBurn b = e.GetComponent<WizardBurn>() ?? e.gameObject.AddComponent<WizardBurn>();
            b.enemy = e;
            b.owner = owner;
            b.tick = Mathf.Max(b.left > 0 ? b.tick : 0, tick);
            b.left = WizardTowerLogic.BurnTicks;
            b.timer = WizardTowerLogic.BurnSeconds / WizardTowerLogic.BurnTicks;
            if (b.flames == null)
            {
                b.flames = SoulFx.Particles("TR_WizardBurn", e.transform, WizardSpells.Fire, true);
                b.flames.transform.localPosition = Vector3.up * 0.25f;
                var main = b.flames.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                var shape = b.flames.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.15f;
                var vel = b.flames.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.y = new ParticleSystem.MinMaxCurve(0.6f);
                vel.x = vel.z = new ParticleSystem.MinMaxCurve(0f);
            }
            var em = b.flames.emission;
            em.rateOverTime = 26f;
            if (!b.flames.isPlaying) b.flames.Play();
        }

        private void Update()
        {
            if (enemy == null || enemy.dead) { Destroy(this); return; }
            if (left <= 0) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = WizardTowerLogic.BurnSeconds / WizardTowerLogic.BurnTicks;
            left--;
            if (owner != null) owner.Hit(enemy, tick, WizardTowerLogic.FireballHealth, WizardTowerLogic.FireballArmor, WizardTowerLogic.FireballShield);
            else Combat.FixedHit(enemy, tick, WizardTowerLogic.FireballHealth, WizardTowerLogic.FireballArmor, WizardTowerLogic.FireballShield);
            if (left <= 0 && flames != null)
            {
                var em = flames.emission;
                em.rateOverTime = 0f;
            }
        }
    }

    /// <summary>
    /// The tornado: a funnel of wind and dust that drifts back up the path (towards where the monsters come from,
    /// <see cref="WizardTowerLogic.TornadoSpeed"/>) for <see cref="WizardTowerLogic.TornadoSeconds"/>. Monsters it touches
    /// (not bosses) are caught: they whirl round inside it, are carried back with it and take its damage 4 times a
    /// second. Released, they walk on from where it left them.
    /// </summary>
    internal sealed class WizardTornado : MonoBehaviour
    {
        private WizardTower owner;
        private Waypoint ahead, behind;
        private float life, tickTimer, catchTimer;
        private int tick;
        private readonly Dictionary<Enemy, (float y, float angle, float radius)> caught = new Dictionary<Enemy, (float, float, float)>();
        private ParticleSystem funnel, dust;
        public static int Active;

        public static void Spawn(WizardTower owner, Vector3 at, Waypoint ahead, int wave)
        {
            var go = new GameObject("TR_WizardTornado");
            go.transform.position = new Vector3(at.x, Mathf.Max(0f, at.y), at.z);
            var t = go.AddComponent<WizardTornado>();
            t.owner = owner;
            t.ahead = ahead;
            Waypoint[] back = ahead != null ? ahead.GetPreviousWaypoints() : new Waypoint[0];
            t.behind = back.Length > 0 ? back[Random.Range(0, back.Length)] : null;
            t.tick = WizardTowerLogic.TornadoTick(wave);
            t.Build();
            SoundLibrary.PlayLoud(WizardSpells.SoundTornado, go.transform.position + Vector3.up * 0.5f, 0.8f, 9f);
            if (owner != null) owner.Count("tornadoes");
        }

        private void Build()
        {
            funnel = SoulFx.Particles("Funnel", transform, new Color(0.5f, 0.52f, 0.58f, 1f), false);
            var main = funnel.main;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.47f, 0.53f, 1f), new Color(0.75f, 0.78f, 0.84f, 1f));
            main.maxParticles = 800;
            var em = funnel.emission;
            em.rateOverTime = 340f;
            var shape = funnel.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var vel = funnel.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(10f);
            vel.radial = new ParticleSystem.MinMaxCurve(0.6f);
            vel.y = new ParticleSystem.MinMaxCurve(2.2f);
            vel.x = vel.z = new ParticleSystem.MinMaxCurve(0f);
            var size = funnel.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.45f, 1f, 3f));
            funnel.Play();

            dust = SoulFx.Particles("Dust", transform, new Color(0.58f, 0.5f, 0.4f, 0.75f), true);
            var dm = dust.main;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            dm.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            dm.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            var de = dust.emission;
            de.rateOverTime = 70f;
            var ds = dust.shape;
            ds.shapeType = ParticleSystemShapeType.Circle;
            ds.radius = 0.35f;
            ds.rotation = new Vector3(90f, 0f, 0f);
            dust.Play();
            Active++;
        }

        private void OnDestroy()
        {
            Active--;
            Release();
        }

        private void Update()
        {
            life += Time.deltaTime;
            if (life >= WizardTowerLogic.TornadoSeconds)
            {
                if (funnel != null && funnel.emission.rateOverTime.constant > 0f)
                {
                    Release();
                    var em = funnel.emission; em.rateOverTime = 0f;
                    var de = dust.emission; de.rateOverTime = 0f;
                    Destroy(gameObject, 1f);
                }
                return;
            }
            // Drift back along the path.
            if (behind != null)
            {
                Vector3 to = behind.transform.position - transform.position;
                to.y = 0f;
                float step = WizardTowerLogic.TornadoSpeed * Time.deltaTime;
                if (to.magnitude <= step)
                {
                    transform.position = new Vector3(behind.transform.position.x, transform.position.y, behind.transform.position.z);
                    ahead = behind;
                    Waypoint[] back = behind.GetPreviousWaypoints();
                    behind = back.Length > 0 ? back[Random.Range(0, back.Length)] : null;
                }
                else transform.position += to.normalized * step;
            }
            // Catch what it touches.
            if ((catchTimer -= Time.deltaTime) <= 0f)
            {
                catchTimer = 0.08f;
                foreach (Enemy e in Combat.EnemiesInRadius(transform.position, WizardTowerLogic.TornadoRadius))
                {
                    if (caught.ContainsKey(e) || Combat.IsBoss(e) || Combat.IsMiniBoss(e) || WizardSpells.IsGoldified(e)) continue;
                    caught[e] = (e.transform.position.y, Random.Range(0f, 6.28f), Random.Range(0.12f, 0.3f));
                    if (owner != null) owner.Count("caught");
                }
            }
            // Grind them.
            if ((tickTimer -= Time.deltaTime) <= 0f)
            {
                tickTimer = 0.25f;
                foreach (Enemy e in new List<Enemy>(caught.Keys))
                    if (Combat.IsAlive(e) && owner != null)
                        owner.Hit(e, tick, WizardTowerLogic.TornadoMult, WizardTowerLogic.TornadoMult, WizardTowerLogic.TornadoMult);
            }
        }

        /// <summary>After the pathfinders moved: the caught monsters whirl inside the funnel and are carried with it.</summary>
        private void LateUpdate()
        {
            if (caught.Count == 0) return;
            float grow = Mathf.Clamp01(life / 0.4f);
            foreach (Enemy e in new List<Enemy>(caught.Keys))
            {
                if (e == null || e.dead) { caught.Remove(e); continue; }
                var (y, angle, radius) = caught[e];
                angle += Time.deltaTime * 7f;
                caught[e] = (y, angle, radius);
                Vector3 c = transform.position;
                e.transform.position = new Vector3(c.x + Mathf.Cos(angle) * radius, y + grow * (0.25f + 0.12f * Mathf.Sin(angle * 0.7f)), c.z + Mathf.Sin(angle) * radius);
                if (e.pathfinder != null && ahead != null)
                {
                    e.pathfinder.currentWaypoint = ahead;
                    e.pathfinder.distanceFromEnd = ahead.distanceFromEnd + Vector3.Distance(ahead.transform.position, c);
                }
            }
        }

        /// <summary>Puts the caught monsters back on the path where the tornado is.</summary>
        private void Release()
        {
            foreach (var kv in caught)
            {
                Enemy e = kv.Key;
                if (e == null || e.dead) continue;
                Vector3 p = transform.position;
                e.transform.position = new Vector3(p.x, kv.Value.y, p.z);
            }
            caught.Clear();
        }
    }

    /// <summary>
    /// The necromantic aura: a glowing green circle round the wizard's whole reach, a turning rune sigil round the tower's
    /// foot, and ghostly wisps rising everywhere inside.
    /// </summary>
    internal sealed class WizardAura : MonoBehaviour
    {
        private LineRenderer ring;
        private MeshRenderer sigil;
        private ParticleSystem wisps;
        private float time;

        public static void Show(WizardTower t)
        {
            var go = new GameObject("TR_WizardAura");
            // Just above the plain grass (height 1 = 0.333), so the ring shows over the land; hills cover parts of it.
            go.transform.position = new Vector3(t.TowerBase.x, 0.36f, t.TowerBase.z);
            var a = go.AddComponent<WizardAura>();
            a.ring = go.AddComponent<LineRenderer>();
            a.ring.sharedMaterial = Fx.GlowMaterial;
            a.ring.useWorldSpace = true;
            a.ring.loop = true;
            a.ring.positionCount = 72;
            a.ring.startWidth = a.ring.endWidth = 0.12f;
            a.ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            a.ring.receiveShadows = false;
            for (int i = 0; i < 72; i++)
            {
                float ang = i * 5f * Mathf.Deg2Rad;
                a.ring.SetPosition(i, new Vector3(t.TowerBase.x, 0.42f, t.TowerBase.z) + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * WizardTowerLogic.Range);
            }
            a.sigil = SoulFx.QuadObject("Sigil", go.transform, SoulFx.FlatQuad, SoulFx.RuneSigil, WizardSpells.Ghoul, 2.4f);
            a.sigil.transform.position = t.TowerBase + Vector3.up * 0.04f;
            a.wisps = SoulFx.Particles("Wisps", go.transform, WizardSpells.Ghoul, true);
            var main = a.wisps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            var shape = a.wisps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = WizardTowerLogic.Range;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var em = a.wisps.emission;
            em.rateOverTime = 45f;
            a.wisps.Play();
        }

        private void Update()
        {
            time += Time.deltaTime;
            float d = WizardTowerLogic.AuraSeconds;
            float fade = Mathf.Clamp01(time / 0.3f) * Mathf.Clamp01((d - time) / 0.5f);
            Color c = WizardSpells.Ghoul;
            float pulse = 0.75f + 0.25f * Mathf.Sin(time * 6f);
            ring.startColor = ring.endColor = new Color(c.r * 0.8f + 0.2f, c.g, c.b * 0.8f + 0.2f, 0.9f * fade * pulse);
            ring.startWidth = ring.endWidth = 0.1f + 0.04f * pulse;
            SoulFx.SetColor(sigil, new Color(c.r, c.g, c.b, 0.85f * fade));
            sigil.transform.localRotation = Quaternion.Euler(0f, -time * 40f, 0f);
            if (time >= d)
            {
                var em = wisps.emission; em.rateOverTime = 0f;
                ring.enabled = sigil.enabled = false;
                Destroy(gameObject, 2f);
                enabled = false;
            }
        }
    }

    /// <summary>
    /// A monster turned to gold: a still, gold copy of it that glints for <see cref="WizardTowerLogic.StatueSeconds"/>, then
    /// bursts into coins and pays the bonus.
    /// </summary>
    internal sealed class GoldStatue : MonoBehaviour
    {
        private static readonly Color Dark = new Color(0.45f, 0.3f, 0.05f), Light = new Color(1f, 0.93f, 0.55f);
        private static readonly Dictionary<Material, Material> cache = new Dictionary<Material, Material>();
        private WizardTower owner;
        private int bonus, total;
        private float time;
        private bool ready;

        public static GoldStatue Create(Enemy source)
        {
            GameObject src = source.gameObject;
            bool was = src.activeSelf;
            src.SetActive(false);
            GameObject copy = Instantiate(src, src.transform.position, src.transform.rotation);
            src.SetActive(was);
            copy.name = "TR_GoldStatue_" + src.name;
            foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(mb);
            foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (Rigidbody r in copy.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
            foreach (Canvas c in copy.GetComponentsInChildren<Canvas>(true)) if (c.gameObject != copy) Destroy(c.gameObject);
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) { r.enabled = false; continue; }
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Gilded(mats[i]);
                r.sharedMaterials = mats;
            }
            copy.SetActive(true);
            var statue = copy.AddComponent<GoldStatue>();
            SoulFx.GlowBurst(copy.transform.position + Vector3.up * 0.3f, WizardSpells.GoldLight, 16, 1.6f, 0.2f, 0.6f, 0.3f);
            return statue;
        }

        /// <summary>A gold copy of a material: its palette mapped onto shades of gold, glossy and metallic where the shader has them.</summary>
        private static Material Gilded(Material m)
        {
            if (m == null) return null;
            if (cache.TryGetValue(m, out Material g) && g != null) return g;
            g = new Material(m) { name = "TR_Gold" };
            Texture gold = Fx.RampTexture(m.mainTexture, Dark, Light);
            if (gold != null) g.mainTexture = gold;
            g.color = Color.white;
            if (g.HasProperty("_Glossiness")) g.SetFloat("_Glossiness", 0.85f);
            if (g.HasProperty("_Metallic")) g.SetFloat("_Metallic", 0.75f);
            if (g.HasProperty("_EmissionColor"))
            {
                g.EnableKeyword("_EMISSION");
                g.SetColor("_EmissionColor", new Color(0.25f, 0.18f, 0.02f));
            }
            cache[m] = g;
            return g;
        }

        public void Init(WizardTower t, int bonusGold, int totalGold)
        {
            owner = t; bonus = bonusGold; total = totalGold; ready = true;
        }

        private void Update()
        {
            time += Time.deltaTime;
            // A glint now and then while it stands.
            if (Random.value < Time.deltaTime * 6f)
                SoulFx.GlowBurst(transform.position + Vector3.up * Random.Range(0.1f, 0.5f) + Random.insideUnitSphere * 0.15f,
                                 WizardSpells.GoldCore, 2, 0.3f, 0.12f, 0.3f, 0.1f);
            if (time < WizardTowerLogic.StatueSeconds) return;
            Vector3 at = transform.position;
            if (ready) WizardSpells.Pay(owner, at, bonus, total);
            Fx.VoxelBurst(at + Vector3.up * 0.3f, new[] { WizardSpells.GoldLight, WizardSpells.GoldCore, new Color(0.8f, 0.6f, 0.15f) }, 24, 0.07f, 2.6f, 0.9f, 9.81f);
            Destroy(gameObject);
        }
    }
}
