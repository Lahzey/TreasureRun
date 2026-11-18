"""Extracts the vanilla monster roster and wave tiers from the AssetRipper export (balance framework step B0).

Run from the TreasureRun folder:
    python tools/balance/extract_monsters.py ../Ripped/ExportedProject/Assets

Writes:
  docs/reference/vanilla-monsters.md          the roster, readable
  src/TreasureRun/Balance/MonsterRoster.cs    the same data as C# (generated, do not edit)
"""
import os
import re
import sys

ASSETS = sys.argv[1] if len(sys.argv) > 1 else '../Ripped/ExportedProject/Assets'
GO = os.path.join(ASSETS, 'GameObject')
SCENE = os.path.join(ASSETS, 'Scenes', 'GameScene.unity')

ENEMY_FIELDS = ['level', 'damageToTower', 'bonusGoldDrop', 'dropsGold', 'baseHealth', 'baseArmor', 'baseShield',
                'baseSpeed', 'healthRegen', 'armorRegen', 'shieldRegen', 'bleedImmune', 'burnImmune', 'poisonImmune',
                'slowImmune']
TRIGGERS = ['Death', 'Spawn', 'NearEnd', 'ArmorBreak', 'ShieldBreak', 'Periodic']


def guid_of(path):
    with open(path + '.meta', encoding='utf-8') as f:
        m = re.search(r'guid: ([0-9a-f]+)', f.read())
    return m.group(1)


def blocks(text):
    """Splits a Unity YAML file into (fileID, body) blocks."""
    parts = re.split(r'^--- !u!\d+ &(-?\d+).*$', text, flags=re.M)
    return [(parts[i], parts[i + 1]) for i in range(1, len(parts) - 1, 2)]


def field(body, name):
    m = re.search(r'^  ' + name + r': (.*)$', body, flags=re.M)
    return m.group(1).strip() if m else None


def parse_prefab(path):
    text = open(path, encoding='utf-8').read()
    bl = dict(blocks(text))
    enemy = next((b for b in bl.values() if '  baseHealth:' in b), None)
    if enemy is None:
        return None
    e = {k: field(enemy, k) for k in ENEMY_FIELDS}
    e['deathSpawn'] = None
    m = re.search(r'deathObjectSpawn: \{fileID: \d+, guid: ([0-9a-f]+)', enemy)
    if m:
        e['deathSpawn'] = m.group(1)
    m = re.search(r'treasureObject: \{fileID: \d+, guid: ([0-9a-f]+)', enemy)
    e['treasure'] = m.group(1) if m else None
    sr = field(enemy, 'spawnRange')
    e['spawnRange'] = sr
    cries = []
    m = re.search(r'battleCries:\n((?:  - \{fileID: -?\d+\}\n)*)', enemy)
    if m:
        for cid in re.findall(r'fileID: (-?\d+)', m.group(1)):
            b = bl.get(cid)
            if b is None:
                continue
            spawns = re.findall(r'guid: ([0-9a-f]+)', b.split('enemiesToSpawn:')[1].split('heal:')[0]) if 'enemiesToSpawn:' in b else []
            heal = re.search(r'heal: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}', b)
            cries.append({
                'trigger': TRIGGERS[int(field(b, 'myTrigger'))],
                'radius': float(field(b, 'cryRadius')),
                'fortify': float(field(b, 'fortifyTime')),
                'haste': float(field(b, 'hastePercentage')),
                'spawns': spawns,
                'heal': tuple(float(x) for x in heal.groups()) if heal else (0, 0, 0),
                'teleport': int(field(b, 'teleport') or 0),
            })
    e['cries'] = cries
    return e


def num(v):
    f = float(v)
    return int(f) if f == int(f) else f


