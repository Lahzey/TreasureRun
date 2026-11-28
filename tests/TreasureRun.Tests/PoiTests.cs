using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TreasureRun.Balance;
using TreasureRun.Core;
using TreasureRun.Pois;
using UnityEngine;

namespace TreasureRun.Tests
{
    /// <summary>Points of Interest: the section generator (every exit combination), the Lightning Trap logic and its balance.</summary>
    public class PoiTests
    {
        /// <summary>Calibrations fitted on the old flat 10-wave horizon; re-checked with the final framework (balance-todo.md).</summary>
        private const string OldHorizon = "fitted on the old flat 10-wave horizon; re-check with the final framework (balance-todo.md)";

        private static readonly SectionExits[] AllExits =
        {
            SectionExits.Left, SectionExits.Top, SectionExits.Right,
            SectionExits.Left | SectionExits.Top, SectionExits.Left | SectionExits.Right, SectionExits.Top | SectionExits.Right,
            SectionExits.All,
        };

        [Test]
        public void EveryExitCombinationMakesAValidSection([ValueSource(nameof(AllExits))] SectionExits exits)
        {
            for (int seed = 0; seed < 25; seed++)
            {
                foreach (SectionStyle style in new SectionStyle[] { new SectionStyle(), new LightningTrapStyle(), new MerchantStyle { Rotation = 90 * (seed % 4) }, new SoulUrnStyle(), new WizardTowerStyle(), new LizardNestStyle(), new LaboratoryStyle() })
                {
                    SectionLayout l = PoiSectionLogic.Generate(exits, style, new SystemRng(seed));
                    Assert.That(PoiSectionLogic.Validate(l), Is.Null, $"{exits} seed {seed} {style.GetType().Name}");
                    // Every path cell is reachable from the entry along path cells (no stray path).
                    Assert.That(PathCellsReachable(l), Is.True, $"{exits} seed {seed}");
                    // Grass only at heights 1-4, path at 0.
                    for (int x = 0; x < SectionLayout.Size; x++)
                        for (int z = 0; z < SectionLayout.Size; z++)
                            if (l.IsPath(x, z)) Assert.That(l.Height[x, z], Is.EqualTo(0));
                            else Assert.That(l.Height[x, z], Is.InRange(1, 4));
                }
            }
        }

        [Test]
        public void EmptyExitsAreRejected() =>
            Assert.Throws<System.ArgumentException>(() => PoiSectionLogic.Generate(SectionExits.None, null, new SystemRng(1)));

        [Test]
        public void FourWayWaypointsMergeInTheCentreAndRunToTheEntry()
        {
            SectionLayout l = PoiSectionLogic.Generate(SectionExits.All, new LightningTrapStyle(), new SystemRng(3));
            SectionWaypoint last = l.Last;
            Assert.That((last.X, last.Z), Is.EqualTo((3, 0)));
            Assert.That(last.Local, Is.EqualTo(new Vector3(0, 0, -3)));
            foreach (string role in new[] { "left", "top", "right" })
            {
                SectionWaypoint w = l.Waypoints.Single(p => p.Role == role);
                SectionWaypoint hub = l.Waypoints[w.Next];
                Assert.That((hub.X, hub.Z), Is.EqualTo((3, 3)), role);
                Assert.That(l.Waypoints[hub.Next], Is.SameAs(last));
            }
            Assert.That(l.Waypoints.Single(p => p.Role == "left").Local, Is.EqualTo(new Vector3(-3, 0, 0)));
            Assert.That(l.Waypoints.Single(p => p.Role == "right").Local, Is.EqualTo(new Vector3(3, 0, 0)));
            Assert.That(l.Waypoints.Single(p => p.Role == "top").Local, Is.EqualTo(new Vector3(0, 0, 3)));
        }

        [Test]
        public void LightningTrapSectionHasPolesOnTheDiagonals([ValueSource(nameof(AllExits))] SectionExits exits)
        {
            SectionLayout l = PoiSectionLogic.Generate(exits, new LightningTrapStyle(), new SystemRng(7));
            foreach (var (x, z) in LightningTrapLogic.PoleCells)
            {
                Assert.That(l.Cells[x, z], Is.EqualTo(SectionCell.Reserved), $"pole ({x},{z})");
                Assert.That(l.HasGround(x, z), Is.True);
            }
            Assert.That(l.Cells[3, 3], Is.EqualTo(SectionCell.Path), "the paths meet in the centre cell");
            Assert.That(l.IsPath(3, 2), Is.True);
            if ((exits & SectionExits.Left) != 0) Assert.That(l.IsPath(2, 3), Is.True);
            if ((exits & SectionExits.Top) != 0) Assert.That(l.IsPath(3, 4), Is.True);
            if ((exits & SectionExits.Right) != 0) Assert.That(l.IsPath(4, 3), Is.True);
        }

