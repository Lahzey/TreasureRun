using System.Collections.Generic;
using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;
using static TreasureRun.Treasures.Effects.Kit;

namespace TreasureRun.Treasures.Effects
{
    /// <summary>Build philosophy, tower-specific and castle treasures (M4).</summary>
    internal static class BuildTreasures
    {
        private const string BulwarkLeftKey = "bulwark.left";

        public static void Register()
        {
            // ----- Uncommon -----
            Add("prodigy", "Prodigy", Rarity.Uncommon,
                "Newly built towers start at level 3.", tags: new[] { "levels" });
            Add("high_ground", "High Ground", Rarity.Uncommon,
                "Elevation bonuses to damage and range are doubled.", tags: new[] { "placement" });
            Add("hoarder", "Hoarder", Rarity.Uncommon,
                "Towers deal 4% more damage for every treasure you own.", tags: new[] { "damage", "treasures" });
            Add("seeker_rounds", "Seeker Rounds", Rarity.Uncommon,
                "Ballista bolts and biplane bullets home in on their target.", tags: new[] { "projectile", "ballista" });
            Add("moat", "Moat", Rarity.Uncommon,
                "Enemies within 6 range of your castle are always slowed by at least 20%.", tags: new[] { "castle", "slow" });
            Add("bulwark", "Bulwark", Rarity.Uncommon,
                "Each wave, your castle ignores the first 3 damage it would take.", tags: new[] { "castle" });

            // ----- Rare -----
            Add("veteran_training", "Veteran Training", Rarity.Rare,
                "Whenever a tower gains a level, it has a 30% chance to gain another one.", tags: new[] { "levels", "proc" });
            Add("lightning_rod", "Lightning Rod", Rarity.Rare,
                "Towers within 2.5 range of a Tesla Coil attack 25% faster.", tags: new[] { "tesla", "placement" },
                available: Requires(TowerType.TeslaCoil));
            Add("supercapacitor", "Supercapacitor", Rarity.Rare,
                "Tesla Coils attack 40% slower but deal triple damage.", tags: new[] { "tesla" },
                available: Requires(TowerType.TeslaCoil));
            Add("bigger_bombs", "Bigger Bombs", Rarity.Rare,
                "Mortars gain +50% blast radius and deal 25% more damage.", tags: new[] { "mortar" },
                available: Requires(TowerType.Morter));
            Add("ricochet_bolts", "Ricochet Bolts", Rarity.Rare,
                "Ballista hits bounce to the nearest other enemy within 3 range for 75% damage.", tags: new[] { "projectile", "ballista", "chain" });

            // ----- Epic -----
            Add("fragmentation", "Fragmentation", Rarity.Epic,
                "Mortar shells burst into 3 extra blasts around the impact, each dealing half damage.", tags: new[] { "mortar", "projectile" },
                available: Requires(TowerType.Morter));
            Add("wingman", "Wingman", Rarity.Epic,
                "Each Radar tower launches a second biplane.", tags: new[] { "radar", "plane" },
                available: Requires(TowerType.Radar));
            Add("retribution", "Retribution", Rarity.Epic,
                "Whenever your castle takes damage, all enemies within 5 range of it take 15% of their maximum health.",
                tags: new[] { "castle" });

            // ----- Legendary: one big army or one big tower (mutually exclusive) -----
            Add("monolith", "Monolith", Rarity.Legendary,
                "Towers deal 25% more damage and attack 5% faster for every tower fewer than 10 you own.",
                tags: new[] { "few-towers" }, exclusionGroup: "army-size");
            Add("legion", "Legion", Rarity.Legendary,
                "Additional towers of a type no longer cost more. Towers attack 3% faster for every other tower within 3 range, but deal 15% less damage.",
                (run, restoring) => { if (!restoring) RemovePriceIncreases(); },
                new[] { "many-towers" }, exclusionGroup: "army-size");
        }

        public static void Hook()
        {
            StatLayer.Register("build_treasures", ApplyStats);
            TreasureRunMode.RunStarted += _ => ResetWingmen();

            GameEvents.Resubscribe += () =>
            {
                GameEvents.TowerBuilt += OnTowerBuilt;
                GameEvents.TowerLeveled += OnTowerLeveled;
                GameEvents.CastleDamaged += OnCastleDamaged;
                GameEvents.WaveStarted += _ => TreasureRunMode.Run?.SetCounter(BulwarkLeftKey, 3 * Count("bulwark"));
            };

            Combat.EnemyDamaged += r =>
            {
                if (!TreasureRunMode.Active || r.Source != TowerType.Crossbow || !r.CanProc || !Has("ricochet_bolts")) return;
                List<Enemy> near = Combat.NearestEnemies(r.Enemy.transform.position, 3f, 1, r.Enemy);
                if (near.Count == 0) return;
                Zap(r.Enemy.transform.position, near[0].transform.position, new Color(0.9f, 0.85f, 0.6f), 0.1f);
                Combat.SecondaryHit(near[0], r.Source, Mathf.RoundToInt(r.BaseDamage * 0.75f), r.HealthMult, r.ArmorMult, r.ShieldMult);
            };
        }

