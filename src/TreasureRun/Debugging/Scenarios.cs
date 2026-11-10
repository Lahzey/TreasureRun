using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.UI;
using UnityEngine;

namespace TreasureRun.Debugging
{
    /// <summary>
    /// An in-game test (docs/ascension-plan.md §2.4): sets up a situation in the running game scene, measures, and
    /// reports PASS/FAIL through the dev console (and console/out.txt). Many scenarios run back to back in one launch.
    /// </summary>
    internal abstract class Scenario
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        /// <summary>True if the scenario changes the run for good (e.g. grants a treasure); such scenarios run last.</summary>
        public virtual bool Permanent => false;
        public abstract IEnumerator Run(ScenarioContext c);
    }

    /// <summary>Helpers for scenarios. Everything built or spawned through it is removed afterwards.</summary>
    internal sealed class ScenarioContext
    {
        public readonly string ScenarioName;
        public int Passed, Failed;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<DamageMeter> meters = new List<DamageMeter>();

        public ScenarioContext(string name) { ScenarioName = name; }

        public void Info(string text) => DevConsole.Print($"  [{ScenarioName}] {text}");

        public void Expect(bool ok, string what)
        {
            if (ok) Passed++; else Failed++;
            DevConsole.Print($"  [{ScenarioName}] {(ok ? "PASS" : "FAIL")} {what}");
        }

        public void ExpectNear(float actual, float expected, float tolerance, string what) =>
            Expect(Mathf.Abs(actual - expected) <= tolerance, $"{what}: {actual:0.##} (expected {expected:0.##} ± {tolerance:0.##})");

        /// <summary>Waits game time (affected by the scenario speed-up).</summary>
        public IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds); }

        /// <summary>A point on the track about <paramref name="distance"/> units from the castle (null if none).</summary>
        public Vector3? TrackPoint(float distance)
        {
            Waypoint best = null;
            float bestScore = float.MaxValue;
            foreach (Waypoint w in UnityEngine.Object.FindObjectsOfType<Waypoint>())
            {
                float d = Vector3.Distance(Flat(w.transform.position), Flat(GameHelpers.CastlePosition));
                float score = Mathf.Abs(d - distance);
                if (score < bestScore) { bestScore = score; best = w; }
            }
            return best != null ? best.transform.position : (Vector3?)null;
        }

        /// <summary>Builds a tower or building on the free grass cell closest to <paramref name="near"/> (within maxDistance).</summary>
        public GameObject BuildNear(string prefab, Vector3 near, float minDistance = 1f, float maxDistance = 4f)
        {
            var cells = new List<(int x, int z, float d)>();
            int r = Mathf.CeilToInt(maxDistance);
            for (int x = -r; x <= r; x++)
                for (int z = -r; z <= r; z++)
                {
                    int cx = Mathf.RoundToInt(near.x) + x, cz = Mathf.RoundToInt(near.z) + z;
                    float d = Vector2.Distance(new Vector2(cx, cz), new Vector2(near.x, near.z));
                    if (d >= minDistance && d <= maxDistance) cells.Add((cx, cz, d));
                }
            foreach (var cell in cells.OrderBy(c => c.d))
            {
                if (Physics.CheckSphere(new Vector3(cell.x, 0.6f, cell.z), 0.3f, ~0, QueryTriggerInteraction.Ignore)
                    && Physics.OverlapSphere(new Vector3(cell.x, 0.6f, cell.z), 0.3f).Any(col => col.GetComponentInParent<IBuildable>() != null)) continue;
                GameObject go = DevConsole.BuildAt(prefab, cell.x, cell.z, false, out string message);
                if (go == null) continue;
                spawned.Add(go);
                Info(message);
                return go;
            }
            Info($"No free cell for {prefab} near {near}.");
            return null;
        }

        /// <summary>
        /// Spawns a stationary dummy enemy (a vanilla tier-1 monster with the given health layers) on the track at a point.
        /// </summary>
        public Enemy Dummy(Vector3 at, int health, int armor = 0, int shield = 0, float speed = 0f)
        {
            GameObject prefab = SpawnManager.instance.tier1.FirstOrDefault(g => g != null);
            if (prefab == null) { Info("No enemy prefab found."); return null; }
            GameObject go = UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity);
            Enemy e = go.GetComponent<Enemy>();
            e.baseHealth = health; e.baseArmor = armor; e.baseShield = shield;
            e.baseSpeed = speed;
            e.healthRegen = e.armorRegen = e.shieldRegen = 0;
            e.SetStats();
            Waypoint nearest = UnityEngine.Object.FindObjectsOfType<Waypoint>().OrderBy(w => Vector3.SqrMagnitude(w.transform.position - at)).FirstOrDefault();
            if (nearest != null) e.SetFirstSpawnPoint(nearest);
            go.name = "TR_Dummy";
            spawned.Add(go);
            return e;
        }

        /// <summary>Starts counting damage dealt (per tower type) from now on.</summary>
        public DamageMeter Meter()
        {
            var m = new DamageMeter();
            meters.Add(m);
            return m;
        }

        public void Shot(string name)
        {
            string file = $"scenario_{ScenarioName}_{name}";
            DevConsole.Execute("shot " + file);
        }

        public void Cleanup()
        {
            foreach (DamageMeter m in meters) m.Dispose();
            meters.Clear();
            foreach (GameObject go in spawned)
            {
                if (go == null) continue;
                try { go.GetComponent<IBuildable>()?.Demolish(); } catch (Exception e) { Plugin.Log.LogWarning("Scenario cleanup: " + e.Message); }
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            spawned.Clear();
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }

    /// <summary>Damage dealt per tower type while it is alive (from Combat.EnemyDamaged).</summary>
    internal sealed class DamageMeter : IDisposable
    {
        private readonly Dictionary<TowerType, long> byType = new Dictionary<TowerType, long>();
        public readonly float StartTime = Time.time;
        public long Total { get; private set; }

        public DamageMeter() { Combat.EnemyDamaged += OnHit; }

        private void OnHit(HitResult r)
        {
            byType.TryGetValue(r.Source, out long v);
            byType[r.Source] = v + r.DamageDealt;
            Total += r.DamageDealt;
        }

        public long Of(TowerType t) => byType.TryGetValue(t, out long v) ? v : 0;
        public float PerSecond(TowerType t) => Of(t) / Mathf.Max(0.001f, Time.time - StartTime);
        public void Dispose() => Combat.EnemyDamaged -= OnHit;
    }

    /// <summary>Registry and runner of the in-game scenarios.</summary>
    internal static class Scenarios
    {
        /// <summary>Game speed while scenarios run (game time per real second).</summary>
        public const float SpeedUp = 4f;

        public static readonly List<Scenario> All = new List<Scenario>
        {
            new BallistaDpsScenario(),
            new AscensionAttachScenario(),
        };

        private static Runner runner;

        public static void Start(string which)
        {
            List<Scenario> list = which.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? All.OrderBy(s => s.Permanent ? 1 : 0).ToList()
                : All.Where(s => s.Name.IndexOf(which, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (list.Count == 0) { DevConsole.Print($"No scenario matches '{which}'."); return; }
            if (runner == null)
            {
                var go = new GameObject("TR_ScenarioRunner");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                runner = go.AddComponent<Runner>();
            }
            DevConsole.Print($"Running {list.Count} scenario(s): {string.Join(", ", list.Select(s => s.Name))}");
            runner.StartCoroutine(RunAll(list));
        }

        private static IEnumerator RunAll(List<Scenario> list)
        {
            float oldScale = Time.timeScale;
            Time.timeScale = SpeedUp;
            int passed = 0, failed = 0, crashed = 0;
            foreach (Scenario s in list)
            {
                var c = new ScenarioContext(s.Name);
                DevConsole.Print($"[{s.Name}] {s.Description}");
                IEnumerator it = s.Run(c);
                while (true)
                {
                    object current;
                    try
                    {
                        if (!it.MoveNext()) break;
                        current = it.Current;
                    }
                    catch (Exception e)
                    {
                        crashed++;
                        DevConsole.Print($"  [{s.Name}] CRASH {e.GetType().Name}: {e.Message}");
                        Plugin.Log.LogError(e);
                        break;
                    }
                    yield return current;
                }
                c.Cleanup();
                passed += c.Passed;
                failed += c.Failed;
                yield return null;
            }
            Time.timeScale = oldScale;
            DevConsole.Print($"#scenarios finished: {passed} passed, {failed} failed, {crashed} crashed");
        }

        private sealed class Runner : MonoBehaviour { }
    }

    // ------------------------------------------------------------------ built-in scenarios

    /// <summary>Checks the measuring itself: a vanilla Ballista deals 10 x 10 every 3 s to a health dummy.</summary>
    internal sealed class BallistaDpsScenario : Scenario
    {
        public override string Name => "ballista-dps";
        public override string Description => "Vanilla Ballista vs a stationary health dummy: about 33 damage per second.";

        public override IEnumerator Run(ScenarioContext c)
        {
            Vector3? spot = c.TrackPoint(4f);
            c.Expect(spot.HasValue, "found a track point");
            if (!spot.HasValue) yield break;
            GameObject tower = c.BuildNear("CrossbowTower", spot.Value, 1.5f, 3.5f);
            c.Expect(tower != null, "built a Ballista");
            if (tower == null) yield break;
            int expectedPerShot = tower.GetComponent<Tower>().damage * tower.GetComponent<Tower>().healthDamage;
            c.Dummy(spot.Value, 10_000_000);
            yield return c.Wait(2f); // first target pick + first shot in flight
            DamageMeter m = c.Meter();
            yield return c.Wait(30f);
            c.ExpectNear(m.PerSecond(TowerType.Crossbow), expectedPerShot / 3f, expectedPerShot / 3f * 0.2f, "Ballista DPS");
        }
    }

    /// <summary>The test ascension attaches to existing and new Ballistas, applies its stat bonus and swaps the model.</summary>
    internal sealed class AscensionAttachScenario : Scenario
    {
        public override string Name => "ascension-attach";
        public override string Description => "Proving Ground (test ascension): component, stats, model swap, build button.";
        public override bool Permanent => true;

        public override IEnumerator Run(ScenarioContext c)
        {
            TreasureDef def = TreasureRegistry.Get(TestAscension.Id);
            c.Expect(def != null, "test ascension registered (Debug.DebugTools)");
            if (def == null) yield break;

            Vector3 spot = c.TrackPoint(4f) ?? Vector3.zero;
            GameObject before = c.BuildNear("CrossbowTower", spot, 1.5f, 4f);
            yield return null;
            int damageBefore = before.GetComponent<Tower>().damage;

            if (!TreasureInventory.Owns(def.Id)) TreasureInventory.Grant(def);
            yield return null;
            GameObject after = c.BuildNear("CrossbowTower", spot, 1.5f, 4f);
            yield return null;

            foreach (var (go, label) in new[] { (before, "existing"), (after, "new") })
            {
                var asc = go.GetComponent<TestAscension>();
                c.Expect(asc != null, $"{label} Ballista has the ascension component");
                c.Expect(go.GetComponent<Tower>().damage == damageBefore + 5, $"{label} Ballista damage {go.GetComponent<Tower>().damage} = {damageBefore} + 5");
                bool vanillaHidden = go.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => r.GetComponent<MeshFilter>()?.sharedMesh?.name == "default").All(r => !r.enabled);
                bool modelShown = go.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.StartsWith("TR_Model_") && r.enabled);
                c.Expect(vanillaHidden && modelShown, $"{label} Ballista shows the swapped model");
            }

            BuildButtonUI button = AscensionTargets.BuildButton(AscensionTarget.Ballista);
            bool renamed = button != null && button.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t => t.text == "Proving Ground");
            c.Expect(renamed, "build button renamed to Proving Ground");
            c.Expect(AscensionRegistry.DisplayNameOf(after) == "Proving Ground (Ballista)", "display name");
        }
    }
}
