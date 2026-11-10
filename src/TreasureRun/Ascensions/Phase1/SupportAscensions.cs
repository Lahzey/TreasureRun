using System.Collections.Generic;
using HarmonyLib;
using TreasureRun.Audio;
using TreasureRun.Cards;
using TreasureRun.Core;
using TreasureRun.Treasures;
using TreasureRun.Voxels;
using UnityEngine;

namespace TreasureRun.Ascensions.Phase1
{
    /// <summary>
    /// Lone Scout (Lookout, Uncommon): a crossbowman on the roof shoots the marked monster 3 times a second (+1 per
    /// Swift Scouts card) for 1 damage. The vanilla mark bonus applies to these hits (Lookout damage and multipliers),
    /// so the tick deals the Lookout's damage (<see cref="LoneScoutLogic"/>).
    /// </summary>
    internal sealed class LoneScout : AscendedTower
    {
        public const string Id = "lone_scout";
        public const string SwiftScoutsCard = "swift_scouts";
        private const float ScoutHeight = 2.15f;
        private float accumulator;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Lone Scout", TowerName = "Scout's Perch", Rarity = Rarity.Uncommon, Kind = AscensionKind.Augment,
                Target = AscensionTarget.Lookout, Component = typeof(LoneScout), CardModel = "ascensions/lookout/lone_scout",
                Description = $"A scout shoots the marked monster {LoneScoutLogic.BaseTicksPerSecond} times a second for 1 damage, " +
                              "which the mark turns into the Lookout's damage.",
            });
        }

        public static void RegisterCards()
        {
            CustomCards.AscensionChain(Id, SwiftScoutsCard, "Swift Scouts", 3, "Tower - Lookout",
                _ => "Lone Scouts shoot once more per second.", () => { });
        }

        protected override void OnAscend() => SwapVanillaMesh("BaseArt", "ascensions/lookout/lone_scout");

        private void Update()
        {
            int due = LoneScoutLogic.TicksDue(ref accumulator, Time.deltaTime, CardCounters.Get(SwiftScoutsCard));
            Enemy e = Tower.currentTarget != null ? Tower.currentTarget.GetComponent<Enemy>() : null;
            if (!Combat.IsAlive(e) || e.mark != Tower) { accumulator = 0f; return; }
            if (due == 0) return;
            Vector3 scout = transform.position + Vector3.up * ScoutHeight;
            for (int i = 0; i < due && Combat.IsAlive(e); i++)
                e.TakeDamage(TowerType.Lookout, 1, 1, 1, 1, 0f, 0f, 0f, 0f, 0f, 0f);
            CosmeticShot.Fly("ascensions/lookout/scout_bolt", 0.45f, scout, e.transform, 0.12f);
            SoundLibrary.Play("ascensions/scout_tick", scout, 0.7f, 0.08f, 3);
        }
    }

    /// <summary>A purely visual projectile that flies to a target in a fixed time.</summary>
    internal sealed class CosmeticShot : MonoBehaviour
    {
        private Vector3 from;
        private Transform target;
        private Vector3 lastTarget;
        private float duration, t;

        public static void Fly(string model, float scale, Vector3 from, Transform target, float duration)
        {
            GameObject go = Phase1Kit.Model(model, scale);
            go.transform.position = from;
            var s = go.AddComponent<CosmeticShot>();
            s.from = from;
            s.target = target;
            s.lastTarget = target.position + Vector3.up * 0.3f;
            s.duration = duration;
        }

        private void Update()
        {
            if (target != null) lastTarget = target.position + Vector3.up * 0.3f;
            t += Time.deltaTime / duration;
            Vector3 p = Vector3.Lerp(from, lastTarget, Mathf.Min(1f, t));
            Vector3 d = lastTarget - from;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
            transform.position = p;
            if (t >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Sealed Fate (Monument, Epic): each stab of a spirit sword does 1 more damage than the last instead of 1 less,
    /// up to 3× the first stab (<see cref="SealedFateLogic"/>). Swords only leave when their victim dies; they grow
    /// as they get stronger.
    /// </summary>
    internal sealed class SealedFate : AscendedTower
    {
        public const string Id = "sealed_fate";

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Sealed Fate", TowerName = "Sealed Monument", Rarity = Rarity.Epic, Kind = AscensionKind.Replace,
                Target = AscensionTarget.Monument, Component = typeof(SealedFate), CardModel = "ascensions/monument/sealed_fate",
                Description = $"Each stab of a spirit sword does 1 more damage than the last instead of 1 less, up to " +
                              $"{SealedFateLogic.MaxFactor}× the first. Swords never let go before their victim dies.",
            });
        }

        protected override void OnAscend() => SwapVanillaMesh("BaseArt", "ascensions/monument/sealed_fate");
    }

    /// <summary>Marks a spirit sword fired by a Sealed Fate Monument; remembers its first stab's damage.</summary>
    internal sealed class SealedSword : MonoBehaviour
    {
        public int Original;
    }

    [HarmonyPatch]
    internal static class SupportAscensionPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(Projectile), nameof(Projectile.SetStats))]
        private static void SwordSetStatsPostfix(Projectile __instance, TowerType whoShotMe, int dmg)
        {
            if (whoShotMe != TowerType.Monument || !(__instance is SpiritSword)) return;
            if (AscensionRegistry.Owned(AscensionTarget.Monument)?.Id != SealedFate.Id) return;
            __instance.gameObject.AddComponent<SealedSword>().Original = Mathf.Max(1, dmg);
        }

        /// <summary>Vanilla stab with the damage counting up instead of down, and no "damage ≤ 0" exit.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(SpiritSword), "DealDamage", typeof(GameObject))]
        private static bool SwordDealDamagePrefix(SpiritSword __instance, GameObject target)
        {
            SealedSword sealedSword = __instance.GetComponent<SealedSword>();
            if (sealedSword == null) return true;
            Enemy e = target != null ? target.GetComponent<Enemy>() : null;
            if (e == null) return false;
            int bonus = 0;
            if (e.bleeding) bonus += __instance.healthSlaughter;
            if (e.burning) bonus += __instance.armorSlaughter;
            if (e.poisoned) bonus += __instance.shieldSlaughter;
            e.TakeDamage(__instance.shotBy, __instance.damage + bonus, __instance.healthDamage, __instance.armorDamage, __instance.shieldDamage,
                         __instance.slowPercent, __instance.bleedPercent, __instance.burnPercent, __instance.poisonPercent,
                         __instance.critChance, __instance.stunChance);
            __instance.damage = SealedFateLogic.NextDamage(__instance.damage, sealedSword.Original);
            if (__instance.hitSound != Sound.None) SFXManager.instance.PlaySound(__instance.hitSound, __instance.transform.position);
            if (__instance.artTransform != null)
            {
                float growth = (__instance.damage - sealedSword.Original) / (float)(SealedFateLogic.Cap(sealedSword.Original) - sealedSword.Original);
                __instance.artTransform.localScale = Vector3.one * (1f + 0.6f * Mathf.Clamp01(growth));
            }
            return false;
        }
    }

    /// <summary>
    /// Nest of Bats (Vampire Lair, Rare): on top of its pulse, the lair sends out 10 bats (+3 per More Bats card) that
    /// fly to monsters in range and bite once a second for the lair's damage. A bat picks a new target (nearer ones
    /// more likely) when its target dies or leaves range, and circles the lair when there is nothing to bite
    /// (<see cref="NestOfBatsLogic"/>). Bites cost no mana.
    /// </summary>
    internal sealed class NestOfBats : AscendedTower
    {
        public const string Id = "nest_of_bats";
        public const string MoreBatsCard = "more_bats";
        private const float Speed = 5f, BiteDistance = 0.35f, Scale = 0.35f;

        private sealed class Bat
        {
            public Transform Transform;
            public MeshFilter Filter;
            public Enemy Target;
            public float Bite, Phase;
            /// <summary>Own idle orbit (radius, angular speed incl. direction, height) and noise seed, so bats don't fly in file.</summary>
            public float OrbitRadius, OrbitSpeed, OrbitHeight, Seed;

            /// <summary>Smooth random wander in [-1, 1] per axis (Perlin noise, different per bat).</summary>
            public Vector3 Wander(float t, float rate) => new Vector3(
                Mathf.PerlinNoise(Seed, t * rate) * 2f - 1f,
                Mathf.PerlinNoise(Seed + 17.3f, t * rate) * 2f - 1f,
                Mathf.PerlinNoise(Seed + 41.7f, t * rate) * 2f - 1f);
        }

        private readonly List<Bat> bats = new List<Bat>();
        private readonly List<Enemy> candidates = new List<Enemy>();
        private readonly List<Vector3> candidatePositions = new List<Vector3>();
        private VoxAsset wingsUp, wingsDown;

        public int BatCount => bats.Count;

        public static void Register()
        {
            AscensionRegistry.Register(new AscensionDef
            {
                Id = Id, Name = "Nest of Bats", Rarity = Rarity.Rare, Kind = AscensionKind.Augment,
                Target = AscensionTarget.VampireLair, Component = typeof(NestOfBats), CardModel = "ascensions/vampire_lair/nest_of_bats",
                Description = $"{NestOfBatsLogic.BaseBats} bats fly out to monsters in range and bite each once a second for the lair's damage, " +
                              $"causing bleed equal to {NestOfBatsLogic.BiteBleed:P0} of it. They prefer nearby monsters.",
            });
        }

        public static void RegisterCards()
        {
            CustomCards.AscensionChain(Id, MoreBatsCard, "More Bats", 3, "Tower - Vampire Lair",
                _ => $"Each Nest of Bats gets {NestOfBatsLogic.BatsPerCard} more bats.", () => { });
        }

        protected override void OnAscend()
        {
            SwapVanillaMesh("BaseArt", "ascensions/vampire_lair/nest_of_bats");
            wingsUp = VoxelLibrary.Get("ascensions/vampire_lair/bat_up");
            wingsDown = VoxelLibrary.Get("ascensions/vampire_lair/bat_down");
            Plugin.Log.LogInfo($"[bats] Nest of Bats on {name}: models {(wingsUp != null ? "ok" : "MISSING")}/{(wingsDown != null ? "ok" : "MISSING")}");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (Bat b in bats) if (b.Transform != null) Destroy(b.Transform.gameObject);
            bats.Clear();
        }

        private Vector3 Roost => transform.position + Vector3.up * 2.4f;

        private void SpawnBat()
        {
            if (wingsUp == null) return;
            var go = new GameObject("TR_Bat");
            go.transform.position = Roost + Random.insideUnitSphere * 0.5f;
            var art = new GameObject("Art");
            art.transform.SetParent(go.transform, false);
            art.transform.localScale = Vector3.one * Scale;
            art.transform.localPosition = -wingsUp.Mesh.bounds.center * Scale;
            var filter = art.AddComponent<MeshFilter>();
            filter.sharedMesh = wingsUp.Mesh;
            art.AddComponent<MeshRenderer>().sharedMaterial = wingsUp.Material;
            bats.Add(new Bat
            {
                Transform = go.transform, Filter = filter, Phase = Random.Range(0f, 10f),
                OrbitRadius = Random.Range(0.5f, 1.6f),
                OrbitSpeed = Random.Range(0.6f, 1.8f) * (Random.value < 0.5f ? -1f : 1f),
                OrbitHeight = Random.Range(-0.4f, 0.8f),
                Seed = Random.Range(0f, 1000f),
            });
        }

        private void Update()
        {
            int want = NestOfBatsLogic.Count(CardCounters.Get(MoreBatsCard));
            while (bats.Count < want && wingsUp != null) SpawnBat();

            Vector3 lair = transform.position;
            float range = Tower.range;
            candidates.Clear();
            candidatePositions.Clear();
            if (SpawnManager.instance != null)
                foreach (Enemy e in SpawnManager.instance.currentEnemies)
                    if (Combat.IsAlive(e) && !NestOfBatsLogic.NeedsNewTarget(true, e.transform.position, lair, range))
                    {
                        candidates.Add(e);
                        candidatePositions.Add(e.transform.position);
                    }

            float dt = Time.deltaTime;
            foreach (Bat b in bats)
            {
                if (b.Transform == null) continue;
                bool alive = Combat.IsAlive(b.Target);
                if (NestOfBatsLogic.NeedsNewTarget(alive, alive ? b.Target.transform.position : lair, lair, range))
                {
                    int pick = NestOfBatsLogic.PickTarget(b.Transform.position, candidatePositions, GameRng.Instance);
                    b.Target = pick >= 0 ? candidates[pick] : null;
                    b.Bite = 0f;
                }

                float now = Time.time;
                Vector3 goal;
                if (b.Target != null)
                {
                    float a = now * 4f + b.Phase;
                    goal = b.Target.transform.position + Vector3.up * 0.55f
                         + new Vector3(Mathf.Cos(a), 0.3f * Mathf.Sin(a * 1.7f), Mathf.Sin(a)) * 0.22f + b.Wander(now, 1.5f) * 0.12f;
                }
                else
                {
                    // Idle: each bat its own orbit, plus a slow wander that stretches and bends it.
                    float a = now * b.OrbitSpeed + b.Phase;
                    float r = b.OrbitRadius * (1f + 0.35f * b.Wander(now, 0.4f).x);
                    goal = Roost + new Vector3(Mathf.Cos(a) * r, b.OrbitHeight, Mathf.Sin(a) * r) + b.Wander(now, 0.7f) * 0.45f;
                }
                Vector3 pos = b.Transform.position;
                Vector3 next = Vector3.MoveTowards(pos, goal, Speed * dt);
                Vector3 move = next - pos;
                if (move.sqrMagnitude > 1e-6f) b.Transform.rotation = Quaternion.Slerp(b.Transform.rotation, Quaternion.LookRotation(move, Vector3.up), dt * 10f);
                b.Transform.position = next;
                b.Filter.sharedMesh = ((int)((Time.time + b.Phase) * 14f) & 1) == 0 ? wingsUp.Mesh : wingsDown.Mesh;

                if (b.Target == null || (next - goal).sqrMagnitude > BiteDistance * BiteDistance) continue;
                b.Bite += dt;
                if (b.Bite < NestOfBatsLogic.BiteInterval) continue;
                b.Bite = 0f;
                Tower t = Tower;
                b.Target.TakeDamage(TowerType.VampireLair, t.damage, t.healthDamage, t.armorDamage, t.shieldDamage, t.slowPercent,
                                    t.bleedPercent + NestOfBatsLogic.BiteBleed, t.burnPercent, t.poisonPercent, t.critChance, t.stunChance);
                SoundLibrary.Play("ascensions/bat_bite", next, 0.6f, 0.1f, 3);
            }
        }
    }
}
