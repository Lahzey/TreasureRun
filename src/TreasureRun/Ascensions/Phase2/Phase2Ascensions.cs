namespace TreasureRun.Ascensions.Phase2
{
    /// <summary>Phase 2 ascensions (docs/ascension-plan.md §4): registration of the treasures and their card chains.</summary>
    internal static class Phase2Ascensions
    {
        public static void Register()
        {
            IcicleCannon.Register();
            ClusterMunitions.Register();
            FiringLine.Register();
            Hwacha.Register();
            MagmaLobber.Register();
            Bladestorm.Register();
            BeamCannon.Register();
            DeadMansLand.Register();
        }

        /// <summary>Follow-up cards, only drawable while their ascension is owned (called from CustomCards.RegisterAll).</summary>
        public static void RegisterCards()
        {
            ClusterMunitions.RegisterCards();
        }
    }
}
