using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;

namespace TreasureRun.Ascensions
{
    /// <summary>How an ascension relates to the tower's vanilla attack (shown in the card footer).</summary>
    public enum AscensionKind
    {
        /// <summary>The vanilla attack is replaced by a new one (Hwacha, Carpet Bomber).</summary>
        Replace,
        /// <summary>Something is added on top of the vanilla attack (Supercharge, Nest of Bats).</summary>
        Augment,
        /// <summary>The vanilla attack keeps working, but changed (Strike Fighter, Repeater).</summary>
        Upgrade,
    }

    /// <summary>Static description of one ascension. Registered through <see cref="AscensionRegistry.Register"/>.</summary>
    public sealed class AscensionDef
    {
        public string Id;
        public string Name;

        /// <summary>
        /// Name of an ascended tower (popup title, build button). Null = <see cref="Name"/>; set where the ascension's
        /// name does not work as a tower name (Chain Lightning → Storm Coil). List: docs/ascension-plan.md §3.
        /// </summary>
        public string TowerName;

        public string Description;
        public Rarity Rarity;
        public AscensionTarget Target;
        public AscensionKind Kind;

        /// <summary>
        /// Component (subclass of <see cref="AscendedTower"/>) added to every tower/building of the target while the
        /// ascension is owned: existing ones on acquire, new ones when built. Null: the ascension handles itself (Radar planes).
        /// </summary>
        public Type Component;

        /// <summary>Voxel model shown as card art (name inside the models folder); null = treasure gem.</summary>
        public string CardModel;

        /// <summary>Extra content tags (castle bans), on top of "ascension" and "ascension:&lt;target&gt;".</summary>
        public string[] Tags = Array.Empty<string>();

        /// <summary>Development-only ascension (never offered, not listed under "Excludes" on other cards).</summary>
        public bool Hidden;

        /// <summary>Extra one-time effect on acquire (after components are attached).</summary>
        public Action<RunState, bool> OnAcquire;

        /// <summary>The treasure that grants this ascension (set at registration).</summary>
        public TreasureDef Treasure { get; internal set; }

        public string TargetName => AscensionTargets.DisplayName(Target);
        /// <summary>The ascended tower's name (<see cref="TowerName"/>, else the ascension's name).</summary>
        public string BuiltName => string.IsNullOrEmpty(TowerName) ? Name : TowerName;
        public string TypeLine => TargetName + " Ascension";
    }

    /// <summary>
    /// All ascensions. Each one is a treasure in the exclusion group <c>ascension:&lt;target&gt;</c>, so a run gets at most
    /// one ascension per tower. Owning one attaches its component to every tower of the target (see <see cref="Attach"/>).
    /// </summary>
    [HarmonyPatch]
    public static class AscensionRegistry
    {
        public const string Tag = "ascension";

        private static readonly List<AscensionDef> all = new List<AscensionDef>();
        private static readonly Dictionary<string, AscensionDef> byTreasure = new Dictionary<string, AscensionDef>();

        public static IReadOnlyList<AscensionDef> All => all;

        public static string Group(AscensionTarget t) => Tag + ":" + AscensionTargets.Key(t);

        /// <summary>The ascension behind a treasure (null for normal treasures).</summary>
        public static AscensionDef ForTreasure(string treasureId) =>
            treasureId != null && byTreasure.TryGetValue(treasureId, out AscensionDef d) ? d : null;

        public static AscensionDef ForTreasure(TreasureDef t) => ForTreasure(t?.Id);

        /// <summary>Other ascensions of the same target (they are excluded once this one is owned).</summary>
        public static IEnumerable<AscensionDef> Siblings(AscensionDef def) => all.Where(d => d.Target == def.Target && d != def);

        /// <summary>The owned ascension of a target in the current Treasure Run (null if none).</summary>
        public static AscensionDef Owned(AscensionTarget t)
        {
            if (!TreasureRunMode.Active) return null;
            return all.FirstOrDefault(d => d.Target == t && TreasureInventory.Owns(d.Id));
        }

        public static void Register(AscensionDef def)
        {
            if (def.Component != null && !typeof(AscendedTower).IsAssignableFrom(def.Component))
                throw new ArgumentException($"Ascension '{def.Id}': component must derive from AscendedTower.");

            var tags = new List<string> { Tag, Group(def.Target), AscensionTargets.Key(def.Target) };
            tags.AddRange(def.Tags ?? Array.Empty<string>());

            var treasure = new TreasureDef
            {
                Id = def.Id,
                Name = def.Name,
                Rarity = def.Rarity,
                Description = def.Description,
                Tags = tags.Distinct().ToArray(),
                ExclusionGroup = Group(def.Target),
                IsAvailable = _ => AscensionTargets.InPlay(def.Target),
                CreateEffect = () => new LambdaEffect((run, restoring) =>
                {
                    AttachAll(def);
                    AscensionUi.RenameBuildButton(def);
                    def.OnAcquire?.Invoke(run, restoring);
                }),
            };
            TreasureRegistry.Register(treasure);
            def.Treasure = treasure;
            all.Add(def);
            byTreasure[def.Id] = def;
        }

