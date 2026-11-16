using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions.Phase3;
using TreasureRun.Core;
using TreasureRun.UI;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>In-game checks of the phase 3 ascensions (granted for the scenario and revoked afterwards).</summary>
    internal static class Phase3Scenarios
    {
        public static IEnumerable<Scenario> All => new Scenario[]
        {
            new SawDimensionScenario(), new SawScalingScenario(), new MegaMortarScenario(), new MegaMortarHillScenario(), new LeyLineScenario(), new LeyLineHillScenario(),
        };
    }

    internal sealed class SawDimensionScenario : Scenario
    {
        public override string Name => "saw-dimension";
        public override string Description => "The Saw Dimension: saws on the whole path deal Σ(Shredder damage)/10 per second, Shredders level up, saws retract without Shredders.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = c.TrackPoint(5f) ?? Vector3.zero;
            Vector3 far = c.TrackPoint(12f) ?? spot;
            // Two Shredders anywhere: the saws work on the whole map, not in range.
            GameObject a = c.BuildNear("Shredder", far, 1f, 4f);
            GameObject b = c.BuildNear("Shredder", far, 1f, 4f);
            if (a == null || b == null) yield break;
            var towers = new[] { a.GetComponent<Tower>(), b.GetComponent<Tower>() };
            c.Ascend(SawEngine.Id);
            yield return null;
            c.Expect(Phase1Scenarios.MeshSwapped(a, "ascensions/shredder/saw_engine_turret"), "Saw Engine model swapped");
            SawField field = SawField.Instance;
            c.Expect(field != null, "saw field exists");
            if (field == null) yield break;
            yield return c.Wait(3f); // layout + saws rising
            c.Expect(field.SawCount >= 1, $"{field.SawCount} saws along the path");
            c.ExpectNear(field.Extension, 1f, 0.01f, "saws fully out");

            Enemy dummy = c.Dummy(spot, 10_000_000);
            int levels0 = towers.Sum(t => t.level);
            float xp0 = towers.Sum(t => t.healthXP);
            yield return c.Wait(1f);
            DamageMeter m = c.Meter();
            yield return c.Wait(15f);
            int sum = towers.Sum(t => t.damage);
            float h = (float)towers.Average(t => t.healthDamage);
            float expected = sum / SawDimensionLogic.DamageDivisor * h;
            // The meter also counts bleed ticks (100% bleed), so allow up to twice the direct damage.
            float dps = m.PerSecond(TowerType.Shredder);
            c.Expect(dps >= expected * 0.8f && dps <= expected * 2.2f,
                     $"saw damage per second {dps:0.#} (direct hits {expected:0.#} = Σ damage {sum} / 10 × health multiplier {h:0.#}, plus bleed)");
            c.Expect(towers.Sum(t => t.level) > levels0 || towers.Sum(t => t.healthXP) > xp0, "Shredders gained XP without attacking");
            yield return Phase1Scenarios.Picture(c, dummy.gameObject, "saws", 4f);

            // Without Shredders the saws sink into the floor and stop.
            foreach (Tower t in towers) t.Demolish();
            yield return c.Wait(2f);
            c.ExpectNear(field.Extension, 0f, 0.01f, "saws retracted without Shredders");
            long hits0 = field.Hits;
            yield return c.Wait(3f);
            c.Expect(field.Hits == hits0, "no saw hits without Shredders");
            yield return Phase1Scenarios.Picture(c, dummy.gameObject, "retracted", 4f);
        }
    }

    internal sealed class SawScalingScenario : Scenario
    {
        public override string Name => "saw-scaling";
        public override string Description => "The Saw Dimension: saw damage grows with every Saw Engine built (1 vs. 4).";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = c.TrackPoint(5f) ?? Vector3.zero;
            Vector3 far = c.TrackPoint(12f) ?? spot;
            var towers = new List<Tower>();
            GameObject first = c.BuildNear("Shredder", far, 1f, 4f);
            if (first == null) yield break;
            towers.Add(first.GetComponent<Tower>());
            c.Ascend(SawEngine.Id);
            c.Dummy(spot, 100_000_000);
            yield return c.Wait(3f);
            DamageMeter one = c.Meter();
            long hits0 = SawField.Instance != null ? SawField.Instance.Hits : 0;
            yield return c.Wait(12f);
            float dpsOne = one.PerSecond(TowerType.Shredder);
            one.Dispose();
            c.Info($"1 engine: Σ damage {towers.Sum(t => t.damage)}, {dpsOne:0.#} DPS, {(SawField.Instance != null ? SawField.Instance.Hits - hits0 : 0)} saw hits");

            for (int i = 0; i < 3; i++)
            {
                GameObject go = c.BuildNear("Shredder", far, 1f, 5f);
                if (go != null) towers.Add(go.GetComponent<Tower>());
            }
            yield return c.Wait(1f);
            c.Expect(SawEngine.All.Count == towers.Count, $"{SawEngine.All.Count} Saw Engines registered for {towers.Count} Shredders");
            DamageMeter four = c.Meter();
            hits0 = SawField.Instance != null ? SawField.Instance.Hits : 0;
            yield return c.Wait(12f);
            float dpsFour = four.PerSecond(TowerType.Shredder);
            c.Info($"{towers.Count} engines: Σ damage {towers.Sum(t => t.damage)}, {dpsFour:0.#} DPS, {(SawField.Instance != null ? SawField.Instance.Hits - hits0 : 0)} saw hits");
            c.Expect(dpsFour > dpsOne * 2.5f, $"damage grows with engines: {dpsOne:0.#} → {dpsFour:0.#} DPS");
        }
    }

    internal sealed class MegaMortarScenario : Scenario
    {
        public override string Name => "mega-mortar";
        public override string Description => "Mega Mortar: an L of 5 modules fuses into a 2x2 (one tower, x4 damage, +2 range, 2x blast) + 1 module; it grows to a 3x3 with weighted levels; prices and selling count every module.";

        private static bool Free(int x, int z)
        {
            if (!Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out RaycastHit hit, 80f, BuildingManager.instance.buildableMask, QueryTriggerInteraction.Ignore))
                return false;
            return hit.collider.gameObject.layer == LayerMask.NameToLayer("Grass");
        }

        private static (int x, int z)? FreeBlock(Vector3 near, int size)
        {
            for (int r = 1; r <= 9; r++)
                for (int x = -r; x <= r; x++)
                    for (int z = -r; z <= r; z++)
                    {
                        int ox = Mathf.RoundToInt(near.x) + x, oz = Mathf.RoundToInt(near.z) + z;
                        bool ok = true;
                        for (int dx = 0; dx < size && ok; dx++)
                            for (int dz = 0; dz < size && ok; dz++)
                                ok = Free(ox + dx, oz + dz);
                        if (ok) return (ox, oz);
                    }
            return null;
        }

        private static List<MortarModule> Units() => MortarModule.All.Where(m => m != null && !m.Dissolved).ToList();

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = c.TrackPoint(6f) ?? Vector3.zero;
            (int x, int z)? origin = FreeBlock(spot, 3);
            for (int i = 0; i < 4 && origin == null; i++)
            {
                DevConsole.Execute("wave"); // the starting map is too small: grow it
                yield return c.Wait(1f);
                origin = FreeBlock(spot, 3);
            }
            if (origin == null) { c.Expect(false, "free 3x3 block near the track"); yield break; }
            int ox = origin.Value.x, oz = origin.Value.z;
            c.Ascend(MortarModule.Id);
            yield return null;
            TowerFlyweight fly = TowerManager.instance.morter;
            int basePrice = fly.basePrice, inc = fly.priceIncrease, before = fly.towers.Count;

            var towers = new List<Tower>();
            foreach (var (dx, dz) in new[] { (0, 0), (1, 0), (0, 1), (1, 1), (0, 2) })
            {
                GameObject go = c.BuildAt("Morter", ox + dx, oz + dz);
                if (go == null) { c.Expect(false, $"module built at {ox + dx},{oz + dz}"); yield break; }
                towers.Add(go.GetComponent<Tower>());
                yield return null;
            }
            yield return c.Wait(0.5f);
            c.Info("heights: " + string.Join(", ", towers.Where(t => t != null).Select(t => "+" + t.heightBonus)));

            List<MortarModule> units = Units();
            MortarModule mega = units.FirstOrDefault(m => m.IsMega);
            c.Expect(mega != null && mega.Size == 2, "an L of 5 modules fused into a 2x2");
            if (mega == null) yield break;
            MortarModule loose = towers[4] != null ? towers[4].GetComponent<MortarModule>() : null;
            c.Expect(loose != null && !loose.IsMega && !loose.Dissolved, "the fifth module stays loose");
            if (loose == null) yield break;
            c.Expect(towers.Count(t => t != null) == 2, $"{towers.Count(t => t != null)} towers left of 5 (one Mega Mortar + one module)");
            c.Expect(MortarModule.ExtraModules == 3 && fly.currentPrice == basePrice + (before + 5) * inc,
                     $"the fusion doesn't change the price: next mortar {fly.currentPrice}g (5 modules owned)");
            c.Expect(mega.DisplayName == "Mega Mortar" && loose.DisplayName == null, "named Mega Mortar (the loose one stays a Mortar Module)");
            Vector3 center = new Vector3(ox + 0.5f, 0f, oz + 0.5f);
            c.Expect(Vector3.Distance(new Vector3(mega.transform.position.x, 0f, mega.transform.position.z), center) < 0.01f, "the Mega Mortar stands in the middle of its square");
            c.Expect(c.BuildAt("Morter", ox + 1, oz + 1) == null, "its tiles can't be built on");

            Tower a = mega.Tower, single = loose.Tower;
            if (a.heightBonus == single.heightBonus)
            {
                c.Expect(a.damage == single.damage * 4, $"damage {a.damage} = 4 x {single.damage}");
                c.Expect(a.healthDamage == single.healthDamage, $"health multiplier {a.healthDamage} averaged (a module has {single.healthDamage})");
                c.ExpectNear(a.range, single.range + 2f, 0.01f, "range +2");
            }
            else c.Info("tiles differ in height: stat checks against the loose module skipped");
            c.ExpectNear(a.blastRadius, single.blastRadius * 2f, 0.01f, "blast radius 2x");

            Enemy dummy = c.Dummy(spot, 50_000_000);
            int maxHit = 0;
            System.Action<HitResult> onHit = r => { if (r.Enemy == dummy) maxHit = Mathf.Max(maxHit, r.DamageDealt); };
            Combat.EnemyDamaged += onHit;
            float xp0 = a.healthXP + a.level * 1000f;
            yield return c.Wait(15f);
            Combat.EnemyDamaged -= onHit;
            c.Expect(mega.Shots >= 2, $"the Mega Mortar fired {mega.Shots} shells");
            int bigHit = a.damage * a.healthDamage;
            c.Expect(maxHit >= bigHit * 0.9f, $"biggest hit on the dummy {maxHit} (one shell: {a.damage} x {a.healthDamage} = {bigHit})");
            c.Expect(a.healthXP + a.level * 1000f > xp0, "it gains XP like a single tower");
            yield return Phase1Scenarios.Picture(c, a.gameObject, "2x2", 4f);

            // Grow to 3x3: a level-5 2x2 (weight 4) plus 5 fresh modules -> level round(25 / 9) = 3.
            a.level = 5; a.damageUpgrade = 4; a.healthDamageUpgrade = 4; a.armorDamageUpgrade = 0; a.shieldDamageUpgrade = 0;
            a.healthXP = a.armorXP = a.shieldXP = 0f;
            single.healthXP = 0f;
            a.SetStats();
            foreach (var (dx, dz) in new[] { (2, 0), (2, 1), (2, 2), (1, 2) })
            {
                if (c.BuildAt("Morter", ox + dx, oz + dz) == null) { c.Expect(false, $"module built at {ox + dx},{oz + dz}"); yield break; }
                yield return null;
            }
            yield return c.Wait(0.5f);
            units = Units();
            c.Expect(units.Count == 1 && units[0] == mega && mega.Size == 3, $"{units.Count} unit(s): the 2x2 grew into a {mega.Size}x{mega.Size}");
            c.Expect(a.level == 3, $"level {a.level} (expected 3: (4 x 5 + 5 x 1) / 9)");
            c.Expect(MortarModule.ExtraModules == 8 && fly.currentPrice == basePrice + (before + 9) * inc, $"9 modules owned: next mortar {fly.currentPrice}g");
            yield return Phase1Scenarios.Picture(c, a.gameObject, "3x3", 5f);

            // Firing: the barrel recoils, steam vents (sideways and, at 3x3, upwards), the shell is the modern one.
            int shots0 = mega.Shots;
            float t0 = Time.time;
            while (mega.Shots == shots0 && Time.time - t0 < 10f) yield return null;
            c.Expect(mega.Shots > shots0, "the 3x3 fired");
            c.Expect(mega.RecoilAmount > 0f, $"barrel recoiling ({mega.RecoilAmount:0.##})");
            yield return Phase1Scenarios.Picture(c, a.gameObject, "steam", 3.5f);
            yield return c.Wait(0.12f);
            yield return Phase1Scenarios.Picture(c, a.gameObject, "steam_later", 3.5f);
            ModernShell shell = Object.FindObjectsOfType<ModernShell>().FirstOrDefault();
            c.Expect(shell != null, "the shell has the modern model");
            yield return c.Wait(0.5f);
            c.Expect(mega.RecoilAmount == 0f, "barrel back in place");
            if (shell != null) yield return Phase1Scenarios.Picture(c, shell.gameObject, "shell", 1f);

            // Demolish sells all 9 modules, one by one.
            int expected = MegaMortarLogic.SellValue(basePrice, inc, before + 9, 9);
            c.Expect(mega.SellValue() == expected, $"sell value {mega.SellValue()}g (expected {expected}g)");
            int gold0 = ResourceManager.instance.gold;
            a.Demolish();
            yield return null;
            c.Expect(ResourceManager.instance.gold - gold0 == expected, $"demolish paid {ResourceManager.instance.gold - gold0}g");
            c.Expect(fly.currentPrice == basePrice + before * inc, $"price back to {fly.currentPrice}g");
        }
    }

    internal sealed class MegaMortarHillScenario : Scenario
    {
        public override string Name => "mega-mortar-hill";
        public override string Description => "Mega Mortar on tiles of different heights: stands on the highest, with a plinth from the lowest tile up (not below the ground).";

        private static float? Ground(int x, int z)
        {
            if (!Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out RaycastHit hit, 80f, BuildingManager.instance.buildableMask, QueryTriggerInteraction.Ignore))
                return null;
            if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Grass")) return null;
            return Mathf.Round(3f * hit.point.y) / 3f;
        }

        public override IEnumerator Run(ScenarioContext c)
        {
            (int x, int z)? origin = null;
            for (int i = 0; i < 6 && origin == null; i++)
            {
                for (int x = -14; x <= 14 && origin == null; x++)
                    for (int z = -14; z <= 14 && origin == null; z++)
                    {
                        float?[] h = { Ground(x, z), Ground(x + 1, z), Ground(x, z + 1), Ground(x + 1, z + 1) };
                        if (h.Any(v => v == null)) continue;
                        if (h.Max().Value - h.Min().Value > 0.2f) origin = (x, z);
                    }
                if (origin == null) { DevConsole.Execute("wave"); yield return c.Wait(1f); }
            }
            if (origin == null) { c.Info("no free 2x2 on a slope found: nothing to check"); yield break; }
            int ox = origin.Value.x, oz = origin.Value.z;
            float low = new[] { Ground(ox, oz), Ground(ox + 1, oz), Ground(ox, oz + 1), Ground(ox + 1, oz + 1) }.Min().Value;
            float top = new[] { Ground(ox, oz), Ground(ox + 1, oz), Ground(ox, oz + 1), Ground(ox + 1, oz + 1) }.Max().Value;
            c.Ascend(MortarModule.Id);
            yield return null;
            foreach (var (dx, dz) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
            {
                if (c.BuildAt("Morter", ox + dx, oz + dz) == null) { c.Expect(false, $"module built at {ox + dx},{oz + dz}"); yield break; }
                yield return null;
            }
            yield return c.Wait(0.5f);
            MortarModule mega = MortarModule.All.FirstOrDefault(m => m != null && !m.Dissolved && m.IsMega);
            c.Expect(mega != null, "fused on the slope");
            if (mega == null) yield break;
            c.ExpectNear(mega.transform.position.y, top, 0.01f, "stands on the highest tile");
            c.Expect(mega.Plinth != null, "plinth under it");
            if (mega.Plinth != null)
            {
                Bounds b = mega.Plinth.GetComponent<MeshRenderer>().bounds;
                c.ExpectNear(b.min.y, low, 0.03f, "plinth starts at the lowest tile");
                c.ExpectNear(b.max.y, top, 0.03f, "plinth reaches the highest tile");
            }
            yield return Phase1Scenarios.Picture(c, mega.gameObject, "slope", 3f);
        }
    }

    internal sealed class LeyLineScenario : Scenario
    {
        public override string Name => "ley-line";
        public override string Description => "Ley Line Network: 3 siphons in a row, each next to one of 3 crystals: 3 taps x (1 + 2 x 1/2) = 6 mana/s; pipes and intakes; splitting the row.";

        private static float? Ground(int x, int z)
        {
            if (!Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out RaycastHit hit, 80f, BuildingManager.instance.buildableMask, QueryTriggerInteraction.Ignore))
                return null;
            if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Grass")) return null;
            return Mathf.Round(3f * hit.point.y) / 3f;
        }

        // The siphon's own list: a part replaced this frame is still in the scene until the end of the frame.
        private static int Parts(LeySiphon s, string model) =>
            s.Parts.Count(p => p != null && p.name == "TR_Ley_" + model);

        public override IEnumerator Run(ScenarioContext c)
        {
            GameObject prefab = Resources.FindObjectsOfTypeAll<ManaCrystal>().Select(m => m.gameObject).FirstOrDefault(g => !g.scene.IsValid());
            if (prefab == null) { c.Expect(false, "mana crystal prefab found"); yield break; }

            // A free row of 5 cells at one height: crystal, siphon, siphon, siphon, crystal.
            (int x, int z, float y)? row = null;
            for (int r = 1; r <= 12 && row == null; r++)
                for (int x = -r; x <= r && row == null; x++)
                    for (int z = -r; z <= r && row == null; z++)
                    {
                        float? y0 = Ground(x, z);
                        if (y0 == null) continue;
                        bool ok = Ground(x + 2, z + 1) is float side && Mathf.Abs(side - y0.Value) < 0.01f;
                        for (int i = 1; i < 5 && ok; i++) ok = Ground(x + i, z) is float y && Mathf.Abs(y - y0.Value) < 0.01f;
                        if (ok) row = (x, z, y0.Value);
                    }
            if (row == null) { c.Expect(false, "free row of 5 cells"); yield break; }
            int ox = row.Value.x, oz = row.Value.z;
            foreach (var (dx, dz) in new[] { (0, 0), (4, 0), (2, 1) })
            {
                GameObject crystal = Object.Instantiate(prefab, new Vector3(ox + dx, row.Value.y, oz + dz), Quaternion.identity);
                // Vanilla rolls the spawn chance in Start and deletes the crystal on a miss: make it always appear.
                ManaCrystal mc = crystal.GetComponent<ManaCrystal>();
                mc.spawnChance = 1f;
                mc.spawnLevels = new Vector2(-1f, 1_000_000f);
                crystal.SetActive(true);
                c.Track(crystal);
            }
            yield return null;
            var siphons = new List<GameObject>();
            for (int i = 1; i <= 3; i++)
            {
                GameObject go = c.BuildAt("ManaSiphon", ox + i, oz);
                if (go == null) { c.Expect(false, $"siphon built at {ox + i},{oz}"); yield break; }
                siphons.Add(go);
                yield return null;
            }
            ResourceManager rm = ResourceManager.instance;
            int shown0 = rm.manaGatherRate;
            c.Info($"vanilla: siphons gathering {string.Join(", ", siphons.Select(s => s.GetComponent<ManaSiphon>().gathering))}, HUD rate +{shown0}");

            c.Ascend(LeySiphon.Id);
            yield return c.Wait(0.5f);
            List<LeySiphon> ley = siphons.Select(s => s.GetComponent<LeySiphon>()).ToList();
            c.Expect(ley.All(l => l != null), "every siphon is a Ley Siphon");
            if (ley.Any(l => l == null)) yield break;
            c.Expect(LeyNetwork.TotalIncome == 6, $"network income {LeyNetwork.TotalIncome} mana/s (3 taps x (1 + 2 x 1/2))");
            c.Expect(ley.All(l => l.Income == 2 && l.IsTap && l.Lit), $"per siphon: {string.Join(", ", ley.Select(l => l.Income))}, all taps, indicators lit");
            c.Expect(rm.manaGatherRate - shown0 == 3, $"HUD mana rate +{rm.manaGatherRate - shown0} (vanilla showed 3 of the 6)");
            c.Expect(Parts(ley[1], "pipe") == 2 && Parts(ley[1], "intake") == 1, "the middle siphon has two pipes and an intake");
            c.Expect(Parts(ley[0], "pipe") == 1 && Parts(ley[0], "intake") == 1, "the end siphons have a pipe and a crystal intake");
            c.Expect(LeyNetwork.Instance != null && LeyNetwork.Instance.Links == 3, $"{LeyNetwork.Instance?.Links} flowing links (3 intakes; all siphons are taps)");

            // Gathering during a wave.
            rm.maxMana = 1_000_000;
            rm.mana = 0;
            if (SpawnManager.instance != null && !SpawnManager.instance.combat) DevConsole.Execute("wave");
            yield return c.Wait(1.5f);
            // Only time spent in a wave counts (the siphons gather during waves only).
            long g0 = ley.Sum(l => l.Gathered);
            float inCombat = 0f;
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                if (SpawnManager.instance.combat) inCombat += Time.deltaTime;
                yield return null;
            }
            float perSecond = (ley.Sum(l => l.Gathered) - g0) / Mathf.Max(1f, inCombat);
            c.ExpectNear(perSecond, 6f, 1.3f, "mana gathered per second by the network");
            DevConsole.Execute($"towerui {ox + 2} {oz}");
            yield return null;
            yield return Phase1Scenarios.Picture(c, siphons[1], "popup", 2.5f);
            DevConsole.Execute("closeui");
            yield return Phase1Scenarios.Picture(c, siphons[1], "network", 2.5f);

            // Taking the middle siphon away splits the row: each end siphon keeps its own crystal.
            ManaSiphon middle = siphons[1].GetComponent<ManaSiphon>();
            middle.Demolish();
            Object.Destroy(siphons[1]);
            yield return c.Wait(0.5f);
            c.Expect(LeyNetwork.TotalIncome == 2, $"after splitting: {LeyNetwork.TotalIncome} mana/s (1 + 1: each end touches one crystal)");
            c.Expect(Parts(ley[0], "pipe") == 0, "the pipe to the demolished siphon is gone (parts: " +
                     string.Join(", ", ley[0].Parts.Where(x => x != null).Select(x => x.name.Replace("TR_Ley_", "") + "@" + Mathf.RoundToInt(x.transform.localEulerAngles.y))) + ")");
            // Vanilla takes the demolished siphon's own +1 off (it touched a crystal); the rest is back to vanilla's.
            c.Expect(rm.manaGatherRate == shown0 - 1, $"HUD mana rate back to vanilla's: +{rm.manaGatherRate}");
        }
    }

    internal sealed class LeyLineHillScenario : Scenario
    {
        public override string Name => "ley-line-hill";
        public override string Description => "Ley Line Network across heights: a low siphon links to higher neighbours with a riser and tee fittings; a crystal on another height still counts.";

        private static float? Ground(int x, int z)
        {
            if (!Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out RaycastHit hit, 80f, BuildingManager.instance.buildableMask, QueryTriggerInteraction.Ignore))
                return null;
            if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Grass")) return null;
            return Mathf.Round(3f * hit.point.y) / 3f;
        }

        private static int Count(LeySiphon s, string model) => s.Parts.Count(p => p != null && p.name == "TR_Ley_" + model);

        public override IEnumerator Run(ScenarioContext c)
        {
            GameObject prefab = Resources.FindObjectsOfTypeAll<ManaCrystal>().Select(m => m.gameObject).FirstOrDefault(g => !g.scene.IsValid());
            if (prefab == null) { c.Expect(false, "mana crystal prefab found"); yield break; }

            // A cell with at least two higher free neighbours and one more free neighbour (any height) for the crystal.
            (int x, int z)? low = null;
            List<(int x, int z)> higher = null;
            (int x, int z) crystalCell = default;
            for (int i = 0; i < 6 && low == null; i++)
            {
                for (int x = -14; x <= 14 && low == null; x++)
                    for (int z = -14; z <= 14 && low == null; z++)
                    {
                        float? h = Ground(x, z);
                        if (h == null) continue;
                        var up = new List<(int, int)>();
                        var other = new List<(int, int)>();
                        foreach (var (dx, dz) in LeyLineLogic.Directions)
                        {
                            float? n = Ground(x + dx, z + dz);
                            if (n == null) continue;
                            if (n.Value > h.Value + 0.2f) up.Add((x + dx, z + dz)); else other.Add((x + dx, z + dz));
                        }
                        bool enough = up.Count >= 2 || up.Count >= 1 && i >= 3;
                        if (enough && (other.Count >= 1 || up.Count >= 3))
                        {
                            low = (x, z);
                            higher = up.Take(2).ToList();
                            // The lowest free neighbour, so a drop down to the crystal is covered when the map has one.
                            crystalCell = other.Count >= 1 ? other.OrderBy(o => Ground(o.Item1, o.Item2) ?? 0f).First() : up[2];
                        }
                    }
                if (low == null) { DevConsole.Execute("killall"); yield return c.Wait(0.5f); DevConsole.Execute("wave"); yield return c.Wait(1f); }
            }
            if (low == null) { c.Info("no low cell with a higher free neighbour found: nothing to check"); yield break; }

            float lowY = Ground(low.Value.x, low.Value.z).Value, crystalY = Ground(crystalCell.x, crystalCell.z).Value;
            int[] rises = higher.Select(h => Mathf.RoundToInt((Ground(h.x, h.z).Value - lowY) * 3f)).ToArray();
            int crystalDiff = Mathf.RoundToInt((crystalY - lowY) * 3f);
            GameObject crystal = Object.Instantiate(prefab, new Vector3(crystalCell.x, crystalY, crystalCell.z), Quaternion.identity);
            ManaCrystal mc = crystal.GetComponent<ManaCrystal>();
            mc.spawnChance = 1f;
            mc.spawnLevels = new Vector2(-1f, 1_000_000f);
            crystal.SetActive(true);
            c.Track(crystal);
            c.Ascend(LeySiphon.Id);
            var built = new List<GameObject>();
            foreach (var cell in new[] { low.Value }.Concat(higher).ToList())
            {
                GameObject go = c.BuildAt("ManaSiphon", cell.x, cell.z);
                if (go == null) { c.Expect(false, $"siphon built at {cell.x},{cell.z}"); yield break; }
                built.Add(go);
                yield return null;
            }
            yield return c.Wait(0.5f);
            List<LeySiphon> ley = built.Select(b => b.GetComponent<LeySiphon>()).ToList();
            LeySiphon bottom = ley[0];
            c.Info($"higher neighbours {string.Join(", ", rises.Select(x => "+" + x))} steps; crystal {crystalDiff:+0;-0;0} steps");
            int count = 1 + higher.Count;
            c.Expect(bottom.NetworkSiphons == count, $"one network of {bottom.NetworkSiphons} siphons across the heights");
            c.Expect(LeyNetwork.TotalIncome == 1, $"{LeyNetwork.TotalIncome} mana/s (only the low siphon touches the crystal)");
            c.Expect(bottom.IsTap && ley.Skip(1).All(l => !l.IsTap && l.Income == 0), "the higher siphons are connectors");
            c.Expect(ley.All(l => l.Lit), "all indicators lit");
            c.Expect(Count(bottom, "riser") == Count(bottom, "corner") && Count(bottom, "riser") >= 1, "the low siphon has a riser (one segment per corner)");
            int tees = rises.Concat(crystalDiff > 0 ? new[] { crystalDiff } : new int[0]).Distinct().Count();
            c.Expect(Count(bottom, "corner") == tees, $"{Count(bottom, "corner")} tee fittings (one per level: {tees})");
            c.Expect(ley.Skip(1).All(l => Count(l, "riser") == 0 || l.Parts.Count > 0), "the higher siphons need no riser towards the low one");
            yield return Phase1Scenarios.Picture(c, built[0], "slope", 2.5f);
            DevConsole.Execute($"towerui {low.Value.x} {low.Value.z}");
            yield return null;
            yield return Phase1Scenarios.Picture(c, built[0], "popup", 3f);
            DevConsole.Execute("closeui");
        }
    }
}
