using HarmonyLib;
using TreasureRun.Core;

namespace TreasureRun.Patches
{
    [HarmonyPatch]
    internal static class RunLifecyclePatches
    {
        /// <summary>
        /// Every scene change in the game goes through LevelLoader.LoadLevel
        /// (PlayMenu.StartGame, GameManager.LoadScene for restart/quit, UpgradeManager reset).
        /// Hooking here sets the mode before any GameScene Awake/Start runs.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(LevelLoader), nameof(LevelLoader.LoadLevel))]
        private static void LoadLevelPrefix(string sceneName)
        {
            TreasureRunMode.OnSceneLoadRequested(sceneName);
        }
    }
}
