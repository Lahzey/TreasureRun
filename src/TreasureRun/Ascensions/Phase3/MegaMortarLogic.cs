using System.Collections.Generic;
using UnityEngine;

// Pure mechanics of Mega Mortar (docs/ascension-plan.md §3, phase 3). No Unity engine calls: only Mathf, so the offline
// tests (tests/TreasureRun.Tests) run them directly.
namespace TreasureRun.Ascensions.Phase3
{
    /// <summary>A square on the build grid: its lowest cell (x, z) and its side length (1 = a loose Mortar Module).</summary>
    public struct MortarSquare
    {
        public int X, Z, Size;
        public MortarSquare(int x, int z, int size) { X = x; Z = z; Size = size; }
        public bool Contains(int x, int z) => x >= X && x < X + Size && z >= Z && z < Z + Size;
        public bool Inside(MortarSquare outer) => X >= outer.X && Z >= outer.Z && X + Size <= outer.X + outer.Size && Z + Size <= outer.Z + outer.Size;
        public bool Overlaps(MortarSquare o) => X < o.X + o.Size && o.X < X + Size && Z < o.Z + o.Size && o.Z < Z + Size;
        /// <summary>Number of modules in it (the weight when fusing).</summary>
        public int Modules => Size * Size;
        public override string ToString() => $"{Size}x{Size} at {X},{Z}";
    }

    /// <summary>A mortar's levels and XP, as they go into a fusion.</summary>
    public struct MortarProgress
    {
        public int Weight, Level, DamageUp, HealthUp, ArmorUp, ShieldUp;
        public float HealthXp, ArmorXp, ShieldXp;
    }

    /// <summary>
    /// Mega Mortar (Mortar, Legendary): Mortar Modules built in a full square (2×2, 3×3, …) fuse for good into one Mega
    /// Mortar, a single tower. It hits like all its modules together (base damage × modules), with the modules'
    /// multipliers and level averaged (weighted by module count), a blast radius × the side length and +2 range per
    /// side length above 1. A Mega Mortar only grows: a bigger square around it (and nothing sticking out) fuses again.
    /// </summary>
    public static class MegaMortarLogic
    {
        /// <summary>Largest side length a Mega Mortar can reach.</summary>
        public const int MaxSize = 8;
        public const float RangePerSide = 2f;

        public static int DamageFactor(int size) => Mathf.Max(1, size) * Mathf.Max(1, size);
        public static float BlastFactor(int size) => Mathf.Max(1, size);
        public static float RangeBonus(int size) => (Mathf.Max(1, size) - 1) * RangePerSide;

        /// <summary>Barrel radius of a Mega Mortar model of side n, in voxels (VoxTool MortarBody: 4.6 n).</summary>
        public static float BarrelRadiusVoxels(int size) => 4.6f * size;

        /// <summary>
        /// Tiles (tx, ty within the square) that carry a vent grille on the Mega Mortar model: every tile whose centre is
        /// at least 4 voxels outside the barrel (same rule as VoxTool MortarBody). A 3x3 has its 4 corners; bigger
        /// squares have more. Steam shoots up from each vent from 3x3 on.
        /// </summary>
        public static List<(int tx, int ty)> VentTiles(int size)
        {
            var vents = new List<(int, int)>();
            if (size < 2) return vents;
            float mid = (16 * size - 1) / 2f, r = BarrelRadiusVoxels(size);
            for (int tx = 0; tx < size; tx++)
                for (int ty = 0; ty < size; ty++)
                {
                    float gx = tx * 16 + 7.5f - mid, gy = ty * 16 + 7.5f - mid;
                    if (Mathf.Sqrt(gx * gx + gy * gy) >= r + 4f) vents.Add((tx, ty));
                }
            return vents;
        }

