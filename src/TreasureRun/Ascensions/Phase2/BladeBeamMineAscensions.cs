using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Ascensions.Phase1;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase2
{
    /// <summary>
    /// Bladestorm (Shredder, Epic): no more saw blades thrown down the path; instead whole saw blades (1.5 across) lie
    /// flat and circle the tower on a ring whose outer edge is the tower's range, each spinning fast on its own axis the
    /// other way round. A blade cuts a monster each time it passes over it (75% damage, the Shredder's bleed). More
    /// range makes the ring bigger and adds blades at the same spacing; attack speed turns the ring faster. Monsters
    /// right next to the tower are inside the ring and safe. Base range 2 (<see cref="BladestormLogic"/>).
    /// </summary>
    internal sealed class Bladestorm : AscendedTower, IBaseRangeOverride
    {
        public const string Id = "bladestorm";
        public float BaseRange => BladestormLogic.BaseRange;
        /// <summary>The blade model is 15 voxels (1.5 units) across at scale 1.</summary>
        private const float BladeScale = 1f;

        private sealed class Blade
        {
            public Transform Transform;
            /// <summary>Monsters under the blade last frame (a pass hits once, on entry).</summary>
            public readonly HashSet<Enemy> Under = new HashSet<Enemy>();
        }

        private readonly List<Blade> blades = new List<Blade>();
        private readonly List<Enemy> scratch = new List<Enemy>();
        private float layoutRange = -1f, turns, spin, hitSoundBudget;
        private Transform hub;
        private SoundLoop whirr;
        public long Hits { get; private set; }
        public int BladeCount => blades.Count;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Bladestorm", Rarity = Rarity.Epic, Kind = AscensionKind.Replace,
                Target = AscensionTarget.Shredder, Component = typeof(Bladestorm), CardModel = "ascensions/shredder/blade",
                Description = $"Big saw blades circle the Shredder at the edge of its range and cut every monster they pass " +
                              $"({BladestormLogic.DamageFactor:P0} damage per pass, with bleed). More range means a bigger circle with more " +
                              $"blades, more attack speed spins them faster. Monsters right next to it are safe. Base range {BladestormLogic.BaseRange:0}.",
            });
        }

        protected override void OnAscend()
        {
            SwapVanillaMesh("BaseArt", "ascensions/shredder/bladestorm_base");
            SwapVanillaMesh("TurretArt", "ascensions/shredder/bladestorm_hub");
            hub = Tower.turret != null ? Tower.turret.transform : null;
            whirr = SoundLoop.Start("ascensions/bladestorm/blade_whirr", transform, 0.35f);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ClearBlades();
            if (whirr != null) Destroy(whirr.gameObject);
        }

        private void ClearBlades()
        {
            foreach (Blade b in blades) if (b.Transform != null) Destroy(b.Transform.gameObject);
            blades.Clear();
        }

        private void Layout()
        {
            ClearBlades();
            layoutRange = Tower.range;
            int count = BladestormLogic.Count(Tower.range);
            for (int i = 0; i < count; i++)
                blades.Add(new Blade { Transform = Phase1Kit.Model("ascensions/shredder/blade", BladeScale, transform).transform });
        }

        // The tower stands on its tile's surface: the blades spin 0.25 above it, clear of grass at the same height (higher
        // terrain nearby can still cut through them). Hits only compare positions on the ground plane, so the blades'
        // and the monsters' heights never matter (a tower high above the path still hits).
        private float BladeY => transform.position.y + 0.25f;

        private void Update()
        {
            if (Tower == null) return;
            if (Mathf.Abs(Tower.range - layoutRange) > 0.01f) Layout();
            float dt = Time.deltaTime;
            turns += BladestormLogic.RevsPerSecond(Phase2Kit.AttackSpeedFactor(Tower)) * dt;
            if (turns > 1000f) turns -= 1000f;
            spin = (spin + BladestormLogic.SpinRevsPerSecond * 360f * dt) % 360f;
            // The ring turns counter-clockwise seen from above (negative yaw); hub along with it, blades the other way.
            if (hub != null) hub.localRotation = Quaternion.Euler(0f, -turns * 360f * 2f, 0f);

            Vector3 c = transform.position;
            float y = BladeY;
            for (int i = 0; i < blades.Count; i++)
            {
                Vector3 off = BladestormLogic.BladeOffset(i, blades.Count, turns, Tower.range);
                Transform tr = blades[i].Transform;
                tr.position = new Vector3(c.x + off.x, y, c.z + off.z);
                tr.rotation = Quaternion.Euler(0f, spin + i * 37f, 0f);
            }
            HitMonsters();
            hitSoundBudget = Mathf.Min(3f, hitSoundBudget + dt * 6f);
        }

        private void HitMonsters()
        {
            if (SpawnManager.instance == null || blades.Count == 0) return;
            Vector3 c = transform.position;
            float outer = Tower.range + 0.5f;
            Tower t = Tower;
            int damage = BladestormLogic.HitDamage(t.damage);
            scratch.Clear();
            foreach (Enemy e in SpawnManager.instance.currentEnemies)
            {
                if (!Combat.IsAlive(e)) continue;
                Vector3 d = e.transform.position - c;
                d.y = 0f;
                if (d.sqrMagnitude <= outer * outer) scratch.Add(e);
            }
            for (int i = 0; i < blades.Count; i++)
            {
                Blade blade = blades[i];
                Vector3 off = BladestormLogic.BladeOffset(i, blades.Count, turns, t.range);
                blade.Under.RemoveWhere(e => e == null || !scratch.Contains(e));
                foreach (Enemy e in scratch)
                {
                    Vector3 d = e.transform.position - c;
                    d.y = 0f;
                    bool under = BladestormLogic.Touches(off, d, Phase2Kit.Radius(e));
                    if (!under) { blade.Under.Remove(e); continue; }
                    if (!blade.Under.Add(e) || !Combat.IsAlive(e)) continue; // already under this blade: one hit per pass
                    e.TakeDamage(TowerType.Shredder, damage, t.healthDamage, t.armorDamage, t.shieldDamage, t.slowPercent,
                                 t.bleedPercent, t.burnPercent, t.poisonPercent, t.critChance, t.stunChance);
                    Hits++;
                    if (hitSoundBudget >= 1f)
                    {
                        hitSoundBudget -= 1f;
                        SoundLibrary.PlayVanilla(Sound.ShredderHit, e.transform.position, Random.Range(1.1f, 1.35f), 0.35f);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Beam Cannon (Particle Cannon, Epic): a continuous beam on its target while there is mana, ticking 10 times a second
    /// on the first monster on the line: 1.3x the vanilla damage per second for 1.6x the mana per second
    /// (<see cref="BeamCannonLogic"/>).
    /// </summary>
    internal sealed class BeamCannon : AscendedTower
    {
        public const string Id = "beam_cannon";
        private static readonly Color Core = new Color(1f, 0.95f, 1f, 0.95f), Glow = new Color(0.75f, 0.35f, 1f, 0.55f);

        private LineRenderer core, glow;
        private Light flare;
        private SoundLoop hum;
        private const float RetargetInterval = 0.05f;
        private float tickTimer, damageCarry, manaCarry, sparkTimer, nextRetarget;
        public long Ticks { get; private set; }

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Beam Cannon", Rarity = Rarity.Epic, Kind = AscensionKind.Replace,
                Target = AscensionTarget.ParticleCannon, Component = typeof(BeamCannon), CardModel = "ascensions/particle/beam_turret",
                Description = $"Fires a continuous beam at its target while there is mana: {BeamCannonLogic.DpsFactor:0.#}x the damage per " +
                              $"second for {BeamCannonLogic.ManaFactor:0.#}x the mana. The beam hits the first monster in its way.",
            });
        }

        protected override void OnAscend()
        {
            SwapVanillaMesh("TurretArt", "ascensions/particle/beam_turret");
            SwapVanillaMesh("BaseArt", "ascensions/particle/beam_base");
            glow = Phase2Kit.Line("BeamGlow", Glow, 0.22f, transform);
            core = Phase2Kit.Line("BeamCore", Core, 0.07f, transform);
            var lightGo = new GameObject("TR_BeamLight");
            lightGo.transform.SetParent(transform, false);
            flare = lightGo.AddComponent<Light>();
            flare.type = LightType.Point;
            flare.color = new Color(0.8f, 0.5f, 1f);
            flare.range = 2.5f;
            flare.intensity = 0f;
            SetBeam(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (core != null) Destroy(core.gameObject);
            if (glow != null) Destroy(glow.gameObject);
            if (flare != null) Destroy(flare.gameObject);
            if (hum != null) Destroy(hum.gameObject);
        }

        private void SetBeam(bool on)
        {
            if (core != null) core.enabled = on;
            if (glow != null) glow.enabled = on;
            if (flare != null) flare.intensity = on ? 1.6f : 0f;
            if (on && hum == null) hum = SoundLoop.Start("ascensions/beam_cannon/beam_hum", transform, 0.45f);
            else if (!on && hum != null) { Destroy(hum.gameObject); hum = null; }
        }

        private Vector3 Muzzle => Tower.muzzle != null ? Tower.muzzle.position : transform.position + Vector3.up;

        private void Update()
        {
            Tower t = Tower;
            Enemy target = Phase2Kit.Target(t);
            if (!Combat.IsAlive(target) && SpawnManager.instance != null && Time.time >= nextRetarget)
            {
                // Vanilla towers re-detect every 0.25–1 s; the beam swings to the next monster right away.
                nextRetarget = Time.time + RetargetInterval;
                t.DetectEnemies();
                target = Phase2Kit.Target(t);
            }
            if (!Combat.IsAlive(target) || SpawnManager.instance == null) { SetBeam(false); tickTimer = 0f; return; }
            Vector3 from = Muzzle;
            Vector3 aim = target.transform.position + Vector3.up * 0.3f;
            Enemy victim = FirstOnLine(from, aim, target, out Vector3 hitPoint);
            int ticks = FireClock.Due(ref tickTimer, Time.deltaTime, 1f / BeamCannonLogic.TicksPerSecond, 3);
            bool powered = true;
            for (int i = 0; i < ticks; i++)
            {
                if (t.consumesMana)
                {
                    int mana = BeamCannonLogic.Take(ref manaCarry, BeamCannonLogic.ManaPerTick(t.damage, t.finalManaConsumption, t.rps));
                    if (mana > 0 && !ResourceManager.instance.CheckMana(mana)) { powered = false; manaCarry += mana; break; }
                    if (mana > 0) ResourceManager.instance.SpendMana(mana);
                }
                int dmg = BeamCannonLogic.Take(ref damageCarry, BeamCannonLogic.DamagePerTick(t.damage, t.rps));
                if (dmg > 0 && Combat.IsAlive(victim))
                    victim.TakeDamage(TowerType.ParticleCannon, dmg, t.healthDamage, t.armorDamage, t.shieldDamage, t.slowPercent,
                                      t.bleedPercent, t.burnPercent, t.poisonPercent, t.critChance, t.stunChance);
                Ticks++;
            }
            SetBeam(powered);
            if (!powered) return;
            float w = 1f + 0.25f * Mathf.Sin(Time.time * 40f) + Random.Range(-0.1f, 0.1f);
            core.startWidth = core.endWidth = 0.07f * w;
            glow.startWidth = 0.24f * w;
            glow.endWidth = 0.18f * w;
            core.SetPosition(0, from); core.SetPosition(1, hitPoint);
            glow.SetPosition(0, from); glow.SetPosition(1, hitPoint);
            flare.transform.position = hitPoint;
            sparkTimer -= Time.deltaTime;
            if (sparkTimer <= 0f)
            {
                sparkTimer = 0.12f;
                Fx.Burst(hitPoint, Glow, 4, 0.06f, 1.2f);
            }
        }

        /// <summary>The first living monster on the line from the muzzle to the target (the target itself if none).</summary>
        private Enemy FirstOnLine(Vector3 from, Vector3 to, Enemy target, out Vector3 point)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            point = to;
            if (len < 0.01f) return target;
            Enemy best = target;
            float bestDist = len;
            foreach (RaycastHit h in Physics.SphereCastAll(from, 0.12f, d / len, len, Tower.enemyLayerMask, QueryTriggerInteraction.Collide))
            {
                Enemy e = h.collider.GetComponentInParent<Enemy>();
                if (e == null || !Combat.IsAlive(e) || h.distance >= bestDist) continue;
                best = e;
                bestDist = h.distance;
                point = h.distance > 0f ? h.point : e.transform.position + Vector3.up * 0.3f;
            }
            return best;
        }
    }

    /// <summary>
    /// Dead Man's Land (Encampment, Epic): mines are no longer cleared when a wave ends. A mine landing on a lying mine
    /// merges into it as an extra charge; a mine with N charges explodes up to N times, once per physics step while a
    /// monster is on it (<see cref="DeadMansLandLogic"/>, <see cref="MineCharges"/>). Merging keeps the count bounded by
    /// the path cells in range. A mine whose Encampment is gone is cleared at the next wave end again.
    /// </summary>
    internal sealed class DeadMansLand : AscendedTower
    {
        public const string Id = "dead_mans_land";

        /// <summary>Set while a Permacamp's vanilla Fire runs (the Projectile.SetStats postfix tags its mine).</summary>
        internal static DeadMansLand Firing;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Dead Man's Land", TowerName = "Permacamp", Rarity = Rarity.Epic, Kind = AscensionKind.Augment,
                Target = AscensionTarget.Encampment, Component = typeof(DeadMansLand), CardModel = "ascensions/encampment/permacamp",
                Description = "Mines stay on the path after the wave ends. A mine thrown onto another one adds a charge to it: " +
                              "a mine with several charges explodes again and again while monsters stand on it.",
            });
            TreasureRunMode.RunStarted += _ => PersistentMine.All.Clear();
        }

        protected override void OnAscend() => SwapVanillaMesh("Encampment", "ascensions/encampment/permacamp");
    }

    /// <summary>A Permacamp mine: survives wave ends, merges with lying mines, explodes once per charge.</summary>
    internal sealed class PersistentMine : MonoBehaviour
    {
        internal static readonly List<PersistentMine> All = new List<PersistentMine>();
        /// <summary>Explosions of all persistent mines so far (scenarios).</summary>
        internal static int TotalExplosions;
        private static readonly List<Vector3> positions = new List<Vector3>();
        private static readonly List<PersistentMine> landed = new List<PersistentMine>();

        public Landmine Mine { get; private set; }
        public DeadMansLand Owner { get; private set; }
        public readonly MineCharges Charges = new MineCharges();
        public bool Landed { get; private set; }
        public int Explosions { get; private set; }
        private bool recheck, released;
        private MeshFilter art;
        private Mesh vanillaMesh;
        private Material vanillaMaterial;

        public static void Attach(Landmine mine, DeadMansLand owner)
        {
            var p = mine.gameObject.AddComponent<PersistentMine>();
            p.Mine = mine;
            p.Owner = owner;
            All.Add(p);
        }

        private void OnDestroy() => All.Remove(this);

        private void Update()
        {
            if (!Landed && Mine != null && Mine.armed) Land();
            if (Owner == null && !released && SpawnManager.instance != null)
            {
                // Its Encampment was demolished (or the ascension revoked): back to vanilla clean-up at the wave end.
                released = true;
                if (!SpawnManager.instance.destroyOnNewLevel.Contains(gameObject)) SpawnManager.instance.destroyOnNewLevel.Add(gameObject);
            }
        }

        private void Land()
        {
            Landed = true;
            positions.Clear();
            landed.Clear();
            foreach (PersistentMine m in All)
                if (m != null && m != this && m.Landed && !m.Charges.Spent) { positions.Add(m.transform.position); landed.Add(m); }
            int merge = DeadMansLandLogic.FindMerge(positions, transform.position);
            if (merge < 0) { UpdateLook(); return; }
            PersistentMine into = landed[merge];
            into.Absorb(this);
            if (SpawnManager.instance != null) SpawnManager.instance.destroyOnNewLevel.Remove(gameObject);
            Destroy(gameObject);
        }

        private void Absorb(PersistentMine other)
        {
            Charges.Merge(other.Charges.Charges);
            Landmine a = Mine, b = other.Mine;
            // Keep the stronger throw's stats (later mines from levelled camps hit harder).
            if (b.damage > a.damage) { a.damage = b.damage; a.healthDamage = b.healthDamage; a.armorDamage = b.armorDamage; a.shieldDamage = b.shieldDamage; }
            a.blastRadius = Mathf.Max(a.blastRadius, b.blastRadius);
            if (Owner == null && other.Owner != null) { Owner = other.Owner; released = false; SpawnManager.instance?.destroyOnNewLevel.Remove(gameObject); }
            UpdateLook();
            Fx.VoxelBurst(transform.position + Vector3.up * 0.1f, Phase2Kit.SmokeColors, 4, 0.04f, 0.6f, 0.3f, 1f);
        }

        private void UpdateLook()
        {
            if (art == null)
            {
                foreach (MeshFilter f in GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null && f.sharedMesh.name == "default")
                    {
                        art = f;
                        vanillaMesh = f.sharedMesh;
                        vanillaMaterial = f.GetComponent<MeshRenderer>()?.sharedMaterial;
                        break;
                    }
            }
            if (art == null) return;
            int look = DeadMansLandLogic.Look(Charges.Charges);
            VoxAsset asset = look == 0 ? null : VoxelLibrary.Get(look == 1 ? "ascensions/encampment/mine_2" : "ascensions/encampment/mine_4");
            // Back to one charge: the vanilla mesh needs its own material again (with a stack's palette it renders white).
            art.sharedMesh = asset != null ? asset.Mesh : vanillaMesh;
            MeshRenderer r = art.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = asset != null ? asset.Material : vanillaMaterial;
        }

        /// <summary>Replaces Landmine.Explode: one blast per charge, the mine stays while charges are left.</summary>
        internal void Detonate()
        {
            if (!Charges.Step(true)) return;
            Explosions++;
            TotalExplosions++;
            Landmine m = Mine;
            foreach (Collider c in Physics.OverlapSphere(transform.position, m.blastRadius, m.layermask, QueryTriggerInteraction.Collide))
            {
                IDamageable d = c.GetComponent<IDamageable>();
                if (d != null) m.DealDamage(d);
            }
            if (m.explosion != null) Instantiate(m.explosion, transform.position, Quaternion.identity).transform.localScale = Vector3.one * 0.5f;
            if (Charges.Spent)
            {
                SpawnManager.instance?.destroyOnNewLevel.Remove(gameObject);
                Destroy(gameObject);
                return;
            }
            UpdateLook();
            recheck = true;
        }

        private void FixedUpdate()
        {
            if (!recheck) return;
            recheck = false;
            foreach (Collider c in Physics.OverlapSphere(transform.position, 0.3f, Mine.layermask, QueryTriggerInteraction.Collide))
            {
                Enemy e = c.GetComponentInParent<Enemy>();
                if (!Combat.IsAlive(e)) continue;
                Detonate();
                return;
            }
        }
    }

    [HarmonyPatch]
    internal static class BladeBeamMinePatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.Fire))]
        private static bool TowerFirePrefix(Tower __instance)
        {
            if (!TreasureRunMode.Active) return true;
            if (__instance.towerType == TowerType.Shredder) return __instance.GetComponent<Bladestorm>() == null;
            if (__instance.towerType == TowerType.ParticleCannon) return __instance.GetComponent<BeamCannon>() == null;
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Encampment), nameof(Encampment.Fire))]
        private static void EncampmentFirePrefix(Encampment __instance)
        {
            DeadMansLand.Firing = TreasureRunMode.Active ? __instance.GetComponent<DeadMansLand>() : null;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Encampment), nameof(Encampment.Fire))]
        private static void EncampmentFirePostfix() => DeadMansLand.Firing = null;

        [HarmonyPostfix, HarmonyPatch(typeof(Projectile), nameof(Projectile.SetStats))]
        private static void MineSetStatsPostfix(Projectile __instance)
        {
            if (DeadMansLand.Firing != null && __instance is Landmine mine) PersistentMine.Attach(mine, DeadMansLand.Firing);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Landmine), nameof(Landmine.Start))]
        private static void LandmineStartPostfix(Landmine __instance)
        {
            if (__instance.GetComponent<PersistentMine>() != null) SpawnManager.instance.destroyOnNewLevel.Remove(__instance.gameObject);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Landmine), nameof(Landmine.Explode))]
        private static bool LandmineExplodePrefix(Landmine __instance)
        {
            PersistentMine p = __instance.GetComponent<PersistentMine>();
            if (p == null) return true;
            p.Detonate();
            return false;
        }
    }
}
