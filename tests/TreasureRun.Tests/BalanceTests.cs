using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TreasureRun.Balance;

namespace TreasureRun.Tests
{
    /// <summary>The balance framework's sanity checks (docs/balance-framework.md section 8) and an offline report preview.</summary>
    public class BalanceTests
    {
        private static string Root => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));

        private static Rating Rate(Spec s) => Valuation.Rate("t", "t", "test", null, false, s);

        [Test]
        public void WaveModelFollowsTheVanillaBudget()
        {
            WaveStats w1 = WaveModel.Get(1), w10 = WaveModel.Get(10), w15 = WaveModel.Get(15), w16 = WaveModel.Get(16);
            Assert.AreEqual(1, w1.Count, 1e-9, "wave 1 spends its budget of 1 on one goblin");
            Assert.AreEqual(100, w1.Health, 1e-9);
            Assert.That(w10.SpawnEvents, Is.InRange(15, 60), "wave 10: budget 100 over tier-1 monsters of cost 1-13");
            Assert.That(w15.BossHealth, Is.EqualTo(20000), "wave 15 boss");
            Assert.That(w16.Shield, Is.GreaterThan(0), "tier 2 (with shields) from wave 16");
            Assert.That(WaveModel.Get(45).Total, Is.GreaterThan(WaveModel.Get(35).Total * 2));
        }

        [Test]
        public void MonsterCardsComeOutNearTheirStatedPercent()
        {
            // +5% health, armor and shield: ln(1.05) = 4.9 points less, minus what overkill and the DOT caps absorb.
            Rating r = Rate(new Spec().MonsterLayers(0.05, 0.05, 0.05));
            Assert.That(r.Points, Is.InRange(-5.5, -3.0), r.Points.ToString());
        }

        [Test]
        public void TwoCopiesOfAPercentEffectAreWorthTwiceOne()
        {
            double one = Rate(new Spec().DamagePercent(0.1)).Points;
            double two = Rate(new Spec().DamagePercent(0.1).DamagePercent(0.1)).Points;
            Assert.That(two / one, Is.InRange(1.85, 2.15), $"{one} vs {two}");
            Assert.That(one, Is.InRange(8.5, 12.5), "+10% damage is about 9.5 points");
        }

        [Test]
        public void NoTowersHoldNothing()
        {
            Board b = ReferenceBoards.Get(20);
            b.Groups.Clear();
            Assert.AreEqual(0, Evaluator.Raw(b).RawStream, 1e-9);
        }

        [Test]
        public void FlatEffectsFallAndScalingEffectsRise()
        {
            Rating flat = Rate(new Spec().BaseDamage(1)), gold = Rate(new Spec().GoldNow(500)), scaling = Rate(new Spec().DamagePerTreasure(0.04));
            Assert.That(flat.Early, Is.GreaterThan(flat.Late));
            Assert.That(gold.Early, Is.GreaterThan(5 * gold.Late));
            Assert.That(scaling.Late, Is.GreaterThan(scaling.Early));
            Assert.That(gold.TimingBreadth, Is.LessThan(0.8), "a peaky item is penalised");
        }

        [Test]
        public void SituationalItemsArePenalisedByBreadth()
        {
            Rating broad = Rate(new Spec().Conditional(3, Condition.Frozen).GivenFreeze());
            Rating narrow = Rate(new Spec().Conditional(3, Condition.Frozen).GivenFreeze().Situational(BoardVariant.Mixed, 0.2));
            Assert.AreEqual(Math.Pow(0.2, 0.3), narrow.Points / broad.Points, 0.01);
        }

        [Test]
        public void RequirementsCostNothing()
        {
            Rating r = Rate(new Spec().DamagePercent(0.5, TowerKind.Shredder).Requires(TowerKind.Shredder));
            Assert.That(r.Points, Is.GreaterThan(1), "the board is given a Shredder");
            Assert.AreEqual(1, r.Breadth);
        }

        [Test]
        public void EveryTreasureHasASpec()
        {
            string doc = File.ReadAllText(Path.Combine(Root, "docs", "treasures.md"));
            var ids = Regex.Matches(doc, @"\(`([a-z_]+)`\)").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.That(ids.Count, Is.GreaterThan(80));
            var missing = ids.Where(id => ContentSpecs.Treasure(id) == null).ToList();
            Assert.IsEmpty(missing, "treasures without a balance spec: " + string.Join(", ", missing));
        }

        [Test]
        public void VanillaCardsTranslate()
        {
            Spec s = VanillaCards.ToSpec("TowerUpgradeCard", "Longbow I", new Dictionary<string, double> { ["towerType"] = 0, ["range"] = 1 });
            Assert.AreEqual(TowerKind.Crossbow, s.RequiredTower);
            Assert.That(Rate(s).Points, Is.GreaterThan(0));
            Spec m = VanillaCards.ToSpec("MonsterUpgradeCard", "Monster Health I", new Dictionary<string, double> { ["extraHealth"] = 0.05f });
            Assert.That(Rate(m).Points, Is.LessThan(0));
            Spec u = VanillaCards.ToSpec("TowerUnlockCard", "Tower - Obelisk", new Dictionary<string, double>());
            Assert.That(Rate(u).Points, Is.GreaterThanOrEqualTo(0), "an unlock is an option, never a loss");
        }

        /// <summary>Offline preview of the report: treasures (rarity from docs/treasures.md), custom cards and towers.</summary>
        [Test]
        public void WritesReportPreview()
        {
            string doc = File.ReadAllText(Path.Combine(Root, "docs", "treasures.md"));
            var items = new List<ReportItem>();
            Rarity current = Rarity.Common;
            foreach (string line in doc.Split('\n'))
            {
                var h = Regex.Match(line, @"^## (\w+)");
                if (h.Success && Enum.TryParse(h.Groups[1].Value, out Rarity rr)) current = rr;
                var m = Regex.Match(line, @"^\| \*\*(.+?)\*\* \(`([a-z_]+)`\)");
                if (m.Success)
                {
                    bool asc = line.TrimEnd().EndsWith("| Ascension |");
                    items.Add(new ReportItem
                    {
                        Id = m.Groups[2].Value, Name = m.Groups[1].Value, Kind = "treasure", Group = asc ? "Treasures (ascension)" : "Treasures",
                        Rarity = current, IsAscension = asc, Spec = ContentSpecs.Treasure(m.Groups[2].Value),
                    });
                }
            }
            foreach (var kv in ContentSpecs.Cards)
                items.Add(new ReportItem { Id = kv.Key, Name = kv.Key, Kind = "card", Group = "Custom cards", IsCard = true, Spec = kv.Value(1) });
            items.AddRange(Report.Towers());
            List<Rating> ratings = Report.RateAll(items);
            string dir = Path.Combine(Root, "tests", "out");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "balance-preview.md"), Report.Markdown(ratings, "Offline preview (`dotnet test`): treasures, custom cards (tier I) and towers. The full report with vanilla cards comes from the dev command `balance report`."));
            File.WriteAllText(Path.Combine(dir, "balance-preview.csv"), Report.Csv(ratings));
            Assert.That(ratings.Count(r => r.Source == "MISSING SPEC"), Is.EqualTo(0));
        }
    }
}
