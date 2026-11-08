using System.Collections.Generic;
using System.Linq;
using TreasureRun.Castles;
using TreasureRun.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.UI
{
    /// <summary>
    /// New play menu, built on its own overlay canvas and shown/hidden with the vanilla PlayMenu object:
    ///  - left: selected castle (name, description, health, availability),
    ///  - right: Treasure Run toggle, 2x2 lane selection (remembered), challenge modes; Back top-left (also Esc),
    ///  - bottom: Start button above a castle carousel. The background castle shows the selected castle.
    /// Vanilla logic is reused: lane start via PlayMenu.StartGame, challenge modes via the (hidden) vanilla toggles,
    /// Back via the vanilla button's onClick.
    /// </summary>
    internal sealed class PlayMenuOverhaul : MonoBehaviour
    {
        private static readonly string[] LaneNames = { "Single", "Double", "Triple", "Quadruple" };

        private PlayMenu menu;
        private MenuStyle style;
        private GameObject canvasRoot;

        // Left
        private Text castleTitle, castleDescription, castleHealth, castleAvailability;
        // Right
        private MenuButton treasureRunButton;
        private readonly MenuButton[] laneButtons = new MenuButton[4];
        private readonly List<(Toggle vanilla, MenuButton button)> challenges = new List<(Toggle, MenuButton)>();
        // Bottom
        private MenuButton startButton;
        private MenuButton prevCard, activeCard, nextCard;

        private int castleIndex;
        private Button vanillaBack;

        public static void Build(PlayMenu menu)
        {
            if (menu.GetComponent<PlayMenuOverhaul>() != null) return;
            menu.gameObject.AddComponent<PlayMenuOverhaul>().Create(menu);
        }

        private void Create(PlayMenu playMenu)
        {
            menu = playMenu;
            Transform scaler = menu.monsterModeToggle.transform.parent.parent;  // PlayMenu/Scaler
            Transform challengePanel = menu.monsterModeToggle.transform.parent;
            Button vanillaBack = scaler.Find("BackButton")?.GetComponent<Button>();

            style = new MenuStyle
            {
                Font = scaler.GetComponentInChildren<Text>(true).font,
                PanelSprite = challengePanel.GetComponent<Image>()?.sprite,
                PanelPpum = challengePanel.GetComponent<Image>()?.pixelsPerUnitMultiplier ?? 5f,
                ButtonSprite = vanillaBack != null ? vanillaBack.GetComponent<Image>().sprite : null,
                ButtonPpum = vanillaBack != null ? vanillaBack.GetComponent<Image>().pixelsPerUnitMultiplier : 7f,
            };

            BuildCanvas();
            BuildLeftPanel();
            BuildRightPanel(vanillaBack);
            BuildBottom();
            BuildBackButton();

            scaler.gameObject.SetActive(false); // vanilla lane buttons, challenge panel, back button

            castleIndex = Mathf.Max(0, CastleRegistry.All.ToList().IndexOf(CastleRuntime.Selected));
            var link = gameObject.AddComponent<ActiveLink>();
            link.Target = canvasRoot;
            link.OnShown = Refresh;
            canvasRoot.SetActive(gameObject.activeInHierarchy);
            Refresh();
            Plugin.Log.LogInfo("Play menu overhaul built.");
        }

        // ------------------------------------------------------------------ layout

        private void BuildCanvas()
        {
            canvasRoot = new GameObject("TR_PlayMenu");
            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scalerComp = canvasRoot.AddComponent<CanvasScaler>();
            scalerComp.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scalerComp.referenceResolution = new Vector2(1920f, 1080f);
            scalerComp.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<GraphicRaycaster>();
        }

        private RectTransform Panel(string name, float anchorMinX, float anchorMaxX, float pivotX)
        {
            RectTransform rt = MenuKit.Rect(name, canvasRoot.transform);
            rt.anchorMin = new Vector2(anchorMinX, 0.5f);
            rt.anchorMax = new Vector2(anchorMaxX, 0.5f);
            rt.pivot = new Vector2(pivotX, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 40f); // slightly above centre, clear of the carousel
            MenuKit.Sliced(rt.gameObject, style.PanelSprite, style.PanelPpum, Color.white);
            MenuKit.Vertical(rt.gameObject, 26, 12);
            MenuKit.FitContent(rt.gameObject, horizontal: false, vertical: true);
            return rt;
        }

        private void BuildLeftPanel()
        {
            RectTransform panel = Panel("CastleInfo", 0.05f, 0.29f, 0f);
            castleTitle = MenuKit.Label(panel, style, "", 34, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuKit.Divider(panel, style);
            castleDescription = MenuKit.Label(panel, style, "", 21, TextAnchor.UpperLeft);
            MenuKit.Divider(panel, style);
            castleHealth = MenuKit.Label(panel, style, "", 23, TextAnchor.MiddleLeft);
            castleAvailability = MenuKit.Label(panel, style, "", 19, TextAnchor.MiddleLeft, FontStyle.Italic);
        }

        private void BuildRightPanel(Button vanillaBack)
        {
            RectTransform panel = Panel("RunSettings", 0.71f, 0.95f, 1f);

            treasureRunButton = MenuKit.Button(panel, style, "Treasure Run", 28, 64, () =>
            {
                TreasureRunMode.Selected = !TreasureRunMode.Selected;
                Click();
                Refresh();
            });

            Header(panel, "Defense Mode");
            RectTransform grid = MenuKit.Rect("Lanes", panel);
            MenuKit.Vertical(grid.gameObject, 0, 10);
            for (int row = 0; row < 2; row++)
            {
                RectTransform r = MenuKit.Rect("Row", grid);
                MenuKit.Horizontal(r.gameObject, 10);
                for (int col = 0; col < 2; col++)
                {
                    int lanes = row * 2 + col + 1;
                    laneButtons[lanes - 1] = MenuKit.Button(r, style, LaneNames[lanes - 1], 23, 84, () =>
                    {
                        PlayerPrefs.SetInt("GameMode", lanes);
                        Click();
                        Refresh();
                    });
                    MenuKit.Size(laneButtons[lanes - 1].gameObject, flexWidth: 1);
                }
            }

            Header(panel, "Challenge Modes");
            foreach (Toggle vanilla in new[] { menu.monsterModeToggle, menu.splitModeToggle, menu.firstPersonModeToggle })
            {
                if (vanilla == null) continue;
                string text = vanilla.GetComponentInChildren<Text>(true)?.text ?? vanilla.name;
                Toggle captured = vanilla;
                MenuButton b = MenuKit.Button(panel, style, text, 21, 50, () =>
                {
                    captured.isOn = !captured.isOn; // runs the vanilla handler (PlayerPrefs + click sound)
                    Refresh();
                });
                challenges.Add((vanilla, b));
            }

            this.vanillaBack = vanillaBack;
        }

        /// <summary>Back button in the top-left corner (Esc does the same).</summary>
        private void BuildBackButton()
        {
            if (vanillaBack == null) return;
            MenuButton back = MenuKit.Button(canvasRoot.transform, style, "Back", 26, 60, GoBack);
            var rt = (RectTransform)back.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(36f, -32f);
            rt.sizeDelta = new Vector2(190f, 60f);
        }

        private void GoBack()
        {
            if (vanillaBack != null) vanillaBack.onClick.Invoke();
        }

        private void Update()
        {
            if (canvasRoot != null && canvasRoot.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape)) GoBack();
        }

        private void Header(Transform parent, string text)
        {
            MenuKit.Size(MenuKit.Label(parent, style, text, 26, TextAnchor.MiddleCenter, FontStyle.Bold).gameObject, prefHeight: 40);
            MenuKit.Divider(parent, style);
        }

        private void BuildBottom()
        {
            RectTransform group = MenuKit.Rect("StartAndCastles", canvasRoot.transform);
            group.anchorMin = group.anchorMax = new Vector2(0.5f, 0f);
            group.pivot = new Vector2(0.5f, 0f);
            group.anchoredPosition = new Vector2(0f, 28f);
            VerticalLayoutGroup v = MenuKit.Vertical(group.gameObject, 0, 14, TextAnchor.LowerCenter);
            v.childForceExpandWidth = false;
            MenuKit.FitContent(group.gameObject, horizontal: true, vertical: true);

            startButton = MenuKit.Button(group, style, "Start", 36, 78, StartGame);
            MenuKit.Size(startButton.gameObject, prefWidth: 360);
            startButton.Background.color = new Color(0.72f, 0.95f, 0.62f);

            RectTransform row = MenuKit.Rect("Carousel", group);
            HorizontalLayoutGroup h = MenuKit.Horizontal(row.gameObject, 16, TextAnchor.MiddleCenter, expandWidth: false);
            h.childControlHeight = true;

            MenuButton left = MenuKit.Button(row, style, "<", 34, 90, () => Step(-1));
            MenuKit.Size(left.gameObject, prefWidth: 60);
            prevCard = Card(row, 220, 96, () => Step(-1));
            activeCard = Card(row, 270, 118, null);
            nextCard = Card(row, 220, 96, () => Step(1));
            MenuButton right = MenuKit.Button(row, style, ">", 34, 90, () => Step(1));
            MenuKit.Size(right.gameObject, prefWidth: 60);
        }

        private MenuButton Card(Transform parent, float width, float height, UnityEngine.Events.UnityAction onClick)
        {
            MenuButton card = MenuKit.Button(parent, style, "", width > 240 ? 27 : 22, height, onClick ?? (() => { }));
            MenuKit.Size(card.gameObject, prefWidth: width);
            if (onClick == null) card.Button.transition = Selectable.Transition.None;
            return card;
        }

        // ------------------------------------------------------------------ behaviour

        private void Step(int dir)
        {
            int n = CastleRegistry.All.Count;
            if (n == 0) return;
            castleIndex = ((castleIndex + dir) % n + n) % n;
            CastleRuntime.Selected = CastleRegistry.All[castleIndex];
            Click();
            Refresh();
        }

        /// <summary>Selects a castle by carousel index (used by the debug screenshot tour).</summary>
        internal void SelectCastle(int index)
        {
            castleIndex = index;
            CastleRuntime.Selected = CastleRegistry.All[Mathf.Clamp(index, 0, CastleRegistry.All.Count - 1)];
            Refresh();
        }

        private void StartGame()
        {
            CastleContext ctx = CastleContext.FromPrefs();
            if (!CastleRuntime.Selected.AvailableIn(ctx)) return;
            Click();
            canvasRoot.SetActive(false); // let the loading transition show
            menu.StartGame(ctx.Lanes);
        }

        private static void Click()
        {
            if (SFXManager.instance != null) SFXManager.instance.ButtonClick();
        }

        private void Refresh()
        {
            if (menu == null) return;
            CastleContext ctx = CastleContext.FromPrefs();

            treasureRunButton.SetOn(ctx.TreasureRun);

            for (int i = 0; i < 4; i++)
            {
                int lanes = i + 1;
                int record = PlayerPrefs.GetInt((ctx.TreasureRun ? MetaKeys.RecordPrefix : "") + "Record" + lanes, 0);
                laneButtons[i].Label.text = $"{LaneNames[i]}\n<size=17>Best: Level {record}</size>";
                laneButtons[i].SetOn(ctx.Lanes == lanes);
            }

            foreach (var (vanilla, button) in challenges) button.SetOn(vanilla.isOn);

            IReadOnlyList<CastleDef> castles = CastleRegistry.All;
            if (castles.Count == 0) return;
            castleIndex = Mathf.Clamp(castleIndex, 0, castles.Count - 1);
            CastleDef current = castles[castleIndex];
            bool available = current.AvailableIn(ctx);

            castleTitle.text = current.Name;
            castleDescription.text = current.Description;
            castleHealth.text = current.HealthText != null
                ? $"Health: <b>{current.HealthText}</b>"
                : $"Health: <b>{(current.StartingHealth.HasValue ? current.StartingHealth.Value.ToString() : "10")}</b>" +
                  (!current.StartingHealth.HasValue && !ctx.TreasureRun ? "  <size=17>(+ skill tree)</size>" : "");
            castleAvailability.text = available
                ? (current.IsAvailable == null ? "Available in every mode" : "Available with the current settings")
                : $"<color=#8a2a1a>Locked: {current.AvailabilityHint ?? "not available with the current settings"}</color>";

            SetCard(activeCard, current, ctx, true);
            SetCard(prevCard, castles[(castleIndex - 1 + castles.Count) % castles.Count], ctx, false);
            SetCard(nextCard, castles[(castleIndex + 1) % castles.Count], ctx, false);
            bool showSides = castles.Count > 1;
            prevCard.gameObject.SetActive(showSides);
            nextCard.gameObject.SetActive(showSides && castles.Count > 2);

            startButton.SetInteractable(available);
            startButton.Label.text = available ? "Start" : "Locked";

            CastleVisuals.Apply(current);
        }

        private static void SetCard(MenuButton card, CastleDef def, CastleContext ctx, bool active)
        {
            bool available = def.AvailableIn(ctx);
            card.Label.text = available ? def.Name : $"{def.Name}\n<size=15>Locked</size>";
            card.SetOn(active);
            card.Background.color = active ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            card.Label.color = active ? card.Label.color : new Color(0.2f, 0.2f, 0.2f, available ? 0.75f : 0.45f);
        }
    }
}
