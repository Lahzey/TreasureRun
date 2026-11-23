using System;
using System.Collections.Generic;

namespace TreasureRun.Balance
{
    /// <summary>
    /// Balance specs of the mod's treasures and custom cards, by id (docs/balance-framework.md section 7). Mostly one
    /// line each. Proxies (an ascension expressed as "about x times its tower") and hand estimates say so in their note;
    /// both are marked for the M8 simulation. Every offerable item needs an entry or an explicit Unrated (checked by
    /// the scenario <c>balance</c> and the offline coverage test).
    /// </summary>
    public static class ContentSpecs
    {
        private static Spec S => new Spec();
        private const TowerKind Ballista = TowerKind.Crossbow, Mortar = TowerKind.Morter, Tesla = TowerKind.TeslaCoil,
            Frost = TowerKind.Frost, Flame = TowerKind.FlameThrower, Poison = TowerKind.PoisonSprayer, Shredder = TowerKind.Shredder,
            Encampment = TowerKind.Encampment, Lookout = TowerKind.Lookout, Vampire = TowerKind.VampireLair, Cannon = TowerKind.Cannon,
            Monument = TowerKind.Monument, Obelisk = TowerKind.Obelisk, Radar = TowerKind.Radar, Particle = TowerKind.ParticleCannon;

        private static readonly TowerKind[] ManaTowers = { Tesla, Frost, Flame, Poison, Vampire, Monument, Obelisk, Particle };

