using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.UI;
using UnityEngine;
using Ids = TreasureRun.Cards.CustomCardIds;

namespace TreasureRun.Cards
{
    public sealed class CustomCardSpec
    {
        public string Id;
        public string Title;
        public string Description;
        /// <summary>Title of the card that must be picked first (null = available from the start).</summary>
        public string Parent;
        /// <summary>Vanilla card whose image is reused (null = treasure gem).</summary>
        public string ImageFrom;
        public Action OnPick;
        /// <summary>Content tags (see ContentTags), used by castle bans.</summary>
        public string[] Tags = Array.Empty<string>();
        /// <summary>Max-health cost in displayed points; the card is only offered while it can be paid.</summary>
        public int MaxHealthCost;
        /// <summary>Castle id this card is exclusive to (null = every castle). Exclusive cards also appear in normal mode.</summary>
        public string Castle;
        /// <summary>Extra availability check at draw time (null = always), e.g. "the ascension is owned".</summary>
        public Func<bool> Available;
    }

    /// <summary>Custom level-up cards (docs/m3-card-design.md, section 4). Numbers are first drafts.</summary>
    public static class CustomCards
    {
        private static readonly List<CustomCardSpec> all = new List<CustomCardSpec>();
        public static IReadOnlyList<CustomCardSpec> All => all;

        private static readonly string[] Roman = { "I", "II", "III", "IV", "V" };

        internal static void RegisterAll()
        {
            // ---------- Treasure-related ----------
            Chain(Ids.LuckyFind, "Lucky Find", 3, null, null,
                _ => "Gain a random treasure.",
                GainRandomTreasure);

            Chain(Ids.TreasureSense, "Treasure Sense", 5, null, null,
                _ => $"Level-up card slots get +{LevelUpTreasureSlots.ChancePerTreasureSense:P0} chance to become a treasure.",
                () => { });

            Chain(Ids.Cartographer, "Cartographer", 2, null, null,
                n => $"Treasure chests offer one more treasure to choose from ({TreasureOfferUI.BaseOfferSize + n}).",
                () => { });

            Single(Ids.BloodPact, "Blood Pact", null, null,
                "Gain a Rare or better treasure. Lose 5 maximum castle health.",
                BloodPact);

            // ---------- Map spawns ----------
            Chain(Ids.OldGrowth, "Old Growth", 2, null, "Golden Forest I",
                _ => "Trees appear 50% more often on new tiles.", () => { });
            Chain(Ids.RestlessDead, "Restless Dead", 2, "Building - Haunted House", "Building - Haunted House",
                _ => "Graves appear 50% more often on new tiles.", () => { });
            Chain(Ids.ForgottenShrines, "Forgotten Shrines", 2, "Building - University", "Building - University",
                _ => "Occult shrines appear 50% more often on new tiles.", () => { });
            Chain(Ids.RichVeins, "Rich Veins", 2, "Building - Mine", "Building - Mine",
                _ => "Iron veins appear 50% more often on new tiles.", () => { });
            Chain(Ids.LeyLines, "Ley Lines", 2, "Building - Mana Siphon", "Building - Mana Siphon",
                _ => "Mana crystals appear 50% more often on new tiles.", () => { });

            // ---------- Economy & build style ----------
            Single(Ids.RoyalGrant, "Royal Grant", null, "Gold Rush!",
                "Gain 500 gold.",
                () => GiveGold(500));

            Chain(Ids.TradeRoute, "Trade Route", 3, null, "Gold Rush!",
                _ => "At the start of each wave, gain 3 × level gold.",
                () => { if (CardCounters.Get(Ids.TradeRoute) == 1) GameEvents.WaveStarted += TradeRouteIncome; });

            Chain(Ids.CompoundInterest, "Compound Interest", 2, null, "Gold Rush!",
                n => n == 1
                    ? "At the end of each wave, gain 4% interest on unspent gold (max 300 + 15 × level)."
                    : "Interest +4% more. The interest cap is doubled.",
                () => { if (CardCounters.Get(Ids.CompoundInterest) == 1) GameEvents.WaveEnded += CompoundInterest; });

            Single(Ids.Investment, "Investment", null, "Gold Rush!",
                "Invest half your gold. Five waves later, get double back.",
                Invest);

            Single(Ids.BountyHunters, "Bounty Hunters", null, "Banditry I",
                "Bosses and mini-bosses drop 10 × level bonus gold.", () => { });

            Single(Ids.LumberMill, "Lumber Mill", null, "Golden Forest I",
                "Chopping trees gives 50% more gold.", () => { });

            Single(Ids.WarBonds, "War Bonds", null, "Fortification I",
                "Whenever your castle takes damage, gain 3 × level gold per health lost.", () => { });

            Single(Ids.BulkDiscount, "Bulk Discount", null, "Gold Rush!",
                "Each additional tower of the same type raises its price 30% less.",
                BulkDiscount);

            Single(Ids.MasterMason, "Master Mason", null, "Fortification I",
                "Buying tower levels costs 20% less.", () => { });

            Single(Ids.UndividedAttention, "Undivided Attention", null, "Quality over Quantity",
                "+100% attack speed, shared equally among all your towers.",
                () => TowerManager.instance.UpdateAllTowers());

            // ---------- Skill-tree replacements ----------
            Chain(Ids.WiderHand, "Wider Hand", 2, null, "Research Breakthrough!",
                _ => "Draw one more card on level-ups.",
                () => CardManager.instance.baseCardDraw = Mathf.Min(6, CardManager.instance.baseCardDraw + 1));

            Chain(Ids.FieldRepairs, "Field Repairs", 2, null, "Fortification I",
                _ => "At the start of each wave, repair 1 castle health.",
                () => { if (CardCounters.Get(Ids.FieldRepairs) == 1) GameEvents.WaveStarted += FieldRepair; });

            // ---------- Content tags / costs (castle rules, see docs/content-rules.md) ----------
            Configure(Ids.FieldRepairs, s => s.Tags = new[] { ContentTags.CastleHealth });
            Configure(Ids.WarBonds, s => s.Tags = new[] { ContentTags.CastleHealth });
            Configure(Ids.BloodPact, s => s.MaxHealthCost = BloodPactHealthCost);
            Configure(Ids.RichVeins, s => s.Tags = new[] { ContentTags.NeedsOre });
            Configure(Ids.RestlessDead, s => s.Tags = new[] { ContentTags.NeedsGraves });

            Castles.CastleCards.RegisterAll();

            Plugin.Log.LogInfo($"Registered {all.Count} custom cards.");
        }

