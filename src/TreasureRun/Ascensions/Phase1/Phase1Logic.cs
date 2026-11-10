using System;
using System.Collections.Generic;
using TreasureRun.Core;
using UnityEngine;

// Pure mechanics of the phase 1 ascensions (docs/ascension-plan.md §3). No Unity engine calls: only Vector3/Mathf and
// IRng, so the offline tests (tests/TreasureRun.Tests) run them directly.
namespace TreasureRun.Ascensions.Phase1
{
    /// <summary>Repeater: 3-round bursts on one target, reload stretched so the DPS is 1.5× the vanilla Ballista.</summary>
    public static class RepeaterLogic
    {
        public const int Burst = 3;
        /// <summary>Seconds between the bolts of a burst.</summary>
        public const float BoltSpacing = 0.15f;
        public const float DpsFactor = 1.5f;

        /// <summary>Time between bursts = vanilla time between shots × this (3 bolts at 1.5× DPS → 2×).</summary>
        public static float IntervalFactor => Burst / DpsFactor;

        /// <summary>Attack-speed multiplier for the stat layer (shots per second scale by this).</summary>
        public static float AttackSpeedMultiplier => 1f / IntervalFactor;

        /// <summary>Delay of bolt <paramref name="index"/> (0-based) after the burst starts.</summary>
        public static float BoltDelay(int index) => index * BoltSpacing;
    }

    /// <summary>Springald: piercing bolts, 100% damage on the first hit, −20% of the original per further hit, 5 hits.</summary>
    public static class SpringaldLogic
    {
        public const int MaxHits = 5;
        public const float FalloffPerHit = 0.2f;

        /// <summary>Base damage of the hit with 0-based <paramref name="hitIndex"/> (0 once the bolt is spent).</summary>
        public static int DamageForHit(int baseDamage, int hitIndex)
        {
            if (hitIndex < 0 || hitIndex >= MaxHits || baseDamage <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * (1f - FalloffPerHit * hitIndex)));
        }

