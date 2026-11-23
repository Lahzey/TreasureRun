using System;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;

namespace TreasureRun.Pois
{
    /// <summary>
    /// A Point of Interest (docs/poi-plan.md): a whole map section, generated for any combination of exits, with its own
    /// function. It is announced by an omen (its icon on an Expand button) and placed when that button is used.
    /// </summary>
    public sealed class PoiDef
    {
        public string Id, Name, Description;
        /// <summary>Icon name in the icons folder (rendered from tools/IconTool/Icons).</summary>
        public string Icon;
        public float Weight = 1f;
        /// <summary>First wave an omen of it can appear.</summary>
        public int MinWave = 1;
        public Func<RunState, bool> IsAvailable;
        /// <summary>How the POI shapes its section (path hub, reserved cells, terrain).</summary>
        public Func<SectionStyle> Style = () => new SectionStyle();
        /// <summary>Adds the POI's behaviour to the built section (the tile root).</summary>
        public Func<UnityEngine.GameObject, PoiSite> Attach;
    }

    public static class PoiRegistry
    {
        private static readonly List<PoiDef> all = new List<PoiDef>();

        public static IReadOnlyList<PoiDef> All
        {
            get { EnsureRegistered(); return all; }
        }

        public static PoiDef Get(string id) => All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

        public static PoiDef Find(string query) =>
            Get(query) ?? All.FirstOrDefault(d => d.Name.IndexOf(query ?? "", StringComparison.OrdinalIgnoreCase) >= 0);

        private static void EnsureRegistered()
        {
            if (all.Count > 0) return;
            all.Add(LightningTrap.Create());
            all.Add(Merchant.Create(MerchantKind.Normal));
            all.Add(Merchant.Create(MerchantKind.Legendary));
            all.Add(Merchant.Create(MerchantKind.Ascension));
            all.Add(SoulUrn.Create());
            all.Add(WizardTower.Create());
        }

        /// <summary>POIs whose omen can appear at a wave.</summary>
        public static List<PoiDef> Available(int wave, RunState run) =>
            All.Where(d => wave >= d.MinWave && (d.IsAvailable == null || d.IsAvailable(run))).ToList();
    }
}
