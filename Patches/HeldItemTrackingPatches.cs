using DeftHands.Runtime;
using HarmonyLib;

namespace DeftHands.Patches
{
    /// <summary>
    /// Keeps <see cref="RotationHandler"/> and <see cref="PushPullHandler"/>'s held item in sync
    /// with GoPointer's own pickup/drop lifecycle.
    /// </summary>
    internal static class HeldItemTracker
    {
        /// <param name="item">The item now held, or null if nothing is held.</param>
        public static void SetCurrentItem(PickupableItem item)
        {
            RotationHandler.GetInstance().SetCurrentItem(item);
            PushPullHandler.GetInstance().SetCurrentItem(item);
        }
    }

    [HarmonyPatch(typeof(GoPointer), "PickUpItem")]
    public static class PickUpItemPatch
    {
        public static void Postfix(PickupableItem item)
        {
            if (item != null)
                HeldItemTracker.SetCurrentItem(item);
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DropItem")]
    public static class DropItemPatch
    {
        public static void Prefix()
        {
            HeldItemTracker.SetCurrentItem(null);
        }
    }
}
