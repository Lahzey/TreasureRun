using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreasureRun.Castles
{
    public enum DevelopmentFate { Vanilla, Keep, Remove }

    public enum PartAnimation { None, Float }

    /// <summary>A separate voxel model attached to the castle mesh (see CastleVisuals).</summary>
    public sealed class CastlePart
    {
        public string Name;
        public string Model;
        /// <summary>Position in castle-model voxels: x/z offset from the centre, y up from the model's floor.</summary>
        public Vector3 VoxelPosition;
        public float Yaw;
        /// <summary>Model scale relative to the castle's voxel size.</summary>
        public float Scale = 1f;
        public PartAnimation Animation;
        /// <summary>Only shown in the menu; in the game a live object takes its place (e.g. the cannon tower).</summary>
        public bool MenuOnly;
    }

    /// <summary>Menu selection a castle's availability is checked against.</summary>
    public struct CastleContext
    {
        public bool TreasureRun;
        public bool MonsterMode;
        public bool SplitMode;
        public int Lanes;

        public static CastleContext FromPrefs() => new CastleContext
        {
            TreasureRun = Core.TreasureRunMode.Selected,
            MonsterMode = PlayerPrefs.GetInt("MonsterMode", 0) == 1,
            SplitMode = PlayerPrefs.GetInt("SplitMode", 0) == 1,
            Lanes = Mathf.Clamp(PlayerPrefs.GetInt("GameMode", 1), 1, 4),
        };
    }

    /// <summary>Static description of a selectable castle (the player's main tower).</summary>
    public sealed class CastleDef
    {
        public string Id;
        public string Name;
        /// <summary>Rich text allowed.</summary>
        public string Description;
        /// <summary>Starting health in displayed points; null = vanilla (100 + skill tree in normal mode).</summary>
        public int? StartingHealth;
        /// <summary>Replaces the health number in the menu (e.g. "none" for castles that ignore health).</summary>
        public string HealthText;
        /// <summary>Voxel model name (models folder, e.g. "castles/arcane_spire"); null = vanilla castle mesh.</summary>
        public string Model;

        /// <summary>
        /// Separate (usually moving) parts shown with the castle in the menu background and in the game, e.g. the
        /// Arcane Spire's floating crystal or the Siege Workshop's cannon.
        /// </summary>
        public List<CastlePart> Parts = new List<CastlePart>();

        /// <summary>Below 1: the castle model is drawn see-through (Standard shader in Fade mode).</summary>
        public float ModelAlpha = 1f;

        /// <summary>
        /// Optional availability rule (e.g. Treasure Run only, or not with March of the Monsters).
        /// Null = always available. Unavailable castles are shown locked in the menu and fall back to the default.
        /// </summary>
        public Func<CastleContext, bool> IsAvailable;
        /// <summary>Shown in the menu when <see cref="IsAvailable"/> fails, e.g. "Treasure Run only".</summary>
        public string AvailabilityHint;

        /// <summary>Creates the per-run behaviour (one instance per game scene). Null = no special behaviour.</summary>
        public Func<CastleBehaviour> CreateBehaviour;

        public bool AvailableIn(CastleContext ctx) => IsAvailable == null || IsAvailable(ctx);
    }

    /// <summary>
    /// Per-run castle logic. All hooks run in every game mode (not only Treasure Run).
    /// Override only what the castle changes.
    /// </summary>
    public abstract class CastleBehaviour
    {
        public CastleDef Def { get; internal set; }

        /// <summary>The castle's root object in the game scene ("MainTower").</summary>
        public GameObject CastleObject { get; internal set; }

        /// <summary>After GameManager.Start: scene, castle object and managers exist; starting health is applied.</summary>
        public virtual void OnGameStart() { }

        /// <summary>Right before SpawnManager.StartNextWave runs (income generators pay out inside it).</summary>
        public virtual void OnBeforeWaveStart(int nextLevel) { }

        /// <summary>
        /// An enemy reached the castle. Return true if the castle handled it completely (vanilla AtEnd is skipped;
        /// the behaviour must then remove/kill the enemy itself).
        /// </summary>
        public virtual bool OnEnemyReachedCastle(Enemy enemy) => false;

        /// <summary>
        /// The start tile contains optional "starting developments" (8 houses "StartingHouse", 4 corner crystals
        /// "StartingManaCrystal") that vanilla keeps or removes at random based on skill-tree progress.
        /// </summary>
        public virtual DevelopmentFate StartingDevelopment(GameObject development) => DevelopmentFate.Vanilla;

        /// <summary>Multiplier for the number of enemies spawned per wave.</summary>
        public virtual float SpawnCountMultiplier => 1f;

        /// <summary>Gold multiplier per source (kill gold, income, trees).</summary>
        public virtual float GoldMultiplier(Core.GoldSource source) => 1f;

        // ----- health -----

        /// <summary>
        /// Health plays no role (Glass Fortress): max-health changes have no effect, any castle damage is lethal and
        /// max-health costs of cards/treasures are free.
        /// </summary>
        public virtual bool IgnoresHealth => false;

        // ----- content (cards and treasures) -----

        /// <summary>Content tags (Cards.ContentTags) whose cards and treasures never appear with this castle.</summary>
        public virtual IEnumerable<string> BannedTags => Array.Empty<string>();

        /// <summary>Extra card bans by exact title (prefer tags).</summary>
        public virtual bool BansCard(string title) => false;

        /// <summary>Card titles granted for free when the game starts (e.g. "Building - Mana Siphon").</summary>
        public virtual IEnumerable<string> StartingCards => Array.Empty<string>();

        private HashSet<string> bannedTagSet;
        public HashSet<string> BannedTagSet => bannedTagSet ?? (bannedTagSet = new HashSet<string>(BannedTags));

        // ----- economy / map -----

        /// <summary>Multiplier for tower prices (build buttons, refunds). Non-tower buildings are unaffected.</summary>
        public virtual float TowerCostMultiplier => 1f;

        /// <summary>False: vanilla houses pay nothing for adjacent towers.</summary>
        public virtual bool HousesGiveIncome => true;

        /// <summary>
        /// Spawn chance of a map decoration (SpawnableObject: trees, houses, graves, iron, crystals, shrines) on new
        /// tiles. Return 0 to forbid it. Runs after vanilla/treasure modifiers.
        /// </summary>
        public virtual float DecorationChance(SpawnableObject decoration, float chance) => chance;

        // ----- events -----

        /// <summary>An enemy died (after vanilla Die: gold already paid).</summary>
        public virtual void OnEnemyKilled(Enemy enemy) { }

        /// <summary>After SpawnManager.StartNextWave (level incremented, spawn points updated, spawning started).</summary>
        public virtual void OnWaveStarted(int level) { }

        /// <summary>A wave was cleared (after vanilla EndWave).</summary>
        public virtual void OnWaveEnded(int level) { }

        // ----- UI -----

        /// <summary>The castle was clicked. Return true if handled (default: the castle info popup opens).</summary>
        public virtual bool OnClicked() => false;

        /// <summary>Live stats for the castle info panel (click on the castle), e.g. income or mana. Null = none.</summary>
        public virtual string InfoText() => null;
    }
}