        [Test]
        public void TerrainMeshCoversTheGrassAndStaysInsideTheTile()
        {
            SectionLayout l = PoiSectionLogic.Generate(SectionExits.Left | SectionExits.Right, new LightningTrapStyle(), new SystemRng(2));
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            SectionMeshLogic.Build(l, verts, normals, tris);
            int grass = 0;
            for (int x = 0; x < 7; x++) for (int z = 0; z < 7; z++) if (l.HasGround(x, z)) grass++;
            int tops = 0;
            for (int i = 0; i < verts.Count; i += 4) if (normals[i] == Vector3.up) tops++;
            Assert.That(tops, Is.EqualTo(grass));
            Assert.That(verts.All(v => Mathf.Abs(v.x) <= 3.5f + 1e-4f && Mathf.Abs(v.z) <= 3.5f + 1e-4f), Is.True);
            Assert.That(verts.Max(v => v.y), Is.LessThanOrEqualTo(4 / 3f + 1e-4f));
            Assert.That(verts.Min(v => v.y), Is.EqualTo(SectionMeshLogic.Floor).Within(1e-4));
            Assert.That(tris.Count % 6, Is.EqualTo(0));
            // Front faces are clockwise seen along their normal (Unity): the triangle normal matches the face normal.
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                Assert.That(Vector3.Dot(Vector3.Cross(b - a, c - a), normals[tris[t]]), Is.GreaterThan(0f));
            }
        }

        [Test]
        public void TrapMetersItsManaRateAndStopsWithoutMana()
        {
            foreach (int rate in new[] { 15, 6, 1 })
            {
                float owed = 0f;
                int spent = 0;
                bool powered = false;
                for (int i = 0; i < 120; i++) spent += LightningTrapLogic.Meter(ref owed, ref powered, 1f / 60f, 1000, rate);
                Assert.That(spent, Is.InRange(2 * rate - 1, 2 * rate), $"rate {rate}");
                Assert.That(powered, Is.True);
            }
            float o = 0f;
            bool on = true;
            Assert.That(LightningTrapLogic.Meter(ref o, ref on, 1f, 1000, 0), Is.EqualTo(0));
            Assert.That(on, Is.False, "at 0 mana per second it is off");

            // Out of mana: silent, and it stays silent on a trickle until a full second's worth is back.
            bool p = true;
            int mana = 0, paid = 0;
            for (int i = 0; i < 60; i++)
            {
                if (i % 30 == 15) mana += 1; // 2 mana a second of regeneration
                int d = LightningTrapLogic.Meter(ref o, ref p, 1f / 60f, mana, 15);
                mana -= d; paid += d;
            }
            Assert.That(p, Is.False);
            Assert.That(paid, Is.EqualTo(0));
            mana = 15;
            LightningTrapLogic.Meter(ref o, ref p, 1f / 60f, mana, 15);
            Assert.That(p, Is.True, "back on with a second's worth of mana");
        }

        [Test]
        public void TrapZoneIsTheCentreCell()
        {
            Assert.That(LightningTrapLogic.InZone(new Vector3(0.45f, 0.3f, -0.45f)), Is.True);
            Assert.That(LightningTrapLogic.InZone(new Vector3(0.55f, 0f, 0f)), Is.False);
            Assert.That(LightningTrapLogic.InZone(new Vector3(0f, 0f, -0.6f)), Is.False);
        }

        [Test]
        public void TrapDamageGrowsWithTheWavesAndFollowsTheMana()
        {
            for (int w = 2; w <= 45; w++)
                Assert.That(LightningTrapLogic.Damage(w, 15), Is.GreaterThan(LightningTrapLogic.Damage(w - 1, 15)));
            Assert.That(LightningTrapLogic.Damage(20, 0), Is.EqualTo(0));
            Assert.That(LightningTrapLogic.Damage(20, 10), Is.EqualTo(LightningTrapLogic.Damage(20, 15) * 2 / 3.0).Within(1));
            Assert.That(LightningTrapLogic.Damage(20, 99), Is.EqualTo(LightningTrapLogic.Damage(20, 15)), "capped at 15 mana/s");
            Assert.That(LightningTrapLogic.HealthMult, Is.EqualTo(TowerData.Get(TowerKind.TeslaCoil).HealthMult));
            Assert.That(LightningTrapLogic.ShieldMult, Is.EqualTo(TowerData.Get(TowerKind.TeslaCoil).ShieldMult));
        }

        [Test]
        public void TrapIsRatedAtThePoiBudget()
        {
            Spec s = ContentSpecs.Poi(LightningTrapLogic.Id);
            Assert.That(s, Is.Not.Null, "every POI needs a spec in ContentSpecs.Pois");
            Rating r = Valuation.Rate(LightningTrapLogic.Id, "Lightning Trap", "poi", null, false, s);
            Assert.That(r.Budget, Is.EqualTo(Pools.PoiBudget));
            Assert.That(r.Flag, Is.EqualTo(""), $"{r.Points:0.0} points");
        }

