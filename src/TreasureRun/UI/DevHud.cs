using System.Linq;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.UI
{
    /// <summary>Minimal IMGUI overlay showing Treasure Run state. Development aid; toggle via config.</summary>
    internal sealed class DevHud : MonoBehaviour
    {
        private GUIStyle style;

        private void OnGUI()
        {
            if (!TreasureRunMode.Active || !ModConfig.ShowDevHud.Value) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
                style.normal.textColor = new Color(1f, 0.78f, 0.25f);
            }

            RunState run = TreasureRunMode.Run;
            string treasures = string.Join(", ", run.OwnedTreasures
                .Select(TreasureRegistry.Get)
                .Where(t => t != null)
                .Select(t => $"<color={RarityInfo.Hex(RarityInfo.IconColor(t.Rarity))}>{t.Name}</color>"));

            GUI.Label(new Rect(10f, Screen.height - 52f, 1400f, 48f),
                $"Treasure Run  |  seed {run.Seed}  |  pity {run.OffersSinceEpicOrBetter}/{TreasureDraft.PityThreshold}" +
                (ModConfig.DebugTools.Value ? $"  |  {ModConfig.DebugLevelUpKey.Value}: level-up, {ModConfig.DebugOfferKey.Value}: offer,{ModConfig.DebugGrantWindowKey.Value}: grant" : "") +
                $"\n{(treasures.Length > 0 ? treasures : "no treasures yet")}", style);
        }
    }
}