        /// <summary>Treasures by id.</summary>
        public static readonly Dictionary<string, Func<Spec>> Treasures = new Dictionary<string, Func<Spec>>
        {
            // ----- Common -----
            ["cryo_conductor"] = () => S.DamagePerSlow(1).Situational(BoardVariant.Mixed, 0.6, "needs slows on the board"),
            ["first_strike"] = () => S.Conditional(2, Condition.FirstHit),
            ["lucky_coin"] = () => S.RarityUpgrade(0.45).Because("best of two rarity rolls: about half a rarity step per offer"),
            ["permafrost"] = () => S.FreezeDuration(1.5, true).GivenFreeze().Situational(BoardVariant.Mixed, 0.2, "needs a freeze source"),
            ["piggy_bank"] = () => S.Interest(0.05, w => 10 * w),
            ["repeater"] = () => S.AttackSpeed(0.5, Ballista).DamagePercent(-0.08, Ballista).Requires(Ballista).Because("+50% damage per second; bolts in the air are lost on kills (-8%)").Simulated("repeater"),
            ["scavenger"] = () => S.KillGoldShare(3, 0.15, "+3 gold on crit kills (15% of kills)"),
            ["sturdy_walls"] = () => S.MaxHealth(5).HealNow(5),
            ["tithe_box"] = () => S.GoldPerWavePerTower(5),
            ["trophy_hunter"] = () => S.Treasures(c => 0.5 * MiniBosses(c.Acquired, c.Wave), "+0.5 chests per mini-boss").Offered(1),
            ["whetstone"] = () => S.BaseDamage(1),
            ["woodcutters_luck"] = () => S.TreasuresPerWave(0.15 * ReferenceBoards.ChopsPerWave),

            // ----- Uncommon -----
            ["bloodthirst"] = () => S.Crit(0.15).Because("+1% crit per kill this wave, max 30%: about +15% on average"),
            ["bounty_board"] = () => S.KillGold(1),
            ["bulwark"] = () => S.AbsorbPerWave(3),
            ["chain_lightning"] = () => S.Targets(0.3, Tesla).Requires(Tesla).Because("20 chains reach further than the coil's pulse in dense waves (proxy)").Simulated("chain-lightning"),
            ["diverse_portfolio"] = () => S.KillGold(ReferenceBoards.TypesPerKill).Because("doubles the vanilla gold per tower type"),
            ["firing_line"] = () => S.Targets(0.5, Cannon).Range(2, Cannon).Requires(Cannon).Situational(BoardVariant.CannonLine, 0.6, "needs a long straight").Simulated("firing-line"),
            ["hemorrhage"] = () => S.Dot(0.5 * 0.15, 0, 0).Because("50% bleed on crits, at about 15% crit"),
            ["high_ground"] = () => S.HeightFactor(2),
            ["hoarder"] = () => S.DamagePerTreasure(0.04),
            ["icicle_cannon"] = () => S.DamagePercent(2, Frost).Targets(0.5, Frost).Range(4, Frost).Requires(Frost).Because("3x damage, pierces a line (proxy)").Simulated("icicle-cannon"),
            ["keen_eye"] = () => S.Crit(0.05),
            ["loan_shark"] = () => S.Loan(1000, 150, 10),
            ["lone_scout"] = () => S.Estimated(10, "scout shots carry the mark's damage; depends on the Lookout's level").Requires(Lookout).Simulated("lone-scout"),
            ["long_sight"] = () => S.Range(1),
            ["moat"] = () => S.ZoneSlow(0.2),
            ["prodigy"] = () => S.StartLevel(3),
            ["seeker_rounds"] = () => S.Accuracy(1, Radar).Requires(Radar).Requires("a Radar (not required to be offered)").Situational(BoardVariant.Mixed, 0.25, "only biplanes miss; Ballista bolts already home"),
            ["witchs_brew"] = () => S.Dot(0, 0.25 * 0.6, 0).Situational(BoardVariant.DamageOverTime, 0.35, "needs poison"),
            ["sapling_seeds"] = () => S.TreeGold(0.6).Requires("Druid Grove").Because("more trees to chop (estimated +60% chops)"),
            ["wall_cannons"] = () => S.Estimated(10, "four small castle cannons at 40% damage").Requires("Siege Workshop"),

            // ----- Rare -----
            ["alchemists_furnace"] = () => S.KillGoldShare(4, 0.5, "+4 gold on burning kills").ManaPerKill(0.01).Situational(BoardVariant.DamageOverTime, 0.35, "needs burn and poison"),
            ["arcane_overflow"] = () => ForEach(ManaTowers, (s, t) => s.Conditional(1.5, Condition.ManaAbove90, t)).Situational(BoardVariant.Mana, 0.3, "needs a mana surplus"),
            ["bigger_bombs"] = () => S.Area(0.5, Mortar).DamagePercent(0.25, Mortar).Requires(Mortar),
            ["cluster_munitions"] = () => S.DamagePercent(1.5, Mortar).Area(-0.5, Mortar).Requires(Mortar).Because("5 bomblets at half damage and radius").Simulated("cluster-munitions"),
            ["executioner"] = () => S.Conditional(2, Condition.BelowHealth20),
            ["frost_nova"] = () => S.GlobalSlow(0.08).GivenFreeze().Situational(BoardVariant.Mixed, 0.2, "needs a freeze source").Because("25% slow around each freeze (about 8% overall)"),
            ["golden_goblin"] = () => S.TreasuresPerWave(0.35 * 0.85),
            ["jackpot"] = () => S.GoldPerWave(w => WaveModel.Get(w).Count / 40 * 15 * w, "every 40th kill: 15 x level gold"),
            ["lightning_rod"] = () => S.AttackSpeed(0.25 * 0.3).Requires(Tesla).Because("about 30% of the towers stand near a coil"),
            ["nest_of_bats"] = () => S.DamagePercent(1.2, Vampire).Dot(1, 0, 0, Vampire).Requires(Vampire).Because("10 bats biting once a second (proxy)").Simulated("nest-of-bats"),
            ["overkill"] = () => S.OverkillCarry(1),
            ["test_ascension"] = () => S.Unrated("development test ascension"),
            ["ricochet_bolts"] = () => S.ProcHits(0.75, Ballista),
            ["shatter"] = () => S.Conditional(3, Condition.Frozen).GivenFreeze().Situational(BoardVariant.Mixed, 0.2, "needs a freeze source"),
            ["springald"] = () => S.Targets(1.5, Ballista).Requires(Ballista).Because("pierces up to 5 monsters, -20% each (proxy)").Simulated("springald"),
            ["static_arc"] = () => S.ProcHits(0.12 * 2),
            ["strike_fighter"] = () => S.AttackSpeed(0.67, Radar).Accuracy(0.6, Radar).Requires(Radar).Because("faster fire, tighter turns (proxy)").Simulated("strike-fighter"),
            ["supercapacitor"] = () => S.AttackSpeed(-0.4, Tesla).DamagePercent(2, Tesla).Requires(Tesla),
            ["supercharge"] = () => S.AttackSpeed(0.3 * 0.3).Requires(Tesla).Because("+30% speed near each coil (about 30% of the towers)").Simulated("supercharge"),
            ["thunderclap"] = () => S.Stun(0.06),
            ["treasure_hoard"] = () => S.GoldNow(a => 3.0 * a * a, "3 x level² gold now"),
            ["veteran_training"] = () => S.LevelChance(0.3),
            ["ironbark"] = () => S.MaxHealth(5).Requires("Druid Grove").Because("about +5 by mid-run"),
            ["catapult"] = () => S.Estimated(14, "a castle catapult, 2x cannon damage in a large area").Requires("Siege Workshop"),
            ["grapeshot"] = () => S.Estimated(12, "3 extra pellets per castle shot").Requires("Siege Workshop"),

            // ----- Epic -----
            ["beam_cannon"] = () => S.DamagePercent(0.3, Particle).ManaCost(0.6, Particle).Requires(Particle).Simulated("beam-cannon"),
            ["bladestorm"] = () => S.DamagePercent(1.5, Shredder).Requires(Shredder).Because("blades circling the Shredder (proxy)").Simulated("bladestorm"),
            ["carpet_bomber"] = () => S.DamagePercent(1.5, Radar).Area(1, Radar).Requires(Radar).Because("3x damage bombs in a line (proxy)").Simulated("carpet-bomber"),
            ["contagion"] = () => S.DotSpread(2).Situational(BoardVariant.DamageOverTime, 0.35, "needs damage over time"),
            ["dead_mans_land"] = () => S.DamagePercent(1.5, Encampment).Requires(Encampment).Because("mines stay and stack charges (proxy)").Simulated("dead-mans-land"),
            ["four_leaf_clover"] = () => S.LuckReroll().RarityUpgrade(0.3),
            ["fragmentation"] = () => S.DamagePercent(1.5, Mortar).Requires(Mortar).Because("3 extra blasts at half damage"),
            ["glass_cannon"] = () => S.DamagePercent(0.5).MaxHealthPercent(-0.5),
            ["hex_of_frailty"] = () => S.Hex(1),
            ["hivemind"] = () => S.BaseDamagePerTowers(1, 3),
            ["hwacha"] = () => S.Targets(1.5, Cannon).AttackSpeed(-0.5, Cannon).Dot(0, 0.3, 0, Cannon).Requires(Cannon).Because("20 rockets with small blasts (proxy)").Simulated("hwacha"),
            ["magma_lobber"] = () => S.DamagePercent(3, Flame).AttackSpeed(-0.6, Flame).Area(1, Flame).Requires(Flame).Because("4x damage in a large blast plus pools (proxy)").Simulated("magma-lobber"),
            ["quick_study"] = () => S.CardsPerWave(1 / 2.0 - 1 / 3.0),
            ["retribution"] = () => S.AbsorbPerWave(6).Because("leaks hit nearby monsters for 15% of their health (estimated as 6 absorbed per wave)"),
            ["sealed_fate"] = () => S.DamagePercent(2, Monument).Requires(Monument).Because("stabs grow to 3x instead of falling (proxy)").Simulated("sealed-fate"),
            ["triple_threat"] = () => S.Conditional(2, Condition.AllThreeDots).Situational(BoardVariant.DamageOverTime, 0.35, "needs bleed, burn and poison"),
            ["volatile_corpses"] = () => S.DeathBlast(0.1, 1.5),
            ["wingman"] = () => S.DamagePercent(1, Radar).Requires(Radar),
            ["golden_sap"] = () => S.TreeGold(1).HealPerWave(0.5).Requires("Druid Grove"),
            ["incendiary_shells"] = () => S.Estimated(14, "castle cannon burn").Requires("Siege Workshop"),
            ["siege_engineer"] = () => S.Estimated(16, "castle cannon +1 damage per tower").Requires("Siege Workshop"),

            // ----- Legendary -----
            ["domino_effect"] = () => S.OnDeathFactor(1.5).DeathBlast(0.03, 1.5).Given("Volatile Corpses", b => { b.Mods.DeathBlast += 0.1; b.Mods.DeathBlastRadius = 1.5; })
                .Situational(BoardVariant.Mixed, 0.25, "needs on-death effects"),
            ["legion"] = () => S.PriceStep(-1).AttackSpeed(0.03 * 4).DamagePercent(-0.15).Because("about 4 towers within 3 range"),
            ["ley_line_network"] = () => S.ManaPerSecond(6).Requires("Mana Siphons").Because("about 6 mana/s more from a network (proxy)").Simulated("ley-lines"),
            ["master_of_elements"] = () => S.Dot(0.1, 0.1, 0.1),
            ["mega_mortar"] = () => S.DamagePercent(0.6, Mortar).Area(2, Mortar).Range(2, Mortar).Requires(Mortar).Because("fused modules: bigger blasts (proxy)").Simulated("mega-mortar"),
            ["monolith"] = () => S.PerFewerTowers(10, 0.25, 0.05).Situational(BoardVariant.Tall, 0.2, "needs fewer than 10 towers"),
            ["particle_accelerator"] = () => S.Estimated(26, "black holes: hold and burst every monster near the target").Requires(Particle).Simulated("particle-accelerator"),
            ["prism_of_ascendance"] = () => S.RarityUpgrade(0.6),
            ["second_wind"] = () => S.SafetyNet(1.5).Because("survives once at half health: the safety net x1.5"),
            ["saw_dimension"] = () => S.DamagePercent(1.8, Shredder).Targets(2, Shredder).Requires(Shredder).Because("saws over the whole path (proxy)").Simulated("saw-dimension"),
            ["grand_bombard"] = () => S.Estimated(18, "castle cannon x3 damage, 30% slower").Requires("Siege Workshop"),
            ["trebuchet_battery"] = () => S.Estimated(16, "three catapult boulders").Requires("Catapult"),

            // ----- Mythical -----
            ["infinity_engine"] = () => S.AttackSpeed(1).ManaCost(-0.5),
            ["midas_touch"] = () => S.IncomePercent(1),
            ["ritual_of_the_sun"] = () => S.Dot(0, 0.25, 0, Obelisk).DamagePercent(0.6, Obelisk).Targets(2, Obelisk).Requires(Obelisk).Because("rings: the average damage on a large area (proxy)").Simulated("ritual-sun"),
            ["time_dilation"] = () => S.GlobalSlow(0.3),
            ["war_blimp"] = () => S.DamagePercent(2, Radar).Targets(1, Radar).Requires(Radar).Because("gatling and bombs over the thickest crowd, plus a deck slot (proxy)").Simulated("war-blimp"),
        };