        /// <summary>
        /// The next fusion among the units on the map (loose modules = size 1, Mega Mortars = their square), or null: the
        /// largest square (ties: lowest z, then lowest x) that is completely covered by at least two units, none of which
        /// sticks out of it. <paramref name="contained"/> gets the indices of the units inside.
        /// </summary>
        public static MortarSquare? NextFusion(IReadOnlyList<MortarSquare> units, List<int> contained)
        {
            contained.Clear();
            if (units.Count < 2) return null;
            var owner = new Dictionary<(int, int), int>();
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            for (int i = 0; i < units.Count; i++)
            {
                MortarSquare u = units[i];
                for (int dx = 0; dx < u.Size; dx++)
                    for (int dz = 0; dz < u.Size; dz++)
                        owner[(u.X + dx, u.Z + dz)] = i;
                minX = Mathf.Min(minX, u.X); minZ = Mathf.Min(minZ, u.Z);
                maxX = Mathf.Max(maxX, u.X + u.Size - 1); maxZ = Mathf.Max(maxZ, u.Z + u.Size - 1);
            }
            var inside = new HashSet<int>();
            for (int size = Mathf.Min(MaxSize, Mathf.Min(maxX - minX, maxZ - minZ) + 1); size >= 2; size--)
                for (int z = minZ; z + size - 1 <= maxZ; z++)
                    for (int x = minX; x + size - 1 <= maxX; x++)
                    {
                        var s = new MortarSquare(x, z, size);
                        inside.Clear();
                        bool ok = true;
                        for (int dx = 0; dx < size && ok; dx++)
                            for (int dz = 0; dz < size && ok; dz++)
                            {
                                if (!owner.TryGetValue((x + dx, z + dz), out int i) || !units[i].Inside(s)) ok = false;
                                else inside.Add(i);
                            }
                        if (!ok || inside.Count < 2) continue;
                        contained.AddRange(inside);
                        contained.Sort();
                        return s;
                    }
            return null;
        }

        /// <summary>Applies every fusion in turn and returns the units left (for tests and planning).</summary>
        public static List<MortarSquare> Resolve(IEnumerable<MortarSquare> units)
        {
            var list = new List<MortarSquare>(units);
            var contained = new List<int>();
            for (MortarSquare? s = NextFusion(list, contained); s.HasValue; s = NextFusion(list, contained))
            {
                for (int i = contained.Count - 1; i >= 0; i--) list.RemoveAt(contained[i]);
                list.Add(s.Value);
            }
            return list;
        }

        /// <summary>Levels, multiplier upgrades and XP of a fusion: weighted averages of its parts (weight = module count).</summary>
        public static MortarProgress Average(IReadOnlyList<MortarProgress> parts)
        {
            float w = 0f, level = 0f, dmg = 0f, h = 0f, a = 0f, s = 0f, hx = 0f, ax = 0f, sx = 0f;
            foreach (MortarProgress p in parts)
            {
                float k = Mathf.Max(1, p.Weight);
                w += k; level += k * p.Level; dmg += k * p.DamageUp; h += k * p.HealthUp; a += k * p.ArmorUp; s += k * p.ShieldUp;
                hx += k * p.HealthXp; ax += k * p.ArmorXp; sx += k * p.ShieldXp;
            }
            if (w <= 0f) return new MortarProgress { Level = 1 };
            int weight = 0;
            foreach (MortarProgress p in parts) weight += Mathf.Max(1, p.Weight);
            return new MortarProgress
            {
                Weight = weight,
                Level = Mathf.Max(1, Mathf.RoundToInt(level / w)),
                DamageUp = Mathf.RoundToInt(dmg / w),
                HealthUp = Mathf.RoundToInt(h / w),
                ArmorUp = Mathf.RoundToInt(a / w),
                ShieldUp = Mathf.RoundToInt(s / w),
                HealthXp = hx / w, ArmorXp = ax / w, ShieldXp = sx / w,
            };
        }

        /// <summary>
        /// Gold back for demolishing a Mega Mortar of <paramref name="modules"/> modules: like selling that many mortars
        /// one by one, with <paramref name="mortarsOwned"/> mortars (modules) on the map before selling. A mortar sells for
        /// what the last one cost: base + (count − 1) × increase.
        /// </summary>
        public static int SellValue(int basePrice, int priceIncrease, int mortarsOwned, int modules)
        {
            int total = 0;
            for (int k = 1; k <= modules; k++) total += basePrice + Mathf.Max(0, mortarsOwned - k) * priceIncrease;
            return total;
        }
    }
}
