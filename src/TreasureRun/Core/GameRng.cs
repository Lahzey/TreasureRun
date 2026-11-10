namespace TreasureRun.Core
{
    /// <summary><see cref="IRng"/> backed by UnityEngine.Random (in game).</summary>
    public sealed class GameRng : IRng
    {
        public static readonly GameRng Instance = new GameRng();
        public float Value() { float v = UnityEngine.Random.value; return v >= 1f ? 0.99999994f : v; } // Unity: [0, 1] inclusive
        public int Range(int min, int max) => UnityEngine.Random.Range(min, max);
    }
}
