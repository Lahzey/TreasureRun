using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TreasureRun.Cards;
using TreasureRun.Treasures;
using UnityEngine;
using BRarity = TreasureRun.Balance.Rarity;

namespace TreasureRun.Balance.Game
{
    /// <summary>
    /// The in-game side of the balance framework (docs/balance-framework.md, step B3): gathers the live content
    /// (treasures, custom and vanilla cards, towers), rates it with the pure model and serves the dev command
    /// <c>balance</c> and the offer overlay.
    /// </summary>
    internal static class BalanceGame
    {
        private static readonly Dictionary<string, Rating> cache = new Dictionary<string, Rating>();

        /// <summary>Every rateable item of the running game.</summary>
        public static List<ReportItem> Items()
        {
            var items = new List<ReportItem>();
            foreach (TreasureDef d in TreasureRegistry.All)
                items.Add(Treasure(d));
            foreach (CustomCardSpec c in CustomCards.All)
                items.Add(new ReportItem
                {
                    Id = c.Id, Name = c.Title, Kind = "card", IsCard = true, Description = c.Description,
                    Group = c.Castle != null ? "Custom cards (castle)" : c.Available != null ? "Custom cards (ascension)" : "Custom cards",
                    Spec = ContentSpecs.Card(c.Id, Tier(c.Title)),
                });
            foreach (Pois.PoiDef p in Pois.PoiRegistry.All)
                items.Add(new ReportItem { Id = p.Id, Name = p.Name, Kind = "poi", Group = "Points of Interest", Description = p.Description, Spec = ContentSpecs.Poi(p.Id) });
            items.AddRange(VanillaCards());
            items.AddRange(Report.Towers());
            return items;
        }

        public static ReportItem Treasure(TreasureDef d) => new ReportItem
        {
            Id = d.Id, Name = d.Name, Kind = "treasure", Rarity = (BRarity)(int)d.Rarity, Description = d.Description,
            IsAscension = d.Module == "Ascension",
            Group = d.Castle != null ? "Treasures (castle)" : d.Module == "Ascension" ? "Treasures (ascension)" : "Treasures",
            Spec = ContentSpecs.Treasure(d.Id),
        };

        /// <summary>Vanilla cards in the scene, one per title (copies share their fields).</summary>
        public static IEnumerable<ReportItem> VanillaCards()
        {
            var seen = new HashSet<string>();
            foreach (UpgradeCard c in Resources.FindObjectsOfTypeAll<UpgradeCard>()
                         .Where(c => c.gameObject.scene.IsValid() && !(c is CustomCard))
                         .OrderBy(c => CardPool.TitleOf(c)))
            {
                string title = CardPool.TitleOf(c), cls = c.GetType().Name;
                if (title.Length == 0 || !seen.Add(cls + "|" + title)) continue;
                yield return Vanilla(c, CardPool.Whitelisted(c));
            }
        }

        public static ReportItem Vanilla(UpgradeCard c, bool inPool)
        {
            string title = CardPool.TitleOf(c), cls = c.GetType().Name;
            return new ReportItem
            {
                Id = title, Name = title, Kind = cls, IsCard = true, Description = c.description,
                Group = inPool ? "Vanilla cards (Treasure Run pool)" : "Vanilla cards (normal mode only)",
                Spec = Balance.VanillaCards.ToSpec(cls, title, Fields(c)),
            };
        }

        /// <summary>The serialized numbers of a card (int, float and bool fields of its own class).</summary>
        public static Dictionary<string, double> Fields(UpgradeCard c)
        {
            var d = new Dictionary<string, double>();
            for (Type t = c.GetType(); t != null && t != typeof(UpgradeCard); t = t.BaseType)
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    object v = f.GetValue(c);
                    if (v is int i) d[f.Name] = i;
                    else if (v is float x) d[f.Name] = x;
                    else if (v is bool b) d[f.Name] = b ? 1 : 0;
                    else if (v is Enum e) d[f.Name] = Convert.ToInt32(e);
                }
            return d;
        }

        public static Rating Rate(ReportItem it)
        {
            string key = it.Kind + "|" + it.Id + "|" + it.Name;
            lock (cache)
                if (cache.TryGetValue(key, out Rating r)) return r;
            Rating rating = Report.RateAll(new[] { it })[0];
            lock (cache) cache[key] = rating;
            return rating;
        }

        private static int Tier(string title)
        {
            string[] roman = { "I", "II", "III", "IV", "V" };
            string last = title.Split(' ').Last();
            int i = Array.IndexOf(roman, last);
            return i >= 0 ? i + 1 : 1;
        }

        // ------------------------------------------------------------------ dev command

