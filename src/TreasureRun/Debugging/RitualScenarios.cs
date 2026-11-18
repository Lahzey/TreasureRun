using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions.Phase1;
using TreasureRun.Ascensions.Phase3;
using TreasureRun.Core;
using TreasureRun.UI;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>In-game checks of Ritual of the Sun (part of <see cref="Phase3Scenarios.All"/>).</summary>
    internal static class RitualScenarios
    {
        /// <summary>Builds Sun Obelisks round a point, at the given angles (degrees) and distance; null entries where no cell was free.</summary>
        public static List<GameObject> Around(ScenarioContext c, Vector3 spot, float radius, params float[] angles)
        {
            var built = new List<GameObject>();
            foreach (float a in angles)
            {
                float r = a * Mathf.Deg2Rad;
                built.Add(c.BuildNear("Obelisk", spot + new Vector3(Mathf.Cos(r), 0f, Mathf.Sin(r)) * radius, 0f, 3f));
            }
            return built;
        }

        /// <summary>The height of a free grass cell (null if it isn't one).</summary>
        public static float? Grass(int x, int z)
        {
            if (!Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out RaycastHit hit, 80f, BuildingManager.instance.buildableMask, QueryTriggerInteraction.Ignore))
                return null;
            if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Grass")) return null;
            return Mathf.Round(3f * hit.point.y) / 3f;
        }

        /// <summary>Three cells round a centre cell whose triangle has its centroid exactly on that cell (each side 3.6 to 4).</summary>
        public static readonly (int X, int Z)[] Triangle = { (-2, -1), (2, -1), (0, 2) };

        /// <summary>The cell nearest to a point (within <paramref name="reach"/>) whose <see cref="Triangle"/> corners are all free grass.</summary>
        public static (int X, int Z)? TriangleNear(Vector3 near, float reach, bool centreOnGrass, Vector3? avoid = null, float avoidDistance = 0f)
        {
            (int, int)? best = null;
            float bestD = float.MaxValue;
            int r = Mathf.CeilToInt(reach);
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    int x = Mathf.RoundToInt(near.x) + dx, z = Mathf.RoundToInt(near.z) + dz;
                    float d = new Vector2(x - near.x, z - near.z).magnitude;
                    if (d > reach || d >= bestD) continue;
                    if (avoid.HasValue && new Vector2(x - avoid.Value.x, z - avoid.Value.z).magnitude < avoidDistance) continue;
                    if (centreOnGrass && Grass(x, z) == null) continue;
                    if (Triangle.Any(k => Grass(x + k.X, z + k.Z) == null)) continue;
                    best = (x, z);
                    bestD = d;
                }
            return best;
        }

        public static string Where(IEnumerable<GameObject> gos) =>
            string.Join(" ", gos.Where(g => g != null).Select(g => $"({g.transform.position.x:0},{g.transform.position.z:0})"));
    }

    internal sealed class RitualSunScenario : Scenario
    {
        public override string Name => "ritual-sun";
        public override string Description => "Ritual of the Sun: two Sun Obelisks link and fire with 25% burn; a third closes a ring: prisms up, no own fire, " +
                                               "the sun beam hits with their average damage at their rate and 100% burn, they still gain XP; demolishing one breaks the ring.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = c.TrackPoint(9f) ?? Vector3.zero;
            c.Ascend(SunObelisk.Id);
            (int X, int Z)? tri = RitualScenarios.TriangleNear(spot, 4f, false);
            if (tri == null) { c.Expect(false, "free cells for a triangle by the track"); yield break; }
            var (tx, tz) = tri.Value;
            // The dummy stands in the middle of the triangle (in every obelisk's range, and where the beam will come down).
            spot = new Vector3(tx, spot.y, tz);
            Enemy dummy = c.Dummy(spot, 50_000_000);
            bool wasCombat = Phase2Scenarios.Combat(true);
            List<GameObject> first = RitualScenarios.Triangle.Take(2).Select(k => c.BuildAt("Obelisk", tx + k.X, tz + k.Z)).ToList();
            if (first.Any(g => g == null)) { c.Expect(false, "two obelisks built"); yield break; }
            yield return null;
            yield return null;
            RitualGrid grid = RitualGrid.Instance;
            c.Expect(grid != null, "the ritual grid exists");
            if (grid == null) yield break;
            List<SunObelisk> suns = first.Select(g => g.GetComponent<SunObelisk>()).ToList();
            c.Expect(suns.All(s => s != null), "both are Sun Obelisks");
            c.Info("obelisks at " + RitualScenarios.Where(first) + $", dummy at ({spot.x:0.#},{spot.z:0.#})");
            c.Expect(grid.Solution != null && grid.Solution.Links.Count == 1 && grid.Solution.Rings.Count == 0, "two obelisks: one link, no ring");
            c.ExpectNear(suns[0].Tower.burnPercent, RitualLogic.ObeliskBurn, 0.001f, "a Sun Obelisk's burn");

            // Alone or in a chain they fire as usual (with burn).
            DamageMeter meter = c.Meter();
            yield return c.Wait(1.5f);
            c.Expect(meter.Of(TowerType.Obelisk) > 0, $"the chain's obelisks fire on their own ({meter.Of(TowerType.Obelisk)} damage)");
            c.Expect(suns.Any(s => s.Obelisk.beam.activeSelf), "their (golden) beams are on");
            yield return Phase1Scenarios.Picture(c, first[0], "chain", 4f);

            // A third closes the ring.
            var third = new List<GameObject> { c.BuildAt("Obelisk", tx + RitualScenarios.Triangle[2].X, tz + RitualScenarios.Triangle[2].Z) };
            if (third[0] == null) { c.Expect(false, "third obelisk built"); yield break; }
            yield return null;
            yield return null;
            suns.Add(third[0].GetComponent<SunObelisk>());
            c.Info("obelisks at " + RitualScenarios.Where(first.Concat(third)));
            c.Expect(grid.Solution.Rings.Count == 1 && grid.Solution.Links.Count == 3, $"three obelisks: a ring ({grid.Solution.Rings.Count} rings, {grid.Solution.Links.Count} links)");
            SunRing ring = grid.Rings.FirstOrDefault(r => r != null && !r.Breaking);
            if (ring == null) { c.Expect(false, "the ring ignites"); Phase2Scenarios.Combat(wasCombat); yield break; }
            c.Expect(suns.All(s => s.InRing && s.Ring == ring), "all three are in it");
            c.ExpectNear(ring.Radius, RitualLogic.Radius(3), 0.001f, "the beam's radius for three obelisks");
            Vector3 centre = (first[0].transform.position + first[1].transform.position + third[0].transform.position) / 3f;
            c.ExpectNear(Vector2.Distance(new Vector2(ring.Centre.x, ring.Centre.z), new Vector2(centre.x, centre.z)), 0f, 0.05f, "the beam's centre (the triangle's centroid)");
            float? groundAtCentre = ring.Field.Ground(ring.Centre.x, ring.Centre.z);
            c.Expect(groundAtCentre == null || ring.Centre.y <= groundAtCentre.Value + 0.01f, $"the beam reaches down to the lowest ground under it ({ring.Centre.y:0.##})");
            // A dummy right in the beam (the first one stands where the obelisks can see it, for their XP).
            Enemy inBeam = c.Dummy(new Vector3(ring.Centre.x, groundAtCentre ?? ring.Centre.y, ring.Centre.z), 50_000_000);
            yield return c.Wait(SunRing.StrikeTime + 0.35f);
            c.Expect(ring.Active, "the sun has struck");
            c.Expect(suns.All(s => !s.Obelisk.beam.activeSelf), "the obelisks no longer fire themselves");
            float upness = suns.Min(s => Vector3.Dot(s.Tower.turret.transform.forward, Vector3.up));
            c.Expect(upness > 0.97f, $"every prism points at the sky (min dot {upness:0.###})");

            // The beam: average damage at the obelisks' rate with 100% burn; the obelisks gain XP.
            float avg = (float)suns.Average(s => s.Tower.damage);
            c.ExpectNear(ring.Stats.Damage, avg, 0.01f, "the beam's damage is the obelisks' average");
            c.ExpectNear(ring.Stats.Burn, RitualLogic.BeamBurn, 0.001f, "the beam's burn");
            float xpBefore = suns.Sum(s => s.Tower.healthXP + s.Tower.armorXP + s.Tower.shieldXP) + suns.Sum(s => s.Tower.level) * 1000f;
            long ticks0 = ring.Ticks, beamDamage0 = meter.Of(TowerType.Obelisk);
            inBeam.burn = 0;
            int health0 = inBeam.health;
            yield return c.Wait(2f);
            long ticks = ring.Ticks - ticks0;
            c.ExpectNear(ticks / 2f, ring.Stats.Rate, 1.5f, "beam hits per second");
            c.Expect(suns.All(s => !s.Obelisk.beam.activeSelf), "still no own fire");
            float xpAfter = suns.Sum(s => s.Tower.healthXP + s.Tower.armorXP + s.Tower.shieldXP) + suns.Sum(s => s.Tower.level) * 1000f;
            c.Expect(xpAfter > xpBefore, $"the obelisks gain XP in the ring ({xpBefore:0.#} -> {xpAfter:0.#})");
            long dealt = meter.Of(TowerType.Obelisk) - beamDamage0;
            c.Expect(dealt > 0, $"the beam's damage counts as Obelisk damage ({dealt})");
            // 100% burn: the burn added is about the damage dealt (minus what burning used up meanwhile).
            int dealtIn = health0 - inBeam.health;
            c.Expect(dealtIn > 0 && inBeam.burn >= dealtIn * 0.5f, $"100% burn ({inBeam.burn} burn after {dealtIn} damage)");
            yield return Phase1Scenarios.Picture(c, ring.gameObject, "ring", 5f);

            // Demolishing one breaks the ring; the others fire again.
            third[0].GetComponent<IBuildable>().Demolish();
            Object.Destroy(third[0]);
            yield return null;
            yield return null;
            c.Expect(ring == null || ring.Breaking, "the ring breaks");
            yield return c.Wait(1f);
            c.Expect(ring == null, "the beam is gone");
            c.Expect(grid.Rings.Count(r => r != null) == 0, "no ring left");
            c.Expect(suns.Take(2).All(s => !s.InRing), "the two left are out of the ring");
            yield return c.Wait(0.8f);
            c.Expect(suns.Take(2).Any(s => s.Obelisk.beam.activeSelf), "and fire on their own again");
            Phase2Scenarios.Combat(wasCombat);
        }
    }

    internal sealed class RitualCrystalScenario : Scenario
    {
        public override string Name => "ritual-crystal";
        public override string Description => "Ritual of the Sun easter egg: a ring whose centre falls on a mana crystal powers it: the crystal turns gold, " +
                                               "a ritual circle 3x the beam's radius round it is the damage area; breaking the ring restores the crystal.";

        private static bool Gold(ManaCrystal m) =>
            m != null && m.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Any(x => x != null && x.name.EndsWith("_Sun")));

        public override IEnumerator Run(ScenarioContext c)
        {
            GameObject prefab = Resources.FindObjectsOfTypeAll<ManaCrystal>().Select(m => m.gameObject).FirstOrDefault(g => !g.scene.IsValid());
            if (prefab == null) { c.Expect(false, "mana crystal prefab found"); yield break; }
            // A crystal cell with three free cells round it whose triangle has its centroid exactly on the crystal.
            var corners = RitualScenarios.Triangle;
            // By the track, away from the castle (it would hide the crystal).
            Vector3 track = c.TrackPoint(9f) ?? new Vector3(6f, 0f, 6f);
            (int X, int Z)? spot = RitualScenarios.TriangleNear(track, 6f, true, GameHelpers.CastlePosition, 4f);
            if (spot == null) { c.Expect(false, "a free spot for a crystal and three obelisks"); yield break; }
            int cx = spot.Value.X, cz = spot.Value.Z;
            float cy = RitualScenarios.Grass(cx, cz) ?? 0f;
            GameObject crystalGo = Object.Instantiate(prefab, new Vector3(cx, cy, cz), Quaternion.identity);
            ManaCrystal crystal = crystalGo.GetComponent<ManaCrystal>();
            crystal.spawnChance = 1f;
            crystal.spawnLevels = new Vector2(-1f, 1_000_000f);
            crystalGo.SetActive(true);
            c.Track(crystalGo);
            yield return null;
            c.Ascend(SunObelisk.Id);
            var obelisks = new List<GameObject>();
            foreach (var (dx, dz) in corners)
            {
                GameObject go = c.BuildAt("Obelisk", cx + dx, cz + dz);
                if (go == null) { c.Expect(false, $"obelisk built at {cx + dx},{cz + dz}"); yield break; }
                obelisks.Add(go);
            }
            yield return null;
            yield return null;
            RitualGrid grid = RitualGrid.Instance;
            SunRing ring = grid != null ? grid.Rings.FirstOrDefault(r => r != null && !r.Breaking) : null;
            c.Expect(ring != null, $"the ring closes round the crystal at ({cx},{cz})");
            if (ring == null) yield break;
            c.Expect(ring.Crystal == crystal, "the beam comes down on the crystal");
            yield return c.Wait(SunRing.StrikeTime + 0.4f);
            c.Expect(ring.Powered, "the crystal is powered");
            c.Expect(Gold(crystal), "the crystal has turned gold");
            c.ExpectNear(ring.DamageRadius, ring.Radius * RitualLogic.CrystalCircleFactor, 0.001f, "the ritual circle is the damage area");

            // A monster outside the beam but inside the circle burns.
            bool wasCombat = Phase2Scenarios.Combat(true);
            float d = ring.Radius * 2f;
            float? dy = ring.Field != null ? ring.Field.Ground(cx + d, cz) : null;
            c.Info($"dummy at {d:0.##} from the crystal, ground {(dy.HasValue ? dy.Value.ToString("0.##") : "none")}");
            Enemy dummy = c.Dummy(new Vector3(cx + d, dy ?? cy, cz), 50_000_000);
            if (dummy == null) { c.Expect(false, "dummy spawned"); Phase2Scenarios.Combat(wasCombat); yield break; }
            int health0 = dummy.health;
            yield return c.Wait(1.5f);
            c.Expect(dummy.health < health0, $"a monster {d:0.##} from the crystal (beam radius {ring.Radius:0.##}) is hit ({health0 - dummy.health} damage)");
            DevConsole.Execute("active UniverseLibCanvas 0");
            DevConsole.Execute("speed 0");
            yield return Phase1Scenarios.Picture(c, crystalGo, "circle", 4.5f);
            yield return Phase1Scenarios.Picture(c, crystalGo, "circle_close", 2.4f);
            DevConsole.Execute("speed 4");
            DevConsole.Execute("active UniverseLibCanvas 1");

            // Breaking the ring gives the crystal back its colour.
            obelisks[0].GetComponent<IBuildable>().Demolish();
            Object.Destroy(obelisks[0]);
            yield return c.Wait(1.2f);
            c.Expect(ring == null, "the ring is gone");
            c.Expect(!Gold(crystal), "the crystal is blue again");
            Phase2Scenarios.Combat(wasCombat);
        }
    }

    internal sealed class RitualLookScenario : Scenario
    {
        public override string Name => "ritual-look";
        public override string Description => "Ritual of the Sun visuals: five obelisks round the track, the ignition in frames (game time at 15%), the ring at rest, close-ups.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3 spot = c.TrackPoint(11f) ?? Vector3.zero;
            c.Ascend(SunObelisk.Id);
            var dummies = new List<Enemy>();
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.7f, r = 0.4f + 0.45f * i;
                dummies.Add(c.Dummy(spot + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), 50_000_000));
            }
            bool wasCombat = Phase2Scenarios.Combat(true);
            List<GameObject> gos = RitualScenarios.Around(c, spot, 2.6f, 90f, 162f, 234f, 306f);
            yield return null;
            yield return null;
            c.Info("obelisks at " + RitualScenarios.Where(gos));
            DevConsole.Execute("active UniverseLibCanvas 0");
            yield return c.Wait(1f);
            DevConsole.Execute("speed 0");
            yield return Phase1Scenarios.Picture(c, gos[0], "open_chain", 6f);
            DevConsole.Execute("speed 1");
            // The fifth closes the ring: frames of the ignition at 15% game time.
            List<GameObject> last = RitualScenarios.Around(c, spot, 2.6f, 18f);
            yield return null;
            RitualGrid grid = RitualGrid.Instance;
            SunRing ring = grid != null ? grid.Rings.FirstOrDefault(r => r != null && !r.Breaking) : null;
            c.Expect(ring != null, $"the ring closes ({grid?.Solution?.Rings.Count ?? 0} rings, obelisks {RitualScenarios.Where(gos.Concat(last))})");
            if (ring == null) { Phase2Scenarios.Combat(wasCombat); DevConsole.Execute("active UniverseLibCanvas 1"); yield break; }
            const int frames = 10;
            DevConsole.Execute("speed 0.15");
            for (int f = 0; f < frames; f++)
            {
                yield return c.Wait((SunRing.StrikeTime + 0.4f) / frames);
                DevConsole.Execute("speed 0");
                yield return Phase1Scenarios.Picture(c, ring.gameObject, $"ignite_{f:00}", 7f);
                DevConsole.Execute("speed 0.15");
            }
            DevConsole.Execute("speed 1");
            yield return c.Wait(1.5f);
            DevConsole.Execute("speed 0");
            yield return Phase1Scenarios.Picture(c, ring.gameObject, "ring_wide", 9f);
            yield return Phase1Scenarios.Picture(c, ring.gameObject, "ring", 6f);
            yield return Phase1Scenarios.Picture(c, ring.gameObject, "ring_close", 3f);
            yield return Phase1Scenarios.Picture(c, gos[0], "prism", 1.6f);
            DevConsole.Execute("speed 1");
            // The ring breaks.
            if (last[0] != null) { last[0].GetComponent<IBuildable>().Demolish(); Object.Destroy(last[0]); }
            yield return c.Wait(0.25f);
            DevConsole.Execute("speed 0");
            yield return Phase1Scenarios.Picture(c, gos[0], "breaking", 6f);
            DevConsole.Execute("speed 4");
            DevConsole.Execute("active UniverseLibCanvas 1");
            Phase2Scenarios.Combat(wasCombat);
            foreach (Enemy d in dummies) if (d != null) SpawnManager.instance.currentEnemies.Remove(d);
        }
    }
}