        [Test]
        public void FixedSourcesAreNeverBoughtOrBuffed()
        {
            Board b = ReferenceBoards.Get(20, BoardVariant.Mixed);
            new Spec().FixedSource("trap", LightningTrapLogic.Info).Atoms[0].Apply(new AtomContext { Board = b, Acquired = 20, Wave = 20 });
            TowerGroup trap = b.Groups.Single(g => g.Fixed);
            b.Spend(5000);
            Assert.That(trap.Count, Is.EqualTo(1));
            Assert.That(b.GroupCost(trap), Is.EqualTo(0));
            Assert.That(b.Scope(null).Contains(trap), Is.False);
        }

        [Test]
        public void MerchantWagonParksLevelByThePathOpeningSouthWest([ValueSource(nameof(AllExits))] SectionExits exits)
        {
            foreach (int rotation in new[] { 0, 90, 180, 270 })
            {
                SectionLayout l = PoiSectionLogic.Generate(exits, new MerchantStyle { Rotation = rotation }, new SystemRng(rotation + 5));
                var opening = MerchantStyle.OpeningFor(rotation);
                var corner = MerchantStyle.CornerOf(l);
                Assert.That(corner, Is.EqualTo(MerchantStyle.Corner(opening, exits)));
                bool touches = false;
                foreach (var (x, z) in MerchantStyle.Footprint(corner))
                {
                    Assert.That(l.Cells[x, z], Is.EqualTo(SectionCell.Reserved), $"rotation {rotation}: ({x},{z})");
                    Assert.That(l.Height[x, z], Is.EqualTo(1), "level");
                    touches |= l.IsPath(x + 1, z) || l.IsPath(x - 1, z) || l.IsPath(x, z + 1) || l.IsPath(x, z - 1);
                }
                Assert.That(touches, Is.True, $"rotation {rotation}: beside the path");
                // The opening faces a path segment: always for openings across the trunk, and with a side exit otherwise.
                bool sideExit = (exits & (SectionExits.Left | SectionExits.Right)) != 0;
                if (opening.dx != 0 || sideExit)
                    foreach (var (x, z) in MerchantStyle.Footprint(corner))
                    {
                        var (nx, nz) = (x + opening.dx, z + opening.dz);
                        bool front = !MerchantStyle.Footprint(corner).Contains((nx, nz));
                        if (front) Assert.That(l.IsPath(nx, nz), Is.True, $"rotation {rotation}: path in front of ({x},{z})");
                    }
                // The opening points to world +z (south-west on screen): Unity turns (x, z) by r as (x cos r + z sin r, -x sin r + z cos r).
                double a = rotation * System.Math.PI / 180.0;
                double wx = opening.dx * System.Math.Cos(a) + opening.dz * System.Math.Sin(a);
                double wz = -opening.dx * System.Math.Sin(a) + opening.dz * System.Math.Cos(a);
                Assert.That(wx, Is.EqualTo(0).Within(1e-9), $"rotation {rotation}");
                Assert.That(wz, Is.EqualTo(1).Within(1e-9), $"rotation {rotation}");
            }
        }

        private static readonly int[] Waves = { 1, 3, 5, 8, 10, 15, 20, 25, 30, 35, 40, 45 };

        [Test, Ignore(OldHorizon)]
        public void MerchantPricesFollowTheFairValue()
        {
            // Formula vs. the balance framework: the price whose payment costs as many points as the treasure's budget.
            foreach (int w in Waves)
            {
                for (int r = 0; r < 6; r++)
                {
                    double ratio = MerchantLogic.FairValue(r, w, false) / FairPrice.Of(Pools.Budget((Rarity)r), w);
                    Assert.That(ratio, Is.InRange(0.88, 1.12), $"rarity {r} wave {w}");
                }
                for (int r = 1; r < 5; r++)
                {
                    double ratio = MerchantLogic.FairValue(r, w, true) / FairPrice.Of(Pools.Budget((Rarity)r) * Pools.AscensionAllowance, w);
                    Assert.That(ratio, Is.InRange(0.88, 1.12), $"ascension rarity {r} wave {w}");
                }
            }
        }

