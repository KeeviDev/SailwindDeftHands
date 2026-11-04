using DeftHands.Utils;
using HarmonyLib;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Converts between the visible world and attachment space, the space the game computes
    /// surface attachment in: that of the static boat copy (walk collider) an item's physics body
    /// sits on while on a boat, and the world otherwise.
    /// </summary>
    internal static class AttachmentSpace
    {
        private static readonly AccessTools.FieldRef<ItemRigidbody, bool> IsBodyOnBoat =
            ReflectionUtils.FindFieldRef<ItemRigidbody, bool>("onBoat");

        /// <summary>Converts a visible-world point to attachment space.</summary>
        public static Vector3 ToAttachmentSpace(ShipItem item, Vector3 point)
        {
            return IsOnBoat(item)
                ? item.currentWalkCol.TransformPoint(item.currentActualBoat.InverseTransformPoint(point))
                : point;
        }

        /// <summary>Converts a visible-world rotation to attachment space.</summary>
        public static Quaternion ToAttachmentSpace(ShipItem item, Quaternion rotation)
        {
            return IsOnBoat(item)
                ? item.currentWalkCol.rotation * Quaternion.Inverse(item.currentActualBoat.rotation) * rotation
                : rotation;
        }

        /// <summary>Converts an attachment-space rotation to the visible world.</summary>
        public static Quaternion ToVisibleWorld(ShipItem item, Quaternion rotation)
        {
            return IsOnBoat(item)
                ? item.currentActualBoat.rotation * Quaternion.Inverse(item.currentWalkCol.rotation) * rotation
                : rotation;
        }

        /// <summary>
        /// Returns the visible-world direction that's up in attachment space: the boat's up on a
        /// boat, world up otherwise.
        /// </summary>
        public static Vector3 UpInWorld(ShipItem item)
        {
            return ToVisibleWorld(item, Quaternion.identity) * Vector3.up;
        }

        private static bool IsOnBoat(ShipItem item)
        {
            if (item.currentWalkCol == null || item.currentActualBoat == null)
                return false;

            return IsBodyOnBoat == null || IsBodyOnBoat(item.GetItemRigidbody());
        }
    }
}
