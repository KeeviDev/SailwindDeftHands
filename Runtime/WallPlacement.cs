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
        /// Gets whether held wall-attachable items turn on the surface they will attach to rather than in the hand:
        /// Easy Wall Hanging is on and runs, with Wall Attachment Rotation, Tilt or Depth.
        /// </summary>
        public static bool TurnsItemsOnWall =>
            FeatureGuard.IsOn(ModConfig.EasyWallHanging)
            && (ModConfig.WallAttachmentRotationEnabled.Value
                || ModConfig.WallAttachmentTilt.Value
                || ModConfig.WallAttachmentDepth.Value);

        /// <summary>
        /// Gets whether the player sets how held wall-attachable items hang: Wall Attachment Rotation or Depth is on,
        /// or <see cref="TurnsItemsOnWall"/>.
        /// </summary>
        public static bool AdjustsHangingPose =>
            ModConfig.WallAttachmentRotationEnabled.Value || ModConfig.WallAttachmentDepth.Value || TurnsItemsOnWall;

        /// <summary>
        /// Returns whether the held <paramref name="item"/> is being hung: it is wall-attachable, and the game shows
        /// where it will attach rather than where it would be set down on a table or other surface for items.
        /// </summary>
        public static bool IsBeingHung(PickupableItem item)
        {
            return item is ShipItem shipItem
                && shipItem.wallAttachment
                && ShowsPreview(shipItem)
                && !SurfaceAim.IsAimedAtSurfaceForItems(shipItem);
        }

        /// <summary>
        /// Returns whether the held <paramref name="item"/> turns on the surface it will attach to rather than in the
        /// hand: it <see cref="IsBeingHung"/>, and <see cref="TurnsItemsOnWall"/>.
        /// </summary>
        public static bool TurnsOnWall(PickupableItem item)
        {
            return TurnsItemsOnWall && IsBeingHung(item);
        }
    }
}