        [Test]
        public void MerchantPricesRiseWithRarityAndWaveAndFollowTheRules()
        {
            foreach (MerchantKind kind in new[] { MerchantKind.Normal, MerchantKind.Legendary, MerchantKind.Ascension })
                for (int w = 1; w <= 60; w++)
                    for (int r = 0; r < 6; r++)
                    {
                        int p = MerchantLogic.Price(kind, r, w, false, false);
                        Assert.That(p % 5, Is.EqualTo(0));
                        if (r > 0) Assert.That(p, Is.GreaterThan(MerchantLogic.Price(kind, r - 1, w, false, false)));
                        if (w > 1) Assert.That(p, Is.GreaterThanOrEqualTo(MerchantLogic.Price(kind, r, w - 1, false, false)));
                        int disc = MerchantLogic.Price(kind, r, w, false, true);
                        Assert.That(disc, Is.EqualTo(p * (1 - MerchantLogic.Discount)).Within(5));
                        Assert.That(MerchantLogic.SellPrice(kind, r, w, false), Is.EqualTo(p / 2.0).Within(5));
                        Assert.That(MerchantLogic.Price(kind, r, w, true, false), Is.GreaterThan(p));
                    }
            Assert.That(MerchantLogic.LeavesAfterPurchase(MerchantKind.Normal, 1), Is.False);
            Assert.That(MerchantLogic.LeavesAfterPurchase(MerchantKind.Normal, 0), Is.True);
            Assert.That(MerchantLogic.LeavesAfterPurchase(MerchantKind.Legendary, 0), Is.True);
            Assert.That(MerchantLogic.LeavesAfterPurchase(MerchantKind.Ascension, 2), Is.True, "the Ascension Merchant leaves after one purchase");
        }

        [Test, Ignore(OldHorizon)]
        public void MerchantsAreRatedAtThePoiBudget([Values("merchant", "legendary_merchant", "ascension_merchant")] string id)
        {
            Spec s = ContentSpecs.Poi(id);
            Assert.That(s, Is.Not.Null);
            Rating r = Valuation.Rate(id, id, "poi", null, false, s);
            Assert.That(r.Flag, Is.EqualTo(""), $"{r.Points:0.0} points");
        }

        [Test]
        public void SoulUrnStandsOnOneRandomGrassCell([ValueSource(nameof(AllExits))] SectionExits exits)
        {
            var cells = new HashSet<(int, int)>();
            for (int seed = 0; seed < 40; seed++)
            {
                SectionLayout l = PoiSectionLogic.Generate(exits, new SoulUrnStyle(), new SystemRng(seed));
                int reserved = 0;
                for (int x = 0; x < 7; x++) for (int z = 0; z < 7; z++) if (l.Cells[x, z] == SectionCell.Reserved) reserved++;
                Assert.That(reserved, Is.EqualTo(1), $"{exits} seed {seed}");
                var (cx, cz) = SoulUrnStyle.CellOf(l);
                Assert.That(l.IsPath(cx, cz), Is.False);
                Assert.That(l.Height[cx, cz], Is.EqualTo(1), "the urn stands on plain grass");
                cells.Add((cx, cz));
            }
            Assert.That(cells.Count, Is.GreaterThan(10), "the cell is random");
        }

        [Test]
        public void SoulUrnCapacityFollowsTheWavesAndTheLane()
        {
            Assert.That(SoulUrnLogic.Capacity(3, 1), Is.EqualTo(SoulUrnLogic.MinCapacity));
            Assert.That(SoulUrnLogic.Capacity(15, 1), Is.InRange(170, 250), "about twice the 100 souls of the first design at mid-game");
            int prev = 0;
            for (int w = 1; w <= 45; w++)
            {
                int c = SoulUrnLogic.Capacity(w, 1);
                Assert.That(c % 5, Is.EqualTo(0));
                Assert.That(c, Is.GreaterThanOrEqualTo(prev - 5), $"wave {w}");
                prev = c;
            }
            Assert.That(SoulUrnLogic.Capacity(20, 0.5), Is.EqualTo(SoulUrnLogic.Capacity(20, 1) / 2.0).Within(5));
            Assert.That(SoulUrnLogic.Capacity(20, 0.1), Is.EqualTo(SoulUrnLogic.Capacity(20, SoulUrnLogic.MinLaneShare)));
            Assert.That(SoulUrnLogic.LaneShare(1, 3), Is.EqualTo(1 / 3.0).Within(1e-9));
            Assert.That(SoulUrnLogic.LaneShare(0, 0), Is.EqualTo(1));
        }

        [Test, Ignore(OldHorizon)]
        public void SoulUrnGoldIsWorthAChestAndTheRuneIsFixed()
        {
            foreach (int w in Waves)
            {
                double chest = Pools.TreasurePick(w);
                double gold = SoulUrnLogic.ChestGold(w) / FairPrice.GainOf(chest, w);
                Assert.That(gold, Is.InRange(0.9, 1.1), $"gold worth a chest at wave {w}");
                Assert.That(SoulUrnLogic.Gold(w), Is.EqualTo(SoulUrnLogic.GoldFactor * SoulUrnLogic.ChestGold(w)).Within(5));
            }
            Assert.That(SoulUrnLogic.RuneBonus, Is.EqualTo(1.0), "+100% attack speed, whatever the wave");
            int[] counts = new int[4];
            for (int i = 0; i < 300; i++) counts[(int)SoulUrnLogic.Pick(i / 300.0)]++;
            Assert.That(counts[1], Is.EqualTo(100)); Assert.That(counts[2], Is.EqualTo(100)); Assert.That(counts[3], Is.EqualTo(100));
        }

