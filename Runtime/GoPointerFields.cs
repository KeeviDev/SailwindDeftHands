using DeftHands.Utils;
using HarmonyLib;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Accessors for the private <see cref="GoPointer"/> fields that keep a held big item's pose in the holder's space.
    /// Each is null when the game has no field of that name and type.
    /// </summary>
    internal static class GoPointerFields
    {
        /// <summary>Where the holder intends to keep the item's pivot.</summary>
        public static readonly AccessTools.FieldRef<GoPointer, Vector3> BigItemLocalPos =
            ReflectionUtils.FindFieldRef<GoPointer, Vector3>("bigItemLocalPos");

        /// <summary>Where the holder draws the item's pivot after pushing it out of colliders.</summary>
        public static readonly AccessTools.FieldRef<GoPointer, Vector3> DecolLocalPos =
            ReflectionUtils.FindFieldRef<GoPointer, Vector3>("decolLocalPos");

        /// <summary>The item's rotation in the holder's space before its heldRotationOffset is applied.</summary>
        public static readonly AccessTools.FieldRef<GoPointer, Quaternion> BigItemLocalRot =
            ReflectionUtils.FindFieldRef<GoPointer, Quaternion>("bigItemLocalRot");
    }
}
