using System;
using System.Collections.Generic;

namespace TreasureRun.Balance
{
    /// <summary>Report split (section 8).</summary>
    public enum Category { Offense, Control, Economy, Survival, Meta }

    /// <summary>Where an atom is applied: the board at the evaluated wave, for an item acquired at <see cref="Acquired"/>.</summary>
    public sealed class AtomContext
    {
        public Board Board;
        public int Acquired, Wave;
        /// <summary>Waves the item has been working: the waves from the acquisition up to (and half of) the evaluated one.</summary>
        public double WavesHeld => Math.Max(0, Wave - Acquired) + 0.5;
        public BoardMods Mods => Board.Mods;
    }

    public sealed class Atom
    {
        public Category Category;
        public string Text;
        public Action<AtomContext> Apply;
    }

    /// <summary>
    /// The balance description of a card, treasure or tower: a list of effect atoms plus how it is judged
    /// (docs/balance-framework.md sections 2 and 7). Built fluently: <c>new Spec().DamagePercent(0.5).MaxHealthPercent(-0.5)</c>.
    /// </summary>
    public sealed class Spec
    {
        public readonly List<Atom> Atoms = new List<Atom>();
        public BoardVariant Variant = BoardVariant.Mixed;
        public double Breadth = 1;
        public string BreadthReason;
        public TowerKind? RequiredTower;
        public string Requirement;
        public int OfferFrom = 1, OfferTo = WaveModel.LastWave;
        public double? Estimate;
        public string EstimateReason, UnratedReason, SimulatedBy, Note;
        /// <summary>Set-ups applied to the board with and without the item (the situation it is judged in).</summary>
        public readonly List<(string Text, Action<Board> Apply)> Setups = new List<(string, Action<Board>)>();

        public bool IsUnrated => UnratedReason != null;

        // ----- how it is judged -----

        /// <summary>Works only in some builds: judged on <paramref name="variant"/>, then scaled by breadth^0.3 (section 1.3).</summary>
        public Spec Situational(BoardVariant variant, double breadth, string reason = null)
        { Variant = variant; Breadth = breadth; BreadthReason = reason; return this; }

        /// <summary>Requirement for the offer (no penalty): the board is given this tower if it lacks it.</summary>
        /// <summary>
        /// Requires a tower in play: the board is given it if it lacks it, and the item is offered from the wave its
        /// tower typically appears (<see cref="ReferenceBoards.FirstWave"/>).
        /// </summary>
        public Spec Requires(TowerKind tower)
        {
            RequiredTower = tower;
            Requirement = Requirement ?? TowerData.Get(tower).Name;
            OfferFrom = Math.Max(OfferFrom, ReferenceBoards.FirstWave(tower));
            return this;
        }
        public Spec Requires(string what) { Requirement = what; return this; }
        public Spec Offered(int from, int to = WaveModel.LastWave) { OfferFrom = from; OfferTo = to; return this; }

        /// <summary>Judges the item on a board that has this set-up too (e.g. "a freeze source" for Shatter).</summary>
        public Spec Given(string text, Action<Board> setup) { Setups.Add((text, setup)); return this; }
        public Spec GivenFreeze(double chance = 0.06) => Given($"{chance:0%} freeze chance", b => b.All.Stun += chance);

        /// <summary>A hand estimate where the formulas can't reach; marked "estimated" in the report.</summary>
        public Spec Estimated(double points, string reason) { Estimate = points; EstimateReason = reason; return this; }
        public Spec Unrated(string reason) { UnratedReason = reason; return this; }
        /// <summary>The in-game scenario or M8 simulation that should measure it.</summary>
        public Spec Simulated(string by) { SimulatedBy = by; return this; }
        public Spec Because(string note) { Note = note; return this; }

        /// <summary>An atom written in place, for effects the builder has no method for.</summary>
        public Spec Custom(Category c, string text, Action<AtomContext> apply) => Add(c, text, apply);

        private Spec Add(Category c, string text, Action<AtomContext> apply)
        {
            Atoms.Add(new Atom { Category = c, Text = text, Apply = apply });
            return this;
        }

        private static IEnumerable<TowerBonus> Bonuses(AtomContext c, TowerKind? t)
        {
            if (t == null) { yield return c.Board.All; yield break; }
            foreach (TowerGroup g in c.Board.Scope(t)) yield return g.Bonus;
        }

