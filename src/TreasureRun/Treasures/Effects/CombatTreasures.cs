using System.Collections.Generic;
using TreasureRun.Core;
using UnityEngine;
using static TreasureRun.Treasures.Effects.Kit;

namespace TreasureRun.Treasures.Effects
{
    /// <summary>Damage modifiers, on-hit procs, status synergies and on-death chain reactions (M4).</summary>
    internal static class CombatTreasures
    {
        public static void Register()
        {
            // ----- Common -----
            Add("first_strike", "First Strike", Rarity.Common,
                "The first hit against each enemy deals double damage.", tags: new[] { "damage" });
            Add("cryo_conductor", "Cryo Conductor", Rarity.Common,
                "Slowed enemies take +1 damage from direct hits for every 15% slow on them.", tags: new[] { "slow", "damage" });
            Add("permafrost", "Permafrost", Rarity.Common,
                "Freezes last 50% longer and no longer get shorter when an enemy is frozen repeatedly.", tags: new[] { "freeze" });

            // ----- Uncommon -----
            Add("bloodthirst", "Bloodthirst", Rarity.Uncommon,
                "Towers gain +1% crit chance for every enemy killed this wave (max +30%).", tags: new[] { "crit" });
            Add("hemorrhage", "Hemorrhage", Rarity.Uncommon,
                "Critical hits apply bleed equal to 50% of the damage they dealt.", tags: new[] { "crit", "bleed" });
            Add("witchs_brew", "Witch's Brew", Rarity.Uncommon,
                "Direct hits on poisoned enemies also apply burn equal to 25% of the damage.", tags: new[] { "poison", "burn" });

            // ----- Rare -----
            Add("executioner", "Executioner", Rarity.Rare,
                "Enemies below 20% health take double damage from everything.", tags: new[] { "damage" });
            Add("shatter", "Shatter", Rarity.Rare,
                "Frozen enemies take triple damage from direct hits.", tags: new[] { "freeze", "damage" });
            Add("static_arc", "Static Arc", Rarity.Rare,
                "Direct hits have a 12% chance to arc to 2 nearby enemies, repeating the hit on them.", tags: new[] { "proc", "chain" });
            Add("overkill", "Overkill", Rarity.Rare,
                "Excess damage from a killing blow hits the nearest enemy within 3 range.", tags: new[] { "on-death", "chain" });
            Add("frost_nova", "Frost Nova", Rarity.Rare,
                "Freezing an enemy slows all enemies within 2 range by 25%.", tags: new[] { "freeze", "slow" });
            Add("arcane_overflow", "Arcane Overflow", Rarity.Rare,
                "While your mana is above 90%, mana-using towers deal 50% more damage.", tags: new[] { "mana", "damage" });

            // ----- Epic -----
            Add("volatile_corpses", "Volatile Corpses", Rarity.Epic,
                "Enemies explode on death, dealing 10% of their maximum health to enemies within 1.5 range.", tags: new[] { "on-death", "chain" });
            Add("contagion", "Contagion", Rarity.Epic,
                "When an enemy dies, its bleed, burn and poison spread to up to 2 nearby enemies.", tags: new[] { "on-death", "bleed", "burn", "poison" });
            Add("hex_of_frailty", "Hex of Frailty", Rarity.Epic,
                "Your first hit on an enemy hexes it. Hexed enemies take +1 to every damage multiplier.", tags: new[] { "damage" });
            Add("triple_threat", "Triple Threat", Rarity.Epic,
                "Enemies suffering from bleed, burn and poison at the same time take double damage.", tags: new[] { "bleed", "burn", "poison" });
            Add("glass_cannon", "Glass Cannon", Rarity.Epic,
                "All towers deal 50% more damage. Your castle's maximum health is halved.",
                (run, restoring) =>
                {
                    if (!restoring) Cards.HealthCost.Pay(GlassCannonCost());
                },
                new[] { "damage", "risk" });
            TreasureRegistry.Get("glass_cannon").MaxHealthCost = GlassCannonCost;

            // ----- Legendary -----
            Add("domino_effect", "Domino Effect", Rarity.Legendary,
                "On-death effects deal 50% more damage, and kills caused by them trigger on-death effects again.",
                tags: new[] { "on-death", "chain" },
                available: _ => Has("volatile_corpses") || Has("overkill") || Has("contagion"));
            Add("master_of_elements", "Master of Elements", Rarity.Legendary,
                "Every direct hit also applies 10% bleed, burn and poison.", tags: new[] { "bleed", "burn", "poison" });
        }

        /// <summary>Half the current max health (displayed points).</summary>
        private static int GlassCannonCost() => GameManager.instance != null ? GameManager.instance.maxHealth / 20 : 0;

        private static readonly HashSet<Enemy> struck = new HashSet<Enemy>();
        private static readonly HashSet<Enemy> hexed = new HashSet<Enemy>();
        private static int deathEffectDepth;

        public static void Hook()
        {
            TreasureRunMode.RunStarted += _ => { struck.Clear(); hexed.Clear(); deathEffectDepth = 0; };
            GameEvents.Resubscribe += () => GameEvents.WaveStarted += _ => { struck.Clear(); hexed.Clear(); };

            Combat.HitModifiers.Add(ModifyHit);
            Combat.EnemyDamaged += OnDamaged;
            Combat.EnemyKilled += OnKilled;
            Combat.EnemyFrozen += OnFrozen;

            StatLayer.Register("glass_cannon", (t, b) => { if (Has("glass_cannon")) b.Multiply(TowerStat.Damage, 1.5f); });
        }

