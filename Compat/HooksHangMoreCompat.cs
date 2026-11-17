using DeftHands.Utils;
using System;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Compat
{
    /// <summary>
    /// Finds the hooks HooksHangMore turns with its own controls and clears that turn. Reports no such hooks when
    /// HooksHangMore isn't installed.
    /// </summary>
    internal static class HooksHangMoreCompat
    {
        private const string AddOnsTypeName = "HooksHangMore.PickupableItemAddOns";

        private static bool initialized;
        private static Type addOnsType;
        private static FieldInfo turnField;

        /// <summary>
        /// Returns the component HooksHangMore keeps the turn of <paramref name="item"/> on, or null if HooksHangMore
        /// doesn't turn the item or isn't installed.
        /// </summary>
        public static Component GetTurnOwner(PickupableItem item)
        {
            EnsureInitialized();
            return turnField != null ? item.GetComponent(addOnsType) : null;
        }

        /// <summary>Returns the turn, in degrees, HooksHangMore keeps on <paramref name="owner"/>.</summary>
        /// <param name="owner">A component returned by <see cref="GetTurnOwner"/>.</param>
        public static float GetTurn(Component owner)
        {
            return (float)turnField.GetValue(owner);
        }

        /// <summary>
        /// Sets the turn HooksHangMore keeps on <paramref name="owner"/> back to none.
        /// </summary>
        /// <param name="owner">A component returned by <see cref="GetTurnOwner"/>.</param>
        public static void ClearTurn(Component owner)
        {
            turnField.SetValue(owner, 0f);
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

            FieldInfo field = addOnsType.GetField(
                "heldRotationYOffset",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            turnField = field != null && field.FieldType == typeof(float) ? field : null;
        }
    }
}
