using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.UI
{
    /// <summary>
    /// Debug hotkeys during a Treasure Run (configurable, F1 ConfigurationManager):
    /// F7 = level-up card draw now, F8 = treasure offer now, F9 = window to grant any treasure (with search and filters).
    /// </summary>
    internal sealed class DebugTools : MonoBehaviour
    {
        private const string SearchControl = "TR_TreasureSearch";

        private bool showGrantWindow;
        private bool focusSearch;
        private Rect windowRect = new Rect(20f, 60f, 380f, 600f);
        private Vector2 scroll;
        private GUIStyle buttonStyle, labelStyle, descStyle, toggleStyle;

        // Filters
        private string search = "";
        private readonly bool[] rarityEnabled = { true, true, true, true, true, true };
        private bool hideOwnedUnique;
        private bool hideUnavailable;
        private bool showDescriptions = true;

        private void Update()
        {
            if (!TreasureRunMode.Active || !ModConfig.DebugTools.Value)
            {
                showGrantWindow = false;
                return;
            }

            if (Input.GetKeyDown(ModConfig.DebugLevelUpKey.Value) && CardManager.instance != null)
            {
                Plugin.Log.LogInfo("[Debug] Level-up card draw requested.");
                CardManager.instance.DrawCards(); // queued by vanilla if a draw is already open
            }
            if (Input.GetKeyDown(ModConfig.DebugOfferKey.Value))
            {
                Plugin.Log.LogInfo("[Debug] Treasure offer requested.");
                TreasureOfferUI.RequestOffer();
            }
            if (Input.GetKeyDown(ModConfig.DebugGrantWindowKey.Value))
            {
                showGrantWindow = !showGrantWindow;
                focusSearch = showGrantWindow;
            }
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
                toggleStyle = new GUIStyle(GUI.skin.toggle) { richText = true };
                descStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 11 };
                descStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            }

            // Search: matches name, id, description, tags and rarity.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(48f));
            GUI.SetNextControlName(SearchControl);
            search = GUILayout.TextField(search ?? "");
            if (GUILayout.Button("x", GUILayout.Width(22f))) search = "";
            GUILayout.EndHorizontal();
            if (focusSearch && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(SearchControl);
                focusSearch = false;
            }

            // Rarity toggles.
            GUILayout.BeginHorizontal();
            for (int r = 0; r < RarityInfo.Count; r++)
            {
                string hex = RarityInfo.Hex(RarityInfo.IconColor((Rarity)r));
                string label = ((Rarity)r).ToString().Substring(0, r == (int)Rarity.Mythical ? 4 : 3);
                rarityEnabled[r] = GUILayout.Toggle(rarityEnabled[r], $"<color={hex}>{label}</color>", toggleStyle);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            hideOwnedUnique = GUILayout.Toggle(hideOwnedUnique, "Hide owned");
            hideUnavailable = GUILayout.Toggle(hideUnavailable, "Hide unavailable");
            showDescriptions = GUILayout.Toggle(showDescriptions, "Descriptions");
            GUILayout.EndHorizontal();

            List<TreasureDef> visible = TreasureRegistry.All.Where(Matches).ToList();
            GUILayout.Label($"{visible.Count} of {TreasureRegistry.All.Count} treasures  |  Mythical weight: {ModConfig.DebugMythicalWeight.Value}");

            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var group in visible.GroupBy(t => t.Rarity).OrderBy(g => g.Key))
            {
                string hex = RarityInfo.Hex(RarityInfo.IconColor(group.Key));
                GUILayout.Label($"<color={hex}><b>{group.Key}</b></color>", labelStyle);
                foreach (TreasureDef t in group.OrderBy(t => t.Name))
                {
                    int owned = TreasureInventory.CountOf(t.Id);
                    string suffix = owned > 0 ? $"  (owned{(owned > 1 ? " x" + owned : "")})" : "";
                    if (!IsAvailable(t)) suffix += "  <i>(not offered now)</i>";
                    if (GUILayout.Button($"<color={hex}>{t.Name}</color>{suffix}", buttonStyle))
                    {
                        if (t.Unique && owned > 0) Plugin.Log.LogWarning($"[Debug] {t.Name} is unique and already owned.");
                        else TreasureInventory.Grant(t);
                    }
                    if (showDescriptions) GUILayout.Label(t.Description, descStyle);
                }
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close")) showGrantWindow = false;
            GUI.DragWindow();
        }

        private bool Matches(TreasureDef t)
        {
            if (!rarityEnabled[(int)t.Rarity]) return false;
            if (hideOwnedUnique && t.Unique && TreasureInventory.Owns(t.Id)) return false;
            if (hideUnavailable && !IsAvailable(t)) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;

            // Every word must match somewhere (name, id, description, tags, rarity).
            string haystack = $"{t.Name} {t.Id} {t.Description} {string.Join(" ", t.Tags)} {t.Rarity}";
            return search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .All(word => haystack.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsAvailable(TreasureDef t)
        {
            RunState run = TreasureRunMode.Run;
            return run == null || t.IsAvailable == null || t.IsAvailable(run);
        }
    }
}
