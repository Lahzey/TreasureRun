using System;
using BepInEx.Configuration;
using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.UI
{
    /// <summary>
    /// Adds mod settings as extra toggle rows to the vanilla Options panel (main menu and in-game pause menu).
    /// A row is a clone of the last vanilla toggle ("Projectile D.O.T. FX"). The panel grows by one row: toggles are
    /// anchored to its top and the sliders/Back button to its bottom, so the new row fits between them.
    /// </summary>
    [HarmonyPatch]
    internal static class OptionsMenuExtras
    {
        [HarmonyPostfix, HarmonyPatch(typeof(OptionsMenu), nameof(OptionsMenu.Start))]
        private static void StartPostfix(OptionsMenu __instance)
        {
            try
            {
                Toggle template = __instance.projectileFXToggle;
                if (template == null) return;
                AddToggleRow(template, __instance.showConditionToggle, "Loading animation", ModConfig.LoadingAnimation);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Options menu: could not add mod settings: " + e);
            }
        }

        private static void AddToggleRow(Toggle template, Toggle previous, string label, ConfigEntry<bool> entry)
        {
            var templateRect = (RectTransform)template.transform;
            var panel = (RectTransform)templateRect.parent;
            float step = previous != null
                ? Mathf.Abs(((RectTransform)previous.transform).anchoredPosition.y - templateRect.anchoredPosition.y)
                : templateRect.sizeDelta.y + 3f;

            // Grow the panel downwards by one row, keeping its top edge in place.
            panel.sizeDelta += new Vector2(0f, step);
            panel.anchoredPosition -= new Vector2(0f, step * (1f - panel.pivot.y));

            GameObject row = UnityEngine.Object.Instantiate(template.gameObject, panel, false);
            row.name = "TreasureRun_" + entry.Definition.Key;
            var rowRect = (RectTransform)row.transform;
            rowRect.anchoredPosition = templateRect.anchoredPosition - new Vector2(0f, step);

            Text text = row.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;

            Toggle toggle = row.GetComponent<Toggle>();
            toggle.onValueChanged = new Toggle.ToggleEvent(); // drop the cloned vanilla listener (ToggleProjectileFX)
            toggle.SetIsOnWithoutNotify(entry.Value);
            toggle.onValueChanged.AddListener(on =>
            {
                if (entry.Value == on) return;
                entry.Value = on;
                if (SFXManager.instance != null) SFXManager.instance.ButtonClick();
            });

            // Keep the row in sync when the setting is changed elsewhere (ConfigurationManager, config file reload).
            EventHandler sync = null;
            sync = (s, e) =>
            {
                if (toggle == null) { entry.SettingChanged -= sync; return; }
                toggle.SetIsOnWithoutNotify(entry.Value);
            };
            entry.SettingChanged += sync;
        }
    }
}
