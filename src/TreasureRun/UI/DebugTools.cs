using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Castles;
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

        private bool autoStarted;
        private float autoStartAt = -1f;

        private void Awake()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
            {
                int lanes = ModConfig.DebugAutoStartGameMode.Value;
                if (scene.name == "MainMenu" && !autoStarted && lanes >= 1 && lanes <= 4) autoStartAt = Time.realtimeSinceStartup + 2f;
                if (scene.name == "MainMenu" && ModConfig.DebugAutoOpenPlayMenu.Value) openPlayMenuAt = Time.realtimeSinceStartup + 1.5f;
                if (scene.name == "GameScene" && ModConfig.DebugScreenshotTour.Value) StartCoroutine(GameTour());
            };
        }

        private void AutoStartRun()
        {
            autoStartAt = -1f;
            autoStarted = true;
            if (LevelLoader.instance == null) return;
            Plugin.Log.LogInfo($"[Debug] Auto-starting a Treasure Run ({ModConfig.DebugAutoStartGameMode.Value} lanes).");
            TreasureRunMode.Selected = true;
            PlayerPrefs.SetInt("GameMode", Mathf.Clamp(ModConfig.DebugAutoStartGameMode.Value, 1, 4));
            CastleDef castle = CastleRegistry.Get(ModConfig.DebugAutoStartCastle.Value);
            if (castle != null) CastleRuntime.Selected = castle;
            LevelLoader.instance.LoadLevel("GameScene");
        }

        private float openPlayMenuAt = -1f;

        /// <summary>Opens the play menu like the main menu's Play button would (dev convenience).</summary>
        private void OpenPlayMenu()
        {
            openPlayMenuAt = -1f;
            PlayMenu playMenu = Resources.FindObjectsOfTypeAll<PlayMenu>().FirstOrDefault(p => p != null && p.gameObject.scene.IsValid());
            if (playMenu == null) return;
            Transform canvas = playMenu.transform.parent;
            Transform mainPanel = canvas != null ? canvas.Find("MainMenu") : null;
            if (mainPanel != null) mainPanel.gameObject.SetActive(false);
            playMenu.gameObject.SetActive(true);
            Plugin.Log.LogInfo("[Debug] Play menu opened.");
            if (ModConfig.DebugScreenshotTour.Value) StartCoroutine(ScreenshotTour(playMenu));
        }

        private static string ScreensDir()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "screens");
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Game scene tour: screenshot at start, start a wave without towers, screenshot and state log later.</summary>
        private System.Collections.IEnumerator GameTour()
        {
            yield return new WaitForSecondsRealtime(4f);
            string id = CastleRuntime.Active?.Id ?? "none";
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScreensDir(), $"game_{id}_start.png"));
            yield return new WaitForSecondsRealtime(1f);
            LogState("start");

            if (id == "castle_town") PlaceTowerNextToCastle();

            TileSpawnLocation spot = FindObjectsOfType<TileSpawnLocation>().FirstOrDefault();
            if (spot != null) spot.SpawnTile();
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSecondsRealtime(5f);
                LogState($"wave +{(i + 1) * 5}s");
            }
            IncomeGenerator castleIncome = CastleRuntime.Behaviour?.CastleObject?.GetComponent<IncomeGenerator>();
            if (castleIncome != null)
                Plugin.Log.LogInfo($"[Debug] Castle income: rate {castleIncome.incomeTimesLevel} x level, net {castleIncome.netGold}g.");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ScreensDir(), $"game_{id}_wave.png"));
            yield return new WaitForSecondsRealtime(1f);
            Plugin.Log.LogInfo("[Debug] Game tour done.");
        }

        /// <summary>Dev test: builds the first build-button tower on a free grass cell next to the castle (free of charge).</summary>
        private static void PlaceTowerNextToCastle()
        {
            BuildButtonUI button = FindObjectsOfType<BuildButtonUI>().FirstOrDefault(b => b.tower != null && b.tower.GetComponent<Tower>() != null);
            if (button == null || BuildingManager.instance == null) return;
            int grass = LayerMask.NameToLayer("Grass");
            foreach (var (x, z) in new[] { (2, 0), (-2, 0), (0, 2), (0, -2), (2, 1), (-2, 1) })
            {
                if (!Physics.Raycast(new Vector3(x, 30f, z), Vector3.down, out RaycastHit hit, 60f, BuildingManager.instance.buildableMask)) continue;
                if (hit.collider.gameObject.layer != grass) continue;
                GameObject t = Instantiate(button.tower, new Vector3(x, Mathf.Round(3f * hit.point.y) / 3f, z), Quaternion.identity);
                t.GetComponent<IBuildable>()?.SetStats();
                Plugin.Log.LogInfo($"[Debug] Placed {button.tower.name} at {x},{z}.");
                return;
            }
        }

        private static void LogState(string label)
        {
            if (GameManager.instance == null || ResourceManager.instance == null || SpawnManager.instance == null) return;
            Plugin.Log.LogInfo($"[Debug] {label}: level {SpawnManager.instance.level}, health {GameManager.instance.health / 10}/{GameManager.instance.maxHealth / 10}, " +
                               $"gold {ResourceManager.instance.gold}, mana {ResourceManager.instance.mana}/{ResourceManager.instance.maxMana}, " +
                               $"enemies {SpawnManager.instance.currentEnemies.Count}, game over {GameManager.instance.gameOver}, " +
                               $"crystals {FindObjectsOfType<ManaCrystal>().Length}");
        }

        /// <summary>Selects every castle in the new play menu and saves a screenshot of each (plugin/screens/).</summary>
        private System.Collections.IEnumerator ScreenshotTour(PlayMenu playMenu)
        {
            yield return new WaitForSecondsRealtime(2f);
            var overhaul = playMenu.GetComponent<PlayMenuOverhaul>();
            if (overhaul == null) yield break;
            string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "screens");
            System.IO.Directory.CreateDirectory(dir);
            CastleDef previous = CastleRuntime.Selected;
            for (int i = 0; i < CastleRegistry.All.Count; i++)
            {
                overhaul.SelectCastle(i);
                yield return new WaitForSecondsRealtime(1f);
                string file = System.IO.Path.Combine(dir, $"menu_{CastleRegistry.All[i].Id}.png");
                ScreenCapture.CaptureScreenshot(file);
                yield return new WaitForSecondsRealtime(0.5f);
                Plugin.Log.LogInfo($"[Debug] Screenshot: {file}");
            }
            overhaul.SelectCastle(Mathf.Max(0, CastleRegistry.All.ToList().IndexOf(previous)));
        }

        private void Update()
        {
            if (autoStartAt > 0f && Time.realtimeSinceStartup >= autoStartAt) AutoStartRun();
            if (openPlayMenuAt > 0f && Time.realtimeSinceStartup >= openPlayMenuAt) OpenPlayMenu();

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
            if (Input.GetKeyDown(ModConfig.DebugModelKey.Value)) ReloadModels();
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

        private readonly List<GameObject> previewObjects = new List<GameObject>();

        /// <summary>Reload .vox files, re-apply overrides, respawn preview models in a row next to the castle.</summary>
        private void ReloadModels()
        {
            foreach (GameObject go in previewObjects) if (go != null) Destroy(go);
            previewObjects.Clear();
            Voxels.ModelOverrides.RestoreAll();
            Voxels.VoxelLibrary.ClearCache();
            ModelIcons.ClearCache();
            Audio.SoundLibrary.ClearCache();
            Ascensions.Phase3.MorphData.ClearCache();
            Voxels.ModelOverrides.ApplyAll();

            string[] names = (ModConfig.DebugPreviewModels.Value ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(n => n.Trim()).ToArray();
            Vector3 basePos = GameHelpers.CastlePosition + new Vector3(4f, 0f, 0f);
            for (int i = 0; i < names.Length; i++)
            {
                GameObject go = Voxels.VoxelLibrary.Spawn(names[i], basePos + new Vector3(2.5f * i, 0f, 0f), Quaternion.identity);
                if (go != null) previewObjects.Add(go);
            }
            Plugin.Log.LogInfo($"[Debug] Models reloaded. Available: {string.Join(", ", Voxels.VoxelLibrary.AvailableNames())}");
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
