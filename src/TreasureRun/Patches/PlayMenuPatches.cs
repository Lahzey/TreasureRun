using HarmonyLib;
using TreasureRun.UI;

namespace TreasureRun.Patches
{
    /// <summary>Replaces the vanilla play menu with <see cref="PlayMenuOverhaul"/> the first time it opens.</summary>
    [HarmonyPatch]
    internal static class PlayMenuPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(PlayMenu), nameof(PlayMenu.Start))]
        private static void PlayMenuStartPostfix(PlayMenu __instance)
        {
            try
            {
                PlayMenuOverhaul.Build(__instance);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Play menu overhaul failed, vanilla menu stays: {e}");
            }
        }
    }
}
