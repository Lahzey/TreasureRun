using System;
using System.Collections.Generic;
using TreasureRun.Core;
using UnityEngine;

// Pure mechanics of the phase 2 ascensions (docs/ascension-plan.md §3). No Unity engine calls: only Vector3/Mathf and
// IRng, so the offline tests (tests/TreasureRun.Tests) run them directly.
namespace TreasureRun.Ascensions.Phase2
{
    /// <summary>Shared timing helper: a fire interval accumulator (fires at most a few times per frame).</summary>
    public static class FireClock
    {
        /// <summary>Advances <paramref name="timer"/>; returns how many shots are due (interval ≤ 0 → none).</summary>
        public static int Due(ref float timer, float dt, float interval, int max = 4)
        {
            if (interval <= 0f) return 0;
            timer += dt;
            int n = 0;
            while (timer >= interval && n < max) { timer -= interval; n++; }
            if (n == max) timer = Mathf.Min(timer, interval);
            return n;
        }
    }

    /// <summary>
    /// Icicle Cannon (Frost Keep): a straight icicle that pierces every monster on its line (each once), with the keep's
    /// slow. Base range 6; one shot per <see cref="BaseInterval"/> s at the keep's attack speed.
    /// </summary>
    public static class IcicleCannonLogic
    {
        public const float BaseRange = 6f;
        public const float BaseInterval = 1.2f;
        public const int DamageFactor = 3;
        /// <summary>Flight distance in multiples of the range (the vanilla cannonball also flies 2× range).</summary>
        public const float TravelFactor = 2f;
        public const float Speed = 14f;

        public static int ShotDamage(int towerDamage) => Mathf.Max(1, towerDamage * DamageFactor);

        public static float Interval(float attackSpeedFactor) => BaseInterval / Mathf.Max(0.05f, attackSpeedFactor);
    }

    /// <summary>
    /// Cluster Munitions (Mortar): at 2/3 of its flight a shell splits into 5 bomblets (+1 per More Bomblets card),
    /// each with 50% damage and 50% blast radius. They land on a ring of radius 0.6 × bomblet radius around the shell's
    /// target point, so the centre is inside every bomblet's blast.
    /// </summary>
    public static class ClusterMunitionsLogic
    {
        public const int BaseBomblets = 5;
        public const int BombletsPerCard = 1;
        public const float DamageShare = 0.5f;
        public const float RadiusShare = 0.5f;
        public const float SplitAt = 2f / 3f;
        public const float RingFactor = 0.6f;

        public static int Count(int moreBomblets) => BaseBomblets + BombletsPerCard * Mathf.Max(0, moreBomblets);

        public static int BombletDamage(int shellDamage) => Mathf.Max(1, Mathf.RoundToInt(shellDamage * DamageShare));

        public static float BombletRadius(float shellRadius) => shellRadius * RadiusShare;

        /// <summary>
        /// Landing points: evenly spread angles (random start, ±15° jitter) on a ring of 0.6 × radius (±20%), flat
        /// around <paramref name="center"/>.
        /// </summary>
        public static List<Vector3> LandingPoints(Vector3 center, int count, float bombletRadius, IRng rng)
        {
            var points = new List<Vector3>(count);
            float start = rng.Value() * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float angle = start + i * Mathf.PI * 2f / count + rng.Range(-0.26f, 0.26f);
                float r = bombletRadius * RingFactor * rng.Range(0.8f, 1.2f);
                points.Add(center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
            }
            return points;
        }

        /// <summary>How many of the blasts reach a point (flat distance ≤ radius + target radius).</summary>
        public static int BlastsReaching(IReadOnlyList<Vector3> landings, Vector3 point, float bombletRadius, float targetRadius = 0f)
        {
            int n = 0;
            float reach = bombletRadius + targetRadius;
            foreach (Vector3 p in landings)
            {
                Vector3 d = p - point;
                d.y = 0f;
                if (d.sqrMagnitude <= reach * reach) n++;
            }
            return n;
        }
    }

    /// <summary>Firing Line (Cannon): the cannon fires along a fixed, player-chosen direction for the whole wave.</summary>
    public static class FiringLineLogic
    {
        /// <summary>Aim points closer than this to the cannon are rejected (no direction).</summary>
        public const float MinAimDistance = 0.75f;

        /// <summary>Flat unit direction from the cannon to the aim point, or null when the point is too close.</summary>
        public static Vector3? Direction(Vector3 cannon, Vector3 aim)
        {
            Vector3 d = aim - cannon;
            d.y = 0f;
            if (d.magnitude < MinAimDistance) return null;
            return d.normalized;
        }

        /// <summary>The cannon shoots whenever a wave is on and a direction is set (no target needed).</summary>
        public static bool Fires(bool combat, bool aimed) => combat && aimed;

        /// <summary>Line Cannon balls fly faster than vanilla ones ...</summary>
        public const float BallSpeedFactor = 1.75f;
        /// <summary>... and further: this many times the range (vanilla: 2×).</summary>
        public const float TravelFactor = 3f;
    }

