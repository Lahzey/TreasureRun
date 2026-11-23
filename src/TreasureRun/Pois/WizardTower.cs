using System.Collections.Generic;
using System.Linq;
using TreasureRun.Ascensions;
using TreasureRun.Ascensions.Phase3;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Wizard's Tower (docs/poi-plan.md §7): an erratic wizard's crooked tower on a cell of its section that is neither on
    /// the edge nor next to the path. The whole section is his land (<see cref="WizardLand"/>): it shimmers blue, and
    /// nothing can be built on it except a Ley Line Network's siphons. During waves he casts a random spell every
    /// <see cref="WizardTowerLogic.Interval"/> seconds at monsters within <see cref="WizardTowerLogic.Range"/>
    /// (<see cref="WizardSpells"/>), each from its own gadget (<see cref="WizardGadgets"/>, which winds up first). Ley siphons next to the tower feed it
    /// <see cref="WizardTowerLogic.LeyManaPerSecond"/> mana a second during waves, and he casts every
    /// <see cref="WizardTowerLogic.LeyInterval"/> seconds. His damage is fixed (Combat.FixedHit): no treasure or card
    /// changes it.
    /// </summary>
    public sealed class WizardTower : PoiSite
    {
        public const string ModelName = "pois/wizard_tower";
        public const float Height = 3.2f;
        public static readonly List<WizardTower> All = new List<WizardTower>();

        public static PoiDef Create() => new PoiDef
        {
            Id = WizardTowerLogic.Id,
            Name = "Wizard's Tower",
            Icon = "poi/wizard_tower",
            MinWave = WizardTowerLogic.MinWave,
            Description = "An erratic wizard lives here. He lends you a hand against nearby monsters with whatever spell comes to his mind, " +
                          "from the everyday to the very rare. His land can't be built on.",
            Style = () => new WizardTowerStyle(),
            Attach = go => go.AddComponent<WizardTower>(),
        };

        private WizardGadgets gadgets;
        internal WizardGadgets Gadgets => gadgets;

        /// <summary>World position of a spell's gadget (where it leaves the tower).</summary>
        public Vector3 Emitter(WizardSpell s) => gadgets != null ? gadgets.Emitter(s) : TowerBase + Vector3.up * Height;

        /// <summary>A spell is winding up in its gadget.</summary>
        public bool Charging => gadgets != null && gadgets.Charging;

        /// <summary>The bottom centre of the tower (on the grass of its cell).</summary>
        public Vector3 TowerBase { get; private set; }
        /// <summary>The tower's map cell (world x, z rounded), for the ley link.</summary>
        public Vector2Int Grid => new Vector2Int(Mathf.RoundToInt(TowerBase.x), Mathf.RoundToInt(TowerBase.z));

        private PoiPopup popup;
        private GameObject tower;
        private LineRenderer reach;
        private float castTimer, linkCheck, refreshTimer, owed;
        private bool manaOk = true;

        /// <summary>A Ley Line Network siphon touches the tower (any height).</summary>
        public bool Linked { get; private set; }
        /// <summary>Linked and paying its mana: he casts faster.</summary>
        public bool LeyPowered => Linked && manaOk;
        public float Interval => WizardTowerLogic.CastInterval(LeyPowered);
        /// <summary>The necromantic aura lasts until this time (Time.time).</summary>
        public float AuraUntil { get; internal set; }
        public bool AuraActive => Time.time < AuraUntil;
        /// <summary>Spell forced for the next cast (dev console).</summary>
        public WizardSpell? Forced;

        protected override void OnPlaced()
        {
            var (cx, cz) = WizardTowerStyle.CellOf(Layout);
            TowerBase = CellWorld(cx, cz, 1f);
            popup = new PoiPopup
            {
                Title = () => Def.Name,
                Text = InfoText,
                Anchor = () => TowerBase + Vector3.up * (Height + 0.3f),
            };
            tower = new GameObject("TR_WizardTower") { layer = LayerMask.NameToLayer("Building") };
            tower.transform.SetParent(transform, false);
            tower.transform.position = TowerBase;
            // A solid box over the cell (building placement hits it before the grass) that opens the popup.
            var box = tower.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, Height / 2f, 0f);
            box.size = new Vector3(1f, Height, 1f);
            popup.AddTarget(tower);
            // World-aligned (not turned with the tile), so its door (and the gadgets, placed the same way) always face the camera.
            if (VoxelLibrary.Spawn(ModelName, TowerBase, Quaternion.identity, tower.transform) == null)
            {
                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(cyl.GetComponent<Collider>());
                cyl.transform.SetParent(tower.transform, false);
                cyl.transform.localPosition = new Vector3(0f, Height / 2f, 0f);
                cyl.transform.localScale = new Vector3(0.8f, Height / 2f, 0.8f);
            }
            gadgets = WizardGadgets.Build(this, TowerBase);
            WizardLand.Dress(this);
            All.Add(this);
            LeyNetwork.MarkDirty();
        }

        private void OnDestroy()
        {
            All.Remove(this);
            LeyNetwork.MarkDirty();
        }

        /// <summary>The wizard's tower whose land holds a world point (null if none).</summary>
        public static WizardTower LandAt(Vector3 p)
        {
            foreach (WizardTower t in All)
                if (t != null && WizardTowerLogic.OnLand(p - t.Center)) return t;
            return null;
        }

        public override string Status =>
            $"interval {Interval:0}s{(Linked ? LeyPowered ? " (ley powered)" : " (ley linked, no mana)" : "")}, casts " +
            string.Join(", ", WizardTowerLogic.Spells.Select(s => $"{WizardTowerLogic.Name(s)} {Record.Get("cast_" + s):0}")) +
            $", hits {Record.Get("hits"):0}, damage {Record.Get("damage"):0}, kills {Record.Get("kills"):0}, raised {Record.Get("raised"):0}, " +
            $"gold {Record.Get("gold"):0}, mana {Record.Get("mana"):0}";

        private string InfoText()
        {
            int mps = WizardTowerLogic.LeyManaPerSecond;
            bool ley = AscensionRegistry.Owned(AscensionTarget.ManaSiphon)?.Id == LeySiphon.Id;
            string link = !ley ? ""
                : !Linked ? $"A ley siphon next to the tower would feed him {mps} mana per second during waves, so he casts a little faster.\n"
                : LeyPowered ? $"<b>Ley link:</b> he draws {mps} mana per second during waves and casts a little faster.\n"
                : $"<b>Ley link:</b> he would draw {mps} mana per second to cast a little faster, but your mana has run dry.\n";
            string gold = Record.Get("gold") > 0 ? $"   Gold: {Record.Get("gold"):0}" : "";
            return "An erratic wizard lives here. He lends you a hand against nearby monsters with whatever spell comes to his mind, " +
                   "from the everyday to the very rare.\n" + link +
                   $"Spells cast: {Record.Get("casts"):0}   Monsters slain: {Record.Get("kills"):0}{gold}";
        }

        private void Update()
        {
            bool open = popup != null && popup.IsOpen;
            if (open)
            {
                if (reach == null) reach = PoiReach.Create("TR_WizardReach", tower.transform, TowerBase, WizardTowerLogic.Range);
                reach.enabled = true;
                if ((refreshTimer -= Time.deltaTime) <= 0f) { refreshTimer = 0.3f; popup.Refresh(); }
            }
            else if (reach != null) reach.enabled = false;

            if (Record == null || !Running) return;
            if ((linkCheck -= Time.deltaTime) <= 0f)
            {
                linkCheck = 0.5f;
                bool linked = LinkedSiphons() > 0;
                if (linked != Linked) { Linked = linked; LeyNetwork.MarkDirty(); }
            }
            if (!Combat)
            {
                // Between waves: the link is good for the next wave if the bank holds a second's worth (the meter's rule).
                manaOk = !Linked || ResourceManager.instance == null || ResourceManager.instance.mana >= WizardTowerLogic.LeyManaPerSecond;
                castTimer = Mathf.Min(castTimer, Interval - 1f);
                owed = 0f;
                return;
            }

            if (Linked && ResourceManager.instance != null)
            {
                bool ok = manaOk;
                int spend = LightningTrapLogic.Meter(ref owed, ref ok, Time.deltaTime, ResourceManager.instance.mana, WizardTowerLogic.LeyManaPerSecond);
                if (spend > 0)
                {
                    ResourceManager.instance.SpendMana(spend);
                    Record.Set("mana", Record.Get("mana") + spend);
                }
                manaOk = ok;
            }
            else manaOk = true;

            if (Charging) return;
            castTimer += Time.deltaTime;
            if (castTimer < Interval) return;
            if (TryCast()) castTimer = 0f;
            else castTimer = Interval; // hold the spell until a monster comes into range
        }

        /// <summary>Ley siphons touching the tower's cell (4 neighbours, any height).</summary>
        public int LinkedSiphons()
        {
            if (AscensionRegistry.Owned(AscensionTarget.ManaSiphon)?.Id != LeySiphon.Id) return 0;
            var me = new LeyCell(Grid.x, Grid.y, 0);
            return LeySiphon.All.Count(s => s != null && s.Cell.Touches(me));
        }

        /// <summary>Monsters within range, and how the wizard sees them.</summary>
        public (List<Enemy> enemies, List<WizardTarget> targets) InRange()
        {
            var enemies = new List<Enemy>();
            var targets = new List<WizardTarget>();
            if (SpawnManager.instance == null) return (enemies, targets);
            foreach (Enemy e in SpawnManager.instance.currentEnemies)
            {
                if (!Core.Combat.IsAlive(e) || WizardSpells.IsGoldified(e)) continue;
                Vector3 p = e.transform.position;
                if (!WizardTowerLogic.InRange(p - TowerBase)) continue;
                enemies.Add(e);
                targets.Add(new WizardTarget
                {
                    Progress = e.pathfinder != null ? e.pathfinder.distanceFromEnd : 0f,
                    Speed = e.pathfinder != null ? e.pathfinder.speed : e.baseSpeed,
                    Health = e.health, Armor = e.armor, Shield = e.shield,
                    Boss = Core.Combat.IsBoss(e) || Core.Combat.IsMiniBoss(e),
                    X = p.x, Z = p.z,
                });
            }
            return (enemies, targets);
        }

        /// <summary>
        /// Starts a random spell that has a target: its gadget winds up (<see cref="WizardGadgets.ChargeTime"/>), then the
        /// spell leaves it at the monsters in range at that moment. False if no monster is in range (or a spell is already
        /// winding up).
        /// </summary>
        public bool TryCast()
        {
            if (Charging) return false;
            var (enemies, targets) = InRange();
            if (enemies.Count == 0) return false;
            WizardSpell? spell = Forced.HasValue && WizardTowerLogic.HasTarget(Forced.Value, targets)
                ? Forced
                : WizardTowerLogic.Pick(Luck.Raw01(), s => WizardTowerLogic.HasTarget(s, targets));
            if (spell == null) return false;
            Forced = null;
            WizardSpell chosen = spell.Value;
            if (gadgets == null) Release(chosen);
            else gadgets.Charge(chosen, () => Release(chosen));
            return true;
        }

        /// <summary>The wound-up spell leaves its gadget at whoever is in range now; with nobody left it fizzles.</summary>
        private void Release(WizardSpell spell)
        {
            if (this == null || Record == null) return;
            var (enemies, targets) = InRange();
            if (!WizardTowerLogic.HasTarget(spell, targets))
            {
                SoulFx.GlowBurst(Emitter(spell), new Color(0.8f, 0.8f, 0.9f, 0.8f), 8, 0.8f, 0.15f, 0.4f, 0.2f);
                return;
            }
            Record.Set("casts", Record.Get("casts") + 1);
            Record.Set("cast_" + spell, Record.Get("cast_" + spell) + 1);
            WizardSpells.Cast(this, spell, enemies, targets);
        }

        /// <summary>Counts what his spells did (for the popup and the dev console).</summary>
        internal void Count(string key, float amount = 1f) => Record?.Set(key, Record.Get(key) + amount);

        /// <summary>A fixed hit from one of his spells, counted.</summary>
        internal void Hit(Enemy e, int baseDamage, int h, int a, int s)
        {
            if (!Core.Combat.IsAlive(e)) return;
            int dealt = Core.Combat.FixedHit(e, baseDamage, h, a, s);
            Count("hits");
            Count("damage", dealt);
            if (e == null || e.dead) Count("kills");
        }
    }
}
