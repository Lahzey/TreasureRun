namespace TreasureRun.Ascensions.Phase1
{
    /// <summary>Phase 1 ascensions (docs/ascension-plan.md §4): registration of the treasures and their card chains.</summary>
    internal static class Phase1Ascensions
    {
        public static void Register()
        {
            Repeater.Register();
            Springald.Register();
            LoneScout.Register();
            ChainLightning.Register();
            Supercharge.Register();
            SealedFate.Register();
            NestOfBats.Register();
        }

        /// <summary>Follow-up cards, only drawable while their ascension is owned (called from CustomCards.RegisterAll).</summary>
        public static void RegisterCards()
        {
            ChainLightning.RegisterCards();
            LoneScout.RegisterCards();
            NestOfBats.RegisterCards();
        }
    }
}
