using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Soul Urn (docs/poi-plan.md §6): an urn on a random grass cell of its section, blocking that cell. Every monster slain
    /// within <see cref="SoulUrnLogic.Radius"/> of it sends its soul flying into the urn; the soul fire in its mouth grows as
    /// it fills. Its size is set when its first wave starts (<see cref="SoulUrnLogic.Capacity"/>, for the waves and the
    /// lane's share of the spawn points). Full, it bursts and leaves one of three rewards at random: a treasure chest, gold
    /// (<see cref="SoulUrnLogic.Gold"/>, valued at its first wave) or a soulfire rune (<see cref="SoulRune"/>, +100% attack
    /// speed for the tower on it). A click shows how full it is and its reach, like a tower's range.
    /// </summary>
    public sealed class SoulUrn : PoiSite
    {
        public const string Model = "pois/soul_urn";
        public const string SoundAbsorb = "pois/soul_urn/soul_absorb", SoundFill = "pois/soul_urn/urn_fill", SoundRune = "pois/soul_urn/rune_ignite";
        /// <summary>Height of the model, and of the soul pool in its mouth, above its cell.</summary>
        private const float UrnHeight = 1.3f, MouthHeight = 1.18f;
        /// <summary>The swell of the fill sound before the burst.</summary>
        private const float SwellSeconds = 0.9f;

        public static PoiDef Create() => new PoiDef
        {
            Id = SoulUrnLogic.Id,
            Name = "Soul Urn",
            Icon = "poi/soul_urn",
            MinWave = SoulUrnLogic.MinWave,
            Description = $"An urn that gathers the souls of monsters slain within {SoulUrnLogic.Radius:0} tiles of it. Fill it, and it bursts " +
                          "into a treasure chest, gold, or a soulfire rune that makes the tower built on it attack much faster.",
            IsAvailable = run => GameHelpers.Level <= SoulUrnLogic.LastOmenWave,
            Style = () => new SoulUrnStyle(),
            Attach = go => go.AddComponent<SoulUrn>(),
        };

        // ------------------------------------------------------------------ state (Record)

        public int Souls => Mathf.RoundToInt(Record.Get("souls"));
        public int Capacity => Mathf.RoundToInt(Record.Get("capacity"));
        public bool Full => Record.Get("filled") > 0f;
        public UrnReward Reward => (UrnReward)Mathf.RoundToInt(Record.Get("reward"));
        /// <summary>Fill shown by the urn: souls that have arrived (souls in flight are counted, but not shown yet).</summary>
        public float Shown => SoulUrnLogic.Fill(arrived, Capacity);

        private (int x, int z) cell;
        private Vector3 ground;
        private GameObject urn;
        private Transform model;
        private PoiPopup popup;
        private ParticleSystem fire;
        private MeshRenderer glow;
        private int arrived, inFlight;
        private float punch, refreshTimer;
        private bool bursting;
        private UrnReward? forced;

        /// <summary>The urn's cell on the ground (world).</summary>
        public Vector3 Ground => ground;
        public Vector3 Mouth => ground + Vector3.up * MouthHeight;
        public int InFlight => inFlight;
        public bool Bursting => bursting;

        public override string Status =>
            $"cell {cell.x},{cell.z} at {ground}, souls {Souls}/{Capacity} (lane share {Record.Get("share"):0.##}), arrived {arrived}, in flight {inFlight}, " +
            (Full ? $"full: {Reward}" + (Reward == UrnReward.Gold ? $" {Record.Get("gold"):0} gold" : Reward == UrnReward.Rune ? $" +{Record.Get("speed"):0%} attack speed" : "")
                  : bursting ? "bursting" : "charging");

        // ------------------------------------------------------------------ placement

        protected override void OnPlaced()
        {
            cell = SoulUrnStyle.CellOf(Layout);
            ground = CellWorld(cell.x, cell.z, Layout.Height[cell.x, cell.z]);
            arrived = Souls;
            if (!Full) BuildUrn();
            else if (Reward == UrnReward.Rune) SoulRune.Create(ground, Record.Get("speed"), transform);
            Core.Combat.EnemyKilled += OnKilled;
        }

        private void OnDestroy() => Core.Combat.EnemyKilled -= OnKilled;

        /// <summary>The urn on its plinth, a solid box over the cell (blocks building; a click shows the popup), and its soul fire.</summary>
        private void BuildUrn()
        {
            int building = LayerMask.NameToLayer("Building");
            urn = new GameObject("TR_SoulUrn") { layer = building >= 0 ? building : PoiPopup.ClickableLayer() };
            urn.transform.SetParent(transform, false);
            urn.transform.position = ground;
            urn.transform.rotation = Quaternion.identity;
            var box = urn.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, UrnHeight / 2f, 0f);
            box.size = new Vector3(1f, UrnHeight, 1f);
            popup = new PoiPopup
            {
                Title = () => Def.Name,
                Text = InfoText,
                Anchor = () => ground + Vector3.up * 1.7f,
            };
            popup.AddTarget(urn);

            var holder = new GameObject("TR_UrnModel").transform;
            holder.SetParent(urn.transform, false);
            model = holder;
            if (VoxelLibrary.Spawn(Model, ground, Quaternion.identity, holder) == null)
            {
                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(cyl.GetComponent<Collider>());
                cyl.transform.SetParent(holder, false);
                cyl.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                cyl.transform.localScale = new Vector3(0.6f, 0.55f, 0.6f);
            }
            fire = SoulFx.SoulFire(urn.transform, Mouth);
            glow = SoulFx.GlowBillboard("TR_UrnGlow", urn.transform, Color.clear, 0.6f);
            glow.transform.position = Mouth + Vector3.up * 0.05f;
            UpdateLooks();
        }

        private string InfoText()
        {
            string intro = $"Monsters slain within {SoulUrnLogic.Radius:0} tiles give it their souls. Full, it bursts into a treasure chest, " +
                           "gold, or a soulfire rune (attack speed for the tower on it).\n";
            if (Full || bursting) return intro + "<b>Full!</b>";
            int cap = Capacity;
            if (cap <= 0) return intro + $"Souls: <b>{Souls}</b> (its size is set when the next wave starts)";
            float fill = SoulUrnLogic.Fill(Souls, cap);
            const int width = 20;
            int blocks = SoulUrnLogic.BarBlocks(fill, width);
            return intro + $"Souls: <b>{Souls}</b> / {cap}  ({fill:0%})\n" +
                   $"<color=#8cecde>{new string('█', blocks)}</color><color=#3a4258>{new string('█', width - blocks)}</color>";
        }

        // ------------------------------------------------------------------ charging

        private void Update()
        {
            if (Record == null || !Running) return;
            if (!Full && !bursting && Capacity == 0 && Combat) SetCapacity();
            if (urn != null && !bursting) UpdateLooks();
            // Souls counted but lost on the way (a soul object destroyed): fill once nothing is in flight any more.
            if (!Full && !bursting && Capacity > 0 && Souls >= Capacity && inFlight == 0) Burst();
            bool open = popup != null && popup.IsOpen;
            ShowReach(open);
            if (open && (refreshTimer -= Time.deltaTime) <= 0f)
            {
                refreshTimer = 0.25f;
                popup.Refresh();
            }
        }

        private LineRenderer reach;
        private static readonly FieldInfo TowerUiLine = AccessTools.Field(typeof(TowerUI), "line");

        /// <summary>
        /// The urn's reach while its popup is open: a circle of <see cref="SoulUrnLogic.Radius"/> drawn like a tower's range
        /// (the vanilla TowerUI line: its material, width and colour, 60 segments at the same height).
        /// </summary>
        private void ShowReach(bool show)
        {
            if (!show) { if (reach != null) reach.enabled = false; return; }
            if (urn == null) return;
            if (reach == null)
            {
                var go = new GameObject("TR_UrnReach");
                go.transform.SetParent(urn.transform, false);
                reach = go.AddComponent<LineRenderer>();
                LineRenderer template = null;
                foreach (TowerUI ui in Resources.FindObjectsOfTypeAll<TowerUI>())
                    if ((template = TowerUiLine?.GetValue(ui) as LineRenderer) != null && template.sharedMaterial != null) break;
                if (template != null)
                {
                    reach.sharedMaterial = template.sharedMaterial;
                    reach.widthCurve = template.widthCurve;
                    reach.widthMultiplier = template.widthMultiplier;
                    reach.colorGradient = template.colorGradient;
                    reach.numCapVertices = template.numCapVertices;
                }
                else
                {
                    reach.sharedMaterial = Fx.LineMaterial ?? Fx.GlowMaterial;
                    reach.startWidth = reach.endWidth = 0.05f;
                }
                reach.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                reach.receiveShadows = false;
                reach.useWorldSpace = true;
                reach.positionCount = 61;
                Vector3 centre = new Vector3(ground.x, 0.4f, ground.z);
                for (int i = 0; i < 61; i++)
                {
                    float a = i * 6f * Mathf.Deg2Rad;
                    reach.SetPosition(i, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * SoulUrnLogic.Radius);
                }
            }
            reach.enabled = true;
        }

        private static readonly FieldInfo SpawnPointsField = AccessTools.Field(typeof(SpawnManager), "spawnPoints");

        /// <summary>The urn's size, for the wave that starts and the share of the spawn points whose monsters pass this section.</summary>
        private void SetCapacity()
        {
            int wave = Mathf.Max(1, GameHelpers.Level);
            var (through, total) = CountSpawnPoints();
            double share = SoulUrnLogic.LaneShare(through, total);
            Record.Set("capacity", SoulUrnLogic.Capacity(wave, share));
            Record.Set("share", (float)share);
            Record.Set("wave", wave); // the rewards are valued at this wave, however long the urn takes to fill
            Plugin.Log.LogInfo($"Soul Urn at {ground}: holds {Capacity} souls (wave {wave}, {through} of {total} spawn points pass it).");
            popup?.Refresh();
        }

        /// <summary>Spawn points whose monsters walk through this section, and all of them.</summary>
        private (int through, int total) CountSpawnPoints()
        {
            if (SpawnManager.instance == null || !(SpawnPointsField?.GetValue(SpawnManager.instance) is HashSet<Waypoint> points)) return (0, 0);
            int through = 0;
            foreach (Waypoint sp in points)
            {
                Waypoint w = sp;
                for (int i = 0; i < 500 && w != null; i++)
                {
                    if (w.transform.IsChildOf(transform)) { through++; break; }
                    Waypoint next = w.GetNextWaypoint(false);
                    if (next == w) break;
                    w = next;
                }
            }
            return (through, points.Count);
        }

        private void OnKilled(KillInfo k)
        {
            if (Record == null || Full || bursting || urn == null) return;
            if (!SoulUrnLogic.InReach(k.Position - ground)) return;
            if (Capacity == 0) SetCapacity();
            if (Souls >= Capacity) return;
            Record.Set("souls", Souls + 1);
            inFlight++;
            SoulFx.LaunchSoul(k.Position, Mouth, OnSoulArrived);
        }

        private void OnSoulArrived()
        {
            if (this == null) return;
            inFlight = Mathf.Max(0, inFlight - 1);
            if (Full || bursting || urn == null) return;
            arrived = Mathf.Min(arrived + 1, Souls);
            punch = 1f;
            SoulFx.Absorb(Mouth);
            SoundLibrary.Play(SoundAbsorb, Mouth, 0.45f, 0.06f, 3);
            if (arrived >= Capacity) Burst();
        }

        /// <summary>The soul fire and the glow in the mouth grow with the fill; a soul arriving flashes it; near full it trembles.</summary>
        private void UpdateLooks()
        {
            float fill = Shown;
            punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 4f);
            SoulFx.SetFire(fire, fill);
            if (glow != null)
            {
                glow.transform.localScale = Vector3.one * (0.45f + 0.5f * fill + 0.25f * punch);
                SoulFx.SetColor(glow, new Color(SoulFx.Soul.r, SoulFx.Soul.g, SoulFx.Soul.b, 0.3f + 0.45f * fill + 0.25f * punch));
            }
            if (model != null)
            {
                float tremble = Mathf.Clamp01((fill - 0.85f) / 0.15f) * 0.012f;
                model.localPosition = tremble > 0f ? new Vector3(Random.Range(-tremble, tremble), 0f, Random.Range(-tremble, tremble)) : Vector3.zero;
            }
        }

        // ------------------------------------------------------------------ bursting

        private void Burst()
        {
            if (bursting || Full) return;
            bursting = true;
            UrnReward reward = forced ?? SoulUrnLogic.Pick(Luck.Raw01());
            StartCoroutine(BurstRoutine(reward));
        }

        private IEnumerator BurstRoutine(UrnReward reward)
        {
            Plugin.Log.LogInfo($"Soul Urn at {ground} is full ({Souls} souls): {reward}.");
            SoundLibrary.PlayLoud(SoundFill, Mouth, 0.9f, 12f);
            SoulFx.Swirl(Mouth, SwellSeconds);
            for (float t = 0f; t < SwellSeconds; t += Time.deltaTime)
            {
                float u = t / SwellSeconds;
                SoulFx.SetFire(fire, 1f + u);
                if (glow != null)
                {
                    glow.transform.localScale = Vector3.one * (1f + 1.2f * u * u);
                    SoulFx.SetColor(glow, new Color(SoulFx.SoulCore.r, SoulFx.SoulCore.g, SoulFx.SoulCore.b, 0.75f + 0.25f * u));
                }
                if (model != null)
                {
                    float shake = 0.01f + 0.04f * u;
                    model.localPosition = new Vector3(Random.Range(-shake, shake), 0.06f * u * u, Random.Range(-shake, shake));
                }
                yield return null;
            }
            SoulFx.UrnBurst(this, ground, Mouth);
            Record.Set("filled", 1f);
            Record.Set("reward", (float)(int)reward);
            if (urn != null) Destroy(urn);
            urn = null;
            popup = null;
            yield return new WaitForSeconds(0.15f);
            GrantReward(reward);
            bursting = false;
        }

        private void GrantReward(UrnReward reward)
        {
            int wave = Mathf.Max(1, Record.Get("wave") > 0f ? Mathf.RoundToInt(Record.Get("wave")) : GameHelpers.Level);
            Vector3 top = ground + Vector3.up * 1.6f;
            switch (reward)
            {
                case UrnReward.Chest:
                    GameObject prefab = SpawnManager.instance != null ? SpawnManager.instance.treasureChest : null;
                    if (prefab != null) Instantiate(prefab, ground + Vector3.up * 0.02f, Quaternion.identity); // the chest's mesh sits just below its pivot
                    GameHelpers.FloatingText(top, "A treasure chest!", "Yellow", 1.1f, 1.6f);
                    break;
                case UrnReward.Gold:
                    int gold = SoulUrnLogic.Gold(wave);
                    ResourceManager.instance?.AddMoney(gold);
                    Record.Set("gold", gold);
                    SoulFx.Coins(ground + Vector3.up * 0.3f);
                    SoundLibrary.PlayVanilla(global::Sound.CoinLong, ground);
                    GameHelpers.FloatingText(top, $"+{gold} gold", "Yellow", 1.3f, 1.6f);
                    break;
                default:
                    float speed = (float)SoulUrnLogic.RuneBonus;
                    Record.Set("speed", speed);
                    SoulRune.Create(ground, speed, transform);
                    SoundLibrary.Play(SoundRune, ground + Vector3.up * 0.3f, 0.9f, 0f, 2);
                    GameHelpers.FloatingText(top, $"Soulfire rune: +{speed:0%} attack speed", "Blue", 1.1f, 2f);
                    break;
            }
            Plugin.Log.LogInfo($"Soul Urn at {ground}: {Status}");
        }

        // ------------------------------------------------------------------ dev tools

        /// <summary>Sends souls from random points around the urn (dev console, scenarios), as if monsters died there.</summary>
        public int AddSouls(int count)
        {
            if (Full || bursting || urn == null) return 0;
            if (Capacity == 0) SetCapacity();
            int n = 0;
            for (int i = 0; i < count && Souls < Capacity; i++, n++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.5f, SoulUrnLogic.Radius - 0.5f);
                Record.Set("souls", Souls + 1);
                inFlight++;
                SoulFx.LaunchSoul(ground + new Vector3(r.x, -ground.y, r.y), Mouth, OnSoulArrived);
            }
            return n;
        }

        /// <summary>Fills the urn at once with a given reward (null: random).</summary>
        public void ForceFill(UrnReward? reward)
        {
            if (Full || bursting || urn == null) return;
            if (Capacity == 0) SetCapacity();
            forced = reward;
            Record.Set("souls", Capacity);
            arrived = Capacity;
            Burst();
        }
    }
}
