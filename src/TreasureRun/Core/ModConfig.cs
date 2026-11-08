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

        public static void Bind(ConfigFile config)
        {
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
            DebugMythicalWeight = config.Bind("Debug", "MythicalWeight", 0f,
                "Draft weight of Mythical treasures (normal play: 0, they never roll). Other rarities weigh roughly 1-55.");
        }
    }
}
