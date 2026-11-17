namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>Phase 3 ascensions (docs/ascension-plan.md §4): registration of the treasures and their card chains.</summary>
    internal static class Phase3Ascensions
    {
        public static void Register()
        {
            SawEngine.Register();
            MortarModule.Register();
            LeySiphon.Register();
            AcceleratorCannon.Register();
        }

        /// <summary>Follow-up cards, only drawable while their ascension is owned (called from CustomCards.RegisterAll).</summary>
        public static void RegisterCards()
        {
        }
    }
}
