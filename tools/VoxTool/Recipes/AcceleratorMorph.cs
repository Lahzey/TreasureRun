using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TreasureRun.Vox;
using static VoxTool.Recipes.AscensionRecipes;

namespace VoxTool.Recipes
{
    /// <summary>
    /// The Particle Accelerator's capacitor and its transformation into the core (played backwards for core →
    /// capacitor), computed here rather than at runtime.
    /// <para>
    /// The capacitor is made of the core's own parts: the barrel stood on end on its turntable as a big battery cell
    /// (steel sleeve, no charge window, a gold + terminal out of the muzzle), on the core's base sunk 7 voxels into the
    /// ground so that the ring accelerator lies on the floor (the Ley sockets are underground). Only the corner feet
    /// stay put. The transformation moves those parts as rigid pieces (slides, a 90° tilt, rods extending) plus a few
    /// voxel-by-voxel changes (the sleeve flipping, the cables feeding out, the charge window opening):
    /// </para>
    /// <list type="number">
    /// <item>rise: the base rises 7 out of the ground with everything on it, revealing the Ley sockets;</item>
    /// <item>unlock: the + terminal sinks into the cell;</item>
    /// <item>sleeve: the steel sleeve flips to the barrel's dark plating, top to bottom;</item>
    /// <item>outriggers: two beams slide out of the turntable to the sides;</item>
    /// <item>rams: hydraulic rams extend from their tips up to the cell's middle ring and clamp on;</item>
    /// <item>lift: the rams lift the cell 2 so it can turn;</item>
    /// <item>tilt: the cell tips forward 90° on the clamps (muzzle from up to forward);</item>
    /// <item>column: the support column rises out of the turntable;</item>
    /// <item>lower: the rams set the barrel down on the column;</item>
    /// <item>release: the clamps open, the rams retract and the outriggers slide back in;</item>
    /// <item>connect: the four cables feed out of the turntable up to the barrel; the two gold rails rise out of the
    /// barrel's top together (touching, on its centre line), slide apart to the window's edges, opening the charge window
    /// between them, and settle.</item>
    /// </list>
    /// <para>
    /// Grid: 16 x 18 x 42 voxels of 1/16 tile in the tower's frame (turret facing forward): x as the base, y = base y + 1
    /// (the barrel overhangs the tile by one at each end), z = height + 8 (room for the sunk base). Composing the pieces
    /// at the first and the last pose gives exactly the capacitor and the core (checked here). Output:
    /// <c>accelerator_morph.txt</c> next to the models (read by the game) and the capacitor model.
    /// </para>
    /// </summary>
    public static class AcceleratorMorph
    {
        /// <summary>
        /// Slow motion: every step's length (seconds at TimeScale 1, see <see cref="Steps"/>) is multiplied by this.
        /// 2 = the slow-motion review. For a transformation of about T seconds set it to T / (the sum the build prints)
        /// and rebuild (<c>dotnet run -- morph ../../models</c>); the game reads the lengths from the morph file.
        /// </summary>
        public const float TimeScale = 0.28f;

        public const int SX = 16, SY = 18, SZ = 42, ZOff = 8;
        /// <summary>How far the capacitor's base is sunk, the lift that lets the cell turn freely, the rams' full length.</summary>
        private const int Sink = 7, Lift = 2, RodHeight = 13;
        /// <summary>The rams' length while they hold the cell at its seat (the clamps at the cell's middle).</summary>
        private const float RodHold = 11f / RodHeight;
        /// <summary>The rails' grid height (turret z 15) and how far each slides out from the barrel's centre line.</summary>
        private const int RailZ = 15 + 9 + ZOff, RailShift = 2;

        private sealed class Piece
        {
            public string Name;
            /// <summary>Voxels at the piece's home position (the core's), coloured as at the start (the capacitor).</summary>
            public readonly Dictionary<(int, int, int), uint> Vox = new Dictionary<(int, int, int), uint>();
            /// <summary>Rotation pivot (about the x axis) / scale anchor, in grid units (voxel edges).</summary>
            public float Ox, Oy, Oz;
            public Pose Start;
        }

        private struct Pose
        {
            public float X, Y, Z, Rot, Len;
            public bool Vis;
            public static Pose At(float x, float y, float z, float rot = 0f, float len = 1f, bool vis = true) =>
                new Pose { X = x, Y = y, Z = z, Rot = rot, Len = len, Vis = vis };
            public Pose With(float? z = null, float? rot = null, float? len = null, bool? vis = null, float? x = null) =>
                new Pose { X = x ?? X, Y = Y, Z = z ?? Z, Rot = rot ?? Rot, Len = len ?? Len, Vis = vis ?? Vis };
        }

        private sealed class Tween
        {
            public Piece P;
            public float A, B;
            public string Ease;
            public Pose To;
        }

