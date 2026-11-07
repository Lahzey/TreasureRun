using HarmonyLib;

namespace TreasureRun.Patches
{
    /// <summary>
    /// M0 smoke test: proves Harmony patching and publicized access work.
    /// Remove once real patches exist.
    /// </summary>
    [HarmonyPatch]
    internal static class SmokeTestPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(PlayMenu), nameof(PlayMenu.Start))]
        private static void PlayMenuStart(PlayMenu __instance)
        {
            // monsterModeToggle is a private field; readable thanks to the publicizer.
            Plugin.Log.LogInfo($"[SmokeTest] PlayMenu opened. MonsterMode toggle = {__instance.monsterModeToggle.isOn}");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CardManager), nameof(CardManager.Start))]
        private static void CardManagerStart(CardManager __instance)
        {
            Plugin.Log.LogInfo($"[SmokeTest] GameScene started. Card pool: {__instance.availableCards.Count} cards, draw size {__instance.baseCardDraw}.");
        }
    }
}
