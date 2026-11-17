using DeftHands.Configuration;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Tells where the game's own rotation of the held item with the scroll wheel works, and turns an item placed on a
    /// surface for items with it.
    /// </summary>
    internal static class GameRotation
    {
        /// <summary>Degrees the game turns a held item by per unit of scroll input with its rotate key held.</summary>
        private const float TurnPerInput = 4f;

        /// <summary>Degrees the game tilts a held item by per unit of scroll input.</summary>
        private const float TiltPerInput = 3f;

        /// <summary>
        /// Gets whether the game's rotate key is kept for the game's scroll-wheel rotation: on the surface for items
        /// the held item is placed on while Legacy Rotation Control is on, and anywhere else while Legacy Rotation
        /// Control is on or Mouse Rotation Control is off.
        /// </summary>
        public static bool KeepsRotateKey =>
            FurniturePlacement.IsPlacingHeldItem ? ModConfig.LegacyRotationEnabled.Value : WorksInHand;

        /// <summary>
        /// Gets whether the game's scroll-wheel rotation works in the hand: Legacy Rotation Control is on or Mouse
        /// Rotation Control is off.
        /// </summary>
        private static bool WorksInHand =>
            ModConfig.LegacyRotationEnabled.Value || !ModConfig.MouseRotationEnabled.Value;

        /// <summary>
        /// Returns whether the game's scroll-wheel rotation turns the held <paramref name="item"/> in the hand: Legacy
        /// Rotation Control is on or Mouse Rotation Control is off, and the item is neither placed on a surface for
        /// items nor <see cref="WallPlacement.TurnsOnWall"/>.
        /// </summary>
        public static bool TurnsInHand(PickupableItem item)
        {
            return WorksInHand && !FurniturePlacement.IsPlacing(item) && !WallPlacement.TurnsOnWall(item);
        }

        /// <summary>
        /// Returns whether the game's scroll-wheel rotation turns the held <paramref name="item"/> placed on a surface
        /// for items: Legacy Rotation Control is on.
        /// </summary>
        public static bool TurnsOnFurniture(PickupableItem item)
        {
            return ModConfig.LegacyRotationEnabled.Value && FurniturePlacement.IsPlacing(item);
        }

        /// <summary>
        /// Turns the item placed on a surface for items around the surface's up by scroll <paramref name="input"/>
        /// given with the game's rotate key held.
        /// </summary>
        public static void TurnOnFurniture(float input)
        {
            FurniturePlacement placement = FurniturePlacement.GetInstance();
            placement.Turn(Quaternion.AngleAxis(input * TurnPerInput, placement.SurfaceUp));
        }

        /// <summary>
        /// Tilts the item placed on a surface for items around the level axis across the player's view by scroll
        /// <paramref name="input"/>.
        /// </summary>
        public static void TiltOnFurniture(float input)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            FurniturePlacement placement = FurniturePlacement.GetInstance();
            placement.Turn(Quaternion.AngleAxis(input * TiltPerInput, placement.GetTiltAxis(mainCamera.transform)));
        }
    }
}