        private struct Change
        {
            public int X, Y, Z;
            public uint Before, After;
            public float T0, T1;
            /// <summary>0 pops; 1..6 grows towards +x, -x, +y, -y, +z, -z (from the opposite face). Unused at the moment.</summary>
#pragma warning disable CS0649
            public int Dir;
#pragma warning restore CS0649
        }

        private sealed class Step
        {
            public string Name;
            public float Seconds, Load;
            public readonly List<Tween> Tweens = new List<Tween>();
            public readonly List<(Piece p, List<Change> e)> Waves = new List<(Piece, List<Change>)>();
            public readonly List<(char dir, float at, string name, float vol)> Sounds = new List<(char, float, string, float)>();
            public readonly List<(string name, float a, float b, float vol)> Loops = new List<(string, float, float, float)>();
        }

        private static (int, int, int) FromBase(int x, int y, int z) => (x, y + 1, z + ZOff);
        private static (int, int, int) FromTurret(int x, int y, int z) => (x + 1, y, z + 9 + ZOff);

        private static Dictionary<(int, int, int), uint> Voxels(VoxCanvas c, Func<int, int, int, (int, int, int)> map)
        {
            var d = new Dictionary<(int, int, int), uint>();
            for (int x = 0; x < c.SizeX; x++)
                for (int y = 0; y < c.SizeY; y++)
                    for (int z = 0; z < c.SizeZ; z++)
                        if (c.Get(x, y, z) != 0) d[map(x, y, z)] = c.Get(x, y, z);
            return d;
        }

        /// <summary>The core (base + barrel) on the grid.</summary>
        private static Dictionary<(int, int, int), uint> CoreModel()
        {
            var d = Voxels(AcceleratorBase(), FromBase);
            foreach (var kv in Voxels(AcceleratorBarrel(), FromTurret)) d[kv.Key] = kv.Value;
            return d;
        }

        private static List<Piece> pieces;
        private static List<Step> steps;
        private static Dictionary<(int, int, int), uint> coreCell;

