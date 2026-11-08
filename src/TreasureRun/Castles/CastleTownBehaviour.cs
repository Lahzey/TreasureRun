using System.Collections.Generic;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Castle Town: earns income like a house (x1.5) for every building/tower edge-adjacent to the castle's 3x3
    /// footprint. 1.5x monsters per wave, each dropping 2/3 of the gold, so total kill gold stays about the same.
    /// </summary>
    internal sealed class CastleTownBehaviour : CastleBehaviour
    {
        public const float IncomePerBuilding = 1.5f;
        public const float SpawnMultiplier = 1.5f;

        private IncomeGenerator income;

        public override float SpawnCountMultiplier => SpawnMultiplier;

        public override float GoldMultiplier(GoldSource source) => source == GoldSource.Kill ? 1f / SpawnMultiplier : 1f;

        public override void OnGameStart()
        {
            if (CastleObject == null) return;
            // A vanilla income generator on the castle: pays incomeTimesLevel x level each wave (with the usual popup).
            income = CastleObject.AddComponent<IncomeGenerator>();
            income.incomePerRound = 0;
            income.incomeTimesLevel = 0f;
        }

        public override void OnBeforeWaveStart(int nextLevel)
        {
            if (income == null || CastleObject == null) return;
            int adjacent = CountAdjacentBuildings(CastleObject.transform.position);
            income.incomeTimesLevel = IncomePerBuilding * adjacent;
        }

        /// <summary>Player buildings on the 12 cells sharing an edge with the castle's 3x3 footprint.</summary>
        private static int CountAdjacentBuildings(Vector3 castle)
        {
            int cx = Mathf.RoundToInt(castle.x), cz = Mathf.RoundToInt(castle.z);
            var seen = new HashSet<GameObject>();
            int count = 0;

            void Check(Component c)
            {
                if (c == null || !seen.Add(c.gameObject)) return;
                int dx = Mathf.Abs(Mathf.RoundToInt(c.transform.position.x) - cx);
                int dz = Mathf.Abs(Mathf.RoundToInt(c.transform.position.z) - cz);
                if ((dx == 2 && dz <= 1) || (dz == 2 && dx <= 1)) count++;
            }

            foreach (Tower t in Object.FindObjectsOfType<Tower>()) Check(t);
            foreach (Mine m in Object.FindObjectsOfType<Mine>()) Check(m);
            foreach (ManaBank m in Object.FindObjectsOfType<ManaBank>()) Check(m);
            foreach (University u in Object.FindObjectsOfType<University>()) Check(u);
            foreach (ManaSiphon s in Object.FindObjectsOfType<ManaSiphon>()) Check(s);
            foreach (HauntedHouse h in Object.FindObjectsOfType<HauntedHouse>()) Check(h);
            return count;
        }
    }
}
