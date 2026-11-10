using System.Collections.Generic;
using HarmonyLib;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Treasures.Effects;
using UnityEngine;
using static TreasureRun.Treasures.Effects.Kit;

namespace TreasureRun.Ascensions
{
    internal enum PlaneKind { BiPlane, StrikeFighter, CarpetBomber, WarBlimp }

    /// <summary>
    /// Radar ascensions (M7): treasures that turn every Radar biplane (wingmen too) into a different aircraft. They
    /// share the exclusion group <see cref="Group"/>, so a run gets at most one.
    ///
    /// The vanilla <see cref="BiPlane"/> stays in place (the Radar keeps feeding it target and stats); an
    /// <see cref="AscendedPlane"/> component swaps the model and either tunes the vanilla flight (Strike Fighter) or
    /// replaces it (Carpet Bomber, War Blimp: BiPlane.Update is skipped).
    /// </summary>
    [HarmonyPatch]
    internal static class PlaneAscensions
    {
        public const string StrikeFighterId = "strike_fighter";
        public const string CarpetBomberId = "carpet_bomber";
        public const string WarBlimpId = "war_blimp";
        public static string Group => AscensionRegistry.Group(AscensionTarget.Radar);

        private static readonly string[] Tags = { "radar", "plane" };

        /// <summary>Planes whose vanilla flight is replaced by their AscendedPlane.</summary>
        internal static readonly HashSet<BiPlane> CustomFlight = new HashSet<BiPlane>();

        public static void Register()
        {
            Add(StrikeFighterId, "Strike Fighter", "Air Base", Rarity.Rare, AscensionKind.Upgrade, "planes/strike_fighter",
                $"Radar biplanes become jet fighters: {StrikeFighter.SpeedFactor:0.#}x flight speed, much tighter turns, " +
                $"{1f / StrikeFighter.FireIntervalFactor - 1f:P0} faster fire, a bigger magazine and instant retargeting.");
            Add(CarpetBomberId, "Carpet Bomber", "Bomb Depot", Rarity.Epic, AscensionKind.Replace, "planes/carpet_bomber",
                $"Radar biplanes become heavy bombers. Instead of shooting they store up to {CarpetBomber.MaxBombs} bombs " +
                $"(built at the Radar's fire rate) and carpet-bomb the track along the line that hits the most monsters: " +
                $"one bomb every {CarpetBomber.DropInterval:0.0} s, {CarpetBomber.BombDamageFactor:0}x damage in a small area.");
            Add(WarBlimpId, "War Blimp", "Airship Mast", Rarity.Mythical, AscensionKind.Replace, "planes/war_blimp",
                "Radar biplanes become huge, slow war blimps that hover over the thickest crowd, fire a gatling gun " +
                "without pause and drop bombs on the track. Each blimp carries a tower slot on its deck: " +
                $"build any tower there and it flies along (counts as +{WarBlimp.DeckHeightBonus} high ground).");
        }

        private static void Add(string id, string name, string towerName, Rarity rarity, AscensionKind kind, string model, string description)
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = id, Name = name, TowerName = towerName, Rarity = rarity, Kind = kind, Target = AscensionTarget.Radar,
                CardModel = model, Description = description, Tags = Tags,
                OnAcquire = (run, restoring) => ApplyAll(),
            });
        }

        public static void Hook()
        {
            TreasureRunMode.RunStarted += _ => { CustomFlight.Clear(); BlimpDeck.All.Clear(); };
        }

        public static PlaneKind Current
        {
            get
            {
                if (!TreasureRunMode.Active) return PlaneKind.BiPlane;
                if (Has(WarBlimpId)) return PlaneKind.WarBlimp;
                if (Has(CarpetBomberId)) return PlaneKind.CarpetBomber;
                if (Has(StrikeFighterId)) return PlaneKind.StrikeFighter;
                return PlaneKind.BiPlane;
            }
        }

        public static void ApplyAll()
        {
            foreach (BiPlane p in Object.FindObjectsOfType<BiPlane>()) Apply(p);
        }

        public static void Apply(BiPlane plane)
        {
            if (plane == null || plane.GetComponent<AscendedPlane>() != null) return;
            switch (Current)
            {
                case PlaneKind.StrikeFighter: plane.gameObject.AddComponent<StrikeFighter>(); break;
                case PlaneKind.CarpetBomber: plane.gameObject.AddComponent<CarpetBomber>(); break;
                case PlaneKind.WarBlimp: plane.gameObject.AddComponent<WarBlimp>(); break;
            }
        }

        /// <summary>The Radar tower that owns this plane (as its plane or its Wingman).</summary>
        public static RadarTower OwnerOf(BiPlane plane)
        {
            RadarTower wingOwner = BuildTreasures.WingmanOwner(plane);
            if (wingOwner != null) return wingOwner;
            foreach (RadarTower r in Object.FindObjectsOfType<RadarTower>())
                if (r.myPlane == plane) return r;
            return null;
        }

        /// <summary>True if there is track (the "Path" layer) right below this point.</summary>
        public static bool OverTrack(Vector3 p)
        {
            if (pathMask == 0)
            {
                pathMask = 1 << LayerMask.NameToLayer("Path");
                Plugin.Log.LogInfo($"Plane ascensions: track layer {LayerMask.NameToLayer("Path")}.");
            }
            return Physics.Raycast(new Vector3(p.x, p.y + 1f, p.z), Vector3.down, 40f, pathMask, QueryTriggerInteraction.Ignore);
        }

        private static int pathMask;

        // ------------------------------------------------------------------ patches

        [HarmonyPostfix, HarmonyPatch(typeof(BiPlane), nameof(BiPlane.Start))]
        private static void BiPlaneStartPostfix(BiPlane __instance)
        {
            if (TreasureRunMode.Active) Apply(__instance);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BiPlane), nameof(BiPlane.Update))]
        private static bool BiPlaneUpdatePrefix(BiPlane __instance) => !CustomFlight.Contains(__instance);

        /// <summary>A demolished Radar takes its blimp(s) along; towers on their decks are demolished first.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(RadarTower), nameof(RadarTower.Demolish)), HarmonyPriority(Priority.First)]
        private static void RadarDemolishPrefix(RadarTower __instance)
        {
            foreach (BiPlane p in new[] { __instance.myPlane, BuildTreasures.WingmanOf(__instance) })
            {
                if (p == null) continue;
                WarBlimp blimp = p.GetComponent<WarBlimp>();
                if (blimp != null && blimp.Deck != null) blimp.Deck.ReleasePassenger();
            }
        }
    }
}
