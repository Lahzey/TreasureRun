using System;
using System.Collections.Generic;

namespace TreasureRun.Balance
{
    /// <summary>
    /// Turns a vanilla card's serialized fields into atoms (section 2: vanilla cards need no hand-written specs).
    /// The game side reads the fields of each UpgradeCard into a dictionary; this file only knows names and numbers.
    /// </summary>
    public static class VanillaCards
    {
        /// <summary>Vanilla TowerType order (Assembly-CSharp TowerType).</summary>
        private static readonly string[] TowerTypes =
        {
            "Crossbow", "Morter", "TeslaCoil", "Frost", "Obelisk", "FlameThrower", "PoisonSprayer", "Siphon", "Radar",
            "ParticleCannon", "DOT", "Global", "Shredder", "Encampment", "Lookout", "VampireLair", "Cannon", "Monument",
        };

        /// <summary>Tower kind of a vanilla TowerType index; null for Global, DOT and Siphon (all towers).</summary>
        public static TowerKind? Kind(int towerType)
        {
            if (towerType < 0 || towerType >= TowerTypes.Length) return null;
            return Enum.TryParse(TowerTypes[towerType], out TowerKind k) ? k : (TowerKind?)null;
        }

        private static double F(IDictionary<string, double> f, string key) => f.TryGetValue(key, out double v) ? v : 0;

        /// <summary>The spec of a vanilla card from its class name, title and serialized fields.</summary>
        public static Spec ToSpec(string cls, string title, IDictionary<string, double> f)
        {
            var s = new Spec();
            switch (cls)
            {
                case "TowerUpgradeCard": return TowerCard(title, f);
                case "DOTUpgradeCard":
                    for (int l = 0; l < 3; l++)
                    {
                        string n = l == 0 ? "bleed" : l == 1 ? "burn" : "poison";
                        if (F(f, n + "Amount") != 0) s.DotCap(l, F(f, n + "Amount"));
                        string cap = char.ToUpper(n[0]) + n.Substring(1);
                        if (F(f, "bonusDamageOn" + cap) != 0) s.DotLayerBonus(l, F(f, "bonusDamageOn" + cap));
                        if (F(f, n + "Pop") != 0) s.Pop(l, F(f, n + "Pop"));
                    }
                    if (F(f, "bonusDamageOnStun") != 0) s.BonusOnFrozen(F(f, "bonusDamageOnStun")).GivenFreeze().Situational(BoardVariant.Mixed, 0.2, "needs a freeze source");
                    if (F(f, "poisonSlowPercent") != 0) s.PoisonSlow(F(f, "poisonSlowPercent"));
                    if (F(f, "burnSpeedDamagePercentBonus") != 0) s.BurnSlowBonus(F(f, "burnSpeedDamagePercentBonus"));
                    if (F(f, "bleedingCritChance") != 0) s.BleedingCrit(F(f, "bleedingCritChance"));
                    if (IsDotOnly(f)) s.Situational(BoardVariant.DamageOverTime, 0.35, "works through damage over time");
                    return s;
                case "MonsterUpgradeCard":
                    if (F(f, "extraHealth") + F(f, "extraArmor") + F(f, "extraShield") != 0)
                        s.MonsterLayers(F(f, "extraHealth"), F(f, "extraArmor"), F(f, "extraShield"));
                    if (F(f, "fortificationBonus") != 0) s.Fortify(F(f, "fortificationBonus"));
                    if (F(f, "extraTowerDamage") != 0) s.CastleDamagePerLeak((int)F(f, "extraTowerDamage"));
                    if (F(f, "extraGoldDrop") + F(f, "goldDropPenalty") != 0) s.KillGold(F(f, "extraGoldDrop") + F(f, "goldDropPenalty"));
                    if (F(f, "manaDropOnDeath") != 0) s.ManaPerKill(F(f, "manaDropOnDeath"));
                    if (F(f, "speedBonus") != 0) s.MonsterSpeed(F(f, "speedBonus"));
                    if (F(f, "slowCapMod") != 0) s.SlowCap(F(f, "slowCapMod"));
                    if (F(f, "hasteCapMod") != 0) s.HasteCap(F(f, "hasteCapMod"));
                    return s;
                case "ManaGenUpgradeCard":
                    if (F(f, "manaPercentRegenIncrease") != 0) s.ManaRegen(F(f, "manaPercentRegenIncrease"));
                    if (F(f, "manaBankMaxManaIncrease") != 0) s.MaxManaPerBank(F(f, "manaBankMaxManaIncrease"));
                    return s.Situational(BoardVariant.Mana, 0.5, "only mana towers profit");
                case "MaxHealthUpgradeCard":
                    return s.MaxHealth(F(f, "healthIncrease")).HealNow(F(f, "healthIncrease"));
                case "UniversityUpgrade":
                    // +bonus% on each of a university's three rolls per wave; each success is +1 multiplier for every tower.
                    double perWave = 3 * F(f, "bonus") / 100;
                    return s.Custom(Category.Offense, $"+{F(f, "bonus")}% university research", c =>
                    {
                        double gained = perWave * c.Board.Universities * c.WavesHeld / 3; // a third of the rolls lands on each layer
                        c.Board.All.HealthMult += gained; c.Board.All.ArmorMult += gained; c.Board.All.ShieldMult += gained;
                    }).Requires("University");
                case "HauntedHouseUpgrade":
                    double rate = F(f, "rateIncrease");
                    return s.GoldPerWave(w => rate * 100 * 2, $"+{rate:0%} death tax (2 graves)").Requires("Haunted House");
                case "TreeUpgradeCard":
                    return s.CardsPerWave(0.03 * Math.Max(1, F(f, "treeCardChanceMultiplierInt"))).Because("tree card chance: estimated 3% per chop");
                case "TowerUnlockCard":
                    return Unlock(title);
                default:
                    return s.Unrated("unknown card class " + cls);
            }
        }

