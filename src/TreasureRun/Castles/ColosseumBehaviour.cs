using UnityEngine;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Colosseum: every 5th wave is a champion wave. Instead of the regular monsters, a single champion enters the
    /// arena: the strongest monster type available at that level, twice the size, with the health of the whole wave
    /// it replaces x<see cref="HealthFactor"/> and heavy castle damage. It always drops a treasure chest.
    /// Bosses and mini-bosses still appear on their usual levels (a boss level 15/25/35/45 means boss + champion).
    /// </summary>
    internal sealed class ColosseumBehaviour : CastleBehaviour
    {
        public const int Interval = 5;
        public const float HealthFactor = 1.2f;

        private bool championWave;
        private int championsDefeated;
        private Enemy currentChampion;

        public static bool IsChampionWave(int level) => level > 0 && level % Interval == 0;

        public override float SpawnCountMultiplier => championWave ? 0f : 1f;

        public override void OnBeforeWaveStart(int nextLevel) => championWave = IsChampionWave(nextLevel);

        public override void OnWaveStarted(int level)
        {
            if (!championWave) return;
            SpawnChampion(SpawnManager.instance, level);
        }

        public override void OnEnemyKilled(Enemy enemy)
        {
            if (enemy != currentChampion) return;
            championsDefeated++;
            currentChampion = null;
            Core.GameHelpers.FloatingText(enemy.transform.position + Vector3.up * 1.5f, "CHAMPION DEFEATED!", "Yellow", 2f, 2f);
        }

        private void SpawnChampion(SpawnManager sm, int level)
        {
            // Strongest unlocked tier and the strongest monster of it at this level (same rules as regular spawns).
            GameObject[] tier = level >= 36 ? sm.tier4 : level >= 26 ? sm.tier3 : level >= 16 ? sm.tier2 : sm.tier1;
            float tierMultiplier = level >= 36 ? 4f : level >= 26 ? 3f : level >= 16 ? 2f : 1f;
            GameObject prefab = null;
            int prefabLevel = 0;
            foreach (GameObject g in tier)
            {
                int l = g.GetComponent<Enemy>().level;
                if (l <= level && l >= prefabLevel) { prefab = g; prefabLevel = l; }
            }
            if (prefab == null) prefab = sm.tier1[0];
            prefabLevel = Mathf.Max(1, prefabLevel);

            // The regular wave would have spawned level² points worth of monsters, each of these costing `points`.
            int points = Mathf.Max(1, Mathf.RoundToInt(prefabLevel / tierMultiplier));
            int replaced = Mathf.Max(1, level * level / points);
            int multiplier = Mathf.Max(2, Mathf.CeilToInt(replaced * HealthFactor));

            Waypoint spawn = null;
            float farthest = -1f;
            foreach (Waypoint w in sm.spawnPoints)
                if (w.distanceFromEnd > farthest) { farthest = w.distanceFromEnd; spawn = w; }
            if (spawn == null) return;

            Enemy champion = sm.SpawnEnemy(prefab, spawn, multiplier);
            champion.SetName("Champion " + prefab.name.Replace("(Clone)", "").Trim());
            champion.transform.localScale = Vector3.one * 2f;
            champion.damageToTower += 4 + level / 5;
            champion.deathObjectSpawn = sm.treasureChest;
            currentChampion = champion;
            Plugin.Log.LogInfo($"Colosseum: champion {prefab.name} x{multiplier} (replaces {replaced} monsters) on level {level}.");
        }

        public override string InfoText()
        {
            int level = Core.GameHelpers.Level;
            int next = (level / Interval + 1) * Interval;
            if (championWave && currentChampion != null) return $"A champion is in the arena!\nChampions defeated: {championsDefeated}";
            return $"Next champion wave: level {next}\nChampions defeated: {championsDefeated}";
        }
    }
}
