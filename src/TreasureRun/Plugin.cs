using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

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
            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{Name} {Version} loaded, {System.Linq.Enumerable.Count(harmony.GetPatchedMethods())} methods patched.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}