        private static void Build()
        {
            if (pieces != null) return;
            pieces = new List<Piece>();
            Piece New(string name, float ox = 0f, float oy = 0f, float oz = 0f)
            {
                var p = new Piece { Name = name, Ox = ox, Oy = oy, Oz = oz, Start = Pose.At(0, 0, 0) };
                pieces.Add(p);
                return p;
            }

            // Base: the corner feet stay, the rest is one piece.
            Piece feet = New("feet"), baseUnit = New("base");
            foreach (var kv in Voxels(AcceleratorBase(), FromBase))
            {
                var (x, y, _) = kv.Key;
                int by = y - 1;
                bool corner = (x <= 2 || x >= 13) && (by <= 2 || by >= 13);
                (corner ? feet : baseUnit).Vox[kv.Key] = kv.Value;
            }

            // Barrel layers (turret canvas).
            var full = Voxels(AcceleratorBarrel(), FromTurret);
            var noCables = Voxels(AcceleratorBarrel(BarrelLayers.Table | BarrelLayers.Column | BarrelLayers.Body | BarrelLayers.Window), FromTurret);
            var column = Voxels(AcceleratorBarrel(BarrelLayers.Column), FromTurret);
            var bodyPlain = Voxels(AcceleratorBarrel(BarrelLayers.Body), FromTurret);
            var bodyWindow = Voxels(AcceleratorBarrel(BarrelLayers.Body | BarrelLayers.Window), FromTurret);
            int tableTop = 1 + 9 + ZOff;

            Piece table = New("table"), col = New("column"), cables = New("cables");
            // The cell turns about its middle: turret y 9, z 11 (voxel edges).
            Piece cell = New("cell", 8f, 9f, 11 + 9 + ZOff);
            // The charge window's gold rails (turret x 4 and 9, z 15): their own pieces, they slide. Anchor: their bottom.
            Piece railL = New("railL", 5f, 9f, RailZ), railR = New("railR", 10f, 9f, RailZ);
            var cablePlace = new Dictionary<(int, int, int), uint>();
            coreCell = new Dictionary<(int, int, int), uint>();
            foreach (var kv in full)
            {
                var p = kv.Key;
                if (p.Item3 <= tableTop) { table.Vox[p] = kv.Value; continue; }
                if (p.Item3 == RailZ && kv.Value == PaGold && (p.Item1 == 5 || p.Item1 == 10)) { (p.Item1 == 5 ? railL : railR).Vox[p] = kv.Value; continue; }
                if (column.ContainsKey(p) && !bodyWindow.ContainsKey(p)) { col.Vox[p] = kv.Value; continue; }
                bool cable = !noCables.TryGetValue(p, out uint nc) || nc != kv.Value;
                if (cable && !bodyWindow.ContainsKey(p)) { cablePlace[p] = kv.Value; continue; }
                coreCell[p] = kv.Value;
            }
            // The capacitor's cell: no window or rails, the body's plating in a steel sleeve.
            foreach (var kv in bodyPlain)
            {
                int ty = kv.Key.Item2;
                cell.Vox[kv.Key] = kv.Value == PaSlab && ty >= 4 && ty <= 14 ? PaDeckLight : kv.Value;
            }

            // The + terminal: a gold post (radius 1.6) out of the muzzle, white-hot tip. Home: standing on the upright cell
            // at the core's height (turret z 20..21 over the muzzle's middle).
            Piece post = New("post");
            for (int tx = 4; tx <= 9; tx++)
                for (int tz = 8; tz <= 13; tz++)
                {
                    double d2 = (tx - 6.5) * (tx - 6.5) + (tz - 10.5) * (tz - 10.5);
                    if (d2 > 2.56) continue;
                    foreach (int k in new[] { 0, 1 })
                    {
                        // Horizontal (y 18 + k) to upright: (x, y, z) -> (x, 19 - z, y + 2).
                        var p = FromTurret(tx, 19 - tz, 18 + k + 2);
                        post.Vox[p] = k == 1 ? (d2 < 0.6 ? PaWhite : PaGold) : PaCopper;
                    }
                }

            // Temporary hydraulics: an outrigger beam each side in the turntable's lower layer (home: slid out, turret x
            // -1..2 and 11..14), a ram on each tip (2 x 2, turret z 1..13: cylinder, rod, gold clamp head).
            Piece outL = New("outL"), outR = New("outR");
            foreach (var (piece, x0) in new[] { (outL, -1), (outR, 11) })
                for (int i = 0; i < 4; i++)
                    for (int y = 8; y <= 9; y++)
                    {
                        bool tip = piece == outL ? i <= 1 : i >= 2;
                        piece.Vox[FromTurret(x0 + i, y, 0)] = tip ? PaEdge : PaPipe;
                    }
            Piece rodL = New("rodL", 0f, 9f, 1 + 9 + ZOff), rodR = New("rodR", 0f, 9f, 1 + 9 + ZOff);
            foreach (var (piece, x0) in new[] { (rodL, -1), (rodR, 13) })
                for (int k = 0; k < RodHeight; k++)
                    for (int x = x0; x <= x0 + 1; x++)
                        for (int y = 8; y <= 9; y++)
                            piece.Vox[FromTurret(x, y, 1 + k)] = k < 3 ? PaEdge : k < RodHeight - 2 ? PaPipe : PaGold;

            // Start poses (the capacitor).
            baseUnit.Start = table.Start = post.Start = Pose.At(0, 0, -Sink);
            cell.Start = Pose.At(0, 0, -Sink, 90f);
            col.Start = Pose.At(0, 0, -5, vis: false);
            outL.Start = Pose.At(3, 0, 0, vis: false);
            outR.Start = Pose.At(-3, 0, 0, vis: false);
            rodL.Start = rodR.Start = Pose.At(0, 0, 0, len: 0f, vis: false);
            // The rails start inside the barrel's top on its centre line (turret x 6 and 7, touching), one voxel up.
            railL.Start = Pose.At(RailShift, 0, 1, len: 0f, vis: false);
            railR.Start = Pose.At(-RailShift, 0, 1, len: 0f, vis: false);

            steps = Steps(baseUnit, table, cell, post, col, outL, outR, rodL, rodR, railL, railR, cables, cablePlace, bodyPlain);
        }

