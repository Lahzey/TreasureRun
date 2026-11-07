using System.Collections.Generic;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.UI
{
    /// <summary>
    /// Shows "pick 1 of N" treasure offers in the vanilla card panel (CardManager's CardUI).
    /// Integrates with CardManager's queueing: an offer waits while cards are being drawn, and vice versa.
    /// The vanilla card select buttons call CardManager.ActivateCard(i), which CardManagerPatches routes here.
    /// </summary>
    internal static class TreasureOfferUI
    {
        public const int DefaultOfferSize = 3;

        private static readonly Queue<int> pendingOffers = new Queue<int>();
        private static List<TreasureDef> current;

        // Original card styles, captured once so vanilla card draws look untouched.
        private static Color[] origTitleColor, origImageColor, origCardColor;

        public static bool IsShowing => current != null;

        internal static void Initialize()
        {
            TreasureRunMode.RunStarted += _ => ResetState();
            TreasureRunMode.RunEnded += _ => ResetState();
        }

        private static void ResetState()
        {
            pendingOffers.Clear();
            current = null;
            origTitleColor = null; // scene objects are recreated on every GameScene load
        }

        /// <summary>Queue a treasure offer. Shown immediately unless a card or treasure draw is open.</summary>
        public static void RequestOffer(int size = DefaultOfferSize)
        {
            if (!TreasureRunMode.Active || CardManager.instance == null) return;
            pendingOffers.Enqueue(size);
            TryShowNext();
        }

        /// <summary>Shows the next queued offer if the card panel is free. Returns true if an offer is now showing.</summary>
        public static bool TryShowNext()
        {
            CardManager cm = CardManager.instance;
            if (cm == null || cm.drawingCards || current != null) return current != null;

            while (pendingOffers.Count > 0)
            {
                int size = pendingOffers.Dequeue();
                var offer = TreasureDraft.RollOffer(Mathf.Clamp(size, 1, 6), SpawnManager.instance.level);
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
            CaptureOriginalStyles(cm);
            current = offer;

            cm.ArrangeCards(offer.Count);
            SFXManager.instance.PlaySound(Sound.Cards, MusicManager.instance.transform.position, MusicManager.instance.transform);
            SpawnManager.instance.ShowSpawnUIs(state: false);
            cm.ui.SetActive(true);
            cm.drawingCards = true;

            for (int i = 0; i < offer.Count; i++)
            {
                TreasureDef t = offer[i];
                Color text = RarityInfo.TextColor(t.Rarity);

                cm.titles[i].text = t.Name;
                cm.titles[i].color = text;
                cm.images[i].sprite = TreasureIcons.Gem;
                cm.images[i].color = RarityInfo.IconColor(t.Rarity);
                cm.descriptions[i].text = $"<color={RarityInfo.Hex(text)}><b>{t.Rarity}</b></color>\n{t.Description}";

                Image card = CardBackground(cm, i);
                if (card != null) card.color = Color.Lerp(Color.white, RarityInfo.IconColor(t.Rarity), 0.3f);
            }
        }

        /// <summary>Called from the ActivateCard prefix while an offer is showing.</summary>
        public static void Choose(int index)
        {
            CardManager cm = CardManager.instance;
            List<TreasureDef> offer = current;
            current = null;

            if (offer != null && index >= 0 && index < offer.Count)
                TreasureInventory.Grant(offer[index]);

            RestoreOriginalStyles(cm);
            cm.drawingCards = false;

            if (TryShowNext()) return;

            // Mirror the tail of vanilla CardManager.ActivateCard: continue queued card draws or close the panel.
            if (cm.queuedDraws.Count > 0)
            {
                int numb = cm.queuedDraws[0];
                cm.queuedDraws.RemoveAt(0);
                cm.DrawCards(numb);
                return;
            }
            if (cm.monsterDrawCallQueued > 0)
            {
                cm.ui.SetActive(false);
                cm.DrawMonsterCards();
                return;
            }
            if (!SpawnManager.instance.combat) SpawnManager.instance.ShowSpawnUIs(state: true);
            cm.ui.SetActive(false);
            cm.monsterCardUI.SetActive(false);
        }

        private static Image CardBackground(CardManager cm, int i)
        {
            Transform parent = cm.titles[i].transform.parent; // "CardN" (button with the card sprite)
            return parent != null ? parent.GetComponent<Image>() : null;
        }

        private static void CaptureOriginalStyles(CardManager cm)
        {
            if (origTitleColor != null) return;
            int n = cm.titles.Length;
            origTitleColor = new Color[n];
            origImageColor = new Color[n];
            origCardColor = new Color[n];
            for (int i = 0; i < n; i++)
            {
                origTitleColor[i] = cm.titles[i].color;
                origImageColor[i] = cm.images[i].color;
                Image card = CardBackground(cm, i);
                origCardColor[i] = card != null ? card.color : Color.white;
            }
        }

        /// <summary>Restores vanilla card styling (also called before every vanilla DisplayCards).</summary>
        public static void RestoreOriginalStyles(CardManager cm)
        {
            if (origTitleColor == null || cm == null) return;
            for (int i = 0; i < origTitleColor.Length; i++)
            {
                cm.titles[i].color = origTitleColor[i];
                cm.images[i].color = origImageColor[i];
                Image card = CardBackground(cm, i);
                if (card != null) card.color = origCardColor[i];
            }
        }
    }
}
