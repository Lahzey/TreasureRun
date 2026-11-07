using BepInEx.Configuration;

namespace TreasureRun.Core
{
    /// <summary>BepInEx config entries (editable in-game via ConfigurationManager, F1).</summary>
    internal static class ModConfig
    {
        public static ConfigEntry<bool> ShowDevHud { get; private set; }

        public static void Bind(ConfigFile config)
        {
            ShowDevHud = config.Bind("Debug", "ShowDevHud", true,
                "Show a small overlay with Treasure Run state while a run is active.");
        }
    }
}