        private Spec Tower(Category cat, string text, TowerKind? t, Action<TowerBonus> f) =>
            Add(cat, t == null ? text : text + " (" + TowerData.Get(t.Value).Name + ")", c => { foreach (TowerBonus x in Bonuses(c, t)) f(x); });

        // ----- hits -----

        public Spec BaseDamage(double n, TowerKind? t = null) => Tower(Category.Offense, $"+{n} base damage", t, x => x.BaseDamage += n);
        public Spec DamagePercent(double p, TowerKind? t = null) => Tower(Category.Offense, $"{p:+0%;-0%} damage", t, x => x.DamageFactor *= 1 + p);
        public Spec LayerDamage(double health, double armor, double shield, TowerKind? t = null) =>
            Tower(Category.Offense, $"+{health}/{armor}/{shield} health/armor/shield damage", t, x => { x.HealthMult += health; x.ArmorMult += armor; x.ShieldMult += shield; });
        public Spec AttackSpeed(double p, TowerKind? t = null) => Tower(Category.Offense, $"{p:+0%;-0%} attack speed", t, x => x.RateFactor *= 1 + p);
        public Spec Range(double n, TowerKind? t = null) => Tower(Category.Offense, $"+{n} range", t, x => x.Range += n);
        public Spec Crit(double p, TowerKind? t = null) => Tower(Category.Offense, $"+{p:0%} crit", t, x => x.Crit += p);
        /// <summary>Crit chance of <paramref name="perLevel"/> percent per tower level (vanilla critChanceLevelMultiplier).</summary>
        public Spec CritPerLevel(double perLevel, TowerKind? t = null) => Add(Category.Offense, $"+{perLevel}% crit per level", c =>
        { foreach (TowerGroup g in c.Board.Scope(t)) g.Bonus.Crit += perLevel * g.Level / 100; });
        /// <summary>Makes towers pay mana per shot: <paramref name="rate"/> x damage (vanilla manaConsumptionAddition).</summary>
        public Spec ManaRate(double rate, TowerKind? t = null) => Tower(Category.Offense, $"+{rate} mana per damage", t, x => x.ManaRate += rate);
        /// <summary>Range from height (vanilla heightRangeBonusMultiplier).</summary>
        public Spec HeightRange(double mult, TowerKind? t = null) => Add(Category.Offense, $"+{mult:0%} range from height", c =>
        { foreach (TowerGroup g in c.Board.Scope(t)) g.Bonus.Range += g.Height / 2 * mult; });
        public Spec Stun(double p, TowerKind? t = null) => Tower(Category.Control, $"+{p:0%} freeze chance", t, x => x.Stun += p);
        public Spec Dot(double bleed, double burn, double poison, TowerKind? t = null) =>
            Tower(Category.Offense, $"+{bleed:0%}/{burn:0%}/{poison:0%} bleed/burn/poison", t, x => { x.Bleed += bleed; x.Burn += burn; x.Poison += poison; });
        public Spec Slow(double percentPerDamage, TowerKind? t = null) => Tower(Category.Control, $"+{percentPerDamage}% slow per damage", t, x => x.Slow += percentPerDamage);
        public Spec Area(double p, TowerKind? t = null) => Tower(Category.Offense, $"{p:+0%} area", t, x => x.AreaFactor *= 1 + p);
        public Spec Targets(double p, TowerKind? t = null) => Tower(Category.Offense, $"{p:+0%} monsters hit per attack", t, x => x.TargetFactor *= 1 + p);
        /// <summary>Extra hits on nearby monsters per direct hit: chance x count x damage share.</summary>
        public Spec ProcHits(double perHit, TowerKind? t = null) => Tower(Category.Offense, $"+{perHit:0.##} extra hits per hit", t, x => x.ProcHits += perHit);
        public Spec Accuracy(double a, TowerKind? t = null) => Tower(Category.Offense, $"accuracy {a:0%}", t, x => x.Accuracy = a);
        public Spec ManaCost(double p, TowerKind? t = null) => Tower(Category.Offense, $"{p:+0%;-0%} mana cost", t, x => x.ManaCostFactor *= 1 + p);
        public Spec Conditional(double mult, Condition when, TowerKind? t = null) =>
            Tower(Category.Offense, $"x{mult} damage while {when}", t, x => x.Conditional.Add((mult, when)));
        /// <summary>+n base damage for every 15% slow on the target (Cryo Conductor).</summary>
        public Spec DamagePerSlow(double n) => Tower(Category.Offense, $"+{n} damage per 15% slow", null, x => x.Conditional.Add((n, Condition.Slowed)));

