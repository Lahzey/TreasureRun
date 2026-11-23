using System;
using System.Collections.Generic;
using TreasureRun.Balance;
using TreasureRun.Core;

namespace TreasureRun.Pois
{
    /// <summary>
    /// The Lizard Nest POI (docs/poi-plan.md §8): a nest on a grass cell beside the path. In the wave it is found, giant
    /// lizards crawl out of it over the first seconds and join the attack: slow, tough (lots of health, a little armor, no
    /// shield). Each lizard that dies sets its brood loose: tiny, fast, squishy hatchlings that wriggle on along the path.
    /// After that wave a treasure chest stands in the nest; the nest stays (its cell stays blocked) but never hatches
    /// again. Counts and stats follow the wave: the lizards' and hatchlings' health is a fixed multiple of the wave's
    /// average monster (<see cref="WaveModel"/>), so they are equally strong for their wave whenever the nest turns up.
    /// Pure (offline-tested).
    /// </summary>
    public static class LizardNestLogic
    {
        public const string Id = "lizard_nest";
        public const int MinWave = 3;
        public const float Weight = 1f;

        /// <summary>Lizards: 1 at wave 1, rising to 5 at <see cref="RampTo"/>; hatchlings per lizard: 5 to 15.</summary>
        public const int MinLizards = 1, MaxLizards = 5, MinBrood = 5, MaxBrood = 15;
        public const int RampTo = 40;

        /// <summary>A lizard's health and armor, and a hatchling's health, in multiples of the wave's average monster (all layers).</summary>
        public const double LizardHealth = 12.0, LizardArmor = 1.2, HatchlingHealth = 0.12;
        /// <summary>Base speeds (vanilla monsters walk at 1-3.6; a Goblin at 2, a Ram at 1, a Bat at 3.6).</summary>
        public const float LizardSpeed = 1.0f, HatchlingSpeed = 3.4f;
        public const int LizardCastleDamage = 3, HatchlingCastleDamage = 1;
        /// <summary>A lizard's kill gold (vanilla level + bonus) in multiples of the wave's average kill; a hatchling pays 1 (plus the tower types).</summary>
        public const double LizardGold = 4.0;
        public const int HatchlingGold = 1;
        /// <summary>Tower types an average kill counts for its gold (balance only).</summary>
        public const double TypesPerKill = 2;
        /// <summary>
        /// The lizards come out over the first seconds of the wave, the first at once, the others this many seconds apart
        /// (a lizard is about 1.35 cells long and walks 1 cell a second, so they don't walk into each other).
        /// </summary>
        public const float SpawnGap = 1.4f;

        public static int Lizards(int wave) => Ramp(wave, MinLizards, MaxLizards);
        public static int Brood(int wave) => Ramp(wave, MinBrood, MaxBrood);

        private static int Ramp(int wave, int lo, int hi)
        {
            double t = Math.Max(0, Math.Min(1, (wave - 1) / (double)(RampTo - 1)));
            return Math.Max(lo, Math.Min(hi, (int)Math.Round(lo + t * (hi - lo), MidpointRounding.AwayFromZero)));
        }

        /// <summary>The wave's average monster (health + armor + shield); past the last modelled wave, +5% a wave like endless.</summary>
        public static double MeanMonster(int wave)
        {
            int w = Math.Max(1, Math.Min(WaveModel.LastWave, wave));
            double mean = WaveModel.Get(w).MeanTotal;
            return wave > WaveModel.LastWave ? mean * Math.Pow(1.05, wave - WaveModel.LastWave) : mean;
        }

        public static int LizardHealthAt(int wave) => Math.Max(1, (int)Math.Round(LizardHealth * MeanMonster(wave)));
        public static int LizardArmorAt(int wave) => (int)Math.Round(LizardArmor * MeanMonster(wave));
        public static int HatchlingHealthAt(int wave) => Math.Max(5, (int)Math.Round(HatchlingHealth * MeanMonster(wave)));

        /// <summary>A lizard's vanilla gold level (its kill pays this plus the tower types that hit it).</summary>
        public static int LizardGoldAt(int wave)
        {
            WaveStats s = WaveModel.Get(Math.Max(1, Math.Min(WaveModel.LastWave, wave)));
            double perKill = s.Count > 0 ? s.KillGold / s.Count : 4;
            return Math.Max(1, (int)Math.Round(LizardGold * perKill));
        }