        /// <summary>
        /// Takes an ascension away again (dev tools and scenarios: several ascensions of one tower in one launch).
        /// Components are removed (restoring the vanilla models) and the build button gets its name back.
        /// </summary>
        public static void Revoke(AscensionDef def)
        {
            TreasureInventory.Revoke(def.Id);
            if (def.Component != null)
                foreach (GameObject go in PlacedObjects(def.Target).ToList())
                {
                    Component c = go.GetComponent(def.Component);
                    if (c == null) continue;
                    UnityEngine.Object.DestroyImmediate(c);
                    go.GetComponent<Tower>()?.SetStats();
                }
            AscensionUi.RestoreBuildButton(def);
        }

        // ------------------------------------------------------------------ components

        /// <summary>Adds the owned ascension's component of this object's target, if any (idempotent).</summary>
        public static void Attach(GameObject go)
        {
            AscensionTarget? target = AscensionTargets.Of(go);
            if (!target.HasValue) return;
            if (go.GetComponent<Castles.SiegeCastleTower>() != null) return; // the Siege Workshop's castle cannon stays a castle weapon
            AscensionDef def = Owned(target.Value);
            if (def?.Component == null || go.GetComponent(def.Component) != null) return;
            var comp = (AscendedTower)go.AddComponent(def.Component);
            comp.Initialize(def, target.Value);
        }

        /// <summary>Attaches the ascension's component to every placed tower/building of its target.</summary>
        public static void AttachAll(AscensionDef def)
        {
            if (def.Component == null) return;
            foreach (GameObject go in PlacedObjects(def.Target).ToList()) Attach(go);
        }

        /// <summary>All placed (scene) towers or buildings of a target.</summary>
        public static IEnumerable<GameObject> PlacedObjects(AscensionTarget t)
        {
            TowerType? type = AscensionTargets.TowerTypeOf(t);
            if (type.HasValue)
                return UnityEngine.Object.FindObjectsOfType<Tower>().Where(x => x.towerType == type.Value).Select(x => x.gameObject);
            Type c = AscensionTargets.BuildingComponent(t);
            return UnityEngine.Object.FindObjectsOfType(c).Cast<Component>().Select(x => x.gameObject);
        }

        /// <summary>The ascended tower's name ("Storm Coil") for a tower or building of an owned ascension, else null.</summary>
        public static string DisplayNameOf(GameObject go) => AscensionOf(go)?.BuiltName;

        /// <summary>The owned ascension that applies to this object (also for ascensions without a component, like the Radar's).</summary>
        public static AscensionDef AscensionOf(GameObject go)
        {
            if (go == null) return null;
            AscendedTower a = go.GetComponent<AscendedTower>();
            if (a != null && a.Def != null) return a.Def;
            AscensionTarget? target = AscensionTargets.Of(go);
            return target.HasValue ? Owned(target.Value) : null;
        }

        // ------------------------------------------------------------------ patches (attach on build)

        [HarmonyPostfix, HarmonyPatch(typeof(Tower), nameof(Tower.Start))]
        private static void TowerStart(Tower __instance) => Attach(__instance.gameObject);

        [HarmonyPostfix, HarmonyPatch(typeof(ManaSiphon), nameof(ManaSiphon.Start))]
        private static void SiphonStart(ManaSiphon __instance) => Attach(__instance.gameObject);

        [HarmonyPostfix, HarmonyPatch(typeof(ManaBank), nameof(ManaBank.Start))]
        private static void BankStart(ManaBank __instance) => Attach(__instance.gameObject);

        [HarmonyPostfix, HarmonyPatch(typeof(Mine), nameof(Mine.Start))]
        private static void MineStart(Mine __instance) => Attach(__instance.gameObject);

        [HarmonyPostfix, HarmonyPatch(typeof(University), nameof(University.Start))]
        private static void UniversityStart(University __instance) => Attach(__instance.gameObject);

        [HarmonyPostfix, HarmonyPatch(typeof(HauntedHouse), nameof(HauntedHouse.Start))]
        private static void HauntedHouseStart(HauntedHouse __instance) => Attach(__instance.gameObject);
    }
}
