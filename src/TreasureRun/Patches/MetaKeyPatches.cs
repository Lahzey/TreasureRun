using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Patches
{
    /// <summary>
    /// Redirects PlayerPrefs.GetInt/SetInt calls in GameScene setup and record methods to <see cref="MetaKeys"/>.
    /// This is how Treasure Run ignores the skill tree (start gold, castle HP, card draw size/frequency,
    /// per-tower bonuses, card unlocks, starting developments) and keeps separate records.
    /// </summary>
    [HarmonyPatch]
    internal static class MetaKeyPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GameManager), nameof(GameManager.Start));
            yield return AccessTools.Method(typeof(GameManager), nameof(GameManager.GameOver));
            yield return AccessTools.Method(typeof(GameManager), nameof(GameManager.EndlessGameOver));
            yield return AccessTools.Method(typeof(GameManager), nameof(GameManager.Victory));
            yield return AccessTools.Method(typeof(GameManager), nameof(GameManager.SetClassRecords));
            yield return AccessTools.Method(typeof(CardManager), nameof(CardManager.Start));
            yield return AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.Start));
            yield return AccessTools.Method(typeof(SpawnManager), nameof(SpawnManager.Start));
            yield return AccessTools.Method(typeof(TreasureUI), nameof(TreasureUI.Start));
            yield return AccessTools.Method(typeof(UpgradeCard), nameof(UpgradeCard.Awake));
            yield return AccessTools.Method(typeof(DevelopmentManager), nameof(DevelopmentManager.Start));
        }

        private static readonly MethodInfo GetInt2 = AccessTools.Method(typeof(PlayerPrefs), nameof(PlayerPrefs.GetInt), new[] { typeof(string), typeof(int) });
        private static readonly MethodInfo GetInt1 = AccessTools.Method(typeof(PlayerPrefs), nameof(PlayerPrefs.GetInt), new[] { typeof(string) });
        private static readonly MethodInfo SetInt = AccessTools.Method(typeof(PlayerPrefs), nameof(PlayerPrefs.SetInt), new[] { typeof(string), typeof(int) });

        private static readonly MethodInfo MetaGetInt2 = AccessTools.Method(typeof(MetaKeys), nameof(MetaKeys.GetInt), new[] { typeof(string), typeof(int) });
        private static readonly MethodInfo MetaGetInt1 = AccessTools.Method(typeof(MetaKeys), nameof(MetaKeys.GetInt), new[] { typeof(string) });
        private static readonly MethodInfo MetaSetInt = AccessTools.Method(typeof(MetaKeys), nameof(MetaKeys.SetInt), new[] { typeof(string), typeof(int) });

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            int replaced = 0;
            foreach (CodeInstruction ins in instructions)
            {
                if (ins.Calls(GetInt2)) { ins.operand = MetaGetInt2; replaced++; }
                else if (ins.Calls(GetInt1)) { ins.operand = MetaGetInt1; replaced++; }
                else if (ins.Calls(SetInt)) { ins.operand = MetaSetInt; replaced++; }
                yield return ins;
            }
            if (replaced == 0) Plugin.Log.LogWarning($"MetaKeyPatches: no PlayerPrefs calls found in {original.DeclaringType?.Name}.{original.Name}");
        }
    }
}
