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
        /// Rotates a big item freely relative to the camera. Vertical movement pitches around
        /// the camera's right axis; horizontal movement rolls around its forward axis, or turns
        /// around its up axis while <paramref name="turnsInsteadOfRolling"/>. Wall-attachable items pause at upright
        /// while rolling through it.
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

            if (turnsInsteadOfRolling)
            {
                float turn = ModInput.ApplyInversion(horizontalInput, ModConfig.InvertTurn) * sensitivity;
                item.transform.Rotate(camera.up, turn, Space.World);
            }
            else
            {
                float roll = -ModInput.ApplyInversion(horizontalInput, ModConfig.InvertRoll) * sensitivity;
                Roll(item, camera.forward, roll);
            }

            item.transform.Rotate(camera.right, tiltInput * sensitivity, Space.World);

            if (WallAttachmentRoll.IsActive)
                WallAttachmentRoll.HoldBigItemUpright(item.transform);

            StoreRotation(item);
        }

        private static void Roll(PickupableItem item, Vector3 axis, float degrees)
        {
            if (WallAttachmentRoll.IsActive)
                WallAttachmentRoll.RollBigItem(item.transform, axis, degrees);
            else
                item.transform.Rotate(axis, degrees, Space.World);
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