        private static bool IsDotOnly(IDictionary<string, double> f) =>
            F(f, "bonusDamageOnStun") == 0 && F(f, "poisonSlowPercent") == 0;

        private static Spec TowerCard(string title, IDictionary<string, double> f)
        {
            var s = new Spec();
            TowerKind? k = Kind((int)F(f, "towerType"));
            if (F(f, "range") != 0) s.Range(F(f, "range"), k);
            if (F(f, "heightRangeBonusMultiplier") != 0) s.HeightRange(F(f, "heightRangeBonusMultiplier"), k);
            if (F(f, "damage") != 0) s.BaseDamage(F(f, "damage"), k);
            if (F(f, "healthDmg") + F(f, "armorDmg") + F(f, "shieldDmg") != 0) s.LayerDamage(F(f, "healthDmg"), F(f, "armorDmg"), F(f, "shieldDmg"), k);
            if (F(f, "slowPercent") != 0) s.Slow(F(f, "slowPercent"), k);
            if (F(f, "bleedPercent") + F(f, "burnPercent") + F(f, "poisonPercent") != 0) s.Dot(F(f, "bleedPercent"), F(f, "burnPercent"), F(f, "poisonPercent"), k);
            if (F(f, "blast") != 0) s.Area(F(f, "blast"), k);
            if (F(f, "critChance") != 0) s.Crit(F(f, "critChance"), k);
            if (F(f, "critChanceLevelMultiplier") != 0) s.CritPerLevel(F(f, "critChanceLevelMultiplier"), k);
            if (F(f, "stunChance") != 0) s.Stun(F(f, "stunChance"), k);
            if (F(f, "manaConsumptionAddition") != 0) s.ManaRate(F(f, "manaConsumptionAddition"), k);
            if (F(f, "obeliskTimeOnTargetMultiplier") != 0)
                s.DamagePercent(0.4 * Math.Min(1, F(f, "obeliskTimeOnTargetMultiplier")), TowerKind.Obelisk)
                 .Because("Focus Fire: +1 damage per second on the same target, up to double (estimated +40%)");
            Special(s, k, 1, F(f, "special1"));
            Special(s, k, 2, F(f, "special2"));
            Special(s, k, 3, F(f, "special3"));
            if (k != null) s.Requires(k.Value);
            if (s.Atoms.Count == 0) s.Unrated("no effect fields");
            return s;
        }

        /// <summary>The tower-specific special fields (vanilla-towers.md).</summary>
        private static void Special(Spec s, TowerKind? k, int which, double n)
        {
            if (n == 0 || k == null) return;
            switch (k.Value)
            {
                case TowerKind.Cannon when which == 1: s.Targets(0.3 * n, k); break; // Ricochet: splits on the first hit
                case TowerKind.Cannon when which == 2: s.BaseDamage(3 * n, k); break; // Collateral: +n per monster pierced
                case TowerKind.Monument: s.BaseDamage(0.5 * n, k); break; // Slaughter / Dismantle / Desecrate on afflicted targets
                case TowerKind.VampireLair when which == 1:
                    s.Custom(Category.Survival, $"{0.06 * n:0%} heal chance per lair", c => c.Mods.HealPerWave += 0.06 * n * (c.Board.Group(TowerKind.VampireLair)?.Count ?? 0));
                    break;
                default: s.Because($"special{which} of {k} not modelled"); break;
            }
        }

        /// <summary>An unlock card: the board moves part of its tower gold to the new tower.</summary>
        public static Spec Unlock(string title)
        {
            string name = title.StartsWith("Tower - ") ? title.Substring(8) : title.StartsWith("Building - ") ? title.Substring(11) : title;
            if (!TowerData.TryParse(name, out TowerKind kind))
                return new Spec().Estimated(3, "building unlock: value depends on map objects").Requires("map object");
            // Without the card the board can't have this tower: its gold went to the other towers.
            return new Spec()
                .Offered(Math.Max(5, ReferenceBoards.FirstWave(kind)))
                .Given("without the " + name, b => Valuation.RemoveTower(b, kind))
                .Custom(Category.Offense, "unlocks " + name, c =>
                {
                    // an option: the player only builds it where it beats what the gold would buy otherwise
                    Board with = c.Board.Clone();
                    Valuation.EnsureTower(with, kind);
                    if (Evaluator.Evaluate(with).Combined > Evaluator.Evaluate(c.Board).Combined)
                    {
                        c.Board.Groups = with.Groups;
                        c.Board.Mods.ManaPerSecond = with.Mods.ManaPerSecond;
                        c.Board.Siphons = with.Siphons;
                    }
                })
                .Because("an option: a board without the tower against one that moves 12% of its tower gold to it, if that is better");
        }
    }
}
