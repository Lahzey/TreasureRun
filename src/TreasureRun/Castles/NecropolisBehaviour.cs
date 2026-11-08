using System.Collections.Generic;
using System.Linq;
using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.Castles
{
    /// <summary>
    /// Necropolis: 6 health. Monsters killed near the castle rise again as ghostly minions that walk back up the path
    /// and blow themselves up on the first monster they touch, dealing damage equal to their own health
    /// (<see cref="RiseHealthFraction"/> of the original's). Starts with the haunted house unlocked and graves on the
    /// start tile; houses pay nothing (the villagers shun necromancers).
    /// </summary>
    internal sealed class NecropolisBehaviour : CastleBehaviour
    {
        public const float RiseRadius = 10.5f;
        public const float RiseHealthFraction = 0.2f;
        public const int MaxMinions = 40;
        public const int StartingGraves = 4;

        private readonly List<Vector3> housePlots = new List<Vector3>();
        internal int Raised, Detonations, DamageDealt;

        public override bool HousesGiveIncome => false;
        public override IEnumerable<string> StartingCards => new[] { "Building - Haunted House" };

        public override DevelopmentFate StartingDevelopment(GameObject development)
        {
            if (!development.name.StartsWith("StartingHouse")) return DevelopmentFate.Vanilla;
            housePlots.Add(development.transform.position);
            return DevelopmentFate.Remove;
        }

        public override void OnGameStart() => CastleObject.AddComponent<DelayedAction>().Run(1, PlaceGraves);

        private void PlaceGraves()
        {
            // Former house plots first (they sit next to buildable cells), then any free start tile cell.
            var spots = housePlots.Select(p => new Vector3(Mathf.Round(p.x), p.y, Mathf.Round(p.z))).ToList();
            MapDecorations.Shuffle(spots);
            List<Vector3> free = MapDecorations.FreeStartTileCells();
            MapDecorations.Shuffle(free);
            spots.AddRange(free);
            int placed = 0;
            foreach (Vector3 s in spots)
            {
                if (placed >= StartingGraves) break;
                if (!MapDecorations.FreeCells(s, 0).Any()) continue; // occupied
                if (MapDecorations.Spawn<Grave>(MapDecorations.FreeCells(s, 0)[0]) != null) placed++;
            }
            Plugin.Log.LogInfo($"Necropolis: {placed} graves placed.");
        }

        public override void OnEnemyKilled(Enemy enemy)
        {
            if (RisenMinion.Active.Count >= MaxMinions) return;
            Vector3 d = enemy.transform.position - GameHelpers.CastlePosition;
            d.y = 0f;
            if (d.magnitude > RiseRadius) return;
            int hp = Mathf.Max(1, Mathf.RoundToInt(Combat.MaxHp(enemy) * RiseHealthFraction));
            if (RisenMinion.Raise(enemy, hp, this) != null) Raised++;
        }

        public override string InfoText() =>
            $"Risen minions: {RisenMinion.Active.Count} walking, {Raised} raised\n" +
            $"Detonations: {Detonations} ({DamageDealt} damage dealt)\n" +
            "Houses pay no gold.";
    }

    /// <summary>
    /// A risen monster: a renderer-only copy of the slain enemy (not targetable, not counted as an enemy) that walks
    /// the path backwards. Splits are chosen at random. Touching a monster detonates it for its health in damage.
    /// </summary>
    internal sealed class RisenMinion : MonoBehaviour
    {
        public static readonly HashSet<RisenMinion> Active = new HashSet<RisenMinion>();
        private static readonly Color Ghost = new Color(0.6f, 0.85f, 1f);
        private static readonly Color GhostDark = new Color(0.08f, 0.16f, 0.42f);
        private static readonly Color GhostLight = new Color(0.72f, 0.9f, 1f);
        private static readonly Color GhostAlpha = new Color(1f, 1f, 1f, 0.8f);
        private const float TouchRadius = 0.45f;

        private Waypoint target;
        private float speed;
        private int health;
        private NecropolisBehaviour owner;
        private float life = 60f;
        private const float GraceTime = 0.6f; // rise first, then hunt (monsters are often bunched up at the kill spot)
        private float age;

        public static RisenMinion Raise(Enemy source, int health, NecropolisBehaviour owner)
        {
            Waypoint current = source.pathfinder != null ? source.pathfinder.currentWaypoint : null;
            if (current == null) return null;

            // Renderer-only copy: built while the source is inactive so no enemy script ever runs on the copy.
            GameObject src = source.gameObject;
            bool was = src.activeSelf;
            src.SetActive(false);
            GameObject copy = Instantiate(src, src.transform.position, src.transform.rotation);
            src.SetActive(was);
            copy.name = "TR_Risen_" + src.name;
            foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is WalkAnimator) && !(mb is Pathfinder)) DestroyImmediate(mb);
            foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (Rigidbody r in copy.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(r);
            foreach (Canvas c in copy.GetComponentsInChildren<Canvas>(true)) if (c.gameObject != copy) Destroy(c.gameObject); // health bar
            Pathfinder pf = copy.GetComponent<Pathfinder>();
            if (pf != null) pf.enabled = false; // kept for WalkAnimator, which reads it
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            // Ghost look for any monster: the monster's palette turned into shades of blue (by brightness), drawn unlit
            // and slightly see-through.
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Texture blue = Fx.RampTexture(mats[i] != null ? mats[i].mainTexture : null, GhostDark, GhostLight);
                    mats[i] = Fx.GhostMaterial(blue, GhostAlpha) ?? mats[i];
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            copy.transform.localScale = src.transform.localScale * 0.85f;

            var minion = copy.AddComponent<RisenMinion>();
            minion.health = health;
            minion.owner = owner;
            minion.speed = Mathf.Max(0.6f, source.baseSpeed * 0.8f);
            // Walk back towards where the monster came from.
            Waypoint[] back = current.GetPreviousWaypoints();
            minion.target = back.Length > 0 ? back[Random.Range(0, back.Length)] : current;
            // The monster was between a previous waypoint and `current`; heading back means picking the previous one.
            copy.SetActive(true);
            Fx.Burst(copy.transform.position + Vector3.up * 0.3f, Ghost, 18, 0.1f, 1.4f);
            Active.Add(minion);
            return minion;
        }

        private void OnDestroy() => Active.Remove(this);

        private void FixedUpdate()
        {
            life -= Time.fixedDeltaTime;
            if (life <= 0f || target == null || GameManager.instance == null || GameManager.instance.gameOver)
            {
                Crumble();
                return;
            }

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float step = speed * Time.fixedDeltaTime;
            if (to.magnitude <= step)
            {
                transform.position = new Vector3(target.transform.position.x, transform.position.y, target.transform.position.z);
                Waypoint[] back = target.GetPreviousWaypoints();
                if (back.Length == 0) { Crumble(); return; } // reached the edge of the map
                target = back[Random.Range(0, back.Length)];
            }
            else
            {
                transform.position += to.normalized * step;
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }

            age += Time.fixedDeltaTime;
            if (age < GraceTime) return;
            foreach (Enemy e in Combat.EnemiesInRadius(transform.position, TouchRadius))
            {
                Detonate(e);
                return;
            }
        }

        private void Detonate(Enemy e)
        {
            int dealt = health;
            Combat.TrueDamage(e, dealt);
            if (owner != null) { owner.Detonations++; owner.DamageDealt += dealt; }
            Fx.Burst(transform.position + Vector3.up * 0.3f, Ghost, 26, 0.13f, 2.4f);
            GameHelpers.FloatingText(transform.position + Vector3.up, dealt.ToString(), "Blue", 1f, 0.6f);
            Destroy(gameObject);
        }

        private void Crumble()
        {
            Fx.Burst(transform.position + Vector3.up * 0.2f, Ghost, 10, 0.08f, 1f);
            Destroy(gameObject);
        }
    }
}
