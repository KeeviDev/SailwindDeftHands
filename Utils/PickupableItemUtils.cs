using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Utils
{
    internal static class PickupableItemUtils
    {
        /// <summary>The heldRotationOffset of a spyglass raised to the eye.</summary>
        private const float SpyglassRaisedOffset = 0f;

        /// <summary>The heldRotationOffset of a spyglass lowered in the hand.</summary>
        private const float SpyglassLoweredOffset = -22f;

        private static readonly Dictionary<Type, Type> OnScrollOwnerByType = new Dictionary<Type, Type>();

        /// <summary>
        /// Returns whether the item reacts to the scroll wheel itself: its class overrides PickupableItem.OnScroll,
        /// except a compass without a latitude scale, whose override does nothing.
        /// </summary>
        public static bool HasCustomOnScroll(PickupableItem item)
        {
            Type owner = GetOnScrollOwner(item.GetType());
            if (owner == typeof(ShipItemCompass))
                return ((ShipItemCompass)item).chronoLatitude != null;

            return owner != typeof(PickupableItem);
        }

        /// <summary>
        /// Returns whether the item's class overrides PickupableItem.OnScroll.
        /// </summary>
        public static bool OverridesOnScroll(PickupableItem item)
        {
            return GetOnScrollOwner(item.GetType()) != typeof(PickupableItem);
        }

        /// <summary>
        /// Returns the heldRotationOffset <paramref name="item"/> rests at in the usual hold: a spyglass at whichever
        /// of its raised and lowered poses is nearer its current offset, any other item at its prefab's.
        /// </summary>
        public static float GetRestingHeldRotationOffset(PickupableItem item)
        {
            if (!(item is ShipItemSpyglass))
                return GetPrefabHeldRotationOffset(item);

            float midway = (SpyglassRaisedOffset + SpyglassLoweredOffset) / 2f;
            return item.heldRotationOffset < midway ? SpyglassLoweredOffset : SpyglassRaisedOffset;
        }

        /// <summary>
        /// Returns the heldRotationOffset of the item's prefab, or 0 for an item that has no prefab.
        /// </summary>
        public static float GetPrefabHeldRotationOffset(PickupableItem item)
        {
            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            GameObject[] prefabs = PrefabsDirectory.instance != null ? PrefabsDirectory.instance.directory : null;
            if (saveable == null || prefabs == null
                || saveable.prefabIndex < 0 || saveable.prefabIndex >= prefabs.Length)
                return 0f;

            GameObject prefab = prefabs[saveable.prefabIndex];
            PickupableItem prefabItem = prefab != null ? prefab.GetComponent<PickupableItem>() : null;
            return prefabItem != null ? prefabItem.heldRotationOffset : 0f;
        }

        /// <summary>
        /// Returns the class whose OnScroll an item of <paramref name="itemType"/> runs.
        /// </summary>
        private static Type GetOnScrollOwner(Type itemType)
        {
            if (!OnScrollOwnerByType.TryGetValue(itemType, out Type owner))
            {
                MethodInfo onScrollMethod = itemType.GetMethod("OnScroll", BindingFlags.Public | BindingFlags.Instance);
                owner = onScrollMethod != null ? onScrollMethod.DeclaringType : typeof(PickupableItem);
                OnScrollOwnerByType[itemType] = owner;
            }

            return owner;
        }
    }
}