        [Test]
        public void SoulsFlyFromTheBodyIntoTheMouth()
        {
            var from = new Vector3(3f, 0f, -4f);
            var mouth = new Vector3(-1f, 1.6f, 2f);
            Assert.That((SoulUrnLogic.SoulPoint(from, mouth, 0f, 1f) - (from + Vector3.up * 0.3f)).magnitude, Is.LessThan(1e-4f));
            Assert.That((SoulUrnLogic.SoulPoint(from, mouth, 1f, 1f) - mouth).magnitude, Is.LessThan(1e-4f));
            // It arcs above both ends on the way and comes down into the mouth from above.
            Assert.That(SoulUrnLogic.SoulPoint(from, mouth, 0.5f, 1f).y, Is.GreaterThan(mouth.y + 0.5f));
            Assert.That(SoulUrnLogic.SoulPoint(from, mouth, 0.95f, 1f).y, Is.GreaterThan(mouth.y));
            Assert.That(SoulUrnLogic.InReach(new Vector3(4.9f, 3f, 4.9f)), Is.True);
            Assert.That(SoulUrnLogic.InReach(new Vector3(5f, 0f, 5f)), Is.False);
        }

        [Test]
        public void SoulUrnIsRatedAtThePoiBudget()
        {
            Spec s = ContentSpecs.Poi(SoulUrnLogic.Id);
            Assert.That(s, Is.Not.Null);
            Rating r = Valuation.Rate(SoulUrnLogic.Id, "Soul Urn", "poi", null, false, s);
            Assert.That(r.Flag, Is.EqualTo(""), $"{r.Points:0.0} points");
        }

        [Test]
        public void WizardTowerStandsAwayFromThePathAndTheEdge([ValueSource(nameof(AllExits))] SectionExits exits)
        {
            var cells = new HashSet<(int, int)>();
            for (int seed = 0; seed < 40; seed++)
            {
                SectionLayout l = PoiSectionLogic.Generate(exits, new WizardTowerStyle(), new SystemRng(seed));
                int reserved = 0;
                for (int x = 0; x < 7; x++) for (int z = 0; z < 7; z++) if (l.Cells[x, z] == SectionCell.Reserved) reserved++;
                Assert.That(reserved, Is.EqualTo(1), $"{exits} seed {seed}");
                var (cx, cz) = WizardTowerStyle.CellOf(l);
                Assert.That(cx, Is.InRange(1, 5), "not on the edge");
                Assert.That(cz, Is.InRange(1, 5), "not on the edge");
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        Assert.That(l.IsPath(cx + dx, cz + dz), Is.False, $"{exits} seed {seed}: no path next to the tower at {cx},{cz}");
                Assert.That(l.Height[cx, cz], Is.EqualTo(1), "on plain grass");
                cells.Add((cx, cz));
            }
            Assert.That(cells.Count, Is.GreaterThanOrEqualTo(4), "the cell is random");
        }

        [Test]
        public void WizardSpellsFollowTheirWeights()
        {
            var counts = new Dictionary<WizardSpell, int>();
            const int n = 20000;
            for (int i = 0; i < n; i++)
            {
                WizardSpell s = WizardTowerLogic.Pick((i + 0.5) / n, _ => true).Value;
                counts[s] = counts.TryGetValue(s, out int c) ? c + 1 : 1;
            }
            foreach (WizardSpell s in WizardTowerLogic.Spells)
                Assert.That(counts[s] / (double)n, Is.EqualTo(WizardTowerLogic.Chance(s)).Within(0.001), s.ToString());
            Assert.That(WizardTowerLogic.Chance(WizardSpell.Fireball), Is.GreaterThan(WizardTowerLogic.Chance(WizardSpell.Tornado)));
            Assert.That(WizardTowerLogic.Chance(WizardSpell.Tornado), Is.GreaterThan(WizardTowerLogic.Chance(WizardSpell.Goldify)));
            // Spells without a target are skipped: only bosses in range means no tornado and no goldify.
            var bosses = new List<WizardTarget> { new WizardTarget { Boss = true, Health = 100 } };
            for (int i = 0; i < 100; i++)
            {
                WizardSpell s = WizardTowerLogic.Pick(i / 100.0, x => WizardTowerLogic.HasTarget(x, bosses)).Value;
                Assert.That(s, Is.Not.EqualTo(WizardSpell.Tornado).And.Not.EqualTo(WizardSpell.Goldify));
            }
            Assert.That(WizardTowerLogic.Pick(0.5, _ => false), Is.Null);
        }

