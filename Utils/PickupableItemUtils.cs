using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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

        /// <summary>
        /// Returns the heldRotationOffset of the item's prefab, or 0 for an item that has no prefab.
        /// </summary>
        public static float GetPrefabHeldRotationOffset(PickupableItem item)
        {
            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            GameObject[] prefabs = PrefabsDirectory.instance != null ? PrefabsDirectory.instance.directory : null;
            if (saveable == null || prefabs == null || saveable.prefabIndex < 0 || saveable.prefabIndex >= prefabs.Length)
                return 0f;

            GameObject prefab = prefabs[saveable.prefabIndex];
            PickupableItem prefabItem = prefab != null ? prefab.GetComponent<PickupableItem>() : null;
            return prefabItem != null ? prefabItem.heldRotationOffset : 0f;
        }

        private static bool DeclaresCustomOnScroll(Type itemType)
        {
            MethodInfo onScrollMethod = itemType.GetMethod("OnScroll", BindingFlags.Public | BindingFlags.Instance);
            return onScrollMethod != null && onScrollMethod.DeclaringType != typeof(PickupableItem);
        }
    }
}
