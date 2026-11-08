using TreasureRun.Core;

namespace TreasureRun.Cards
{
    /// <summary>
    /// How many copies of each custom card were picked this game. Stored in RunState during a Treasure Run (for
    /// save/resume); in normal mode (castle-exclusive cards) in a per-scene table.
    /// </summary>
    public static class CardCounters
    {
        private static readonly System.Collections.Generic.Dictionary<string, int> normalMode = new System.Collections.Generic.Dictionary<string, int>();

        static CardCounters()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => normalMode.Clear();
        }

        private static string Key(string id) => "card." + id;

        public static int Get(string id)
        {
            RunState run = TreasureRunMode.Run;
            if (run != null) return run.GetCounter(Key(id));
            return normalMode.TryGetValue(id, out int n) ? n : 0;
        }

        /// <summary>Increments and returns the new count.</summary>
        public static int Increment(string id)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null)
            {
                normalMode[id] = Get(id) + 1;
                return normalMode[id];
            }
            int n = run.GetCounter(Key(id)) + 1;
            run.SetCounter(Key(id), n);
            return n;
        }
    }

    /// <summary>Ids of custom cards whose count matters to other systems.</summary>
    public static class CustomCardIds
    {
        public const string LuckyFind = "lucky_find";
        public const string TreasureSense = "treasure_sense";
        public const string Cartographer = "cartographer";
        public const string BloodPact = "blood_pact";
        public const string OldGrowth = "old_growth";
        public const string RestlessDead = "restless_dead";
        public const string ForgottenShrines = "forgotten_shrines";
        public const string RichVeins = "rich_veins";
        public const string LeyLines = "ley_lines";
        public const string RoyalGrant = "royal_grant";
        public const string TradeRoute = "trade_route";
        public const string CompoundInterest = "compound_interest";
        public const string Investment = "investment";
        public const string BountyHunters = "bounty_hunters";
        public const string LumberMill = "lumber_mill";
        public const string WarBonds = "war_bonds";
        public const string BulkDiscount = "bulk_discount";
        public const string MasterMason = "master_mason";
        public const string UndividedAttention = "undivided_attention";
        public const string WiderHand = "wider_hand";
        public const string FieldRepairs = "field_repairs";
    }
}