        // ----- growth -----

        public Spec Levels(double n, TowerKind? t = null) => Tower(Category.Offense, $"+{n} levels", t, x => x.Levels += n);
        public Spec XpPercent(double p, TowerKind? t = null) => Tower(Category.Offense, $"{p:+0%} XP", t, x => x.XpFactor *= 1 + p);
        public Spec HeightFactor(double f) => Tower(Category.Offense, $"height bonus x{f}", null, x => x.HeightFactor *= f);

        /// <summary>Towers built after the acquisition start at a higher level (Prodigy).</summary>
        public Spec StartLevel(double level) => Add(Category.Offense, $"new towers start at level {level}", c =>
        {
            Board then = ReferenceBoards.Get(c.Acquired, c.Board.Variant);
            foreach (TowerGroup g in c.Board.Groups)
            {
                double before = then.Group(g.Kind)?.Count ?? 0;
                double newShare = g.Count > 0 ? Math.Max(0, g.Count - before) / g.Count : 0;
                double xpStart = 5 * level * (level - 1); // XP of a fresh tower at that level
                double l = g.Level, xp = 5 * l * (l - 1) + xpStart;
                double boosted = (1 + Math.Sqrt(1 + 4 * xp / 5)) / 2;
                g.Bonus.Levels += (boosted - l) * newShare;
            }
        });

        /// <summary>Each level gained after the acquisition has this chance to come twice (Veteran Training).</summary>
        public Spec LevelChance(double p) => Add(Category.Offense, $"{p:0%} chance of a double level", c =>
        {
            double gained = Math.Max(0, ReferenceBoards.Level(c.Wave) - ReferenceBoards.Level(c.Acquired));
            foreach (TowerGroup g in c.Board.Groups) g.Bonus.Levels += gained * p;
        });

        // ----- scaling with the board -----

        public Spec DamagePerTreasure(double p) => Add(Category.Offense, $"+{p:0%} damage per treasure", c =>
            c.Board.All.DamageFactor *= 1 + p * (c.Board.TreasuresOwned + 1));
        public Spec BaseDamagePerTowers(double n, double per) => Add(Category.Offense, $"+{n} damage per {per} towers", c =>
            c.Board.All.BaseDamage += n * Math.Floor(c.Board.TowerCount / per));
        /// <summary>Monolith: damage and attack speed per tower fewer than <paramref name="limit"/>.</summary>
        public Spec PerFewerTowers(int limit, double damage, double speed) => Add(Category.Offense, $"+{damage:0%} damage, +{speed:0%} speed per tower under {limit}", c =>
        {
            double fewer = Math.Max(0, limit - c.Board.TowerCount);
            c.Board.All.DamageFactor *= 1 + damage * fewer;
            c.Board.All.RateFactor *= 1 + speed * fewer;
        });

        // ----- damage over time and on-death -----

        /// <summary>Layer index: 0 bleed (health), 1 burn (armor), 2 poison (shield).</summary>
        public Spec DotCap(int layer, double n) => Add(Category.Offense, $"+{n} {DotName(layer)}/s cap", c => c.Mods.DotCap[layer] += n);
        public Spec DotLayerBonus(int layer, double n) => Add(Category.Offense, $"+{n} layer damage while {DotName(layer)}", c => c.Mods.DotLayerBonus[layer] += n);
        public Spec Pop(int layer, double p) => Add(Category.Offense, $"crits pop {p:0%} of {DotName(layer)}", c => c.Mods.Pop[layer] += p);
        public Spec BleedingCrit(double p) => Add(Category.Offense, $"+{p:0%} crit on bleeding", c => c.Mods.BleedingCrit += p);
        public Spec BonusOnFrozen(double n) => Add(Category.Offense, $"+{n} damage on frozen", c => c.Mods.BonusOnStun += n);
        public Spec PoisonSlow(double p) => Add(Category.Control, $"poison slows {p}%", c => c.Mods.PoisonSlow += p);
        public Spec BurnSlowBonus(double p) => Add(Category.Offense, $"burn faster with slow (+{p})", c => c.Mods.BurnSlowBonus += p);
        public Spec Hex(double n) => Add(Category.Offense, $"+{n} to every multiplier on hexed", c => c.Mods.Hex += n);
        public Spec DeathBlast(double share, double radius) => Add(Category.Offense, $"death blast {share:0%} max hp r{radius}", c => { c.Mods.DeathBlast += share; c.Mods.DeathBlastRadius = Math.Max(c.Mods.DeathBlastRadius, radius); });
        public Spec OverkillCarry(double share) => Add(Category.Offense, $"{share:0%} of overkill passed on", c => c.Mods.OverkillCarry = Math.Min(1, c.Mods.OverkillCarry + share));
        public Spec DotSpread(double n) => Add(Category.Offense, $"DOT spreads to {n} on death", c => c.Mods.DotSpread += n);
        public Spec OnDeathFactor(double f) => Add(Category.Offense, $"on-death effects x{f}", c => c.Mods.OnDeathFactor *= f);

