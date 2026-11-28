using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TreasureRun.Ascensions;
using TreasureRun.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TreasureRun.Pois
{
    /// <summary>
    /// The Laboratory's research (docs/poi-plan.md §9): waves of research per kind of tower or building, kept in the run
    /// (<see cref="RunState.Counters"/>) whatever is in a chamber later. Attacking towers get attack speed and the Lookout
    /// a stronger mark (both through the <see cref="StatLayer"/>); buildings yield more (<see cref="LabYieldPatches"/>):
    /// siphons and banks more mana, Haunted Houses more gold, Mines a higher repair chance, Universities higher odds.
    /// </summary>
    public static class LabResearch
    {
        private const string Prefix = "lab_research:";
        private static bool registered;

        public static int Waves(AscensionTarget t) => TreasureRunMode.Run != null ? TreasureRunMode.Run.GetCounter(Prefix + t) : 0;

        public static ResearchKind KindOf(AscensionTarget t) =>
            AscensionTargets.IsBuilding(t) ? ResearchKind.Output : t == AscensionTarget.Lookout ? ResearchKind.Mark : ResearchKind.AttackSpeed;

        /// <summary>"+35% attack speed", "+35% mark" or "+35% output".</summary>
        public static string Describe(AscensionTarget t) => LaboratoryLogic.Describe(KindOf(t), Waves(t));

        /// <summary>Every kind with research, e.g. "Ballista +35% attack speed, Mana Siphon +15% output" (empty if none).</summary>
        public static string Summary() =>
            string.Join(", ", AscensionTargets.All.Where(t => Waves(t) > 0).Select(t => $"{AscensionTargets.DisplayName(t)} {Describe(t)}"));

        /// <summary>One more wave of research for a kind; the towers update.</summary>
        public static void Advance(AscensionTarget t)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null) return;
            run.SetCounter(Prefix + t, Waves(t) + 1);
            Changed();
        }

        /// <summary>Sets a kind's research (dev tools and scenarios).</summary>
        public static void Set(AscensionTarget t, int waves)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null) return;
            run.SetCounter(Prefix + t, Math.Max(0, waves));
            Changed();
        }

        public static void Changed()
        {
            Register();
            StatLayer.Refresh();
        }

        /// <summary>The attack speed and mark provider (registered with the first Laboratory).</summary>
        public static void Register()
        {
            if (registered) return;
            registered = true;
            StatLayer.Register("Laboratory", (tower, stats) =>
            {
                if (LabSpecimen.Is(tower)) return;
                AscensionTarget? target = AscensionTargets.FromTowerType(tower.towerType);
                if (!target.HasValue) return;
                int waves = Waves(target.Value);
                if (waves <= 0) return;
                float bonus = (float)LaboratoryLogic.Bonus(waves);
                if (KindOf(target.Value) == ResearchKind.Mark)
                {
                    // The Lookout's mark adds its damage and multipliers to every hit on the marked monster.
                    stats.Percent(TowerStat.Damage, bonus);
                    stats.Percent(TowerStat.HealthMult, bonus);
                    stats.Percent(TowerStat.ArmorMult, bonus);
                    stats.Percent(TowerStat.ShieldMult, bonus);
                }
                else stats.Percent(TowerStat.AttackSpeed, bonus);
            });
        }

        /// <summary>Research waves of a building's kind (0 for anything else, a specimen, or outside Treasure Run).</summary>
        public static int WavesOf(Component building)
        {
            if (!TreasureRunMode.Active || building == null || LabSpecimen.Is(building)) return 0;
            AscensionTarget? t = AscensionTargets.Of(building.gameObject);
            return t.HasValue && KindOf(t.Value) == ResearchKind.Output ? Waves(t.Value) : 0;
        }

        private static readonly Dictionary<int, double> carries = new Dictionary<int, double>();

        /// <summary>
        /// The extra mana a siphon or bank gathers on top of <paramref name="gathered"/> (a fraction is carried to its
        /// next tick), added to the mana now. Called after the vanilla or Ley Line gather.
        /// </summary>
        public static void ExtraMana(Component building, int gathered)
        {
            int waves = WavesOf(building);
            if (waves <= 0 || gathered <= 0 || ResourceManager.instance == null) return;
            int id = building.gameObject.GetInstanceID();
            carries.TryGetValue(id, out double carry);
            int extra = LaboratoryLogic.Yield(gathered, waves, ref carry) - gathered;
            carries[id] = carry;
            if (extra > 0) ResourceManager.instance.AddMana(extra);
        }
    }

    /// <summary>
    /// Hooks of the Laboratory: building into a chamber (and the build ghost finding it past the lab's boxes); the
    /// specimen kept inert (no stats, so no Radar plane and no Dropper drop points).
    /// </summary>
    [HarmonyPatch]
    internal static class LabPatches
    {
        /// <summary>
        /// A placement (click, shift-click, paint stroke) on an empty chamber goes into the lab; anything else is vanilla.
        /// Runs before the War Blimp's hook and leaves a deck to it.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.Build)), HarmonyPriority(Priority.High)]
        private static bool BuildPrefix(BuildingManager __instance)
        {
            if (!TreasureRunMode.Active || __instance.currentGhost == null || !__instance.currentGhost.activeSelf || BlimpDeckPatches.OverDeck) return true;
            Laboratory lab = Laboratory.SlotAt(__instance.currentGhost.transform.position);
            if (lab == null || !lab.Empty) return true;
            GameObject go = lab.Insert(__instance.thingToBuild, __instance.buildingCost);
            if (go == null) return true;
            Object.Instantiate(__instance.placementFXObject, go.transform.position + Vector3.up * 0.333f, Quaternion.identity);
            __instance.buildSpotAvailable = __instance.SamplePoint();
            return false;
        }

        /// <summary>
        /// Build mode: a mouse ray aimed at an empty chamber past the lab's tall boxes shows the ghost in the chamber
        /// (vanilla would stop at the box: not grass, no ghost). Runs after the War Blimp's deck hook and leaves a deck to it.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.SamplePoint)), HarmonyPriority(Priority.Low)]
        private static bool SamplePointPrefix(BuildingManager __instance, ref bool __result)
        {
            if (!TreasureRunMode.Active || Laboratory.All.Count == 0 || Camera.main == null || BlimpDeckPatches.OverDeck) return true;
            if (!SampleChamber(__instance, Camera.main.ScreenPointToRay(Input.mousePosition))) return true;
            __result = true;
            return false;
        }

        /// <summary>Shows the build ghost in the empty chamber a ray aims at (the mouse's, or a scenario's); false if none.</summary>
        internal static bool SampleChamber(BuildingManager bm, Ray ray)
        {
            Laboratory lab = Laboratory.ChamberUnder(ray, bm.buildableMask);
            if (lab == null) return false;
            bm.DisplayGhost(lab.Slot, lab.Slot.y > 0.34f ? "+" + (Mathf.RoundToInt(lab.Slot.y * 3f) - 1) : "");
            return true;
        }

    }

    /// <summary>
    /// What studied buildings yield: siphons and banks gather more mana (Ley Line siphons call
    /// <see cref="LabResearch.ExtraMana"/> from their own tick), Haunted Houses tax at a higher rate, Mines repair more
    /// often, Universities roll with better odds. Each by <see cref="LaboratoryLogic.Bonus"/> of its kind's research.
    /// </summary>
    [HarmonyPatch]
    internal static class LabYieldPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(ManaSiphon), "Gather")]
        private static void SiphonGather(ManaSiphon __instance) => LabResearch.ExtraMana(__instance, __instance.gatherRatePerSec);

        [HarmonyPostfix, HarmonyPatch(typeof(ManaBank), "Gather")]
        private static void BankGather(ManaBank __instance) => LabResearch.ExtraMana(__instance, __instance.gatherRatePerSec);

        /// <summary>The tax is computed inside: raise the rate for this house's collection only.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(HauntedHouse), nameof(HauntedHouse.GenerateIncome))]
        private static void HouseIncomePrefix(HauntedHouse __instance, out float __state)
        {
            __state = GameManager.instance != null ? GameManager.instance.hauntedHouseTaxRate : 0f;
            int waves = LabResearch.WavesOf(__instance);
            if (waves > 0 && GameManager.instance != null)
                GameManager.instance.hauntedHouseTaxRate = (float)(__state * (1 + LaboratoryLogic.Bonus(waves)));
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HauntedHouse), nameof(HauntedHouse.GenerateIncome))]
        private static void HouseIncomePostfix(float __state)
        {
            if (GameManager.instance != null) GameManager.instance.hauntedHouseTaxRate = __state;
        }

        /// <summary>Vanilla: a 10% chance to repair 1 health at a wave start; researched, a higher chance.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Mine), nameof(Mine.Repair))]
        private static bool MineRepairPrefix(Mine __instance)
        {
            int waves = LabResearch.WavesOf(__instance);
            if (waves <= 0) return true;
            if (__instance.gathering && UnityEngine.Random.Range(0f, 1f) < LaboratoryLogic.Chance(0.1, waves)) GameManager.instance.RepairTower(10);
            return false;
        }

        /// <summary>The three rolls use the university's percentages plus the card bonus: scale their sum for this research.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(University), nameof(University.Research))]
        private static void UniversityPrefix(University __instance, out int[] __state)
        {
            __state = null;
            int waves = LabResearch.WavesOf(__instance);
            if (waves <= 0 || GameManager.instance == null) return;
            __state = new[] { __instance.healthPercent, __instance.armorPercent, __instance.shieldPercent };
            int card = GameManager.instance.universityBonus;
            int Scaled(int pct) => (int)Math.Round((pct + card) * (1 + LaboratoryLogic.Bonus(waves))) - card;
            __instance.healthPercent = Scaled(__state[0]);
            __instance.armorPercent = Scaled(__state[1]);
            __instance.shieldPercent = Scaled(__state[2]);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(University), nameof(University.Research))]
        private static void UniversityPostfix(University __instance, int[] __state)
        {
            if (__state == null) return;
            __instance.healthPercent = __state[0];
            __instance.armorPercent = __state[1];
            __instance.shieldPercent = __state[2];
        }
    }

    /// <summary>A specimen's stats are never computed: vanilla SetStats and every override (the Radar's spawns its plane).</summary>
    [HarmonyPatch]
    internal static class LabSpecimenStatsPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Tower), nameof(Tower.SetStats));
            foreach (Type t in typeof(Tower).Assembly.GetTypes())
            {
                if (!t.IsSubclassOf(typeof(Tower))) continue;
                MethodInfo m = t.GetMethod(nameof(Tower.SetStats), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                                           null, Type.EmptyTypes, null);
                if (m != null) yield return m;
            }
        }

        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Tower __instance) => !LabSpecimen.Is(__instance);
    }
}
