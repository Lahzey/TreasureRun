using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TreasureRun.UI
{
    /// <summary>
    /// Owned treasures along the right screen edge (uGUI, game font and panel sprite), laid out like CSS
    /// flex-direction: column + flex-wrap: wrap-reverse: top to bottom, then further columns to the left. The margin to
    /// the screen edges equals the gap between slots. Hovering a slot shows name, rarity and description.
    /// Created per GameScene during a Treasure Run.
    /// </summary>
    internal static class InventoryBar
    {
        private const float SlotSize = 44f;
        private const float Gap = 6f;
        private const float TooltipWidth = 320f;

        private static GameObject root;
        private static RectTransform bar;
        private static RectTransform tooltip;
        private static Text tooltipText;
        private static Font font;
        private static Sprite panelSprite;

        internal static void Initialize()
        {
            TreasureInventory.TreasureGained += _ => Refresh();
            TreasureRunMode.RunEnded += _ => Destroy();
        }

        /// <summary>Called from the CardManager.Start postfix (GameScene, Treasure Run).</summary>
        public static void Create(CardManager cm)
        {
            Destroy();
            font = cm.titles[0].font;
            Image cardBg = cm.titles[0].transform.parent.GetComponent<Image>();
            panelSprite = cardBg != null ? cardBg.sprite : null;

            root = new GameObject("TR_InventoryBar");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // Full-screen container; slots are positioned by Layout() from the top-right corner.
            bar = NewRect("Bar", root.transform);
            Stretch(bar);
            bar.gameObject.AddComponent<RelayoutOnResize>();

            tooltip = NewRect("Tooltip", root.transform);
            tooltip.pivot = new Vector2(1f, 1f);
            tooltip.anchorMin = tooltip.anchorMax = new Vector2(1f, 1f);
            var tipBg = tooltip.gameObject.AddComponent<Image>();
            ApplyPanel(tipBg, Color.white);
            tipBg.raycastTarget = false;
            tooltipText = NewText("Text", tooltip, 17, TextAnchor.UpperLeft);
            var tr = (RectTransform)tooltipText.transform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(14f, 10f);
            tr.offsetMax = new Vector2(-14f, -10f);
            tooltip.gameObject.SetActive(false);

            Refresh();
        }

        public static void Destroy()
        {
            if (root != null) Object.Destroy(root);
            root = null;
        }

        public static void Refresh()
        {
            if (root == null || TreasureRunMode.Run == null) return;
            foreach (Transform child in bar)
            {
                child.name = DestroyedMark; // Destroy is deferred; Layout skips these
                Object.Destroy(child.gameObject);
            }

            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (string id in TreasureRunMode.Run.OwnedTreasures)
            {
                if (!counts.ContainsKey(id)) { order.Add(id); counts[id] = 0; }
                counts[id]++;
            }
            // Ascensions first: they define the run.
            order = order.OrderBy(id => Ascensions.AscensionRegistry.ForTreasure(id) == null ? 1 : 0).ToList();
            foreach (string id in order)
            {
                TreasureDef def = TreasureRegistry.Get(id);
                if (def != null) AddSlot(def, counts[id]);
            }
            Layout();
        }

        /// <summary>Column-major from the top-right corner; wraps into a new column to the left when a column is full.</summary>
        internal static void Layout()
        {
            if (bar == null) return;
            float height = bar.rect.height;
            int perColumn = Mathf.Max(1, Mathf.FloorToInt((height - Gap) / (SlotSize + Gap)));
            int i = 0;
            foreach (Transform child in bar)
            {
                if (child.name == DestroyedMark) continue;
                var rt = (RectTransform)child;
                int col = i / perColumn, row = i % perColumn;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-(Gap + col * (SlotSize + Gap)), -(Gap + row * (SlotSize + Gap)));
                i++;
            }
        }

        private const string DestroyedMark = "TR_Destroyed";

        private sealed class RelayoutOnResize : MonoBehaviour
        {
            private void OnRectTransformDimensionsChange() => Layout();
        }

        private static void AddSlot(TreasureDef def, int count)
        {
            RectTransform slot = NewRect(def.Id, bar);
            slot.sizeDelta = new Vector2(SlotSize, SlotSize);
            var bg = slot.gameObject.AddComponent<Image>();
            ApplyPanel(bg, Color.Lerp(Color.white, RarityInfo.IconColor(def.Rarity), 0.45f));

            RectTransform gem = NewRect("Gem", slot);
            gem.sizeDelta = new Vector2(SlotSize * 0.62f, SlotSize * 0.62f);
            var gemImg = gem.gameObject.AddComponent<Image>();
            gemImg.sprite = TreasureIcons.Gem;
            gemImg.color = RarityInfo.IconColor(def.Rarity);
            gemImg.raycastTarget = false;

            Ascensions.AscensionDef asc = Ascensions.AscensionRegistry.ForTreasure(def);
            if (asc != null)
            {
                // Ascension: a gold rim and the tower's crest as a badge in the lower left corner.
                slot.gameObject.AddComponent<Outline>().effectColor = new Color(0.95f, 0.78f, 0.3f, 1f);
                Sprite crest = AscensionCard.CrestOf(asc.Target);
                if (crest != null)
                {
                    RectTransform badgeRt = NewRect("Crest", slot);
                    badgeRt.anchorMin = badgeRt.anchorMax = badgeRt.pivot = new Vector2(0f, 0f);
                    badgeRt.anchoredPosition = new Vector2(1f, 1f);
                    badgeRt.sizeDelta = new Vector2(SlotSize * 0.42f, SlotSize * 0.42f);
                    var badge = badgeRt.gameObject.AddComponent<Image>();
                    badge.sprite = crest;
                    badge.preserveAspect = true;
                    badge.color = Ascensions.AscensionTargets.BannerColor(asc.Target);
                    badge.raycastTarget = false;
                }
            }

            Text initials = NewText("Initials", slot, 15, TextAnchor.MiddleCenter);
            initials.text = Initials(def.Name);
            initials.fontStyle = FontStyle.Bold;
            initials.color = Color.white;
            initials.raycastTarget = false;
            var outline = initials.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            Stretch((RectTransform)initials.transform);

            if (count > 1)
            {
                Text badge = NewText("Count", slot, 14, TextAnchor.LowerRight);
                badge.text = "x" + count;
                badge.color = Color.white;
                badge.raycastTarget = false;
                badge.gameObject.AddComponent<Outline>().effectColor = Color.black;
                Stretch((RectTransform)badge.transform, 2f);
            }

            var trigger = slot.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => ShowTooltip(def, count, slot));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => tooltip.gameObject.SetActive(false));
        }

        private static void ShowTooltip(TreasureDef def, int count, RectTransform slot)
        {
            string name = RarityInfo.Hex(RarityInfo.TextColor(def.Rarity));
            tooltipText.text = $"<b><color={name}>{def.Name}</color></b>{(count > 1 ? $"  x{count}" : "")}\n" +
                               $"<color={name}><i>{def.Rarity}{TypeLineOf(def)}</i></color>\n{def.Description}";
            tooltip.sizeDelta = new Vector2(TooltipWidth, 0f);
            float textHeight = tooltipText.cachedTextGeneratorForLayout.GetPreferredHeight(
                tooltipText.text, tooltipText.GetGenerationSettings(new Vector2(TooltipWidth - 28f, 0f))) / tooltipText.pixelsPerUnit;
            tooltip.sizeDelta = new Vector2(TooltipWidth, textHeight + 24f);

            // Left of the hovered slot, top-aligned with it, kept on screen.
            var slotRt = slot;
            var canvasRect = (RectTransform)root.transform;
            float y = Mathf.Max(slotRt.anchoredPosition.y, -(canvasRect.rect.height - tooltip.sizeDelta.y - Gap));
            tooltip.anchoredPosition = new Vector2(slotRt.anchoredPosition.x - SlotSize - Gap, y);
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
        }

        private static string TypeLineOf(TreasureDef def)
        {
            Ascensions.AscensionDef asc = Ascensions.AscensionRegistry.ForTreasure(def);
            return asc != null ? "  ·  " + asc.TypeLine : "";
        }

        // ----- helpers -----

        /// <summary>Two-letter label: initials of the first two significant words ("Prism of Ascendance" = "PA").</summary>
        private static string Initials(string name)
        {
            string[] words = name.Split(new[] { ' ', '-' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Where(w => char.IsUpper(w[0])).ToArray();
            if (words.Length == 0) return name.Substring(0, 1);
            return words.Length == 1 ? words[0].Substring(0, Mathf.Min(2, words[0].Length)) : $"{words[0][0]}{words[1][0]}";
        }

        private static void ApplyPanel(Image img, Color color)
        {
            img.sprite = panelSprite;
            img.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = 4f;
            img.color = color;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Text NewText(string name, Transform parent, int size, TextAnchor anchor)
        {
            RectTransform rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.supportRichText = true;
            text.color = new Color(0.2f, 0.2f, 0.2f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
