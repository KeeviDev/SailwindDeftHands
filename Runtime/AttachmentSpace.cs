using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Converts between the visible world and the space the game computes surface attachment in.
    /// On a boat, an item's physics body lives on a static copy of the boat (its walk collider),
    /// so attachment rays, positions and rotations are in that copy's space rather than the
    /// visible boat's.
    /// </summary>
    internal static class AttachmentSpace
    {
        private static readonly FieldInfo OnBoatField = AccessTools.Field(typeof(ItemRigidbody), "onBoat");

        private static readonly AccessTools.FieldRef<ItemRigidbody, bool> IsBodyOnBoat =
            OnBoatField != null ? AccessTools.FieldRefAccess<ItemRigidbody, bool>(OnBoatField) : null;

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

        /// <summary>
        /// Returns the visible-world direction that's up in attachment space: the boat's up on a
        /// boat, world up otherwise.
        /// </summary>
        public static Vector3 UpInWorld(ShipItem item)
        {
            return IsOnBoat(item)
                ? item.currentActualBoat.rotation * Quaternion.Inverse(item.currentWalkCol.rotation) * Vector3.up
                : Vector3.up;
        }

        private static bool IsOnBoat(ShipItem item)
        {
            if (item.currentWalkCol == null || item.currentActualBoat == null)
                return false;

            return IsBodyOnBoat == null || IsBodyOnBoat(item.GetItemRigidbody());
        }
    }
}