        /// <summary>Custom cards by id; the tier (1-based) is passed for chains whose tiers differ.</summary>
        public static readonly Dictionary<string, Func<int, Spec>> Cards = new Dictionary<string, Func<int, Spec>>
        {
            ["lucky_find"] = _ => S.TreasureNow(),
            ["treasure_sense"] = _ => S.TreasuresPerWave(0.05 * 3 * Pools.CardsPerWave).Because("5% per level-up card slot to become a treasure"),
            ["cartographer"] = _ => S.TreasureOptions(1),
            ["blood_pact"] = _ => S.TreasureNow(Rarity.Rare).MaxHealth(-5),
            ["old_growth"] = _ => S.TreeGold(0.25).Because("more trees, more chops (estimated)"),
            ["restless_dead"] = _ => S.Estimated(2, "more graves for Haunted Houses").Requires("Haunted House"),
            ["forgotten_shrines"] = _ => S.Estimated(2, "more shrines for Universities").Requires("University"),
            ["rich_veins"] = _ => S.Estimated(2, "more iron veins for Mines").Requires("Mine"),
            ["ley_lines"] = _ => S.Estimated(2, "more mana crystals for Siphons").Requires("Mana Siphon"),
            ["royal_grant"] = _ => S.GoldNow(500),
            ["trade_route"] = _ => S.GoldPerWave(w => 3 * w, "3 x level gold per wave"),
            ["compound_interest"] = t => t == 1 ? S.Interest(0.04, w => 300 + 15 * w) : S.Interest(0.04, w => 2 * (300 + 15 * w)),
            ["investment"] = _ => S.Custom(Category.Economy, "half the gold, double back after 5 waves", c =>
            {
                double half = ReferenceBoards.Get(c.Acquired).Bank / 2;
                c.Mods.ExtraGold += c.Wave - c.Acquired >= 5 ? 3 * half : -half;
            }),
            ["bounty_hunters"] = _ => S.GoldPerWave(w => (w > 16 && w % 2 == 1 || w % 10 == 5) ? 10 * w : 0, "10 x level gold per boss and mini-boss"),
            ["lumber_mill"] = _ => S.TreeGold(0.5),
            ["war_bonds"] = _ => S.GoldPerWave(w => 3 * w * Evaluator.LeakShare * Evaluator.AverageHealth * 100 * 0.5, "3 x level gold per health lost"),
            ["bulk_discount"] = _ => S.PriceStep(-0.3),
            ["master_mason"] = _ => S.LevelCost(-0.2),
            ["undivided_attention"] = _ => S.Custom(Category.Offense, "+100% attack speed shared", c => c.Board.All.RateFactor *= 1 + 1 / Math.Max(1, c.Board.TowerCount))
                .Situational(BoardVariant.Tall, 0.2, "strong only with few towers"),
            ["wider_hand"] = _ => S.CardOptions(1),
            ["field_repairs"] = _ => S.HealPerWave(1),

            // castle cards
            ["heartwood"] = _ => S.HealPerWave(0.5).Requires("Druid Grove"),
            ["vault_interest"] = _ => S.Interest(0.01, w => 30 * w).Requires("Gilded Vault"),
            ["deeper_vaults"] = _ => S.Interest(0.01, w => 30 * w).Requires("Gilded Vault").Because("raises the vault's interest cap (estimated)"),
            ["royal_charter"] = _ => S.GoldPerWave(w => 0.15 * 5 * w, "+15% vault income").Requires("Gilded Vault"),
            ["heavier_shot"] = _ => S.Estimated(4, "+4 castle cannon damage").Requires("Siege Workshop"),
            ["quick_reload"] = _ => S.Estimated(3.5, "castle cannon fires 12% faster").Requires("Siege Workshop"),
            ["long_barrel"] = _ => S.Estimated(3, "+1.5 castle cannon range").Requires("Siege Workshop"),

            // ascension cards
            ["swift_scouts"] = _ => S.Estimated(4, "one more scout shot per second").Requires("Lone Scout"),
            ["more_bats"] = _ => S.DamagePercent(0.3, Vampire).Requires(Vampire).Requires("Nest of Bats"),
            ["more_chains"] = _ => S.Targets(0.06, Tesla).Requires(Tesla).Requires("Chain Lightning"),
            ["more_bomblets"] = _ => S.DamagePercent(0.2, Mortar).Requires(Mortar).Requires("Cluster Munitions"),
        };