        /// <summary>The steps, capacitor → core. Lengths are seconds at TimeScale 1.</summary>
        private static List<Step> Steps(Piece baseUnit, Piece table, Piece cell, Piece post, Piece col, Piece outL, Piece outR,
                                        Piece rodL, Piece rodR, Piece railL, Piece railR, Piece cables, Dictionary<(int, int, int), uint> cablePlace,
                                        Dictionary<(int, int, int), uint> bodyPlain)
        {
            var list = new List<Step>();
            var cur = pieces.ToDictionary(p => p, p => p.Start);
            Step S(string name, float seconds, float load)
            {
                var s = new Step { Name = name, Seconds = seconds, Load = load };
                list.Add(s);
                return s;
            }
            void Tw(Step s, Piece p, float a, float b, string ease, Func<Pose, Pose> to)
            {
                Pose end = to(cur[p]);
                s.Tweens.Add(new Tween { P = p, A = a, B = b, Ease = ease, To = end });
                cur[p] = end;
            }
            void Snd(Step s, char dir, float at, string name, float vol) => s.Sounds.Add((dir, at, name, vol));

            // 1. The base rises out of the ground with everything on it: the Ley sockets come up.
            Step s1 = S("rise", 0.6f, 1f);
            foreach (Piece p in new[] { baseUnit, table, cell, post }) Tw(s1, p, 0.08f, 1f, "settle", q => q.With(z: 0f));
            Snd(s1, 'f', 1f, "accel_lock_heavy", 1f); Snd(s1, 'f', 0.95f, "accel_hiss", 0.8f);
            Snd(s1, 'r', 0f, "accel_hiss", 0.6f); Snd(s1, 'r', 1f, "accel_lock_heavy", 1f);

            // 2. The + terminal sinks into the cell (3 deep: fully inside the muzzle) and is gone.
            Step s2 = S("unlock", 0.3f, 0.3f);
            Tw(s2, post, 0f, 1f, "smooth", q => q.With(z: -3f, vis: false));
            Snd(s2, 'f', 0f, "accel_unlock", 0.9f); Snd(s2, 'f', 0.9f, "accel_hiss", 0.4f);
            Snd(s2, 'r', 0f, "accel_hiss", 0.4f); Snd(s2, 'r', 1f, "accel_lock", 0.9f);

            // 3. The steel sleeve flips to the dark plating, top (muzzle end) to bottom.
            Step s3 = S("sleeve", 0.45f, 0f);
            var sleeve = new List<Change>();
            foreach (var kv in cell.Vox.Where(kv => kv.Value == PaDeckLight))
            {
                var (x, y, z) = kv.Key;
                float t0 = (14 - y) / 10f * 0.8f;
                sleeve.Add(new Change { X = x, Y = y, Z = z, Before = PaDeckLight, After = PaSlab, T0 = t0, T1 = t0 + 0.2f });
            }
            s3.Waves.Add((cell, sleeve));
            foreach (float at in new[] { 0.05f, 0.3f, 0.55f, 0.8f }) { Snd(s3, 'f', at, "accel_click", 0.55f); Snd(s3, 'r', at, "accel_click", 0.55f); }

            // 4. Outrigger beams slide out of the turntable.
            Step s4 = S("outriggers", 0.3f, 0.5f);
            Tw(s4, outL, 0f, 1f, "settle", q => q.With(x: 0f, vis: true));
            Tw(s4, outR, 0f, 1f, "settle", q => q.With(x: 0f, vis: true));
            s4.Loops.Add(("accel_gears", 0f, 1f, 0.6f));
            Snd(s4, 'f', 1f, "accel_lock", 0.7f); Snd(s4, 'r', 1f, "accel_lock", 0.7f);

            // 5. The rams extend to the cell's middle ring; the clamp heads lock on (light up).
            Step s5 = S("rams", 0.55f, 1f);
            Tw(s5, rodL, 0f, 0.85f, "smooth", q => q.With(len: RodHold, vis: true));
            Tw(s5, rodR, 0f, 0.85f, "smooth", q => q.With(len: RodHold, vis: true));
            foreach (Piece rod in new[] { rodL, rodR })
                s5.Waves.Add((rod, Heads(rod, PaGold, PaGlow, 0.86f, 0.9f)));
            Snd(s5, 'f', 0.88f, "accel_lock", 1f);
            Snd(s5, 'r', 0f, "accel_unlock", 0.9f); Snd(s5, 'r', 1f, "accel_click", 0.6f);

            // 6. Lift: the cell comes off the turntable.
            Step s6 = S("lift", 0.35f, 1f);
            Tw(s6, cell, 0f, 1f, "smooth", q => q.With(z: Lift));
            Tw(s6, rodL, 0f, 1f, "smooth", q => q.With(len: 1f));
            Tw(s6, rodR, 0f, 1f, "smooth", q => q.With(len: 1f));
            Snd(s6, 'f', 0.1f, "accel_hiss", 0.5f);
            Snd(s6, 'r', 1f, "accel_lock_heavy", 0.9f);

            // 7. Tilt: the cell tips forward on the clamps, muzzle from up to forward.
            Step s7 = S("tilt", 0.8f, 0.6f);
            Tw(s7, cell, 0f, 1f, "settle", q => q.With(rot: 0f));
            s7.Loops.Add(("accel_gears", 0f, 0.95f, 1f));
            Snd(s7, 'f', 1f, "accel_lock_heavy", 1f); Snd(s7, 'r', 1f, "accel_lock_heavy", 1f);

            // 8. The support column rises out of the turntable.
            Step s8 = S("column", 0.35f, 1f);
            Tw(s8, col, 0f, 1f, "smooth", q => q.With(z: 0f, vis: true));
            Snd(s8, 'f', 1f, "accel_lock", 0.8f);
            Snd(s8, 'r', 0f, "accel_unlock", 0.7f); Snd(s8, 'r', 1f, "accel_hiss", 0.4f);

            // 9. Lower: the barrel sits down on the column.
            Step s9 = S("lower", 0.35f, 1f);
            Tw(s9, cell, 0f, 1f, "settle", q => q.With(z: 0f));
            Tw(s9, rodL, 0f, 1f, "settle", q => q.With(len: RodHold));
            Tw(s9, rodR, 0f, 1f, "settle", q => q.With(len: RodHold));
            Snd(s9, 'f', 1f, "accel_lock_heavy", 1f); Snd(s9, 'f', 1f, "accel_hiss", 0.7f);
            Snd(s9, 'r', 0f, "accel_unlock", 0.8f); Snd(s9, 'r', 0.05f, "accel_hiss", 0.6f);

            // 10. Release: the clamps open, the rams retract, the outriggers slide back in.
            Step s10 = S("release", 0.55f, 0.8f);
            foreach (Piece rod in new[] { rodL, rodR })
            {
                s10.Waves.Add((rod, Heads(rod, PaGlow, PaGold, 0f, 0.05f)));
                Tw(s10, rod, 0.08f, 0.6f, "smooth", q => q.With(len: 0f, vis: false));
            }
            Tw(s10, outL, 0.62f, 1f, "smooth", q => q.With(x: 3f, vis: false));
            Tw(s10, outR, 0.62f, 1f, "smooth", q => q.With(x: -3f, vis: false));
            s10.Loops.Add(("accel_gears", 0.62f, 1f, 0.6f));
            Snd(s10, 'f', 0f, "accel_unlock", 0.9f); Snd(s10, 'f', 1f, "accel_click", 0.6f);
            Snd(s10, 'r', 1f, "accel_lock", 1f);

            // 11. Connect: the cables feed out up to the barrel. Meanwhile the two gold rails rise out of the barrel's top on
            // its centre line, touching; they slide apart to the window's edges (the window opens behind them, column by
            // column) and settle down onto them.
            Step s11 = S("connect", 0.7f, 0.3f);
            var runs = CableRuns();
            var feed = new List<Change>();
            foreach (var kv in cablePlace)
            {
                var (x, y, z) = kv.Key;
                float r = runs.TryGetValue((x - 1, y, z - 9 - ZOff), out float rr) ? rr : 1f;
                float t0 = 0.05f + 0.72f * r;
                feed.Add(new Change { X = x, Y = y, Z = z, Before = 0, After = kv.Value, T0 = t0, T1 = t0 + 0.06f });
            }
            s11.Waves.Add((cables, feed));
            const float rise0 = 0.12f, rise1 = 0.3f, slide0 = 0.32f, slide1 = 0.72f, seat0 = 0.74f, seat1 = 0.88f;
            foreach (Piece rail in new[] { railL, railR })
            {
                Tw(s11, rail, rise0, rise1, "smooth", q => q.With(len: 1f, vis: true));
                Tw(s11, rail, slide0, slide1, "smooth", q => q.With(x: 0f));
                Tw(s11, rail, seat0, seat1, "settle", q => q.With(z: 0f));
            }
            var reveal = new List<Change>();
            foreach (var p in bodyPlain.Keys.Union(coreCell.Keys))
            {
                uint before = bodyPlain.TryGetValue(p, out uint b) ? b : 0, after = coreCell.TryGetValue(p, out uint a) ? a : 0;
                if (before == after) continue;
                var (x, y, z) = p;
                int tx = x - 1, tz = z - 9 - ZOff;
                float t0 = 0.78f, t1 = 0.84f;                                         // the cable plugs
                if (tx >= 4 && tx <= 9 && y >= 3 && y <= 14 && tz >= 12)
                {
                    // Window columns open as the rails leave them (x 6, 7 at once, x 5, 8 halfway); under the rails' seats
                    // (x 4, 9: copper ring voxels) just before they land.
                    float at = tx == 6 || tx == 7 ? slide0 + 0.02f : tx == 5 || tx == 8 ? (slide0 + slide1) / 2f : seat0 - 0.02f;
                    t0 = at;
                    t1 = at + 0.04f;
                }
                reveal.Add(new Change { X = x, Y = y, Z = z, Before = before, After = after, T0 = t0, T1 = t1 });
            }
            s11.Waves.Add((cell, reveal));
            s11.Loops.Add(("accel_slither", 0.05f, 0.9f, 0.8f));
            Snd(s11, 'f', 0.12f, "accel_click", 0.6f); Snd(s11, 'f', 0.82f, "accel_click", 0.8f);
            Snd(s11, 'f', 0.86f, "accel_lock", 0.8f); Snd(s11, 'f', 1f, "accel_latch", 1f);
            Snd(s11, 'r', 0f, "accel_unlock", 0.8f); Snd(s11, 'r', 0.7f, "accel_click", 0.6f);
            return list;
        }

