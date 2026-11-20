using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;

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

            TrackInHeldItemAside(item);
            HeldItemVisibility.SetCurrentItem(item);
            TrackInExtendedHold(item);
            TrackInFurniturePlacement(item, wasHung);
            ModifierPickupClickPatch.SetCurrentItem(item);
        }

        private static void TrackInHeldItemAside(PickupableItem item)
        {
            if (!FeatureGuard.IsRunning(ModConfig.EasyWallHanging))
                return;

            try
            {
                HeldItemAside.GetInstance().SetCurrentItem(item);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.EasyWallHanging, error);
            }
        }

        private static void TrackInExtendedHold(PickupableItem item)
        {
            if (!FeatureGuard.IsRunning(ModConfig.ExtendedSmallItemsRotation))
                return;

            try
            {
                ExtendedHold.GetInstance().SetCurrentItem(item);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.ExtendedSmallItemsRotation, error);
            }
        }

        private static void TrackInFurniturePlacement(PickupableItem item, bool wasHung)
        {
            if (!FeatureGuard.IsRunning(ModConfig.NeatItemPlacement))
                return;

            try
            {
                FurniturePlacement.GetInstance().SetCurrentItem(item, wasHung);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.NeatItemPlacement, error);
            }
        }
    }

    [HarmonyPatch(typeof(GoPointer), "PickUpItem")]
    public static class PickUpItemPatch
    {
        /// <summary>Records whether the item hangs on a surface before pickup detaches it.</summary>
        public static void Prefix(PickupableItem item, out bool __state)
        {
            __state = false;
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                ItemRigidbody body = item is ShipItem shipItem ? shipItem.GetItemRigidbody() : null;
                __state = body != null && HungItemPose.IsHanging(body);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
            }
        }

        /// <summary>Starts tracking the item if the pointer now holds it.</summary>
        public static void Postfix(GoPointer __instance, PickupableItem item, bool __state)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                if (item != null && __instance.GetHeldItem() == item)
                    HeldItemTracker.SetCurrentItem(item, __state);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
            }
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DropItem")]
    public static class DropItemPatch
    {
        /// <summary>Records the item about to be let go.</summary>
        public static void Prefix(GoPointer __instance, out ShipItem __state)
        {
            __state = FeatureGuard.IsRunning(FeatureGuard.Core) ? __instance.GetHeldItem() as ShipItem : null;
        }

        /// <summary>
        /// Puts an item let go on a surface for items down in the pose it was shown in, remembers how an item let go on
        /// a wall, ceiling or floor was hung, then stops tracking the held item if the pointer no longer holds
        /// anything.
        /// </summary>
        public static void Postfix(GoPointer __instance, ShipItem __state)
        {
            PutDownPlacedItem();
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                if (__instance.GetHeldItem() != null)
                    return;

                if (__state != null && __state.held == null)
                    HungItemPose.Remember(__state);

                HeldItemTracker.SetCurrentItem(null, false);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
            }
        }

        private static void PutDownPlacedItem()
        {
            if (!FeatureGuard.IsRunning(ModConfig.NeatItemPlacement))
                return;

            try
            {
                FurniturePlacement.GetInstance().PutDownPlacedItem();
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.NeatItemPlacement, error);
            }
        }
    }
}