        /// <summary>
        /// Points of Interest by id (docs/poi-plan.md). Budget: <see cref="Pools.PoiBudget"/> (15) from the wave the POI can
        /// appear; a POI is rated like a treasure taken when its section is placed.
        /// </summary>
        public static readonly Dictionary<string, Func<Spec>> Pois = new Dictionary<string, Func<Spec>>
        {
            ["lightning_trap"] = () => S.FixedSource("lightning in the centre cell (damage follows the wave and the mana)", global::TreasureRun.Pois.LightningTrapLogic.Info)
                .Offered(global::TreasureRun.Pois.LightningTrapLogic.MinWave)
                .Because("strikes everything in the centre cell 4 times a second; 0-15 mana/s (rated at the best rate per wave), damage in proportion, mana paid in mana buildings; solved for 15 points per wave"),
            ["merchant"] = () => Merchant(global::TreasureRun.Pois.MerchantKind.Normal),
            ["legendary_merchant"] = () => Merchant(global::TreasureRun.Pois.MerchantKind.Legendary),
            ["ascension_merchant"] = () => Merchant(global::TreasureRun.Pois.MerchantKind.Ascension),
            ["soul_urn"] = SoulUrn,
            ["wizard_tower"] = () => WizardTower(global::TreasureRun.Pois.WizardTowerLogic.Power),
            ["lizard_nest"] = () => LizardNest(1),
        };

