using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.Ascensions
{
    /// <summary>
    /// World UI for ascended towers: a title line above the tower popup and the build button label, both showing the
    /// ascended tower's name (<see cref="AscensionDef.BuiltName"/>, e.g. "Storm Coil").
    /// </summary>
    [HarmonyPatch]
    internal static class AscensionUi
    {
        private const string TitleName = "TR_AscensionTitle";

        [HarmonyPostfix, HarmonyPatch(typeof(TowerUI), nameof(TowerUI.SetStats))]
        private static void TowerUiSetStats(TowerUI __instance, Tower _myTower)
        {
            AscensionDef def = AscensionRegistry.AscensionOf(_myTower != null ? _myTower.gameObject : null);
            string title = def?.BuiltName;
            if (title == null || __instance.levelText == null) return;

            // Clone the whole "Level" panel (background + text) and put the copy right above it.
            var levelPanel = (RectTransform)__instance.levelText.transform.parent;
            Transform existing = levelPanel.parent.Find(TitleName);
            Text text;
            if (existing != null) text = existing.GetComponentInChildren<Text>(true);
            else
            {
                GameObject go = Object.Instantiate(levelPanel.gameObject, levelPanel.parent, false);
                go.name = TitleName;
                var rt = (RectTransform)go.transform;
                rt.anchoredPosition = levelPanel.anchoredPosition +
                                      new Vector2(0f, levelPanel.rect.height * levelPanel.localScale.y * 1.08f);
                foreach (Text other in go.GetComponentsInChildren<Text>(true))
                    if (other.name != __instance.levelText.name) Object.Destroy(other.gameObject);
                foreach (Selectable sel in go.GetComponentsInChildren<Selectable>(true)) sel.interactable = false;
                text = go.transform.Find(__instance.levelText.name)?.GetComponent<Text>() ?? go.GetComponentInChildren<Text>(true);
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                FitOneLine(text, title, rt.rect.width * 0.9f);
            }
            text.text = title;
            text.color = Color.Lerp(AscensionTargets.BannerColor(def.Target), Color.black, 0.25f);
        }

        /// <summary>Shrinks the font so the text fits on one line within <paramref name="width"/> (in the panel's units).</summary>
        private static void FitOneLine(Text text, string content, float width)
        {
            float scale = Mathf.Max(0.0001f, text.transform.localScale.x);
            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(10000f, 1000f));
            float preferred = text.cachedTextGeneratorForLayout.GetPreferredWidth(content, settings) / text.pixelsPerUnit * scale;
            if (preferred > width) text.fontSize = Mathf.Max(1, Mathf.FloorToInt(text.fontSize * width / preferred));
        }

        /// <summary>Gives the build button its vanilla name back (after <see cref="AscensionRegistry.Revoke"/>).</summary>
        public static void RestoreBuildButton(AscensionDef def)
        {
            BuildButtonUI button = AscensionTargets.BuildButton(def.Target);
            Text label = button != null ? button.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.text == def.BuiltName) : null;
            if (label != null) label.text = def.TargetName;
        }

        /// <summary>Renames the target's build button to the ascension (e.g. "Ballista" → "Repeater").</summary>
        public static void RenameBuildButton(AscensionDef def)
        {
            BuildButtonUI button = AscensionTargets.BuildButton(def.Target);
            if (button == null) return;
            Text label = button.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(t => t != button.priceTag && t != button.modifiersText && t.text == def.TargetName)
                ?? button.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t != button.priceTag && t != button.modifiersText);
            if (label == null) return;
            label.text = def.BuiltName;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }
}