        /// <summary>Recolours a ram's clamp head (its top two layers).</summary>
        private static List<Change> Heads(Piece rod, uint from, uint to, float t0, float t1)
        {
            int top = rod.Vox.Keys.Max(k => k.Item3);
            return rod.Vox.Keys.Where(k => k.Item3 >= top - 1)
                .Select(k => new Change { X = k.Item1, Y = k.Item2, Z = k.Item3, Before = from, After = to, T0 = t0, T1 = t1 }).ToList();
        }

        // ---- Composing ----

        /// <summary>A piece's voxels at a pose (exact for 0° and 90°; nearest voxel otherwise, for previews).</summary>
        private static IEnumerable<((int, int, int) p, uint c)> Placed(Piece pc, Dictionary<(int, int, int), uint> vox, Pose pose)
        {
            if (!pose.Vis || pose.Len <= 0f) yield break;
            int tx = (int)Math.Round(pose.X), ty = (int)Math.Round(pose.Y), tz = (int)Math.Round(pose.Z);
            if (pose.Len < 1f)
            {
                // A ram drawn shorter: its column sampled down to the length (the head stays on top).
                int z0 = (int)Math.Round(pc.Oz), height = vox.Keys.Max(k => k.Item3) - z0 + 1, n = (int)Math.Round(pose.Len * height);
                foreach (var kv in vox)
                {
                    var (x, y, z) = kv.Key;
                    if (z != z0) continue;
                    for (int k = 0; k < n; k++)
                    {
                        int src = Math.Min(height - 1, (int)((k + 0.5f) / pose.Len));
                        if (vox.TryGetValue((x, y, z0 + src), out uint c)) yield return ((x + tx, y + ty, z0 + k + tz), c);
                    }
                }
                yield break;
            }
            float rot = ((pose.Rot % 360f) + 360f) % 360f;
            if (Math.Abs(rot) < 0.01f)
            {
                foreach (var kv in vox) yield return ((kv.Key.Item1 + tx, kv.Key.Item2 + ty, kv.Key.Item3 + tz), kv.Value);
                yield break;
            }
            if (Math.Abs(rot - 90f) < 0.01f)
            {
                foreach (var kv in vox)
                {
                    var (x, y, z) = kv.Key;
                    yield return ((x + tx, (int)Math.Round(pc.Oy + pc.Oz - z - 1) + ty, (int)Math.Round(pc.Oz - pc.Oy + y) + tz), kv.Value);
                }
                yield break;
            }
            double th = rot * Math.PI / 180, cs = Math.Cos(th), sn = Math.Sin(th);
            int minX = vox.Keys.Min(k => k.Item1), maxX = vox.Keys.Max(k => k.Item1);
            for (int x = minX; x <= maxX; x++)
                for (int y = -8; y < SY + 8; y++)
                    for (int z = 0; z < SZ + 8; z++)
                    {
                        double yc = y + 0.5, zc = z + 0.5;
                        double sy = pc.Oy + (yc - pc.Oy) * cs + (zc - pc.Oz) * sn, sz = pc.Oz - (yc - pc.Oy) * sn + (zc - pc.Oz) * cs;
                        if (vox.TryGetValue((x, (int)Math.Floor(sy), (int)Math.Floor(sz)), out uint c)) yield return ((x + tx, y + ty, z + tz), c);
                    }
        }