        public static bool Spent(int hits) => hits >= MaxHits;
    }

    /// <summary>Lone Scout: the marked enemy takes a 1-damage tick 3 times a second (+1 per Swift Scouts card).</summary>
    public static class LoneScoutLogic
    {
        public const int BaseTicksPerSecond = 3;

        public static int TicksPerSecond(int swiftScouts) => BaseTicksPerSecond + Mathf.Max(0, swiftScouts);

        /// <summary>Advances the tick accumulator by <paramref name="dt"/>; returns how many ticks are due now.</summary>
        public static int TicksDue(ref float accumulator, float dt, int swiftScouts)
        {
            float interval = 1f / TicksPerSecond(swiftScouts);
            accumulator += dt;
            int due = 0;
            while (accumulator >= interval && due < 16) { accumulator -= interval; due++; }
            return due;
        }
    }

    /// <summary>One jump of a chain: from a previous victim (−1 = the coil) to a new one (indices into the enemy list).</summary>
    public struct ChainHop
    {
        public int From, To;
        public ChainHop(int from, int to) { From = from; To = to; }
    }

    /// <summary>
    /// Chain Lightning: a single-target bolt that jumps to the nearest not-yet-hit enemy within range of the last
    /// victim, up to a number of hits. When nothing is in reach of the last victim, it continues from earlier victims
    /// (latest first). Each enemy is hit at most once per attack. Distances are flat (x/z).
    /// <para>"In reach" works like vanilla tower range: an enemy counts once its collider touches the circle of
    /// <c>range</c> (the tower's full range, height bonus included) around the last victim, i.e. its centre is within
    /// range + its radius (<paramref name="radii"/>; monsters have 0.5-wide box colliders, radius ≈ 0.25).</para>
    /// </summary>
    public static class ChainLightningLogic
    {
        public const int BaseHits = 20;
        public const int HitsPerCard = 5;

        public static int MaxHits(int moreChains) => BaseHits + HitsPerCard * Mathf.Max(0, moreChains);

        public static List<ChainHop> Plan(IReadOnlyList<Vector3> enemies, int first, float range, int maxHits,
                                          IReadOnlyList<float> radii = null)
        {
            var hops = new List<ChainHop>();
            if (first < 0 || first >= enemies.Count || maxHits <= 0) return hops;
            var hit = new bool[enemies.Count];
            var victims = new List<int>();
            hops.Add(new ChainHop(-1, first));
            hit[first] = true;
            victims.Add(first);

            while (hops.Count < maxHits)
            {
                int next = -1, from = -1;
                // Latest victim first; fall back to earlier ones when it has no enemy in reach.
                for (int v = victims.Count - 1; v >= 0 && next < 0; v--)
                {
                    int src = victims[v];
                    float best = float.MaxValue;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        if (hit[i]) continue;
                        float d = Flat2(enemies[i] - enemies[src]);
                        float reach = range + (radii != null && i < radii.Count ? radii[i] : 0f);
                        if (d <= reach * reach && d < best) { best = d; next = i; from = src; }
                    }
                }
                if (next < 0) break;
                hit[next] = true;
                victims.Add(next);
                hops.Add(new ChainHop(from, next));
            }
            return hops;
        }

        private static float Flat2(Vector3 v) => v.x * v.x + v.z * v.z;
    }

    /// <summary>
    /// Supercharge buffs: each coil attack gives the towers in its range +30% attack speed for 1 s. A coil only
    /// refreshes its own buff; buffs of different coils stack. Ids are arbitrary ints (instance ids in game).
    /// </summary>
    public sealed class SuperchargeBuffs
    {
        public const float Duration = 1f;
        public const float BonusPerCoil = 0.3f;

        // tower → (coil → expiry)
        private readonly Dictionary<int, Dictionary<int, float>> buffs = new Dictionary<int, Dictionary<int, float>>();

        /// <summary>Refreshes the coil's buff on the towers; returns the towers whose stack count changed.</summary>
        public List<int> Apply(int coil, IEnumerable<int> towers, float now)
        {
            var changed = new List<int>();
            foreach (int t in towers)
            {
                if (t == coil) continue; // a coil never buffs itself
                if (!buffs.TryGetValue(t, out var byCoil)) buffs[t] = byCoil = new Dictionary<int, float>();
                int before = Count(byCoil, now);
                byCoil[coil] = now + Duration;
                if (Count(byCoil, now) != before) changed.Add(t);
            }
            return changed;
        }

        /// <summary>Drops expired buffs; returns the towers whose stack count changed.</summary>
        public List<int> Expire(float now)
        {
            var changed = new List<int>();
            var emptied = new List<int>();
            foreach (var kv in buffs)
            {
                int removed = 0;
                var expired = new List<int>();
                foreach (var c in kv.Value) if (c.Value <= now) expired.Add(c.Key);
                foreach (int c in expired) { kv.Value.Remove(c); removed++; }
                if (removed > 0) changed.Add(kv.Key);
                if (kv.Value.Count == 0) emptied.Add(kv.Key);
            }
            foreach (int t in emptied) buffs.Remove(t);
            return changed;
        }

        public int Stacks(int tower, float now) => buffs.TryGetValue(tower, out var byCoil) ? Count(byCoil, now) : 0;

        /// <summary>Attack-speed bonus as an additive percent (0.3 per active coil).</summary>
        public float Bonus(int tower, float now) => BonusPerCoil * Stacks(tower, now);

        public void Clear() => buffs.Clear();

        private static int Count(Dictionary<int, float> byCoil, float now)
        {
            int n = 0;
            foreach (float expiry in byCoil.Values) if (expiry > now) n++;
            return n;
        }
    }

    /// <summary>Sealed Fate: every stab does 1 more damage than the last (vanilla: 1 less), up to 3× the first.</summary>
    public static class SealedFateLogic
    {
        public const int MaxFactor = 3;

        public static int Cap(int original) => Mathf.Max(1, original) * MaxFactor;

        public static int NextDamage(int current, int original) => Mathf.Min(current + 1, Cap(original));
    }

    /// <summary>Nest of Bats: bat count and the weighted target choice (nearer enemies are picked more often).</summary>
    public static class NestOfBatsLogic
    {
        public const int BaseBats = 10;
        public const int BatsPerCard = 3;
        /// <summary>Seconds between bites of one bat.</summary>
        public const float BiteInterval = 1f;
        /// <summary>Extra bleed per bite (1 = bleed stacks equal to 100% of the damage dealt), on top of the lair's own.</summary>
        public const float BiteBleed = 1f;

        public static int Count(int moreBats) => BaseBats + BatsPerCard * Mathf.Max(0, moreBats);

        /// <summary>Index of the picked target, weight 1/d² (d at least 0.5); −1 if there are no candidates.</summary>
        public static int PickTarget(Vector3 from, IReadOnlyList<Vector3> candidates, IRng rng)
        {
            if (candidates.Count == 0) return -1;
            var weights = new float[candidates.Count];
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 d = candidates[i] - from;
                d.y = 0f;
                weights[i] = 1f / Mathf.Max(0.25f, d.sqrMagnitude);
                total += weights[i];
            }
            float roll = rng.Value() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0f) return i;
            }
            return candidates.Count - 1;
        }

        /// <summary>A bat needs a new target when its target is dead or outside the lair's range (flat distance).</summary>
        public static bool NeedsNewTarget(bool targetAlive, Vector3 target, Vector3 lair, float range)
        {
            if (!targetAlive) return true;
            Vector3 d = target - lair;
            d.y = 0f;
            return d.sqrMagnitude > range * range;
        }
    }
}
