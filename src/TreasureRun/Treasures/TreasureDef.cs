using System;
using TreasureRun.Core;

namespace TreasureRun.Treasures
{
    /// <summary>Static description of a treasure. Behaviour lives in the <see cref="TreasureEffect"/> it creates.</summary>
    public sealed class TreasureDef
    {
        public string Id;
        public string Name;
        public string Description;
        public Rarity Rarity;
        public string[] Tags = Array.Empty<string>();

        /// <summary>Owning any treasure of a group removes the other members from the pool (e.g. "plane-ascension").</summary>
        public string ExclusionGroup;

        /// <summary>Extra availability condition (e.g. "Radar unlocked"). Null = always available.</summary>
        public Func<RunState, bool> IsAvailable;

        /// <summary>Relative weight within its rarity.</summary>
        public float Weight = 1f;

        /// <summary>Unique treasures can be owned once; non-unique ones stack.</summary>
        public bool Unique = true;

        public Func<TreasureEffect> CreateEffect;
    }

    /// <summary>Runtime behaviour of an owned treasure. One instance per owned copy.</summary>
    public abstract class TreasureEffect
    {
        public TreasureDef Def { get; internal set; }

        /// <summary>
        /// Called when the treasure is gained. <paramref name="restoring"/> is true when rebuilding from a save
        /// (future feature): one-time rewards (gold, HP) must then be skipped, persistent hooks re-applied.
        /// </summary>
        public abstract void OnAcquire(RunState run, bool restoring);
    }

    /// <summary>Effect built from a lambda, for simple treasures.</summary>
    public sealed class LambdaEffect : TreasureEffect
    {
        private readonly Action<RunState, bool> onAcquire;
        public LambdaEffect(Action<RunState, bool> onAcquire) { this.onAcquire = onAcquire; }
        public override void OnAcquire(RunState run, bool restoring) => onAcquire(run, restoring);
    }
}
