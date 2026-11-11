using BepInEx.Configuration;

namespace TreasureRun.Core
{
    /// <summary>BepInEx config entries (editable in-game via ConfigurationManager, F1).</summary>
    internal static class ModConfig
    {
        public static ConfigEntry<bool> ShowDevHud { get; private set; }
        public static ConfigEntry<bool> DebugTools { get; private set; }
        public static ConfigEntry<UnityEngine.KeyCode> DebugLevelUpKey { get; private set; }
        public static ConfigEntry<UnityEngine.KeyCode> DebugOfferKey { get; private set; }
        public static ConfigEntry<UnityEngine.KeyCode> DebugGrantWindowKey { get; private set; }
        public static ConfigEntry<float> DebugMythicalWeight { get; private set; }
        public static ConfigEntry<int> DebugAutoStartGameMode { get; private set; }
        public static ConfigEntry<bool> DebugAutoOpenPlayMenu { get; private set; }
        public static ConfigEntry<bool> DebugScreenshotTour { get; private set; }
        public static ConfigEntry<string> DebugAutoStartCastle { get; private set; }
        public static ConfigEntry<UnityEngine.KeyCode> DebugModelKey { get; private set; }
        public static ConfigEntry<string> DebugPreviewModels { get; private set; }
        public static ConfigEntry<bool> LoadingAnimation { get; private set; }
        public static ConfigEntry<int> HwachaRockets { get; private set; }

        /// <summary>The plugin's config file (for the dev console's <c>config</c> command).</summary>
        public static ConfigFile File { get; private set; }

        public static void Bind(ConfigFile config)
        {
            File = config;
            LoadingAnimation = config.Bind("General", "LoadingAnimation", true,
                "Play the circle wipe when switching between menu and game. Off: the loading screen appears and disappears " +
                "instantly. (The vanilla 3-second minimum loading time is always removed.) Also in the in-game Options menu.");
            HwachaRockets = config.Bind("Ascensions", "HwachaRockets", 20,
                "Rockets per Hwacha volley (Cannon ascension). The damage per rocket stays the same, so more rockets = more damage.");
            ShowDevHud = config.Bind("Debug", "ShowDevHud", true,
                "Show a small overlay with Treasure Run state while a run is active.");
            DebugTools = config.Bind("Debug", "DebugTools", true,
                "Enable debug hotkeys during a Treasure Run.");
            DebugLevelUpKey = config.Bind("Debug", "LevelUpDrawKey", UnityEngine.KeyCode.F7,
                "Hotkey: show a level-up card draw right now (normal cards, slots can roll treasures).");
            DebugOfferKey = config.Bind("Debug", "OfferTreasureKey", UnityEngine.KeyCode.F8,
                "Hotkey: show a treasure offer (pick 1 of 3) right now.");
            DebugGrantWindowKey = config.Bind("Debug", "GrantWindowKey", UnityEngine.KeyCode.F9,
                "Hotkey: open a window listing all treasures; click one to gain it.");
            DebugAutoStartGameMode = config.Bind("Debug", "AutoStartGameMode", 0,
                "Development: 1-4 = load straight into a Treasure Run with that many lanes when the main menu appears (once per launch). 0 = off.");
            DebugAutoOpenPlayMenu = config.Bind("Debug", "AutoOpenPlayMenu", false,
                "Development: open the play menu automatically when the main menu appears.");
            DebugScreenshotTour = config.Bind("Debug", "ScreenshotTour", false,
                "Development: with AutoOpenPlayMenu, select every castle and save a screenshot of each to plugins/TreasureRun/screens.");
            DebugAutoStartCastle = config.Bind("Debug", "AutoStartCastle", "",
                "Development: castle id used by AutoStartGameMode (empty = current selection). With ScreenshotTour, the run starts " +
                "a wave and saves screenshots of the game scene.");
            DebugModelKey = config.Bind("Debug", "ReloadModelsKey", UnityEngine.KeyCode.F6,
                "Hotkey: reload all .vox models from disk, re-apply model overrides and spawn the preview models next to the castle.");
            DebugPreviewModels = config.Bind("Debug", "PreviewModels", "examples/castle",
                "Comma-separated model names (path inside the models folder, without .vox) spawned next to the castle by the reload hotkey.");
            DebugMythicalWeight = config.Bind("Debug", "MythicalWeight", 0f,
                "Draft weight of Mythical treasures (normal play: 0, they never roll). Other rarities weigh roughly 1-55.");
        }
    }
}
