using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Ascensions;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TreasureRun.Pois
{
    /// <summary>Colours and patterns of a merchant's shop window (one per kind).</summary>
    internal sealed class MerchantSkin
    {
        public Color Backdrop, Border, Panel, Card, CardBorder, Text, Muted, Title, Accent, Button, ButtonText, Banner, StripeA, StripeB, Rod;
        public bool Dark, Stars;
        public string Tagline;

        public static MerchantSkin For(MerchantKind kind)
        {
            switch (kind)
            {
                case MerchantKind.Legendary:
                    return new MerchantSkin
                    {
                        Backdrop = new Color(0.08f, 0.02f, 0.1f, 0.6f), Border = new Color(0.95f, 0.76f, 0.28f), Panel = new Color(0.2f, 0.09f, 0.24f),
                        Card = new Color(0.3f, 0.15f, 0.34f), CardBorder = new Color(0.62f, 0.45f, 0.18f), Text = new Color(0.97f, 0.92f, 0.82f),
                        Muted = new Color(0.8f, 0.7f, 0.78f), Title = new Color(1f, 0.82f, 0.35f), Accent = new Color(1f, 0.78f, 0.3f),
                        Button = new Color(0.95f, 0.74f, 0.25f), ButtonText = new Color(0.25f, 0.1f, 0.2f), Banner = new Color(0.45f, 0.2f, 0.45f),
                        StripeA = new Color(0.45f, 0.16f, 0.52f), StripeB = new Color(0.98f, 0.8f, 0.32f), Rod = new Color(0.62f, 0.45f, 0.12f),
                        Dark = true, Tagline = "Only the finest. Only Legendary.",
                    };
                case MerchantKind.Ascension:
                    return new MerchantSkin
                    {
                        Backdrop = new Color(0.01f, 0.03f, 0.1f, 0.62f), Border = new Color(0.45f, 0.85f, 1f), Panel = new Color(0.06f, 0.09f, 0.21f),
                        Card = new Color(0.11f, 0.15f, 0.32f), CardBorder = new Color(0.3f, 0.5f, 0.85f), Text = new Color(0.88f, 0.94f, 1f),
                        Muted = new Color(0.62f, 0.72f, 0.88f), Title = new Color(0.62f, 0.92f, 1f), Accent = new Color(0.5f, 0.88f, 1f),
                        Button = new Color(0.38f, 0.74f, 0.98f), ButtonText = new Color(0.04f, 0.08f, 0.2f), Banner = new Color(0.16f, 0.24f, 0.5f),
                        StripeA = new Color(0.15f, 0.19f, 0.5f), StripeB = new Color(0.62f, 0.9f, 1f), Rod = new Color(0.55f, 0.6f, 0.75f),
                        Dark = true, Stars = true, Tagline = "Power beyond the tower's own.",
                    };
                default:
                    return new MerchantSkin
                    {
                        Backdrop = new Color(0.05f, 0.03f, 0.01f, 0.5f), Border = new Color(0.4f, 0.24f, 0.12f), Panel = new Color(0.96f, 0.9f, 0.76f),
                        Card = new Color(1f, 0.97f, 0.88f), CardBorder = new Color(0.66f, 0.5f, 0.32f), Text = new Color(0.22f, 0.15f, 0.1f),
                        Muted = new Color(0.45f, 0.36f, 0.28f), Title = new Color(0.45f, 0.2f, 0.08f), Accent = new Color(0.78f, 0.2f, 0.15f),
                        Button = new Color(0.9f, 0.66f, 0.28f), ButtonText = new Color(0.25f, 0.13f, 0.05f), Banner = new Color(0.9f, 0.8f, 0.6f),
                        StripeA = new Color(0.78f, 0.18f, 0.15f), StripeB = new Color(0.99f, 0.94f, 0.83f), Rod = new Color(0.42f, 0.26f, 0.13f),
                        Tagline = "Fine wares from far away!",
                    };
            }
        }

        /// <summary>Rarity colour readable on this skin's cards.</summary>
        public Color RarityColor(Rarity r) => Dark ? RarityInfo.IconColor(r) : RarityInfo.TextColor(r);
    }

    /// <summary>
    /// The Wandering Merchant's shop window (screen-space uGUI, built in code; 1920 x 1080 reference units): a striped awning
    /// valance over a panel in the merchant's colours, the rule (when the merchant leaves), three offer cards (one with a
    /// discount ribbon) and a counter where owned treasures are sold for half the merchant's price. The final purchase and
    /// every sale ask for a second click. While it is open, clicks and the camera don't reach the map.
    /// </summary>
    public static class MerchantShopUI
    {
        private static GameObject root;
        private static Merchant current;
        private static ShopView view;

        public static bool IsOpen => root != null;
        public static Merchant Current => current;

        public static void Open(Merchant merchant)
        {
            if (merchant == null || merchant.Closed) return;
            Close();
            UIManager.instance?.SetNewUI(null); // a vanilla tower popup closes
            current = merchant;
            root = new GameObject("TR_MerchantShop");
            UnityEngine.Object.DontDestroyOnLoad(root);
            view = root.AddComponent<ShopView>();
            view.Build(merchant);
            if (CameraController.instance != null) CameraController.instance.enabled = false;
            SoundLibrary.PlayUi(Merchant.SoundOpen, 0.9f);
        }

        public static void Close()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            view = null;
            current = null;
            if (CameraController.instance != null) CameraController.instance.enabled = true;
        }

        /// <summary>Rebuilds the window (after a purchase or sale), or closes it once the merchant has packed up.</summary>
        public static void Refresh()
        {
            if (current == null) return;
            if (current.Closed) { Close(); return; }
            view?.Build(current);
        }

        /// <summary>Clicks a button of the open window (scenarios): "buy &lt;slot&gt;" or "sell &lt;id&gt;", twice for the confirmation.</summary>
        internal static bool Press(string what) => view != null && view.Press(what);

        /// <summary>
        /// Scenarios: what a click at the centre of the named element would hit first (the UI raycast, as the mouse does),
        /// e.g. "Close" must hit the close button itself.
        /// </summary>
        internal static string TopHitAt(string element)
        {
            if (view == null) return null;
            Transform t = view.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == element);
            if (t == null) return null;
            Canvas.ForceUpdateCanvases();
            Vector2 p = RectTransformUtility.WorldToScreenPoint(null, t.position);
            // What GraphicRaycaster does: the raycast-target graphic under the point that is drawn last (highest depth).
            Graphic top = view.GetComponentsInChildren<Graphic>()
                .Where(g => g.raycastTarget && g.isActiveAndEnabled && g.depth >= 0 && RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, p, null))
                .OrderByDescending(g => g.depth).FirstOrDefault();
            if (top == null) return "";
            Button b = top.GetComponentInParent<Button>();
            return b != null ? b.name : top.name;
        }

        // While the shop is open the map gets no clicks (vanilla UIManager raycasts straight through screen UI); Escape and
        // a right click close the shop.
        [HarmonyPatch(typeof(UIManager), "Update")]
        internal static class UIManagerPatch
        {
            [HarmonyPrefix]
            private static bool Prefix()
            {
                if (!IsOpen) return true;
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) Close();
                return false;
            }
        }

        // ------------------------------------------------------------------ view

        private sealed class OfferWidget
        {
            public int Slot;
            public Button Button;
            public Image ButtonImage;
            public Text Label;
        }

        private sealed class SellWidget
        {
            public TreasureDef Def;
            public Button Button;
            public Text Label;
        }

        private sealed class ShopView : MonoBehaviour
        {
            private Merchant m;
            private MerchantSkin skin;
            private Font font;
            private RectTransform canvasRoot;
            private Text goldText, hint, ruleText;
            private readonly List<OfferWidget> offers = new List<OfferWidget>();
            private readonly List<SellWidget> sells = new List<SellWidget>();
            private int confirmSlot = -1;
            private string confirmSell;
            private float confirmUntil, refreshTimer;
            private float scroll = 1f;

            private const float W = 1240f, H = 860f;

            public void Build(Merchant merchant)
            {
                m = merchant;
                skin = MerchantSkin.For(m.Kind);
                font = UiFont();
                ScrollRect oldScroll = canvasRoot != null ? canvasRoot.GetComponentInChildren<ScrollRect>() : null;
                if (oldScroll != null) scroll = oldScroll.verticalNormalizedPosition;
                foreach (Transform child in transform) Destroy(child.gameObject);
                offers.Clear();
                sells.Clear();
                confirmSlot = -1;
                confirmSell = null;

                if (GetComponent<Canvas>() == null)
                {
                    var canvas = gameObject.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 60;
                    var scaler = gameObject.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920f, 1080f);
                    scaler.matchWidthOrHeight = 0.5f;
                    gameObject.AddComponent<GraphicRaycaster>();
                }
                canvasRoot = (RectTransform)transform;

                // Backdrop: dims the map and closes the shop when clicked.
                RectTransform backdrop = MenuKit.Rect("Backdrop", transform);
                MenuKit.Stretch(backdrop);
                backdrop.gameObject.AddComponent<Image>().color = skin.Backdrop;
                backdrop.gameObject.AddComponent<Button>().onClick.AddListener(Close);

                RectTransform panel = MenuKit.Rect("Panel", transform);
                panel.sizeDelta = new Vector2(W, H);
                panel.anchoredPosition = new Vector2(0f, -10f);
                Frame(panel, skin.Border, skin.Panel, 6f);
                panel.gameObject.AddComponent<Image>().color = Color.clear; // swallow clicks on the panel
                if (skin.Stars) Stars(panel);

                Valance(panel);
                Header(panel);
                Rule(panel);
                Offers(panel);
                SellCounter(panel);
                RectTransform hintRt = Place(MenuKit.Rect("Hint", panel), 0f, 0f, 1f, 0f, 30f, 12f, -30f, 44f);
                hint = Label(hintRt, "", 17, skin.Muted, TextAnchor.MiddleCenter);
                UpdateLive();
            }

            // ----- sections

            private void Valance(RectTransform panel)
            {
                // A striped awning with scalloped edges hanging over the top of the panel, on a wooden rod.
                RectTransform rod = Place(MenuKit.Rect("Rod", panel), 0.5f, 1f, 0.5f, 1f, -W / 2f - 40f, 6f, W / 2f + 40f, 22f);
                Rounded(rod, skin.Rod);
                RectTransform band = Place(MenuKit.Rect("Valance", panel), 0.5f, 1f, 0.5f, 1f, -W / 2f - 30f, -62f, W / 2f + 30f, 10f);
                var raw = band.gameObject.AddComponent<RawImage>();
                raw.texture = ShopTextures.Valance(skin.StripeA, skin.StripeB);
                raw.uvRect = new Rect(0f, 0f, (W + 60f) / 72f / 8f, 1f); // a stripe pair every 72 units
                raw.raycastTarget = false;
            }

            private void Header(RectTransform panel)
            {
                RectTransform title = Place(MenuKit.Rect("Title", panel), 0f, 1f, 0.7f, 1f, 34f, -122f, 0f, -70f);
                Text t = Label(title, m.Def.Name, 40, skin.Title, TextAnchor.MiddleLeft, FontStyle.Bold);
                Shadow(t);
                RectTransform tag = Place(MenuKit.Rect("Tagline", panel), 0f, 1f, 0.7f, 1f, 36f, -150f, 0f, -120f);
                Label(tag, $"<i>{skin.Tagline}</i>   Prices rise with every wave.", 19, skin.Muted, TextAnchor.MiddleLeft);

                RectTransform gold = Place(MenuKit.Rect("Gold", panel), 0.6f, 1f, 1f, 1f, 0f, -132f, -96f, -82f);
                goldText = Label(gold, "", 28, skin.Text, TextAnchor.MiddleRight, FontStyle.Bold);

                RectTransform close = Place(MenuKit.Rect("Close", panel), 1f, 1f, 1f, 1f, -82f, -134f, -30f, -82f);
                Frame(close, skin.Border, skin.Card, 3f);
                // The frame's images don't take clicks: the button needs a graphic of its own, or the click reaches the panel.
                var hit = close.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var closeButton = close.gameObject.AddComponent<Button>();
                closeButton.targetGraphic = hit;
                closeButton.onClick.AddListener(Close);
                Label(close, "X", 26, skin.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            private void Rule(RectTransform panel)
            {
                RectTransform banner = Place(MenuKit.Rect("Rule", panel), 0f, 1f, 1f, 1f, 30f, -212f, -30f, -162f);
                Frame(banner, skin.Accent, skin.Banner, 2f);
                ruleText = Label(banner, "", 20, skin.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                ((RectTransform)ruleText.transform).offsetMin = new Vector2(16f, 0f);
                ((RectTransform)ruleText.transform).offsetMax = new Vector2(-16f, 0f);
            }

            private void Offers(RectTransform panel)
            {
                RectTransform head = Place(MenuKit.Rect("ForSale", panel), 0f, 1f, 1f, 1f, 34f, -250f, -30f, -220f);
                Label(head, "FOR SALE", 22, skin.Title, TextAnchor.MiddleLeft, FontStyle.Bold);
                RectTransform row = Place(MenuKit.Rect("Offers", panel), 0f, 1f, 1f, 1f, 30f, -600f, -30f, -254f);
                HorizontalLayoutGroup h = MenuKit.Horizontal(row.gameObject, 26f, TextAnchor.MiddleCenter, expandWidth: false);
                h.childForceExpandHeight = true;
                List<TreasureDef> stock = m.Stock;
                for (int i = 0; i < stock.Count; i++) Offer(row, i, stock[i]);
            }

            private void Offer(RectTransform row, int slot, TreasureDef t)
            {
                RectTransform card = MenuKit.Rect("Offer" + slot, row);
                MenuKit.Size(card.gameObject, prefWidth: 372f, prefHeight: 346f);
                bool sold = m.Bought(slot);
                string blocked = m.Blocked(slot);
                Color rc = skin.RarityColor(t.Rarity);
                Frame(card, sold ? skin.CardBorder : rc, skin.Card, 4f);
                var group = card.gameObject.AddComponent<CanvasGroup>();
                if (sold || (blocked != null)) group.alpha = 0.6f;
                AddHover(card.gameObject, () => hint.text = $"<b>{t.Name}</b> ({t.Rarity}): {t.Description}", () => hint.text = "");

                AscensionDef asc = AscensionRegistry.ForTreasure(t);
                RectTransform iconRt = Place(MenuKit.Rect("Icon", card), 0f, 1f, 0f, 1f, 18f, -98f, 98f, -18f);
                var icon = iconRt.gameObject.AddComponent<Image>();
                Sprite art = asc != null && asc.CardModel != null ? ModelIcons.Get(asc.CardModel, 128) : null;
                icon.sprite = art != null ? art : TreasureIcons.Gem;
                icon.color = art != null ? Color.white : RarityInfo.IconColor(t.Rarity);
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                RectTransform nameRt = Place(MenuKit.Rect("Name", card), 0f, 1f, 1f, 1f, 108f, -62f, -16f, -18f);
                Text name = Label(nameRt, t.Name, 24, rc, TextAnchor.MiddleLeft, FontStyle.Bold);
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 16; name.resizeTextMaxSize = 24;
                RectTransform kindRt = Place(MenuKit.Rect("Kind", card), 0f, 1f, 1f, 1f, 108f, -94f, -16f, -62f);
                Label(kindRt, asc != null ? $"{t.Rarity} Ascension · {asc.TargetName}" : $"{t.Rarity} Treasure", 18, skin.Muted, TextAnchor.MiddleLeft);

                RectTransform descRt = Place(MenuKit.Rect("Description", card), 0f, 0f, 1f, 1f, 18f, 78f, -18f, -108f);
                Text desc = Label(descRt, t.Description, 18, skin.Text, TextAnchor.UpperLeft);
                desc.resizeTextForBestFit = true; desc.resizeTextMinSize = 12; desc.resizeTextMaxSize = 18;
                desc.verticalOverflow = VerticalWrapMode.Truncate;

                // Buy button.
                RectTransform btnRt = Place(MenuKit.Rect("Buy", card), 0f, 0f, 1f, 0f, 16f, 14f, -16f, 66f);
                Image btnImg = Rounded(btnRt, skin.Button);
                var btn = btnRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = btnImg;
                Text label = Label(btnRt, "", 22, skin.ButtonText, TextAnchor.MiddleCenter, FontStyle.Bold);
                int s = slot;
                btn.onClick.AddListener(() => ClickBuy(s));
                offers.Add(new OfferWidget { Slot = slot, Button = btn, ButtonImage = btnImg, Label = label });

                if (slot == m.DiscountSlot && !sold)
                {
                    RectTransform ribbon = Place(MenuKit.Rect("Discount", card), 1f, 1f, 1f, 1f, -104f, -6f, 8f, 30f);
                    ribbon.localRotation = Quaternion.Euler(0f, 0f, -8f);
                    Frame(ribbon, new Color(0.45f, 0.05f, 0.05f), new Color(0.88f, 0.16f, 0.12f), 2f);
                    Label(ribbon, $"-{Mathf.RoundToInt((float)MerchantLogic.Discount * 100f)}%", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                if (sold)
                {
                    RectTransform stamp = Place(MenuKit.Rect("Sold", card), 0.5f, 0.5f, 0.5f, 0.5f, -150f, -40f, 150f, 50f);
                    stamp.localRotation = Quaternion.Euler(0f, 0f, 12f);
                    Text st = Label(stamp, "SOLD", 64, skin.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
                    var o = st.gameObject.AddComponent<Outline>();
                    o.effectColor = new Color(0f, 0f, 0f, 0.6f);
                    o.effectDistance = new Vector2(2f, -2f);
                }
            }

            private void SellCounter(RectTransform panel)
            {
                RectTransform head = Place(MenuKit.Rect("SellHead", panel), 0f, 1f, 1f, 1f, 34f, -646f, -30f, -612f);
                Label(head, $"SELL YOUR TREASURES  <size=18><color={RarityInfo.Hex(skin.Muted)}>for half of what this merchant would ask</color></size>",
                      22, skin.Title, TextAnchor.MiddleLeft, FontStyle.Bold);

                RectTransform frame = Place(MenuKit.Rect("SellFrame", panel), 0f, 0f, 1f, 1f, 30f, 50f, -30f, -650f);
                Frame(frame, skin.CardBorder, Color.Lerp(skin.Panel, skin.Card, 0.5f), 2f);
                RectTransform viewport = MenuKit.Rect("Viewport", frame);
                MenuKit.Stretch(viewport, 8f, 8f, 6f, 6f);
                viewport.gameObject.AddComponent<RectMask2D>();
                viewport.gameObject.AddComponent<Image>().color = Color.clear;
                RectTransform content = MenuKit.Rect("Content", viewport);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = Vector2.zero;
                content.anchoredPosition = Vector2.zero;
                var grid = content.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(380f, 44f);
                grid.spacing = new Vector2(8f, 6f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
                grid.padding = new RectOffset(2, 2, 2, 2);
                MenuKit.FitContent(content.gameObject, false, true);
                var sr = frame.gameObject.AddComponent<ScrollRect>();
                sr.viewport = viewport;
                sr.content = content;
                sr.horizontal = false;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.scrollSensitivity = 30f;

                // One row per owned treasure (copies grouped); sellable ones first.
                var owned = TreasureRunMode.Run.OwnedTreasures.GroupBy(id => id).Select(g => (def: TreasureRegistry.Get(g.Key), count: g.Count()))
                    .Where(x => x.def != null).OrderBy(x => TreasureSale.WhyNot(x.def) != null).ThenByDescending(x => x.def.Rarity).ThenBy(x => x.def.Name).ToList();
                if (owned.Count == 0)
                {
                    RectTransform none = MenuKit.Rect("None", content);
                    Label(none, "You have no treasures to sell.", 18, skin.Muted, TextAnchor.MiddleLeft);
                }
                foreach (var (def, count) in owned) SellRow(content, def, count);
                Canvas.ForceUpdateCanvases();
                sr.verticalNormalizedPosition = scroll;
            }

            private void SellRow(RectTransform content, TreasureDef def, int count)
            {
                RectTransform row = MenuKit.Rect(def.Id, content);
                string why = TreasureSale.WhyNot(def);
                Frame(row, Color.Lerp(skin.CardBorder, skin.Card, 0.4f), skin.Card, 2f);
                var group = row.gameObject.AddComponent<CanvasGroup>();
                if (why != null) group.alpha = 0.5f;
                AddHover(row.gameObject, () => hint.text = why != null ? $"<b>{def.Name}</b> can't be sold: {why}" : $"<b>{def.Name}</b> ({def.Rarity}): {def.Description}",
                         () => hint.text = "");

                RectTransform iconRt = Place(MenuKit.Rect("Icon", row), 0f, 0.5f, 0f, 0.5f, 8f, -14f, 36f, 14f);
                var icon = iconRt.gameObject.AddComponent<Image>();
                icon.sprite = TreasureIcons.Gem;
                icon.color = RarityInfo.IconColor(def.Rarity);
                icon.raycastTarget = false;
                RectTransform nameRt = Place(MenuKit.Rect("Name", row), 0f, 0f, 1f, 1f, 44f, 0f, -124f, 0f);
                Text name = Label(nameRt, def.Name + (count > 1 ? $" <color={RarityInfo.Hex(skin.Muted)}>x{count}</color>" : ""), 18,
                                  skin.RarityColor(def.Rarity), TextAnchor.MiddleLeft, FontStyle.Bold);
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 12; name.resizeTextMaxSize = 18;

                RectTransform btnRt = Place(MenuKit.Rect("Sell", row), 1f, 0.5f, 1f, 0.5f, -118f, -16f, -6f, 16f);
                if (why != null)
                {
                    Label(btnRt, "can't sell", 16, skin.Muted, TextAnchor.MiddleCenter);
                    return;
                }
                Image img = Rounded(btnRt, skin.Button);
                var btn = btnRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                Text label = Label(btnRt, "", 17, skin.ButtonText, TextAnchor.MiddleCenter, FontStyle.Bold);
                btn.onClick.AddListener(() => ClickSell(def));
                sells.Add(new SellWidget { Def = def, Button = btn, Label = label });
            }

            // ----- actions

            private void ClickBuy(int slot)
            {
                if (m.Blocked(slot) != null || !ResourceManager.instance.CheckMoney(m.PriceOf(slot))) return;
                bool last = MerchantLogic.LeavesAfterPurchase(m.Kind, m.OpenOffers - 1);
                if (last && (confirmSlot != slot || Time.unscaledTime > confirmUntil))
                {
                    confirmSlot = slot;
                    confirmSell = null;
                    confirmUntil = Time.unscaledTime + 4f;
                    SFXManager.instance?.ButtonClick();
                    UpdateLive();
                    return;
                }
                if (m.Buy(slot)) Refresh();
            }

            private void ClickSell(TreasureDef def)
            {
                if (confirmSell != def.Id || Time.unscaledTime > confirmUntil)
                {
                    confirmSell = def.Id;
                    confirmSlot = -1;
                    confirmUntil = Time.unscaledTime + 4f;
                    SFXManager.instance?.ButtonClick();
                    UpdateLive();
                    return;
                }
                if (m.Sell(def)) Refresh();
            }

            public bool Press(string what)
            {
                string[] p = what.Split(' ');
                if (p.Length < 2) return false;
                if (p[0] == "buy" && int.TryParse(p[1], out int slot)) { ClickBuy(slot); return true; }
                if (p[0] == "sell") { TreasureDef d = TreasureRegistry.Get(p[1]); if (d == null) return false; ClickSell(d); return true; }
                return false;
            }

            // ----- live state (gold changes during waves)

            private void Update()
            {
                if (m == null || m.Closed) { Close(); return; }
                if ((confirmSlot >= 0 || confirmSell != null) && Time.unscaledTime > confirmUntil)
                {
                    confirmSlot = -1;
                    confirmSell = null;
                    UpdateLive();
                }
                refreshTimer -= Time.unscaledDeltaTime;
                if (refreshTimer > 0f) return;
                refreshTimer = 0.25f;
                UpdateLive();
            }

            private void UpdateLive()
            {
                int gold = GameHelpers.Gold;
                if (goldText != null) goldText.text = $"<color={RarityInfo.Hex(skin.Muted)}><size=20>Your gold</size></color>  {gold}g";
                if (ruleText != null) ruleText.text = m.Rule;
                foreach (OfferWidget o in offers)
                {
                    string blocked = m.Blocked(o.Slot);
                    int price = m.PriceOf(o.Slot);
                    bool afford = gold >= price;
                    bool canBuy = blocked == null && afford;
                    o.Button.interactable = canBuy;
                    o.ButtonImage.color = canBuy ? (confirmSlot == o.Slot ? skin.Accent : skin.Button) : Color.Lerp(skin.Button, skin.Card, 0.65f);
                    string was = o.Slot == m.DiscountSlot ? $"  <size=16><color={RarityInfo.Hex(Color.Lerp(skin.ButtonText, skin.Button, 0.4f))}>(was {m.FullPriceOf(o.Slot)}g)</color></size>" : "";
                    if (blocked == "Sold") o.Label.text = "Sold";
                    else if (blocked != null) o.Label.text = $"<size=18>{blocked}</size>";
                    else if (confirmSlot == o.Slot) o.Label.text = $"<size=18>{(m.Kind == MerchantKind.Ascension ? "Buy? The merchant leaves" : "Last one! Buy and close shop?")}</size>";
                    else if (!afford) o.Label.text = $"{price}g  <size=17>(need {price - gold} more)</size>";
                    else o.Label.text = $"Buy  {price}g{was}";
                }
                foreach (SellWidget s in sells)
                {
                    int price = m.SellPriceOf(s.Def);
                    s.Label.text = confirmSell == s.Def.Id ? "Sure?" : $"Sell +{price}g";
                    s.Button.targetGraphic.color = confirmSell == s.Def.Id ? skin.Accent : skin.Button;
                }
            }

            // ----- widgets

            private RectTransform Place(RectTransform rt, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
            {
                rt.anchorMin = new Vector2(minX, minY);
                rt.anchorMax = new Vector2(maxX, maxY);
                rt.offsetMin = new Vector2(left, bottom);
                rt.offsetMax = new Vector2(right, top);
                return rt;
            }

            private Text Label(RectTransform parent, string text, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Normal)
            {
                RectTransform rt = MenuKit.Rect("Text", parent);
                MenuKit.Stretch(rt);
                var t = rt.gameObject.AddComponent<Text>();
                t.font = font;
                t.fontSize = size;
                t.fontStyle = style;
                t.alignment = align;
                t.color = color;
                t.supportRichText = true;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.text = text;
                return t;
            }

            private static void Shadow(Text t)
            {
                var s = t.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.35f);
                s.effectDistance = new Vector2(2f, -2f);
            }

            private static Image Rounded(RectTransform rt, Color color)
            {
                Image img = rt.gameObject.GetComponent<Image>();
                if (img == null) img = rt.gameObject.AddComponent<Image>();
                img.sprite = ShopTextures.Round;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1.4f;
                img.color = color;
                return img;
            }

            /// <summary>A rounded frame: the border colour behind, the fill inset by <paramref name="width"/>.</summary>
            private static void Frame(RectTransform rt, Color border, Color fill, float width)
            {
                RectTransform b = MenuKit.Rect("Border", rt);
                MenuKit.Stretch(b);
                Rounded(b, border).raycastTarget = false;
                RectTransform f = MenuKit.Rect("Fill", rt);
                MenuKit.Stretch(f, width, width, width, width);
                Rounded(f, fill).raycastTarget = false;
                b.SetAsFirstSibling();
                f.SetSiblingIndex(1);
            }

            private void Stars(RectTransform panel)
            {
                RectTransform s = MenuKit.Rect("Stars", panel);
                MenuKit.Stretch(s, 8f, 8f, 8f, 8f);
                var raw = s.gameObject.AddComponent<RawImage>();
                raw.texture = ShopTextures.Stars;
                raw.uvRect = new Rect(0f, 0f, W / 256f, H / 256f);
                raw.color = new Color(0.75f, 0.9f, 1f, 0.55f);
                raw.raycastTarget = false;
                s.SetSiblingIndex(2);
            }

            private static void AddHover(GameObject go, Action enter, Action exit)
            {
                if (go.GetComponent<Graphic>() == null) go.AddComponent<Image>().color = Color.clear;
                var h = go.AddComponent<Hover>();
                h.Enter = enter;
                h.Exit = exit;
            }

            private static Font UiFont()
            {
                CardManager cm = CardManager.instance;
                if (cm != null && cm.titles != null && cm.titles.Length > 0 && cm.titles[0] != null) return cm.titles[0].font;
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        private sealed class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action Enter, Exit;
            public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
            public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
        }
    }

    /// <summary>Procedural textures of the shop window: a rounded 9-slice, the scalloped awning stripes, a star field.</summary>
    internal static class ShopTextures
    {
        private static Sprite round;
        private static Texture2D stars;
        private static readonly Dictionary<string, Texture2D> valances = new Dictionary<string, Texture2D>();

        /// <summary>White rounded rectangle (32 px, radius 10), sliced with a 12 px border.</summary>
        public static Sprite Round
        {
            get
            {
                if (round != null) return round;
                const int n = 32, r = 10;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "TR_ShopRound" };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
                    }
                tex.Apply();
                round = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
                return round;
            }
        }

        /// <summary>
        /// Eight awning stripes (alternating colours) with a scalloped bottom edge and a soft shade under the rod; repeats
        /// horizontally. 512 x 80 px.
        /// </summary>
        public static Texture2D Valance(Color a, Color b)
        {
            string key = ColorUtility.ToHtmlStringRGB(a) + ColorUtility.ToHtmlStringRGB(b);
            if (valances.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
            const int w = 512, h = 80, stripe = 64, radius = 30;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "TR_ShopValance" };
            for (int x = 0; x < w; x++)
            {
                int i = x / stripe;
                Color c = i % 2 == 0 ? a : b;
                float local = x % stripe + 0.5f - stripe / 2f;
                // Scallop: each stripe ends in a half circle below the straight part.
                float edge = radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - local * local)) * 0.55f;
                for (int y = 0; y < h; y++)
                {
                    float fromBottom = y + 0.5f;
                    float alpha = Mathf.Clamp01(fromBottom - edge + 0.5f);
                    // Folds: darker towards the stripe edges, a shade under the rod and along the scallop.
                    float fold = 1f - 0.12f * Mathf.Pow(Mathf.Abs(local) / (stripe / 2f), 2f);
                    float shade = (y > h - 10 ? 0.82f : 1f) * (fromBottom - edge < 5f ? 0.85f : 1f);
                    Color px = c * fold * shade;
                    px.a = alpha;
                    tex.SetPixel(x, y, px);
                }
            }
            tex.Apply();
            valances[key] = tex;
            return tex;
        }

        /// <summary>Scattered small stars (white, some brighter with a cross), tiling, 256 px.</summary>
        public static Texture2D Stars
        {
            get
            {
                if (stars != null) return stars;
                const int n = 256;
                stars = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "TR_ShopStars" };
                var clear = new Color[n * n];
                stars.SetPixels(clear);
                var rng = new System.Random(7);
                for (int k = 0; k < 70; k++)
                {
                    int x = rng.Next(n), y = rng.Next(n);
                    float a = 0.35f + (float)rng.NextDouble() * 0.65f;
                    stars.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    if (k % 9 == 0)
                        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                            stars.SetPixel((x + dx + n) % n, (y + dy + n) % n, new Color(1f, 1f, 1f, a * 0.5f));
                }
                stars.Apply();
                return stars;
            }
        }
    }
}
