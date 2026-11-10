using System.Collections.Generic;
using TreasureRun.Cards;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.UI
{
    /// <summary>
    /// Shows "pick 1 of N" treasure offers (from chests) in the vanilla card panel.
    /// Integrates with CardManager's queueing: an offer waits while cards are being drawn, and vice versa.
    /// The vanilla card select buttons call CardManager.ActivateCard(i), which CardAndChestPatches routes here.
    /// </summary>
    internal static class TreasureOfferUI
    {
        public const int BaseOfferSize = 3;

        private static readonly Queue<int> pendingOffers = new Queue<int>();
        private static readonly Queue<List<TreasureDef>> pendingFixed = new Queue<List<TreasureDef>>();
        private static List<TreasureDef> current;

        public static bool IsShowing => current != null;

        /// <summary>Offer size including Cartographer cards.</summary>
        public static int OfferSize => Mathf.Clamp(BaseOfferSize + CardCounters.Get(CustomCardIds.Cartographer), 1, 6);

        internal static void Initialize()
        {
            TreasureRunMode.RunStarted += _ => ResetState();
            TreasureRunMode.RunEnded += _ => ResetState();
        }

        private static void ResetState()
        {
            pendingOffers.Clear();
            pendingFixed.Clear();
            current = null;
        }

        /// <summary>Queue a treasure offer (size &lt;= 0: default size). Shown immediately unless the card panel is busy.</summary>
        public static void RequestOffer(int size = 0)
        {
            if (!TreasureRunMode.Active || CardManager.instance == null) return;
            pendingOffers.Enqueue(size);
            TryShowNext();
        }

        /// <summary>Queues an offer of exactly these treasures (dev console; no draft, no pity).</summary>
        public static void RequestFixed(List<TreasureDef> treasures)
        {
            if (!TreasureRunMode.Active || CardManager.instance == null || treasures.Count == 0) return;
            pendingFixed.Enqueue(treasures.GetRange(0, Mathf.Min(6, treasures.Count)));
            TryShowNext();
        }

        /// <summary>Shows the next queued offer if the card panel is free. Returns true if an offer is now showing.</summary>
        public static bool TryShowNext()
        {
            CardManager cm = CardManager.instance;
            if (cm == null || cm.drawingCards || current != null) return current != null;

            if (pendingFixed.Count > 0)
            {
                Show(cm, pendingFixed.Dequeue());
                return true;
            }

            while (pendingOffers.Count > 0)
            {
                int size = pendingOffers.Dequeue();
                if (size <= 0) size = OfferSize;
                var offer = TreasureDraft.RollOffer(Mathf.Clamp(size, 1, 6), GameHelpers.Level);
                if (offer.Count == 0)
                {
                    Plugin.Log.LogWarning("Treasure offer skipped: no treasures left in the pool.");
                    continue;
                }
                Show(cm, offer);
                return true;
            }
            return false;
        }

        private static void Show(CardManager cm, List<TreasureDef> offer)
        {
            current = offer;
            LevelUpTreasureSlots.Clear();

            cm.ArrangeCards(offer.Count);
            SFXManager.instance.PlaySound(Sound.Cards, MusicManager.instance.transform.position, MusicManager.instance.transform);
            SpawnManager.instance.ShowSpawnUIs(state: false);
            cm.ui.SetActive(true);
            cm.drawingCards = true;

            for (int i = 0; i < offer.Count; i++) CardPanel.StyleAsTreasure(cm, i, offer[i]);
        }

        /// <summary>Called from the ActivateCard prefix while an offer is showing.</summary>
        public static void Choose(int index)
        {
            List<TreasureDef> offer = current;
            current = null;

            if (offer != null && index >= 0 && index < offer.Count)
                TreasureInventory.Grant(offer[index]);

            CardPanel.FinishPick(CardManager.instance);
        }
    }
}
