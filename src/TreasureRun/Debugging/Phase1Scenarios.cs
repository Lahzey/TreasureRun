using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions;
using TreasureRun.Ascensions.Phase1;
using TreasureRun.Treasures;
using TreasureRun.UI;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>
    /// In-game checks of the phase 1 ascensions. Each one grants its ascension for the duration of the scenario and
    /// revokes it afterwards (<see cref="ScenarioContext.Ascend"/>), so ascensions of the same tower can be tested in
    /// one launch.
    /// </summary>
    internal static class Phase1Scenarios
    {
        public static IEnumerable<Scenario> All => new Scenario[]
        {
            new RepeaterScenario(), new SpringaldScenario(), new LoneScoutScenario(), new ChainLightningScenario(),
            new SuperchargeScenario(), new SealedFateScenario(), new NestOfBatsScenario(),
        };

        /// <summary>Plenty of mana for mana towers.</summary>
        public static void FillMana()
        {
            ResourceManager rm = ResourceManager.instance;
            rm.maxMana = Mathf.Max(rm.maxMana, 100_000);
            rm.mana = rm.maxMana;
            rm.SetManaBar();
        }

        public static bool MeshSwapped(GameObject tower, string model) =>
            tower.GetComponentsInChildren<MeshFilter>(true).Any(f => f.sharedMesh != null && f.sharedMesh.name == "TR_" + model);

        /// <summary>Zooms onto the tower and saves a screenshot (waits until it is written, before cleanup).</summary>
        public static IEnumerator Picture(ScenarioContext c, GameObject tower, string name, float size = 3.5f)
        {
            if (tower == null) yield break;
            Vector3 p = tower.transform.position;
            DevConsole.Execute($"zoom {size} {p.x:0.##} {p.z:0.##}");
            yield return null;
            c.Shot(name);
            yield return new WaitForEndOfFrame();
            yield return null;
        }
    }

    internal sealed class RepeaterScenario : Scenario
    {
        public override string Name => "repeater";
        public override string Description => "Repeater: 3-bolt bursts, about 1.5x the vanilla Ballista DPS, model swapped.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = c.TrackPoint(4f) ?? Vector3.zero;
            GameObject go = c.BuildNear("CrossbowTower", spot, 1.5f, 3.5f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            float vanillaDps = t.damage * t.healthDamage / t.rps;
            c.Ascend(Repeater.Id);
            yield return null;
            c.Expect(go.GetComponent<Repeater>() != null, "component attached");
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/ballista/repeater_turret"), "turret model swapped");
            c.ExpectNear(t.rps, 6f, 0.3f, "time between bursts (vanilla 3 s, doubled)");
            c.Dummy(spot, 10_000_000);
            yield return c.Wait(2f);
            DamageMeter m = c.Meter();
            yield return c.Wait(36f);
            c.ExpectNear(m.PerSecond(TowerType.Crossbow), vanillaDps * 1.5f, vanillaDps * 1.5f * 0.2f, "Repeater DPS");
            yield return Phase1Scenarios.Picture(c, go, "tower", 2.5f);
        }
    }

    internal sealed class SpringaldScenario : Scenario
    {
        public override string Name => "springald";
        public override string Description => "Springald: a bolt through 5 stacked dummies deals 100+80+60+40+20% (3x a vanilla shot).";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = c.TrackPoint(4f) ?? Vector3.zero;
            GameObject go = c.BuildNear("CrossbowTower", spot, 2f, 3.5f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            c.Ascend(Springald.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/ballista/springald_turret"), "turret model swapped");
            for (int i = 0; i < 6; i++) c.Dummy(spot + new Vector3(0.05f * i, 0f, 0f), 10_000_000);
            yield return c.Wait(2f);
            DamageMeter m = c.Meter();
            yield return c.Wait(30f);
            float perShot = t.healthDamage * Enumerable.Range(0, SpringaldLogic.MaxHits).Sum(k => SpringaldLogic.DamageForHit(t.damage, k));
            c.ExpectNear(m.PerSecond(TowerType.Crossbow), perShot / t.rps, perShot / t.rps * 0.2f, "Springald DPS with 5 pierces per bolt");
            yield return Phase1Scenarios.Picture(c, go, "tower", 2.5f);
        }
    }

    internal sealed class LoneScoutScenario : Scenario
    {
        public override string Name => "lone-scout";
        public override string Description => "Lone Scout: the marked dummy takes 3 ticks per second with the Lookout's mark bonus.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = c.TrackPoint(5f) ?? Vector3.zero;
            GameObject go = c.BuildNear("Lookout", spot, 1.5f, 4f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            c.Ascend(LoneScout.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/lookout/lone_scout"), "model swapped");
            Enemy dummy = c.Dummy(spot, 10_000_000);
            yield return c.Wait(2f);
            c.Expect(dummy.mark == t, "dummy is marked by the Lookout");
            DamageMeter m = c.Meter();
            float before = 3f * (1 + t.damage) * (1 + t.healthDamage);
            yield return c.Wait(20f);
            float after = 3f * (1 + t.damage) * (1 + t.healthDamage); // the Lookout levels up while marking
            float dps = m.PerSecond(TowerType.Lookout);
            c.Expect(dps >= before * 0.85f && dps <= after * 1.15f,
                     $"Lone Scout damage per second: {dps:0.#} (3 ticks × (1 + Lookout damage) × (1 + its health multiplier): {before:0} at the start, {after:0} at the end)");
            yield return Phase1Scenarios.Picture(c, go, "tower", 2.5f);
        }
    }

    internal sealed class ChainLightningScenario : Scenario
    {
        public override string Name => "chain-lightning";
        public override string Description => "Chain Lightning: one attack on a line of 25 dummies 1.2 apart hits exactly 20.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Phase1Scenarios.FillMana();
            Vector3 spot = c.TrackPoint(5f) ?? Vector3.zero;
            GameObject go = c.BuildNear("Tesla Coil", spot, 1f, 1.8f);
            if (go == null) yield break;
            c.Ascend(ChainLightning.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/tesla/chain_lightning"), "model swapped");
            Vector3 dir = Phase1Kit.Flat(spot - go.transform.position).normalized;
            var dummies = new List<Enemy>();
            for (int i = 0; i < 25; i++) dummies.Add(c.Dummy(spot + dir * 1.2f * i, 1_000_000));
            float t0 = Time.time;
            yield return new WaitUntil(() => dummies.Any(d => d.health < d.baseHealth) || Time.time - t0 > 6f);
            yield return null;
            int hit = dummies.Count(d => d.health < d.baseHealth);
            c.Expect(hit == ChainLightningLogic.BaseHits, $"one attack hit {hit} dummies (expected {ChainLightningLogic.BaseHits})");
            yield return Phase1Scenarios.Picture(c, go, "chain", 5f);
        }
    }

    internal sealed class SuperchargeScenario : Scenario
    {
        public override string Name => "supercharge";
        public override string Description => "Supercharge: a Ballista next to the coil fires 30% faster for 1 s after each zap.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Phase1Scenarios.FillMana();
            Vector3 spot = c.TrackPoint(5f) ?? Vector3.zero;
            GameObject coil = c.BuildNear("Tesla Coil", spot, 1f, 1.8f);
            if (coil == null) yield break;
            GameObject ballista = c.BuildNear("CrossbowTower", coil.transform.position, 1f, 1.5f);
            if (ballista == null) yield break;
            Tower b = ballista.GetComponent<Tower>();
            float baseInterval = b.rps;
            c.Ascend(Supercharge.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(coil, "ascensions/tesla/supercharge"), "model swapped");
            c.Dummy(spot, 10_000_000);
            bool sawBuff = false, sawExpire = false;
            float t0 = Time.time;
            while (Time.time - t0 < 8f)
            {
                if (Mathf.Abs(b.rps - baseInterval / 1.3f) < 0.02f) sawBuff = true;
                else if (sawBuff && Mathf.Abs(b.rps - baseInterval) < 0.02f) sawExpire = true;
                yield return null;
            }
            c.Expect(sawBuff, $"Ballista interval dropped to {baseInterval / 1.3f:0.00} s while buffed");
            c.Expect(sawExpire, "and returned to the vanilla interval when the buff ran out");
            yield return Phase1Scenarios.Picture(c, coil, "coil", 3f);
        }
    }

    internal sealed class SealedFateScenario : Scenario
    {
        public override string Name => "sealed-fate";
        public override string Description => "Sealed Fate: swords keep stabbing with rising damage, capped at 3x.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Phase1Scenarios.FillMana();
            Vector3 spot = c.TrackPoint(5f) ?? Vector3.zero;
            GameObject go = c.BuildNear("Monument", spot, 1.5f, 4f);
            if (go == null) yield break;
            c.Ascend(SealedFate.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/monument/sealed_fate"), "model swapped");
            c.Dummy(spot, 10_000_000);
            yield return c.Wait(25f);
            SpiritSword[] swords = Object.FindObjectsOfType<SpiritSword>();
            SpiritSword oldest = swords.OrderByDescending(s => s.damage).FirstOrDefault();
            SealedSword tag = oldest != null ? oldest.GetComponent<SealedSword>() : null;
            c.Expect(tag != null, $"{swords.Length} sealed swords are stabbing");
            if (tag != null)
            {
                c.Expect(oldest.damage > tag.Original, $"stab damage grew: {tag.Original} → {oldest.damage}");
                c.Expect(oldest.damage <= SealedFateLogic.Cap(tag.Original), "stab damage within the 3x cap");
            }
            yield return Phase1Scenarios.Picture(c, go, "swords", 3f);
        }
    }

    internal sealed class NestOfBatsScenario : Scenario
    {
        public override string Name => "nest-of-bats";
        public override string Description => "Nest of Bats: 10 bats bite a dummy in range once a second each.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Phase1Scenarios.FillMana();
            Vector3 spot = c.TrackPoint(5f) ?? Vector3.zero;
            GameObject go = c.BuildNear("VampireLair", spot, 2f, 5f);
            if (go == null) yield break;
            Tower t = go.GetComponent<Tower>();
            c.Ascend(NestOfBats.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(go, "ascensions/vampire_lair/nest_of_bats"), "model swapped");
            c.Dummy(spot, 10_000_000);
            yield return c.Wait(4f);
            c.Expect(go.GetComponent<NestOfBats>().BatCount == NestOfBatsLogic.BaseBats, $"{NestOfBatsLogic.BaseBats} bats");
            DamageMeter m = c.Meter();
            yield return c.Wait(20f);
            float pulse = t.damage * t.healthDamage / t.rps;
            float bites = NestOfBatsLogic.BaseBats * t.damage * t.healthDamage / NestOfBatsLogic.BiteInterval;
            c.ExpectNear(m.PerSecond(TowerType.VampireLair), pulse + bites, (pulse + bites) * 0.25f, "lair damage per second (pulse + bites)");
            yield return Phase1Scenarios.Picture(c, go, "bats", 3.5f);
        }
    }
}
