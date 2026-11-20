using DeftHands.Configuration;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Tells where the game's own rotation of the held item with the scroll wheel works.
    /// </summary>
    internal static class GameRotation
    {
        /// <summary>
        /// Gets whether the game's rotate key is kept for the game's scroll-wheel rotation: while Legacy Rotation Control
        /// is on or Mouse Rotation Control is off.
        /// </summary>
        public static bool KeepsRotateKey => WorksInHand;

        /// <summary>
        /// Gets whether the game's scroll-wheel rotation works in the hand: Legacy Rotation Control is on or Mouse
        /// Rotation Control is off.
        /// </summary>
        private static bool WorksInHand =>
            ModConfig.LegacyRotationEnabled.Value || !ModConfig.MouseRotationEnabled.Value;

        /// <summary>
        /// Returns whether the game's scroll-wheel rotation turns the held <paramref name="item"/> in the hand: Legacy
        /// Rotation Control is on or Mouse Rotation Control is off.
        /// </summary>
        public static bool TurnsInHand(PickupableItem item)
        {
            return WorksInHand;
        }
    }
}
