using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreasureRun.Core
{
    /// <summary>
    /// Replacement for PlayerPrefs reads/writes inside selected GameScene methods (see MetaKeyPatches).
    /// During a Treasure Run:
    ///  - skill-tree (meta) keys read as their default (0), so the skill tree has no effect,
    ///  - records ("Record*", "Class*") go to separate "TR_" keys,
    ///  - everything listed in <see cref="PassThrough"/> behaves as vanilla.
    /// Outside a Treasure Run every call is forwarded unchanged.
    /// </summary>
    public static class MetaKeys
    {
        public const string RecordPrefix = "TR_";

        /// <summary>Non-meta keys read by patched methods; these keep their real value.</summary>
        private static readonly HashSet<string> PassThrough = new HashSet<string>(StringComparer.Ordinal)
        {
            "GameMode", "MonsterMode", "SplitMode", "FirstPersonMode",
            "XP", "Prestige", "ShowDamageNumbers", "ShowConditionText", "PreviousTip",
        };

        private static bool IsRecordKey(string key) =>
            key.StartsWith("Record", StringComparison.Ordinal) || key.StartsWith("Class", StringComparison.Ordinal);

        private static bool PassesThrough(string key) =>
            PassThrough.Contains(key) || key.StartsWith(RecordPrefix, StringComparison.Ordinal);

        public static int GetInt(string key, int defaultValue)
        {
            if (!TreasureRunMode.Active || key == null) return PlayerPrefs.GetInt(key, defaultValue);
            if (IsRecordKey(key)) return PlayerPrefs.GetInt(RecordPrefix + key, defaultValue);
            if (PassesThrough(key)) return PlayerPrefs.GetInt(key, defaultValue);
            return defaultValue; // skill-tree key: ignored in Treasure Run
        }

        public static int GetInt(string key) => GetInt(key, 0);

        public static void SetInt(string key, int value)
        {
            if (TreasureRunMode.Active && key != null && IsRecordKey(key))
                PlayerPrefs.SetInt(RecordPrefix + key, value);
            else
                PlayerPrefs.SetInt(key, value);
        }
    }
}
