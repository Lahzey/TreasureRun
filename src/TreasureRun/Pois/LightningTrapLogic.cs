using System;
using TreasureRun.Balance;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// The Lightning Trap POI (docs/poi-plan.md): four poles on the cells diagonal to the section's centre, joined by
    /// electric arcs. A slider in its popup sets how much mana it draws during waves (0 to <see cref="MaxManaPerSecond"/>
    /// per second); it strikes everything in the centre cell <see cref="StrikesPerSecond"/> times a second, with damage
    /// in proportion to the mana.
    /// </summary>
    public static class LightningTrapLogic
    {
        public const string Id = "lightning_trap";
        public const int MaxManaPerSecond = 15;
        public const float StrikesPerSecond = 4f;
        /// <summary>The strike zone: the centre cell, a square of half size 0.5 around the section centre.</summary>
        public const float HalfSize = 0.5f;
        /// <summary>Layer multipliers of the vanilla Tesla Coil (it favours shields).</summary>
        public const int HealthMult = 6, ArmorMult = 3, ShieldMult = 10;
        /// <summary>First wave an omen of it can appear.</summary>
        public const int MinWave = 1;

        /// <summary>The pole cells (section coordinates): diagonal to the centre.</summary>
        public static readonly (int x, int z)[] PoleCells = { (2, 2), (4, 2), (4, 4), (2, 4) };
        /// <summary>Heights of the arcs between the poles (world units above the grass).</summary>
        public static readonly float[] ArcHeights = { 0.45f, 0.85f, 1.25f };

        /// <summary>
        /// Base damage per strike for each mana per second the trap draws, at a wave. Solved so that the trap at its
        /// best mana rate is worth the POI budget (15 points) at every wave, the mana paid in mana buildings
        /// (probe BalanceProbe.LightningTrapDamage), and fitted.
        /// </summary>
        public static double DamagePerMana(int wave)
        {
            double w = Math.Max(1, wave);
            return DamageBase + DamageLinear * w + DamageSquare * w * w;
        }

        /// <summary>Fit of the solved values (waves 3-44 without boss waves, within about 15%).</summary>
        public const double DamageBase = 2.5, DamageLinear = 0.3, DamageSquare = 0.008;

        /// <summary>Base damage per strike at a wave and mana rate (0 at 0 mana).</summary>
        public static int Damage(int wave, int manaPerSecond)
        {
            if (manaPerSecond <= 0) return 0;
            return Math.Max(1, (int)Math.Round(DamagePerMana(wave) * Mathf.Min(manaPerSecond, MaxManaPerSecond)));
        }

        /// <summary>
        /// The mana rate the balance rating assumes at a wave: the best one on the reference boards at the fitted damage
        /// (probe BalanceProbe.LightningTrapDamage, last line). Low early, when mana is scarce, and on boss waves (a
        /// one-cell trap barely touches a boss); mostly the full 15 late. A player tunes the slider the same way.
        /// </summary>
        public static int PlannedRate(int wave) => BestRates[Mathf.Clamp(wave, 1, BestRates.Length) - 1];

        private static readonly int[] BestRates =
        {
            1, 1, 1, 1, 1, 11, 13, 12, 15, 14, 6, 14, 15, 14, 1, 8, 7, 8, 7, 10, 9, 9, 11, 10, 2,
            13, 13, 15, 15, 15, 15, 15, 15, 15, 7, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        };

        /// <summary>The trap as a fixed damage source for the balance model, at the planned rate.</summary>
        public static TowerInfo Info(int wave) => InfoWith(Damage(wave, PlannedRate(wave)), PlannedRate(wave));

        public static TowerInfo InfoWith(double damage, double manaPerSecond) => new TowerInfo
        {
            Kind = TowerKind.Poi, Name = "Lightning Trap", Damage = (int)Math.Round(damage),
            HealthMult = HealthMult, ArmorMult = ArmorMult, ShieldMult = ShieldMult,
            Interval = 1 / StrikesPerSecond, Shape = AttackShape.Trap, Area = 1, ManaPerSecond = manaPerSecond,
        };

        /// <summary>Is a point (relative to the section centre, world axes) inside the strike zone?</summary>
        public static bool InZone(Vector3 offset) => Mathf.Abs(offset.x) <= HalfSize && Mathf.Abs(offset.z) <= HalfSize;

        /// <summary>Visual intensity (0..1) at a mana rate.</summary>
        public static float Intensity(int manaPerSecond) => Mathf.Clamp01(manaPerSecond / (float)MaxManaPerSecond);

        /// <summary>
        /// Mana metering: the trap owes <paramref name="rate"/> × dt; whole points are paid when the bank has them.
        /// Returns the mana to spend now. <paramref name="powered"/> keeps the state between frames: out of mana, the trap
        /// stays silent until the bank holds a full second's worth again (no flicker on the castle's trickle of regeneration).
        /// </summary>
        public static int Meter(ref float owed, ref bool powered, float dt, int mana, int rate)
        {
            if (rate <= 0) { owed = 0f; powered = false; return 0; }
            if (!powered && mana < rate) { owed = 0f; return 0; }
            owed += rate * dt;
            int due = Mathf.FloorToInt(owed);
            if (due <= 0) { powered = true; return 0; }
            if (mana < due) { powered = false; owed = 0f; return 0; }
            owed -= due;
            powered = true;
            return due;
        }
    }

    /// <summary>Lightning Trap section: the paths meet in the centre cell; the four diagonal cells hold the poles.</summary>
    public sealed class LightningTrapStyle : SectionStyle
    {
        public override void Reserve(SectionLayout l)
        {
            foreach (var (x, z) in LightningTrapLogic.PoleCells) l.Cells[x, z] = SectionCell.Reserved;
        }
    }
}