    /// <summary>
    /// Hwacha (Cannon): a volley of rockets (20 by default, a config value) launched over about a second, each flying to
    /// a random point within <see cref="Scatter"/> of the target, with a small blast. Reload: twice the cannon's.
    /// </summary>
    public static class HwachaLogic
    {
        public const int DefaultRockets = 20;
        public const float LaunchWindow = 1f;
        public const float DamageFactor = 0.3f;
        /// <summary>Blast radius around the landing point (a monster counts once its collider touches it).</summary>
        public const float BlastRadius = 0.6f;
        /// <summary>Burn per rocket hit (on top of the Cannon's own).</summary>
        public const float Burn = 0.3f;
        /// <summary>Monsters closer than this to the cart cannot be targeted.</summary>
        public const float MinRange = 2f;
        public const float Scatter = 1.5f;
        public const float IntervalFactor = 2f;

        public static int RocketDamage(int towerDamage) => Mathf.Max(1, Mathf.RoundToInt(towerDamage * DamageFactor));

        /// <summary>Launch time of rocket <paramref name="index"/> (0-based) after the volley starts.</summary>
        public static float LaunchDelay(int index, int count) => count <= 1 ? 0f : LaunchWindow * index / (count - 1);

        /// <summary>Uniform random point in a flat disc of radius <see cref="Scatter"/> (offset from the target).</summary>
        public static Vector3 ScatterOffset(IRng rng)
        {
            float a = rng.Value() * Mathf.PI * 2f;
            float r = Scatter * Mathf.Sqrt(rng.Value());
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
    }

    /// <summary>
    /// Magma Lobber (Flame Thrower): slow, big lobbed magma balls with a large blast that set monsters on fire. Base
    /// range 8. Pays the flame thrower's mana for every second between lobs.
    /// </summary>
    public static class MagmaLobberLogic
    {
        public const float BaseRange = 8f;
        public const float BaseInterval = 4f;
        public const int DamageFactor = 4;
        public const float BlastRadius = 1.4f;
        /// <summary>Burn on top of the tower's own (the Flame Thrower has 100%).</summary>
        public const float ExtraBurn = 1f;

        public static int LobDamage(int towerDamage) => Mathf.Max(1, towerDamage * DamageFactor);

        public static float Interval(float attackSpeedFactor) => BaseInterval / Mathf.Max(0.05f, attackSpeedFactor);

        /// <summary>The magma pool: hot, then cooling, then gone (seconds). Monsters standing in it burn.</summary>
        public const float PoolHot = 1.2f, PoolWarm = 2.4f, PoolBurnUntil = 3.5f, PoolFade = 1f;
        /// <summary>Burn per second while standing in the pool, as a share of the shot's damage to that monster.</summary>
        public const float PoolBurnPerSecond = 0.2f;
        public const float PoolTick = 0.5f;

        /// <summary>
        /// Burn stacks added per pool tick: <see cref="PoolBurnPerSecond"/> of the shot's damage (base × the multiplier
        /// of the monster's current layer) per second, in <see cref="PoolTick"/> steps.
        /// </summary>
        public static int PoolBurnPerTick(int lobDamage, int layerMultiplier) =>
            Mathf.Max(1, Mathf.RoundToInt(lobDamage * Mathf.Max(1, layerMultiplier) * PoolBurnPerSecond * PoolTick));

        /// <summary>
        /// Base damage per pool tick: <see cref="PoolBurnPerSecond"/> of the shot's base damage per second (the hit's
        /// layer multiplier then applies like on the shot itself, so it is 20% of the shot's damage per second).
        /// </summary>
        public static int PoolDamagePerTick(int lobDamage) => Mathf.Max(1, Mathf.RoundToInt(lobDamage * PoolBurnPerSecond * PoolTick));

        /// <summary>Look of the pool at an age: 0 = hot, 1 = cooling, 2 = crusted.</summary>
        public static int PoolStage(float age) => age < PoolHot ? 0 : age < PoolWarm ? 1 : 2;

        /// <summary>Mana per lob: the vanilla per-second cost (damage × rate, once a second) for each second of reload.</summary>
        public static int ManaPerLob(int towerDamage, float rate) => Mathf.Max(0, Mathf.RoundToInt(towerDamage * rate * BaseInterval));
    }

    /// <summary>
    /// Bladestorm (Shredder): whole saw blades (1.5 across) lie flat and circle the tower on one ring whose outer edge
    /// is the tower's range, so monsters right next to the tower are not hit. More range = a bigger ring = more blades
    /// at the same spacing; attack speed turns the ring faster. A blade hits a monster once each time it passes over it.
    /// </summary>
    public static class BladestormLogic
    {
        public const float BaseRange = 2f;
        public const float BladeDiameter = 1.5f;
        /// <summary>Distance along the ring between blade centres (constant, so more range means more blades).</summary>
        public const float BladeSpacing = 2.5f;
        public const float BaseRevsPerSecond = 0.4f;
        /// <summary>Blades also spin on their own axis (visual only), the other way round.</summary>
        public const float SpinRevsPerSecond = 3f;
        public const float DamageFactor = 0.75f;
        public const int MinBlades = 2, MaxBlades = 24;