        /// <summary>
        /// The Wizard's Tower at a power (its base damage by wave, Pois.WizardTowerLogic.Power): every damaging spell is a
        /// fixed source with its share of the casts (fireball, lightning, ice spikes, the tornado's grinding); the tornado's
        /// drag counts as a slow, the necromancy's minions and goldify's kills as hit points taken, goldify's gold as gold.
        /// </summary>
        public static Spec WizardTower(Func<int, double> power)
        {
            return S.FixedSource("fireball at the largest group (blast, burn)", w => global::TreasureRun.Pois.WizardTowerLogic.FireballInfo(power(w)),
                                 b => b.TargetFactor = global::TreasureRun.Pois.WizardTowerLogic.GroupBonus)
                .FixedSource("lightning (shields first)", w => global::TreasureRun.Pois.WizardTowerLogic.LightningInfo(power(w)))
                .FixedSource("ice spikes (fastest first, slow)", w => global::TreasureRun.Pois.WizardTowerLogic.IceInfo(power(w)))
                .FixedSource("the tornado's grinding", w => global::TreasureRun.Pois.WizardTowerLogic.TornadoInfo(power(w)))
                .Custom(Category.Control, "the tornado drags monsters back", c =>
                    c.Mods.GlobalSlow = 1 - (1 - c.Mods.GlobalSlow) * (1 - global::TreasureRun.Pois.WizardTowerLogic.TornadoSlow(c.Board)))
                .Custom(Category.Offense, "necromancy: monsters slain in the aura rise as minions", c =>
                    c.Mods.ExtraRemoved += global::TreasureRun.Pois.WizardTowerLogic.NecromancyShare(c.Board))
                .Custom(Category.Offense, "goldify kills the toughest monster (not a boss)", c =>
                {
                    WaveStats w = WaveModel.Get(c.Wave);
                    c.Mods.ExtraRemoved += global::TreasureRun.Pois.WizardTowerLogic.GoldifiesPerWave(c.Board) * 2 / Math.Max(1, w.Count);
                })
                .Custom(Category.Economy, "goldify: 25x its kill gold", c =>
                {
                    double gold = 0;
                    for (int w = c.Acquired; w <= c.Wave; w++)
                        gold += (w == c.Wave ? 0.5 : 1) * global::TreasureRun.Pois.WizardTowerLogic.GoldifiesPerWave(ReferenceBoards.Get(w, c.Board.Variant))
                                * global::TreasureRun.Pois.WizardTowerLogic.GoldifyFactor * global::TreasureRun.Pois.WizardTowerLogic.KillGold(w);
                    c.Mods.ExtraGold += gold;
                })
                .Offered(global::TreasureRun.Pois.WizardTowerLogic.MinWave)
                .Because($"a random spell every 5 s at monsters within {(global::TreasureRun.Pois.WizardTowerLogic.Range):0} cells (fireball, lightning, ice spikes 3 in 11.4 each, tornado and " +
                         "necromancy 1, goldify 0.4); damage fixed by wave, solved for 15 points per wave (probe BalanceProbe.WizardTowerPower). " +
                         "The ley link (10 mana/s for a spell every 4 s) needs the Ley Line Network and isn't rated");
        }

