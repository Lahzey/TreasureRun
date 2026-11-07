using TreasureRun.Core;
using UnityEngine;

namespace TreasureRun.UI
{
    /// <summary>Minimal IMGUI overlay showing Treasure Run state. Development aid; toggle via config.</summary>
    internal sealed class DevHud : MonoBehaviour
    {
        private GUIStyle style;

        private void OnGUI()
        {
            if (!TreasureRunMode.Active || !ModConfig.ShowDevHud.Value) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                style.normal.textColor = new Color(1f, 0.78f, 0.25f);
            }

            RunState run = TreasureRunMode.Run;
            GUI.Label(new Rect(10f, Screen.height - 30f, 600f, 24f),
                $"Treasure Run  |  seed {run.Seed}  |  treasures {run.OwnedTreasures.Count}", style);
        }
    }
}