        public static string Command(string[] a, string dir)
        {
            string sub = a.Length > 0 ? a[0].ToLowerInvariant() : "";
            switch (sub)
            {
                case "report": return WriteReport(dir);
                case "coverage": return Coverage();
                case "pathstats": return PathStatsText();
                case "":
                    return "balance <id|title> | balance report | balance coverage | balance pathstats";
                default:
                    string q = string.Join(" ", a);
                    List<ReportItem> items = Items();
                    ReportItem it = items.FirstOrDefault(x => string.Equals(x.Id, q, StringComparison.OrdinalIgnoreCase))
                                    ?? items.FirstOrDefault(x => string.Equals(x.Name, q, StringComparison.OrdinalIgnoreCase))
                                    ?? items.FirstOrDefault(x => x.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (it == null) return $"No item matches '{q}'.";
                    if (it.Spec == null) return $"{it.Name} ({it.Id}): MISSING SPEC";
                    return Report.Detail(Rate(it), it.Spec);
            }
        }

        public static string Coverage()
        {
            List<ReportItem> missing = Items().Where(i => i.Spec == null).ToList();
            return missing.Count == 0
                ? "Every treasure, card and Point of Interest has a balance spec."
                : $"{missing.Count} without a spec: " + string.Join(", ", missing.Select(m => $"{m.Name} ({m.Kind} {m.Id})"));
        }

        private static string WriteReport(string dir)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            List<Rating> ratings = Report.RateAll(Items());
            string intro = "Generated by the dev command `balance report` from the running game (all treasures, custom and vanilla cards, Points of Interest, towers). " +
                           "Copy `console/balance.md` to `docs/balance-report.md` after balance changes.";
            File.WriteAllText(Path.Combine(dir, "balance.md"), Report.Markdown(ratings, intro));
            File.WriteAllText(Path.Combine(dir, "balance.csv"), Report.Csv(ratings));
            int flagged = ratings.Count(r => r.OverBudget || r.UnderBudget), missing = ratings.Count(r => r.Source == "MISSING SPEC");
            return $"Wrote {Path.Combine(dir, "balance.md")} and balance.csv: {ratings.Count} items, {flagged} flagged, {missing} missing specs ({sw.ElapsedMilliseconds} ms).";
        }

        // ------------------------------------------------------------------ path statistics (B0)

        /// <summary>
        /// Measures the map's path statistics: path length, and for every free grass cell within 1.5 of the path the path
        /// length within range R, as percentiles over the cells. Compare with <see cref="PathStats"/>.
        /// </summary>
        public static string PathStatsText()
        {
            var points = PathPoints(0.25f);
            if (points.Count == 0) return "No path found.";
            var cells = new List<(int, int)>();
            var seen = new HashSet<(int, int)>();
            foreach (Vector3 p in points)
                for (int dx = -2; dx <= 2; dx++)
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        var cell = (Mathf.RoundToInt(p.x) + dx, Mathf.RoundToInt(p.z) + dz);
                        if (!seen.Add(cell)) continue;
                        float dist = points.Min(q => Vector2.Distance(new Vector2(q.x, q.z), new Vector2(cell.Item1, cell.Item2)));
                        if (dist < 0.9f || dist > 1.6f) continue; // next to the path, not on it
                        if (Debugging.RitualScenarios.Grass(cell.Item1, cell.Item2) == null) continue;
                        cells.Add(cell);
                    }
            float length = points.Count * 0.25f;
            var sb = new StringBuilder();
            sb.AppendLine($"Path length {length:0.0} (wave {SpawnManager.instance?.level}; model {PathStats.PathLength(SpawnManager.instance?.level ?? 1):0.0}), {cells.Count} cells next to it.");
            sb.AppendLine("Range | path within range p10 / p50 / p90 | model p10 / p50 / p90");
            foreach (float r in new[] { 2f, 3f, 4f, 5f, 7f, 10f })
            {
                var cover = cells.Select(c => points.Count(q => Vector2.Distance(new Vector2(q.x, q.z), new Vector2(c.Item1, c.Item2)) <= r) * 0.25f)
                                 .OrderBy(x => x).ToList();
                if (cover.Count == 0) continue;
                float P(float p) => cover[Mathf.Clamp(Mathf.RoundToInt(p * (cover.Count - 1)), 0, cover.Count - 1)];
                sb.AppendLine($"{r,5} | {P(0.1f):0.0} / {P(0.5f):0.0} / {P(0.9f):0.0} | " +
                              $"{PathStats.Coverage(r, 0.1, length):0.0} / {PathStats.Coverage(r, 0.5, length):0.0} / {PathStats.Coverage(r, 0.9, length):0.0}");
            }
            return sb.ToString();
        }

        /// <summary>Points every <paramref name="step"/> along every path segment (waypoint to next waypoint).</summary>
        internal static List<Vector3> PathPoints(float step)
        {
            var pts = new List<Vector3>();
            foreach (Waypoint w in UnityEngine.Object.FindObjectsOfType<Waypoint>())
            {
                Waypoint next = w.next;
                if (next == null) continue;
                Vector3 a = w.transform.position, b = next.transform.position;
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / step));
                for (int i = 0; i < n; i++) pts.Add(Vector3.Lerp(a, b, i / (float)n));
            }
            return pts;
        }
    }
}