        /// <summary>
        /// The Lizard Nest: in the wave it appears (the acquisition wave) its lizards and their hatchlings join the wave, as
        /// more health and armor on that wave's monsters (Pois.LizardNestLogic.ExtraLayers); a treasure chest from the next
        /// wave on (a treasure pick); their kill gold.
        /// </summary>
        public static Spec LizardNest(double toughness)
        {
            return S.Custom(Category.Survival, "lizards and their brood join the wave it appears in", c =>
                {
                    if (c.Wave != c.Acquired) return;
                    WaveStats w = WaveModel.Get(c.Wave);
                    var (health, armor) = global::TreasureRun.Pois.LizardNestLogic.ExtraLayers(c.Wave);
                    health *= toughness; armor *= toughness;
                    if (w.Armor > 0) c.Mods.Monster.ExtraArmor += (float)(armor / w.Armor);
                    else health += armor;
                    c.Mods.Monster.ExtraHealth += (float)(health / Math.Max(1, w.Health));
                })
                .Custom(Category.Meta, "a treasure chest after that wave", c => { if (c.Wave > c.Acquired) c.Mods.DirectPoints += Pools.TreasurePick(c.Wave); })
                .Custom(Category.Economy, "their kill gold", c => c.Mods.ExtraGold += (c.Wave > c.Acquired ? 1 : 0.5) * global::TreasureRun.Pois.LizardNestLogic.KillGold(c.Acquired))
                .Offered(global::TreasureRun.Pois.LizardNestLogic.MinWave)
                .Because($"{(global::TreasureRun.Pois.LizardNestLogic.MinLizards)}-{(global::TreasureRun.Pois.LizardNestLogic.MaxLizards)} lizards ({(global::TreasureRun.Pois.LizardNestLogic.LizardHealth):0.#}x " +
                         $"the wave's average monster in health, {(global::TreasureRun.Pois.LizardNestLogic.LizardArmor):0.#}x in armor), each releasing " +
                         $"{(global::TreasureRun.Pois.LizardNestLogic.MinBrood)}-{(global::TreasureRun.Pois.LizardNestLogic.MaxBrood)} hatchlings ({(global::TreasureRun.Pois.LizardNestLogic.HatchlingHealth):0.##}x), " +
                         "counted as more health and armor on its one wave; then a chest");
        }

