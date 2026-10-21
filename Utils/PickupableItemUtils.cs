using System;
using System.Reflection;

namespace DeftHands.Utils
{
    internal static class PickupableItemUtils
    {
        /// <summary>
        /// Returns whether the item's class overrides PickupableItem.OnScroll.
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