        private static string DotName(int l) => l == 0 ? "bleed" : l == 1 ? "burn" : "poison";

        // ----- crowd control and monsters -----

        public Spec GlobalSlow(double p) => Add(Category.Control, $"monsters {p:0%} slower", c => c.Mods.GlobalSlow = 1 - (1 - c.Mods.GlobalSlow) * (1 - p));
        public Spec ZoneSlow(double p) => Add(Category.Control, $"at least {p:0%} slow near the castle", c => c.Mods.ZoneSlow = Math.Max(c.Mods.ZoneSlow, p));
        public Spec SlowCap(double p) => Add(Category.Control, $"{p:+0%;-0%} slow cap", c => c.Mods.SlowCap += p);
        public Spec HasteCap(double p) => Add(Category.Control, $"{p:+0%;-0%} haste cap", c => c.Mods.HasteCap += p);
        public Spec FreezeDuration(double f, bool noDecay) => Add(Category.Control, $"freezes x{f}" + (noDecay ? ", no decay" : ""), c => { c.Mods.FreezeDuration *= f; c.Mods.FreezeNoDecay |= noDecay; });
        public Spec LuckReroll() => Add(Category.Offense, "luck rolls twice", c => c.Mods.LuckReroll = true);
        public Spec MonsterLayers(double health, double armor, double shield) => Add(Category.Offense, $"monsters {health:+0%;-0%}/{armor:+0%;-0%}/{shield:+0%;-0%} health/armor/shield", c =>
        { c.Mods.Monster.ExtraHealth += (float)health; c.Mods.Monster.ExtraArmor += (float)armor; c.Mods.Monster.ExtraShield += (float)shield; });
        public Spec MonsterSpeed(double n) => Add(Category.Control, $"monsters {n:+0.##;-0.##} speed", c => c.Mods.Monster.SpeedBonus += (float)n);
        public Spec Fortify(double n) => Add(Category.Offense, $"fortify {n:+0;-0}", c => c.Mods.Fortify += n);
        public Spec CastleDamagePerLeak(int n) => Add(Category.Survival, $"leaks deal {n:+0;-0}", c => c.Mods.Monster.ExtraCastleDamage += n);

        // ----- economy -----

