namespace TreasureRun.Core
{
    /// <summary>
    /// Random source for pure ascension logic (<c>*Logic.cs</c>), so the same code runs in the game (backed by Unity's
    /// RNG) and in the offline tests (seeded <see cref="SystemRng"/>). Pure file: no Unity engine calls.
    /// </summary>
    public interface IRng
    {
        /// <summary>Uniform float in [0, 1).</summary>
        float Value();

        /// <summary>Uniform int in [min, max).</summary>
        int Range(int min, int max);
    }

    /// <summary>Deterministic RNG on System.Random (tests, simulations).</summary>
    public sealed class SystemRng : IRng
    {
        private readonly System.Random random;
        public SystemRng(int seed) { random = new System.Random(seed); }
        public float Value() => (float)random.NextDouble();
        public int Range(int min, int max) => random.Next(min, max);
    }

    public static class RngExtensions
    {
        public static float Range(this IRng rng, float min, float max) => min + (max - min) * rng.Value();
        public static bool Chance(this IRng rng, float p) => rng.Value() < p;
    }
}
