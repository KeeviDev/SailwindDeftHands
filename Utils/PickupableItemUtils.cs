using System;
using System.Collections.Generic;
using System.Reflection;

namespace DeftHands.Utils
{
    internal static class PickupableItemUtils
    {
        private static readonly Dictionary<Type, bool> CustomOnScrollByType = new Dictionary<Type, bool>();

        /// <summary>
        /// Returns whether the item's class overrides PickupableItem.OnScroll.
        /// </summary>
        public static bool HasCustomOnScroll(PickupableItem item)
        {
            Type itemType = item.GetType();
            if (!CustomOnScrollByType.TryGetValue(itemType, out bool hasCustomOnScroll))
            {
                hasCustomOnScroll = DeclaresCustomOnScroll(itemType);
                CustomOnScrollByType[itemType] = hasCustomOnScroll;
            }

            return hasCustomOnScroll;
        }

        private static bool DeclaresCustomOnScroll(Type itemType)
        {
            MethodInfo onScrollMethod = itemType.GetMethod("OnScroll", BindingFlags.Public | BindingFlags.Instance);
            return onScrollMethod != null && onScrollMethod.DeclaringType != typeof(PickupableItem);
        }
    }
}
