using System;

namespace TreasureRun.Core
{
    /// <summary>
    /// Fixed damage (Points of Interest: the Lightning Trap, the Wizard's Tower): a hit split over the monster's layers like
    /// vanilla, but exact: no crits, no card bonuses (damage-over-time layer bonuses, marks) and no treasure modifiers.
    /// <c>Combat.FixedHit</c> applies it. Pure, offline-tested.
    /// </summary>
    public static class FixedDamageLogic
    {
        /// <summary>Damage of a hit on one layer: base minus fortify (at least 1) times the layer's multiplier.</summary>
        public static int LayerDamage(int baseDamage, int fortify, int mult) => Math.Max(baseDamage - fortify, 1) * Math.Max(0, mult);

        /// <summary>
        /// Splits a hit over the layers like vanilla: shield, then armor, then health; the excess of a broken layer carries
        /// on as base damage for the next one (excess / that layer's multiplier, fortify applied again). A layer with a
        /// multiplier of 0 stops the hit. Returns the damage per layer.
        /// </summary>
        public static (int shield, int armor, int health) Split(int baseDamage, int fortify, int h, int a, int s, int health, int armor, int shield)
        {
            int ds = 0, da = 0;
            int b = baseDamage;
            if (shield > 0)
            {
                if (s <= 0) return (0, 0, 0);
                int d = LayerDamage(b, fortify, s);
                if (d <= shield) return (d, 0, 0);
                ds = shield;
                b = (d - shield) / s;
                if (b <= 0) return (ds, 0, 0);
            }
            if (armor > 0)
            {
                if (a <= 0) return (ds, 0, 0);
                int d = LayerDamage(b, fortify, a);
                if (d <= armor) return (ds, d, 0);
                da = armor;
                b = (d - armor) / a;
                if (b <= 0) return (ds, da, 0);
            }
            return (ds, da, LayerDamage(b, fortify, h));
        }
    }
}