        public static float BladeRadius => BladeDiameter / 2f;

        /// <summary>Radius of the blade centres: the blades' outer edge is at the range.</summary>
        public static float OrbitRadius(float range) => Mathf.Max(BladeRadius, range - BladeRadius);

        public static int Count(float range) =>
            Mathf.Clamp(Mathf.FloorToInt(2f * Mathf.PI * OrbitRadius(range) / BladeSpacing), MinBlades, MaxBlades);

        public static float RevsPerSecond(float attackSpeedFactor) => BaseRevsPerSecond * Mathf.Max(0.05f, attackSpeedFactor);

        public static int HitDamage(int towerDamage) => Mathf.Max(1, Mathf.RoundToInt(towerDamage * DamageFactor));

        /// <summary>Angle (radians) of blade <paramref name="index"/> after <paramref name="turns"/> revolutions.</summary>
        public static float BladeAngle(int index, int count, float turns) => (turns + (float)index / count) * Mathf.PI * 2f;

        /// <summary>Flat offset of a blade's centre from the tower.</summary>
        public static Vector3 BladeOffset(int index, int count, float turns, float range)
        {
            float a = BladeAngle(index, count, turns), r = OrbitRadius(range);
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        /// <summary>True if a monster (flat offset from the tower, collider radius) is under a blade.</summary>
        public static bool Touches(Vector3 bladeOffset, Vector3 monsterOffset, float monsterRadius)
        {
            Vector3 d = monsterOffset - bladeOffset;
            d.y = 0f;
            float reach = BladeRadius + monsterRadius;
            return d.sqrMagnitude <= reach * reach;
        }
    }

    /// <summary>
    /// Beam Cannon (Particle Cannon): a continuous beam ticking 10× a second on the first monster on the line to its
    /// target, 1.3× the vanilla damage per second for 1.6× the mana per second. Fractions are carried between ticks.
    /// </summary>
    public static class BeamCannonLogic
    {
        public const int TicksPerSecond = 10;
        public const float DpsFactor = 1.3f;
        public const float ManaFactor = 1.6f;

        /// <summary>Base damage per tick (fractional): vanilla damage per shot / shot interval × factor / ticks.</summary>
        public static float DamagePerTick(int towerDamage, float shotInterval) =>
            towerDamage / Mathf.Max(0.05f, shotInterval) * DpsFactor / TicksPerSecond;

        /// <summary>Mana per tick (fractional): vanilla mana per shot (damage × rate) / interval × factor / ticks.</summary>
        public static float ManaPerTick(int towerDamage, float rate, float shotInterval) =>
            towerDamage * rate / Mathf.Max(0.05f, shotInterval) * ManaFactor / TicksPerSecond;

        /// <summary>Adds <paramref name="amount"/> to the carry and takes out the whole part.</summary>
        public static int Take(ref float carry, float amount)
        {
            carry += amount;
            int whole = Mathf.FloorToInt(carry);
            carry -= whole;
            return whole;
        }
    }

    /// <summary>
    /// Dead Man's Land (Encampment): mines stay after the wave. A mine landing within <see cref="MergeRadius"/> of a
    /// lying mine merges into it as an extra charge. A mine with N charges explodes up to N times: once per frame while
    /// a monster is on it.
    /// </summary>
    public static class DeadMansLandLogic
    {
        /// <summary>Mines land on path cell centres ± 0.125, so two mines on one cell are always within this.</summary>
        public const float MergeRadius = 0.4f;

        /// <summary>Index of the nearest mine within merge distance of <paramref name="at"/> (flat), or −1.</summary>
        public static int FindMerge(IReadOnlyList<Vector3> mines, Vector3 at)
        {
            int best = -1;
            float bestD = MergeRadius * MergeRadius;
            for (int i = 0; i < mines.Count; i++)
            {
                Vector3 d = mines[i] - at;
                d.y = 0f;
                if (d.sqrMagnitude <= bestD) { bestD = d.sqrMagnitude; best = i; }
            }
            return best;
        }

        /// <summary>Look of a mine: 0 = single, 1 = two or three charges, 2 = four or more.</summary>
        public static int Look(int charges) => charges >= 4 ? 2 : charges >= 2 ? 1 : 0;
    }

    /// <summary>Charge counter of one persistent mine (call <see cref="Step"/> once per frame).</summary>
    public sealed class MineCharges
    {
        public int Charges { get; private set; }

        public MineCharges(int charges = 1) { Charges = Mathf.Max(1, charges); }

        public void Merge(int charges) => Charges += Mathf.Max(0, charges);

        public bool Spent => Charges <= 0;

        /// <summary>Explodes (returns true, one charge used) when a monster is on the mine and charges are left.</summary>
        public bool Step(bool monsterOnMine)
        {
            if (Charges <= 0 || !monsterOnMine) return false;
            Charges--;
            return true;
        }
    }
}
