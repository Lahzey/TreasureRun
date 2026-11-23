using System.Collections.Generic;
using TreasureRun.Ascensions.Phase1;
using TreasureRun.Audio;
using TreasureRun.Core;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Pois
{
    /// <summary>
    /// Lightning Trap (docs/poi-plan.md): four poles on the cells diagonal to the section's centre; electric arcs at three
    /// heights run between opposite poles and cross over the centre cell. Clicking any pole opens the popup; its slider sets the mana it draws during waves (0-15 per
    /// second). It strikes everything in the centre cell 4 times a second, the damage in proportion to the mana
    /// (<see cref="LightningTrapLogic.Damage"/>); the arcs, bolts and sparks grow with it, and monsters hit flash as an X-ray.
    /// </summary>
    public sealed class LightningTrap : PoiSite
    {
        public const string PoleModel = "pois/lightning_pole";
        private const float PoleHeight = 1.8f;
        private static readonly Color BoltColor = new Color(0.72f, 0.92f, 1f, 1f);

        public static PoiDef Create() => new PoiDef
        {
            Id = LightningTrapLogic.Id,
            Name = "Lightning Trap",
            Icon = "poi/lightning_trap",
            MinWave = LightningTrapLogic.MinWave,
            Description = $"Four poles around the crossing, their arcs meeting over its centre. Click a pole to set how much mana the trap draws " +
                          $"during waves (up to {LightningTrapLogic.MaxManaPerSecond} per second): it strikes everything in the centre " +
                          $"{LightningTrapLogic.StrikesPerSecond:0} times a second, harder the more mana it gets, and harder as the waves grow.",
            Style = () => new LightningTrapStyle(),
            Attach = go => go.AddComponent<LightningTrap>(),
        };

        private float owed, strikeTimer, arcTimer;
        private bool powered = true;
        private readonly List<Vector3> poleBases = new List<Vector3>();
        private readonly List<LineRenderer> arcs = new List<LineRenderer>();
        private readonly List<(Vector3 a, Vector3 b)> arcEnds = new List<(Vector3, Vector3)>();
        private PoiPopup popup;
        private int soundToggle;

        /// <summary>Mana per second set with the slider (0 = off).</summary>
        public int Rate
        {
            get => Mathf.Clamp(Mathf.RoundToInt(Record.Get("rate")), 0, LightningTrapLogic.MaxManaPerSecond);
            set => Record.Set("rate", Mathf.Clamp(value, 0, LightningTrapLogic.MaxManaPerSecond));
        }

        public bool Powered => powered;

        protected override void OnPlaced()
        {
            popup = new PoiPopup
            {
                Title = () => Def.Name,
                Text = InfoText,
                SliderMin = 0, SliderMax = LightningTrapLogic.MaxManaPerSecond,
                SliderValue = () => Rate,
                SliderChanged = SetRate,
                Anchor = () => Center + Vector3.up * 1.6f,
            };
            int building = LayerMask.NameToLayer("Building");
            foreach (var (x, z) in LightningTrapLogic.PoleCells)
            {
                Vector3 bottom = CellWorld(x, z, Layout.Height[x, z]);
                poleBases.Add(bottom);
                var pole = new GameObject("TR_TrapPole") { layer = building >= 0 ? building : PoiPopup.ClickableLayer() };
                pole.transform.SetParent(transform, false);
                pole.transform.position = bottom;
                pole.transform.rotation = transform.rotation;
                // A solid box over the whole cell: building placement hits it before the grass (the cell is blocked),
                // and the click opens the trap's popup.
                var box = pole.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, PoleHeight / 2f, 0f);
                box.size = new Vector3(1f, PoleHeight, 1f);
                popup.AddTarget(pole);
                GameObject model = VoxelLibrary.Spawn(PoleModel, bottom, transform.rotation, pole.transform);
                if (model == null)
                {
                    model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(model.GetComponent<Collider>());
                    model.transform.SetParent(pole.transform, false);
                    model.transform.localPosition = new Vector3(0f, PoleHeight / 2f, 0f);
                    model.transform.localScale = new Vector3(0.2f, PoleHeight / 2f, 0.2f);
                }
            }
            // Arcs between opposite poles, at the insulator heights: they cross over the centre cell.
            for (int i = 0; i < poleBases.Count / 2; i++)
            {
                Vector3 a = poleBases[i], b = poleBases[i + 2];
                foreach (float h in LightningTrapLogic.ArcHeights)
                {
                    Vector3 dir = (b - a).normalized;
                    arcEnds.Add((a + Vector3.up * h + dir * 0.18f, b + Vector3.up * h - dir * 0.18f));
                    arcs.Add(NewArc());
                }
            }
            UpdateArcs(true);
        }

        private LineRenderer NewArc()
        {
            var go = new GameObject("TR_TrapArc");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Fx.GlowMaterial;
            line.useWorldSpace = true;
            line.positionCount = 12;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        public void SetRate(int rate)
        {
            int before = Rate;
            Rate = rate;
            if (before == 0 && Rate > 0) SoundLibrary.Play("pois/lightning_trap/trap_on", Center + Vector3.up, 0.9f);
            else if (before > 0 && Rate == 0) SoundLibrary.Play("pois/lightning_trap/trap_off", Center + Vector3.up, 0.9f);
            UpdateArcs(true);
        }

        private string InfoText()
        {
            int wave = Mathf.Max(1, GameHelpers.Level);
            int rate = Rate;
            string state = rate == 0 ? "<b>Off</b>" : powered ? "<b>On</b>" : "<b>On</b> (not enough mana)";
            return $"Strikes everything in the centre {LightningTrapLogic.StrikesPerSecond:0} times a second during waves. More mana, more damage.\n" +
                   $"Mana: <b>{rate}</b>/{LightningTrapLogic.MaxManaPerSecond} per second\n" +
                   $"Strike: {LightningTrapLogic.Damage(wave, rate)} damage (x{LightningTrapLogic.HealthMult} health, x{LightningTrapLogic.ArmorMult} armor, " +
                   $"x{LightningTrapLogic.ShieldMult} shield)\n" +
                   $"Status: {state}   Kills: {Record.Get("kills"):0}";
        }

        public override string Status => $"rate {Rate}{(powered ? "" : " (no mana)")}, strikes {Record.Get("strikes"):0}, " +
                                         $"hits {Record.Get("hits"):0}, damage {Record.Get("damage"):0}, kills {Record.Get("kills"):0}, mana {Record.Get("mana"):0}";

        private void Update()
        {
            if (Record == null || !Running) return;
            int rate = Rate;
            if (rate > 0 && Combat && ResourceManager.instance != null)
            {
                bool ok = powered;
                int spend = LightningTrapLogic.Meter(ref owed, ref ok, Time.deltaTime, ResourceManager.instance.mana, rate);
                if (spend > 0)
                {
                    ResourceManager.instance.SpendMana(spend);
                    Record.Set("mana", Record.Get("mana") + spend);
                }
                if (ok != powered) { powered = ok; popup?.Refresh(); }
                if (powered)
                {
                    strikeTimer += Time.deltaTime;
                    float interval = 1f / LightningTrapLogic.StrikesPerSecond;
                    int n = 0;
                    while (strikeTimer >= interval && n++ < 4)
                    {
                        strikeTimer -= interval;
                        Strike(rate);
                    }
                }
            }
            else
            {
                strikeTimer = 0f;
                if (!powered) { powered = true; popup?.Refresh(); }
            }
            arcTimer -= Time.deltaTime;
            if (arcTimer <= 0f)
            {
                arcTimer = 0.06f;
                UpdateArcs(false);
            }
        }

        /// <summary>Re-jitters the arcs; their width, brightness and number follow the mana rate (dimmer between waves).</summary>
        private void UpdateArcs(bool force)
        {
            float intensity = LightningTrapLogic.Intensity(Rate) * (powered ? 1f : 0.25f) * (Combat ? 1f : 0.45f);
            int shown = intensity <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(intensity * LightningTrapLogic.ArcHeights.Length + 0.01f), 1, LightningTrapLogic.ArcHeights.Length);
            for (int i = 0; i < arcs.Count; i++)
            {
                LineRenderer line = arcs[i];
                int level = i % LightningTrapLogic.ArcHeights.Length;
                // The top arcs come on first; at low rates they also flicker.
                bool on = level >= LightningTrapLogic.ArcHeights.Length - shown && (intensity > 0.5f || Random.value < 0.35f + intensity);
                line.enabled = on;
                if (!on) continue;
                var (a, b) = arcEnds[i];
                float jitter = 0.04f + 0.1f * intensity;
                for (int k = 0; k < line.positionCount; k++)
                {
                    float t = k / (line.positionCount - 1f);
                    Vector3 p = Vector3.Lerp(a, b, t);
                    if (k > 0 && k < line.positionCount - 1) p += Random.insideUnitSphere * jitter;
                    line.SetPosition(k, p);
                }
                line.startWidth = line.endWidth = 0.015f + 0.025f * intensity;
                Color c = BoltColor;
                c.a = 0.35f + 0.6f * intensity;
                line.startColor = line.endColor = c;
            }
        }

        private void Strike(int rate)
        {
            int wave = Mathf.Max(1, GameHelpers.Level);
            int damage = LightningTrapLogic.Damage(wave, rate);
            float intensity = LightningTrapLogic.Intensity(rate);
            var victims = new List<Enemy>();
            if (SpawnManager.instance != null)
                foreach (Enemy e in SpawnManager.instance.currentEnemies)
                    if (Core.Combat.IsAlive(e) && LightningTrapLogic.InZone(e.transform.position - Center)) victims.Add(e);

            Record.Set("strikes", Record.Get("strikes") + 1);
            float width = 0.03f + 0.05f * intensity;
            if (victims.Count == 0)
            {
                // Idle discharge across the crossing.
                if (Random.value < 0.25f + 0.5f * intensity)
                {
                    Vector3 from = PoleTop(Random.Range(0, poleBases.Count));
                    Vector3 to = Center + new Vector3(Random.Range(-0.4f, 0.4f), 0.03f, Random.Range(-0.4f, 0.4f));
                    Phase1Kit.Bolt(from, to, BoltColor, width * 0.7f, 0.12f, 6);
                }
                if ((soundToggle++ & 3) == 0) SoundLibrary.Play("pois/lightning_trap/trap_strike", Center, 0.25f + 0.25f * intensity, 0.1f, 3);
                return;
            }
            foreach (Enemy e in victims)
            {
                Vector3 hit = e.transform.position + Vector3.up * 0.3f;
                int bolts = 1 + (intensity > 0.6f ? 1 : 0);
                Phase1Kit.Bolt(Crossing(), hit, BoltColor, width, 0.1f, 5);
                if (bolts > 1) Phase1Kit.Bolt(PoleTop(Random.Range(0, poleBases.Count)), hit, BoltColor, width * 0.8f, 0.15f, 7);
                Fx.Burst(hit, BoltColor, 5 + Mathf.RoundToInt(10 * intensity), 0.06f + 0.05f * intensity, 1.2f + 1.4f * intensity);
                XRayFlash.Flash(e, 0.09f + 0.06f * intensity);
                Record.Set("hits", Record.Get("hits") + 1);
                Record.Set("damage", Record.Get("damage") + damage);
                // Fixed damage: no treasure or card changes it (docs/poi-plan.md §3.6).
                Core.Combat.FixedHit(e, damage, LightningTrapLogic.HealthMult, LightningTrapLogic.ArmorMult, LightningTrapLogic.ShieldMult);
                if (e == null || e.dead || e.health <= 0) Record.Set("kills", Record.Get("kills") + 1);
            }
            SoundLibrary.Play("pois/lightning_trap/trap_strike", Center, 0.45f + 0.45f * intensity, 0.1f, 3);
        }

        /// <summary>A random point where the arcs cross over the centre (strikes come down from there).</summary>
        private Vector3 Crossing()
        {
            float[] h = LightningTrapLogic.ArcHeights;
            return Center + Vector3.up * h[Random.Range(0, h.Length)] + new Vector3(Random.Range(-0.12f, 0.12f), 0f, Random.Range(-0.12f, 0.12f));
        }

        /// <summary>A random point on a pole: one of the insulators or the crystal on top.</summary>
        private Vector3 PoleTop(int i)
        {
            float[] h = LightningTrapLogic.ArcHeights;
            int pick = Random.Range(0, h.Length + 1);
            float y = pick < h.Length ? h[pick] : PoleHeight - 0.1f;
            Vector3 toCenter = Center - poleBases[i];
            toCenter.y = 0f;
            return poleBases[i] + Vector3.up * y + toCenter.normalized * 0.15f;
        }
    }
}
