using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Runs the Points of Interest of a Treasure Run (docs/poi-plan.md):
    ///  - after each expansion, every new Expand button rolls for an omen (no pity, no cap: a four-way split can show three);
    ///  - an omen can't be removed: using its button places the POI's section, whatever exits vanilla picked for it;
    ///  - a dead end (no exits) stays a vanilla dead end and the omen is lost.
    /// </summary>
    public static class PoiDirector
    {
        private static PoiOmenRecord pending;
        /// <summary>Debug: the next tile gets exactly these exits (if vanilla has such a tile and they fit).</summary>
        public static SectionExits? ForceExits;
        /// <summary>Debug: the next omen roll always succeeds with this POI.</summary>
        public static string ForceNextOmen;

        public static bool Enabled => TreasureRunMode.Active && TreasureRunMode.Run != null && ModConfig.PoiEnabled.Value;

        public static PoiOmenRecord OmenAt(int x, int y) => TreasureRunMode.Run?.PoiOmens.FirstOrDefault(o => o.X == x && o.Y == y);

        /// <summary>A new Expand button appeared (TileSpawnLocation.Start).</summary>
        public static void OnMarker(TileSpawnLocation marker)
        {
            if (!Enabled || marker == null) return;
            RunState run = TreasureRunMode.Run;
            PoiOmenRecord omen = OmenAt(marker.posX, marker.posY);
            if (omen == null)
            {
                int wave = Core.GameHelpers.Level;
                if (wave < 1) return; // the start tile's buttons: no expansion yet
                omen = Roll(run, wave, marker.posX, marker.posY);
                if (omen == null) return;
                run.PoiOmens.Add(omen);
                Plugin.Log.LogInfo($"POI omen: {omen.Id} on the Expand button at {marker.posX},{marker.posY} (wave {wave}).");
            }
            OmenBadge.Attach(marker, PoiRegistry.Get(omen.Id));
        }

        private static PoiOmenRecord Roll(RunState run, int wave, int x, int y)
        {
            PoiDef def;
            if (ForceNextOmen != null)
            {
                def = PoiRegistry.Find(ForceNextOmen);
                ForceNextOmen = null;
            }
            else
            {
                List<PoiDef> pool = PoiRegistry.Available(wave, run);
                if (pool.Count == 0) return null;
                if (!ModConfig.DebugPoiForce.Value && !Luck.Check(LuckCategory.POI, Mathf.Clamp01(ModConfig.PoiOmenChance.Value))) return null;
                float total = pool.Sum(d => d.Weight), r = Luck.Raw01() * total;
                def = pool.Last();
                foreach (PoiDef d in pool) { r -= d.Weight; if (r < 0f) { def = d; break; } }
            }
            return def == null ? null : new PoiOmenRecord { Id = def.Id, X = x, Y = y, Wave = wave };
        }

        /// <summary>Puts an omen on an Expand button now (dev console).</summary>
        public static bool PlaceOmen(TileSpawnLocation marker, PoiDef def)
        {
            if (!Enabled || marker == null || def == null) return false;
            RunState run = TreasureRunMode.Run;
            run.PoiOmens.RemoveAll(o => o.X == marker.posX && o.Y == marker.posY);
            run.PoiOmens.Add(new PoiOmenRecord { Id = def.Id, X = marker.posX, Y = marker.posY, Wave = Core.GameHelpers.Level });
            OmenBadge.Attach(marker, def);
            return true;
        }

        /// <summary>An Expand button is used (TileSpawnLocation.SpawnTile, before the tile spawns).</summary>
        public static void OnExpand(TileSpawnLocation marker)
        {
            pending = null;
            if (!Enabled || marker == null) return;
            pending = OmenAt(marker.posX, marker.posY);
            if (pending != null) TreasureRunMode.Run.PoiOmens.Remove(pending);
        }

        /// <summary>Vanilla placed the tile (TileManager.SpawnNewTile): turn it into the POI's section.</summary>
        public static void OnTileSpawned(int x, int y, int rotation)
        {
            PoiOmenRecord omen = pending;
            pending = null;
            ForceExits = null;
            if (omen == null || omen.X != x || omen.Y != y || !Enabled) return;
            TerrainTile tile = TileManager.instance.tileArray[x, y];
            PoiDef def = PoiRegistry.Get(omen.Id);
            if (tile == null || def == null) return;

            SectionExits exits = SectionBuilder.ExitsOf(tile);
            if (exits == SectionExits.None)
            {
                Plugin.Log.LogInfo($"POI {def.Id} lost: the tile at {x},{y} is a dead end.");
                return;
            }
            var record = new PoiRecord
            {
                Id = def.Id, X = x, Y = y, Rotation = rotation, Exits = (int)exits,
                Seed = Luck.RawRange(0, int.MaxValue), Wave = Core.GameHelpers.Level + 1,
            };
            if (SectionBuilder.Build(tile, def, record) == null) return;
            TreasureRunMode.Run.Pois.Add(record);
            Plugin.Log.LogInfo($"POI placed: {def.Id} at {x},{y} (exits {exits}, rotation {rotation}).");
        }

        /// <summary>The live POI sections of this run.</summary>
        public static IEnumerable<PoiSite> Sites => Object.FindObjectsOfType<PoiSite>();
    }
}
