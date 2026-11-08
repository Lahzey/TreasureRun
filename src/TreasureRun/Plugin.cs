using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TreasureRun.Core;
using TreasureRun.UI;

namespace TreasureRun
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "arno.roguetower.treasurerun";
        public const string Name = "Treasure Run";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log { get; private set; }

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            ModConfig.Bind(Config);

            // Order matters for run-start handlers: TreasureInventory resets GameEvents first.
            Treasures.TreasureRegistry.RegisterAll();
            Treasures.TreasureInventory.Initialize();
            Patches.CombatPatches.Initialize();
            Patches.TowerStatPatches.Initialize();
            Treasures.TreasureRegistry.HookAll();
            Cards.CustomCards.RegisterAll();
            Cards.CardEffectPatches.Initialize();
            CardPanel.Initialize();
            TreasureOfferUI.Initialize();
            InventoryBar.Initialize();
            Voxels.ModelOverrides.Initialize();

            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);

            // Own persistent host object: some games destroy BepInEx's manager object on scene load.
            var host = new UnityEngine.GameObject("TreasureRun.Host");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
            host.AddComponent<DevHud>();
            host.AddComponent<DebugTools>();
            host.AddComponent<Treasures.Effects.TreasureTicker>();

            Log.LogInfo($"{Name} {Version} loaded, {System.Linq.Enumerable.Count(harmony.GetPatchedMethods())} methods patched.");
        }
    }
}