def main():
    roster = {}
    names = {}
    for fn in sorted(os.listdir(GO)):
        if not fn.endswith('.prefab'):
            continue
        p = os.path.join(GO, fn)
        g = guid_of(p)
        names[g] = fn[:-7]
        e = parse_prefab(p)
        if e is not None:
            roster[g] = e

    scene = open(SCENE, encoding='utf-8').read()
    sm = next(b for _, b in blocks(scene) if '  level15Boss:' in b)

    def lst(name):
        m = re.search(r'^  ' + name + r':\n((?:  - .*\n)*)', sm, flags=re.M)
        if not m:
            return []
        return [re.search(r'guid: ([0-9a-f]+)', l).group(1) if 'guid' in l else l[4:].strip()
                for l in m.group(1).splitlines()]

    def one(name):
        return re.search(r'guid: ([0-9a-f]+)', field(sm, name)).group(1)

    groups = [('tier1', 1, '1', lst('tier1'), None)]
    for t in (2, 3, 4):
        for c in (1, 2, 3):
            groups.append((f'class{t}{c}', t, f'{t}.{c}', lst(f'class{t}{c}'), lst(f'names{t}{c}')))
    bosses = [('level15Boss', 15, one('level15Boss'))]
    for t, lvl in ((2, 25), (3, 35), (4, 45)):
        for c, g in enumerate(lst(f'tier{t}Bosses'), 1):
            bosses.append((f'tier{t}Bosses[{c}]', lvl, g))
    bosses.append(('endlessBoss', 50, one('endlessBoss')))

    used = set()
    for _, _, _, gs, _ in groups:
        used.update(gs)
    for _, _, g in bosses:
        used.add(g)
    # death spawns and battle-cry spawns
    frontier = list(used)
    while frontier:
        g = frontier.pop()
        e = roster.get(g)
        if not e:
            continue
        extra = [e['deathSpawn']] + [s for c in e['cries'] for s in c['spawns']]
        for s in extra:
            if s and s in roster and s not in used:
                used.add(s)
                frontier.append(s)

    def has_effect(c):
        return c['haste'] or c['fortify'] or any(c['heal']) or c['spawns'] or c['teleport']

    def cry_text(c):
        parts = []
        if c['haste']:
            parts.append(f"haste {int(round(c['haste'] * 100))}%")
        if c['fortify']:
            parts.append(f"fortify {num(c['fortify'])} s")
        if any(c['heal']):
            parts.append('heal %s/%s/%s' % tuple(num(x) for x in c['heal']))
        if c['spawns']:
            parts.append('spawns ' + ', '.join(names.get(s, s) for s in c['spawns']))
        if c['teleport']:
            parts.append(f"teleport {c['teleport']}")
        return f"{c['trigger']} (r {num(c['radius'])}): " + ', '.join(parts)

    def row(name, e, extra=''):
        imm = ', '.join(k for k in ('bleed', 'burn', 'poison', 'slow') if e[k + 'Immune'] == '1')
        regen = '/'.join(e[k] for k in ('healthRegen', 'armorRegen', 'shieldRegen'))
        death = names.get(e['deathSpawn'], '') if e['deathSpawn'] in roster else ''
        cries = '; '.join(cry_text(c) for c in e['cries'] if has_effect(c))
        return (f"| {name}{extra} | {e['level']} | {e['baseHealth']}/{e['baseArmor']}/{e['baseShield']} | {num(e['baseSpeed'])} "
                f"| {regen if regen != '0/0/0' else ''} | {imm} | {e['damageToTower']} | {death} | {cries} |")

    out = ['# Vanilla monsters',
           '',
           'Generated by `tools/balance/extract_monsters.py` from the AssetRipper export (`Enemy` and `BattleCry` on the monster prefabs, `SpawnManager` in `GameScene`). Do not edit by hand.',
           '',
           '## Rules',
           '',
           '- **Wave budget:** wave *w* spends a budget of *w²* (`SpawnManager.Spawn(level * level)`). Each spawn walks the highest unlocked tier first (tier 4 from wave 36, tier 3 from 26, tier 2 from 16, tier 1 always) and, within a tier, its list from last to first: a monster with level ≤ *w* is taken with chance `tier / level`, and costs `max(round(level / tier), 1)` of the budget. If a tier takes nothing, the next lower tier is tried.',
           '- **Spawn timing:** the spawner loops over the spawn points, one monster per point, waiting `0.5 / spawn points` seconds after each: two monsters a second in total, spread over the lanes.',
           '- **Stats:** health, armor and shield are `base × multiplier × (1 + extra %)` (multiplier 1 normally, 2–4 for mini-bosses), speed is `base + speedBonus`.',
           '- **Tier classes:** tiers 2–4 each have three classes; one class per tier is picked at random when the run starts.',
           '- **Bosses:** waves 15, 25, 35 and 45 (the boss of the run\'s class), plus mini-bosses on odd waves from 17 (`tier[(wave − 17) / 2]` of waves 17–25 from tier 2, 27–35 from tier 3, 37–45 from tier 4), with the tier as stat multiplier, scale 1.5, +4 castle damage and a 50% chest chance.',
           '- **Gold:** a kill pays `level + number of tower types that damaged it + bonus + card bonuses`.',
           '- **Castle damage:** `damageToTower + extraTowerDamage` per leak.',
           '- **Endless (after wave 45):** +5% health, armor and shield per wave beyond 45 (cumulative), +0.1 speed and −2 gold per wave.',
           '',
           'Columns: level (spawn cost and gold), health/armor/shield, speed, regeneration per second (health/armor/shield), immunities, castle damage per leak, what spawns on death, battle cries (trigger, radius, effect; teleport moves the monster that many path cells forward).',
           '']
    gs_rows = []
    for key, tier, label, gs, nm in groups:
        out.append(f'## Tier {label}' + ('' if tier == 1 else f' (class {label.split(".")[1]})'))
        out.append('')
        out.append('| Monster | Level | Health/Armor/Shield | Speed | Regen | Immune | Castle dmg | On death | Battle cries |')
        out.append('|---|---|---|---|---|---|---|---|---|')
        for i, g in enumerate(gs):
            e = roster[g]
            mini = f" (mini-boss: {nm[i]})" if nm and i < len(nm) else ''
            out.append(row(names[g], e, mini))
        out.append('')
        gs_rows.append((key, tier, label, gs))
    out.append('## Bosses')
    out.append('')
    out.append('| Monster | Level | Health/Armor/Shield | Speed | Regen | Immune | Castle dmg | On death | Battle cries |')
    out.append('|---|---|---|---|---|---|---|---|---|')
    for key, lvl, g in bosses:
        out.append(row(f'{names[g]} (wave {lvl}, `{key}`)', roster[g]))
    out.append('')
    others = sorted(g for g in used if all(g not in gs for _, _, _, gs in gs_rows) and all(g != b for _, _, b in bosses))
    if others:
        out.append('## Spawned by others')
        out.append('')
        out.append('| Monster | Level | Health/Armor/Shield | Speed | Regen | Immune | Castle dmg | On death | Battle cries |')
        out.append('|---|---|---|---|---|---|---|---|---|')
        for g in others:
            out.append(row(names[g], roster[g]))
        out.append('')
    with open('docs/reference/vanilla-monsters.md', 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(out))

    # ---- C# ----
    def cs_name(g):
        return names[g]

    def cs_monster(g):
        e = roster[g]
        cries = []
        for c in e['cries']:
            if not has_effect(c):
                continue
            spawns = ', '.join(f'"{cs_name(s)}"' for s in c['spawns'] if s in roster)
            cries.append(f'new Cry(CryTrigger.{c["trigger"]}, {num(c["radius"])}f, {num(c["haste"])}f, {num(c["fortify"])}f, '
                         f'{num(c["heal"][0])}, {num(c["heal"][1])}, {num(c["heal"][2])}, new[] {{ {spawns} }})'.replace('new[] {  }', 'NoSpawns'))
        death = f'"{cs_name(e["deathSpawn"])}"' if e['deathSpawn'] in roster else 'null'
        imm = ' | '.join(f'Immunity.{k.capitalize()}' for k in ('bleed', 'burn', 'poison', 'slow') if e[k + 'Immune'] == '1') or 'Immunity.None'
        return (f'            new Monster("{cs_name(g)}", {e["level"]}, {e["baseHealth"]}, {e["baseArmor"]}, {e["baseShield"]}, '
                f'{num(e["baseSpeed"])}f, {e["healthRegen"]}, {e["armorRegen"]}, {e["shieldRegen"]}, {imm}, {e["damageToTower"]}, '
                f'{e["bonusGoldDrop"]}, {"true" if e["dropsGold"] == "1" else "false"}, {death}, new Cry[] {{ {", ".join(cries)} }}),')

    cs = ['// <auto-generated> by tools/balance/extract_monsters.py from the AssetRipper export. Do not edit by hand. </auto-generated>',
          'namespace TreasureRun.Balance',
          '{',
          '    public static partial class MonsterRoster',
          '    {',
          '        /// <summary>Every monster that can appear: the tier lists, bosses and what they spawn.</summary>',
          '        public static readonly Monster[] All =',
          '        {']
    for g in sorted(used, key=lambda g: names[g]):
        cs.append(cs_monster(g))
    cs += ['        };', '',
           '        /// <summary>Spawn lists in game order (the spawner walks them from last to first). Key: "1" or "tier.class".</summary>',
           '        public static readonly (string Key, int Tier, string[] Monsters, string[] MiniBossNames)[] Lists =',
           '        {']
    for key, tier, label, gs, in [(a, b, c, d) for a, b, c, d in gs_rows]:
        nm = next((n for k2, _, _, _, n in groups if k2 == key), None)
        ms = ', '.join(f'"{cs_name(g)}"' for g in gs)
        nms = ', '.join('"' + n.replace('"', '\\"') + '"' for n in (nm or []))
        cs.append(f'            ("{label}", {tier}, new[] {{ {ms} }}, new string[] {{ {nms} }}),')
    cs += ['        };', '',
           '        /// <summary>Bosses: wave, class (0 = any) and monster.</summary>',
           '        public static readonly (int Wave, int Class, string Monster)[] Bosses =',
           '        {']
    for key, lvl, g in bosses:
        c = int(key.split('[')[1][0]) if '[' in key else 0
        cs.append(f'            ({lvl}, {c}, "{cs_name(g)}"),')
    cs += ['        };', '    }', '}', '']
    os.makedirs('src/TreasureRun/Balance', exist_ok=True)
    with open('src/TreasureRun/Balance/MonsterRoster.cs', 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(cs))
    print(f'{len(used)} monsters, {len(gs_rows)} lists, {len(bosses)} bosses')


if __name__ == '__main__':
    main()
