using System;

namespace TreasureRun.Cards
{
    /// <summary>
    /// A mod-defined level-up card. It is a real <see cref="UpgradeCard"/>, so the vanilla card UI,
    /// unlock chains and draw queueing work unchanged. Instances live on inactive GameObjects
    /// under the CardManager (cards never need to be active).
    /// </summary>
    public class CustomCard : UpgradeCard
    {
        public string Id;
        public Action OnPick;

        public override void Upgrade()
        {
            base.Upgrade();
            CardCounters.Increment(Id);
            try { OnPick?.Invoke(); }
            catch (Exception e) { Plugin.Log.LogError($"Custom card '{Id}' failed: {e}"); }
            Plugin.Log.LogInfo($"Card picked: {title}");
        }
    }
}