        /// <summary>
        /// The Soul Urn: full after about <c>FillDelay</c> waves of kills in its reach, it leaves one of three rewards: a
        /// treasure chest (a treasure pick), gold worth 1.5 chests at the wave the urn appeared (Pois.SoulUrnLogic.Gold), or a
        /// rune that gives one tower +100% attack speed (<c>RuneBonus</c>; the tower counts <c>RuneTowerShare</c> average
        /// towers). Each counts a third, from the wave the urn fills.
        /// </summary>
        private static Spec SoulUrn()
        {
            const int d = global::TreasureRun.Pois.SoulUrnLogic.FillDelay;
            return S.Custom(Category.Meta, "a treasure chest (1 in 3)", c =>
                    { if (c.Wave >= c.Acquired + d) c.Mods.DirectPoints += Pools.TreasurePick(c.Wave) / 3; })
                .Custom(Category.Economy, "gold worth 1.5 chests (1 in 3)", c =>
                    { if (c.Wave >= c.Acquired + d) c.Mods.ExtraGold += global::TreasureRun.Pois.SoulUrnLogic.Gold(c.Acquired) / 3.0; })
                .Custom(Category.Offense, "a rune: one tower +100% attack speed (1 in 3)", c =>
                {
                    if (c.Wave < c.Acquired + d) return;
                    double bonus = global::TreasureRun.Pois.SoulUrnLogic.RuneBonus * global::TreasureRun.Pois.SoulUrnLogic.RuneTowerShare;
                    c.Board.All.RateFactor *= 1 + bonus / Math.Max(1, c.Board.TowerCount) / 3;
                })
                .Offered(global::TreasureRun.Pois.SoulUrnLogic.MinWave, global::TreasureRun.Pois.SoulUrnLogic.LastOmenWave)
                .Because("fills after about 4 waves of kills in its reach (all its lane's monsters over 3 waves), then a chest, gold " +
                         "worth 1.5 chests at the wave it appeared, or a rune: +100% attack speed for the strongest tower (counted as three average towers), a third each");
        }

