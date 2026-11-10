using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Castles;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.Cards
{
    /// <summary>
    /// Content tags shared by cards and treasures. Castles ban content by tag (see CastleBehaviour.BannedTags), so new
    /// content only needs the right tag to be handled by every castle. Documented in docs/content-rules.md.
    /// </summary>
    public static class ContentTags
    {
        /// <summary>More gold from killing regular monsters (boss-only bonuses do not count).</summary>
        public const string KillGold = "kill-gold";
        /// <summary>Raises, restores or protects castle health.</summary>
        public const string CastleHealth = "castle-health";
        /// <summary>Only useful with iron veins on the map (mines).</summary>
        public const string NeedsOre = "needs-ore";
        /// <summary>Only useful with graves on the map (haunted houses).</summary>
        public const string NeedsGraves = "needs-graves";

        /// <summary>Tags of vanilla cards (by class or title).</summary>
        public static IEnumerable<string> Of(UpgradeCard card)
        {
            if (card is CustomCard custom)
            {
                foreach (string t in custom.Tags) yield return t;
                yield break;
            }
            string title = CardPool.TitleOf(card);
            if (card is MaxHealthUpgradeCard || title == "Building - Mine") yield return CastleHealth;
            if (title == "Building - Mine") yield return NeedsOre;
            if (card is HauntedHouseUpgrade || title == "Building - Haunted House") yield return NeedsGraves;
            if (title.StartsWith("Banditry")) yield return KillGold;
        }
    }

    /// <summary>
    /// Card pool rules that apply in every mode (normal and Treasure Run):
    ///  - castle bans (by tag) are removed from the pool for good,
    ///  - cards whose cost cannot be paid right now (max health, see <see cref="HealthCost"/>) are hidden for one draw,
    ///  - castle-exclusive custom cards are added to the vanilla pool in normal mode (Treasure Run: CardPool.Build),
    ///  - castles can grant cards for free at the start (e.g. a building unlock).
    /// </summary>
    [HarmonyPatch]
    public static class CardRules
    {
        /// <summary>Cards granted for free at the start; never offered again (vanilla chains could re-add them).</summary>
        private static readonly HashSet<UpgradeCard> granted = new HashSet<UpgradeCard>();

        public static bool IsBanned(UpgradeCard card)
        {
            if (card != null && granted.Contains(card)) return true;
            CastleBehaviour b = CastleRuntime.Behaviour;
            if (b == null || card == null) return false;
            HashSet<string> banned = b.BannedTagSet;
            if (banned.Count > 0 && ContentTags.Of(card).Any(banned.Contains)) return true;
            return b.BansCard(CardPool.TitleOf(card));
        }

        /// <summary>Not banned and affordable right now.</summary>
        public static bool IsDrawable(UpgradeCard card) =>
            card != null && !IsBanned(card) &&
            (!(card is CustomCard c) || (HealthCost.CanPay(c.MaxHealthCost) && (c.Available == null || c.Available())));

        /// <summary>Custom card specs that belong in this game (castle-exclusive ones only for their castle).</summary>
        public static bool SpecAllowed(CustomCardSpec spec) =>
            spec.Castle == null || spec.Castle == CastleRuntime.Active?.Id;

        /// <summary>
        /// Applies a card as if it was picked in a level-up (without UI): Upgrade, follow-up cards join the pool,
        /// the card leaves it.
        /// </summary>
        public static void Grant(UpgradeCard card)
        {
            CardManager cm = CardManager.instance;
            if (TreasureRunMode.Active) CardPool.OnCardPicked(card);
            card.Upgrade();
            foreach (UpgradeCard u in card.unlocks)
                if (u != null && u.unlocked && !cm.availableCards.Contains(u)) cm.availableCards.Add(u);
            cm.availableCards.Remove(card);
            if (TreasureRunMode.Active) CardPool.CleanUpPool(cm);
        }

        // ------------------------------------------------------------------ hooks

        /// <summary>After the pool is built (Treasure Run postfix runs first): castle cards and starting grants.</summary>
        [HarmonyPostfix, HarmonyPriority(Priority.Low), HarmonyPatch(typeof(CardManager), nameof(CardManager.Start))]
        private static void CardManagerStartPostfix(CardManager __instance)
        {
            granted.Clear();
            CastleBehaviour b = CastleRuntime.Behaviour;
            if (b == null) return;

            if (!TreasureRunMode.Active) InjectCastleCards(__instance);
            __instance.availableCards.RemoveAll(IsBanned);

            List<UpgradeCard> sceneCards = Resources.FindObjectsOfTypeAll<UpgradeCard>().Where(c => c.gameObject.scene.IsValid()).ToList();
            foreach (string title in b.StartingCards)
            {
                UpgradeCard card = __instance.availableCards.FirstOrDefault(c => CardPool.TitleOf(c) == title)
                                   ?? sceneCards.FirstOrDefault(c => CardPool.TitleOf(c) == title && !(c is CustomCard));
                if (card == null) { Plugin.Log.LogWarning($"Castle starting card '{title}' not found."); continue; }
                Grant(card);
                granted.Add(card);
                Plugin.Log.LogInfo($"Castle grants '{title}' at the start.");
            }
        }

        /// <summary>Normal mode: castle-exclusive custom cards join the vanilla pool, chained like in Treasure Run.</summary>
        private static void InjectCastleCards(CardManager cm)
        {
            List<CustomCardSpec> specs = CustomCards.All.Where(s => s.Castle != null && SpecAllowed(s)).ToList();
            if (specs.Count == 0) return;
            List<UpgradeCard> sceneCards = Resources.FindObjectsOfTypeAll<UpgradeCard>().Where(c => c.gameObject.scene.IsValid()).ToList();
            var created = specs.Select(s => (spec: s, card: (UpgradeCard)CustomCards.Create(cm, s, sceneCards))).ToList();
            foreach (var (spec, card) in created)
            {
                if (spec.Parent == null) { cm.availableCards.Add(card); continue; }
                UpgradeCard parent = created.Select(c => c.card).FirstOrDefault(c => c.title == spec.Parent)
                                     ?? sceneCards.FirstOrDefault(c => CardPool.TitleOf(c) == spec.Parent);
                if (parent != null) parent.unlocks.Add(card);
                else cm.availableCards.Add(card);
            }
            Plugin.Log.LogInfo($"Castle cards added to the vanilla pool: {created.Count}.");
        }

        /// <summary>Every draw: banned cards leave the pool, unaffordable ones sit this draw out.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(CardManager), nameof(CardManager.DrawCards), typeof(int))]
        private static void DrawCardsPrefix(CardManager __instance, out List<UpgradeCard> __state)
        {
            __state = null;
            if (__instance.drawingCards) return; // queued, filtered when it actually runs
            __instance.availableCards.RemoveAll(IsBanned);
            List<UpgradeCard> hidden = __instance.availableCards.Where(c => !IsDrawable(c)).ToList();
            if (hidden.Count == 0) return;
            __instance.availableCards.RemoveAll(hidden.Contains);
            __state = hidden;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CardManager), nameof(CardManager.DrawCards), typeof(int))]
        private static void DrawCardsPostfix(CardManager __instance, List<UpgradeCard> __state)
        {
            if (__state != null) __instance.availableCards.AddRange(__state);
        }
    }

    /// <summary>
    /// Max-health costs of cards and treasures (see docs/content-rules.md):
    ///  - an option whose cost would drop max health below 1 is never offered (filtered at draw time, so a draw can
    ///    never consist only of unaffordable options),
    ///  - castles that ignore health (Glass Fortress) pay nothing.
    /// Every effect that costs max health must go through <see cref="Pay"/>.
    /// </summary>
    public static class HealthCost
    {
        public static bool Waived => CastleRuntime.Behaviour?.IgnoresHealth == true;

        /// <summary>Cost in displayed health points; 0 = no cost.</summary>
        public static bool CanPay(int cost)
        {
            if (cost <= 0 || Waived) return true;
            GameManager gm = GameManager.instance;
            return gm == null || gm.maxHealth / 10 - cost >= 1;
        }

        public static void Pay(int cost)
        {
            if (cost <= 0) return;
            GameManager gm = GameManager.instance;
            if (gm == null) return;
            if (Waived)
            {
                GameHelpers.FloatingText(GameHelpers.CastlePosition + Vector3.up * 2.5f, "No health to pay", "Grey", 1f, 1.5f);
                return;
            }
            gm.maxHealth = Mathf.Max(10, gm.maxHealth - cost * 10);
            gm.health = Mathf.Min(gm.health, gm.maxHealth);
            gm.healthLoss = Mathf.Min(gm.healthLoss, gm.maxHealth);
            gm.SetHealthBar();
        }

        /// <summary>Max-health cost of a treasure right now (0 = none).</summary>
        public static int Of(TreasureDef def) => def.MaxHealthCost?.Invoke() ?? 0;
    }
}