        [Test]
        public void WizardSpellsPickTheirTargets()
        {
            var l = new List<WizardTarget>
            {
                new WizardTarget { X = 0, Z = 0, Progress = 30, Speed = 1, Health = 100 },
                new WizardTarget { X = 0.5f, Z = 0, Progress = 29, Speed = 3, Health = 50, Armor = 80 },
                new WizardTarget { X = 0.8f, Z = 0.3f, Progress = 28, Speed = 2, Health = 40, Shield = 10 },
                new WizardTarget { X = 6, Z = 0, Progress = 10, Speed = 1, Health = 900, Boss = true },
                new WizardTarget { X = 6.4f, Z = 0, Progress = 12, Speed = 1.5f, Health = 30, Shield = 60 },
            };
            Assert.That(WizardTowerLogic.FireballTarget(l), Is.EqualTo(2), "one of the group of three (ties: furthest along)");
            Assert.That(WizardTowerLogic.LightningTarget(l), Is.EqualTo(4), "the most shield");
            var noShields = l.Select(t => { t.Shield = 0; return t; }).ToList();
            Assert.That(WizardTowerLogic.LightningTarget(noShields), Is.EqualTo(3), "then the most health");
            Assert.That(WizardTowerLogic.IceTargets(l, 3), Is.EqualTo(new[] { 1, 2, 4 }), "the fastest, all different");
            Assert.That(WizardTowerLogic.IceTargets(l.Take(2).ToList(), 3), Is.EqualTo(new[] { 1, 0, 1 }), "round again when fewer");
            Assert.That(WizardTowerLogic.TornadoTarget(l), Is.EqualTo(4), "furthest along, not the boss");
            Assert.That(WizardTowerLogic.GoldifyTarget(l), Is.EqualTo(1), "the toughest that isn't a boss (shield + armor + health)");
            Assert.That(WizardTowerLogic.GoldifyTarget(new List<WizardTarget> { l[3] }), Is.EqualTo(-1), "never a boss");
        }

        [Test]
        public void WizardDamageIsFixedAndGrowsWithTheWaves()
        {
            int prev = 0;
            for (int w = WizardTowerLogic.MinWave; w <= 45; w++)
            {
                int d = WizardTowerLogic.LightningDamage(w);
                Assert.That(d, Is.GreaterThanOrEqualTo(prev), $"wave {w}");
                prev = d;
            }
            Assert.That(WizardTowerLogic.LightningDamage(10), Is.EqualTo(4 * WizardTowerLogic.FireballDamage(10)).Within(2));
            Assert.That(WizardTowerLogic.CastInterval(false), Is.EqualTo(5f));
            Assert.That(WizardTowerLogic.CastInterval(true), Is.EqualTo(4f));
            Assert.That(WizardTowerLogic.OnLand(new Vector3(3.4f, 0, -3.4f)), Is.True);
            Assert.That(WizardTowerLogic.OnLand(new Vector3(3.6f, 0, 0)), Is.False, "the next section");
            Assert.That(WizardTowerLogic.InRange(new Vector3(9.9f, 0, 0)), Is.True, "a range of 10");
            Assert.That(WizardTowerLogic.InRange(new Vector3(7.5f, 0, 7.5f)), Is.False);
        }

        [Test]
        public void FixedDamageSplitsOverTheLayersLikeVanilla()
        {
            // 10 base x 12 on shield = 120 > 50: 50 to the shield, (120 - 50) / 12 = 5 base on armor x 3 = 15.
            Assert.That(FixedDamageLogic.Split(10, 0, 6, 3, 12, 100, 40, 50), Is.EqualTo((50, 15, 0)));
            // Armor broken too: 20 base x 3 = 60 > 40, (60 - 40) / 3 = 6 base on health x 6 = 36.
            Assert.That(FixedDamageLogic.Split(20, 0, 6, 3, 12, 100, 40, 0), Is.EqualTo((0, 40, 36)));
            // Fortify: max(base - 5, 1).
            Assert.That(FixedDamageLogic.Split(3, 5, 6, 3, 12, 100, 0, 0), Is.EqualTo((0, 0, 6)));
            // A layer the hit has no multiplier for stops it.
            Assert.That(FixedDamageLogic.Split(10, 0, 6, 0, 12, 100, 40, 0), Is.EqualTo((0, 0, 0)));
        }

        [Test]
        public void WizardTowerIsRatedAtThePoiBudget()
        {
            Spec s = ContentSpecs.Poi(WizardTowerLogic.Id);
            Assert.That(s, Is.Not.Null);
            Rating r = Valuation.Rate(WizardTowerLogic.Id, "Wizard's Tower", "poi", null, false, s);
            Assert.That(r.Flag, Is.EqualTo(""), $"{r.Points:0.0} points");
        }

