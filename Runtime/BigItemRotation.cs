using DeftHands.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Rotates a held big item, or a small item held like one, through the rotation its holder keeps for it in the
    /// hand.
    /// </summary>
    internal static class BigItemRotation
    {
        private static readonly FieldInfo BigItemLocalRotField = AccessTools.Field(typeof(GoPointer), "bigItemLocalRot");

        /// <summary>
        /// Rotates a big item freely relative to the camera. Vertical movement tilts it around the camera's right axis;
        /// horizontal movement rolls it around the camera's forward axis, or turns it around the camera's up axis while
        /// <paramref name="turnsInsteadOfRolling"/>. Keep Turning Level, Keep Rolling Level and Keep Tilting Level
        /// replace these axes with level ones in <see cref="RotationFrame"/>. Vertical movement is ignored while
        /// <see cref="IsTiltLocked"/>. Wall-attachable items pause at upright while rolling through it.
        /// </summary>
        /// <param name="horizontalInput">Raw horizontal input; inverted here per the active axis's setting.</param>
        /// <param name="tiltInput">Vertical input with vertical inversion already applied.</param>
        public static void Rotate(
            PickupableItem item,
            Camera mainCamera,
            float horizontalInput,
            float tiltInput,
            float sensitivity,
            bool turnsInsteadOfRolling)
        {
            Transform camera = mainCamera.transform;
            TurnToHolderPose(item);

            if (turnsInsteadOfRolling)
            {
                float turn = ModInput.ApplyInversion(horizontalInput, ModConfig.InvertTurn) * sensitivity;
                item.transform.Rotate(GetTurnAxis(camera), turn, Space.World);
            }
            else
            {
                float roll = -ModInput.ApplyInversion(horizontalInput, ModConfig.InvertRoll) * sensitivity;
                Roll(item, GetRollAxis(camera), roll);
            }

            if (!ModInput.IsTiltLocked())
                item.transform.Rotate(GetTiltAxis(camera), tiltInput * sensitivity, Space.World);

            if (WallAttachmentRoll.IsActive)
                WallAttachmentRoll.HoldBigItemUpright(item.transform);

            StoreRotation(item);
        }

        private static Vector3 GetTurnAxis(Transform camera)
        {
            return ModConfig.KeepTurningLevel.Value ? RotationFrame.Up : camera.up;
        }

        private static Vector3 GetRollAxis(Transform camera)
        {
            return ModConfig.KeepRollingLevel.Value
                ? Vector3.Cross(RotationFrame.Flatten(camera.right), RotationFrame.Up)
                : camera.forward;
        }

        private static Vector3 GetTiltAxis(Transform camera)
        {
            return ModConfig.KeepTiltingLevel.Value ? RotationFrame.Flatten(camera.right) : camera.right;
        }

        private static void Roll(PickupableItem item, Vector3 axis, float degrees)
        {
            if (WallAttachmentRoll.IsActive)
                WallAttachmentRoll.RollBigItem(item.transform, axis, degrees);
            else
                item.transform.Rotate(axis, degrees, Space.World);
        }

        /// <summary>
        /// Sets the big item's rotation to the one its holding GoPointer gives it this frame, with
        /// <see cref="BigItemFacing"/> applied first.
        /// </summary>
        private static void TurnToHolderPose(PickupableItem item)
        {
            if (BigItemLocalRotField == null)
                return;

            BigItemFacing.KeepFacing(item.held);
            Quaternion rotationInHand = (Quaternion)BigItemLocalRotField.GetValue(item.held);
            Quaternion offset = Quaternion.AngleAxis(item.heldRotationOffset, Vector3.right);
            item.transform.rotation = item.held.transform.rotation * offset * rotationInHand;
        }

        /// <summary>
        /// Stores the item's current rotation as the holding GoPointer's bigItemLocalRot, so it
        /// persists on following frames, and resets the item's heldRotationOffset.
        /// </summary>
        private static void StoreRotation(PickupableItem item)
        {
            if (BigItemLocalRotField == null)
                return;

            Quaternion holderRotation = item.held.transform.rotation;
            BigItemLocalRotField.SetValue(item.held, Quaternion.Inverse(holderRotation) * item.transform.rotation);
            item.heldRotationOffset = 0f;
        }
    }
}
