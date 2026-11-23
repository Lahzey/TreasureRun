using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Lizard Nest (docs/poi-plan.md §8): a nest of giant lizards on a grass cell beside the path, blocking that cell for
    /// good. In the wave it is found, <see cref="LizardNestLogic.Lizards"/> lizards crawl out of it over the first seconds
    /// and walk on along the path: slow, tough, a little armor. Each lizard that dies (unless turned to gold) bursts and
    /// sets its brood loose, <see cref="LizardNestLogic.Brood"/> tiny, fast, squishy hatchlings that wriggle on from there.
    /// When that wave is over a treasure chest stands in the nest. The nest never hatches again.
    /// </summary>
    public sealed class LizardNest : PoiSite
    {
        public const string ModelWhole = "pois/lizard_nest", ModelHatched = "pois/lizard_nest_hatched";
        public const string SoundRustle = "pois/lizard_nest/nest_rustle", SoundBurst = "pois/lizard_nest/lizard_burst", SoundSquish = "pois/lizard_nest/hatchling_squish";
        /// <summary>Height of the nest's rim and of its bowl (where the chest stands) above its cell.</summary>
        private const float RimHeight = 0.5f, BowlHeight = 0.18f;

        internal static readonly Color[] TwigColors =
        {
            new Color(0.44f, 0.32f, 0.2f), new Color(0.29f, 0.21f, 0.13f), new Color(0.59f, 0.45f, 0.3f), new Color(0.81f, 0.72f, 0.43f), new Color(0.7f, 0.62f, 0.47f),
        };
        internal static readonly Color[] GooColors =
        {
            new Color(0.61f, 0.8f, 0.28f), new Color(0.75f, 0.88f, 0.45f), new Color(0.33f, 0.5f, 0.22f), new Color(0.84f, 0.78f, 0.53f),
        };

        public static PoiDef Create() => new PoiDef
        {
            Id = LizardNestLogic.Id,
            Name = "Lizard Nest",
            Icon = "poi/lizard_nest",
            Weight = LizardNestLogic.Weight,
            MinWave = LizardNestLogic.MinWave,
            Description = "A nest of giant lizards beside the path. In the wave it is found, they crawl out and join the attack: " +
                          "slow and tough, and when one dies its brood swarms out. Live through that wave, and the treasure chest " +
                          "they guarded is yours.",
            Style = () => new LizardNestStyle(),
            Attach = go => go.AddComponent<LizardNest>(),
        };

        public enum NestState { Waiting = 0, Hatching = 1, Done = 2 }

        // ------------------------------------------------------------------ state (Record)

        public NestState State => (NestState)Mathf.RoundToInt(Record.Get("state"));
        /// <summary>The wave the nest hatched in.</summary>
        public int HatchWave => Mathf.RoundToInt(Record.Get("wave"));
        public int LizardsOut => Mathf.RoundToInt(Record.Get("lizards"));
        public int BroodOut => Mathf.RoundToInt(Record.Get("brood"));
        public int Kills => Mathf.RoundToInt(Record.Get("kills"));

        private (int x, int z) cell;
        private Vector3 ground, exitPoint;
        private Waypoint exit, target;
        private GameObject nest;
        private Transform holder;
        private PoiPopup popup;
        private int toSpawn;
        private float spawnTimer, shake, refreshTimer;

        public Vector3 Ground => ground;
        /// <summary>The path cell the lizards crawl onto, and the waypoint there (theirs, leading on to the section's route).</summary>
        public Vector3 ExitPoint => exitPoint;
        internal Waypoint Exit => exit;
        internal Waypoint Target => target;
        /// <summary>The grid cell of the nest (for the dev console and scenarios).</summary>
        public Vector2Int Grid => new Vector2Int(Mathf.RoundToInt(ground.x), Mathf.RoundToInt(ground.z));
        public int Pending => toSpawn;

        public override string Status =>
            $"cell {cell.x},{cell.z} at {ground}, {State}" + (State != NestState.Waiting ? $" (wave {HatchWave}): lizards {LizardsOut}, brood {BroodOut}, kills {Kills}" : "") +
            (target != null ? $", joins the path at {exitPoint} towards {target.transform.position}" : ", no path");

        // ------------------------------------------------------------------ placement

        protected override void OnPlaced()
        {
            cell = LizardNestStyle.CellOf(Layout);
            ground = CellWorld(cell.x, cell.z, Layout.Height[cell.x, cell.z]);
            var pc = LizardNestLogic.PathCell(Layout, cell);
            if (pc.x >= 0)
            {
                exitPoint = CellWorld(pc.x, pc.z, 0f);
                target = FindWaypoint(LizardNestLogic.TargetWaypoint(Layout, pc.x, pc.z));
            }
            if (target != null)
            {
                exit = new GameObject("TR_NestExit").AddComponent<Waypoint>();
                exit.transform.SetParent(transform, true);
                exit.transform.position = exitPoint;
                exit.SetNextWaypoint(target);
                exit.distanceFromEnd = target.distanceFromEnd + Vector3.Distance(exitPoint, target.transform.position);
                exit.trueDistance = target.trueDistance;
            }
            BuildNest();
            Core.Combat.EnemyKilled += OnKilled;
        }

        private void OnDestroy() => Core.Combat.EnemyKilled -= OnKilled;

        /// <summary>The section's waypoint for a layout index (matched by position in the tile).</summary>
        private Waypoint FindWaypoint(int index)
        {
            if (index < 0 || index >= Layout.Waypoints.Count) return null;
            Vector3 local = Layout.Waypoints[index].Local;
            foreach (Waypoint w in GetComponentsInChildren<Waypoint>(true))
            {
                Vector3 lp = transform.InverseTransformPoint(w.transform.position);
                if (Mathf.Abs(lp.x - local.x) < 0.3f && Mathf.Abs(lp.z - local.z) < 0.3f) return w;
            }
            return null;
        }

        /// <summary>The nest on its cell, a low solid box over it (blocks building; a click shows the popup).</summary>
        private void BuildNest()
        {
            int building = LayerMask.NameToLayer("Building");
            nest = new GameObject("TR_LizardNest") { layer = building >= 0 ? building : PoiPopup.ClickableLayer() };
            nest.transform.SetParent(transform, false);
            nest.transform.position = ground;
            nest.transform.rotation = Quaternion.identity;
            var box = nest.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, RimHeight / 2f, 0f);
            box.size = new Vector3(1f, RimHeight, 1f);
            popup = new PoiPopup
            {
                Title = () => Def.Name,
                Text = InfoText,
                Anchor = () => ground + Vector3.up * 1.1f,
            };
            popup.AddTarget(nest);
            holder = new GameObject("TR_NestModel").transform;
            holder.SetParent(nest.transform, false);
            SetModel(State == NestState.Waiting ? ModelWhole : ModelHatched);
        }

        private void SetModel(string model)
        {
            if (holder == null) return;
            for (int i = holder.childCount - 1; i >= 0; i--) Destroy(holder.GetChild(i).gameObject);
            if (VoxelLibrary.Spawn(model, ground, Quaternion.identity, holder) == null)
            {
                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(cyl.GetComponent<Collider>());
                cyl.transform.SetParent(holder, false);
                cyl.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                cyl.transform.localScale = new Vector3(1f, 0.15f, 1f);
            }
        }

        private string InfoText()
        {
            switch (State)
            {
                case NestState.Waiting:
                    return "Giant lizards nest here. When the next wave starts they crawl out and join it: slow and tough, and each one " +
                           "that dies sets its brood loose.\nLive through that wave, and the treasure chest they guard is yours.";
                case NestState.Hatching:
                    int alive = 0;
                    foreach (NestCreature c in NestCreature.Active) if (c != null && c.Nest == this && Core.Combat.IsAlive(c.Enemy)) alive++;
                    return $"The lizards are out! Live through this wave, and the treasure chest they guard is yours.\n" +
                           $"Lizards: <b>{LizardsOut}</b> / {LizardNestLogic.Lizards(HatchWave)}, hatchlings: <b>{BroodOut}</b>, still out there: <b>{alive}</b>";
                default:
                    return $"An empty nest. Its lizards came out in wave {HatchWave}; {Kills} of them and their brood were slain.";
            }
        }

        // ------------------------------------------------------------------ the wave it hatches in

        private void Update()
        {
            if (Record == null || !Running) return;
            if (State == NestState.Waiting && Combat) BeginHatching();
            if (State == NestState.Hatching)
            {
                if (!Combat) toSpawn = 0; // the wave is over: nobody comes out any more
                if (toSpawn > 0 && (spawnTimer -= Time.deltaTime) <= 0f)
                {
                    SpawnLizard();
                    spawnTimer = LizardNestLogic.SpawnGap;
                }
                if (!Combat && toSpawn == 0) Finish();
            }
            UpdateLooks();
            if (popup != null && popup.IsOpen && (refreshTimer -= Time.deltaTime) <= 0f)
            {
                refreshTimer = 0.25f;
                popup.Refresh();
            }
        }

        private void BeginHatching()
        {
            int wave = Mathf.Max(1, GameHelpers.Level);
            Record.Set("state", (float)NestState.Hatching);
            Record.Set("wave", wave);
            toSpawn = target != null ? LizardNestLogic.Lizards(wave) : 0;
            spawnTimer = 0f;
            Plugin.Log.LogInfo($"Lizard Nest at {ground}: {toSpawn} lizards hatch in wave {wave} " +
                               $"({LizardNestLogic.LizardHealthAt(wave)} health, {LizardNestLogic.LizardArmorAt(wave)} armor; " +
                               $"{LizardNestLogic.Brood(wave)} hatchlings of {LizardNestLogic.HatchlingHealthAt(wave)} each).");
        }

        private void SpawnLizard()
        {
            toSpawn--;
            if (LizardsOut == 0) SetModel(ModelHatched); // the eggs break as the first one comes out
            float toExit = Vector3.Distance(ground, exitPoint);
            Enemy e = NestCreature.Spawn(NestCreatureKind.Lizard, this, HatchWave, ground, exit, exit.distanceFromEnd + toExit);
            if (e == null) return;
            Record.Set("lizards", LizardsOut + 1);
            shake = 1f;
            Fx.VoxelBurst(ground + Vector3.up * 0.3f, TwigColors, 16, 0.07f, 2.2f, 0.9f, 7f);
            SoundLibrary.Play(SoundRustle, ground, 1f, 0.06f, 3);
        }

        /// <summary>The wave is over: the treasure chest in the nest.</summary>
        private void Finish()
        {
            Record.Set("state", (float)NestState.Done);
            GameObject prefab = SpawnManager.instance != null ? SpawnManager.instance.treasureChest : null;
            if (prefab != null) Instantiate(prefab, ground + Vector3.up * BowlHeight, Quaternion.identity);
            shake = 0.6f;
            Fx.VoxelBurst(ground + Vector3.up * 0.35f, TwigColors, 10, 0.06f, 1.6f, 0.8f, 7f);
            SoundLibrary.Play(SoundRustle, ground, 0.8f, 0.06f, 3);
            GameHelpers.FloatingText(ground + Vector3.up * 1.4f, "The lizards' treasure!", "Yellow", 1.1f, 1.6f);
            Plugin.Log.LogInfo($"Lizard Nest at {ground}: {Status}; a treasure chest.");
        }

        /// <summary>The nest shakes when a lizard pushes its way out.</summary>
        private void UpdateLooks()
        {
            if (holder == null) return;
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 2.5f);
            float s = shake * 0.035f;
            holder.localPosition = s > 0f ? new Vector3(Random.Range(-s, s), Random.Range(0f, s), Random.Range(-s, s)) : Vector3.zero;
        }

        // ------------------------------------------------------------------ deaths

        private void OnKilled(KillInfo k)
        {
            if (k.Enemy == null) return;
            NestCreature c = k.Enemy.GetComponent<NestCreature>();
            if (c == null || c.Nest != this) return;
            Record.Set("kills", Kills + 1);
            if (c.Kind == NestCreatureKind.Hatchling)
            {
                Fx.VoxelBurst(k.Position + Vector3.up * 0.06f, GooColors, 5, 0.035f, 1.1f, 0.4f, 6f);
                SoundLibrary.Play(SoundSquish, k.Position, 0.45f, 0.1f, 3);
                return;
            }
            // A lizard turned to gold (the Wizard's Tower) is pure gold: no brood.
            if (WizardSpells.IsGoldified(k.Enemy)) return;
            SpawnBrood(k.Enemy, k.Position, c.Wave);
        }

        /// <summary>The lizard bursts and its brood swarms out where it fell, on along its way.</summary>
        private void SpawnBrood(Enemy lizard, Vector3 at, int wave)
        {
            Pathfinder pf = lizard.pathfinder;
            Waypoint toward = pf != null ? pf.currentWaypoint : null;
            if (toward == null) return;
            float dfe = pf.distanceFromEnd;
            int n = LizardNestLogic.Brood(wave), spawned = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle * 0.3f;
                Vector3 p = new Vector3(at.x + r.x, at.y, at.z + r.y);
                if (NestCreature.Spawn(NestCreatureKind.Hatchling, this, wave, p, toward, dfe + Random.Range(-0.2f, 0.2f)) != null) spawned++;
            }
            Record.Set("brood", BroodOut + spawned);
            Fx.VoxelBurst(at + Vector3.up * 0.2f, GooColors, 22, 0.06f, 2.4f, 0.7f, 8f);
            Fx.VoxelBurst(at + Vector3.up * 0.15f, TwigColors, 4, 0.04f, 1.2f, 0.5f, 8f);
            SoundLibrary.Play(SoundBurst, at, 1f, 0.06f, 3);
        }

        // ------------------------------------------------------------------ dev tools

        /// <summary>Hatches now if it hasn't yet (dev console, scenarios): the lizards come out as if its wave had started.</summary>
        public bool ForceHatch()
        {
            if (State != NestState.Waiting) return false;
            BeginHatching();
            return true;
        }
    }
}
