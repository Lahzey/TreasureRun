using System;
using System.Collections.Generic;
using TreasureRun.Core;
using TreasureRun.Treasures.Effects;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Ascensions
{
    /// <summary>
    /// Carpet Bomber (Epic): stores up to <see cref="MaxBombs"/> bombs (one per <see cref="BombIntervalFactor"/> Radar
    /// shots) and flies straight bombing runs, dropping a bomb every <see cref="DropInterval"/> s while it is above the
    /// track.
    ///
    /// <para><b>Choosing a run</b> (<see cref="Plan"/>): a run is a straight segment of length L = (bombs − 1) ×
    /// bomb spacing. Monster positions are predicted for when the bombs would land (approach time + fall time). Candidate
    /// lines are (a) each monster's walking direction through it, since the track is made of straight pieces and
    /// monsters on one piece line up along it, and (b) the line through every pair of monsters at most L apart. For
    /// each line, the monsters within the hit width are projected onto it and a sliding window of length L over the
    /// sorted projections finds the best segment (weighted by remaining health). The direction (either way along the
    /// segment) is picked by how cheaply the bomber can line up for it. That is O(n³ log n) for n ≤
    /// <see cref="MaxCandidates"/> monsters, a few thousand operations, run about once per second: not optimal, but a
    /// good run.</para>
    ///
    /// <para><b>Flying it</b>: approach an entry point one turning circle before the segment, then follow the line
    /// (pursuit of a point ahead on the line) and drop while lined up, inside the segment and above the track.</para>
    /// </summary>
    internal sealed class CarpetBomber : AscendedPlane
    {
        public const int MaxBombs = 20;
        public const float DropInterval = 0.2f;
        /// <summary>Seconds per new bomb = the Radar's seconds per shot × this (vanilla 700 rpm → ~0.86 s).</summary>
        public const float BombIntervalFactor = 10f;
        public const float BombDamageFactor = 3f;
        public const float BlastRadius = 0.9f;
        public const float Speed = 5.5f, TurnRate = 75f, Altitude = 4.2f;
        private const int MinBombsForRun = 8;
        private const int MaxCandidates = 36;
        private const float HitWidth = 0.75f;

        private enum Phase { Patrol, Approach, Run }

        private struct RunPlan
        {
            public Vector3 Start, Dir;
            public float Length, Score;
            public int Hits;
        }

        private float bombs = MaxBombs;
        private float dropTimer, planTimer, phaseTime, yaw, bank;
        private Phase phase;
        private RunPlan run;
        private Transform[] propellers = Array.Empty<Transform>();

        private static float TurnRadius => Speed / (TurnRate * Mathf.Deg2Rad);
        private static float Lead => TurnRadius * 1.6f;
        private static float Spacing => Speed * DropInterval;

        protected override bool CustomFlight => true;
        protected override string ModelName => "planes/carpet_bomber";
        protected override float ModelScale => 0.7f;

        public int Bombs => (int)bombs;
        public string PhaseName => phase.ToString();

        protected override void Awake()
        {
            base.Awake();
            yaw = transform.eulerAngles.y;
            var props = new List<Transform>();
            foreach (int x in new[] { 6, 11, 21, 26 })
            {
                Transform p = AddPart("planes/propeller", new Vector3(x + 0.5f, 20.2f, 3.5f), 0.9f);
                if (p != null) props.Add(p);
            }
            propellers = props.ToArray();
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            foreach (Transform p in propellers) p.Rotate(0f, 0f, 1400f * dt, Space.Self);

            bombs = Mathf.Min(MaxBombs, bombs + dt / (RadarShotInterval * BombIntervalFactor));
            planTimer -= dt;
            phaseTime += dt;
            Vector3 pos = transform.position;
            Vector3 steer;

            switch (phase)
            {
                case Phase.Approach:
                {
                    if (planTimer <= 0f)
                    {
                        planTimer = 1f;
                        // Monsters move: re-plan, but only switch for a clearly better run (no dithering).
                        if (Plan(out RunPlan p) && p.Score > run.Score * 1.25f + 0.5f) run = p;
                    }
                    // Entry point one turning circle before the run; if we reach it facing the wrong way, it moves
                    // further back so the bomber loops around and comes in straight.
                    Vector3 entry = run.Start - run.Dir * (wideEntry ? Lead + 2.2f * TurnRadius : Lead);
                    steer = entry;
                    float along = Along(pos);
                    float facing = Vector3.Dot(Forward, run.Dir);
                    bool lined = along > -Lead * 2.5f && along < 0.5f && Perp(pos) < 1.5f && facing > 0.8f;
                    bool atEntry = Flat(entry - pos).magnitude < TurnRadius * 0.8f;
                    if (lined || (atEntry && facing > 0.4f)) SetPhase(Phase.Run);
                    else if (atEntry) wideEntry = true;
                    else if (phaseTime > 12f) SetPhase(Phase.Patrol);
                    break;
                }
                case Phase.Run:
                {
                    float along = Along(pos);
                    steer = run.Start + run.Dir * (along + 2.5f);
                    // Inside the planned segment (plus a margin), or past it while there are still monsters below.
                    bool inWindow = (along > -1.2f && along < run.Length + 1.2f) || (along > 0f && dropTimer <= dt && MonsterBelow(pos));
                    bool aligned = Perp(pos) < 1.2f && Vector3.Dot(Forward, run.Dir) > 0.9f;
                    dropTimer -= dt;
                    bool track = PlaneAscensions.OverTrack(pos);
                    dbgFrames++; if (inWindow) dbgWindow++; if (aligned) dbgAligned++; if (track) dbgTrack++;
                    if (bombs >= 1f && inWindow && aligned && dropTimer <= 0f && track)
                    {
                        Drop(pos);
                        dropTimer = DropInterval;
                    }
                    if ((along > run.Length + 1.5f && dropTimer < -DropInterval) || bombs < 1f || phaseTime > 12f) SetPhase(Phase.Patrol);
                    break;
                }
                default:
                {
                    // Circle the Radar until there is something worth a run.
                    Vector3 home = HomePoint;
                    Vector3 r = Flat(pos - home);
                    float ang = Mathf.Atan2(r.x, r.z) + 0.7f;
                    steer = home + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 6f;
                    if (planTimer <= 0f)
                    {
                        planTimer = 0.5f;
                        if ((bombs >= MinBombsForRun || bombs >= MaxBombs - 0.01f) && Plan(out RunPlan p))
                        {
                            run = p;
                            SetPhase(Phase.Approach);
                        }
                    }
                    break;
                }
            }
            Fly(steer, dt);
        }

        private int dbgFrames, dbgWindow, dbgAligned, dbgTrack;

        private bool wideEntry;

        /// <summary>A monster will be within the blast of a bomb dropped now (positions predicted for the fall time).</summary>
        private static bool MonsterBelow(Vector3 pos)
        {
            float fall = Mathf.Sqrt(2f * Mathf.Max(0.5f, pos.y) / AerialBomb.Gravity);
            foreach (Enemy e in Combat.EnemiesInRadius(pos, 4f))
                if (Flat(Future(e, fall) - pos).sqrMagnitude <= BlastRadius * BlastRadius * 1.4f) return true;
            return false;
        }

        private void SetPhase(Phase p)
        {
            wideEntry = false;
            if (ModConfig.DebugTools.Value)
            {
                Plugin.Log.LogInfo($"[bomber] {phase} -> {p} after {phaseTime:0.0}s at {transform.position}, bombs {bombs:0.0}; run start {run.Start} dir {run.Dir} len {run.Length:0.0} hits {run.Hits} score {run.Score:0.0}" +
                                   (phase == Phase.Run ? $"; run frames {dbgFrames} window {dbgWindow} aligned {dbgAligned} track {dbgTrack}" : ""));
                dbgFrames = dbgWindow = dbgAligned = dbgTrack = 0;
            }
            phase = p;
            phaseTime = 0f;
            planTimer = 0f;
        }

        private Vector3 Forward => new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        private float Along(Vector3 p) => Vector3.Dot(Flat(p - run.Start), run.Dir);
        private float Perp(Vector3 p)
        {
            Vector3 rel = Flat(p - run.Start);
            return (rel - run.Dir * Vector3.Dot(rel, run.Dir)).magnitude;
        }

        private void Fly(Vector3 target, float dt)
        {
            Vector3 to = Flat(target - transform.position);
            float rate = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                float desired = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                rate = Mathf.Clamp(Mathf.DeltaAngle(yaw, desired) * 3f, -TurnRate, TurnRate);
                yaw += rate * dt;
            }
            bank = Mathf.Lerp(bank, -rate / TurnRate * 30f, dt * 3f);
            Vector3 pos = transform.position + Forward * Speed * dt;
            float climb = Mathf.Clamp((Altitude - pos.y) * 1.5f, -2f, 2f);
            pos.y += climb * dt;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(-climb * 4f, yaw, 0f);
            if (plane.artTransform != null) plane.artTransform.localRotation = Quaternion.Euler(0f, 0f, bank);
        }

        private void Drop(Vector3 pos)
        {
            bombs -= 1f;
            int damage = Mathf.RoundToInt(plane.damage * BombDamageFactor);
            AerialBomb.Drop(pos + Vector3.down * 0.25f, Forward, plane, damage, BlastRadius);
        }

        // ------------------------------------------------------------------ run planning

        private readonly List<Enemy> enemies = new List<Enemy>();
        private Vector3[] pts = new Vector3[MaxCandidates];
        private Vector3[] dirs = new Vector3[MaxCandidates];
        private float[] weights = new float[MaxCandidates];
        private readonly List<KeyValuePair<float, float>> proj = new List<KeyValuePair<float, float>>();
        private static readonly Comparison<KeyValuePair<float, float>> ByAlong = (a, b) => a.Key.CompareTo(b.Key);

        /// <summary>Finds a good straight bombing run (see class summary). False if there is nothing to bomb.</summary>
        private bool Plan(out RunPlan best)
        {
            best = default;
            best.Score = float.MinValue;
            if (SpawnManager.instance == null) return false;
            Vector3 pos = transform.position, home = HomePoint;
            float range = RadarRange;

            enemies.Clear();
            foreach (Enemy e in SpawnManager.instance.currentEnemies)
                if (Combat.IsAlive(e) && Flat(e.transform.position - home).sqrMagnitude <= range * range) enemies.Add(e);
            if (enemies.Count == 0) return false;
            if (enemies.Count > MaxCandidates)
            {
                enemies.Sort((a, b) => Flat(a.transform.position - pos).sqrMagnitude.CompareTo(Flat(b.transform.position - pos).sqrMagnitude));
                enemies.RemoveRange(MaxCandidates, enemies.Count - MaxCandidates);
            }

            int n = enemies.Count;
            Vector3 centroid = Vector3.zero;
            foreach (Enemy e in enemies) centroid += e.transform.position;
            centroid /= n;
            float fall = Mathf.Sqrt(2f * Altitude / AerialBomb.Gravity);
            float horizon = Mathf.Clamp(Flat(centroid - pos).magnitude / Speed + Lead / Speed + fall, 0.8f, 7f);
            float maxHp = 1f;
            for (int i = 0; i < n; i++) maxHp = Mathf.Max(maxHp, Combat.CurrentHp(enemies[i]));
            for (int i = 0; i < n; i++)
            {
                Enemy e = enemies[i];
                Vector3 now = Future(e, horizon), later = Future(e, horizon + 0.6f);
                pts[i] = Flat(now);
                Vector3 d = Flat(later - now);
                dirs[i] = d.sqrMagnitude > 0.0004f ? d.normalized : Vector3.zero;
                weights[i] = 1f + 0.5f * Combat.CurrentHp(e) / maxHp;
            }

            float length = Mathf.Max(2f, (Mathf.Min(bombs, MaxBombs) - 1f) * Spacing);
            for (int i = 0; i < n; i++)
                if (dirs[i] != Vector3.zero) Evaluate(pts[i], dirs[i], n, length, pos, ref best);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    Vector3 d = pts[j] - pts[i];
                    float m = d.magnitude;
                    if (m > 0.4f && m <= length) Evaluate(pts[i], d / m, n, length, pos, ref best);
                }
            return best.Hits > 0;
        }

        private static Vector3 Future(Enemy e, float t)
        {
            try { return e.pathfinder != null && e.pathfinder.currentWaypoint != null ? e.GetFuturePosition(t) : e.transform.position; }
            catch { return e.transform.position; }
        }

        private void Evaluate(Vector3 a, Vector3 d, int n, float length, Vector3 pos, ref RunPlan best)
        {
            proj.Clear();
            for (int k = 0; k < n; k++)
            {
                Vector3 rel = pts[k] - a;
                float al = Vector3.Dot(rel, d);
                if (rel.sqrMagnitude - al * al <= HitWidth * HitWidth) proj.Add(new KeyValuePair<float, float>(al, weights[k]));
            }
            if (proj.Count == 0) return;
            proj.Sort(ByAlong);

            // Sliding window of the given length over the sorted projections.
            float sum = 0f, bestSum = 0f;
            int lo = 0, bestLo = 0, bestHi = 0;
            for (int hi = 0; hi < proj.Count; hi++)
            {
                sum += proj[hi].Value;
                while (proj[hi].Key - proj[lo].Key > length) sum -= proj[lo++].Value;
                if (sum > bestSum) { bestSum = sum; bestLo = lo; bestHi = hi; }
            }
            if (bestSum + 0.01f < best.Score) return; // cannot win even before the approach cost

            float a0 = proj[bestLo].Key, a1 = proj[bestHi].Key;
            Vector3 heading = Forward;
            for (int s = 0; s < 2; s++)
            {
                Vector3 dir = s == 0 ? d : -d;
                Vector3 start = a + d * (s == 0 ? a0 : a1);
                Vector3 entry = start - dir * Lead;
                float cost = Flat(entry - pos).magnitude * 0.03f + (1f - Vector3.Dot(heading, dir)) * 0.25f;
                float score = bestSum - cost;
                if (score > best.Score)
                {
                    best.Score = score;
                    best.Start = start;
                    best.Dir = dir;
                    best.Length = Mathf.Max(0.5f, a1 - a0);
                    best.Hits = bestHi - bestLo + 1;
                }
            }
        }
    }

    /// <summary>A bomb falling straight down; explodes on the ground, hurting monsters in a small area.</summary>
    internal sealed class AerialBomb : MonoBehaviour
    {
        public const float Gravity = 12f;
        private static readonly Color[] FireColors =
            { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.55f, 0.15f), new Color(0.85f, 0.3f, 0.1f), new Color(0.3f, 0.28f, 0.27f) };
        private static int groundMask;

        private float velocity, groundY;
        private int damage, healthDamage, armorDamage, shieldDamage;
        private float slow, bleed, burn, poison, crit, stun, radius;

        public static void Drop(Vector3 at, Vector3 forward, BiPlane source, int damage, float radius, float scale = 1f)
        {
            var go = new GameObject("TR_Bomb");
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, forward);
            VoxAsset art = VoxelLibrary.Get("planes/bomb");
            if (art != null)
            {
                var mesh = new GameObject("Art");
                mesh.transform.SetParent(go.transform, false);
                mesh.transform.localScale = Vector3.one * 0.8f * scale;
                mesh.transform.localPosition = -art.Mesh.bounds.center * 0.8f * scale;
                mesh.AddComponent<MeshFilter>().sharedMesh = art.Mesh;
                mesh.AddComponent<MeshRenderer>().sharedMaterial = art.Material;
            }
            var b = go.AddComponent<AerialBomb>();
            b.damage = damage;
            b.radius = radius;
            b.healthDamage = source.healthDamage; b.armorDamage = source.armorDamage; b.shieldDamage = source.shieldDamage;
            b.slow = source.slowPercent; b.bleed = source.bleedPercent; b.burn = source.burnPercent; b.poison = source.poisonPercent;
            b.crit = source.critChance; b.stun = source.stunChance;
            if (groundMask == 0) groundMask = (1 << LayerMask.NameToLayer("Path")) | (1 << LayerMask.NameToLayer("Grass"));
            b.groundY = Physics.Raycast(at, Vector3.down, out RaycastHit hit, 40f, groundMask, QueryTriggerInteraction.Ignore)
                ? hit.point.y : 0.33f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            velocity += Gravity * dt;
            Vector3 p = transform.position;
            p.y -= velocity * dt;
            transform.position = p;
            if (p.y > groundY) return;
            p.y = groundY;
            Explode(p);
            Destroy(gameObject);
        }

        private void Explode(Vector3 p)
        {
            Fx.VoxelBurst(p + Vector3.up * 0.15f, FireColors, 14, 0.1f, 2f * radius, 0.5f, 1.2f);
            Kit.Boom(p, 0.4f * radius);
            foreach (Enemy e in Combat.EnemiesInRadius(p, radius))
            {
                try
                {
                    e.TakeDamage(TowerType.Radar, damage, healthDamage, armorDamage, shieldDamage, slow, bleed, burn, poison, crit, stun);
                }
                catch (Exception ex) { Plugin.Log.LogError($"Bomb hit failed: {ex}"); }
            }
        }
    }
}
