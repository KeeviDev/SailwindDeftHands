using DeftHands.Compat;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Measures how far in front of its holder a held item is kept, at a depth that turning the item leaves alone.
    /// </summary>
    internal static class HoldDepth
    {
        /// <summary>
        /// Returns how far in front of its holder the held <paramref name="item"/> is kept: for a big item, the depth of
        /// the point it turns around in the hand, and its hold distance otherwise.
        /// </summary>
        public static float Of(PickupableItem item)
        {
            GoPointer holder = item.held;
            if (holder == null
                || !item.big
                || GoPointerFields.BigItemLocalPos == null
                || GoPointerFields.BigItemLocalRot == null)
                return item.holdDistance;

            Vector3 pivot = GoPointerFields.BigItemLocalPos(holder);
            if (!KeeviFixesCompat.TryGetRotationCenter(holder, item, out Vector3 pivotToCenter))
                return pivot.z;

            Quaternion rotationInHand = Quaternion.AngleAxis(item.heldRotationOffset, Vector3.right)
                * GoPointerFields.BigItemLocalRot(holder);
            return (pivot + rotationInHand * pivotToCenter).z;
        }
    }
}
