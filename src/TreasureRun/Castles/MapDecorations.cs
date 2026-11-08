using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Places vanilla map decorations (trees, graves, iron veins, ...) at runtime. The prefabs are the
    /// SpawnableObjects inside the tile prefabs; spawned copies are forced to pass their own spawn roll.
    /// Tiles are 7x7 cells; cells are integer world x/z, the castle sits at 0,0.
    /// </summary>
    public static class MapDecorations
    {
        private static readonly HashSet<SpawnableObject> forced = new HashSet<SpawnableObject>();

        public static bool IsForced(SpawnableObject so) => forced.Contains(so);

        public static T Prefab<T>() where T : SpawnableObject =>
            Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(x => x != null && !x.gameObject.scene.IsValid() && x.GetType() == typeof(T));

        /// <summary>Spawns a copy of the decoration prefab at a cell position (y = ground height).</summary>
        public static T Spawn<T>(Vector3 position, Transform parent = null) where T : SpawnableObject
        {
            T prefab = Prefab<T>();
            if (prefab == null) { Plugin.Log.LogWarning($"No {typeof(T).Name} prefab loaded."); return null; }
            GameObject go = Object.Instantiate(prefab.gameObject, position, Quaternion.identity, parent);
            go.SetActive(true);
            T so = go.GetComponent<T>();
            so.spawnChance = 2f;
            so.spawnLevels = new Vector2(-1f, 99999f);
            forced.Add(so);
            return so;
        }

        /// <summary>
        /// Free buildable grass cells (same test as vanilla building placement) in a square around a centre.
        /// Returns the snapped placement position (ground height).
        /// </summary>
        public static List<Vector3> FreeCells(Vector3 center, int radius)
        {
            var result = new List<Vector3>();
            if (BuildingManager.instance == null) return result;
            int grass = LayerMask.NameToLayer("Grass");
            int cx = Mathf.RoundToInt(center.x), cz = Mathf.RoundToInt(center.z);
            for (int x = cx - radius; x <= cx + radius; x++)
                for (int z = cz - radius; z <= cz + radius; z++)
                {
                    if (!Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out RaycastHit hit, 80f,
                                         BuildingManager.instance.buildableMask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.gameObject.layer != grass) continue;
                    var snapped = new Vector3(x, Mathf.Round(3f * hit.point.y) / 3f, z);
                    if ((hit.point - snapped).sqrMagnitude >= 0.25f) continue;
                    result.Add(snapped);
                }
            return result;
        }

        /// <summary>Free cells on every placed tile.</summary>
        public static List<Vector3> FreeCellsOnMap() =>
            Object.FindObjectsOfType<TerrainTile>().SelectMany(t => FreeCells(t.transform.position, 3)).Distinct().ToList();

        /// <summary>Free cells on the start tile at ground level (+0), excluding the castle top.</summary>
        public static List<Vector3> FreeStartTileCells() =>
            FreeCells(Core.GameHelpers.CastlePosition, 3).Where(p => p.y < 0.5f).ToList();

        public static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
