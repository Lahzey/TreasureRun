using System.Collections.Generic;
using TreasureRun.Core;
using UnityEngine;

// Pure mechanics of the Particle Accelerator (docs/ascension-plan.md §3, phase 3). No Unity engine calls: only
// Vector3/Mathf and IRng, so the offline tests (tests/TreasureRun.Tests) run them directly.
namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>What one Particle Cannon brings to the black hole (its current tower stats).</summary>
    public struct CannonStats
    {
        public int Level, Damage, Health, Armor, Shield;

        public CannonStats(int level, int damage, int health, int armor, int shield)
        {
            Level = level; Damage = damage; Health = health; Armor = armor; Shield = shield;
        }
    }

    /// <summary>
    /// The black hole's damage: the summed base damage of the core and every capacitor, with each layer's multiplier
    /// weighted by base damage, so Base × Health = Σ(damage × health multiplier) of all cannons (rounded).
    /// </summary>
    public struct PooledDamage
    {
        public int Base, Health, Armor, Shield, Cannons;
    }

    /// <summary>
    /// The charge of the (one) Accelerator core. It drains mana every frame during waves; the charge is the mana drained
    /// divided by 30 s of the base drain (60 mana/s), so without Ley Line income it can never be full in less than 30 s:
    /// with less mana coming in it simply takes longer. A Ley Line network plugged into the core adds its income to the
    /// drain, and that mana charges too (the secret interaction). It never drains mana below 25% of the maximum.
    /// </summary>
    public sealed class AcceleratorCharge
    {
        /// <summary>0 = empty, 1 = full (ready to fire).</summary>
        public float Progress;
        private float carry;
        /// <summary>Whether the last step wanted to drain but could not (mana at the 25% floor).</summary>
        public bool Starved { get; private set; }
        public bool Full => Progress >= 1f;

        /// <summary>
        /// One frame of charging: returns the whole mana to spend now (0 when full or held at the floor). Fractions carry
        /// over between frames. <paramref name="leyIncome"/>: mana per second of the Ley Line networks plugged into the core.
        /// </summary>
        public int Step(float dt, int mana, int maxMana, int leyIncome)
        {
            Starved = false;
            if (Full || dt <= 0f) return 0;
            float want = ParticleAcceleratorLogic.Drain(leyIncome) * dt;
            carry += want;
            int whole = (int)(carry + 1e-4f);
            if (whole <= 0) return 0;
            if (!ParticleAcceleratorLogic.MayDrain(mana, maxMana, whole))
            {
                // Held at the floor: nothing piles up meanwhile.
                carry -= want;
                Starved = true;
                return 0;
            }
            carry -= whole;
            Progress = Mathf.Min(1f, Progress + ParticleAcceleratorLogic.ChargeFor(whole));
            return whole;
        }

        /// <summary>Fired: starts over (the black hole is out).</summary>
        public void Reset() { Progress = 0f; carry = 0f; }
    }

    /// <summary>A caught monster's place in the black hole's disc: its own radius and angle, so no two share a point.</summary>
    public struct OrbitSlot
    {
        public float Radius, Angle, Wobble, Phase;
    }

    /// <summary>
    /// Particle Accelerator (Particle Cannon, Legendary): one core charges for 30 s on mana (Ley Line income speeds it
    /// up), then fires a black hole that pulls every monster nearby into orbit for 5 s, ticking damage, and ends in a
    /// supernova that hits once more and throws them away. Every other Particle Cannon is a capacitor: it stops
    /// attacking and adds its damage to the black hole.
    /// </summary>
    public static class ParticleAcceleratorLogic
    {
        public const float ChargeSeconds = 30f;
        /// <summary>Mana per second the core drains while charging (without Ley Line income): 1800 mana per black hole.</summary>
        public const float BaseDrain = 60f;
        /// <summary>The charge never takes mana below this share of the maximum.</summary>
        public const float ManaFloor = 0.25f;

        public const float HoleSeconds = 5f;
        /// <summary>The hole opens over this long (it already pulls meanwhile).</summary>
        public const float FormSeconds = 0.4f;
        /// <summary>The last part of the hole's life: the disc tightens and spins up, the sphere shrinks to a point.</summary>
        public const float CollapseSeconds = 0.6f;
        /// <summary>Monsters within this distance (on the ground plane) are caught while the hole is open.</summary>
        public const float CaptureRadius = 4f;
        public const int TicksPerSecond = 4;
        /// <summary>Base damage per tick = this × the pooled base damage (5 s × 4 ticks × 0.4 = 8× over the hole).</summary>
        public const float TickFactor = 0.4f;
        /// <summary>The supernova's single hit = this × the pooled base damage.</summary>
        public const float BurstFactor = 4f;

        /// <summary>Caught monsters orbit between these radii, in the disc, lifted to <see cref="Lift"/> above the ground.</summary>
        public const float OrbitMin = 1f, OrbitMax = 2.3f, Lift = 0.85f;
        /// <summary>Orbit radii shrink to this share during the collapse.</summary>
        public const float CollapseShrink = 0.35f;
        public const float PullRate = 4f, PullMaxSpeed = 7f;
        public const float ThrowMin = 1f, ThrowMax = 3f, ThrowSeconds = 0.4f, ThrowHeight = 0.45f;

        private const float GoldenAngle = 2.39996323f;

        /// <summary>Mana per second drained: the base plus the income of Ley Line networks plugged into the core.</summary>
        public static float Drain(int leyIncome) => BaseDrain + Mathf.Max(0, leyIncome);

        /// <summary>Charge (0..1) gained by draining <paramref name="mana"/>: full after 30 s of the base drain.</summary>
        public static float ChargeFor(float mana) => mana / (BaseDrain * ChargeSeconds);

        /// <summary>Seconds left to full charge at the full drain (mana permitting).</summary>
        public static float SecondsLeft(float progress, int leyIncome) =>
            Mathf.Max(0f, 1f - progress) / ChargeFor(Drain(leyIncome));

        /// <summary>Whether <paramref name="amount"/> can be drained without dropping below 25% of the maximum.</summary>
        public static bool MayDrain(int mana, int maxMana, int amount) =>
            amount > 0 && mana - amount >= ManaFloor * maxMana;

        /// <summary>
        /// The new core when there is none: the highest level, then the most base damage, then the first. -1 if empty.
        /// </summary>
        public static int PickCore(IList<CannonStats> cannons)
        {
            int best = -1;
            for (int i = 0; i < cannons.Count; i++)
            {
                if (best < 0) { best = i; continue; }
                CannonStats c = cannons[i], b = cannons[best];
                if (c.Level > b.Level || (c.Level == b.Level && c.Damage > b.Damage)) best = i;
            }
            return best;
        }

        /// <summary>Adds up the core and its capacitors (see <see cref="PooledDamage"/>).</summary>
        public static PooledDamage Pool(IList<CannonStats> cannons)
        {
            long sum = 0, h = 0, a = 0, s = 0;
            foreach (CannonStats c in cannons)
            {
                int d = Mathf.Max(0, c.Damage);
                sum += d;
                h += (long)d * c.Health;
                a += (long)d * c.Armor;
                s += (long)d * c.Shield;
            }
            var p = new PooledDamage { Base = (int)sum, Cannons = cannons.Count };
            if (sum > 0)
            {
                p.Health = Mathf.RoundToInt(h / (float)sum);
                p.Armor = Mathf.RoundToInt(a / (float)sum);
                p.Shield = Mathf.RoundToInt(s / (float)sum);
            }
            return p;
        }

        public static int TickDamage(int pooledBase) => pooledBase <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(pooledBase * TickFactor));
        public static int BurstDamage(int pooledBase) => pooledBase <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(pooledBase * BurstFactor));

        /// <summary>
        /// The <paramref name="index"/>-th caught monster's slot: a sunflower layout (radii by the golden ratio, evenly
        /// over the disc's area, angles by the golden angle) with a little random jitter, so slots never coincide
        /// however many are caught.
        /// </summary>
        public static OrbitSlot Slot(int index, IRng rng)
        {
            float f = (index * 0.618034f + 0.31f) % 1f;
            return new OrbitSlot
            {
                Radius = Mathf.Sqrt(OrbitMin * OrbitMin + (OrbitMax * OrbitMax - OrbitMin * OrbitMin) * f),
                Angle = index * GoldenAngle + rng.Range(-0.05f, 0.05f),
                Wobble = rng.Range(0.01f, 0.04f),
                Phase = rng.Range(0f, 6.2831853f),
            };
        }

        /// <summary>
        /// Radians per second the caught monsters turn. The whole disc turns as one, so slots never pass each other
        /// (with inner orbits faster, neighbours on close radii would brush past one another).
        /// </summary>
        public const float AngularSpeed = 1.6f;

        /// <summary>How far the collapse has gone (0 before it, 1 at the burst) at <paramref name="age"/> seconds.</summary>
        public static float Collapse(float age) => Mathf.Clamp01((age - (HoleSeconds - CollapseSeconds)) / CollapseSeconds);

        /// <summary>
        /// Where a slot is at <paramref name="age"/> seconds (on the ground plane through the centre). During the collapse
        /// every orbit tightens and the disc spins up.
        /// </summary>
        public static Vector3 OrbitPoint(Vector3 centre, OrbitSlot s, float age)
        {
            float k = Collapse(age);
            float r = s.Radius * Mathf.Lerp(1f, CollapseShrink, k * k) + s.Wobble * Mathf.Sin(age * 5f + s.Phase);
            // Turned angle: the integral of the angular speed, with a spin-up of up to 3x in the collapse.
            float turned = AngularSpeed * (age + 1.5f * CollapseSeconds * k * k);
            float a = s.Angle + turned;
            return centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        /// <summary>
        /// One step of the pull: a spring towards the slot's point, at most <see cref="PullMaxSpeed"/> units a second, so
        /// a monster at the rim is dragged in over about half a second rather than snapped.
        /// </summary>
        public static Vector3 Pull(Vector3 position, Vector3 target, float dt)
        {
            Vector3 step = (target - position) * (1f - Mathf.Exp(-PullRate * dt));
            float max = PullMaxSpeed * dt;
            if (step.sqrMagnitude > max * max) step = step.normalized * max;
            return position + step;
        }

        /// <summary>Whether a monster at <paramref name="position"/> is close enough to be caught (ground plane only).</summary>
        public static bool InReach(Vector3 centre, Vector3 position)
        {
            float dx = position.x - centre.x, dz = position.z - centre.z;
            return dx * dx + dz * dz <= CaptureRadius * CaptureRadius;
        }

        /// <summary>Where the supernova throws a monster: a random direction on the ground, 1 to 3 units away.</summary>
        public static Vector3 ThrowOffset(IRng rng)
        {
            float a = rng.Range(0f, 6.2831853f);
            float d = rng.Range(ThrowMin, ThrowMax);
            return new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
        }

        /// <summary>Position along a throw at <paramref name="t"/> (0..1): eased out, with a small hop.</summary>
        public static Vector3 ThrowPoint(Vector3 from, Vector3 offset, float t)
        {
            t = Mathf.Clamp01(t);
            float eased = 1f - (1f - t) * (1f - t);
            return from + offset * eased + Vector3.up * (ThrowHeight * 4f * t * (1f - t));
        }
    }
}
