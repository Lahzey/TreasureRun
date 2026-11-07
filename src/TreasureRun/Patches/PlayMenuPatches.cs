using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.Patches
{
    /// <summary>
    /// Adds a "Treasure Run" toggle button to the PlayMenu, centered above the lane-count buttons.
    /// It is a clone of the vanilla Back button. When on, it takes the look of the
    /// "Challenge Modes" panel (UI9SliceOrange sprite) with golden text.
    /// </summary>
    [HarmonyPatch]
    internal static class PlayMenuPatches
    {
        private const string ButtonObjectName = "TreasureRunMode";
        private const float GapAboveLaneButtons = 30f; // leaves room for the lane button titles
        private static readonly Color OnTextColor = new Color(1f, 0.84f, 0.3f);
        private static readonly Color OnOutlineColor = new Color(0.3f, 0.18f, 0.05f, 0.9f);

        [HarmonyPostfix, HarmonyPatch(typeof(PlayMenu), nameof(PlayMenu.Start))]
        private static void PlayMenuStartPostfix(PlayMenu __instance)
        {
            try
            {
                AddButton(__instance);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Failed to add Treasure Run button: {e}");
            }
        }

        private static void AddButton(PlayMenu menu)
        {
            // Hierarchy: PlayMenu/Scaler/{ChallengePanel, BackButton, Single, Double, ...}
            Transform challengePanel = menu.monsterModeToggle.transform.parent;
            Transform scaler = challengePanel.parent;
            if (scaler.Find(ButtonObjectName) != null) return;

            Transform backButton = scaler.Find("BackButton");
            if (backButton == null)
            {
                Plugin.Log.LogError("PlayMenu BackButton not found; Treasure Run button not added.");
                return;
            }

            GameObject go = Object.Instantiate(backButton.gameObject, scaler, false);
            go.name = ButtonObjectName;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, LaneButtonsTop(scaler) + GapAboveLaneButtons);

            var toggle = go.AddComponent<TreasureRunToggleButton>();
            toggle.Init(go.GetComponent<Button>(), go.GetComponent<Image>(), go.GetComponentInChildren<Text>(true),
                        challengePanel.GetComponent<Image>());

            Plugin.Log.LogInfo($"Treasure Run button added (selected: {TreasureRunMode.Selected}).");
        }

        /// <summary>Top edge of the lane-count buttons in Scaler-local coordinates (fallback: vanilla value 150).</summary>
        private static float LaneButtonsTop(Transform scaler)
        {
            var single = scaler.Find("Single") as RectTransform;
            if (single == null) return 150f;

            var corners = new Vector3[4];
            single.GetWorldCorners(corners);
            return scaler.InverseTransformPoint(corners[1]).y; // corners[1] = top-left
        }

        /// <summary>Button behaving as an on/off toggle bound to <see cref="TreasureRunMode.Selected"/>.</summary>
        internal sealed class TreasureRunToggleButton : MonoBehaviour
        {
            private Image image;
            private Text label;
            private Outline outline;

            private Sprite offSprite;
            private float offPpum;
            private Color offTextColor;
            private FontStyle offFontStyle;

            private Sprite onSprite;
            private float onPpum;

            public void Init(Button button, Image buttonImage, Text buttonLabel, Image panelImage)
            {
                image = buttonImage;
                label = buttonLabel;

                offSprite = image.sprite;
                offPpum = image.pixelsPerUnitMultiplier;
                offTextColor = label.color;
                offFontStyle = label.fontStyle;

                onSprite = panelImage != null ? panelImage.sprite : offSprite;
                onPpum = panelImage != null ? panelImage.pixelsPerUnitMultiplier : offPpum;

                label.text = "Treasure Run";
                outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = OnOutlineColor;
                // Vanilla button texts use a large font scaled down; keep the outline ~1.5 button units thick.
                float textScale = Mathf.Max(0.0001f, Mathf.Abs(label.transform.localScale.x));
                outline.effectDistance = new Vector2(1.5f, -1.5f) / textScale;

                // Drop the cloned persistent listener (it would navigate back).
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(OnClick);

                Apply(TreasureRunMode.Selected);
            }

            private void OnClick()
            {
                bool on = !TreasureRunMode.Selected;
                TreasureRunMode.Selected = on;
                Apply(on);
                if (SFXManager.instance != null) SFXManager.instance.ButtonClick();
            }

            private void Apply(bool on)
            {
                image.sprite = on ? onSprite : offSprite;
                image.pixelsPerUnitMultiplier = on ? onPpum : offPpum;
                label.color = on ? OnTextColor : offTextColor;
                label.fontStyle = on ? FontStyle.Bold : offFontStyle;
                outline.enabled = on;
            }
        }
    }
}
