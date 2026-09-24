using DeftHands.Utils;
using System;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Compat
{
    /// <summary>
    /// Optional integration with HooksHangMore: lets mouse rotation drive its hook-swing axis
    /// (PickupableItemAddOns.heldRotationYOffset). Works purely via reflection, so it does
    /// nothing when HooksHangMore isn't installed.
    /// </summary>
    internal static class HooksHangMoreCompat
    {
        private const string AddOnsTypeName = "HooksHangMore.PickupableItemAddOns";
        private const float MinOffset = -90f;
        private const float MaxOffset = 90f;

        private static bool initialized;
        private static Type addOnsType;
        private static FieldInfo offsetField;

        /// <summary>
        /// Adds <paramref name="delta"/> to the item's hook-swing offset, if it has one.
        /// </summary>
        /// <param name="delta">Offset change in degrees; the result is clamped to ±90.</param>
        public static void AddHookSwing(PickupableItem item, float delta)
        {
            EnsureInitialized();
            if (offsetField == null)
                return;

            Component addOns = item.GetComponent(addOnsType);
            if (addOns == null)
                return;

            float current = (float)offsetField.GetValue(addOns);
            offsetField.SetValue(addOns, Mathf.Clamp(current + delta, MinOffset, MaxOffset));
        }

        /// <summary>
        /// Looks HooksHangMore up once. Must only be called after every plugin has loaded, since
        /// a missing mod is never looked up again.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;

            addOnsType = ReflectionUtils.FindType(AddOnsTypeName);
            if (addOnsType == null)
                return;

            offsetField = addOnsType.GetField(
                "heldRotationYOffset",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
}
