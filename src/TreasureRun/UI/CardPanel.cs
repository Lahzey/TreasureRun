using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.UI
{
    /// <summary>
    /// Helpers for the vanilla card panel (CardManager's CardUI with 6 card holders):
    /// treasure styling of individual card slots, restoring the vanilla look, and closing a pick.
    /// </summary>
    internal static class CardPanel
    {
        private static Color[] origTitleColor, origImageColor, origCardColor;

        internal static void Initialize()
        {
            TreasureRunMode.RunStarted += _ => origTitleColor = null;
            TreasureRunMode.RunEnded += _ => origTitleColor = null;
        }

        /// <summary>Capture the pristine vanilla card colors. Call while nothing is styled (CardManager.Start).</summary>
        public static void Capture(CardManager cm)
        {
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

        /// <summary>Restore vanilla card styling on all slots.</summary>
        public static void Restore(CardManager cm)
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

        /// <summary>Show a treasure in card slot <paramref name="i"/>.</summary>
        public static void StyleAsTreasure(CardManager cm, int i, TreasureDef t)
        {
            if (origTitleColor == null) Capture(cm);
            Color text = RarityInfo.TextColor(t.Rarity);

            cm.titles[i].text = t.Name;
            cm.titles[i].color = text;
            cm.images[i].sprite = TreasureIcons.Gem;
            cm.images[i].color = RarityInfo.IconColor(t.Rarity);
            cm.descriptions[i].text = $"<color={RarityInfo.Hex(text)}><b>{t.Rarity} Treasure</b></color>\n{t.Description}";

            Image card = CardBackground(cm, i);
            if (card != null) card.color = Color.Lerp(Color.white, RarityInfo.IconColor(t.Rarity), 0.3f);
        }

        /// <summary>
        /// Close a pick: show the next queued treasure offer, else mirror the tail of vanilla
        /// CardManager.ActivateCard (continue queued card draws, monster draws, or close the panel).
        /// </summary>
        public static void FinishPick(CardManager cm)
        {
            Restore(cm);
            cm.drawingCards = false;

            if (TreasureOfferUI.TryShowNext()) return;

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
    }
}
