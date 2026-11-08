using System.Collections.Generic;
using HarmonyLib;
using TreasureRun.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Glass Fortress: no health. Any monster that reaches it ends the run. Health is ignored completely (max-health
    /// gains do nothing, see CastlePatches), content that raises or protects health is banned, and max-health costs
    /// of cards and treasures are free (HealthCost). The HUD shows a skull instead of the health bar.
    /// </summary>
    [HarmonyPatch]
    internal sealed class GlassFortressBehaviour : CastleBehaviour
    {
        public override bool IgnoresHealth => true;
        public override IEnumerable<string> BannedTags => new[] { ContentTags.CastleHealth };

        public override void OnGameStart()
        {
            GameManager gm = GameManager.instance;
            gm.health = gm.maxHealth;
            gm.healthLoss = gm.maxHealth;
            gm.SetHealthBar();
            ShowSkull(gm);
        }

        public override string InfoText() => "Leaks allowed: none.";

        private static void ShowSkull(GameManager gm)
        {
            foreach (Image bar in new[] { gm.healthBar, gm.healthLossBar, gm.maskBar })
                if (bar != null) bar.enabled = false;
            Text text = gm.healthText;
            if (text == null) return;
            text.enabled = false; // vanilla keeps rewriting it; our row replaces it

            // "Tower:" + skull, centred where the health text was; the layout group sizes both parts.
            var row = new GameObject("TR_SkullRow", typeof(RectTransform));
            row.transform.SetParent(text.transform, false);
            var rowRt = (RectTransform)row.transform;
            // The vanilla text box is small and its text overflows it, so the row sizes itself around the centre.
            rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = new Vector2(0.5f, 0.5f);
            var fitter = row.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = text.fontSize * 0.25f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            label.transform.SetParent(row.transform, false);
            label.font = text.font;
            label.fontSize = text.fontSize;
            label.fontStyle = text.fontStyle;
            label.color = text.color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = "Tower:";

            var skull = new GameObject("TR_Skull", typeof(RectTransform)).AddComponent<Image>();
            skull.transform.SetParent(row.transform, false);
            skull.sprite = SkullSprite();
            skull.preserveAspect = true;
            skull.raycastTarget = false;
            var size = skull.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = text.fontSize * 1.2f;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GameManager), nameof(GameManager.UpdateHealthText))]
        private static void UpdateHealthTextPostfix(GameManager __instance)
        {
            if (CastleRuntime.Behaviour is GlassFortressBehaviour && __instance.healthText != null) __instance.healthText.enabled = false;
        }

        private static Sprite skullSprite;

        /// <summary>12x12 pixel-art skull with a dark outline.</summary>
        private static Sprite SkullSprite()
        {
            if (skullSprite != null) return skullSprite;
            string[] rows =
            {
                "...XXXXXX...",
                ".XXXXXXXXXX.",
                "XXXXXXXXXXXX",
                "XXXXXXXXXXXX",
                "XX..XXXX..XX",
                "X...XXXX...X",
                "XX..XXXX..XX",
                "XXXXX..XXXXX",
                ".XXXX..XXXX.",
                "..XXXXXXXX..",
                "..X.X..X.X..",
                "..XXXXXXXX..",
            };
            const int n = 12, pad = 1, size = n + 2 * pad;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var white = new Color(0.95f, 0.93f, 0.88f);
            var outline = new Color(0.1f, 0.08f, 0.08f);
            bool Filled(int x, int y) => x >= 0 && y >= 0 && x < n && y < n && rows[n - 1 - y][x] == 'X';
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    int sx = x - pad, sy = y - pad;
                    Color c = Color.clear;
                    if (Filled(sx, sy)) c = white;
                    else if (Filled(sx + 1, sy) || Filled(sx - 1, sy) || Filled(sx, sy + 1) || Filled(sx, sy - 1)) c = outline;
                    else if (sx >= 0 && sy >= 0 && sx < n && sy < n) // eye/nose holes inside the skull read as dark
                    {
                        bool inside = false;
                        for (int k = 0; k < sx && !inside; k++) inside = Filled(k, sy);
                        bool right = false;
                        for (int k = sx + 1; k < n && !right; k++) right = Filled(k, sy);
                        if (inside && right) c = outline;
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            skullSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 10f);
            return skullSprite;
        }
    }
}