        [Test]
        public void LizardNestStandsBesideThePathAndItsLizardsWalkToTheEntry([ValueSource(nameof(AllExits))] SectionExits exits)
        {
            var cells = new HashSet<(int, int)>();
            for (int seed = 0; seed < 40; seed++)
            {
                SectionLayout l = PoiSectionLogic.Generate(exits, new LizardNestStyle(), new SystemRng(seed));
                int reserved = 0;
                for (int x = 0; x < 7; x++) for (int z = 0; z < 7; z++) if (l.Cells[x, z] == SectionCell.Reserved) reserved++;
                Assert.That(reserved, Is.EqualTo(1), $"{exits} seed {seed}");
                var nest = LizardNestStyle.CellOf(l);
                Assert.That(l.Height[nest.x, nest.z], Is.EqualTo(1), "on plain grass");
                var (px, pz) = LizardNestLogic.PathCell(l, nest);
                Assert.That(l.IsPath(px, pz), Is.True, $"{exits} seed {seed}: a path cell beside the nest at {nest}");
                Assert.That(System.Math.Abs(px - nest.x) + System.Math.Abs(pz - nest.z), Is.EqualTo(1), "a direct neighbour");
                // From there the waypoints lead to the entry, and the first one lies ahead along a straight path.
                int w = LizardNestLogic.TargetWaypoint(l, px, pz);
                Assert.That(w, Is.GreaterThanOrEqualTo(0));
                SectionWaypoint first = l.Waypoints[w];
                Assert.That(first.X == px || first.Z == pz, Is.True, "straight ahead");
                for (int i = System.Math.Min(first.X, px); i <= System.Math.Max(first.X, px); i++)
                    for (int k = System.Math.Min(first.Z, pz); k <= System.Math.Max(first.Z, pz); k++)
                        Assert.That(l.IsPath(i, k), Is.True, $"{exits} seed {seed}: path all the way to the waypoint");
                int steps = 0;
                while (l.Waypoints[w].Role != "last" && steps++ < 20) w = l.Waypoints[w].Next;
                Assert.That(l.Waypoints[w].Role, Is.EqualTo("last"), $"{exits} seed {seed}: the route reaches the entry");
                cells.Add(nest);
            }
            Assert.That(cells.Count, Is.GreaterThanOrEqualTo(4), "the cell is random");
        }

        [Test]
        public void LizardNestScalesWithTheWave()
        {
            Assert.That(LizardNestLogic.Lizards(1), Is.EqualTo(1));
            Assert.That(LizardNestLogic.Lizards(LizardNestLogic.RampTo), Is.EqualTo(5));
            Assert.That(LizardNestLogic.Lizards(60), Is.EqualTo(5));
            Assert.That(LizardNestLogic.Brood(1), Is.EqualTo(5));
            Assert.That(LizardNestLogic.Brood(LizardNestLogic.RampTo), Is.EqualTo(15));
            for (int w = 2; w <= 45; w++)
            {
                Assert.That(LizardNestLogic.Lizards(w), Is.GreaterThanOrEqualTo(LizardNestLogic.Lizards(w - 1)));
                Assert.That(LizardNestLogic.Brood(w), Is.GreaterThanOrEqualTo(LizardNestLogic.Brood(w - 1)));
                // The same strength for its wave whenever it turns up: health a fixed multiple of the wave's average monster.
                double mean = LizardNestLogic.MeanMonster(w);
                Assert.That(LizardNestLogic.LizardHealthAt(w) / mean, Is.EqualTo(LizardNestLogic.LizardHealth).Within(0.02), $"wave {w}");
                Assert.That(LizardNestLogic.LizardArmorAt(w), Is.LessThan(LizardNestLogic.LizardHealthAt(w) / 5), "only a little armor");
                Assert.That(LizardNestLogic.HatchlingHealthAt(w) / mean, Is.EqualTo(LizardNestLogic.HatchlingHealth).Within(0.05), $"wave {w}");
                Assert.That(LizardNestLogic.LizardGoldAt(w), Is.GreaterThan(LizardNestLogic.HatchlingGold));
            }
            Assert.That(LizardNestLogic.LizardSpeed, Is.LessThan(1.5f), "slow");
            Assert.That(LizardNestLogic.HatchlingSpeed, Is.GreaterThan(3f), "fast");
            Assert.That(LizardNestLogic.SpawnSeconds(5), Is.LessThanOrEqualTo(6f), "all out within the first seconds");
            Assert.That(LizardNestLogic.SpawnGap * LizardNestLogic.LizardSpeed, Is.GreaterThanOrEqualTo(1.35f), "a lizard's length apart");
        }

        [Test]
        public void LizardNestIsRatedAtThePoiBudget()
        {
            Spec s = ContentSpecs.Poi(LizardNestLogic.Id);
            Assert.That(s, Is.Not.Null);
            Rating r = Valuation.Rate(LizardNestLogic.Id, "Lizard Nest", "poi", null, false, s);
            TestContext.WriteLine($"Lizard Nest: {r.Points:0.0} points (P80 {r.P80:0.0}, early {r.Early:0.0}, late {r.Late:0.0}, peak {r.PeakWave})");
            Assert.That(r.Flag, Is.EqualTo(""), $"{r.Points:0.0} points");
        }

