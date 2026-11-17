using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase1
{
    /// <summary>
    /// Repeater (Ballista, Common): every shot becomes a 3-bolt burst on one target, with the reload stretched so the
    /// DPS is 1.5× (<see cref="RepeaterLogic"/>). Bolts whose target died fly on to its last position as harmless duds.
    /// </summary>
    internal sealed class Repeater : AscendedTower
    {
        public const string Id = "repeater";

        /// <summary>Set while a burst bolt goes through the vanilla Fire (the Projectile.SetStats postfix tags it).</summary>
        internal static Repeater Firing;
        internal static int FiringIndex;
        internal static Enemy FiringTarget;
        internal static Vector3 FiringLast;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Repeater", Rarity = Rarity.Common, Kind = AscensionKind.Upgrade,
                Target = AscensionTarget.Ballista, Component = typeof(Repeater), CardModel = "ascensions/ballista/repeater_turret",
                Description = $"Fires {RepeaterLogic.Burst}-bolt bursts at one target: {RepeaterLogic.DpsFactor - 1f:P0} more damage per second. " +
                              "Bolts still in the air when their target dies are lost.",
            });
            StatLayer.Register("Repeater", (tower, stats) =>
            {
                if (tower.GetComponent<Repeater>() != null) stats.Multiply(TowerStat.AttackSpeed, RepeaterLogic.AttackSpeedMultiplier);
            });
        }

        protected override void OnAscend() => SwapVanillaMesh("TurretArt", "ascensions/ballista/repeater_turret");

        internal void StartBurst()
        {
            if (Tower.currentTarget != null) StartCoroutine(Burst(Tower.currentTarget.GetComponent<Enemy>()));
        }

        private IEnumerator Burst(Enemy target)
        {
            Vector3 last = target != null ? target.transform.position : transform.position + transform.forward;
            for (int i = 0; i < RepeaterLogic.Burst; i++)
            {
                if (i > 0) yield return new WaitForSeconds(RepeaterLogic.BoltSpacing);
                bool alive = Combat.IsAlive(target);
                if (alive) last = target.transform.position;
                FireBolt(i, alive ? target : null, last);
            }
        }

        private void FireBolt(int index, Enemy target, Vector3 last)
        {
            GameObject saved = Tower.currentTarget;
            Tower.currentTarget = target != null ? target.gameObject : null;
            if (Tower.turret != null)
            {
                Vector3 d = Phase1Kit.Flat(last - Tower.turret.transform.position);
                if (d.sqrMagnitude > 0.001f) Tower.turret.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
            }
            Firing = this; FiringIndex = index; FiringTarget = target; FiringLast = last;
            try { Tower.Fire(); }
            finally
            {
                Firing = null;
                Tower.currentTarget = saved;
            }
        }
    }

    /// <summary>A Repeater bolt: homes like vanilla while its target lives, otherwise turns into a dud (no hits).</summary>
    internal sealed class BurstBolt : MonoBehaviour
    {
        private Projectile projectile;
        private Enemy target;
        private Vector3 last;
        private bool dud;

        public bool IsDud => dud;

        public static void Attach(Projectile p, Enemy target, Vector3 last, int index)
        {
            var b = p.gameObject.AddComponent<BurstBolt>();
            b.projectile = p;
            b.target = target;
            b.last = last;
            p.launchSound = Sound.None; // replaced by a rising pitch per bolt of the burst
            SoundLibrary.PlayVanilla(Sound.Ballista, p.transform.position, 1f + 0.07f * index, 0.8f);
            if (!Combat.IsAlive(target)) b.MakeDud();
        }

        private void FixedUpdate()
        {
            if (!dud)
            {
                if (Combat.IsAlive(target)) { last = target.transform.position; return; }
                MakeDud();
            }
            Vector3 aim = last + Vector3.up * 0.25f - transform.position;
            if (aim.sqrMagnitude < 0.09f) { Finish(); return; }
            transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
        }

        private void MakeDud()
        {
            dud = true;
            projectile.layermask = 0;
            projectile.homing = false;
        }

        private void Finish()
        {
            if (projectile.detachOnDestruction != null) projectile.detachOnDestruction.transform.parent = null;
            if (projectile.extraFX != null) projectile.extraFX.OnDetach();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Springald (Ballista, Rare): heavy bolts that pierce up to 5 monsters, losing 20% of the first hit's damage per
    /// further hit (<see cref="SpringaldLogic"/>). They home in until the first hit, then fly straight on.
    /// </summary>
    internal sealed class Springald : AscendedTower
    {
        public const string Id = "springald";

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Springald", Rarity = Rarity.Rare, Kind = AscensionKind.Replace,
                Target = AscensionTarget.Ballista, Component = typeof(Springald), CardModel = "ascensions/ballista/springald_turret",
                Description = $"Fires heavy bolts that pierce up to {SpringaldLogic.MaxHits} monsters: full damage to the first, " +
                              $"{SpringaldLogic.FalloffPerHit:P0} less of it to each one after.",
            });
        }

        protected override void OnAscend() => SwapVanillaMesh("TurretArt", "ascensions/ballista/springald_turret");

        internal void Fire()
        {
            Enemy target = Tower.currentTarget != null ? Tower.currentTarget.GetComponent<Enemy>() : null;
            if (target == null) return;
            Vector3 from = Tower.muzzle != null ? Tower.muzzle.position : transform.position + Vector3.up;
            PiercingBolt.Launch(Tower, from, target);
            SoundLibrary.Play("ascensions/springald/springald_launch", from, 0.9f);
        }
    }

    /// <summary>The Springald's bolt: sphere-cast each physics step, each monster hit once, damage falling per hit.</summary>
    internal sealed class PiercingBolt : MonoBehaviour
    {
        private const float Radius = 0.2f, MaxLifetime = 6f;
        private Enemy target;
        private readonly HashSet<Enemy> struck = new HashSet<Enemy>();
        private int hits, baseDamage, healthDamage, armorDamage, shieldDamage;
        private float slow, bleed, burn, poison, crit, stun, speed, maxDistance, travelled, life;
        private LayerMask mask;
        private bool straight;

        public int Hits => hits;

        public static PiercingBolt Launch(Tower t, Vector3 from, Enemy target)
        {
            GameObject go = Phase1Kit.Model("ascensions/ballista/springald_bolt", 0.3f);
            go.transform.position = from;
            Vector3 aim = target.transform.position + Vector3.up * 0.25f - from;
            go.transform.rotation = Quaternion.LookRotation(aim.sqrMagnitude > 0.001f ? aim : t.transform.forward, Vector3.up);
            var b = go.AddComponent<PiercingBolt>();
            b.target = target;
            b.baseDamage = t.damage; b.healthDamage = t.healthDamage; b.armorDamage = t.armorDamage; b.shieldDamage = t.shieldDamage;
            b.slow = t.slowPercent; b.bleed = t.bleedPercent; b.burn = t.burnPercent; b.poison = t.poisonPercent;
            b.crit = t.critChance; b.stun = t.stunChance;
            b.speed = Mathf.Max(8f, t.projectileSpeed);
            b.mask = t.enemyLayerMask;
            b.maxDistance = aim.magnitude + 2.5f * t.range;
            return b;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            life += dt;
            if (!straight && Combat.IsAlive(target))
            {
                Vector3 aim = target.transform.position + Vector3.up * 0.25f - transform.position;
                if (aim.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
            }
            float step = speed * dt;
            RaycastHit[] cast = Physics.SphereCastAll(transform.position, Radius, transform.forward, step + 0.1f, mask, QueryTriggerInteraction.Collide);
            foreach (RaycastHit h in cast.OrderBy(h => h.distance))
            {
                Enemy e = h.collider.GetComponentInParent<Enemy>();
                if (e == null || struck.Contains(e) || !Combat.IsAlive(e)) continue;
                int dmg = SpringaldLogic.DamageForHit(baseDamage, hits);
                if (dmg <= 0) break;
                struck.Add(e);
                e.TakeDamage(TowerType.Crossbow, dmg, healthDamage, armorDamage, shieldDamage, slow, bleed, burn, poison, crit, stun);
                SoundLibrary.PlayVanilla(Sound.BallistaHit, transform.position, 1f - 0.08f * hits, 0.8f);
                hits++;
                straight = true;
                if (SpringaldLogic.Spent(hits)) { Destroy(gameObject); return; }
            }
            transform.position += transform.forward * step;
            travelled += step;
            if (travelled > maxDistance || life > MaxLifetime || transform.position.y < -1f) Destroy(gameObject);
        }
    }

    [HarmonyPatch]
    internal static class BallistaAscensionPatches
    {
        /// <summary>Repeater and Springald replace the Ballista's vanilla Fire (Ballistas use the base Tower.Fire).</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Tower), nameof(Tower.Fire))]
        private static bool TowerFirePrefix(Tower __instance)
        {
            if (Repeater.Firing != null || !TreasureRunMode.Active) return true;
            Repeater r = __instance.GetComponent<Repeater>();
            if (r != null) { r.StartBurst(); return false; }
            Springald s = __instance.GetComponent<Springald>();
            if (s != null) { s.Fire(); return false; }
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Projectile), nameof(Projectile.SetStats))]
        private static void ProjectileSetStatsPostfix(Projectile __instance)
        {
            if (Repeater.Firing != null) BurstBolt.Attach(__instance, Repeater.FiringTarget, Repeater.FiringLast, Repeater.FiringIndex);
        }
    }
}
