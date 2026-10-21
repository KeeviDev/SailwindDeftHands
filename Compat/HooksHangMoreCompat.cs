using DeftHands.Utils;
using System;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Compat
{
    /// <summary>
    /// Reads and writes the hook-swing angle of items HooksHangMore manages. Reports no managed
    /// items when HooksHangMore isn't installed.
    /// </summary>
    internal static class HooksHangMoreCompat
    {
        /// <summary>HooksHangMore's own limit on the swing angle, in degrees either way.</summary>
        public const float MaxSwing = 90f;

        private const string AddOnsTypeName = "HooksHangMore.PickupableItemAddOns";

        private static bool initialized;
        private static Type addOnsType;
        private static FieldInfo swingField;

        /// <summary>
        /// Returns the component HooksHangMore keeps the item's swing angle on, or null if
        /// HooksHangMore doesn't manage the item or isn't installed.
        /// </summary>
        public static Component GetSwingOwner(PickupableItem item)
        {
            EnsureInitialized();
            return swingField != null ? item.GetComponent(addOnsType) : null;
        }

        /// <param name="owner">A component returned by <see cref="GetSwingOwner"/>.</param>
        public static float GetSwing(Component owner)
        {
            return (float)swingField.GetValue(owner);
        }

        /// <param name="owner">A component returned by <see cref="GetSwingOwner"/>.</param>
        /// <param name="swing">Swing angle in degrees; clamped to ±<see cref="MaxSwing"/>.</param>
        public static void SetSwing(Component owner, float swing)
        {
            swingField.SetValue(owner, Mathf.Clamp(swing, -MaxSwing, MaxSwing));
        }

        /// <summary>
        /// Looks HooksHangMore up on the first call only, keeping the result whether it was found
        /// or not. Must only be called after every plugin has loaded.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;

            addOnsType = ReflectionUtils.FindType(AddOnsTypeName);
            if (addOnsType == null)
                return;

            swingField = addOnsType.GetField(
                "heldRotationYOffset",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
}
