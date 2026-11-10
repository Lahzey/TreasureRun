using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Audio;
using TreasureRun.Cards;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase1
{
    /// <summary>
    /// Chain Lightning (Tesla Coil, Uncommon): instead of zapping everything around it, the coil strikes its target and
    /// the bolt jumps on to the nearest monster within the coil's range of the last victim, 20 hits per attack
    /// (+5 per More Chains card), never the same monster twice (<see cref="ChainLightningLogic"/>).
    /// </summary>
    internal sealed class ChainLightning : AscendedTower
    {
        public const string Id = "chain_lightning";
        public const string MoreChainsCard = "more_chains";
        private static readonly Color BoltColor = new Color(0.6f, 0.88f, 1f, 1f);
        /// <summary>Height of the focusing crystal above the tower's base.</summary>
        private const float TipHeight = 2.65f;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Chain Lightning", TowerName = "Storm Coil", Rarity = Rarity.Uncommon, Kind = AscensionKind.Replace,
                Target = AscensionTarget.TeslaCoil, Component = typeof(ChainLightning), CardModel = "ascensions/tesla/chain_lightning",
                Description = $"Each attack strikes one monster and chains {ChainLightningLogic.BaseHits} times to the nearest monster " +
                              "within the coil's range of the last one, never hitting the same monster twice. Chains can leave the coil's range.",
            });
        }

        public static void RegisterCards()
        {
            CustomCards.AscensionChain(Id, MoreChainsCard, "More Chains", 4, "Tower - Tesla Coil",
                _ => $"Chain Lightning chains {ChainLightningLogic.HitsPerCard} more times.", () => { });
        }

        protected override void OnAscend() => SwapVanillaMesh("BaseArt", "ascensions/tesla/chain_lightning");

        /// <summary>Flat radius of a monster's collider (vanilla range checks count a monster once its collider touches the range).</summary>
        private static float Radius(Enemy e)
        {
            Collider c = e.GetComponent<Collider>();
            if (c == null) return 0.25f;
            Vector3 ext = c.bounds.extents;
            return Mathf.Max(ext.x, ext.z);
        }

        internal void Fire()
        {
            Enemy first = Tower.currentTarget != null ? Tower.currentTarget.GetComponent<Enemy>() : null;
            if (!Combat.IsAlive(first) || SpawnManager.instance == null) return;
            if (!Phase1Kit.PayMana(Tower, Tower.manaConsumptionRate)) return; // vanilla coils pay the base rate

            List<Enemy> enemies = SpawnManager.instance.currentEnemies.Where(Combat.IsAlive).ToList();
            int start = enemies.IndexOf(first);
            if (start < 0) { enemies.Add(first); start = enemies.Count - 1; }
            var positions = enemies.Select(e => e.transform.position).ToList();
            var radii = enemies.Select(Radius).ToList();
            List<ChainHop> hops = ChainLightningLogic.Plan(positions, start, Tower.range,
                ChainLightningLogic.MaxHits(CardCounters.Get(MoreChainsCard)), radii);

            Vector3 tip = transform.position + Vector3.up * TipHeight;
            foreach (ChainHop h in hops)
            {
                Vector3 from = h.From < 0 ? tip : positions[h.From] + Vector3.up * 0.4f;
                Phase1Kit.Bolt(from, positions[h.To] + Vector3.up * 0.4f, BoltColor, h.From < 0 ? 0.09f : 0.07f);
                Phase1Kit.Hit(Tower, enemies[h.To], Tower.damage);
            }
            if (ModConfig.DebugTools.Value)
            {
                // Why did the chain stop? Shortest gap from any victim to a monster it did not reach.
                var hitSet = new HashSet<int>(hops.Select(h => h.To));
                float gap = float.MaxValue;
                foreach (int v in hitSet)
                    for (int i = 0; i < positions.Count; i++)
                        if (!hitSet.Contains(i)) gap = Mathf.Min(gap, Phase1Kit.Flat(positions[i] - positions[v]).magnitude);
                Plugin.Log.LogInfo($"[chain] {hops.Count} hits of {enemies.Count} monsters, range {Tower.range:0.00} (height bonus {Tower.heightBonus}), " +
                                   $"max {ChainLightningLogic.MaxHits(CardCounters.Get(MoreChainsCard))}, nearest unreached {(gap < float.MaxValue ? gap.ToString("0.00") : "-")}");
            }
            Fx.Burst(tip, BoltColor, 8, 0.07f, 0.8f);
            SoundLibrary.Play("ascensions/chain_crackle", transform.position, 0.9f);
        }
    }

    /// <summary>
    /// Supercharge (Tesla Coil, Rare): each attack (vanilla zap unchanged) also gives every other tower in the coil's
    /// range +30% attack speed for 1 s. A coil only refreshes its own buff; buffs of different coils stack
    /// (<see cref="SuperchargeBuffs"/>). Applied through the stat layer, so planes, Frost Keeps and Encampments speed
    /// up too.
    /// </summary>
    internal sealed class Supercharge : AscendedTower
    {
        public const string Id = "supercharge";
        private static readonly Color ArcColor = new Color(0.95f, 0.85f, 0.35f, 0.9f);

        internal static readonly SuperchargeBuffs Buffs = new SuperchargeBuffs();
        private static readonly Dictionary<int, Tower> buffed = new Dictionary<int, Tower>();
        private static readonly List<Supercharge> live = new List<Supercharge>();
        private static int lastExpireFrame = -1;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Supercharge", TowerName = "Capacitor Coil", Rarity = Rarity.Rare, Kind = AscensionKind.Augment,
                Target = AscensionTarget.TeslaCoil, Component = typeof(Supercharge), CardModel = "ascensions/tesla/supercharge",
                Description = $"Each attack also gives the other towers in the coil's range +{SuperchargeBuffs.BonusPerCoil:P0} attack speed " +
                              $"for {SuperchargeBuffs.Duration:0} s. Buffs from different coils stack.",
            });
            StatLayer.Register("Supercharge", (tower, stats) =>
            {
                float bonus = Buffs.Bonus(tower.GetInstanceID(), Time.time);
                if (bonus > 0f) stats.Percent(TowerStat.AttackSpeed, bonus);
            });
            TreasureRunMode.RunStarted += _ => { Buffs.Clear(); buffed.Clear(); };
        }

        protected override void OnAscend()
        {
            SwapVanillaMesh("BaseArt", "ascensions/tesla/supercharge");
            live.Add(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            live.Remove(this);
            if (live.Count > 0) return;
            // The last coil is gone (or the ascension was revoked): drop every buff at once.
            Buffs.Clear();
            foreach (Tower t in buffed.Values) if (t != null) t.SetStats();
            buffed.Clear();
        }

        internal void OnZap()
        {
            float r = Tower.range + 0.5f; // towers are one cell wide: count them in when their edge is in range
            var inRange = new List<int>();
            foreach (Tower t in FindObjectsOfType<Tower>())
            {
                if (t == Tower || Phase1Kit.Flat(t.transform.position - transform.position).sqrMagnitude > r * r) continue;
                int id = t.GetInstanceID();
                buffed[id] = t;
                inRange.Add(id);
                Phase1Kit.Bolt(transform.position + Vector3.up * 2f, t.transform.position + Vector3.up * 1.1f, ArcColor, 0.04f, 0.1f, 5);
            }
            foreach (int id in Buffs.Apply(Tower.GetInstanceID(), inRange, Time.time))
                if (buffed.TryGetValue(id, out Tower t) && t != null) t.SetStats();
            if (inRange.Count > 0) SoundLibrary.Play("ascensions/supercharge", transform.position, 0.8f, 0.05f, 3);
        }

        private void Update()
        {
            if (Time.frameCount == lastExpireFrame) return;
            lastExpireFrame = Time.frameCount;
            foreach (int id in Buffs.Expire(Time.time))
                if (buffed.TryGetValue(id, out Tower t) && t != null) t.SetStats();
        }
    }

    [HarmonyPatch]
    internal static class TeslaAscensionPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(TeslaCoil), nameof(TeslaCoil.Fire))]
        private static bool TeslaFirePrefix(TeslaCoil __instance, out bool __state)
        {
            __state = false;
            if (!TreasureRunMode.Active) return true;
            ChainLightning chain = __instance.GetComponent<ChainLightning>();
            if (chain != null) { chain.Fire(); return false; }
            // Will the vanilla zap happen (enough mana)? Supercharge only buffs on real attacks.
            __state = !__instance.consumesMana ||
                      ResourceManager.instance.CheckMana((int)(__instance.damage * __instance.manaConsumptionRate));
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(TeslaCoil), nameof(TeslaCoil.Fire))]
        private static void TeslaFirePostfix(TeslaCoil __instance, bool __state)
        {
            if (__state) __instance.GetComponent<Supercharge>()?.OnZap();
        }
    }
}
