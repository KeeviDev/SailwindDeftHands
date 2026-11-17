using DeftHands.Runtime;
using HarmonyLib;

namespace DeftHands.Patches
{
    /// <summary>
    /// Keeps <see cref="RotationHandler"/>, <see cref="PushPullHandler"/>, <see cref="WallAttachmentRoll"/>,
    /// <see cref="WallAttachmentAdjustment"/>, <see cref="HeldItemAside"/>, <see cref="HeldItemVisibility"/>,
    /// <see cref="ExtendedHold"/>, <see cref="FurniturePlacement"/> and <see cref="ModifierPickupClickPatch"/>'s held
    /// item in sync with GoPointer's own pickup/drop lifecycle.
    /// </summary>
    internal static class HeldItemTracker
    {
        /// <summary>
        /// Registers <paramref name="item"/> as held with each of them, an item taken off a surface with the rotation
        /// and depth it hangs with when <see cref="HungItemPose.TakesHungPose"/>.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        /// <param name="wasAttached">Whether the item hung on a surface when picked up.</param>
        public static void SetCurrentItem(PickupableItem item, bool wasAttached)
        {
            bool wasHung = wasAttached && HungItemPose.HangsWhereHung(item as ShipItem);
            bool takesHungPose = wasAttached && HungItemPose.TakesHungPose(item as ShipItem);
            RotationHandler.GetInstance().SetCurrentItem(item);
            PushPullHandler.GetInstance().SetCurrentItem(item);
            WallAttachmentRoll.SetCurrentItem(item, takesHungPose);
            WallAttachmentAdjustment.SetCurrentItem(item);
            if (takesHungPose)
                HungItemPose.TakeOff(item as ShipItem);

            HeldItemAside.GetInstance().SetCurrentItem(item);
            HeldItemVisibility.SetCurrentItem(item);
            ExtendedHold.GetInstance().SetCurrentItem(item);
            FurniturePlacement.GetInstance().SetCurrentItem(item, wasHung);
            ModifierPickupClickPatch.SetCurrentItem(item);
        }
    }

    [HarmonyPatch(typeof(GoPointer), "PickUpItem")]
    public static class PickUpItemPatch
    {
        /// <summary>Records whether the item hangs on a surface before pickup detaches it.</summary>
        public static void Prefix(PickupableItem item, out bool __state)
        {
            ItemRigidbody body = item is ShipItem shipItem ? shipItem.GetItemRigidbody() : null;
            __state = body != null && HungItemPose.IsHanging(body);
        }

        /// <summary>Starts tracking the item if the pointer now holds it.</summary>
        public static void Postfix(GoPointer __instance, PickupableItem item, bool __state)
        {
            if (item != null && __instance.GetHeldItem() == item)
                HeldItemTracker.SetCurrentItem(item, __state);
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DropItem")]
    public static class DropItemPatch
    {
        /// <summary>Records the item about to be let go.</summary>
        public static void Prefix(GoPointer __instance, out ShipItem __state)
        {
            __state = __instance.GetHeldItem() as ShipItem;
        }

        /// <summary>
        /// Puts an item let go on a surface for items down in the pose it was shown in, remembers how an item let go on
        /// a wall, ceiling or floor was hung, then stops tracking the held item if the pointer no longer holds
        /// anything.
        /// </summary>
        public static void Postfix(GoPointer __instance, ShipItem __state)
        {
            FurniturePlacement.GetInstance().PutDownPlacedItem();
            if (__instance.GetHeldItem() != null)
                return;

            if (__state != null && __state.held == null)
                HungItemPose.Remember(__state);

            HeldItemTracker.SetCurrentItem(null, false);
        }
    }
}
