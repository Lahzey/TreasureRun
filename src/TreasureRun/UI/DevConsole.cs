using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TreasureRun.Cards;
using TreasureRun.Castles;
using TreasureRun.Core;
using TreasureRun.Treasures;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreasureRun.UI
{
    /// <summary>
    /// Cheat / dev command line (only with Debug.DebugTools enabled).
    ///  - In game: toggle with § (or ` / F10), type a command, Enter. Up/Down = history. "help" lists commands.
    ///  - Remote: every line written to &lt;plugin&gt;/console/in.txt is executed (the file is then deleted), and the
    ///    output is appended to console/out.txt followed by "#done &lt;command&gt;". Lets tools drive the game.
    /// </summary>
    internal sealed class DevConsole : MonoBehaviour
    {
        private sealed class Command
        {
            public string Name, Usage, Help;
            public bool NeedsGame;
            public Action<string[]> Run;
        }

        private static readonly Dictionary<string, Command> commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> lines = new List<string>();
        private static StringBuilder capture;

        private bool open;
        private bool focus;
        private string input = "";
        private readonly List<string> history = new List<string>();
        private int historyIndex;
        private Vector2 scroll;
        private GUIStyle logStyle;
        private float pollTimer;

        public static bool GodMode { get; private set; }

        private static string Dir
        {
            get
            {
                string dir = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "console");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private void Awake()
        {
            RegisterAll();
            if (ModConfig.DebugTools.Value)
            {
                Application.runInBackground = true; // remote commands keep working without focus
                try { File.Delete(Path.Combine(Dir, "in.txt")); } catch { /* ignore */ }
            }
        }

        // ------------------------------------------------------------------ input

        private void Update()
        {
            if (!ModConfig.DebugTools.Value) return;

            pollTimer -= Time.unscaledDeltaTime;
            if (pollTimer <= 0f)
            {
                pollTimer = 0.25f;
                PollFile();
            }
        }

        private void PollFile()
        {
            string path = Path.Combine(Dir, "in.txt");
            if (!File.Exists(path)) return;
            string[] cmds;
            try
            {
                cmds = File.ReadAllLines(path);
                File.Delete(path);
            }
            catch (IOException) { return; } // still being written

            var output = new StringBuilder();
            foreach (string raw in cmds)
            {
                string cmd = raw.Trim();
                if (cmd.Length == 0) continue;
                capture = new StringBuilder();
                Execute(cmd);
                output.Append(capture).Append("#done ").AppendLine(cmd);
                capture = null;
            }
            try { File.AppendAllText(Path.Combine(Dir, "out.txt"), output.ToString()); }
            catch (Exception e) { Plugin.Log.LogWarning($"Console output failed: {e.Message}"); }
        }

        private void OnGUI()
        {
            if (!ModConfig.DebugTools.Value) return;
            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.character == '§' || e.character == '`' || e.keyCode == KeyCode.F10 || e.keyCode == KeyCode.BackQuote))
            {
                open = !open;
                focus = open;
                e.Use();
                return;
            }
            if (!open) return;

            if (logStyle == null)
                logStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = false };

            float h = Mathf.Min(420f, Screen.height * 0.45f);
            GUI.Box(new Rect(0, 0, Screen.width, h), GUIContent.none);
            GUI.Box(new Rect(0, 0, Screen.width, h), GUIContent.none);
            GUILayout.BeginArea(new Rect(8, 6, Screen.width - 16, h - 12));
            scroll = GUILayout.BeginScrollView(scroll);
            int from = Mathf.Max(0, lines.Count - 300);
            for (int i = from; i < lines.Count; i++) GUILayout.Label(lines[i], logStyle);
            GUILayout.EndScrollView();

            if (e.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == "TR_Console")
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    string cmd = input.Trim();
                    input = "";
                    if (cmd.Length > 0)
                    {
                        history.Add(cmd);
                        historyIndex = history.Count;
                        Execute(cmd);
                        scroll.y = float.MaxValue;
                    }
                    e.Use();
                }
                else if (e.keyCode == KeyCode.UpArrow && history.Count > 0)
                {
                    historyIndex = Mathf.Max(0, historyIndex - 1);
                    input = history[historyIndex];
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow && history.Count > 0)
                {
                    historyIndex = Mathf.Min(history.Count, historyIndex + 1);
                    input = historyIndex < history.Count ? history[historyIndex] : "";
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    open = false;
                    e.Use();
                }
            }

            GUI.SetNextControlName("TR_Console");
            input = GUILayout.TextField(input, GUILayout.Height(26));
            GUILayout.EndArea();
            if (focus)
            {
                GUI.FocusControl("TR_Console");
                focus = false;
            }
        }

        // ------------------------------------------------------------------ execution

        public static void Print(string text)
        {
            foreach (string l in text.Split('\n'))
            {
                lines.Add(l);
                capture?.AppendLine(l);
            }
            Plugin.Log.LogInfo("[Console] " + text);
            // Output outside a file command (e.g. from a running scenario) goes to out.txt directly.
            if (capture == null)
            {
                try { File.AppendAllText(Path.Combine(Dir, "out.txt"), text + Environment.NewLine); } catch { /* ignore */ }
            }
            if (lines.Count > 1000) lines.RemoveRange(0, lines.Count - 1000);
        }

        public static void Execute(string commandLine)
        {
            lines.Add("> " + commandLine);
            string[] parts = Tokenize(commandLine);
            if (parts.Length == 0) return;
            if (!commands.TryGetValue(parts[0], out Command cmd))
            {
                Print($"Unknown command '{parts[0]}'. Type 'help'.");
                return;
            }
            if (cmd.NeedsGame && GameManager.instance == null)
            {
                Print("Only available in a game.");
                return;
            }
            try { cmd.Run(parts.Skip(1).ToArray()); }
            catch (Exception e) { Print($"Error: {e.Message}"); Plugin.Log.LogError(e); }
        }

        private static string[] Tokenize(string s)
        {
            var result = new List<string>();
            var cur = new StringBuilder();
            bool quoted = false;
            foreach (char c in s)
            {
                if (c == '"') { quoted = !quoted; continue; }
                if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (cur.Length > 0) { result.Add(cur.ToString()); cur.Clear(); }
                    continue;
                }
                cur.Append(c);
            }
            if (cur.Length > 0) result.Add(cur.ToString());
            return result.ToArray();
        }

        private static void Add(string name, string usage, string help, bool needsGame, Action<string[]> run)
        {
            commands[name] = new Command { Name = name, Usage = usage, Help = help, NeedsGame = needsGame, Run = run };
        }

        /// <summary>"+5" / "-5" are relative to current, "5" absolute.</summary>
        private static int Amount(string arg, int current)
        {
            if (arg.StartsWith("+") || arg.StartsWith("-")) return current + int.Parse(arg);
            return int.Parse(arg);
        }

        private static string Arg(string[] a, int i, string fallback = null) => i < a.Length ? a[i] : fallback;
        private static string Rest(string[] a, int from) => string.Join(" ", a.Skip(from));

        // ------------------------------------------------------------------ commands

        private static void RegisterAll()
        {
            if (commands.Count > 0) return;

            Add("help", "help [filter]", "Lists commands.", false, a =>
            {
                string f = Arg(a, 0, "");
                foreach (Command c in commands.Values.OrderBy(c => c.Name))
                    if (c.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
                        Print($"{c.Usage,-34} {c.Help}");
            });

            Add("state", "state", "Dumps the current game state.", false, a => Print(StateText()));

            Add("config", "config [key] [value]", "Lists mod settings, or shows/sets one (key: name or part of it).", false, a =>
            {
                var entries = ModConfig.File.Keys.Select(k => ModConfig.File[k])
                    .Where(e => a.Length == 0 || Matches(e.Definition.Key, a[0])).ToList();
                if (a.Length >= 2)
                {
                    if (entries.Count != 1) { Print($"{entries.Count} settings match '{a[0]}'; be more specific."); return; }
                    string value = Rest(a, 1);
                    if (entries[0].SettingType == typeof(bool))
                        value = value == "1" || value.Equals("on", StringComparison.OrdinalIgnoreCase) ? "true"
                              : value == "0" || value.Equals("off", StringComparison.OrdinalIgnoreCase) ? "false" : value;
                    entries[0].SetSerializedValue(value);
                }
                foreach (var e in entries) Print($"{e.Definition.Section}.{e.Definition.Key} = {e.GetSerializedValue()}");
            });

            Add("gold", "gold <n|+n|-n>", "Sets or changes gold (bypasses gold multipliers).", true, a =>
            {
                ResourceManager rm = ResourceManager.instance;
                rm.gold = Amount(Arg(a, 0, "+1000"), rm.gold);
                rm.AddMoney(0);
                Print($"Gold: {rm.gold}");
            });

            Add("health", "health <n|+n|-n>", "Sets current castle health (displayed points).", true, a =>
            {
                GameManager gm = GameManager.instance;
                gm.health = Mathf.Clamp(Amount(Arg(a, 0, "+1"), gm.health / 10) * 10, 0, gm.maxHealth);
                gm.healthLoss = gm.health;
                gm.SetHealthBar();
                Print($"Health: {gm.health / 10}/{gm.maxHealth / 10}");
            });

            Add("maxhealth", "maxhealth <n|+n|-n>", "Sets max castle health (raw, ignores castle rules).", true, a =>
            {
                GameManager gm = GameManager.instance;
                gm.maxHealth = Mathf.Max(10, Amount(Arg(a, 0, "+1"), gm.maxHealth / 10) * 10);
                gm.health = Mathf.Min(gm.health, gm.maxHealth);
                gm.healthLoss = gm.health;
                gm.SetHealthBar();
                Print($"Health: {gm.health / 10}/{gm.maxHealth / 10}");
            });

            Add("mana", "mana <n|+n|-n>", "Sets current mana.", true, a =>
            {
                ResourceManager rm = ResourceManager.instance;
                rm.mana = Mathf.Clamp(Amount(Arg(a, 0, "+100"), rm.mana), 0, rm.maxMana);
                rm.SetManaBar();
                Print($"Mana: {rm.mana}/{rm.maxMana}");
            });

            Add("maxmana", "maxmana <n|+n|-n>", "Sets max mana.", true, a =>
            {
                ResourceManager rm = ResourceManager.instance;
                rm.maxMana = Mathf.Max(0, Amount(Arg(a, 0, "+100"), rm.maxMana));
                rm.mana = Mathf.Min(rm.mana, rm.maxMana);
                rm.SetManaBar();
                Print($"Mana: {rm.mana}/{rm.maxMana}");
            });

            Add("god", "god", "Toggles castle invulnerability.", false, a =>
            {
                GodMode = !GodMode;
                Print($"God mode {(GodMode ? "on" : "off")}.");
            });

            Add("speed", "speed <x>", "Sets the time scale (0 pauses).", false, a =>
            {
                Time.timeScale = float.Parse(Arg(a, 0, "1"), System.Globalization.CultureInfo.InvariantCulture);
                Print($"Time scale {Time.timeScale}.");
            });

            Add("wave", "wave [tileIndex]", "Places a new tile (starting the next wave) or starts the wave directly.", true, a =>
            {
                if (SpawnManager.instance.combat) { Print("A wave is already running."); return; }
                TileSpawnLocation[] spots = UnityEngine.Object.FindObjectsOfType<TileSpawnLocation>();
                int idx = int.Parse(Arg(a, 0, "0"));
                if (spots.Length > 0)
                {
                    spots[Mathf.Clamp(idx, 0, spots.Length - 1)].SpawnTile();
                    Print($"Tile placed ({spots.Length} spots), wave {SpawnManager.instance.level} started.");
                }
                else
                {
                    SpawnManager.instance.StartNextWave();
                    Print($"Wave {SpawnManager.instance.level} started.");
                }
            });

            Add("killall", "killall", "Kills every enemy (with gold and drops).", true, a =>
            {
                Enemy[] all = SpawnManager.instance.currentEnemies.Where(e => e != null && !e.dead).ToArray();
                foreach (Enemy e in all) e.Die();
                Print($"Killed {all.Length} enemies.");
            });

            Add("level", "level <n>", "Sets the current level (the next wave is n+1).", true, a =>
            {
                SpawnManager.instance.level = int.Parse(Arg(a, 0, "1"));
                Print($"Level {SpawnManager.instance.level}.");
            });

            Add("treasure", "treasure <id|name> [count]", "Grants a treasure (Treasure Run).", true, a =>
            {
                if (!TreasureRunMode.Active) { Print("Not a Treasure Run."); return; }
                int count = 1;
                string query = Rest(a, 0);
                if (a.Length > 1 && int.TryParse(a[a.Length - 1], out int n)) { count = n; query = Rest(a.Take(a.Length - 1).ToArray(), 0); }
                TreasureDef def = FindTreasure(query);
                if (def == null) { Print($"No treasure matches '{query}'."); return; }
                for (int i = 0; i < count; i++) TreasureInventory.Grant(def);
                Print($"Granted {def.Name} x{count}.");
            });

            Add("treasures", "treasures [filter]", "Lists treasures (* = owned, - = unavailable now).", false, a =>
            {
                string f = Rest(a, 0);
                foreach (TreasureDef d in TreasureRegistry.All.Where(d => f.Length == 0 || Matches(d.Id, f) || Matches(d.Name, f)))
                {
                    string mark = TreasureInventory.Owns(d.Id) ? "*" : TreasureRunMode.Active && !TreasureDraft.IsOfferable(d) ? "-" : " ";
                    Print($"{mark} {d.Id,-24} {d.Rarity,-10} {d.Name}{(d.Castle != null ? $"  [{d.Castle}]" : "")}");
                }
            });

            Add("cards", "cards [all] [filter]", "Lists the drawable card pool (or all cards in the scene).", true, a =>
            {
                bool all = Arg(a, 0) == "all";
                string f = Rest(a, all ? 1 : 0);
                IEnumerable<UpgradeCard> list = all
                    ? Resources.FindObjectsOfTypeAll<UpgradeCard>().Where(c => c.gameObject.scene.IsValid())
                    : CardManager.instance.availableCards;
                foreach (UpgradeCard c in list.Where(c => c != null && Matches(c.title, f)).OrderBy(c => c.title))
                    Print($"{(CardRules.IsDrawable(c) ? " " : "x")} {c.title,-30} {c.GetType().Name}");
            });

            Add("card", "card <title>", "Picks a card as if chosen in a level-up (no UI).", true, a =>
            {
                string q = Rest(a, 0);
                UpgradeCard card = CardManager.instance.availableCards.FirstOrDefault(c => c != null && string.Equals(c.title, q, StringComparison.OrdinalIgnoreCase))
                                   ?? CardManager.instance.availableCards.FirstOrDefault(c => c != null && Matches(c.title, q))
                                   ?? Resources.FindObjectsOfTypeAll<UpgradeCard>().FirstOrDefault(c => c.gameObject.scene.IsValid() && Matches(c.title, q));
                if (card == null) { Print($"No card matches '{q}'."); return; }
                CardRules.Grant(card);
                Print($"Picked {card.title}.");
            });

            Add("draw", "draw [n]", "Opens a level-up card draw.", true, a =>
            {
                if (a.Length > 0) CardManager.instance.DrawCards(int.Parse(a[0])); else CardManager.instance.DrawCards();
                Print("Drawing cards.");
            });

            Add("offer", "offer [size | id id ...]", "Opens a treasure offer (Treasure Run): rolled, or exactly the given treasures.", true, a =>
            {
                if (!TreasureRunMode.Active) { Print("Not a Treasure Run."); return; }
                if (a.Length == 0 || int.TryParse(a[0], out _))
                {
                    TreasureOfferUI.RequestOffer(int.Parse(Arg(a, 0, "0")));
                    Print("Treasure offer requested.");
                    return;
                }
                var defs = a.Select(FindTreasure).Where(d => d != null).ToList();
                if (defs.Count == 0) { Print("No treasure matches."); return; }
                TreasureOfferUI.RequestFixed(defs);
                Print("Offer: " + string.Join(", ", defs.Select(d => d.Name)));
            });

            Add("pick", "pick <slot>", "Clicks card slot n (0-based) of the open card panel.", true, a =>
            {
                CardManager.instance.ActivateCard(int.Parse(Arg(a, 0, "0")));
                Print("Picked slot.");
            });

            Add("buildings", "buildings", "Lists buildable prefabs (from all build buttons, locked ones too).", true, a =>
            {
                foreach (var b in BuildPrefabs()) Print($"{b.Key,-28} {b.Value.Value?.currentPrice}g");
            });

            Add("build", "build <name> <x> <z> [free=1]", "Builds at a grid cell (world x/z, castle = 0,0). Free unless free=0.", true, a =>
            {
                int x = int.Parse(Arg(a, 1, "2")), z = int.Parse(Arg(a, 2, "0"));
                BuildAt(Arg(a, 0, ""), x, z, Arg(a, 3, "1") == "0", out string message);
                Print(message);
            });

            Add("scenario", "scenario [name|all]", "Runs in-game test scenarios (results in out.txt); no name lists them.", true, a =>
            {
                if (a.Length == 0)
                {
                    foreach (Debugging.Scenario sc in Debugging.Scenarios.All) Print($"{sc.Name,-24} {sc.Description}");
                    return;
                }
                Debugging.Scenarios.Start(a[0]);
            });

            Add("sound", "sound <name> [volume]", "Plays a mod sound (sounds folder, e.g. ui/ascension_pick) at the camera target.", false, a =>
            {
                Audio.SoundLibrary.ClearCache();
                Audio.SoundLibrary.PlayUi(Arg(a, 0, "ui/ascension_pick"), float.Parse(Arg(a, 1, "1"), System.Globalization.CultureInfo.InvariantCulture));
                AudioClip clip = Audio.SoundLibrary.Get(Arg(a, 0, "ui/ascension_pick"));
                Print(clip != null ? $"Playing {clip.name}: {clip.length:0.00} s, {clip.channels} ch, {clip.frequency} Hz." : "Sound not found.");
            });

            Add("restart", "restart", "Reloads the game scene with the current settings (castle, lanes, mode).", false, a =>
            {
                Time.timeScale = 1f;
                Print("Restarting.");
                LevelLoader.instance.LoadLevel("GameScene");
            });

            Add("castle", "castle [id]", "Lists castles or selects one for the next game.", false, a =>
            {
                if (a.Length == 0)
                {
                    foreach (CastleDef c in CastleRegistry.All)
                        Print($"{(CastleRuntime.Selected == c ? "*" : " ")} {c.Id,-18} {c.Name}{(CastleRuntime.Active == c ? "  (active)" : "")}");
                    return;
                }
                CastleDef def = CastleRegistry.All.FirstOrDefault(c => Matches(c.Id, a[0]) || Matches(c.Name, Rest(a, 0)));
                if (def == null) { Print("Unknown castle."); return; }
                CastleRuntime.Selected = def;
                Print($"Selected {def.Name}.");
            });

            Add("start", "start [lanes] [castle] [tr|normal]", "Starts a new game from anywhere.", false, a =>
            {
                int lanes = Mathf.Clamp(int.Parse(Arg(a, 0, PlayerPrefs.GetInt("GameMode", 1).ToString())), 1, 4);
                PlayerPrefs.SetInt("GameMode", lanes);
                if (a.Length > 1)
                {
                    CastleDef def = CastleRegistry.All.FirstOrDefault(c => Matches(c.Id, a[1]));
                    if (def != null) CastleRuntime.Selected = def;
                }
                if (a.Length > 2) TreasureRunMode.Selected = a[2] != "normal";
                Time.timeScale = 1f;
                Print($"Starting: {lanes} lanes, {CastleRuntime.Selected.Name}, {(TreasureRunMode.Selected ? "Treasure Run" : "normal")}.");
                LevelLoader.instance.LoadLevel("GameScene");
            });

            Add("menu", "menu", "Returns to the main menu.", false, a =>
            {
                Time.timeScale = 1f;
                LevelLoader.instance.LoadLevel("MainMenu");
                Print("Loading main menu.");
            });

            Add("shot", "shot [name]", "Saves a screenshot to <plugin>/screens/<name>.png.", false, a =>
            {
                string dir = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".", "screens");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, Arg(a, 0, "shot") + ".png");
                ScreenCapture.CaptureScreenshot(file);
                Print($"Screenshot: {file}");
            });

            Add("chest", "chest [x] [z]", "Drops a treasure chest (default next to the castle).", true, a =>
            {
                GameObject prefab = SpawnManager.instance.treasureChest;
                Vector3 pos = new Vector3(float.Parse(Arg(a, 0, "2")), 0.4f, float.Parse(Arg(a, 1, "-2")));
                UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                Print("Chest dropped.");
            });

            Add("enemies", "enemies", "Lists the current enemies.", true, a =>
            {
                foreach (var g in SpawnManager.instance.currentEnemies.Where(e => e != null).GroupBy(e => e.name.Replace("(Clone)", "")))
                    Print($"{g.Count(),3} x {g.Key}  (hp {g.First().health})");
                Print($"Total {SpawnManager.instance.currentEnemies.Count}.");
            });

            Add("find", "find <name>", "Lists scene objects whose name contains the text (with position and layer).", false, a =>
            {
                string q = Rest(a, 0);
                int n = 0;
                foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (!go.scene.IsValid() || !Matches(go.name, q)) continue;
                    Print($"{PathOf(go.transform)}  pos {go.transform.position}  layer {LayerMask.LayerToName(go.layer)}  {(go.activeInHierarchy ? "" : "(inactive)")}");
                    if (++n >= 40) { Print("..."); break; }
                }
            });

            Add("shaders", "shaders [filter]", "Lists loaded shaders (what materials can use at runtime).", false, a =>
            {
                string f = Rest(a, 0);
                foreach (string s in Resources.FindObjectsOfTypeAll<Shader>().Select(s => s.name).Where(n => Matches(n, f)).Distinct().OrderBy(n => n))
                    Print(s);
            });

            Add("colliders", "colliders <objectName>", "Lists colliders on and under an object (bounds, layer, trigger).", false, a =>
            {
                string q = Rest(a, 0);
                GameObject go = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.activeInHierarchy && g.name == q)
                                ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.activeInHierarchy && Matches(g.name, q));
                if (go == null) { Print("Not found."); return; }
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                    Print($"{PathOf(c.transform)}  {c.GetType().Name}  bounds {c.bounds.min} - {c.bounds.max}  layer {LayerMask.LayerToName(c.gameObject.layer)}  trigger {c.isTrigger}  enabled {c.enabled}");
                Print($"UIManager clickable: {MaskNames(UIManager.instance != null ? UIManager.instance.clickableMask.value : 0)}");
                Print($"Buildable mask: {MaskNames(BuildingManager.instance != null ? BuildingManager.instance.buildableMask.value : 0)}");
            });

            Add("active", "active <objectName> <0|1>", "Shows/hides a scene object (e.g. UniverseLibCanvas for clean screenshots).", false, a =>
            {
                string name = Arg(a, 0, "");
                bool on = Arg(a, 1, "1") != "0";
                GameObject go = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.name == name)
                                ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && Matches(g.name, name));
                if (go == null) { Print("Not found."); return; }
                go.SetActive(on);
                Print($"{PathOf(go.transform)} {(on ? "shown" : "hidden")}.");
            });

            Add("towerui", "towerui <x> <z>", "Opens the popup of the tower or building nearest to a grid cell (like a click).", true, a =>
            {
                var at = new Vector3(float.Parse(Arg(a, 0, "0")), 0f, float.Parse(Arg(a, 1, "0")));
                MonoBehaviour target = FindObjectsOfType<MonoBehaviour>().Where(m => m is IBuildable)
                    .OrderBy(m => Vector3.SqrMagnitude(new Vector3(m.transform.position.x, 0f, m.transform.position.z) - at)).FirstOrDefault();
                if (target == null) { Print("No tower or building."); return; }
                ((IBuildable)target).SpawnUI();
                Print($"Opened {target.name} at {target.transform.position}.");
            });

            Add("click", "click", "Clicks the castle (opens its info popup like a mouse click).", true, a =>
            {
                CastleInfo.Show();
                Print("Castle popup opened.");
            });

            Add("flash", "flash", "Arcane Spire: plays the obliteration flash.", true, a =>
            {
                (CastleRuntime.Behaviour as ArcaneSpireBehaviour)?.DebugFlash();
                Print("Flash.");
            });

            Add("bounds", "bounds <objectName>", "Renderer bounds of matching active objects (to check overlaps).", false, a =>
            {
                string q = Rest(a, 0);
                int n = 0;
                foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>())
                {
                    if (!r.gameObject.scene.IsValid() || !r.gameObject.activeInHierarchy || !Matches(PathOf(r.transform), q)) continue;
                    Print($"{PathOf(r.transform)}  min {r.bounds.min}  max {r.bounds.max}");
                    if (++n >= 40) { Print("..."); break; }
                }
            });

            Add("treasuredoc", "treasuredoc", "Writes the treasure list (docs/treasures.md format) to console/treasures.md.", false, a =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Treasures").AppendLine();
                sb.AppendLine("Generated from the source (`src/TreasureRun/Treasures/Effects`, console command `treasuredoc`). " +
                              "Treasures in the `Starter` module are M2 placeholders and not representative of the intended design. " +
                              "Castle treasures only appear with their castle.").AppendLine();
                foreach (Rarity r in Enum.GetValues(typeof(Rarity)))
                {
                    var list = TreasureRegistry.All.Where(d => d.Rarity == r).OrderBy(d => d.Castle ?? "").ThenBy(d => d.Name).ToList();
                    if (list.Count == 0) continue;
                    sb.AppendLine().AppendLine($"## {r}").AppendLine();
                    sb.AppendLine("| Treasure | Effect | Module |").AppendLine("|---|---|---|");
                    foreach (TreasureDef d in list)
                    {
                        string module = d.Castle != null ? $"Castle ({CastleRegistry.Get(d.Castle)?.Name ?? d.Castle})" : d.Module;
                        sb.AppendLine($"| **{d.Name}** (`{d.Id}`) | {d.Description.Replace("|", "/")} | {module} |");
                    }
                }
                sb.AppendLine().AppendLine($"Total: {TreasureRegistry.All.Count}");
                string file = System.IO.Path.Combine(Dir, "treasures.md");
                File.WriteAllText(file, sb.ToString());
                Print($"Wrote {file}");
            });

            Add("zoom", "zoom <size> [x z]", "Orthographic camera size (vanilla 1-50) and optional focus point.", false, a =>
            {
                if (Camera.main != null) Camera.main.orthographicSize = float.Parse(Arg(a, 0, "10"), System.Globalization.CultureInfo.InvariantCulture);
                if (a.Length >= 3 && CameraController.instance != null)
                    CameraController.instance.transform.position = new Vector3(
                        float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture), 0f,
                        float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture));
                Print("Camera set.");
            });

            Add("closeui", "closeui", "Closes the open building/tower popup.", true, a =>
            {
                if (UIManager.instance != null && UIManager.instance.currentUI != null) UIManager.instance.CloseUI(UIManager.instance.currentUI);
                Print("Closed.");
            });

            Add("ghost", "ghost", "Necropolis: raises a ghost from the enemy farthest from the castle (without killing it).", true, a =>
            {
                var owner = CastleRuntime.Behaviour as NecropolisBehaviour;
                Enemy e = SpawnManager.instance.currentEnemies.Where(x => x != null && x.pathfinder != null)
                    .OrderByDescending(x => x.pathfinder.distanceFromEnd).FirstOrDefault(); // farthest: in the open, easy to see
                if (owner == null || e == null) { Print("Needs the Necropolis and an enemy."); return; }
                Print(RisenMinion.Raise(e, 50, owner) != null ? "Ghost raised." : "Failed.");
            });

            Add("siegeaim", "siegeaim", "Siege Workshop: prints the cannon's current ballistic solution.", true, a =>
            {
                var tower = UnityEngine.Object.FindObjectOfType<SiegeCastleTower>()?.Tower;
                Enemy e = tower != null && tower.currentTarget != null ? tower.currentTarget.GetComponent<Enemy>() : null;
                if (e == null) { Print("No target."); return; }
                Vector3 from = tower.muzzle.position;
                Ballistics.Solve(from, e.transform.position, out Vector3 v0, out float t0);
                Ballistics.SolveLeading(from, e, out Vector3 v1, out Vector3 aim);
                float pitch(Vector3 v) => Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;
                Print($"muzzle {from} target {e.transform.position} aim {aim}\nno lead: pitch {pitch(v0):0} speed {v0.magnitude:0.0} t {t0:0.00}\nlead: pitch {pitch(v1):0} speed {v1.magnitude:0.0}");
            });

            Add("planes", "planes", "Lists Radar planes: ascension, position, target, bomber/blimp state.", true, a =>
            {
                foreach (BiPlane p in UnityEngine.Object.FindObjectsOfType<BiPlane>())
                {
                    var asc = p.GetComponent<Ascensions.AscendedPlane>();
                    string extra = asc is Ascensions.CarpetBomber b ? $" bombs {b.Bombs} phase {b.PhaseName}"
                                 : asc is Ascensions.WarBlimp w ? " " + w.Status : "";
                    Print($"{(asc != null ? asc.GetType().Name : "BiPlane")} at {p.transform.position} target {(p.target != null ? p.target.name : "-")} " +
                          $"owner {(asc?.Owner != null ? asc.Owner.transform.position.ToString() : "?")} rps {p.rps:0.000} speed {p.speed:0.0}{extra}");
                }
            });

            Add("deckbuild", "deckbuild <name>", "War Blimp: builds a tower on the first free blimp deck (free).", true, a =>
            {
                var prefabs = BuildPrefabs();
                string q = Arg(a, 0, "");
                var match = prefabs.FirstOrDefault(p => string.Equals(p.Key, q, StringComparison.OrdinalIgnoreCase));
                if (match.Value.Key == null) match = prefabs.FirstOrDefault(p => Matches(p.Key, q));
                if (match.Value.Key == null) { Print($"No building matches '{q}'."); return; }
                var deck = Ascensions.BlimpDeck.All.FirstOrDefault(d => d.IsFree);
                if (deck == null) { Print("No free blimp deck."); return; }
                if (!Ascensions.BlimpDeck.CanCarry(match.Value.Key)) { Print($"{match.Key} cannot go on a deck."); return; }
                GameObject go = deck.Build(match.Value.Key);
                Tower t = go.GetComponent<Tower>();
                Print($"Built {match.Key} on the blimp deck at {go.transform.position} (damage {t?.damage}, range {t?.range:0.0}).");
            });

            Add("castleinfo", "castleinfo", "Opens the castle info panel (same as clicking the castle).", true, a =>
            {
                CastleInfo.Show();
                Print(CastleInfo.BuildText());
            });
        }

        private static bool Matches(string text, string query) =>
            !string.IsNullOrEmpty(text) && (query.Length == 0 || text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

        private static TreasureDef FindTreasure(string q) =>
            TreasureRegistry.Get(q)
            ?? TreasureRegistry.All.FirstOrDefault(d => string.Equals(d.Name, q, StringComparison.OrdinalIgnoreCase))
            ?? TreasureRegistry.All.FirstOrDefault(d => Matches(d.Id, q) || Matches(d.Name, q));

        /// <summary>
        /// Places a building from the build buttons (name or part of it) at a grid cell like the build menu would.
        /// Returns null (with the reason in <paramref name="message"/>) if the cell is not free grass.
        /// </summary>
        internal static GameObject BuildAt(string name, int x, int z, bool pay, out string message)
        {
            var prefabs = BuildPrefabs();
            var match = prefabs.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase));
            if (match.Value.Key == null) match = prefabs.FirstOrDefault(p => Matches(p.Key, name));
            if (match.Value.Key == null) { message = $"No building matches '{name}'. See 'buildings'."; return null; }
            if (!Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out RaycastHit hit, 80f, BuildingManager.instance.buildableMask, QueryTriggerInteraction.Ignore))
            { message = "Nothing below that cell."; return null; }
            if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Grass"))
            { message = $"Cell blocked by {hit.collider.name} (layer {LayerMask.LayerToName(hit.collider.gameObject.layer)})."; return null; }
            Vector3 pos = new Vector3(x, Mathf.Round(3f * hit.point.y) / 3f, z);
            GameObject go = UnityEngine.Object.Instantiate(match.Value.Key, pos, Quaternion.identity);
            go.GetComponent<IBuildable>()?.SetStats();
            if (pay) ResourceManager.instance.Spend(match.Value.Value.currentPrice);
            message = $"Built {match.Key} at {x},{z} (height +{Mathf.RoundToInt(pos.y * 3f) - 1}).";
            return go;
        }

        /// <summary>Build button prefab per name (key = prefab name), with its flyweight for the price.</summary>
        private static List<KeyValuePair<string, KeyValuePair<GameObject, TowerFlyweight>>> BuildPrefabs()
        {
            return Resources.FindObjectsOfTypeAll<BuildButtonUI>()
                .Where(b => b.gameObject.scene.IsValid() && b.tower != null)
                .GroupBy(b => b.tower.name)
                .Select(g => new KeyValuePair<string, KeyValuePair<GameObject, TowerFlyweight>>(
                    g.Key, new KeyValuePair<GameObject, TowerFlyweight>(g.First().tower, g.First().towerFlyweight)))
                .OrderBy(p => p.Key)
                .ToList();
        }

        private static string MaskNames(int mask) =>
            string.Join(", ", Enumerable.Range(0, 32).Where(i => (mask & (1 << i)) != 0).Select(i => $"{i}:{LayerMask.LayerToName(i)}"));

        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        private static string StateText()
        {
            var sb = new StringBuilder();
            sb.Append($"scene {SceneManager.GetActiveScene().name}, time scale {Time.timeScale}");
            if (GameManager.instance != null && ResourceManager.instance != null && SpawnManager.instance != null)
            {
                GameManager gm = GameManager.instance;
                ResourceManager rm = ResourceManager.instance;
                SpawnManager sm = SpawnManager.instance;
                sb.Append($"\ncastle {CastleRuntime.Active?.Name ?? "?"}, {(TreasureRunMode.Active ? "Treasure Run" : "normal")}, lanes {gm.gameMode}");
                sb.Append($"\nlevel {sm.level}, combat {sm.combat}, enemies {sm.currentEnemies.Count}, game over {gm.gameOver}");
                sb.Append($"\nhealth {gm.health / 10}/{gm.maxHealth / 10}, gold {rm.gold}, mana {rm.mana}/{rm.maxMana}");
                sb.Append($"\ncards in pool {CardManager.instance?.availableCards.Count}, drawing {CardManager.instance?.drawingCards}, offer open {TreasureOfferUI.IsShowing}");
                if (TreasureRunMode.Active) sb.Append($"\ntreasures {TreasureInventory.TotalOwned}: {string.Join(", ", TreasureRunMode.Run.OwnedTreasures)}");
                string info = CastleRuntime.Behaviour?.InfoText();
                if (!string.IsNullOrEmpty(info)) sb.Append("\n" + info);
            }
            return sb.ToString();
        }
    }
}
