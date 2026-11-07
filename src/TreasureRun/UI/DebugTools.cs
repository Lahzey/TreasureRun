using System.Linq;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.UI
{
    /// <summary>
    /// Debug hotkeys during a Treasure Run (configurable, F1 ConfigurationManager):
    /// F8 = show a treasure offer now, F9 = window to grant any treasure.
    /// </summary>
    internal sealed class DebugTools : MonoBehaviour
    {
        private bool showGrantWindow;
        private Rect windowRect = new Rect(20f, 60f, 340f, 520f);
        private Vector2 scroll;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;

        private void Update()
        {
            if (!TreasureRunMode.Active || !ModConfig.DebugTools.Value)
            {
                showGrantWindow = false;
                return;
            }

            if (Input.GetKeyDown(ModConfig.DebugOfferKey.Value))
            {
                Plugin.Log.LogInfo("[Debug] Treasure offer requested.");
                TreasureOfferUI.RequestOffer();
            }
            if (Input.GetKeyDown(ModConfig.DebugGrantWindowKey.Value))
                showGrantWindow = !showGrantWindow;
        }

        private void OnGUI()
        {
            if (!showGrantWindow) return;
            windowRect = GUILayout.Window(0x7E5A, windowRect, DrawWindow, "Treasure Run: grant treasure");
        }

        private void DrawWindow(int id)
        {
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, richText = true };
                labelStyle = new GUIStyle(GUI.skin.label) { richText = true };
            }

            GUILayout.Label($"Mythical draft weight (config): {ModConfig.DebugMythicalWeight.Value}");
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var group in TreasureRegistry.All.GroupBy(t => t.Rarity).OrderBy(g => g.Key))
            {
                string hex = RarityInfo.Hex(RarityInfo.IconColor(group.Key));
                GUILayout.Label($"<color={hex}><b>{group.Key}</b></color>", labelStyle);
                foreach (TreasureDef t in group)
                {
                    int owned = TreasureInventory.CountOf(t.Id);
                    string suffix = owned > 0 ? $"  (owned{(owned > 1 ? " x" + owned : "")})" : "";
                    if (GUILayout.Button($"<color={hex}>{t.Name}</color>{suffix}", buttonStyle))
                    {
                        if (t.Unique && owned > 0) Plugin.Log.LogWarning($"[Debug] {t.Name} is unique and already owned.");
                        else TreasureInventory.Grant(t);
                    }
                }
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close")) showGrantWindow = false;
            GUI.DragWindow();
        }
    }
}
