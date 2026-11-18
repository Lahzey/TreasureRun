using System.Collections;
using System.Linq;
using TreasureRun.Core;
using TreasureRun.Patches;
using TreasureRun.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TreasureRun.Debugging
{
    /// <summary>
    /// Shift-drag building (<see cref="PaintBuildPatches"/>): a stroke over five free grass cells places five Ballistas at
    /// rising prices, the same stroke again places none, and the Options panel has the setting's row under the loading
    /// animation's. The mouse itself can't be driven from here: the stroke goes through the same placement code.
    /// </summary>
    internal sealed class PaintBuildScenario : Scenario
    {
        public override string Name => "paint-build";
        public override string Description => "Shift-drag building: a stroke over 5 free cells places 5 Ballistas at rising prices, again places none; the Options row exists.";

        public override IEnumerator Run(ScenarioContext c)
        {
            (int X, int Z)? row = null;
            for (int r = 1; r <= 14 && row == null; r++)
                for (int x = -r; x <= r && row == null; x++)
                    for (int z = -r; z <= r && row == null; z++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != r) continue;
                        bool ok = true;
                        for (int i = 0; i < 5 && ok; i++) ok = RitualScenarios.Grass(x + i, z) != null;
                        if (ok) row = (x, z);
                    }
            if (row == null) { c.Expect(false, "a free row of 5 grass cells"); yield break; }
            var (rx, rz) = row.Value;

            DevConsole.Execute("gold 100k");
            BuildButtonUI button = Resources.FindObjectsOfTypeAll<BuildButtonUI>()
                .FirstOrDefault(b => b.gameObject.scene.IsValid() && b.tower != null && b.tower.name == "CrossbowTower");
            if (button == null) { c.Expect(false, "Ballista build button found"); yield break; }
            button.Build();
            BuildingManager bm = BuildingManager.instance;
            c.Expect(bm.buildMode, "build mode");
            int price0 = bm.buildingCost, inc = bm.priceIncrease, gold0 = ResourceManager.instance.gold;

            var built = PaintBuildPatches.PaintLine(bm, new Vector2Int(rx, rz), new Vector2Int(rx + 4, rz));
            foreach (GameObject go in built) c.Track(go);
            c.Expect(built.Count == 5, $"a stroke over five free cells places five ({built.Count}, row at {rx},{rz})");
            bool inPlace = Enumerable.Range(0, 5).All(i => built.Any(g => g != null && Mathf.RoundToInt(g.transform.position.x) == rx + i &&
                                                                         Mathf.RoundToInt(g.transform.position.z) == rz));
            c.Expect(inPlace, "one on each cell");
            int spent = gold0 - ResourceManager.instance.gold, expected = 5 * price0 + inc * (0 + 1 + 2 + 3 + 4);
            c.Expect(spent == expected, $"each at its usual price, rising ({spent} spent, expected {expected})");
            c.Expect(bm.buildingCost == price0 + 5 * inc, $"the next one costs {bm.buildingCost} (expected {price0 + 5 * inc})");
            var again = PaintBuildPatches.PaintLine(bm, new Vector2Int(rx, rz), new Vector2Int(rx + 4, rz));
            foreach (GameObject go in again) c.Track(go);
            c.Expect(again.Count == 0, $"painting over them again places none ({again.Count})");
            bm.ExitBuildMode();
            yield return null;
            c.Expect(built.All(g => g != null && g.GetComponent<Tower>() != null), "they are working Ballistas");
            yield return Phase1Scenarios.Picture(c, built[2], "row", 3.5f);

            // The setting's row in the Options panel, under the loading animation's.
            Toggle drag = Resources.FindObjectsOfTypeAll<Toggle>().FirstOrDefault(t => t.name == "TreasureRun_DragToBuild" && t.gameObject.scene.IsValid());
            Toggle load = Resources.FindObjectsOfTypeAll<Toggle>().FirstOrDefault(t => t.name == "TreasureRun_LoadingAnimation" && t.gameObject.scene.IsValid());
            c.Expect(drag != null && load != null, "the Options panel has both mod rows");
            if (drag != null && load != null)
            {
                float dy = ((RectTransform)load.transform).anchoredPosition.y - ((RectTransform)drag.transform).anchoredPosition.y;
                c.Expect(dy > 1f, $"the new row is under the loading animation's ({dy:0.#} lower)");
                c.Expect(drag.isOn == ModConfig.DragToBuild.Value, "and shows the setting");
            }
        }
    }
}
