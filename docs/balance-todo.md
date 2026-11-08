# Balance TODO

Things known to need tuning, collected for M9 (content & balance). All numbers are first drafts.

## Castles

| Castle | Concern | Knobs |
|---|---|---|
| **Arcane Spire** | Too tanky: mana is easy to come by, so the 1-health fragility rarely shows. It is meant to collapse once the mana economy falters. | `ArcaneSpireBehaviour.ManaPerDamage` (5), regen 5 mana per 2.5 s pulse, full mana at start, free Mana Siphon + 4 corner crystals. Ideas: 8–10 mana per damage, no passive regen, or a cost that scales with level. |
| Gilded Vault | Flat income `8 × L + L²` and 4% interest (cap `25 × L`) are rough guesses at replacing kill gold. | `GildedVaultBehaviour` constants, vault card bonuses |
| Siege Workshop | The castle cannon is a vanilla Cannon tower (20 damage, 20 rpm) with range 12 that levels like any tower; Cannon cards also buff it. 2× tower prices may be too harsh or too soft. | `SiegeCastleTower` constants, `TowerPriceMultiplier` |
| Necropolis | Rise radius 10.5 and 20% health; minions only rise from kills near the castle, so value depends on the build. | `NecropolisBehaviour` constants |
| Druid Grove | 20 health; regrowth `1 + level/15` trees per wave; +1 health per chop (+1 per Heartwood card, +2 with Golden Sap). Possibly too strong, since health regenerates without limit. | `DruidGroveBehaviour` constants |
| Colosseum | Champion health = replaced wave × 1.2 (strongest monster type); damage to castle `+4 + level/5`. Check early champions (level 5/10) against a starter build. | `ColosseumBehaviour.HealthFactor`, damage formula |
| Crow's Nest | +10 high ground is huge early; the start tile is fully blocked with ore. | `PlatformHeight` |
| Glass Fortress | Free max-health costs are the only upside. Probably needs more, e.g. a damage bonus or extra treasure choices. | — |
| Castle Town | 1.5× spawns at 2/3 gold each; income 1.5 × level per adjacent building. | `CastleTownBehaviour` constants |

## Treasures and cards

- Starter treasures are M2 placeholders. Review all of them in M9 (see `docs/treasures.md`).
- Siege Workshop treasures (Wall Cannons … Trebuchet Battery) and Druid Grove treasures (Sapling Seeds, Ironbark, Golden Sap) are untested beyond a smoke test.