        /// <summary>Seconds until the last lizard has crawled out.</summary>
        public static float SpawnSeconds(int lizards) => Math.Max(0, lizards - 1) * SpawnGap;

        // ------------------------------------------------------------------ balance inputs

        /// <summary>Health and armor the nest adds to its wave (the lizards and all their hatchlings).</summary>
        public static (double health, double armor) ExtraLayers(int wave)
        {
            int n = Lizards(wave);
            return (n * (LizardHealthAt(wave) + Brood(wave) * (double)HatchlingHealthAt(wave)), n * (double)LizardArmorAt(wave));
        }

        /// <summary>Gold of killing the lizards and their hatchlings (with <see cref="TypesPerKill"/> tower types each).</summary>
        public static double KillGold(int wave)
        {
            int n = Lizards(wave);
            return n * (LizardGoldAt(wave) + TypesPerKill + Brood(wave) * (HatchlingGold + TypesPerKill));
        }

        // ------------------------------------------------------------------ where the lizards join the path

        private static readonly (int dx, int dz)[] Dirs = { (0, -1), (-1, 0), (1, 0), (0, 1) };

        /// <summary>The path cell beside the nest that the lizards crawl onto (the first in a fixed order); (-1, -1) if none.</summary>
        public static (int x, int z) PathCell(SectionLayout l, (int x, int z) nest)
        {
            foreach (var (dx, dz) in Dirs)
                if (l.IsPath(nest.x + dx, nest.z + dz) && TargetWaypoint(l, nest.x + dx, nest.z + dz) >= 0) return (nest.x + dx, nest.z + dz);
            return (-1, -1);
        }

        /// <summary>
        /// The waypoint (index into <see cref="SectionLayout.Waypoints"/>) a monster standing on path cell (x, z) walks to
        /// next: the cell's own waypoint if it is one (the pathfinder moves on from there at once), else the end of the
        /// route segment through the cell (waypoints point towards the entry, so that is the way to the castle). -1 if the
        /// cell is on no segment.
        /// </summary>
        public static int TargetWaypoint(SectionLayout l, int x, int z)
        {
            for (int i = 0; i < l.Waypoints.Count; i++)
                if (l.Waypoints[i].X == x && l.Waypoints[i].Z == z) return i;
            for (int i = 0; i < l.Waypoints.Count; i++)
            {
                SectionWaypoint a = l.Waypoints[i];
                if (a.Next < 0 || a.Next >= l.Waypoints.Count) continue;
                SectionWaypoint b = l.Waypoints[a.Next];
                bool onX = a.Z == b.Z && z == a.Z && x >= Math.Min(a.X, b.X) && x <= Math.Max(a.X, b.X);
                bool onZ = a.X == b.X && x == a.X && z >= Math.Min(a.Z, b.Z) && z <= Math.Max(a.Z, b.Z);
                if (onX || onZ) return a.Next;
            }
            return -1;
        }
    }

    /// <summary>
    /// The nest's cell: a random grass cell next to the path (4 neighbours), off the section's edge where possible. Chosen
    /// after the paths are laid, so it is plain grass at height 1.
    /// </summary>
    public sealed class LizardNestStyle : SectionStyle
    {
        public override void Shape(SectionLayout l, IRng rng)
        {
            List<(int x, int z)> free = Candidates(l, inner: true);
            if (free.Count == 0) free = Candidates(l, inner: false);
            if (free.Count == 0) return;
            var (cx, cz) = free[rng.Range(0, free.Count)];
            l.Cells[cx, cz] = SectionCell.Reserved;
        }

        /// <summary>Grass cells with a path cell among their 4 neighbours that the lizards can walk on from.</summary>
        public static List<(int x, int z)> Candidates(SectionLayout l, bool inner)
        {
            var free = new List<(int x, int z)>();
            int lo = inner ? 1 : 0, hi = inner ? SectionLayout.Size - 1 : SectionLayout.Size;
            for (int x = lo; x < hi; x++)
                for (int z = lo; z < hi; z++)
                    if (l.IsGrass(x, z) && LizardNestLogic.PathCell(l, (x, z)).x >= 0) free.Add((x, z));
            return free;
        }

        /// <summary>The nest's cell in a generated layout (its one reserved cell).</summary>
        public static (int x, int z) CellOf(SectionLayout l)
        {
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                    if (l.Cells[x, z] == SectionCell.Reserved) return (x, z);
            return (1, 1);
        }
    }
}