        private const int BloodPactHealthCost = 5;

        /// <summary>Adjusts every spec (all tiers) of a card id.</summary>
        internal static void Configure(string id, Action<CustomCardSpec> change)
        {
            foreach (CustomCardSpec s in all.Where(s => s.Id == id)) change(s);
        }

        /// <summary>Adds a castle-exclusive card chain (tiers I..N).</summary>
        internal static void CastleChain(string castle, string id, string baseTitle, int tiers, string imageFrom,
                                         Func<int, string> description, Action onPick)
        {
            int before = all.Count;
            Chain(id, baseTitle, tiers, null, imageFrom, description, onPick);
            for (int i = before; i < all.Count; i++) all[i].Castle = castle;
        }

        /// <summary>Adds a single castle-exclusive card.</summary>
        internal static void CastleSingle(string castle, string id, string title, string parent, string imageFrom, string description, Action onPick)
        {
            Single(id, title, parent, imageFrom, description, onPick);
            all[all.Count - 1].Castle = castle;
        }

        /// <summary>
        /// Adds a follow-up card chain of an ascension (More Bomblets, More Chains, ...): only drawable while the
        /// ascension treasure is owned.
        /// </summary>
        internal static void AscensionChain(string ascensionId, string id, string baseTitle, int tiers, string imageFrom,
                                            Func<int, string> description, Action onPick)
        {
            int before = all.Count;
            Chain(id, baseTitle, tiers, null, imageFrom, description, onPick);
            for (int i = before; i < all.Count; i++) all[i].Available = () => TreasureInventory.Owns(ascensionId);
        }

        // ----- registration helpers -----

        private static void Single(string id, string title, string parent, string imageFrom, string description, Action onPick)
        {
            all.Add(new CustomCardSpec { Id = id, Title = title, Parent = parent, ImageFrom = imageFrom, Description = description, OnPick = onPick });
        }

        /// <summary>Tiers I..N; tier k+1 requires tier k. <paramref name="description"/> receives the tier number (1-based).</summary>
        private static void Chain(string id, string baseTitle, int tiers, string firstParent, string imageFrom,
                                  Func<int, string> description, Action onPick)
        {
            for (int t = 1; t <= tiers; t++)
            {
                all.Add(new CustomCardSpec
                {
                    Id = id,
                    Title = $"{baseTitle} {Roman[t - 1]}",
                    Parent = t == 1 ? firstParent : $"{baseTitle} {Roman[t - 2]}",
                    ImageFrom = imageFrom,
                    Description = description(t),
                    OnPick = onPick,
                });
            }
        }

