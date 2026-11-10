using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions;
using TreasureRun.Treasures;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.UI
{
    /// <summary>
    /// Ascension look for a card slot of the vanilla card panel (docs/ascension-plan.md §1.2), inspired by the type
    /// line of card games:
    /// <list type="bullet">
    /// <item>a banner below the title, "BALLISTA ASCENSION", in the tower's colour, with the tower's build icon as crest</item>
    /// <item>a render of the ascension's model as card art</item>
    /// <item>a double frame behind the card (rarity colour outside, gold inside) that shimmers</item>
    /// <item>a first description line: rarity · how it changes the attack; an "Excludes" line at the end</item>
    /// </list>
    /// Elements are created once per slot and only shown for ascensions.
    /// </summary>
    internal static class AscensionCard
    {
        private const string RootName = "TR_Ascension";
        private const string FrameName = "TR_AscensionFrame";

        private static readonly Color Gold = new Color(0.95f, 0.78f, 0.30f);

        /// <summary>Card-local layout (vanilla card: 174 x 230, title at y 94, image 100x100 at y 22).</summary>
        private const float BannerY = 66f, BannerWidth = 150f, BannerHeight = 17f;
        private const float ArtY = 14f, ArtSize = 84f;

        private sealed class Slot
        {
            public RectTransform Root, Frame;
            public Image Banner, Crest, Outer, Inner;
            public Text BannerText;
            public bool DescCaptured, DescBestFit;
            public int DescMin, DescMax;
            public Shimmer Shimmer;
            public Vector2 ImagePos, ImageSize;
        }

        private static readonly Dictionary<int, Slot> slots = new Dictionary<int, Slot>();

        public static void Show(CardManager cm, int i, AscensionDef def, Image card)
        {
            Slot s = Ensure(cm, i, card);
            if (s == null) return;

            Color banner = AscensionTargets.BannerColor(def.Target);
            s.Banner.color = banner;
            s.BannerText.text = def.TypeLine.ToUpperInvariant();

            Sprite crest = CrestOf(def.Target);
            s.Crest.sprite = crest;
            s.Crest.enabled = crest != null;
            s.Crest.color = Color.Lerp(banner, Color.black, 0.45f);

            s.Outer.color = RarityInfo.IconColor(def.Rarity);
            s.Shimmer.Base = s.Outer.color;
            s.Inner.color = Gold;

            string kind = def.Kind == AscensionKind.Replace ? "replaces attack"
                        : def.Kind == AscensionKind.Augment ? "adds to attack" : "upgrades attack";

            // Card art: the ascension's model instead of the gem; slightly smaller to make room for the banner.
            Image art = cm.images[i];
            var artRt = (RectTransform)art.transform;
            artRt.anchoredPosition = new Vector2(s.ImagePos.x, ArtY);
            artRt.sizeDelta = new Vector2(ArtSize, ArtSize);
            Sprite model = ModelIcons.Get(def.CardModel);
            if (model != null)
            {
                art.sprite = model;
                art.color = Color.white;
                art.preserveAspect = true;
            }

            // Description: first line "Rare · upgrades attack", excluded siblings at the end; shrinks to fit.
            string excludes = string.Join(", ", AscensionRegistry.Siblings(def).Select(d => d.Name));
            Text desc = cm.descriptions[i];
            if (!s.DescCaptured) { s.DescBestFit = desc.resizeTextForBestFit; s.DescMin = desc.resizeTextMinSize; s.DescMax = desc.resizeTextMaxSize; s.DescCaptured = true; }
            desc.resizeTextForBestFit = true;
            desc.resizeTextMaxSize = desc.fontSize;
            desc.resizeTextMinSize = Mathf.RoundToInt(desc.fontSize * 0.6f);
            desc.text = $"<color={RarityInfo.Hex(RarityInfo.TextColor(def.Rarity))}><b>{def.Rarity}</b> · {kind}</color>\n" + def.Description +
                (excludes.Length > 0 ? $"\n<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(banner, Color.black, 0.2f))}><i>Excludes: {excludes}</i></color>" : "");

            s.Root.gameObject.SetActive(true);
            s.Frame.gameObject.SetActive(true);
        }

        public static void Hide(CardManager cm, int i)
        {
            if (!slots.TryGetValue(i, out Slot s) || s.Root == null) return;
            s.Root.gameObject.SetActive(false);
            if (s.Frame != null) s.Frame.gameObject.SetActive(false);
            var artRt = (RectTransform)cm.images[i].transform;
            artRt.anchoredPosition = s.ImagePos;
            artRt.sizeDelta = s.ImageSize;
            cm.images[i].preserveAspect = false;
            if (s.DescCaptured)
            {
                Text desc = cm.descriptions[i];
                desc.resizeTextForBestFit = s.DescBestFit;
                desc.resizeTextMinSize = s.DescMin;
                desc.resizeTextMaxSize = s.DescMax;
            }
        }

        private static Slot Ensure(CardManager cm, int i, Image card)
        {
            if (card == null) return null;
            if (slots.TryGetValue(i, out Slot existing) && existing.Root != null) return existing;

            var cardRt = (RectTransform)card.transform;
            var imageRt = (RectTransform)cm.images[i].transform;
            Text titleTemplate = cm.titles[i];
            var s = new Slot { ImagePos = imageRt.anchoredPosition, ImageSize = imageRt.sizeDelta };

            // Frame: siblings drawn behind the card (same parent, earlier in order), slightly larger.
            s.Frame = NewRect(FrameName, cardRt.parent);
            s.Frame.SetSiblingIndex(cardRt.GetSiblingIndex());
            CopyPlacement(cardRt, s.Frame);
            s.Outer = NewImage("Outer", s.Frame, card.sprite);
            Stretch((RectTransform)s.Outer.transform, -9f);
            s.Inner = NewImage("Inner", s.Frame, card.sprite);
            Stretch((RectTransform)s.Inner.transform, -4.5f);
            s.Shimmer = s.Frame.gameObject.AddComponent<Shimmer>();
            s.Shimmer.Target = s.Outer;

            // Overlay on the card.
            s.Root = NewRect(RootName, cardRt);
            Stretch(s.Root, 0f);

            s.Banner = NewImage("Banner", s.Root, card.sprite);
            var bannerRt = (RectTransform)s.Banner.transform;
            Place(bannerRt, new Vector2(0f, BannerY), new Vector2(BannerWidth, BannerHeight));
            s.Banner.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.35f);

            s.BannerText = NewText("TypeLine", bannerRt, titleTemplate, 9.5f, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place((RectTransform)s.BannerText.transform, new Vector2(8f, 0f), new Vector2(BannerWidth - 20f, BannerHeight));
            s.BannerText.color = Color.white;
            s.BannerText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);

            s.Crest = NewImage("Crest", s.Root, null);
            Place((RectTransform)s.Crest.transform, new Vector2(-BannerWidth / 2f + 8f, BannerY + 1f), new Vector2(30f, 30f));
            s.Crest.preserveAspect = true;

            slots[i] = s;
            return s;
        }

        /// <summary>The icon of the target's vanilla build button (child Image of the button).</summary>
        internal static Sprite CrestOf(AscensionTarget t)
        {
            BuildButtonUI button = AscensionTargets.BuildButton(t);
            if (button == null) return null;
            Image own = button.GetComponent<Image>();
            Image icon = button.GetComponentsInChildren<Image>(true).FirstOrDefault(img => img != own && img.sprite != null);
            return icon != null ? icon.sprite : null;
        }

        // ------------------------------------------------------------------ helpers

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// Text styled like the card title. The vanilla card texts use a big rect scaled down (crisper glyphs), so new
        /// texts copy the title's scale and multiply size/font size accordingly.
        /// </summary>
        private static Text NewText(string name, Transform parent, Text template, float fontSize, TextAnchor anchor, FontStyle style)
        {
            RectTransform rt = NewRect(name, parent);
            float scale = Mathf.Max(0.001f, template.transform.localScale.x);
            rt.localScale = Vector3.one * scale;
            var text = rt.gameObject.AddComponent<Text>();
            text.font = template.font;
            text.fontSize = Mathf.RoundToInt(fontSize / scale);
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Positions a rect (centre anchors) by local size; accounts for its own scale (scaled texts).</summary>
        private static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            float scale = Mathf.Max(0.001f, rt.localScale.x);
            rt.sizeDelta = size / scale;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void CopyPlacement(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
            to.localScale = from.localScale;
            to.localRotation = from.localRotation;
        }

        /// <summary>Slow brightness pulse of the outer frame (unscaled time: the card panel may pause the game).</summary>
        internal sealed class Shimmer : MonoBehaviour
        {
            public Image Target;
            public Color Base;

            private void Update()
            {
                if (Target == null) return;
                float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
                Target.color = Color.Lerp(Base, Color.white, 0.35f * t);
            }
        }
    }
}
