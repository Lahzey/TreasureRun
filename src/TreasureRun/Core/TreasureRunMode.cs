using System;
using UnityEngine;

namespace TreasureRun.Core
{
    /// <summary>
    /// Mode selection and run lifecycle.
    /// <para><see cref="Selected"/> is the menu choice (persisted like the vanilla challenge toggles).
    /// <see cref="Active"/> is snapshotted when GameScene starts loading and stays fixed for the whole run,
    /// so every patch can simply check <c>TreasureRunMode.Active</c>.</para>
    /// </summary>
    public static class TreasureRunMode
    {
        public const string SelectedPrefKey = "TR_TreasureRunMode";
        public const string GameSceneName = "GameScene";

        public static bool Selected
        {
            get => PlayerPrefs.GetInt(SelectedPrefKey, 0) == 1;
            set => PlayerPrefs.SetInt(SelectedPrefKey, value ? 1 : 0);
        }

        public static bool Active { get; private set; }

        public static RunState Run { get; private set; }

        public static event Action<RunState> RunStarted;
        public static event Action<RunState> RunEnded;

        /// <summary>Called right before a scene load begins (all loads go through LevelLoader.LoadLevel).</summary>
        internal static void OnSceneLoadRequested(string sceneName)
        {
            // Any scene change ends the current run, including a restart of GameScene.
            EndRun();

            if (sceneName == GameSceneName && Selected)
                BeginRun();
        }

        private static void BeginRun()
        {
            int gameMode = Mathf.Clamp(PlayerPrefs.GetInt("GameMode", 1), 1, 4);
            bool monsterMode = PlayerPrefs.GetInt("MonsterMode", 0) == 1;
            Run = RunState.CreateNew(gameMode, monsterMode);
            Active = true;
            Plugin.Log.LogInfo($"Treasure Run started (seed {Run.Seed}, game mode {gameMode}, monster mode {monsterMode}).");
            SafeInvoke(RunStarted, Run);
        }

        private static void EndRun()
        {
            if (!Active) return;
            var ended = Run;
            Active = false;
            Run = null;
            Plugin.Log.LogInfo("Treasure Run ended.");
            SafeInvoke(RunEnded, ended);
        }

        private static void SafeInvoke(Action<RunState> evt, RunState run)
        {
            if (evt == null) return;
            foreach (Action<RunState> handler in evt.GetInvocationList())
            {
                try { handler(run); }
                catch (Exception e) { Plugin.Log.LogError($"Run lifecycle handler failed: {e}"); }
            }
        }
    }
}
