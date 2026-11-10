namespace TreasureRun.Ascensions
{
    /// <summary>Registers every ascension (docs/ascension-plan.md). Add new families here.</summary>
    internal static class AscensionCatalog
    {
        public static void RegisterAll()
        {
            PlaneAscensions.Register();
            TestAscension.Register();
        }

        public static void HookAll()
        {
            // Picking an ascension gets its own sound (sounds/ui/ascension_pick.wav, tools/SfxTool).
            Treasures.TreasureInventory.TreasureGained += def =>
            {
                if (AscensionRegistry.ForTreasure(def) != null) Audio.SoundLibrary.PlayUi("ui/ascension_pick");
            };
            PlaneAscensions.Hook();
        }
    }
}
