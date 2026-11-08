using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Cards
{
    /// <summary>
    /// Builds the Treasure Run level-up card pool (see docs/m3-card-design.md):
    ///  - only whitelisted vanilla cards plus custom cards; the skill tree and UpgradeCard.unlocked are ignored,
    ///  - de-chaining: a card's effective parents are its nearest whitelisted ancestors in the vanilla chain;
    ///    with none it is available from the start. Tower-specific cards thereby stay gated on their tower unlock card,
    ///  - Monster Mode's monster card pool is left untouched.
    /// Chains are implemented by rewriting each pooled card's vanilla <c>unlocks</c> list, so CardManager.ActivateCard
    /// keeps doing the work.
    /// </summary>
    public static class CardPool
    {
        /// <summary>Card classes whose every (normal-pool) card is whitelisted.</summary>
        private static readonly string[] WhitelistedClasses =
        {
            nameof(TowerUnlockCard), nameof(ManaGenUpgradeCard), nameof(MaxHealthUpgradeCard),
            "UniversityUpgrade", nameof(HauntedHouseUpgrade), nameof(TreeUpgradeCard),
        };

        /// <summary>Individually whitelisted vanilla cards: title and number of copies.</summary>
        private static readonly (string title, int count)[] WhitelistedTitles =
        {
            // MonsterUpgradeCard class, but part of the normal pool (not Monster Mode)
            ("Banditry I", 1), ("Banditry II", 1), ("Banditry III", 1), ("Gold Rush!", 1),
            ("Life Steal I", 1), ("Life Steal II", 1), ("Life Steal III", 1),
            ("Absolute Zero", 2), ("Watch your step!", 2),
            // Elements
            ("Everything Bleeds", 1), ("Everything Burns", 1), ("Pandemic", 1), ("Make them Suffer", 1),
            ("Creeping Cough", 1), ("Slow Cooker I", 1), ("Slow Cooker II", 1), ("Slow Cooker III", 1),
            ("Eviscerate", 1), ("Exsanguinate I", 1), ("Exsanguinate II", 1), ("Ignite I", 1), ("Ignite II", 1),
            ("Expunge I", 1), ("Expunge II", 1),
            ("Trail of Blood I", 1), ("Fire and Flames I", 1), ("Vile Consumption I", 1), ("Ice Breaker I", 1),
            // Tower-specific
            ("Ricochet", 1), ("Collateral I", 1), ("Collateral II", 1), ("Collateral III", 1),
            ("Focus Fire", 1), ("Mana Cycling I", 1),
            ("Longbow I", 1), ("Longbow II", 1), ("Longbow III", 1), ("Mana Bolts I", 1), ("Mana Bolts II", 1), ("Freezing Bolts", 1),
            ("Mana Bombs I", 1), ("Area of Effect I", 1), ("Deep Freeze I", 1), ("Tower of Blood", 1),
            ("Slaughter I", 1), ("Dismantle I", 1), ("Desecrate I", 1),
            // Global
            ("Quality over Quantity", 1), ("Research Breakthrough!", 1),
        };

        /// <summary>Explicit parents replacing the computed ones (vanilla leaves Savings Account III unlinked).</summary>
        private static readonly Dictionary<string, string[]> ParentOverrides = new Dictionary<string, string[]>
        {
            { "Savings Account III", new[] { "Savings Account II" } },
        };

        private sealed class Entry
        {
            public UpgradeCard Card;
            public string Title;
            public List<string> ParentTitles = new List<string>();
            public bool Start;
        }

        private static readonly HashSet<UpgradeCard> picked = new HashSet<UpgradeCard>();
        private static readonly HashSet<UpgradeCard> pooled = new HashSet<UpgradeCard>();

        public static int Size => pooled.Count;

        internal static string TitleOf(UpgradeCard c) => (c.title ?? "").Trim();

        public static void Build(CardManager cm)
        {
            picked.Clear();
            pooled.Clear();

            List<UpgradeCard> sceneCards = Resources.FindObjectsOfTypeAll<UpgradeCard>()
                .Where(c => c.gameObject.scene.IsValid() && !(c is CustomCard))
                .ToList();

            // Monster Mode pool: everything reachable from availableMonsterCards stays out of our hands.
            var monster = new HashSet<UpgradeCard>();
            var stack = new Stack<UpgradeCard>(cm.availableMonsterCards.Where(c => c != null));
            while (stack.Count > 0)
            {
                UpgradeCard c = stack.Pop();
                if (!monster.Add(c)) continue;
                foreach (UpgradeCard u in c.unlocks) if (u != null) stack.Push(u);
            }
            List<UpgradeCard> normal = sceneCards.Where(c => !monster.Contains(c)).ToList();

            // Vanilla parent links among normal cards.
            var parents = normal.ToDictionary(c => c, c => new List<UpgradeCard>());
            foreach (UpgradeCard c in normal)
                foreach (UpgradeCard u in c.unlocks)
                    if (u != null && parents.ContainsKey(u)) parents[u].Add(c);

            // Whitelisted titles (title-level; used for ancestor search).
            var whitelistedTitles = new HashSet<string>(WhitelistedTitles.Select(w => w.title));
            foreach (UpgradeCard c in normal)
                if (WhitelistedClasses.Contains(c.GetType().Name)) whitelistedTitles.Add(TitleOf(c));

            var entries = new List<Entry>();

            // Class-whitelisted: every copy.
            foreach (UpgradeCard c in normal.Where(c => WhitelistedClasses.Contains(c.GetType().Name)))
                entries.Add(MakeEntry(c, parents, whitelistedTitles));

            // Title-whitelisted: N copies, preferring copies gated behind a whitelisted parent.
            foreach (var (title, count) in WhitelistedTitles)
            {
                var copies = normal.Where(c => TitleOf(c) == title && !WhitelistedClasses.Contains(c.GetType().Name))
                    .Select(c => MakeEntry(c, parents, whitelistedTitles))
                    .OrderBy(e => e.Start ? 1 : 0)
                    .Take(count)
                    .ToList();
                if (copies.Count < count)
                    Plugin.Log.LogWarning($"Card whitelist: found {copies.Count}/{count} copies of '{title}'.");
                entries.AddRange(copies);
            }

            // Custom cards.
            foreach (CustomCardSpec spec in CustomCards.All)
                entries.Add(new Entry
                {
                    Card = CustomCards.Create(cm, spec, sceneCards),
                    Title = spec.Title,
                    ParentTitles = spec.Parent != null ? new List<string> { spec.Parent } : new List<string>(),
                    Start = spec.Parent == null,
                });

            foreach (Entry e in entries)
                if (ParentOverrides.TryGetValue(e.Title, out string[] overrideParents))
                {
                    e.ParentTitles = overrideParents.ToList();
                    e.Start = false;
                }

            // Rewrite chains: children keyed by parent title.
            var childrenByTitle = new Dictionary<string, List<UpgradeCard>>();
            foreach (Entry e in entries)
                foreach (string p in e.ParentTitles.Distinct())
                {
                    if (!childrenByTitle.TryGetValue(p, out var list)) childrenByTitle[p] = list = new List<UpgradeCard>();
                    list.Add(e.Card);
                }

            foreach (Entry e in entries)
            {
                e.Card.unlocked = true;
                e.Card.unlocks = childrenByTitle.TryGetValue(e.Title, out var kids)
                    ? kids.Where(k => k != e.Card).ToList()
                    : new List<UpgradeCard>();
                pooled.Add(e.Card);
            }

            cm.availableCards = entries.Where(e => e.Start).Select(e => e.Card).Distinct().ToList();

            Plugin.Log.LogInfo($"Treasure Run card pool: {entries.Count} cards ({entries.Count(e => e.Card is CustomCard)} custom), " +
                               $"{cm.availableCards.Count} available at start, draw size {cm.baseCardDraw}.");
            if (ModConfig.DebugTools.Value)
                foreach (Entry e in entries.OrderBy(e => e.Start ? 0 : 1).ThenBy(e => e.Title))
                    Plugin.Log.LogInfo($"  [pool] {e.Title,-30} {(e.Start ? "start" : "after " + string.Join(" | ", e.ParentTitles))}");
        }

        private static Entry MakeEntry(UpgradeCard card, Dictionary<UpgradeCard, List<UpgradeCard>> parents, HashSet<string> whitelisted)
        {
            var entry = new Entry { Card = card, Title = TitleOf(card) };
            var found = new HashSet<string>();
            bool reachesRoot = false;

            var visited = new HashSet<UpgradeCard> { card };
            var queue = new Queue<UpgradeCard>();
            queue.Enqueue(card);
            while (queue.Count > 0)
            {
                UpgradeCard node = queue.Dequeue();
                List<UpgradeCard> ps = parents.TryGetValue(node, out var l) ? l : new List<UpgradeCard>();
                if (ps.Count == 0) { reachesRoot = true; continue; }
                foreach (UpgradeCard p in ps)
                {
                    string pt = TitleOf(p);
                    if (pt != entry.Title && whitelisted.Contains(pt)) { found.Add(pt); continue; }
                    if (visited.Add(p)) queue.Enqueue(p);
                }
            }

            entry.ParentTitles = found.ToList();
            entry.Start = reachesRoot || found.Count == 0;
            if (entry.Start) entry.ParentTitles.Clear();
            return entry;
        }

        /// <summary>Called before vanilla ActivateCard processes a card pick.</summary>
        public static void OnCardPicked(UpgradeCard card)
        {
            if (card != null) picked.Add(card);
        }

        /// <summary>After a vanilla pick: drop duplicates (multi-parent cards) and already picked cards.</summary>
        public static void CleanUpPool(CardManager cm)
        {
            cm.availableCards = cm.availableCards
                .Where(c => c != null && !picked.Contains(c))
                .Distinct()
                .ToList();
        }
    }
}
