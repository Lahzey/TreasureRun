using UnityEngine;

namespace TreasureRun.Core
{
    /// <summary>Small wrappers around vanilla game systems.</summary>
    public static class GameHelpers
    {
        /// <summary>Position of the castle (the start tile).</summary>
        public static Vector3 CastlePosition =>
            TileManager.instance != null && TileManager.instance.startTile != null
                ? TileManager.instance.startTile.transform.position
                : Vector3.zero;

        /// <summary>Vanilla floating text. Colors: "Grey", "Green", "Red", "Yellow", "Blue".</summary>
        public static void FloatingText(Vector3 position, string text, string color = "Green", float scale = 1f, float holdTime = 1f)
        {
            if (ObjectPool.instance == null) return;
            var number = ObjectPool.instance.SpawnObject(ObjectPool.ObjectType.DamageNumber, position, Quaternion.identity)
                .GetComponent<DamageNumber>();
            number.SetText(text, color, scale);
            number.SetHoldTime(holdTime);
        }

        public static int Gold => ResourceManager.instance != null ? ResourceManager.instance.gold : 0;

        public static int Level => SpawnManager.instance != null ? SpawnManager.instance.level : 1;
    }
}