        private static void ApplyStats(Tower t, TowerStatBuilder b)
        {
            if (!TreasureRunMode.Active) return;

            int height = Castles.CrowsNestBehaviour.EffectiveHeightBonus(t);
            if (Has("high_ground") && height > 0)
            {
                b.Flat(TowerStat.Damage, height);
                b.Flat(TowerStat.Range, height / 2f);
            }

            if (Has("hoarder")) b.Percent(TowerStat.Damage, 0.04f * TreasureInventory.TotalOwned);

            if (Has("monolith"))
            {
                int missing = Mathf.Max(0, 10 - GameEvents.TowerCount);
                b.Percent(TowerStat.Damage, 0.25f * missing);
                b.Percent(TowerStat.AttackSpeed, 0.05f * missing);
            }

            if (Has("legion"))
            {
                b.Multiply(TowerStat.Damage, 0.85f);
                int neighbours = 0;
                foreach (Tower other in GameEvents.Towers)
                    if (other != null && other != t && (other.transform.position - t.transform.position).sqrMagnitude <= 9f) neighbours++;
                b.Percent(TowerStat.AttackSpeed, 0.03f * neighbours);
            }

            if (Has("lightning_rod") && t.towerType != TowerType.TeslaCoil)
            {
                foreach (Tower other in GameEvents.Towers)
                    if (other != null && other.towerType == TowerType.TeslaCoil
                        && (other.transform.position - t.transform.position).sqrMagnitude <= 6.25f)
                    {
                        b.Percent(TowerStat.AttackSpeed, 0.25f);
                        break;
                    }
            }

            if (Has("supercapacitor") && t.towerType == TowerType.TeslaCoil)
            {
                b.Multiply(TowerStat.Damage, 3f);
                b.Multiply(TowerStat.AttackSpeed, 0.6f);
            }

            if (Has("bigger_bombs") && t.towerType == TowerType.Morter)
            {
                b.Percent(TowerStat.BlastRadius, 0.5f);
                b.Percent(TowerStat.Damage, 0.25f);
            }
        }

        private static void OnTowerBuilt(Tower t)
        {
            if (!Has("prodigy") || t == null) return;
            for (int i = 0; i < 2; i++) GrantLevel(t, i);
            t.SetStats();
        }

        private static void OnTowerLeveled(Tower t)
        {
            if (!Has("veteran_training") || t == null) return;
            if (!Luck.Check(LuckCategory.Proc, 0.3f)) return;
            GrantLevel(t, t.level);
            t.SetStats();
            t.LevelUpText(); // may chain (GameEventPatches limits the depth)
        }

        /// <summary>Same bookkeeping as a vanilla level-up; the damage type rotates health → armor → shield.</summary>
        private static void GrantLevel(Tower t, int rotation)
        {
            t.level++;
            t.damageUpgrade++;
            switch (rotation % 3)
            {
                case 0: t.healthDamageUpgrade++; break;
                case 1: t.armorDamageUpgrade++; break;
                default: t.shieldDamageUpgrade++; break;
            }
        }

        private static void OnCastleDamaged(int damage)
        {
            if (!Has("retribution")) return;
            Vector3 castle = GameHelpers.CastlePosition;
            Boom(castle + Vector3.up, 1.5f);
            foreach (Enemy e in Combat.EnemiesInRadius(castle, 5f))
                Combat.TrueDamage(e, Mathf.Max(1, Mathf.RoundToInt(Combat.MaxHp(e) * 0.15f)));
        }

        private static void RemovePriceIncreases()
        {
            foreach (TowerFlyweight fw in Object.FindObjectsOfType<TowerFlyweight>())
            {
                fw.priceIncrease = 0;
                fw.currentPrice = fw.basePrice;
                fw.sellPrice = fw.basePrice;
                if (fw.myBuildButton != null) fw.myBuildButton.UpdatePriceText();
            }
        }

