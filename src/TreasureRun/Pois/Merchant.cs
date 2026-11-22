using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Wandering Merchant (docs/poi-plan.md §5): a wagon parked beside the path, with three offers (one of them discounted)
    /// and a counter that buys your treasures back at half price. Three kinds: Normal (any treasure), Legendary (Legendary
    /// treasures only) and Ascension (ascensions only). The Normal and Legendary merchants pack up once every offer is
    /// bought, the Ascension Merchant after the first purchase; the wagon then shutters its windows and stays put, still
    /// blocking its cells. Prices follow <see cref="MerchantLogic"/> at the current wave.
    /// </summary>
    public sealed class Merchant : PoiSite
    {
        public const string SoundOpen = "pois/merchant/shop_open", SoundBuy = "pois/merchant/register", SoundSell = "pois/merchant/register",
                            SoundShutter = "pois/merchant/shutter";

        public MerchantKind Kind { get; private set; }

        public static string IdOf(MerchantKind kind) =>
            kind == MerchantKind.Legendary ? "legendary_merchant" : kind == MerchantKind.Ascension ? "ascension_merchant" : "merchant";

        public static string NameOf(MerchantKind kind) =>
            kind == MerchantKind.Legendary ? "Legendary Merchant" : kind == MerchantKind.Ascension ? "Ascension Merchant" : "Wandering Merchant";

        /// <summary>Model folder of the kind: pois/merchant/&lt;skin&gt;_wagon, _awning, _shutter.</summary>
        public static string SkinOf(MerchantKind kind) =>
            kind == MerchantKind.Legendary ? "legendary" : kind == MerchantKind.Ascension ? "ascension" : "normal";

        public static PoiDef Create(MerchantKind kind)
        {
            string what = kind == MerchantKind.Legendary ? "three Legendary treasures" :
                          kind == MerchantKind.Ascension ? "three ascensions" : "three treasures";
            string leaves = kind == MerchantKind.Ascension ? "It leaves after your first purchase." : "It leaves once everything is bought.";
            return new PoiDef
            {
                Id = IdOf(kind),
                Name = NameOf(kind),
                Icon = "poi/" + IdOf(kind),
                MinWave = MerchantLogic.MinWave(kind),
                Weight = kind == MerchantKind.Normal ? 1f : kind == MerchantKind.Legendary ? 0.25f : 0.3f,
                Description = $"A merchant's wagon parked by the path, selling {what} (one at a discount) and buying your treasures " +
                              $"for half their price. {leaves}",
                IsAvailable = run => MerchantStock.Candidates(kind, new List<TreasureDef>()).Count >= (kind == MerchantKind.Normal ? 3 : 2),
                Style = () => new MerchantStyle(),
                Attach = go =>
                {
                    Merchant m = go.AddComponent<Merchant>();
                    m.Kind = kind;
                    return m;
                },
            };
        }

        // ------------------------------------------------------------------ state (Record)

        /// <summary>The offered treasures (Record.Items), in slot order.</summary>
        public List<TreasureDef> Stock => Record.Items.Select(TreasureRegistry.Get).ToList();
        public int DiscountSlot => Mathf.RoundToInt(Record.Get("discount"));
        public bool Bought(int slot) => Record.Get("bought" + slot) > 0f;
        public bool Closed => Record.Get("closed") > 0f;
        public int Wave => Mathf.Max(1, GameHelpers.Level);

        public static bool IsAscension(TreasureDef t) => AscensionRegistry.ForTreasure(t) != null;

        public int PriceOf(int slot)
        {
            TreasureDef t = Stock[slot];
            return MerchantLogic.Price(Kind, (int)t.Rarity, Wave, IsAscension(t), slot == DiscountSlot);
        }

        public int FullPriceOf(int slot)
        {
            TreasureDef t = Stock[slot];
            return MerchantLogic.Price(Kind, (int)t.Rarity, Wave, IsAscension(t), false);
        }

        public int SellPriceOf(TreasureDef t) => MerchantLogic.SellPrice(Kind, (int)t.Rarity, Wave, IsAscension(t));

        /// <summary>Why an offer can't be bought right now (null: it can, gold aside).</summary>
        public string Blocked(int slot)
        {
            if (Closed) return "Closed";
            if (Bought(slot)) return "Sold";
            TreasureDef t = Stock[slot];
            if (t == null) return "Gone";
            RunState run = TreasureRunMode.Run;
            if (t.Unique && run.OwnedTreasures.Contains(t.Id)) return "Already owned";
            if (t.ExclusionGroup != null && run.OwnedTreasures.Select(TreasureRegistry.Get).Any(o => o != null && o.ExclusionGroup == t.ExclusionGroup))
                return IsAscension(t) ? "Tower already ascended" : "Excluded by a treasure you own";
            if (!Cards.HealthCost.CanPay(Cards.HealthCost.Of(t))) return "Not enough health";
            return null;
        }

        /// <summary>Offers that can still be bought (not sold and not blocked).</summary>
        public int OpenOffers => Enumerable.Range(0, Stock.Count).Count(i => Blocked(i) == null);

        public string Rule => MerchantLogic.Rule(Kind, OpenOffers);

        public override string Status =>
            $"{Kind}, {(Closed ? "closed" : "open")}, stock " +
            string.Join(", ", Stock.Select((t, i) => $"{t?.Id}{(i == DiscountSlot ? " (-40%)" : "")}{(Bought(i) ? " SOLD" : "")} {PriceOf(i)}g")) +
            $", spent {Record.Get("spent"):0}, earned {Record.Get("earned"):0}";

        // ------------------------------------------------------------------ placement

        private Transform wagon, awning;
        private readonly List<(Transform pivot, float open, float closed)> shutters = new List<(Transform, float, float)>();
        private const float AwningOpen = -35f, AwningClosed = 90f; // open: propped up like a hatch, so the window shows

        protected override void OnPlaced()
        {
            if (Record.Items.Count == 0) RollStock();
            BuildWagon();
            SetShutters(Closed ? 1f : 0f);
        }

        private void RollStock()
        {
            List<TreasureDef> stock = MerchantStock.Roll(Kind, GameHelpers.Level);
            Record.Items.AddRange(stock.Select(t => t.Id));
            Record.Set("discount", stock.Count > 0 ? Luck.RawRange(0, stock.Count) : 0);
            Plugin.Log.LogInfo($"{Def.Name}: {string.Join(", ", stock.Select(t => $"{t.Name} ({t.Rarity})"))}, discount on slot {DiscountSlot}.");
        }

        /// <summary>
        /// The wagon model (20 x 20 voxels = the 2 x 2 footprint, serving side towards model -x), always turned so its opening
        /// faces south-west on screen (world +z; its front end with the end window then faces south-east), plus its awning and
        /// the two shutter leaves of its end window on hinges, and a solid box over the footprint (blocks building; a click
        /// opens the shop).
        /// </summary>
        private void BuildWagon()
        {
            Vector3 centre = FootprintCentre;
            // Model -x serves: a turn of 90 degrees around y points it to world +z.
            Quaternion rot = Quaternion.Euler(0f, 90f, 0f);

            var root = new GameObject("TR_MerchantWagon") { layer = LayerMask.NameToLayer("Building") };
            wagon = root.transform;
            wagon.SetParent(transform, false);
            wagon.position = centre;
            wagon.rotation = rot;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.95f, 0f);
            box.size = new Vector3(2f, 1.9f, 2f);
            PoiPopup.AddClick(root, OnClicked);

            string skin = SkinOf(Kind);
            if (VoxelLibrary.Spawn($"pois/merchant/{skin}_wagon", centre, rot, wagon) == null)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cube.GetComponent<Collider>());
                cube.transform.SetParent(wagon, false);
                cube.transform.localPosition = new Vector3(0.2f, 0.7f, 0f);
                cube.transform.localScale = new Vector3(1.2f, 1.2f, 1.8f);
            }
            // Awning: hinged along the top of the serving window (voxel x = 6, z = 15), hanging towards -x.
            awning = Hinge($"pois/merchant/{skin}_awning", V(6f, 15f, 10f), hingeAtMaxX: true, out _);
            // End window shutters (front end, voxel y = 3 face, window x 9..15): leaves hinged at both sides, folding outwards.
            Transform left = Hinge($"pois/merchant/{skin}_shutter", V(9f, 9f, 3f), hingeAtMaxX: false, out _);
            Transform right = Hinge($"pois/merchant/{skin}_shutter", V(15f, 9f, 3f), hingeAtMaxX: true, out _);
            if (left != null) shutters.Add((left, 165f, 0f));
            if (right != null) shutters.Add((right, -165f, 0f));
        }

        /// <summary>World centre of the wagon's 2 x 2 patch, on the grass.</summary>
        public Vector3 FootprintCentre
        {
            get
            {
                Vector3 centre = Vector3.zero;
                foreach (var (x, z) in MerchantStyle.Footprint(MerchantStyle.CornerOf(Layout))) centre += CellWorld(x, z, 1f);
                return centre / 4f;
            }
        }

        /// <summary>Wagon-local position of a voxel corner (x across, z up, y along; the model is centred on its 20 x 20 base).</summary>
        private static Vector3 V(float x, float z, float y) => new Vector3((x - 10f) * 0.1f, z * 0.1f, (y - 10f) * 0.1f);

        /// <summary>
        /// A model on a pivot at a hinge line: the model's top edge at max or min x sits on the pivot (awning: top, +x edge;
        /// shutters: the outer face's x edge at the window bottom).
        /// </summary>
        private Transform Hinge(string model, Vector3 local, bool hingeAtMaxX, out GameObject spawned)
        {
            spawned = null;
            VoxAsset asset = VoxelLibrary.Get(model);
            if (asset == null) return null;
            var pivot = new GameObject("TR_Hinge").transform;
            pivot.SetParent(wagon, false);
            pivot.localPosition = local;
            spawned = VoxelLibrary.Spawn(model, Vector3.zero, Quaternion.identity, pivot);
            Bounds b = asset.Mesh.bounds;
            bool awningModel = model.EndsWith("_awning");
            // Awning: pivot at its +x top edge. Shutter: pivot at the bottom of its hinge edge, on the side facing the wagon (max z:
            // the leaf lies outside the front end, towards -z).
            spawned.transform.localPosition = awningModel
                ? new Vector3(-b.max.x, -b.max.y, -b.center.z)
                : new Vector3(hingeAtMaxX ? -b.max.x : -b.min.x, -b.min.y, -b.max.z);
            spawned.transform.localRotation = Quaternion.identity;
            return pivot;
        }

        /// <summary>0 = open, 1 = shut: the awning swings down over the window, the shutter leaves fold in.</summary>
        private void SetShutters(float t)
        {
            if (awning != null) awning.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(AwningOpen, AwningClosed, t));
            foreach (var (pivot, open, closed) in shutters)
                pivot.localRotation = Quaternion.Euler(0f, Mathf.LerpUnclamped(open, closed, Mathf.Clamp01(t * 1.6f)), 0f);
        }

        private IEnumerator ShutterAnimation()
        {
            // A UI sound (no position), like the shop's other sounds: the same volume wherever the camera is. Volume 1.75 on
            // two sources: +5.8 dB over the shop's other sounds (0.9), plus 4 dB of soft limiting in its recipe.
            SoundLibrary.PlayUi(SoundShutter, 1.75f);
            const float length = 1.1f;
            for (float time = 0f; time < length; time += Time.deltaTime)
            {
                float u = time / length;
                // Ease in (gravity), then a small bounce as the awning hits the frame.
                float t = u < 0.75f ? Mathf.Pow(u / 0.75f, 2f) : 1f - Mathf.Sin((u - 0.75f) / 0.25f * Mathf.PI) * 0.06f;
                SetShutters(t);
                yield return null;
            }
            SetShutters(1f);
        }

        // ------------------------------------------------------------------ shop

        private void OnClicked()
        {
            if (Closed)
            {
                GameHelpers.FloatingText(wagon.position + Vector3.up * 2f, "Closed", "Red");
                return;
            }
            MerchantShopUI.Open(this);
        }

        /// <summary>Buys an offer: pays, grants the treasure, and packs up if it was the last (or the first, for ascensions).</summary>
        public bool Buy(int slot)
        {
            if (slot < 0 || slot >= Stock.Count || Blocked(slot) != null) return false;
            int price = PriceOf(slot);
            ResourceManager rm = ResourceManager.instance;
            if (rm == null || !rm.CheckMoney(price)) return false;
            rm.Spend(price);
            Record.Set("bought" + slot, 1f);
            Record.Set("spent", Record.Get("spent") + price);
            TreasureInventory.Grant(Stock[slot]);
            SoundLibrary.PlayUi(SoundBuy, 0.9f);
            Plugin.Log.LogInfo($"{Def.Name}: bought {Stock[slot].Name} for {price} gold.");
            if (MerchantLogic.LeavesAfterPurchase(Kind, OpenOffers)) Close();
            return true;
        }

        /// <summary>Sells one copy of an owned treasure for half this merchant's price.</summary>
        public bool Sell(TreasureDef t)
        {
            if (Closed || t == null) return false;
            int price = SellPriceOf(t);
            if (!TreasureSale.Sell(t)) return false;
            ResourceManager.instance?.AddMoney(price);
            Record.Set("earned", Record.Get("earned") + price);
            Record.Set("sold", Record.Get("sold") + 1);
            SoundLibrary.PlayUi(SoundSell, 0.9f);
            Plugin.Log.LogInfo($"{Def.Name}: sold {t.Name} for {price} gold.");
            return true;
        }

        /// <summary>The merchant packs up: shutters close, the wagon stays (and keeps blocking its cells).</summary>
        public void Close()
        {
            if (Closed) return;
            Record.Set("closed", 1f);
            if (wagon != null) GameHelpers.FloatingText(wagon.position + Vector3.up * 2.2f, "The merchant packs up", "Yellow", 1f, 1.5f);
            StartCoroutine(ShutterAnimation());
            Plugin.Log.LogInfo($"{Def.Name} closed.");
        }
    }

    /// <summary>What a merchant offers: three distinct treasures that could be offered right now, by kind.</summary>
    public static class MerchantStock
    {
        /// <summary>
        /// All treasures a merchant of this kind could offer now (no duplicates of <paramref name="taken"/>). The Ascension
        /// Merchant may offer several ascensions of the same tower (only one can be bought anyway); the others keep offers
        /// of one exclusion group apart, as treasure offers do.
        /// </summary>
        public static List<TreasureDef> Candidates(MerchantKind kind, List<TreasureDef> taken)
        {
            RunState run = TreasureRunMode.Run;
            if (run == null) return new List<TreasureDef>();
            bool asc = kind == MerchantKind.Ascension;
            var all = new List<TreasureDef>();
            for (Rarity r = Rarity.Common; r <= Rarity.Mythical; r++)
            {
                if (kind == MerchantKind.Legendary && r != Rarity.Legendary) continue;
                if (r == Rarity.Mythical && RarityInfo.Weight(Rarity.Mythical, 1) <= 0f) continue;
                all.AddRange(TreasureDraft.Available(r, run, asc ? new List<TreasureDef>() : taken)
                    .Where(t => Merchant.IsAscension(t) == asc && !taken.Contains(t)));
            }
            return all;
        }

        public static List<TreasureDef> Roll(MerchantKind kind, int level)
        {
            var stock = new List<TreasureDef>();
            for (int i = 0; i < MerchantLogic.Offers; i++)
            {
                TreasureDef pick = kind == MerchantKind.Normal ? RollNormal(level, stock) : RollWeighted(kind, level, stock);
                if (pick == null) break;
                stock.Add(pick);
            }
            return stock;
        }

        /// <summary>Normal: the rarity is rolled like a treasure offer (luck counts, no pity, no Prism), then a treasure of it.</summary>
        private static TreasureDef RollNormal(int level, List<TreasureDef> taken)
        {
            Rarity rolled = Luck.Best(LuckCategory.TreasureRarity, () => TreasureDraft.RollRarity(level, Rarity.Common), (a, b) => a.CompareTo(b));
            List<TreasureDef> all = Candidates(MerchantKind.Normal, taken);
            // The rolled rarity, else the nearest below, else above.
            for (int d = 0; d < RarityInfo.Count; d++)
                foreach (int r in new[] { (int)rolled - d, (int)rolled + d })
                {
                    List<TreasureDef> pool = all.Where(t => (int)t.Rarity == r).ToList();
                    if (pool.Count > 0) return Weighted(pool, t => t.Weight);
                }
            return null;
        }

        /// <summary>Legendary / Ascension: weighted by the rarity weights of the wave and the treasure's own weight.</summary>
        private static TreasureDef RollWeighted(MerchantKind kind, int level, List<TreasureDef> taken)
        {
            List<TreasureDef> pool = Candidates(kind, taken);
            return pool.Count == 0 ? null : Weighted(pool, t => t.Weight * Mathf.Max(0.01f, RarityInfo.Weight(t.Rarity, level)));
        }

        private static TreasureDef Weighted(List<TreasureDef> pool, System.Func<TreasureDef, float> weight)
        {
            float total = pool.Sum(weight), roll = Luck.Raw01() * total;
            foreach (TreasureDef t in pool)
            {
                roll -= weight(t);
                if (roll < 0f) return t;
            }
            return pool[pool.Count - 1];
        }
    }
}
