using System;
using TreasureRun.Core;

namespace TreasureRun.Pois
{
    /// <summary>What a wave of research improves for a kind of tower or building.</summary>
    public enum ResearchKind
    {
        /// <summary>Attacking towers: attack speed for every tower of the kind.</summary>
        AttackSpeed,
        /// <summary>The Lookout (it never fires): the strength of its mark (its damage and multipliers).</summary>
        Mark,
        /// <summary>Buildings (Mana Siphon, Mana Bank, Mine, University, Haunted House): what they yield (mana, gold, chances).</summary>
        Output,
    }

    /// <summary>
    /// The Laboratory POI (docs/poi-plan.md §9): a 3x3 structure in the middle of its section with a chamber in its centre
    /// cell. A tower or building placed in the chamber is only studied: it does nothing else, but it counts for its kind's
    /// price like any other copy. At the start of every wave the kind in the chamber gains one wave of research
    /// (<see cref="PerWave"/>): attacking towers +5% attack speed, the Lookout +5% mark, buildings +5% output. The
    /// research is kept for the rest of the run, whatever is in the chamber later. Pure (offline-tested).
    /// </summary>
    public static class LaboratoryLogic
    {
        public const string Id = "laboratory";
        public const int MinWave = 2;
        public const float Weight = 1f;

        /// <summary>What one wave of research gives (0.05 = 5%).</summary>
        public const double PerWave = 0.05;

        /// <summary>The chamber: the section's centre cell; the structure fills the eight cells round it.</summary>
        public static readonly (int x, int z) Slot = (SectionLayout.Half, SectionLayout.Half);

        public static bool InStructure(int x, int z) => Math.Abs(x - Slot.x) <= 1 && Math.Abs(z - Slot.z) <= 1;

        /// <summary>The bonus after a number of waves of research (attack speed, mark strength or output; 0.35 = +35%).</summary>
        public static double Bonus(int waves) => PerWave * Math.Max(0, waves);

        /// <summary>
        /// A whole-number yield after research, with the fraction carried over between yields (a siphon's 1 mana a
        /// second becomes 1.05: one extra mana every 20 seconds). Returns the amount to give now.
        /// </summary>
        public static int Yield(int baseAmount, int waves, ref double carry)
        {
            double exact = baseAmount * (1 + Bonus(waves)) + carry;
            int whole = (int)Math.Floor(exact + 1e-9);
            carry = exact - whole;
            return whole;
        }

        /// <summary>A chance after research (0.1 becomes 0.15 at +50%), at most 1.</summary>
        public static double Chance(double chance, int waves) => Math.Min(1, chance * (1 + Bonus(waves)));

        /// <summary>How the research of a kind reads ("+35% attack speed", "+35% mark", "+35% output").</summary>
        public static string Describe(ResearchKind kind, int waves)
        {
            int pct = (int)Math.Round(100 * Bonus(waves));
            switch (kind)
            {
                case ResearchKind.Mark: return $"+{pct}% mark";
                case ResearchKind.Output: return $"+{pct}% output";
                default: return $"+{pct}% attack speed";
            }
        }
    }

    /// <summary>
    /// The Laboratory's section: the structure takes the centre 3x3 (reserved before routing) and the routes run round
    /// it. They meet in the cell in front of the entry, so for every exit combination the path rings the structure on one
    /// or both sides. The chamber (the centre cell) is reserved too, so neither a route nor a hill touches it; in the game
    /// it is the one cell of the structure that can be built on.
    /// </summary>
    public sealed class LaboratoryStyle : SectionStyle
    {
        public LaboratoryStyle()
        {
            Hub = (SectionLayout.Half, 1);
        }

        public override void Reserve(SectionLayout l)
        {
            for (int x = LaboratoryLogic.Slot.x - 1; x <= LaboratoryLogic.Slot.x + 1; x++)
                for (int z = LaboratoryLogic.Slot.z - 1; z <= LaboratoryLogic.Slot.z + 1; z++)
                    l.Cells[x, z] = SectionCell.Reserved;
        }
    }
}
