using DeftHands.Configuration;
using DeftHands.Utils;
using HarmonyLib;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Tells whether a held wall-attachable item is being placed: the game shows where it will attach to a wall,
    /// ceiling or floor, rather than drawing the item where it would be set down on a table or other surface for items.
    /// </summary>
    internal static class WallPlacement
    {
        private static readonly AccessTools.FieldRef<ShipItem, bool> InRangeOfWall =
            ReflectionUtils.FindFieldRef<ShipItem, bool>("inRangeOfWall");

        /// <summary>
        /// Returns whether the game shows where the held <paramref name="item"/> will attach.
        /// </summary>
        public static bool ShowsPreview(ShipItem item)
        {
            return InRangeOfWall != null && item != null && item.held != null && InRangeOfWall(item);
        }

        /// <summary>
        /// Returns whether the holder draws the held <paramref name="item"/> where it would set it down on a table or
        /// other surface for items.
        /// </summary>
        public static bool IsPlacedOnSurface(ShipItem item)
        {
            if (item == null || item.held == null || item.big || GoPointerFields.PointedAtButton == null)
                return false;

            GoPointerButton button = GoPointerFields.PointedAtButton(item.held);
            return button != null && button.allowPlacingItems;
        }

        /// <summary>
        /// Returns whether the held <paramref name="item"/> turns only around its axis into the surface: the game
        /// shows where it will attach, and Wall Attachment Rotation and Easy Wall Hanging are on.
        /// </summary>
        public static bool LocksRotationAxis(PickupableItem item)
        {
            return ModConfig.WallAttachmentRotationEnabled.Value
                && ModConfig.EasyWallHanging.Value
                && item is ShipItem shipItem
                && shipItem.wallAttachment
                && ShowsPreview(shipItem)
                && !IsPlacedOnSurface(shipItem);
        }
    }
}