        /// <summary>Bulwark: called from the GameManager.TakeDamage prefix; returns the damage that gets through.</summary>
        internal static int AbsorbCastleDamage(int damage)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null || damage <= 0) return damage;
            int left = run.GetCounter(BulwarkLeftKey);
            if (left <= 0) return damage;
            int absorbed = Mathf.Min(left, damage);
            run.SetCounter(BulwarkLeftKey, left - absorbed);
            GameHelpers.FloatingText(CastleTop, $"BLOCKED {absorbed}", "Grey");
            return damage - absorbed;
        }

        // ----- Moat (ticked by TreasureTicker) -----
        internal static void TickMoat()
        {
            if (!Has("moat") || SpawnManager.instance == null) return;
            Vector3 castle = GameHelpers.CastlePosition;
            foreach (Enemy e in SpawnManager.instance.currentEnemies)
            {
                if (e == null || e.slowImmune) continue;
                Vector3 d = e.transform.position - castle;
                d.y = 0f;
                if (d.sqrMagnitude <= 36f && e.currentSlowPercentage < 0.2f) e.currentSlowPercentage = 0.2f;
            }
        }

        // ----- Wingman: second biplane per Radar tower -----
        private static readonly Dictionary<RadarTower, BiPlane> wingmen = new Dictionary<RadarTower, BiPlane>();

        internal static void SyncWingman(RadarTower radar)
        {
            if (!Has("wingman") || radar.myPlane == null) return;
            if (!wingmen.TryGetValue(radar, out BiPlane wing) || wing == null)
            {
                wing = Object.Instantiate(radar.biPlanePrefab, radar.transform.position + new Vector3(50f, 5f, 0f), Quaternion.identity).GetComponent<BiPlane>();
                wingmen[radar] = wing;
            }
            BiPlane lead = radar.myPlane;
            wing.damage = lead.damage; wing.healthDamage = lead.healthDamage; wing.armorDamage = lead.armorDamage; wing.shieldDamage = lead.shieldDamage;
            wing.rps = lead.rps; wing.slowPercent = lead.slowPercent; wing.bleedPercent = lead.bleedPercent;
            wing.burnPercent = lead.burnPercent; wing.poisonPercent = lead.poisonPercent;
            wing.critChance = lead.critChance; wing.stunChance = lead.stunChance;
        }

        internal static void WingmanTarget(RadarTower radar)
        {
            if (wingmen.TryGetValue(radar, out BiPlane wing) && wing != null && wing.target == null && radar.currentTarget != null)
                wing.target = radar.currentTarget;
        }

        internal static void RemoveWingman(RadarTower radar)
        {
            if (wingmen.TryGetValue(radar, out BiPlane wing))
            {
                if (wing != null) Object.Destroy(wing.gameObject);
                wingmen.Remove(radar);
            }
        }

        internal static void ResetWingmen() => wingmen.Clear();

        /// <summary>The Radar tower whose wingman this plane is (null if it is no wingman).</summary>
        internal static RadarTower WingmanOwner(BiPlane plane)
        {
            foreach (var kv in wingmen)
                if (kv.Value == plane) return kv.Key;
            return null;
        }

        /// <summary>The wingman of a Radar tower (null if none).</summary>
        internal static BiPlane WingmanOf(RadarTower radar) => wingmen.TryGetValue(radar, out BiPlane wing) ? wing : null;

        // ----- Fragmentation -----
        internal static void Fragment(MorterShell shell)
        {
            if (!Has("fragmentation")) return;
            Vector3 center = shell.transform.position;
            float radius = Mathf.Max(0.5f, shell.blastRadius);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f + Random.Range(-20f, 20f);
                Vector3 pos = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                Boom(pos, 0.5f);
                foreach (Collider c in Physics.OverlapSphere(pos, radius * 0.6f, shell.layersAffectedByBlast, QueryTriggerInteraction.Collide))
                {
                    Enemy e = c.GetComponent<Enemy>();
                    if (e != null)
                        Combat.SecondaryHit(e, shell.shotBy, shell.damage / 2, shell.healthDamage, shell.armorDamage, shell.shieldDamage,
                                            shell.slowPercent, shell.bleedPercent, shell.burnPercent, shell.poisonPercent);
                }
            }
        }
    }

    /// <summary>Game hooks for build/tower/castle treasures.</summary>
    [HarmonyPatch]
    internal static class BuildTreasurePatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(GameManager), nameof(GameManager.TakeDamage)), HarmonyPriority(Priority.First)]
        private static void CastleTakeDamagePrefix(ref int damage)
        {
            if (TreasureRunMode.Active && TreasureInventory.Owns("bulwark")) damage = BuildTreasures.AbsorbCastleDamage(damage);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(RadarTower), nameof(RadarTower.SetStats))]
        private static void RadarSetStatsPostfix(RadarTower __instance)
        {
            if (TreasureRunMode.Active) BuildTreasures.SyncWingman(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(RadarTower), nameof(RadarTower.Update))]
        private static void RadarUpdatePostfix(RadarTower __instance)
        {
            if (TreasureRunMode.Active) BuildTreasures.WingmanTarget(__instance);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(RadarTower), nameof(RadarTower.Demolish))]
        private static void RadarDemolishPrefix(RadarTower __instance) => BuildTreasures.RemoveWingman(__instance);

        [HarmonyPrefix, HarmonyPatch(typeof(MorterShell), nameof(MorterShell.OnHit))]
        private static void MorterShellOnHitPrefix(MorterShell __instance)
        {
            if (TreasureRunMode.Active) BuildTreasures.Fragment(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Projectile), nameof(Projectile.SetStats))]
        private static void ProjectileSetStatsPostfix(Projectile __instance, TowerType whoShotMe)
        {
            if (!TreasureRunMode.Active || !TreasureInventory.Owns("seeker_rounds")) return;
            if (__instance.GetType() == typeof(Projectile) && (whoShotMe == TowerType.Crossbow || whoShotMe == TowerType.Radar))
                __instance.homing = true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(TreeUI), nameof(TreeUI.Chop))]
        private static void ChopPostfix(TreeUI __instance)
        {
            if (TreasureRunMode.Active) EconomyTreasures.OnTreeChopped(__instance);
        }
    }
}
