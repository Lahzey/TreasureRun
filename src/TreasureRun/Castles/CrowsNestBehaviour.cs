using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Crow's Nest: a towering keep with a building platform on top at +<see cref="PlatformHeight"/> high ground
    /// (one cell). Every free cell of the start tile is filled with iron veins, so the platform is the only place to
    /// build at the start.
    ///
    /// The platform is a thin collider on the "Grass" layer, which is what vanilla building placement accepts
    /// (BuildingManager.SamplePoint). It sits on top of the (tall) model at <see cref="PlatformY"/>; vanilla would give
    /// that height +13, so towers on the platform are corrected to +10 (SetStats postfix, build ghost text). The
    /// castle's own colliders are capped below the platform so the placement ray reaches it.
    /// </summary>
    [HarmonyPatch]
    internal sealed class CrowsNestBehaviour : CastleBehaviour
    {
        /// <summary>High ground granted on the platform (mechanics).</summary>
        public const int PlatformHeight = 10;
        /// <summary>
        /// World height of the platform surface (visual, top of voxel layer 49). Placement snaps to thirds, so this is
        /// 14/3 = +13 by vanilla rules; towers standing on the platform are corrected down to +<see cref="PlatformHeight"/>.
        /// </summary>
        public const float PlatformY = 14f / 3f;

        private int veins;

        public override void OnGameStart()
        {
            BuildPlatform();
            // After the starting developments are settled (destroyed objects disappear at the end of the frame).
            CastleObject.AddComponent<DelayedAction>().Run(1, FillStartTileWithOre);
        }

        private void BuildPlatform()
        {
            Vector3 c = CastleObject.transform.position;
            float top = PlatformY;

            // Anything of the castle that would stick out above the platform would block the placement ray.
            foreach (Collider col in CastleObject.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger || col.bounds.max.y <= top - 0.05f) continue;
                if (col is BoxCollider box)
                {
                    // Cap the box just below the platform surface (keep its footprint so buildings can't overlap).
                    Bounds b = col.bounds;
                    float newTop = top - 0.1f;
                    Vector3 worldCenter = new Vector3(b.center.x, (b.min.y + newTop) / 2f, b.center.z);
                    Vector3 worldSize = new Vector3(b.size.x, Mathf.Max(0.05f, newTop - b.min.y), b.size.z);
                    Vector3 lossy = box.transform.lossyScale;
                    box.center = box.transform.InverseTransformPoint(worldCenter);
                    box.size = new Vector3(worldSize.x / Mathf.Abs(lossy.x), worldSize.y / Mathf.Abs(lossy.y), worldSize.z / Mathf.Abs(lossy.z));
                }
                else col.enabled = false;
            }

            var platform = new GameObject("TR_CrowsNestPlatform") { layer = LayerMask.NameToLayer("Grass") };
            platform.transform.SetParent(CastleObject.transform, true);
            platform.transform.position = new Vector3(c.x, top - 0.05f, c.z);
            platform.transform.rotation = Quaternion.identity;
            var collider = platform.AddComponent<BoxCollider>();
            Vector3 s = platform.transform.lossyScale;
            collider.size = new Vector3(0.98f / s.x, 0.1f / s.y, 0.98f / s.z);
        }

        private void FillStartTileWithOre()
        {
            foreach (Vector3 cell in MapDecorations.FreeStartTileCells())
                if (MapDecorations.Spawn<IronVein>(cell) != null) veins++;
            Plugin.Log.LogInfo($"Crow's Nest: {veins} iron veins placed on the start tile.");
        }

        private static bool OnPlatform(Vector3 p)
        {
            if (!(CastleRuntime.Behaviour is CrowsNestBehaviour) || p.y < PlatformY - 0.2f) return false;
            Vector3 c = Core.GameHelpers.CastlePosition;
            return Mathf.Abs(p.x - c.x) < 0.5f && Mathf.Abs(p.z - c.z) < 0.5f;
        }

        /// <summary>High ground a tower effectively has (the platform counts as +PlatformHeight). For treasures like High Ground.</summary>
        public static int EffectiveHeightBonus(Tower t) =>
            OnPlatform(t.transform.position) ? Mathf.Min(t.heightBonus, PlatformHeight) : t.heightBonus;

        /// <summary>Towers on the platform get +PlatformHeight high ground, not the +13 their height would give.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(Tower), nameof(Tower.SetStats)), HarmonyPriority(Priority.High)]
        private static void TowerSetStatsPostfix(Tower __instance)
        {
            if (!OnPlatform(__instance.transform.position)) return;
            int excess = __instance.heightBonus - PlatformHeight;
            if (excess <= 0) return;
            __instance.damage = Mathf.Max(0, __instance.damage - excess);
            __instance.range -= excess / 2f * (1f + TowerManager.instance.GetHeightRangeBonusMultiplier(__instance.towerType));
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BuildingGhost), nameof(BuildingGhost.SetText))]
        private static void GhostTextPrefix(BuildingGhost __instance, ref string newText)
        {
            if (OnPlatform(__instance.transform.position)) newText = "+" + PlatformHeight;
        }

        public override string InfoText()
        {
            bool occupied = Object.FindObjectsOfType<Tower>().Any(t => t.transform.position.y > PlatformY - 0.2f
                && Mathf.Abs(t.transform.position.x - CastleObject.transform.position.x) < 0.5f
                && Mathf.Abs(t.transform.position.z - CastleObject.transform.position.z) < 0.5f);
            return $"Building platform: +{PlatformHeight} high ground ({(occupied ? "occupied" : "free")})";
        }
    }
}
