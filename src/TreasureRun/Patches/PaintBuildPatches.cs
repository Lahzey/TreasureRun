using System.Collections.Generic;
using HarmonyLib;
using TreasureRun.Ascensions;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Patches
{
    /// <summary>
    /// Shift-drag building (setting <c>General.DragToBuild</c>, also in the Options menu): in build mode, holding Shift
    /// and the left mouse button after the first placement keeps placing the same tower or building on every cell the
    /// cursor passes (a 4-connected line, <see cref="PaintBuildLogic.Line"/>), like a paint brush. Each placement is
    /// vanilla's own shift-click step: pay, track the cost, build where the ghost is, raise the price. Cells that are
    /// not free grass are skipped; running out of gold stops the stroke.
    /// </summary>
    [HarmonyPatch]
    internal static class PaintBuildPatches
    {
        private static bool painting, warnedGold;
        private static Vector2Int last;
        private static float plane;

        private static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        /// <summary>Runs after vanilla's Update, which has already placed the first one on the click.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(BuildingManager), "Update")]
        private static void UpdatePostfix(BuildingManager __instance)
        {
            if (!ModConfig.DragToBuild.Value || !__instance.buildMode || !Shift || !Input.GetMouseButton(0)) { painting = false; return; }
            Vector2Int? cell = Hovered(__instance);
            if (cell == null) return;
            if (!painting || Input.GetMouseButtonDown(0))
            {
                // The stroke starts on the clicked cell. Vanilla built there already if the click was near the cell's
                // middle; a click nearer its corner builds nothing in vanilla, so the stroke fills its first cell itself
                // (an occupied cell is skipped; vanilla has already said if the gold isn't enough).
                painting = true;
                last = cell.Value;
                warnedGold = !ResourceManager.instance.CheckMoney(__instance.buildingCost);
                if (Input.GetMouseButtonDown(0) && !warnedGold) TryBuild(__instance, last, out _);
                return;
            }
            if (cell.Value == last) return;
            List<Vector2Int> line = PaintBuildLogic.Line(last, cell.Value);
            last = cell.Value;
            for (int i = 1; i < line.Count && i <= 40; i++)
                if (TryBuild(__instance, line[i], out _) == Outcome.Stop) { painting = false; break; }
        }

        /// <summary>The grid cell under the cursor: where the ray meets the map, or (over a building) the plane of the stroke's start.</summary>
        private static Vector2Int? Hovered(BuildingManager bm)
        {
            Camera cam = Camera.main;
            if (cam == null) return null;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Vector3 p;
            if (Physics.Raycast(ray, out RaycastHit hit, 2000f, bm.buildableMask, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<IBuildable>() == null)
            {
                p = hit.point;
                plane = p.y;
            }
            else
            {
                if (Mathf.Abs(ray.direction.y) < 1e-4f) return null;
                float t = (plane - ray.origin.y) / ray.direction.y;
                if (t < 0f) return null;
                p = ray.origin + ray.direction * t;
            }
            return new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));
        }

        internal enum Outcome { Built, Skipped, Stop }

        /// <summary>The position to build on a cell, or null if it isn't free grass.</summary>
        private static Vector3? FreeCell(Vector2Int c, LayerMask mask)
        {
            if (!Physics.Raycast(new Vector3(c.x, 40f, c.y), Vector3.down, out RaycastHit hit, 80f, mask, QueryTriggerInteraction.Ignore)) return null;
            if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Grass")) return null;
            var pos = new Vector3(c.x, Mathf.Round(3f * hit.point.y) / 3f, c.y);
            foreach (Collider col in Physics.OverlapSphere(pos + Vector3.up * 0.45f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                if (col.GetComponentInParent<IBuildable>() != null) return null;
            return pos;
        }

        /// <summary>
        /// One placement on a cell, the way vanilla's shift-click does it. <see cref="Outcome.Stop"/> when the stroke
        /// can't go on (no gold, paused, over a War Blimp deck).
        /// </summary>
        internal static Outcome TryBuild(BuildingManager bm, Vector2Int c, out GameObject built)
        {
            built = null;
            if (PauseMenu.instance != null && PauseMenu.instance.paused) return Outcome.Stop;
            if (BlimpDeckPatches.OverDeck) return Outcome.Stop;
            Vector3? free = FreeCell(c, bm.buildableMask);
            if (free == null) return Outcome.Skipped;
            Vector3 pos = free.Value;
            if (!ResourceManager.instance.CheckMoney(bm.buildingCost))
            {
                if (!warnedGold)
                {
                    warnedGold = true;
                    DamageNumber n = ObjectPool.instance.SpawnObject(ObjectPool.ObjectType.DamageNumber, pos + Vector3.up, Quaternion.identity).GetComponent<DamageNumber>();
                    n.SetText("Not enough gold", "Grey", 1f);
                    n.SetHoldTime(0.25f);
                }
                return Outcome.Stop;
            }
            ResourceManager.instance.Spend(bm.buildingCost);
            DamageTracker.instance.AddCost(bm.tType, bm.buildingCost);
            bm.DisplayGhost(pos, pos.y > 0.34f ? "+" + (Mathf.RoundToInt(pos.y * 3f) - 1) : "");
            bm.Build();
            bm.buildingCost += bm.priceIncrease;
            foreach (Collider col in Physics.OverlapSphere(pos + Vector3.up * 0.45f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                var b = col.GetComponentInParent<IBuildable>() as Component;
                if (b != null) { built = b.gameObject; break; }
            }
            return Outcome.Built;
        }

        /// <summary>A whole stroke from one cell to another, start included (scenarios: the mouse can't be driven from there).</summary>
        internal static List<GameObject> PaintLine(BuildingManager bm, Vector2Int from, Vector2Int to)
        {
            var built = new List<GameObject>();
            warnedGold = false;
            foreach (Vector2Int c in PaintBuildLogic.Line(from, to))
            {
                Outcome o = TryBuild(bm, c, out GameObject go);
                if (o == Outcome.Stop) break;
                if (go != null) built.Add(go);
            }
            return built;
        }
    }
}