        /// <summary>Creates the UpgradeCard object for a spec on an inactive GameObject under the CardManager.</summary>
        internal static CustomCard Create(CardManager cm, CustomCardSpec spec, List<UpgradeCard> vanillaCards)
        {
            var go = new GameObject("TR_Card_" + spec.Title);
            go.SetActive(false); // UpgradeCard.Awake (PlayerPrefs lookup) never runs
            go.transform.SetParent(cm.transform, false);

            var card = go.AddComponent<CustomCard>();
            card.Id = spec.Id;
            card.OnPick = spec.OnPick;
            card.Tags = spec.Tags ?? Array.Empty<string>();
            card.MaxHealthCost = spec.MaxHealthCost;
            card.Available = spec.Available;
            card.title = spec.Title;
            card.description = spec.Description;
            card.unlockedByDefault = true;
            card.unlocked = true;
            card.unlocks = new List<UpgradeCard>();

            UpgradeCard source = spec.ImageFrom == null ? null
                : vanillaCards.FirstOrDefault(c => CardPool.TitleOf(c) == spec.ImageFrom && c.image != null);
            card.image = source != null ? source.image : TreasureIcons.Gem;
            return card;
        }

        // ----- effects -----

        private static Vector3 PopupPos => GameHelpers.CastlePosition + Vector3.up * 2f;

        private static void GiveGold(int amount)
        {
            if (amount <= 0) return;
            ResourceManager.instance.AddMoney(amount);
            GameHelpers.FloatingText(PopupPos, $"+{amount}g", "Yellow", 1.5f);
        }

        private static void GainRandomTreasure()
        {
            TreasureDef t = TreasureDraft.RollOffer(1, GameHelpers.Level).FirstOrDefault();
            if (t == null) { Plugin.Log.LogWarning("Lucky Find: treasure pool is empty."); return; }
            TreasureInventory.Grant(t);
            GameHelpers.FloatingText(PopupPos, t.Name, "Green", 1.5f, 2f);
        }

        private static void BloodPact()
        {
            TreasureDef t = TreasureDraft.RollOffer(1, GameHelpers.Level, Rarity.Rare).FirstOrDefault();
            if (t != null)
            {
                TreasureInventory.Grant(t);
                GameHelpers.FloatingText(PopupPos, t.Name, "Red", 1.5f, 2f);
            }
            HealthCost.Pay(BloodPactHealthCost);
        }

        private static void TradeRouteIncome(int level) => GiveGold(3 * level * CardCounters.Get(Ids.TradeRoute));

        private static void CompoundInterest(int level)
        {
            int n = CardCounters.Get(Ids.CompoundInterest);
            int cap = (300 + 15 * level) * (n >= 2 ? 2 : 1);
            int interest = Mathf.Min(Mathf.FloorToInt(GameHelpers.Gold * 0.04f * n), cap);
            if (interest <= 0) return;
            ResourceManager.instance.AddMoney(interest);
            GameHelpers.FloatingText(PopupPos, $"+{interest}g interest", "Yellow");
        }

        private const string InvestAmountKey = "investment.amount";
        private const string InvestDueKey = "investment.due";

        private static void Invest()
        {
            RunState run = TreasureRunMode.Run;
            int amount = GameHelpers.Gold / 2;
            ResourceManager.instance.Spend(amount);
            run.SetCounter(InvestAmountKey, amount);
            run.SetCounter(InvestDueKey, GameHelpers.Level + 5);
            GameHelpers.FloatingText(PopupPos, $"-{amount}g invested", "Grey");
            GameEvents.WaveEnded += InvestmentPayout;
        }

        private static void InvestmentPayout(int level)
        {
            RunState run = TreasureRunMode.Run;
            int amount = run.GetCounter(InvestAmountKey);
            if (amount <= 0 || level < run.GetCounter(InvestDueKey)) return;
            run.SetCounter(InvestAmountKey, 0);
            GiveGold(2 * amount);
        }

        private static void FieldRepair(int level)
        {
            GameManager.instance.RepairTower(10 * CardCounters.Get(Ids.FieldRepairs)); // 10 internal units = 1 health
        }

        private static void BulkDiscount()
        {
            foreach (TowerFlyweight fw in UnityEngine.Object.FindObjectsOfType<TowerFlyweight>())
            {
                fw.priceIncrease = Mathf.RoundToInt(fw.priceIncrease * 0.7f);
                int count = fw.towers.Count;
                fw.currentPrice = fw.basePrice + count * fw.priceIncrease;
                fw.sellPrice = fw.basePrice + (count - 1) * fw.priceIncrease;
                if (fw.myBuildButton != null) fw.myBuildButton.UpdatePriceText();
            }
        }

        /// <summary>Spawn chance multiplier for map decorations (trees, graves, shrines, iron veins, mana crystals).</summary>
        internal static float SpawnMultiplier(SpawnableObject obj)
        {
            string id;
            switch (obj)
            {
                case TreeSpawn _: id = Ids.OldGrowth; break;
                case Grave _: id = Ids.RestlessDead; break;
                case Shrine _: id = Ids.ForgottenShrines; break;
                case IronVein _: id = Ids.RichVeins; break;
                case ManaCrystal _: id = Ids.LeyLines; break;
                default: return 1f;
            }
            return 1f + 0.5f * CardCounters.Get(id);
        }
    }
}