        /// <summary>Gold at the acquisition (amount depends on the acquisition wave).</summary>
        public Spec GoldNow(Func<int, double> gold, string text) => Add(Category.Economy, text, c => c.Mods.ExtraGold += gold(c.Acquired));
        public Spec GoldNow(double gold) => GoldNow(_ => gold, $"+{gold} gold now");
        /// <summary>Gold every wave since the acquisition (amount by wave).</summary>
        public Spec GoldPerWave(Func<int, double> gold, string text) => Add(Category.Economy, text, c => c.Mods.ExtraGold += SumWaves(c, gold));
        public Spec GoldPerWavePerTower(double n) => Add(Category.Economy, $"+{n} gold per tower per wave", c =>
            c.Mods.ExtraGold += SumWaves(c, w => n * ReferenceBoards.Get(w, c.Board.Variant).TowerCount));
        public Spec KillGold(double n) => Add(Category.Economy, $"{n:+0;-0} gold per kill", c =>
            c.Mods.ExtraGold += SumWaves(c, w => n * WaveModel.Get(w).Count));
        /// <summary>Share more of all monster, income and tree gold (Midas Touch).</summary>
        public Spec IncomePercent(double p) => Add(Category.Economy, $"{p:+0%} gold income", c =>
            c.Mods.ExtraGold += SumWaves(c, w => p * ReferenceBoards.Income(w)));
        /// <summary>Gold per kill of a share of the kills (bosses, burning monsters, ...).</summary>
        public Spec KillGoldShare(double n, double shareOfKills, string text) => Add(Category.Economy, text, c =>
            c.Mods.ExtraGold += SumWaves(c, w => n * shareOfKills * WaveModel.Get(w).Count));
        public Spec TreeGold(double p) => Add(Category.Economy, $"{p:+0%} tree gold", c =>
            c.Mods.ExtraGold += SumWaves(c, w => p * ReferenceBoards.ChopsPerWave * w * w));
        /// <summary>Interest on unspent gold each wave, capped by wave.</summary>
        public Spec Interest(double rate, Func<int, double> cap) => Add(Category.Economy, $"{rate:0%} interest", c =>
            c.Mods.ExtraGold += SumWaves(c, w => Math.Min(rate * ReferenceBoards.Get(w, c.Board.Variant).Bank, cap(w))));
        public Spec Loan(double now, double repay, int waves) => Add(Category.Economy, $"+{now} now, -{repay} for {waves} waves", c =>
            c.Mods.ExtraGold += now - repay * Math.Min(waves, c.WavesHeld));
        /// <summary>Cheaper copies: the gold saved on copies bought since the acquisition.</summary>
        public Spec PriceStep(double p) => Add(Category.Economy, $"{p:+0%;-0%} price step", c =>
        {
            Board then = ReferenceBoards.Get(c.Acquired, c.Board.Variant);
            double saved = 0;
            foreach (TowerGroup g in c.Board.Groups)
            {
                TowerInfo t = g.Info;
                double n0 = then.Group(g.Kind)?.Count ?? 0, n1 = g.Count;
                saved += -p * t.PriceStep * (n1 * (n1 - 1) - n0 * (n0 - 1)) / 2;
            }
            c.Mods.ExtraGold += Math.Max(0, saved);
            c.Mods.PriceStepFactor *= 1 + p;
        });
        /// <summary>Cheaper levels: a typical run spends about 15% of its income on levels.</summary>
        public Spec LevelCost(double p) => Add(Category.Economy, $"{p:+0%;-0%} level cost", c =>
            c.Mods.ExtraGold += SumWaves(c, w => -p * 0.15 * ReferenceBoards.Income(w)));

        // ----- mana -----

        public Spec MaxMana(double n) => Add(Category.Offense, $"+{n} max mana", c => c.Mods.MaxMana += n);
        public Spec MaxManaPerBank(double n) => Add(Category.Offense, $"+{n} max mana per bank", c => c.Mods.MaxMana += n * c.Board.Banks);
        public Spec ManaRegen(double p) => Add(Category.Offense, $"+{p:0.#%} max mana per second", c => c.Mods.ManaRegen += p);
        public Spec ManaPerSecond(double n) => Add(Category.Offense, $"+{n} mana per second", c => c.Mods.ManaPerSecond += n);
        public Spec ManaPerKill(double p) => Add(Category.Offense, $"+{p:0%} max mana per kill", c => c.Mods.ManaPerKill += p);

        // ----- castle -----

        public Spec MaxHealth(double n) => Add(Category.Survival, $"{n:+0;-0} max health", c => c.Mods.MaxHealth += n);
        public Spec MaxHealthPercent(double p) => Add(Category.Survival, $"{p:+0%;-0%} max health", c => c.Mods.MaxHealthFactor *= 1 + p);
        /// <summary>A one-time heal, spread over the ten waves it typically saves.</summary>
        public Spec HealNow(double n) => Add(Category.Survival, $"heal {n}", c => c.Mods.HealPerWave += n / 10);
        public Spec HealPerWave(double n) => Add(Category.Survival, $"heal {n:0.##} per wave", c => c.Mods.HealPerWave += n);
        public Spec AbsorbPerWave(double n) => Add(Category.Survival, $"absorb {n} per wave", c => c.Mods.AbsorbPerWave += n);
        /// <summary>More safety than health alone gives (Second Wind: survive once at half health, x1.5).</summary>
        public Spec SafetyNet(double factor) => Add(Category.Survival, $"safety net x{factor}", c => c.Mods.SafetyFactor *= factor);

        // ----- meta (valued from the pools, section 4.7) -----

