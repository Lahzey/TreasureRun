using System;
using System.Collections.Generic;
using TreasureRun.Balance;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>What a full Soul Urn leaves behind (Record "reward").</summary>
    public enum UrnReward
    {
        None = 0,
        /// <summary>A vanilla treasure chest where the urn stood.</summary>
        Chest = 1,
        /// <summary>Gold, paid at once.</summary>
        Gold = 2,
        /// <summary>A soulfire rune on the urn's cell: the tower on it attacks faster.</summary>
        Rune = 3,
    }

    /// <summary>
    /// The Soul Urn POI (docs/poi-plan.md §6): an urn on a random grass cell of its section. Monsters slain within
    /// <see cref="Radius"/> of it give it their souls; full, it bursts and leaves a treasure chest, gold or a soulfire rune.
    /// Its size follows the waves (<see cref="Capacity"/>), the rewards follow the balance framework (<see cref="Gold"/>,
    /// <see cref="RuneBonus"/>). Pure, offline-tested.
    /// </summary>
    public static class SoulUrnLogic
    {
        public const string Id = "soul_urn";
        public const int MinWave = 3;
        /// <summary>Last wave an omen can appear: later urns would have no time to fill.</summary>
        public const int LastOmenWave = 40;
        /// <summary>Kills within this distance of the urn (world units = cells; one section is 7 wide) charge it.</summary>
        public const float Radius = 7f;

        /// <summary>
        /// The urn holds the souls of this share of the monsters its lane sends over <see cref="ChargeWaves"/> waves: a kill
        /// zone in its reach that takes half of them fills it in about six waves, towers elsewhere barely at all.
        /// </summary>
        public const double KillShare = 1.0;
        public const int ChargeWaves = 3, MinCapacity = 30;
        /// <summary>The share of the run's spawn points below which a lane is not counted smaller (a branch of a 4-way map).</summary>
        public const double MinLaneShare = 0.25;
        /// <summary>
        /// Waves from the omen until the rewards work, for the balance rating: a kill zone that takes three quarters of its
        /// lane's monsters fills it by the end of its fourth wave.
        /// </summary>
        public const int FillDelay = 4;
        /// <summary>
        /// The tower on the rune counts as this many average towers in the rating: the player puts their strongest tower
        /// there (an Obelisk, levelled, perhaps ascended) and keeps monsters in its range with slows.
        /// </summary>
        public const double RuneTowerShare = 3;
        /// <summary>
        /// Attack speed of the soulfire rune (1 = +100%), whatever the wave. Fixed so that a build of a few strong towers,
        /// or a single one, gains a lot but not everything.
        /// </summary>
        public const double RuneBonus = 1.0;

        /// <summary>Expected monsters of a wave, death spawns included (the balance wave model).</summary>
        public static double Monsters(int wave) => WaveModel.Get(Math.Max(1, Math.Min(WaveModel.LastWave, wave))).Count;

        /// <summary>
        /// Souls the urn holds, set when its first wave starts: <see cref="KillShare"/> of the monsters of that wave and the
        /// next two, times the lane's share of the spawn points (rounded to 5, at least <see cref="MinCapacity"/>). About 30
        /// at wave 3, 120 at wave 10, 210 at wave 15, 280 at wave 20, 450 at wave 30 on a single lane.
        /// </summary>
        public static int Capacity(int wave, double laneShare)
        {
            double sum = 0;
            for (int k = 0; k < ChargeWaves; k++) sum += Monsters(wave + k);
            double share = Math.Max(MinLaneShare, Math.Min(1, laneShare));
            return Math.Max(MinCapacity, Round5(KillShare * share * sum));
        }

        /// <summary>
        /// Gold of the gold reward, at the wave the urn appeared (so waiting to fill it pays nothing): <see cref="GoldFactor"/>
        /// times what a treasure chest is worth at that wave (<see cref="ChestGold"/>): about 770 at wave 5, 1000 at 10, 2120
        /// at 20, 5430 at 30, 12,540 at 40.
        /// </summary>
        public static int Gold(int wave) => Round5(GoldFactor * ChestGold(wave));

        /// <summary>
        /// The gold whose gain is worth a treasure chest at a wave (Pools.TreasurePick as Balance.FairPrice.GainOf), fitted
        /// within 8%: about 515 at wave 5, 670 at 10, 1410 at 20, 3620 at 30, 8360 at 40.
        /// </summary>
        public static double ChestGold(int wave)
        {
            double w = Math.Max(1, wave);
            return GoldBase + GoldLinear * w + GoldSquare * w * w + GoldCube * w * w * w;
        }

        public const double GoldBase = 330, GoldLinear = 49, GoldSquare = -3.29, GoldCube = 0.177;
        /// <summary>
        /// The gold reward is worth one and a half chests: it is paid at the urn's first wave's value but arrives about
        /// <see cref="FillDelay"/> waves later, and with it the urn reaches the POI budget (balance-todo.md).
        /// </summary>
        public const double GoldFactor = 1.5;

        /// <summary>One of the three rewards, equally likely, from a roll in [0, 1).</summary>
        public static UrnReward Pick(double roll01) => roll01 < 1 / 3.0 ? UrnReward.Chest : roll01 < 2 / 3.0 ? UrnReward.Gold : UrnReward.Rune;

        /// <summary>Is a kill (offset from the urn, world axes) close enough to charge it? Horizontal distance only.</summary>
        public static bool InReach(Vector3 offset) => offset.x * offset.x + offset.z * offset.z <= Radius * Radius;

        /// <summary>The lane's share of the spawn points (1 on a single lane).</summary>
        public static double LaneShare(int through, int total) => total <= 0 || through <= 0 ? 1 : Math.Min(1, through / (double)total);

        /// <summary>Seconds a soul takes from its monster to the urn.</summary>
        public static float FlightTime(float distance) => 0.75f + 0.09f * distance;

        /// <summary>
        /// Where a soul is at <paramref name="t"/> (0..1) of its flight: it rises from the body, arcs over to the urn and
        /// dives into its mouth (a cubic curve, eased), weaving sideways a little (<paramref name="phase"/> varies the weave).
        /// Starts at <paramref name="from"/> + 0.3 up, ends exactly at <paramref name="mouth"/>.
        /// </summary>
        public static Vector3 SoulPoint(Vector3 from, Vector3 mouth, float t, float phase)
        {
            t = Mathf.Clamp01(t);
            Vector3 p0 = from + Vector3.up * 0.3f;
            Vector3 flat = mouth - p0;
            flat.y = 0f;
            float dist = flat.magnitude;
            Vector3 p1 = p0 + Vector3.up * (1.2f + 0.12f * dist);
            Vector3 p2 = mouth + Vector3.up * (1.4f + 0.1f * dist);
            float u = t * t * (3f - 2f * t);
            float a = 1f - u;
            Vector3 p = a * a * a * p0 + 3f * a * a * u * p1 + 3f * a * u * u * p2 + u * u * u * mouth;
            Vector3 side = dist > 0.01f ? Vector3.Cross(flat / dist, Vector3.up) : Vector3.right;
            return p + side * (0.3f * Mathf.Sin(Mathf.PI * t) * Mathf.Sin(2f * Mathf.PI * t + phase));
        }

        /// <summary>The urn's fill (0..1) for its souls and capacity.</summary>
        public static float Fill(int souls, int capacity) => capacity <= 0 ? 0f : Mathf.Clamp01(souls / (float)capacity);

        /// <summary>A text progress bar of <paramref name="width"/> blocks.</summary>
        public static int BarBlocks(float fill, int width) => Mathf.Clamp(Mathf.RoundToInt(fill * width), 0, width);

        public static int Round5(double v) => Math.Max(5, (int)Math.Round(v / 5.0) * 5);
    }

    /// <summary>
    /// The Soul Urn's section: a generic section (paths from every exit to the centre, random hills); the urn stands on a
    /// random grass cell that no path uses, chosen after the paths are laid (height 1, the hills come later).
    /// </summary>
    public sealed class SoulUrnStyle : SectionStyle
    {
        public override void Shape(SectionLayout l, IRng rng)
        {
            var free = new List<(int x, int z)>();
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                    if (l.IsGrass(x, z)) free.Add((x, z));
            if (free.Count == 0) return;
            var (cx, cz) = free[rng.Range(0, free.Count)];
            l.Cells[cx, cz] = SectionCell.Reserved;
        }

        /// <summary>The urn's cell in a generated layout (its one reserved cell).</summary>
        public static (int x, int z) CellOf(SectionLayout l)
        {
            for (int x = 0; x < SectionLayout.Size; x++)
                for (int z = 0; z < SectionLayout.Size; z++)
                    if (l.Cells[x, z] == SectionCell.Reserved) return (x, z);
            return (1, 1);
        }
    }
}
