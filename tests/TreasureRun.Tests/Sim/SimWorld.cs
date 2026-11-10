using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Tests.Sim
{
    /// <summary>A walkable path: a polyline from the spawn (index 0) to the castle (last point).</summary>
    public sealed class SimPath
    {
        public readonly Vector3[] Points;
        public readonly float Length;
        private readonly float[] cumulative;

        public SimPath(params Vector3[] points)
        {
            Points = points;
            cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++) cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            Length = cumulative[cumulative.Length - 1];
        }

        /// <summary>Straight path along +x from (x0, 0, z) to (x1, 0, z).</summary>
        public static SimPath Straight(float x0, float x1, float z = 0f) => new SimPath(new Vector3(x0, 0f, z), new Vector3(x1, 0f, z));

        public Vector3 PointAt(float distance)
        {
            if (distance <= 0f) return Points[0];
            if (distance >= Length) return Points[Points.Length - 1];
            int i = Array.BinarySearch(cumulative, distance);
            if (i >= 0) return Points[i];
            i = ~i; // first index with cumulative > distance
            float t = (distance - cumulative[i - 1]) / (cumulative[i] - cumulative[i - 1]);
            return Vector3.Lerp(Points[i - 1], Points[i], t);
        }
    }

    /// <summary>A simulated tower: vanilla-style fire loop (timer, target in range by priority) with a pluggable attack.</summary>
    public sealed class SimTower
    {
        public string Name = "tower";
        public Vector3 Position;
        public float Range;
        /// <summary>Seconds between attacks (vanilla rps = 60 / rpm).</summary>
        public float FireInterval;
        public int BaseDamage, HealthDamage, ArmorDamage, ShieldDamage;
        public float Slow, Bleed, Burn, Poison, Crit, Stun;

        /// <summary>The attack; default: one instant hit on the target. Ascension tests plug their logic in here.</summary>
        public Action<SimWorld, SimTower, SimEnemy> Attack;

        /// <summary>Picks the target among enemies in range; default: furthest along the path (vanilla "Progress").</summary>
        public Func<IReadOnlyList<SimEnemy>, SimEnemy> SelectTarget;

        internal float TimeSinceShot;
        public int Shots;

        public SimHit Hit() => new SimHit(Name, BaseDamage, HealthDamage, ArmorDamage, ShieldDamage)
        {
            Slow = Slow, Bleed = Bleed, Burn = Burn, Poison = Poison, Crit = Crit, Stun = Stun,
        };

        /// <summary>Vanilla base stats of a tower at height 0, level 1 (docs/reference/vanilla-towers.md).</summary>
        public static SimTower Vanilla(string type, Vector3 position)
        {
            switch (type)
            {
                case "Ballista": return Make(type, position, 5f, 20f, 10, 10, 5, 5);
                case "Mortar": return Make(type, position, 10f, 10f, 20, 10, 15, 5);
                case "TeslaCoil": return Make(type, position, 2f, 30f, 10, 6, 3, 10);
                case "Cannon": return Make(type, position, 6f, 20f, 20, 10, 20, 5);
                case "Obelisk": return Make(type, position, 5f, 360f, 8, 5, 10, 2);
                case "ParticleCannon": return Make(type, position, 20f, 12f, 40, 15, 10, 20);
                case "Shredder": var s = Make(type, position, 5f, 10f, 10, 20, 10, 10); s.Bleed = 1f; return s;
                case "Monument": return Make(type, position, 7f, 12f, 15, 10, 5, 15);
                case "VampireLair": return Make(type, position, 9f, 6f, 12, 10, 5, 5);
                case "Lookout": return Make(type, position, 8f, 0f, 1, 2, 1, 3);
                default: throw new ArgumentException("Unknown tower type " + type);
            }
        }

        private static SimTower Make(string name, Vector3 pos, float range, float rpm, int dmg, int h, int a, int s) =>
            new SimTower
            {
                Name = name, Position = pos, Range = range, FireInterval = rpm > 0f ? 60f / rpm : float.PositiveInfinity,
                BaseDamage = dmg, HealthDamage = h, ArmorDamage = a, ShieldDamage = s,
            };
    }

    /// <summary>
    /// Deterministic mini combat world for offline tests (docs/ascension-plan.md §2.3): enemies walk a path at a fixed
    /// time step, towers fire on a timer at targets in range, damage goes through <see cref="SimDamage"/>.
    /// Simplifications: instant hits unless an attack models travel; targets re-picked every step (vanilla: every
    /// 0.25–1 s); tower XP and levels are not simulated.
    /// </summary>
    public sealed class SimWorld
    {
        public const float DefaultStep = 0.02f; // vanilla fixed time step

        public readonly SimPath Path;
        public readonly IRng Rng;
        public readonly SimMonsterMods Mods = new SimMonsterMods();
        public readonly SimDamageLog Log = new SimDamageLog();
        public readonly List<SimEnemy> Enemies = new List<SimEnemy>();
        public readonly List<SimTower> Towers = new List<SimTower>();
        public readonly List<SimEnemy> Leaked = new List<SimEnemy>();
        public float Time { get; private set; }

        /// <summary>Called after every step (custom effects: saw fields, black holes, ...).</summary>
        public event Action<SimWorld, float> Stepped;

        private readonly List<(float time, SimEnemy enemy)> spawns = new List<(float, SimEnemy)>();

        public SimWorld(SimPath path, int seed = 1)
        {
            Path = path;
            Rng = new SystemRng(seed);
        }

        public IEnumerable<SimEnemy> Alive => Enemies.Where(e => !e.Dead);

        /// <summary>Places an enemy on the path at a distance (0 = spawn).</summary>
        public SimEnemy Add(SimEnemy e, float distance = 0f)
        {
            e.Distance = distance;
            e.Position = Path.PointAt(distance);
            Enemies.Add(e);
            return e;
        }

        /// <summary>Spawns an enemy at the path start at a later time.</summary>
        public void SpawnAt(float time, SimEnemy e) => spawns.Add((time, e));

        public SimTower Add(SimTower t)
        {
            Towers.Add(t);
            return t;
        }

        public void Hit(SimEnemy target, SimHit hit) => SimDamage.TakeDamage(target, hit, Rng, Mods, Log);

        public List<SimEnemy> InRange(Vector3 center, float range) =>
            Alive.Where(e => FlatDistance(e.Position, center) <= range).ToList();

        public static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        public void Run(float seconds, float step = DefaultStep)
        {
            int steps = Mathf.RoundToInt(seconds / step);
            for (int i = 0; i < steps; i++) Step(step);
        }

        public void Step(float dt)
        {
            Time += dt;
            for (int i = spawns.Count - 1; i >= 0; i--)
                if (spawns[i].time <= Time) { Add(spawns[i].enemy); spawns.RemoveAt(i); }

            foreach (SimEnemy e in Enemies)
            {
                if (e.Dead) continue;
                SimDamage.Update(e, dt, Rng, Mods, Log);
                if (e.Dead) continue;
                e.Distance += e.CurrentSpeed * dt;
                e.Position = Path.PointAt(e.Distance);
                if (e.Distance >= Path.Length) { e.Dead = true; Leaked.Add(e); }
            }

            foreach (SimTower t in Towers)
            {
                t.TimeSinceShot += dt;
                List<SimEnemy> inRange = InRange(t.Position, t.Range);
                if (inRange.Count == 0 || t.TimeSinceShot <= t.FireInterval) continue;
                SimEnemy target = t.SelectTarget != null ? t.SelectTarget(inRange) : inRange.OrderByDescending(e => e.Distance).First();
                if (target == null) continue;
                t.TimeSinceShot = 0f;
                t.Shots++;
                if (t.Attack != null) t.Attack(this, t, target);
                else Hit(target, t.Hit());
            }

            Stepped?.Invoke(this, dt);
        }
    }
}
