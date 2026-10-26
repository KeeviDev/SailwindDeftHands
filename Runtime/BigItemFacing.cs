using DeftHands.Configuration;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Keeps a held big item facing the same way in <see cref="RotationFrame"/> while the player looks up or down,
    /// when Keep Facing When Looking Up And Down is on, and while the player turns around, when Keep Facing When
    /// Turning Around is on. A rotation other code gives the item in the hand becomes its new facing.
    /// </summary>
    internal static class BigItemFacing
    {
        private static PickupableItem facingItem;

        /// <summary>The boat <see cref="facingInReference"/> is measured on, or null for the world.</summary>
        private static Transform frame;

        private static bool keepsFacingWhenLookingUpAndDown;

        private static bool keepsFacingWhenTurningAround;

        private static Quaternion facingInReference;

        private static Quaternion lastWrittenRotationInHand;

        /// <summary>
        /// Rewrites the rotation in the hand of the big item <paramref name="holder"/> carries so that the item keeps
        /// its facing, and folds the item's heldRotationOffset into it. Must be called before the holder places the
        /// item for the frame; calling it again in the same frame keeps the result.
        /// </summary>
        public static void KeepFacing(GoPointer holder)
        {
            PickupableItem heldItem = holder.GetHeldItem();
            if (GoPointerFields.BigItemLocalRot == null || !IsEnabled() || heldItem == null || !heldItem.big)
            {
                facingItem = null;
                return;
            }

            if (HasFacingChanged(holder, heldItem))
                CaptureFacing(holder, heldItem);

            lastWrittenRotationInHand =
                Quaternion.Inverse(holder.transform.rotation) * GetReferenceRotation(holder) * facingInReference;
            GoPointerFields.BigItemLocalRot(holder) = lastWrittenRotationInHand;
        }

        private static bool IsEnabled()
        {
            return ModConfig.KeepFacingWhenLookingUpAndDown.Value || ModConfig.KeepFacingWhenTurningAround.Value;
        }

        private static bool HasFacingChanged(GoPointer holder, PickupableItem heldItem)
        {
            return heldItem != facingItem
                || RotationFrame.Boat != frame
                || ModConfig.KeepFacingWhenLookingUpAndDown.Value != keepsFacingWhenLookingUpAndDown
                || ModConfig.KeepFacingWhenTurningAround.Value != keepsFacingWhenTurningAround
                || heldItem.heldRotationOffset != 0f
                || !GoPointerFields.BigItemLocalRot(holder).Equals(lastWrittenRotationInHand);
        }

        /// <summary>
        /// Takes the facing the holder would give <paramref name="heldItem"/> this frame as the facing to keep, in the
        /// current frame and with the current settings, and resets the item's heldRotationOffset.
        /// </summary>
        private static void CaptureFacing(GoPointer holder, PickupableItem heldItem)
        {
            Quaternion rotationInHand = Quaternion.AngleAxis(heldItem.heldRotationOffset, Vector3.right) * GoPointerFields.BigItemLocalRot(holder);
            Quaternion facing = holder.transform.rotation * rotationInHand;

            facingItem = heldItem;
            frame = RotationFrame.Boat;
            keepsFacingWhenLookingUpAndDown = ModConfig.KeepFacingWhenLookingUpAndDown.Value;
            keepsFacingWhenTurningAround = ModConfig.KeepFacingWhenTurningAround.Value;
            facingInReference = Quaternion.Inverse(GetReferenceRotation(holder)) * facing;
            heldItem.heldRotationOffset = 0f;
        }

        /// <summary>
        /// Returns the rotation the kept facing is measured against: the frame's, turned with the player's heading
        /// unless the facing is kept when turning around, and tilted with the player's view up or down unless it is
        /// kept when looking up and down.
        /// </summary>
        private static Quaternion GetReferenceRotation(GoPointer holder)
        {
            Vector3 up = RotationFrame.Up;
            Vector3 levelRight = RotationFrame.Flatten(holder.transform.right);
            Vector3 headingForward = Vector3.Cross(levelRight, up);

            Quaternion reference = keepsFacingWhenTurningAround
                ? RotationFrame.RotationOf(frame)
                : Quaternion.LookRotation(headingForward, up);
            if (keepsFacingWhenLookingUpAndDown)
                return reference;

            Vector3 viewForward = Vector3.ProjectOnPlane(holder.transform.forward, levelRight);
            float viewTilt = Vector3.SignedAngle(headingForward, viewForward, levelRight);
            return Quaternion.AngleAxis(viewTilt, levelRight) * reference;
        }
    }
}
