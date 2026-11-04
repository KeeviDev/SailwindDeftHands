using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Tells whether the held item is aimed at a surface for items.
    /// </summary>
    internal static class SurfaceAim
    {
        /// <summary>
        /// Returns whether the held small <paramref name="item"/> is aimed at a table or other surface for items, where
        /// its holder draws it where it would set it down instead of in the hand.
        /// </summary>
        public static bool IsAimedAtSurfaceForItems(ShipItem item)
        {
            if (item == null || item.held == null || item.big || GoPointerFields.PointedAtButton == null)
                return false;

            GoPointerButton button = GoPointerFields.PointedAtButton(item.held);
            return button != null && button != item && button.allowPlacingItems;
        }
    }
}