        private static Dictionary<(int, int, int), uint> Compose(Dictionary<Piece, Pose> poses, Dictionary<Piece, Dictionary<(int, int, int), uint>> content,
                                                                 List<string> overlaps = null)
        {
            var d = new Dictionary<(int, int, int), uint>();
            var owner = new Dictionary<(int, int, int), Piece>();
            foreach (Piece p in pieces)
                foreach (var (q, c) in Placed(p, content[p], poses[p]))
                {
                    if (owner.TryGetValue(q, out Piece other) && other != p) overlaps?.Add($"{other.Name}/{p.Name} at {q}");
                    owner[q] = p;
                    d[q] = c;
                }
            return d;
        }

        private static void Apply(Dictionary<(int, int, int), uint> vox, List<Change> changes, float upTo)
        {
            foreach (Change c in changes)
            {
                if ((c.T0 + c.T1) / 2f > upTo) continue;
                if (c.After == 0) vox.Remove((c.X, c.Y, c.Z));
                else vox[(c.X, c.Y, c.Z)] = c.After;
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static Pose Mix(Pose a, Pose b, float t) => new Pose
        {
            X = Lerp(a.X, b.X, t), Y = Lerp(a.Y, b.Y, t), Z = Lerp(a.Z, b.Z, t), Rot = Lerp(a.Rot, b.Rot, t), Len = Lerp(a.Len, b.Len, t),
            Vis = t <= 0f ? a.Vis : t >= 1f ? b.Vis : a.Vis || b.Vis,
        };

        /// <summary>The state after <paramref name="stepCount"/> steps plus a fraction <paramref name="f"/> of the next one.</summary>
        private static (Dictionary<Piece, Pose>, Dictionary<Piece, Dictionary<(int, int, int), uint>>) StateAt(int stepCount, float f)
        {
            var poses = pieces.ToDictionary(p => p, p => p.Start);
            var content = pieces.ToDictionary(p => p, p => new Dictionary<(int, int, int), uint>(p.Vox));
            for (int i = 0; i <= stepCount && i < steps.Count; i++)
            {
                float k = i < stepCount ? 1f : f;
                if (i == stepCount && f <= 0f) break;
                Step s = steps[i];
                foreach (Tween tw in s.Tweens)
                {
                    float u = tw.B > tw.A ? Math.Clamp((k - tw.A) / (tw.B - tw.A), 0f, 1f) : 1f;
                    poses[tw.P] = Mix(poses[tw.P], tw.To, u);
                }
                foreach (var (p, e) in s.Waves) Apply(content[p], e, k);
            }
            return (poses, content);
        }

        /// <summary>The capacitor model (BaseArt, 16 x 16 like the vanilla base): the start of the transformation, above ground.</summary>
        public static VoxCanvas CapacitorModel()
        {
            Build();
            var (poses, content) = StateAt(0, 0f);
            var overlaps = new List<string>();
            var vox = Compose(poses, content, overlaps);
            if (overlaps.Count > 0) throw new InvalidOperationException($"Capacitor: pieces overlap, e.g. {overlaps[0]}");
            var above = vox.Where(kv => kv.Key.Item3 >= ZOff).ToList();
            int top = above.Max(kv => kv.Key.Item3) - ZOff + 1;
            var c = new VoxCanvas(16, 16, top);
            foreach (var kv in above)
            {
                var (x, y, z) = kv.Key;
                if (x < 0 || x > 15 || y < 1 || y > 16) throw new InvalidOperationException($"Capacitor voxel outside the tile: {kv.Key}");
                c.Set(x, y - 1, z - ZOff, kv.Value);
            }
            return c;
        }

        private static void Check()
        {
            var (poses, content) = StateAt(steps.Count, 0f);
            var overlaps = new List<string>();
            var end = Compose(poses, content, overlaps);
            if (overlaps.Count > 0) throw new InvalidOperationException($"Core: pieces overlap, e.g. {overlaps[0]}");
            var goal = CoreModel();
            var diff = end.Keys.Union(goal.Keys).Where(p => (end.TryGetValue(p, out uint a) ? a : 0) != (goal.TryGetValue(p, out uint b) ? b : 0)).ToList();
            if (diff.Count > 0) throw new InvalidOperationException($"The transformation doesn't end at the core: {diff.Count} voxels differ, e.g. {diff[0]}");
            foreach (Piece p in pieces)
                if (poses[p].Vis && (poses[p].Rot != 0f || poses[p].X != 0f || poses[p].Y != 0f || poses[p].Z != 0f || poses[p].Len != 1f))
                    throw new InvalidOperationException($"Piece {p.Name} isn't home at the end");
            // Wave changes must start from what is there.
            var vox = pieces.ToDictionary(p => p, p => new Dictionary<(int, int, int), uint>(p.Vox));
            foreach (Step s in steps)
                foreach (var (p, e) in s.Waves)
                    foreach (Change c in e)
                    {
                        uint now = vox[p].TryGetValue((c.X, c.Y, c.Z), out uint v) ? v : 0;
                        if (now != c.Before) throw new InvalidOperationException($"Step {s.Name}: {p.Name} at {(c.X, c.Y, c.Z)} is {now:X8}, expected {c.Before:X8}");
                        if (c.After == 0) vox[p].Remove((c.X, c.Y, c.Z)); else vox[p][(c.X, c.Y, c.Z)] = c.After;
                    }
        }

        // ---- Output ----

        public static void Write(string modelsDir, string previewDir = null)
        {
            Build();
            Check();
            var inv = CultureInfo.InvariantCulture;
            var palette = new List<uint>();
            int Pal(uint c) { if (c == 0) return 0; int i = palette.IndexOf(c); if (i < 0) { palette.Add(c); i = palette.Count - 1; } return i + 1; }
            string F(float v) => v.ToString("0.###", inv);
            string PoseText(Pose q) => $"{F(q.X)} {F(q.Y)} {F(q.Z)} {F(q.Rot)} {F(q.Len)} {(q.Vis ? 1 : 0)}";

            var body = new StringBuilder();
            foreach (Piece p in pieces)
            {
                body.AppendLine($"piece {p.Name} {p.Vox.Count} {F(p.Ox)} {F(p.Oy)} {F(p.Oz)}");
                foreach (var kv in p.Vox.OrderBy(k => k.Key)) body.AppendLine($"{kv.Key.Item1} {kv.Key.Item2} {kv.Key.Item3} {Pal(kv.Value)}");
            }
            foreach (Piece p in pieces) body.AppendLine($"pose {p.Name} {PoseText(p.Start)}");
            foreach (Step s in steps)
            {
                body.AppendLine($"step {s.Name} {F(s.Seconds * TimeScale)} {F(s.Load)}");
                foreach (Tween tw in s.Tweens) body.AppendLine($"tween {tw.P.Name} {F(tw.A)} {F(tw.B)} {tw.Ease} {PoseText(tw.To)}");
                foreach (var (p, e) in s.Waves)
                {
                    body.AppendLine($"wave {p.Name} {e.Count}");
                    foreach (Change c in e) body.AppendLine($"{c.X} {c.Y} {c.Z} {Pal(c.Before)} {Pal(c.After)} {F(c.T0)} {F(c.T1)} {c.Dir}");
                }
                foreach (var (dir, at, name, vol) in s.Sounds) body.AppendLine($"snd {dir} {F(at)} ascensions/particle_accelerator/{name} {F(vol)}");
                foreach (var (name, a, b, vol) in s.Loops) body.AppendLine($"loop ascensions/particle_accelerator/{name} {F(a)} {F(b)} {F(vol)}");
            }
            body.AppendLine("end");

            var sb = new StringBuilder();
            sb.AppendLine("# Particle Accelerator: capacitor -> core (VoxTool AcceleratorMorph); the game plays it backwards for core -> capacitor.");
            sb.AppendLine($"# Step lengths include TimeScale {F(TimeScale)} (change it in AcceleratorMorph.cs and rebuild).");
            sb.AppendLine($"size {SX} {SY} {SZ}");
            sb.AppendLine($"origin -8 -9 {-ZOff}");
            sb.AppendLine($"timescale {F(TimeScale)}");
            sb.AppendLine("palette " + string.Join(" ", palette.Select(c => c.ToString("X8"))));
            sb.Append(body);
            string path = Path.Combine(modelsDir, "ascensions", "particle", "accelerator_morph.txt");
            File.WriteAllText(path, sb.ToString());
            float baseSum = steps.Sum(s => s.Seconds);
            Console.WriteLine($"accelerator morph: {steps.Count} steps, {pieces.Count} pieces, {baseSum:0.00} s at TimeScale 1, " +
                              $"{baseSum * TimeScale:0.00} s now (TimeScale {TimeScale}) -> {path}");
            foreach (Step s in steps)
                Console.WriteLine($"  {s.Name,-11} {s.Seconds * TimeScale,5:0.00} s  moves {string.Join(", ", s.Tweens.Select(t => t.P.Name))}" +
                                  (s.Waves.Count > 0 ? $"  changes {string.Join(", ", s.Waves.Select(w => $"{w.p.Name} x{w.e.Count}"))}" : ""));

            if (previewDir == null) return;
            Directory.CreateDirectory(previewDir);
            var frames = new List<Image>();
            void Frame(int n, float f, bool barrelTop = false)
            {
                var (poses, content) = StateAt(n, f);
                var vox = Compose(poses, content);
                // Room around the grid for the tilting cell (or only the barrel's top half, for a close look).
                int z0 = barrelTop ? 28 : 0;
                var canvas = barrelTop ? new VoxCanvas(SX, SY, SZ - z0) : new VoxCanvas(SX + 4, SY + 16, SZ + 6);
                foreach (var kv in vox)
                    if (barrelTop) { if (kv.Key.Item3 >= z0) canvas.Set(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3 - z0, kv.Value); }
                    else if (kv.Key.Item3 >= ZOff - 1) canvas.Set(kv.Key.Item1 + 2, kv.Key.Item2 + 8, kv.Key.Item3, kv.Value);
                Image full = Preview.Render(canvas.ToModel("f"), 260);
                var half = new Image(260, 260);
                for (int y = 0; y < 260; y++) for (int x = 0; x < 260; x++) half.Set(x, y, full.Get(x, y));
                frames.Add(half);
            }
            Frame(0, 0f);
            for (int i = 0; i < steps.Count; i++) { Frame(i, 0.5f); Frame(i + 1, 0f); }
            // The last step (cables and rails) in detail.
            foreach (float f in new[] { 0.2f, 0.31f, 0.42f, 0.52f, 0.62f, 0.73f, 0.8f, 0.9f }) Frame(steps.Count - 1, f, true);
            const int cols = 6;
            int rows = (frames.Count + cols - 1) / cols;
            var sheet = new Image(260 * cols, 260 * rows, 0xFF2B2B2Bu);
            for (int i = 0; i < frames.Count; i++)
                for (int y = 0; y < 260; y++)
                    for (int x = 0; x < 260; x++)
                        sheet.Set((i % cols) * 260 + x, (i / cols) * 260 + y, frames[i].Get(x, y));
            sheet.SavePng(Path.Combine(previewDir, "morph_sheet.png"));
            Console.WriteLine($"  preview: {frames.Count} frames (start, then the middle and end of every step) -> {previewDir}");
        }
    }
}