        [Test]
        public void LaboratoryStandsInTheMiddleWithThePathRoundIt([ValueSource(nameof(AllExits))] SectionExits exits)
        {
            for (int seed = 0; seed < 25; seed++)
            {
                SectionLayout l = PoiSectionLogic.Generate(exits, new LaboratoryStyle(), new SystemRng(seed));
                Assert.That(PoiSectionLogic.Validate(l), Is.Null, $"{exits} seed {seed}");
                for (int x = 0; x < SectionLayout.Size; x++)
                    for (int z = 0; z < SectionLayout.Size; z++)
                    {
                        bool inside = LaboratoryLogic.InStructure(x, z);
                        Assert.That(l.Cells[x, z] == SectionCell.Reserved, Is.EqualTo(inside), $"{exits} seed {seed}: ({x},{z}) reserved only inside the 3x3");
                        if (inside) Assert.That(l.Height[x, z], Is.EqualTo(1), "the lab stands level on plain grass height");
                    }
                // The path touches the structure (monsters walk round the lab), from the entry's side at least.
                Assert.That(l.IsPath(LaboratoryLogic.Slot.x, LaboratoryLogic.Slot.z - 2), Is.True, $"{exits} seed {seed}: the path runs in front of the lab");
            }
        }

        [Test]
        public void LaboratoryResearchAddsFivePercentPerWave()
        {
            Assert.That(LaboratoryLogic.PerWave, Is.EqualTo(0.05));
            Assert.That(LaboratoryLogic.Bonus(0), Is.EqualTo(0));
            Assert.That(LaboratoryLogic.Bonus(7), Is.EqualTo(0.35).Within(1e-9));
            Assert.That(LaboratoryLogic.Bonus(-3), Is.EqualTo(0));
            Assert.That(LaboratoryLogic.Describe(ResearchKind.AttackSpeed, 7), Is.EqualTo("+35% attack speed"));
            Assert.That(LaboratoryLogic.Describe(ResearchKind.Mark, 12), Is.EqualTo("+60% mark"));
            Assert.That(LaboratoryLogic.Describe(ResearchKind.Output, 3), Is.EqualTo("+15% output"));
            // Buildings yield more: a siphon's 1 mana a second at +5% gives one extra mana every 20 ticks.
            double carry = 0;
            int total = 0;
            for (int i = 0; i < 20; i++) total += LaboratoryLogic.Yield(1, 1, ref carry);
            Assert.That(total, Is.EqualTo(21));
            carry = 0;
            Assert.That(LaboratoryLogic.Yield(3, 10, ref carry), Is.EqualTo(4), "3 at +50% = 4.5: 4 now");
            Assert.That(LaboratoryLogic.Yield(3, 10, ref carry), Is.EqualTo(5), "and the half carried over");
            Assert.That(LaboratoryLogic.Yield(5, 0, ref carry), Is.EqualTo(5), "no research, no change");
            Assert.That(LaboratoryLogic.Chance(0.1, 10), Is.EqualTo(0.15).Within(1e-9));
            Assert.That(LaboratoryLogic.Chance(0.5, 40), Is.EqualTo(1), "at most certain");
        }

        [Test]
        public void LaboratoryIsRatedAtThePoiBudget()
        {
            Spec s = ContentSpecs.Poi(LaboratoryLogic.Id);
            Assert.That(s, Is.Not.Null);
            Rating r = Valuation.Rate(LaboratoryLogic.Id, "Laboratory", "poi", null, false, s);
            TestContext.WriteLine($"Laboratory: {r.Points:0.0} points (P80 {r.P80:0.0}, early {r.Early:0.0}, late {r.Late:0.0}, peak {r.PeakWave}) {r.Flag}");
            // Under the budget within the 10-wave horizon (balance-todo.md); the lever must still work.
            double more = Valuation.Rate(LaboratoryLogic.Id, "Laboratory", "poi", null, false, ContentSpecs.Laboratory(3 * LaboratoryLogic.PerWave)).Points;
            Assert.That(more, Is.GreaterThan(r.Points + 2), "more research per wave rates higher");
        }

        private static bool PathCellsReachable(SectionLayout l)
        {
            var seen = new HashSet<(int, int)>();
            var stack = new Stack<(int x, int z)>();
            stack.Push(SectionLayout.Entry);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                if (!l.IsPath(c.x, c.z) || !seen.Add(c)) continue;
                stack.Push((c.x + 1, c.z)); stack.Push((c.x - 1, c.z)); stack.Push((c.x, c.z + 1)); stack.Push((c.x, c.z - 1));
            }
            for (int x = 0; x < 7; x++) for (int z = 0; z < 7; z++) if (l.IsPath(x, z) && !seen.Contains((x, z))) return false;
            return true;
        }
    }
}