        /// <summary>Extra card picks since the acquisition, each worth an average card.</summary>
        public Spec CardsPerWave(double n) => Add(Category.Meta, $"+{n:0.##} cards per wave", c =>
            c.Mods.DirectPoints += Pools.CardPick * n * c.WavesHeld);
        /// <summary>One more option in every card draw.</summary>
        public Spec CardOptions(double n) => Add(Category.Meta, $"+{n} card options", c =>
            c.Mods.DirectPoints += Pools.CardPick * Pools.ExtraOption * n * Pools.CardsPerWave * c.WavesHeld);
        /// <summary>Treasures gained (count by wave since the acquisition).</summary>
        public Spec Treasures(Func<AtomContext, double> count, string text) => Add(Category.Meta, text, c =>
            c.Mods.DirectPoints += count(c) * Pools.TreasurePick(c.Wave));
        public Spec TreasuresPerWave(double n) => Treasures(c => n * c.WavesHeld, $"+{n:0.###} treasures per wave");
        public Spec TreasureNow(Rarity? atLeast = null) => Treasures(_ => 1, "a treasure now")
            .Add(Category.Meta, "", c => { if (atLeast != null) c.Mods.DirectPoints += Pools.RarityBoost(atLeast.Value, c.Wave); });
        /// <summary>One more option in every treasure offer.</summary>
        public Spec TreasureOptions(double n) => Add(Category.Meta, $"+{n} treasure options", c =>
            c.Mods.DirectPoints += Pools.TreasurePick(c.Wave) * Pools.ExtraOption * n * Pools.TreasuresPerWave * c.WavesHeld);
        /// <summary>Better rarity on a share of the treasure offers since the acquisition (in rarity steps).</summary>
        public Spec RarityUpgrade(double steps) => Add(Category.Meta, $"+{steps} rarity steps per offer", c =>
            c.Mods.DirectPoints += Pools.RarityStep * steps * Pools.TreasuresPerWave * c.WavesHeld);

        /// <summary>Gold summed over the waves the item has been working.</summary>
        private static double SumWaves(AtomContext c, Func<int, double> perWave)
        {
            double sum = 0;
            for (int w = c.Acquired; w < c.Wave; w++) sum += perWave(w);
            return sum + 0.5 * perWave(c.Wave);
        }
    }

    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Mythical }

    /// <summary>
    /// Values of the pools for meta items (section 4.7). Fixed estimates for now (uncalibrated): the points of an
    /// average card and treasure pick follow the budgets; the fixed-point iteration over the real pool comes later.
    /// </summary>
    public static class Pools
    {
        public const double CardPick = 4.5;
        public const double CardsPerWave = 1 / 3.0;
        public const double TreasuresPerWave = 0.25;
        /// <summary>Value of one more option in an offer, as a share of the pick.</summary>
        public const double ExtraOption = 0.12;
        /// <summary>Points of one rarity step on one treasure.</summary>
        public const double RarityStep = 4;

        public static double Budget(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return 12;
                case Rarity.Uncommon: return 18;
                case Rarity.Rare: return 24;
                case Rarity.Epic: return 30;
                case Rarity.Legendary: return 36;
                default: return 54;
            }
        }

        public const double CardBudget = 4, CardBudgetMin = 2, CardBudgetMax = 6;
        /// <summary>Ascensions may be this much stronger than their rarity: they are rolled rarer within it.</summary>
        public const double AscensionAllowance = 1.25;

        /// <summary>Rarity weights by wave (Treasures/Rarity.cs: early 55/28/12/4/1, late 30/30/22/13/5).</summary>
        public static double Weight(Rarity r, int wave)
        {
            double t = Math.Max(0, Math.Min(1, (wave - 1) / 44.0));
            switch (r)
            {
                case Rarity.Common: return 55 + (30 - 55) * t;
                case Rarity.Uncommon: return 28 + (30 - 28) * t;
                case Rarity.Rare: return 12 + (22 - 12) * t;
                case Rarity.Epic: return 4 + (13 - 4) * t;
                case Rarity.Legendary: return 1 + (5 - 1) * t;
                default: return 0;
            }
        }

        /// <summary>Points of picking the best of a three-treasure offer at a wave.</summary>
        public static double TreasurePick(int wave)
        {
            double sum = 0, wsum = 0;
            for (Rarity r = Rarity.Common; r <= Rarity.Legendary; r++) { sum += Weight(r, wave) * Budget(r); wsum += Weight(r, wave); }
            return 1.15 * sum / wsum;
        }

        /// <summary>Extra points of a treasure of at least <paramref name="r"/> over an average one.</summary>
        public static double RarityBoost(Rarity r, int wave)
        {
            double sum = 0, wsum = 0;
            for (Rarity x = r; x <= Rarity.Legendary; x++) { sum += Weight(x, wave) * Budget(x); wsum += Weight(x, wave); }
            return Math.Max(0, 1.15 * sum / wsum - TreasurePick(wave));
        }
    }
}