        /// <summary>
        /// A Wandering Merchant: the treasures a player is assumed to buy, at their budgets, minus the gold they cost
        /// (Pois.MerchantLogic). Normal and Legendary: the discounted offer and one more (the last offer stays, so the shop
        /// stays open for selling). Ascension: one purchase, the discounted one half the time.
        /// </summary>
        private static Spec Merchant(global::TreasureRun.Pois.MerchantKind kind)
        {
            const double d = global::TreasureRun.Pois.MerchantLogic.Discount;
            bool asc = kind == global::TreasureRun.Pois.MerchantKind.Ascension;
            // Expected budget and price of an offer at the wave the merchant arrives.
            (double budget, double price) Offer(int wave)
            {
                double wsum = 0, b = 0, p = 0;
                for (Rarity r = Rarity.Common; r <= Rarity.Legendary; r++)
                {
                    double weight = kind == global::TreasureRun.Pois.MerchantKind.Legendary ? (r == Rarity.Legendary ? 1 : 0)
                        : Pools.Weight(r, wave) * (asc ? global::TreasureRun.Pois.MerchantLogic.AscensionsPerRarity[(int)r] : 1);
                    wsum += weight;
                    b += weight * Pools.Budget(r) * (asc ? Pools.AscensionAllowance : 1);
                    p += weight * global::TreasureRun.Pois.MerchantLogic.Price(kind, (int)r, wave, asc, false);
                }
                return (b / wsum, p / wsum);
            }
            int bought = asc ? 1 : 2;
            double paid = asc ? 1 - d / 2 : (1 - d) + 1;
            return S.Custom(Category.Meta, $"{bought} {(asc ? "ascension" : "treasures")} bought", c => c.Mods.DirectPoints += bought * Offer(c.Acquired).budget)
                .GoldNow(a => -paid * Offer(a).price, "their price")
                .Offered(global::TreasureRun.Pois.MerchantLogic.MinWave(kind))
                .Because(asc ? "one ascension bought (the discounted one half the time), rated at its budget; price from MerchantLogic"
                             : "the discounted offer and one more bought, rated at their budgets; price from MerchantLogic");
        }

        /// <summary>Spec of a Point of Interest (null when missing).</summary>
        public static Spec Poi(string id) => Pois.TryGetValue(id, out var f) ? f() : null;

        /// <summary>Spec of a treasure (null when missing).</summary>
        public static Spec Treasure(string id) => Treasures.TryGetValue(id, out var f) ? f() : null;

        /// <summary>Spec of a custom card tier (null when missing).</summary>
        public static Spec Card(string id, int tier) => Cards.TryGetValue(id, out var f) ? f(tier) : null;

        private static Spec ForEach(TowerKind[] kinds, Action<Spec, TowerKind> add)
        {
            var s = new Spec();
            foreach (TowerKind k in kinds) add(s, k);
            return s;
        }

        private static int MiniBosses(int from, int to)
        {
            int n = 0;
            for (int w = Math.Max(17, from); w <= to; w++) if (w % 2 == 1) n++;
            return n;
        }
    }
}
