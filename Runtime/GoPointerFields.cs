using DeftHands.Utils;
using HarmonyLib;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Accessors for private <see cref="GoPointer"/> fields. Each is null when the game has no field of that name and
    /// type.
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

        /// <summary>
        /// How far the holder has eased a newly picked up small item into the hand, from 0 to 1 and beyond.
        /// </summary>
        public static readonly AccessTools.FieldRef<GoPointer, float> TimerAfterPickup =
            ReflectionUtils.FindFieldRef<GoPointer, float>("timerAfterPickup");

        /// <summary>The button or item the holder points at, or null if it points at none.</summary>
        public static readonly AccessTools.FieldRef<GoPointer, GoPointerButton> PointedAtButton =
            ReflectionUtils.FindFieldRef<GoPointer, GoPointerButton>("pointedAtButton");
    }
}
