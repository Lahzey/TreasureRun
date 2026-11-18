using System;
using TreasureRun.Cards;
using TreasureRun.Core;
using TreasureRun.Treasures;

namespace TreasureRun.Balance.Game
{
    /// <summary>
    /// Dev overlay (setting Debug.BalanceOverlay): each offered treasure and level-up card shows its balance points
    /// under its description, so playtest picks can be compared with the numbers.
    /// </summary>
    internal static class BalanceOverlay
    {
        private static bool On => ModConfig.DebugTools.Value && ModConfig.DebugBalanceOverlay.Value;

        public static void Treasure(CardManager cm, int i, TreasureDef t)
        {
            if (!On) return;
            // Treasure texts are long: the points go into the title, "+" over budget, "-" under.
            try
            {
                ReportItem it = BalanceGame.Treasure(t);
                if (it.Spec == null) { cm.titles[i].text += " [no spec]"; return; }
                Rating r = BalanceGame.Rate(it);
                cm.titles[i].text += r.Unrated ? " [unrated]" : $" [{r.Points:0.0}{(r.OverBudget ? "+" : r.UnderBudget ? "-" : "")}]";
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Balance overlay: {e.Message}"); }
        }

        public static void Cards(CardManager cm, int count)
        {
            if (!On) return;
            for (int i = 0; i < count && i < cm.cards.Length; i++)
            {
                UpgradeCard c = cm.cards[i];
                if (c == null) continue;
                ReportItem it = c is CustomCard cc
                    ? new ReportItem { Id = cc.Id, Name = cc.title, Kind = "card", IsCard = true, Spec = ContentSpecs.Card(cc.Id, TierOf(cc.title)) }
                    : BalanceGame.Vanilla(c, CardPool.Whitelisted(c));
                Append(cm, i, it);
            }
        }

        private static void Append(CardManager cm, int i, ReportItem it)
        {
            try
            {
                string text;
                if (it.Spec == null) text = "no balance spec";
                else
                {
                    Rating r = BalanceGame.Rate(it);
                    text = r.Unrated ? "unrated" : $"{r.Points:0.0} pts" + (r.OverBudget ? ", high" : r.UnderBudget ? ", low" : "");
                }
                cm.descriptions[i].text += $"\n<color=#7a5c2e><i>[{text}]</i></color>";
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Balance overlay: {e.Message}"); }
        }

        private static int TierOf(string title)
        {
            string[] roman = { "I", "II", "III", "IV", "V" };
            string[] parts = (title ?? "").Split(' ');
            int k = Array.IndexOf(roman, parts[parts.Length - 1]);
            return k >= 0 ? k + 1 : 1;
        }
    }
}
