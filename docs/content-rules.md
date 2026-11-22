# Content rules (cards, treasures, castles)

These rules apply to **every** card and treasure, current and future. Check them whenever content is added or
changed. Code: `src/TreasureRun/Cards/CardRules.cs`, `Treasures/TreasureDraft.cs` (`IsOfferable`).

## 1. Max-health costs

Some options cost **max castle health** (e.g. the Blood Pact card, the Glass Cannon treasure).

1. **Declare the cost.** It must not be hidden inside the effect.
   - Cards: `CustomCardSpec.MaxHealthCost` (displayed points).
   - Treasures: `TreasureDef.MaxHealthCost` (a `Func<int>`, evaluated when offered).
2. **Pay through `HealthCost.Pay(cost)`.** Never subtract `GameManager.maxHealth` directly.
3. **Options you cannot afford are never offered.**
   - "Afford" means max health after paying would still be at least 1.
   - Unaffordable options are filtered out when a draw is rolled: level-up cards sit out that draw, and treasures are left out of the offer pool.
   - Because they are filtered rather than shown as locked, a level-up or treasure offer can never consist only of options you cannot pick.
4. **Castles that ignore health** (`CastleBehaviour.IgnoresHealth`, i.e. Glass Fortress) pay nothing: every max-health cost is free and always affordable.

## 2. Content tags and castle bans

Castles remove content by **tag** (`CastleBehaviour.BannedTags`), so new content only needs the right tag.

| Tag (`ContentTags`) | Meaning | Banned by |
|---|---|---|
| `kill-gold` | More gold from killing regular monsters. Boss-only bonuses (Bounty Hunters) don't count. | Gilded Vault |
| `castle-health` | Raises, restores or protects castle health (incl. the Mine) | Glass Fortress |
| `needs-ore` | Only useful with iron veins (Mine, Rich Veins) | Druid Grove |
| `needs-graves` | Only useful with graves (Haunted House, Seance, Restless Dead) | Druid Grove |

**Where tags come from:**
- Vanilla cards: `ContentTags.Of(UpgradeCard)`, by class or title.
- Custom cards: `CustomCards.Configure(id, s => s.Tags = ...)`.
- Treasures: the `tags` parameter, or `AddTags` in `TreasureRegistry.RegisterAll`.

**When adding content, ask:**
- Does it give kill gold?
- Does it touch castle health?
- Does it depend on map decorations?

**Banned cards:**
- They leave the pool for good.
- In Treasure Run, cards that were gated only behind a banned card become available from the start.
- Bans are re-checked on every draw (vanilla chains can re-add cards).

## 3. Castle-exclusive content

- Exclusive cards are added with `CustomCards.CastleChain` / `CastleSingle` (`Castles/CastleCards.cs`).
  - They also appear in **normal mode**; cards there are injected into the vanilla pool.
  - In normal mode their counters live in `CardCounters`.
- Exclusive treasures set `TreasureDef.Castle` (`Treasures/Effects/CastleTreasures.cs`).
  - Treasures only exist in Treasure Run.
- **Cards stay tame. Treasures, especially rare ones, can go wild.**

## 4. Starting cards

- `CastleBehaviour.StartingCards` grants cards for free at game start, e.g. Arcane Spire → Mana Siphon, Necropolis → Haunted House.
- Granted cards are never offered again.

## 5. Gold sources

- Gold is tagged by source (`GoldEconomy`: Kill, Income, Tree, Other). Castles and treasures multiply per source.
- Gold that must not count as kill gold, although it is paid while a monster dies, is wrapped in `GoldEconomy.Push(GoldSource.Other)`. Example: Bounty Hunters' boss bounty.

## 6. Ascensions

Ascensions are treasures with extra rules (`docs/ascension-plan.md`, code `src/TreasureRun/Ascensions`).

- **Register through `AscensionRegistry.Register`** (from `AscensionCatalog.RegisterAll`), never directly as a treasure. It sets:
  - the exclusion group `ascension:<target>`: at most one ascension per tower or building per run
  - the tags `ascension`, `ascension:<target>` and `<target>`, plus the def's own `Tags` (content tags for castle bans still apply)
  - availability: the target tower was built in this run (buildings: unlocked)
- **Follow-up cards** of an ascension (More Bomblets, More Chains, …) use `CustomCards.AscensionChain(ascensionId, ...)`, so they are only drawable while the ascension is owned. They stay tame (§3).
- The other content rules (max-health costs, tags, castle exclusivity) apply to ascensions like to any treasure.

## 7. Points of Interest

Points of Interest are map sections with their own function (`docs/poi-plan.md`, code `src/TreasureRun/Pois`).

- **Register through `PoiRegistry`** (`PoiDef`): icon, `MinWave`, a `SectionStyle` and the behaviour (`PoiSite`).
- **The section must work for every exit combination** (one, two or three exits in any direction). The generator's `Validate` and the offline tests check all seven.
- **No purely negative POIs:** a risky one must pay more when it works out.
- **Omens can't be removed;** a POI on a dead end is lost (the vanilla dead end stays).
- **Balance:** a spec in `ContentSpecs.Pois` (budget 15). Costs in max health still go through `HealthCost`; content tags and castle bans apply to POIs too.
- Treasure Run only (`TreasureRunMode.Active`).

