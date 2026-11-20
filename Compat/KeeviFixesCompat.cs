using DeftHands.Utils;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Compat
{
    /// <summary>
    /// Reads the center Keevi Fixes' "Fix rotation center" turns a held big item around.
    /// </summary>
    internal static class KeeviFixesCompat
    {
        private const string CenteringTypeName = "KeeviFixes.Runtime.CenteredBigItemRotation";
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static bool initialized;
        private static IDictionary centeringByHolder;
        private static FieldInfo itemField;
        private static FieldInfo pivotToCenterField;

        /// <summary>
        /// The record Keevi Fixes keeps for one held item, last read by <see cref="TryGetRotationCenter"/>.
        /// </summary>
        private static object lastCentering;

        private static PickupableItem lastCenteredItem;

        private static Vector3 lastPivotToCenter;

        /// <summary>
        /// Gets the offset from the pivot of <paramref name="item"/>, held by <paramref name="holder"/>, to the center
        /// Keevi Fixes turns it around, in the item's rotation frame, and returns whether Keevi Fixes turns it around a
        /// center. Must only be called after every plugin has loaded.
        /// </summary>
        public static bool TryGetRotationCenter(GoPointer holder, PickupableItem item, out Vector3 pivotToCenter)
        {
            EnsureInitialized();
            pivotToCenter = Vector3.zero;
            object centering = centeringByHolder?[holder];
            if (centering == null)
                return false;

            if (!ReferenceEquals(centering, lastCentering))
                ReadCentering(centering);

            if (!ReferenceEquals(lastCenteredItem, item))
                return false;

            pivotToCenter = lastPivotToCenter;
            return true;
        }

        private static void ReadCentering(object centering)
        {
            lastCentering = centering;
            lastCenteredItem = itemField.GetValue(centering) as PickupableItem;
            lastPivotToCenter = (Vector3)pivotToCenterField.GetValue(centering);
        }

        private static void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;

            Type centeringType = ReflectionUtils.FindType(CenteringTypeName);
            if (centeringType == null)
                return;

            itemField = centeringType.GetField("item", InstanceFlags);
            pivotToCenterField = centeringType.GetField("pivotToCenter", InstanceFlags);
            object byHolder = centeringType.GetField("TrackedByHolder", StaticFlags)?.GetValue(null);
            if (itemField == null || pivotToCenterField == null || pivotToCenterField.FieldType != typeof(Vector3))
                return;

            centeringByHolder = byHolder as IDictionary;
        }
    }
}
