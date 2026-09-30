using DeftHands.Runtime;
using HarmonyLib;

namespace DeftHands.Patches
{
    /// <summary>
    /// Keeps <see cref="RotationHandler"/>, <see cref="PushPullHandler"/> and
    /// <see cref="WallAttachmentRoll"/>'s held item in sync with GoPointer's own pickup/drop lifecycle.
    /// </summary>
    internal static class HeldItemTracker
    {
        /// <param name="item">The item now held, or null if nothing is held.</param>
        /// <param name="wasAttached">Whether the item was attached to a surface when picked up.</param>
        public static void SetCurrentItem(PickupableItem item, bool wasAttached)
        {
            RotationHandler.GetInstance().SetCurrentItem(item);
            PushPullHandler.GetInstance().SetCurrentItem(item);
            WallAttachmentRoll.SetCurrentItem(item, wasAttached);
        }
    }

    [HarmonyPatch(typeof(GoPointer), "PickUpItem")]
    public static class PickUpItemPatch
    {
        /// <summary>Records whether the item is attached to a surface before pickup detaches it.</summary>
        public static void Prefix(PickupableItem item, out bool __state)
        {
            __state = item is ShipItem shipItem && shipItem.GetItemRigidbody() != null && shipItem.GetItemRigidbody().attached;
        }

        public static void Postfix(PickupableItem item, bool __state)
        {
            if (item != null)
                HeldItemTracker.SetCurrentItem(item, __state);
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DropItem")]
    public static class DropItemPatch
    {
        public static void Prefix()
        {
            HeldItemTracker.SetCurrentItem(null, false);
        }
    }
}
