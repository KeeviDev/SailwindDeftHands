using System;
using System.Reflection;

namespace DeftHands.Utils
{
    internal static class PickupableItemUtils
    {
        /// <summary>
        /// Returns true if the item's class overrides OnScroll, meaning scroll input drives
        /// item-specific behaviour (compass needle, spyglass zoom, fishing line) rather than
        /// rotation.
        /// </summary>
        public static bool HasCustomOnScroll(PickupableItem item)
        {
            Type itemType = item.GetType();
            MethodInfo onScrollMethod = itemType.GetMethod("OnScroll", BindingFlags.Public | BindingFlags.Instance);

            if (onScrollMethod == null)
                return false;

            return onScrollMethod.DeclaringType != typeof(PickupableItem);
        }
    }
}
