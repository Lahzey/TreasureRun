using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions.Phase1;
using TreasureRun.Ascensions.Phase2;
using TreasureRun.Cards;
using TreasureRun.UI;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>
    /// In-game checks of the phase 2 ascensions (one per ascension, granted for the scenario and revoked afterwards).
    /// Scenarios that need a running wave (Firing Line, Dead Man's Land) switch <c>SpawnManager.combat</c> on for their
    /// duration and off again before the dummies are removed.
    /// </summary>
    internal static class Phase2Scenarios
    {
        public static IEnumerable<Scenario> All => new Scenario[]
        {
            new IcicleCannonScenario(), new ClusterMunitionsScenario(), new FiringLineScenario(), new HwachaScenario(),
            new MagmaLobberScenario(), new BladestormScenario(), new BeamCannonScenario(), new DeadMansLandScenario(),
        };

        /// <summary>A spot on the track and its track height, so dummies stand where cannonballs and icicles fly.</summary>
        public static Vector3 Spot(ScenarioContext c, float distance) => c.TrackPoint(distance) ?? Vector3.zero;

        public static bool Combat(bool on)
        {
            bool was = SpawnManager.instance.combat;
            SpawnManager.instance.combat = on;
            return was;
        }
    }

    internal sealed class IcicleCannonScenario : Scenario
    {
        public override string Name => "icicle-cannon";
        public override string Description => "Icicle Cannon: range 6, icicles pierce 4 stacked dummies and slow them.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Phase1Scenarios.FillMana();
            Vector3 spot = Phase2Scenarios.Spot(c, 5f);
            GameObject go = c.BuildNear("FrostTower", spot, 2f, 4f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            c.Ascend(IcicleCannon.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/frost/icicle_turret"), "turret model swapped");
            c.Expect(t.range >= IcicleCannonLogic.BaseRange - 0.01f, $"range {t.range:0.##} (base {IcicleCannonLogic.BaseRange})");
            var dummies = Enumerable.Range(0, 4).Select(i => c.Dummy(spot + new Vector3(0.04f * i, 0f, 0f), 10_000_000)).ToList();
            yield return c.Wait(2f);
            DamageMeter m = c.Meter();
            yield return c.Wait(18f);
            IcicleCannon ice = go.GetComponent<IcicleCannon>();
            c.Expect(ice.Shots > 0, $"{ice.Shots} icicles fired");
            c.Expect(dummies.All(d => d.health < d.baseHealth), "every stacked dummy was pierced");
            c.Expect(dummies.All(d => d.currentSlowPercentage > 0f), $"dummies slowed ({dummies[0].currentSlowPercentage:P0})");
            float perShot = dummies.Count * IcicleCannonLogic.ShotDamage(t.damage) * t.healthDamage;
            float expected = perShot / IcicleCannonLogic.Interval(Phase2Kit.AttackSpeedFactor(t));
            c.ExpectNear(m.PerSecond(TowerType.Frost), expected, expected * 0.25f, "Icicle Cannon DPS over 4 pierced dummies");
            yield return Phase1Scenarios.Picture(c, go, "tower", 3f);
        }
    }

    internal sealed class ClusterMunitionsScenario : Scenario
    {
        public override string Name => "cluster-munitions";
        public override string Description => "Cluster Munitions: shells split into 5 bomblets that all hit a dummy on the target point.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = Phase2Scenarios.Spot(c, 6f);
            GameObject go = c.BuildNear("Morter", spot, 3f, 6f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            c.Ascend(ClusterMunitions.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/mortar/cluster_mortar"), "model swapped");
            c.Dummy(spot, 10_000_000);
            yield return c.Wait(1f);
            DamageMeter m = c.Meter();
            yield return c.Wait(30f);
            ClusterMunitions cm = go.GetComponent<ClusterMunitions>();
            c.Expect(cm.Splits > 0, $"{cm.Splits} shells split");
            float expected = ClusterMunitionsLogic.BaseBomblets * ClusterMunitionsLogic.BombletDamage(t.damage) * t.healthDamage / t.rps;
            c.ExpectNear(m.PerSecond(TowerType.Morter), expected, expected * 0.3f, "DPS with all 5 bomblets hitting");
            yield return Phase1Scenarios.Picture(c, go, "tower", 3f);
        }
    }

    internal sealed class FiringLineScenario : Scenario
    {
        public override string Name => "firing-line";
        public override string Description => "Firing Line: Set Target button; the aimed cannon fires during a wave and hits a dummy out of its range.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = Phase2Scenarios.Spot(c, 5f);
            GameObject go = c.BuildNear("Cannon", spot, 1.5f, 3f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            c.Ascend(FiringLine.Id);
            yield return null;
            FiringLine line = go.GetComponent<FiringLine>();
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/cannon/line_cannon"), "model swapped");
            Vector3 p = go.transform.position;
            DevConsole.Execute($"towerui {Mathf.RoundToInt(p.x)} {Mathf.RoundToInt(p.z)}");
            yield return null;
            yield return null;
            c.Expect(GameObject.Find("TR_SetTargetButton") != null, "Set Target button in the popup");
            yield return Phase1Scenarios.Picture(c, go, "popup", 3f);
            DevConsole.Execute("closeui");

            Vector3 dir = Phase1Kit.Flat(spot - p).normalized;
            Vector3 far = p + dir * (t.range * 1.5f);
            far.y = spot.y;
            c.Expect(line.SetAim(far), "aim set");
            Enemy dummy = c.Dummy(far, 10_000_000);
            bool was = Phase2Scenarios.Combat(true);
            yield return c.Wait(12f);
            Phase2Scenarios.Combat(was);
            c.Expect(line.Shots >= 2, $"{line.Shots} shots without a target in range");
            c.Expect(dummy.health < dummy.baseHealth, "the ball hit a dummy at 1.5x range on the line");
            yield return Phase1Scenarios.Picture(c, go, "tower", 4f);
        }
    }

    internal sealed class HwachaScenario : Scenario
    {
        public override string Name => "hwacha";
        public override string Description => "Hwacha: volleys of 20 rockets around the target, rack empties and refills.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = Phase2Scenarios.Spot(c, 5f);
            GameObject go = c.BuildNear("Cannon", spot, 2f, 4f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            c.Ascend(Hwacha.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/cannon/hwacha_loaded"), "rack model swapped");
            c.ExpectNear(t.rps, 6f, 0.3f, "time between volleys (vanilla 3 s, doubled)");
            c.Dummy(spot, 10_000_000);
            yield return c.Wait(1f);
            DamageMeter m = c.Meter();
            bool sawEmpty = false;
            float t0 = Time.time;
            while (Time.time - t0 < 20f)
            {
                if (Phase1Scenarios.MeshSwapped(go, "ascensions/cannon/hwacha_empty")) sawEmpty = true;
                yield return null;
            }
            Hwacha h = go.GetComponent<Hwacha>();
            c.Expect(h.Rockets >= 20, $"{h.Rockets} rockets launched");
            c.Expect(sawEmpty, "rack showed empty slots after a volley");
            // Rockets land uniformly within 1.5 of the target; a blast reaches the dummy within 0.45 + 0.25.
            float share = Mathf.Pow((HwachaLogic.BlastRadius + 0.25f) / HwachaLogic.Scatter, 2f);
            float expected = HwachaLogic.DefaultRockets * share * HwachaLogic.RocketDamage(t.damage) * t.healthDamage / t.rps;
            float dps = m.PerSecond(TowerType.Cannon);
            c.Expect(dps > expected * 0.4f && dps < expected * 2.5f, $"Hwacha DPS {dps:0.#} (about {expected:0.#} expected from the scatter)");
            // Picture in the middle of a volley.
            int before = h.Rockets;
            float t1 = Time.time;
            yield return new WaitUntil(() => h.Rockets >= before + 8 || Time.time - t1 > 10f);
            yield return Phase1Scenarios.Picture(c, go, "volley", 2.5f);
        }
    }

    internal sealed class MagmaLobberScenario : Scenario
    {
        public override string Name => "magma-lobber";
        public override string Description => "Magma Lobber: range 8, lobbed magma hits 3 dummies in its blast and sets them on fire.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Phase1Scenarios.FillMana();
            Vector3 spot = Phase2Scenarios.Spot(c, 6f);
            GameObject go = c.BuildNear("FlameThrower", spot, 3f, 6f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            c.Ascend(MagmaLobber.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/flame/magma_turret"), "turret model swapped");
            c.Expect(t.range >= MagmaLobberLogic.BaseRange - 0.01f, $"range {t.range:0.##} (base {MagmaLobberLogic.BaseRange})");
            var dummies = new[] { c.Dummy(spot, 10_000_000), c.Dummy(spot + new Vector3(0.6f, 0f, 0f), 10_000_000), c.Dummy(spot + new Vector3(0f, 0f, 0.6f), 10_000_000) };
            yield return c.Wait(16f);
            MagmaLobber lob = go.GetComponent<MagmaLobber>();
            c.Expect(lob.Lobs > 0, $"{lob.Lobs} magma balls lobbed");
            c.Expect(dummies.All(d => d.health < d.baseHealth), "all three dummies in the blast took damage");
            c.Expect(dummies.Any(d => d.burning), "dummies are burning");
            yield return Phase1Scenarios.Picture(c, go, "tower", 4f);
        }
    }

    internal sealed class BladestormScenario : Scenario
    {
        public override string Name => "bladestorm";
        public override string Description => "Bladestorm: saw blades on the range edge hit a dummy once per pass; one next to the tower is safe.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = Phase2Scenarios.Spot(c, 5f);
            GameObject go = c.BuildNear("Shredder", spot, 1f, 3f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            c.Ascend(Bladestorm.Id);
            yield return null;
            yield return null;
            Bladestorm b = go.GetComponent<Bladestorm>();
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/shredder/bladestorm_hub"), "hub model swapped");
            c.Expect(b.BladeCount == BladestormLogic.Count(t.range), $"{b.BladeCount} blades for range {t.range:0.##}");
            Vector3 p = go.transform.position;
            Enemy onRing = c.Dummy(new Vector3(p.x + BladestormLogic.OrbitRadius(t.range), spot.y, p.z), 10_000_000);
            Enemy inside = c.Dummy(new Vector3(p.x + 0.15f, spot.y, p.z), 10_000_000);
            yield return c.Wait(1f);
            long hits0 = b.Hits;
            float t0 = Time.time;
            yield return c.Wait(10f);
            float perSecond = (b.Hits - hits0) / (Time.time - t0);
            float expected = b.BladeCount * BladestormLogic.RevsPerSecond(Phase2Kit.AttackSpeedFactor(t));
            c.ExpectNear(perSecond, expected, expected * 0.25f, "blade passes per second over the ring dummy");
            c.Expect(onRing.health < onRing.baseHealth, "ring dummy took damage");
            c.Expect(inside.health == inside.baseHealth, "dummy next to the tower is safe");
            yield return Phase1Scenarios.Picture(c, go, "tower", 3f);
        }
    }

    internal sealed class BeamCannonScenario : Scenario
    {
        public override string Name => "beam-cannon";
        public override string Description => "Beam Cannon: a continuous beam deals 1.3x the vanilla Particle Cannon DPS.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Phase1Scenarios.FillMana();
            Vector3 spot = Phase2Scenarios.Spot(c, 6f);
            GameObject go = c.BuildNear("ParticleCannon", spot, 3f, 6f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            c.Dummy(spot, 10_000_000);
            yield return c.Wait(1f);
            DamageMeter vanillaMeter = c.Meter();
            yield return c.Wait(30f);
            float vanilla = vanillaMeter.PerSecond(TowerType.ParticleCannon);
            vanillaMeter.Dispose();
            c.Ascend(BeamCannon.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/particle/beam_turret"), "turret model swapped");
            yield return c.Wait(1f);
            DamageMeter m = c.Meter();
            ResourceManager rm = ResourceManager.instance;
            int mana0 = rm.mana;
            float t0 = Time.time;
            yield return c.Wait(15f);
            c.ExpectNear(m.PerSecond(TowerType.ParticleCannon), vanilla * BeamCannonLogic.DpsFactor, vanilla * 0.3f,
                         $"beam DPS (1.3x the measured vanilla {vanilla:0.#}; crit chance {t.critChance:P0})");
            float manaPerSecond = (mana0 - rm.mana) / (Time.time - t0);
            c.Info($"mana per second {manaPerSecond:0.##} (vanilla {t.damage * t.finalManaConsumption / t.rps:0.##}, expected x{BeamCannonLogic.ManaFactor})");
            yield return Phase1Scenarios.Picture(c, go, "beam", 4f);
        }
    }

    internal sealed class DeadMansLandScenario : Scenario
    {
        public override string Name => "dead-mans-land";
        public override string Description => "Dead Man's Land: mines survive the wave clean-up, merge into charges, a 3-charge mine explodes 3 times.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = Phase2Scenarios.Spot(c, 6f);
            GameObject go = c.BuildNear("Encampment", spot, 1f, 2f);
            if (go == null) yield break;
            c.Ascend(DeadMansLand.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/encampment/permacamp"), "model swapped");
            // Encampments only throw with a monster in range: a dummy on the camp's own cell (off the path).
            c.Dummy(go.transform.position, 10_000_000);
            bool was = Phase2Scenarios.Combat(true);
            float t0 = Time.time;
            yield return new WaitUntil(() => PersistentMine.All.Count(m => m != null && m.Landed) >= 3 || Time.time - t0 > 60f);
            Phase2Scenarios.Combat(was);
            var mines = PersistentMine.All.Where(m => m != null && m.Landed).ToList();
            c.Expect(mines.Count >= 1, $"{mines.Count} mines lying, charges {string.Join(",", mines.Select(m => m.Charges.Charges))}");
            c.Expect(mines.All(m => !SpawnManager.instance.destroyOnNewLevel.Contains(m.gameObject)), "mines are not cleared at the wave end");
            if (mines.Count == 0) yield break;

            PersistentMine mine = mines[0];
            mine.Charges.Merge(3 - mine.Charges.Charges);
            int charges = mine.Charges.Charges;
            yield return Phase1Scenarios.Picture(c, go, "mines", 3f);
            int before = PersistentMine.TotalExplosions;
            Enemy dummy = c.Dummy(mine.transform.position, 10_000_000);
            yield return c.Wait(1.5f);
            int explosions = PersistentMine.TotalExplosions - before;
            c.Expect(explosions == charges && mine == null, $"the {charges}-charge mine exploded {explosions} times and is gone");
            c.Expect(dummy.health < dummy.baseHealth, "the dummy on it took damage");
        }
    }
}
