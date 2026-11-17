using HarmonyLib;
using TreasureRun.Audio;
using TreasureRun.Castles;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase2
{
    /// <summary>
    /// Icicle Cannon (Frost Keep, Uncommon): the keep stops dropping snowflakes and becomes a targeting tower that fires
    /// straight icicles, piercing every monster on the line (each once) with the keep's slow. Base range 6. It still
    /// pays the keep's mana per second during waves (<see cref="IcicleCannonLogic"/>).
    /// </summary>
    internal sealed class IcicleCannon : AscendedTower, IBaseRangeOverride
    {
        public const string Id = "icicle_cannon";
        public float BaseRange => IcicleCannonLogic.BaseRange;

        /// <summary>Height of the ice ballista: on top of the keep's tower (the vanilla, hidden turret sits inside it).</summary>
        private const float TurretHeight = 0.92f;
        private float timer, nextManaCheck;
        private bool hasMana = true, turretWasActive, wasSquare;
        private GameObject turretObject;
        private Vector3 turretPosition;

        public int Shots { get; private set; }

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Icicle Cannon", Rarity = Rarity.Uncommon, Kind = AscensionKind.Replace,
                Target = AscensionTarget.FrostKeep, Component = typeof(IcicleCannon), CardModel = "ascensions/frost/icicle_turret",
                Description = $"Instead of dropping snowflakes, the keep fires icicles in a straight line that pierce every monster " +
                              $"they pass ({IcicleCannonLogic.DamageFactor}x damage) and slow them. Base range {IcicleCannonLogic.BaseRange:0}.",
            });
        }

        protected override void OnAscend()
        {
            SwapVanillaMesh("BaseArt", "ascensions/frost/icicle_keep");
            wasSquare = Tower.squareUI; // the keep shows a square range (its 5×5 drop area); a cannon has a circle
            Tower.squareUI = false;
            // The keep's turret (a flame thrower turret) is inactive in vanilla; it becomes the ice ballista.
            // The keep has no turret reference (it never aims); find the inactive Turret child by name.
            turretObject = Tower.turret != null ? Tower.turret : transform.Find("Turret")?.gameObject;
            if (turretObject != null)
            {
                turretWasActive = turretObject.activeSelf;
                turretPosition = turretObject.transform.localPosition;
                turretObject.transform.localPosition = new Vector3(turretPosition.x, TurretHeight, turretPosition.z);
                turretObject.SetActive(true);
                foreach (ParticleSystem ps in turretObject.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                SwapVanillaMesh("TurretArt", "ascensions/frost/icicle_turret");
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Tower != null) Tower.squareUI = wasSquare;
            if (turretObject != null)
            {
                turretObject.SetActive(turretWasActive);
                turretObject.transform.localPosition = turretPosition;
            }
        }

        /// <summary>Replaces Dropper.Update: mana per second, aiming, XP and the icicle fire loop.</summary>
        internal void Tick()
        {
            Dropper d = (Dropper)Tower;
            bool combat = SpawnManager.instance != null && SpawnManager.instance.combat;
            if (d.manaPerSecond > 0 && combat && Time.time >= nextManaCheck)
            {
                nextManaCheck = Time.time + 1f;
                hasMana = ResourceManager.instance.CheckMana(d.manaPerSecond);
                if (hasMana) ResourceManager.instance.SpendMana(d.manaPerSecond);
            }
            Enemy target = Phase2Kit.Target(Tower);
            if (target != null)
            {
                if (turretObject != null)
                {
                    Transform tt = turretObject.transform;
                    Vector3 aim = Phase1.Phase1Kit.Flat(target.transform.position - tt.position);
                    if (aim.sqrMagnitude > 0.001f)
                        tt.rotation = Quaternion.Slerp(tt.rotation, Quaternion.LookRotation(aim, Vector3.up), Time.deltaTime * 12f);
                }
                Tower.GainXP();
            }
            float interval = IcicleCannonLogic.Interval(Phase2Kit.AttackSpeedFactor(Tower));
            if (target == null || !hasMana) { timer = Mathf.Min(timer + Time.deltaTime, interval); return; }
            if (FireClock.Due(ref timer, Time.deltaTime, interval, 1) > 0) Fire(target);
        }

        private void Fire(Enemy target)
        {
            Tower t = Tower;
            Transform muzzle = t.muzzle != null ? t.muzzle : turretObject != null ? turretObject.transform.Find("Muzzle") : null;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * TurretHeight;
            Vector3 dir = Phase1.Phase1Kit.Flat(target.transform.position - from);
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            if (turretObject != null) turretObject.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            int damage = IcicleCannonLogic.ShotDamage(t.damage);
            float flat = Phase1.Phase1Kit.Flat(target.transform.position - from).magnitude;
            LineShot.Launch("ascensions/frost/icicle", 0.3f, from, dir, target.transform.position.y + 0.25f, flat * 0.8f,
                IcicleCannonLogic.Speed, t.range * IcicleCannonLogic.TravelFactor, t.enemyLayerMask, e =>
                {
                    e.TakeDamage(TowerType.Frost, damage, t.healthDamage, t.armorDamage, t.shieldDamage, t.slowPercent,
                                 t.bleedPercent, t.burnPercent, t.poisonPercent, t.critChance, t.stunChance);
                    Fx.VoxelBurst(e.transform.position + Vector3.up * 0.4f, Phase2Kit.IceColors, 6, 0.05f, 1.4f, 0.4f, 2f);
                    SoundLibrary.Play("ascensions/icicle_cannon/icicle_shatter", e.transform.position, 0.6f, 0.1f, 4);
                    return true;
                });
            Fx.VoxelBurst(from, Phase2Kit.IceColors, 5, 0.05f, 1f, 0.35f, 0f);
            SoundLibrary.Play("ascensions/icicle_cannon/icicle_launch", from, 0.8f, 0.06f, 3);
            Shots++;
        }
    }

    /// <summary>
    /// Magma Lobber (Flame Thrower, Epic): no more flames; every 4 s (at the tower's attack speed) it lobs a big ball of
    /// magma like the Siege Workshop's catapult. The blast hits everything within 1.4 (+ blast cards) for 4x damage and
    /// sets it on fire (+100% burn) and leaves a cooling magma pool that burns monsters standing in it. Base range 8. Pays the flame thrower's mana for every second of reload.
    /// </summary>
    internal sealed class MagmaLobber : AscendedTower, IBaseRangeOverride
    {
        public const string Id = "magma_lobber";
        public float BaseRange => MagmaLobberLogic.BaseRange;

        private float timer;
        public int Lobs { get; private set; }

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Magma Lobber", Rarity = Rarity.Epic, Kind = AscensionKind.Replace,
                Target = AscensionTarget.FlameThrower, Component = typeof(MagmaLobber), CardModel = "ascensions/flame/magma_turret",
                Description = $"Instead of flames, slowly lobs big balls of magma: {MagmaLobberLogic.DamageFactor}x damage to every " +
                              $"monster in a large blast, which also catch fire (+{MagmaLobberLogic.ExtraBurn:P0} burn). The magma pool left behind " +
                              $"keeps hurting and burning monsters in it ({MagmaLobberLogic.PoolBurnPerSecond:P0} of the damage per second each). Base range {MagmaLobberLogic.BaseRange:0}.",
            });
        }

        protected override void OnAscend()
        {
            SwapVanillaMesh("BaseArt", "ascensions/flame/magma_base");
            SwapVanillaMesh("TurretArt", "ascensions/flame/magma_turret");
            var ft = (FlameThrower)Tower;
            if (ft.flames != null) ft.flames.Stop();
            if (ft.audioS != null) ft.audioS.Stop();
            ft.flaming = false;
            ft.soundPlaying = false;
            timer = MagmaLobberLogic.BaseInterval * 0.5f;
        }

        /// <summary>Replaces FlameThrower.Update (and the base Tower.Update).</summary>
        internal void Tick()
        {
            Enemy target = Phase2Kit.Target(Tower);
            float interval = MagmaLobberLogic.Interval(Phase2Kit.AttackSpeedFactor(Tower));
            if (target == null) { timer = Mathf.Min(timer + Time.deltaTime, interval); return; }
            Ballistics.SolveLeading(Muzzle, target, out Vector3 velocity, out _);
            if (Tower.turret != null)
            {
                Vector3 flat = Phase1.Phase1Kit.Flat(velocity);
                if (flat.sqrMagnitude > 0.001f)
                    Tower.turret.transform.rotation = Quaternion.Slerp(Tower.turret.transform.rotation, Quaternion.LookRotation(flat, Vector3.up), Time.deltaTime * 6f);
            }
            Tower.GainXP();
            if (FireClock.Due(ref timer, Time.deltaTime, interval, 1) > 0) Lob(target);
        }

        private Vector3 Muzzle => Tower.muzzle != null ? Tower.muzzle.position + Vector3.up * 0.15f : transform.position + Vector3.up;

        private void Lob(Enemy target)
        {
            Tower t = Tower;
            if (t.consumesMana)
            {
                int cost = MagmaLobberLogic.ManaPerLob(t.damage, t.manaConsumptionRate);
                if (!ResourceManager.instance.CheckMana(cost)) return;
                ResourceManager.instance.SpendMana(cost);
            }
            Vector3 from = Muzzle;
            Ballistics.SolveLeading(from, target, out Vector3 velocity, out Vector3 aim);
            float ground = target.transform.position.y + 0.1f;
            int damage = MagmaLobberLogic.LobDamage(t.damage);
            float radius = MagmaLobberLogic.BlastRadius + t.blastRadius;
            float smokeTimer = 0f;
            LobShot.Fire("ascensions/flame/magma_ball", 0.75f, from, velocity, ground, pos => Impact(t, pos, damage, radius), 160f, shot =>
            {
                smokeTimer -= Time.deltaTime;
                if (smokeTimer > 0f) return;
                smokeTimer = 0.06f;
                Fx.VoxelBurst(shot.transform.position, Phase2Kit.FireColors, 2, 0.06f, 0.3f, 0.5f, -0.5f);
            });
            Fx.VoxelBurst(from, Phase2Kit.FireColors, 10, 0.07f, 1.2f, 0.4f, 0f);
            SoundLibrary.Play("ascensions/magma_lobber/magma_lob", from, 0.9f, 0.05f, 3);
            Lobs++;
        }

        private static void Impact(Tower t, Vector3 pos, int damage, float radius)
        {
            if (t != null)
            {
                Phase2Kit.Blast(t, pos, radius, damage, MagmaLobberLogic.ExtraBurn);
                MagmaPool.Spawn(t, pos, damage, radius);
            }
            Fx.VoxelBurst(pos + Vector3.up * 0.15f, Phase2Kit.FireColors, 30, 0.12f, 2.4f * radius, 0.8f, 2.5f);
            Fx.Burst(pos + Vector3.up * 0.2f, new Color(1f, 0.5f, 0.1f), 24, 0.14f, 1.6f);
            SoundLibrary.Play("ascensions/magma_lobber/magma_impact", pos, 1f, 0.06f, 3);
        }
    }

    /// <summary>
    /// The pool a magma ball leaves: glowing orange at first, then cooling to a black crust (three models), shrinking
    /// away at the end. Monsters standing in it take 20% of the shot's damage per second and burn for the same amount
    /// (<see cref="MagmaLobberLogic.PoolBurnPerTick"/>).
    /// </summary>
    internal sealed class MagmaPool : MonoBehaviour
    {
        private static readonly string[] Stages = { "ascensions/flame/scorch_hot", "ascensions/flame/scorch_warm", "ascensions/flame/scorch" };
        private int lobDamage, health, armor, shield, stage = -1;
        private float radius, age, tick;
        private MeshFilter filter;
        private MeshRenderer rend;
        private Vector3 scale;

        public static void Spawn(Tower t, Vector3 pos, int lobDamage, float radius)
        {
            GameObject go = Phase1.Phase1Kit.Model(Stages[0], radius);
            go.transform.position = pos + Vector3.up * 0.02f;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var p = go.AddComponent<MagmaPool>();
            p.lobDamage = lobDamage;
            p.radius = radius;
            p.health = t.healthDamage; p.armor = t.armorDamage; p.shield = t.shieldDamage;
            p.filter = go.GetComponentInChildren<MeshFilter>();
            p.rend = go.GetComponentInChildren<MeshRenderer>();
            p.scale = go.transform.localScale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            int s = MagmaLobberLogic.PoolStage(age);
            if (s != stage && filter != null)
            {
                stage = s;
                VoxAsset asset = VoxelLibrary.Get(Stages[s]);
                if (asset != null) { filter.sharedMesh = asset.Mesh; rend.sharedMaterial = asset.Material; }
            }
            if (age < MagmaLobberLogic.PoolBurnUntil)
            {
                tick += Time.deltaTime;
                while (tick >= MagmaLobberLogic.PoolTick) { tick -= MagmaLobberLogic.PoolTick; Burn(); }
                return;
            }
            float k = 1f - (age - MagmaLobberLogic.PoolBurnUntil) / MagmaLobberLogic.PoolFade;
            if (k <= 0f) { Destroy(gameObject); return; }
            transform.localScale = new Vector3(scale.x * k, scale.y, scale.z * k);
        }

        private void Burn()
        {
            foreach (Enemy e in Combat.EnemiesInRadius(transform.position, radius * 0.9f + 0.25f))
            {
                // Direct damage (20% of the shot per second) plus the same amount as burn.
                e.TakeDamage(TowerType.FlameThrower, MagmaLobberLogic.PoolDamagePerTick(lobDamage), health, armor, shield, 0f, 0f, 0f, 0f, 0f, 0f);
                if (!Combat.IsAlive(e) || e.burnImmune) continue;
                int layer = e.shield > 0 ? shield : e.armor > 0 ? armor : health;
                e.burn += MagmaLobberLogic.PoolBurnPerTick(lobDamage, layer);
                e.BurnCheck();
            }
        }
    }

    [HarmonyPatch]
    internal static class FrostFlamePatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Dropper), nameof(Dropper.Update))]
        private static bool DropperUpdatePrefix(Dropper __instance)
        {
            if (!TreasureRunMode.Active) return true;
            IcicleCannon ice = __instance.GetComponent<IcicleCannon>();
            if (ice == null) return true;
            ice.Tick();
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(FlameThrower), nameof(FlameThrower.Update))]
        private static bool FlameThrowerUpdatePrefix(FlameThrower __instance)
        {
            if (!TreasureRunMode.Active) return true;
            MagmaLobber lobber = __instance.GetComponent<MagmaLobber>();
            if (lobber == null) return true;
            lobber.Tick();
            return false;
        }
    }
}
