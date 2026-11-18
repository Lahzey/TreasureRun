using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TreasureRun.Balance
{
    /// <summary>One item to rate: a treasure, a card tier or a tower.</summary>
    public sealed class ReportItem
    {
        public string Id, Name, Kind, Group, Description;
        public Rarity? Rarity;
        public bool IsCard, IsAscension;
        /// <summary>Null = missing spec (a coverage failure).</summary>
        public Spec Spec;
    }

    /// <summary>The balance report (section 8): markdown for reading, CSV for sorting.</summary>
    public static class Report
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static List<Rating> RateAll(IEnumerable<ReportItem> items)
        {
            var list = new List<Rating>();
            foreach (ReportItem it in items)
            {
                Rating r = it.Spec == null
                    ? new Rating { Id = it.Id, Name = it.Name, Kind = it.Kind, Rarity = it.Rarity, IsCard = it.IsCard, Unrated = true, Source = "MISSING SPEC" }
                    : Valuation.Rate(it.Id, it.Name, it.Kind, it.Rarity, it.IsCard, it.Spec, it.IsAscension);
                r.Group = it.Group;
                list.Add(r);
            }
            return list;
        }

        /// <summary>Spec for a base tower: what 1000 gold more of it is worth (reported per 100 gold).</summary>
        public static Spec TowerSpec(TowerKind kind) => new Spec()
            .Custom(Category.Offense, "1000 gold of " + TowerData.Get(kind).Name, c => Valuation.AddTowerGold(c.Board, kind, 1000))
            .Because("points for 1000 gold more of this tower; the table shows it per 100 gold");

        public static IEnumerable<ReportItem> Towers() =>
            TowerData.All.Select(t => new ReportItem { Id = t.Kind.ToString(), Name = t.Name, Kind = "tower", Group = "Towers", Spec = TowerSpec(t.Kind) });

        public static string Markdown(List<Rating> ratings, string intro)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Balance report").AppendLine();
            sb.AppendLine(intro).AppendLine();
            sb.AppendLine("How to read it (docs/balance-framework.md): **points** ≈ % tougher monsters the item lets the board hold, at a good moment for it " +
                          "(the 80th percentile of its value curve over the waves it can be offered), × (situation breadth × timing breadth)^0.3. " +
                          "**Early / late** are the value if taken at the first offer wave / at wave 40, **peak** the best wave. " +
                          "**Flag** marks items outside ±25% of their rarity budget (cards: outside 2–6). " +
                          "Source says where the number comes from: *formula*, *estimated* (hand estimate) or a proxy marked for the M8 simulation.").AppendLine();
            sb.AppendLine("Budgets: cards 2–6, Common 12, Uncommon 18, Rare 24, Epic 30, Legendary 36, Mythical 54; ascensions 25% more than their rarity. " +
                          "All constants are uncalibrated until M8. The treasures are mostly placeholders: no balance changes until M9.").AppendLine();

            int missing = ratings.Count(r => r.Source == "MISSING SPEC");
            var flagged = ratings.Where(r => r.OverBudget || r.UnderBudget).ToList();
            sb.AppendLine($"**Summary:** {ratings.Count} items, {flagged.Count(r => r.OverBudget)} over budget, {flagged.Count(r => r.UnderBudget)} under, " +
                          $"{ratings.Count(r => r.Unrated && r.Source != "MISSING SPEC")} unrated, {missing} missing specs.").AppendLine();

            foreach (var group in ratings.GroupBy(r => r.Group))
            {
                sb.AppendLine($"## {group.Key}").AppendLine();
                if (group.Key == "Towers")
                {
                    sb.AppendLine("| Tower | Points per 100 gold | Early | Late | Peak wave |").AppendLine("|---|---|---|---|---|");
                    foreach (Rating r in group.OrderByDescending(r => r.Points))
                        sb.AppendLine($"| {r.Name} | {N(r.Points / 10)} | {N(r.Early / 10)} | {N(r.Late / 10)} | {r.PeakWave} |");
                    sb.AppendLine();
                    continue;
                }
                sb.AppendLine("| Item | Rarity | Points | Budget | Flag | Early / late / peak | b · b_t | Situation | Split (off/ctl/eco/surv/meta) | Source | Note |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (Rating r in group.OrderBy(r => r.Rarity ?? (Rarity)(-1)).ThenByDescending(r => r.Points))
                {
                    string rarity = r.IsCard ? "card" : r.Rarity?.ToString() ?? "";
                    string budget = r.IsCard ? "2–6" : r.Rarity != null ? N(r.Budget) : "";
                    string flag = r.Flag == "over" ? "**over**" : r.Flag == "under" ? "under" : r.Flag;
                    string curve = r.Unrated ? "" : $"{N(r.Early)} / {N(r.Late)} / w{r.PeakWave}";
                    string breadth = r.Unrated ? "" : $"{N2(r.Breadth)} · {N2(r.TimingBreadth)}";
                    string situation = r.Variant != BoardVariant.Mixed || r.Breadth < 1 ? r.Variant.ToString() : "";
                    if (r.Requirement != null) situation = (situation.Length > 0 ? situation + ", " : "") + "needs " + r.Requirement;
                    string split = r.Unrated ? "" : string.Join("/", r.Split.Select(x => ((int)Math.Round(100 * x)).ToString(Inv)));
                    sb.AppendLine($"| **{Esc(r.Name)}** (`{r.Id}`) | {rarity} | {(r.Unrated ? "–" : N(r.Points))} | {budget} | {flag} | {curve} | {breadth} | {Esc(situation)} | {split} | {r.Source} | {Esc(r.Note ?? "")} |");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string Csv(List<Rating> ratings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("group,id,name,kind,rarity,points,p80,budget,flag,early,late,peak,breadth,timing_breadth,variant,offense,control,economy,survival,meta,source,requirement,note");
            foreach (Rating r in ratings)
                sb.AppendLine(string.Join(",", new[]
                {
                    Q(r.Group), Q(r.Id), Q(r.Name), Q(r.Kind), Q(r.IsCard ? "card" : r.Rarity?.ToString() ?? ""), N(r.Points), N(r.P80), N(r.Budget), Q(r.Flag),
                    N(r.Early), N(r.Late), r.PeakWave.ToString(Inv), N2(r.Breadth), N2(r.TimingBreadth), Q(r.Variant.ToString()),
                    N2(r.Split[0]), N2(r.Split[1]), N2(r.Split[2]), N2(r.Split[3]), N2(r.Split[4]), Q(r.Source), Q(r.Requirement ?? ""), Q(r.Note ?? ""),
                }));
            return sb.ToString();
        }

        /// <summary>Console text for one rating: points, curve and breakdown.</summary>
        public static string Detail(Rating r, Spec s)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{r.Name} ({r.Id}) - {(r.IsCard ? "card" : r.Rarity?.ToString() ?? r.Kind)}");
            if (r.Unrated) { sb.AppendLine("  unrated: " + (r.Note ?? r.Source)); return sb.ToString(); }
            sb.AppendLine($"  points {N(r.Points)} (budget {(r.IsCard ? "2-6" : N(r.Budget))}{(r.Flag != "" ? ", " + r.Flag : "")}), source {r.Source}");
            sb.AppendLine($"  P80 {N(r.P80)} x (b {N2(r.Breadth)} x b_t {N2(r.TimingBreadth)})^0.3 = x{N2(r.Factor)}; early {N(r.Early)}, late {N(r.Late)}, peak wave {r.PeakWave}");
            if (r.Variant != BoardVariant.Mixed) sb.AppendLine($"  judged on the {r.Variant} board");
            if (r.Requirement != null) sb.AppendLine("  needs " + r.Requirement);
            foreach (var g in s.Setups) sb.AppendLine("  given " + g.Text);
            sb.AppendLine("  atoms: " + string.Join("; ", s.Atoms.Where(a => a.Text.Length > 0).Select(a => a.Text)));
            sb.AppendLine("  split off/ctl/eco/surv/meta: " + string.Join("/", r.Split.Select(x => ((int)Math.Round(100 * x)).ToString(Inv))));
            if (r.Curve.Length > 0)
                sb.AppendLine("  curve V(a): " + string.Join(" ", r.Curve.Select((v, i) => (r.OfferFrom + i) % 5 == 0 || i == 0 ? $"w{r.OfferFrom + i}:{N(v)}" : null).Where(x => x != null)));
            if (r.Note != null) sb.AppendLine("  note: " + r.Note);
            return sb.ToString();
        }

        private static string N(double v) => double.IsInfinity(v) || double.IsNaN(v) ? "" : v.ToString("0.0", Inv);
        private static string N2(double v) => v.ToString("0.00", Inv);
        private static string Esc(string s) => s.Replace("|", "/");
        private static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    }
}
