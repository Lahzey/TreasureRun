using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Arcane Spire: 1 health. Leaking monsters are destroyed for mana (5 per damage point) and still give kill gold.
    /// Starts with full mana and generates mana during waves. The start tile's four corner mana crystals (vanilla
    /// "starting developments", normally tied to skill-tree progress) are always kept.
    /// </summary>
    internal sealed class ArcaneSpireBehaviour : CastleBehaviour
    {
        public const int ManaPerDamage = 5;
        public const int ManaPerSecond = 2;
        /// <summary>Mana arrives in pulses (each with a lightning bolt from a corner crystal).</summary>
        public const float PulseInterval = 2.5f;
        public static int ManaPerPulse => Mathf.RoundToInt(ManaPerSecond * PulseInterval);
        private static readonly Color ArcaneBlue = new Color(0.45f, 0.65f, 1f);

        private ArcaneSpireFx fx;

        public override System.Collections.Generic.IEnumerable<string> StartingCards => new[] { "Building - Mana Siphon" };

        public override void OnGameStart()
        {
            ArcaneManaWell well = CastleObject.AddComponent<ArcaneManaWell>();
            // After the starting developments settled, so the bolts find the kept corner crystals.
            CastleObject.AddComponent<DelayedAction>().Run(1, () => well.Fx = fx = ArcaneSpireFx.Attach(CastleObject));
            ResourceManager rm = ResourceManager.instance;
            rm.UpdateManaGatherRate(ManaPerSecond); // shows up in the mana HUD
            rm.AddMana(rm.maxMana);                 // the spire starts fully charged
            rm.SetManaBar();
        }

        private int absorbedTotal, obliterated;

        /// <summary>Dev console: plays the obliteration flash.</summary>
        internal void DebugFlash() => fx?.Flash();

        public override string InfoText() =>
            $"Mana: {ResourceManager.instance.mana}/{ResourceManager.instance.maxMana} " +
            $"(absorbs {ResourceManager.instance.mana / ManaPerDamage} damage)\n" +
            $"Generates {ManaPerPulse} mana every {PulseInterval:0.#} s during waves\n" +
            $"Monsters obliterated: {obliterated} ({absorbedTotal} mana spent)";

        public override DevelopmentFate StartingDevelopment(GameObject development) =>
            development.name.StartsWith("StartingManaCrystal") ? DevelopmentFate.Keep : DevelopmentFate.Vanilla;

        public override bool OnEnemyReachedCastle(Enemy enemy)
        {
            ResourceManager rm = ResourceManager.instance;
            int damage = enemy.damageToTower + MonsterManager.instance.extraTowerDamage;
            if (damage <= 0) return false;

            int absorbable = Mathf.Min(damage, rm.mana / ManaPerDamage);
            if (absorbable <= 0) return false;

            rm.SpendMana(absorbable * ManaPerDamage);
            Fx.Burst(enemy.transform.position + Vector3.up * 0.3f, ArcaneBlue, 32);
            if (fx != null) fx.Flash();
            absorbedTotal += absorbable * ManaPerDamage;
            obliterated++;

            if (absorbable >= damage)
            {
                // Obliterated by the mages: counts as a kill (gold, chest drops, on-kill effects).
                enemy.Die();
                return true;
            }

            // Not enough mana for all of it: the rest hits the spire.
            GameManager.instance.TakeDamage(damage - absorbable);
            SpawnManager.instance.currentEnemies.Remove(enemy);
            Object.Destroy(enemy.gameObject);
            return true;
        }
    }

    /// <summary>Generates mana in pulses while a wave is running; each pulse is a lightning bolt.</summary>
    internal sealed class ArcaneManaWell : MonoBehaviour
    {
        public ArcaneSpireFx Fx;
        private float timer = ArcaneSpireBehaviour.PulseInterval;

        private void Update()
        {
            if (SpawnManager.instance == null || !SpawnManager.instance.combat || ResourceManager.instance == null) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = ArcaneSpireBehaviour.PulseInterval;
            ResourceManager.instance.AddMana(ArcaneSpireBehaviour.ManaPerPulse);
            if (Fx != null) Fx.Lightning();
        }
    }
}