        private static void ModifyHit(HitContext h)
        {
            if (!TreasureRunMode.Active) return;
            Enemy e = h.Enemy;

            if (h.IsDirect && !h.IsNested)
            {
                if (Has("first_strike") && struck.Add(e)) h.MultiplyDamage(2f);

                if (Has("hex_of_frailty"))
                {
                    if (hexed.Contains(e)) { h.HealthMult++; h.ArmorMult++; h.ShieldMult++; }
                    else hexed.Add(e);
                }

                if (Has("cryo_conductor")) h.BaseDamage += Mathf.FloorToInt(e.currentSlowPercentage / 0.15f);
                if (Has("shatter") && e.freezeTime > 0f) h.MultiplyDamage(3f);
                if (Has("bloodthirst")) h.Crit += Mathf.Min(0.3f, 0.01f * Combat.KillsThisWave);
                if (Has("witchs_brew") && e.poisoned) h.Burn += 0.25f;
                if (Has("master_of_elements")) { h.Bleed += 0.1f; h.Burn += 0.1f; h.Poison += 0.1f; }

                if (Has("arcane_overflow") && UsesMana(h.Source) && ResourceManager.instance != null
                    && ResourceManager.instance.mana >= 0.9f * ResourceManager.instance.maxMana)
                    h.MultiplyDamage(1.5f);
            }

            if (Has("executioner") && e.CurrentHealth() < 0.2f) h.MultiplyDamage(2f);
            if (Has("triple_threat") && e.bleeding && e.burning && e.poisoned) h.MultiplyDamage(2f);
        }

        private static void OnDamaged(HitResult r)
        {
            if (!TreasureRunMode.Active) return;
            Enemy e = r.Enemy;

            if (r.WasCrit && !r.Killed && Has("hemorrhage") && !e.bleedImmune && r.DamageDealt > 0)
                e.bleed += r.DamageDealt / 2;

            if (r.CanProc && Has("static_arc") && Luck.Check(LuckCategory.Proc, 0.12f))
            {
                Vector3 from = e.transform.position;
                foreach (Enemy target in Combat.NearestEnemies(from, 2.5f, 2, e))
                {
                    Zap(from, target.transform.position, new Color(0.6f, 0.8f, 1f));
                    Combat.SecondaryHit(target, r.Source, r.BaseDamage, r.HealthMult, r.ArmorMult, r.ShieldMult);
                }
            }
        }

        private static void OnKilled(KillInfo k)
        {
            if (!TreasureRunMode.Active) return;
            bool domino = Has("domino_effect");
            if (deathEffectDepth > 0 && !domino) return; // without Domino, on-death kills don't chain
            float mult = domino ? 1.5f : 1f;

            deathEffectDepth++;
            try
            {
                if (Has("overkill") && k.Overkill > 0)
                {
                    List<Enemy> near = Combat.NearestEnemies(k.Position, 3f, 1, k.Enemy);
                    if (near.Count > 0)
                    {
                        Enemy target = near[0];
                        Zap(k.Position, target.transform.position, new Color(1f, 0.4f, 0.3f));
                        Combat.TrueDamage(target, Mathf.RoundToInt(k.Overkill * mult));
                    }
                }

                if (Has("contagion")) SpreadAilments(k.Enemy, mult);

                if (Has("volatile_corpses"))
                {
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(k.MaxHp * 0.1f * mult));
                    Boom(k.Position, 0.6f);
                    foreach (Enemy target in Combat.EnemiesInRadius(k.Position, 1.5f, k.Enemy))
                        Combat.TrueDamage(target, dmg);
                }
            }
            finally { deathEffectDepth--; }
        }

        private static void SpreadAilments(Enemy source, float mult)
        {
            int bleed = Mathf.RoundToInt(source.bleed * mult), burn = Mathf.RoundToInt(source.burn * mult), poison = Mathf.RoundToInt(source.poison * mult);
            if (bleed + burn + poison <= 0) return;
            List<Enemy> targets = Combat.NearestEnemies(source.transform.position, 2.5f, 2, source);
            if (targets.Count == 0) return;
            foreach (Enemy t in targets)
            {
                if (!t.bleedImmune) t.bleed += bleed / targets.Count;
                if (!t.burnImmune) t.burn += burn / targets.Count;
                if (!t.poisonImmune) t.poison += poison / targets.Count;
                Zap(source.transform.position, t.transform.position, new Color(0.5f, 0.9f, 0.3f));
            }
        }

        private static void OnFrozen(Enemy e)
        {
            if (!TreasureRunMode.Active) return;
            if (Has("permafrost"))
            {
                e.freezeTime *= 1.5f;
                e.maxFreeze /= 0.8f; // undo vanilla's diminishing returns
            }
            if (Has("frost_nova"))
                foreach (Enemy t in Combat.EnemiesInRadius(e.transform.position, 2f, e))
                    t.AddSlow(0.25f);
        }
    }
}
