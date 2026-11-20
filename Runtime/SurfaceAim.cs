using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Keeps the point the player's look ray last hit in the space of the collider it hit, so that the point moves with
    /// that collider between physics steps, and tells whether the held item is aimed at a surface for items.
    /// </summary>
    internal static class SurfaceAim
    {
        private static Transform surface;

        private static Vector3 pointOnSurface;

        private static Vector3 normalOnSurface;

        /// <summary>Gets whether the look ray hit a collider that still exists.</summary>
        public static bool HasHit => surface != null;

        /// <summary>
        /// Returns whether the held small <paramref name="item"/> is aimed at a table or other surface for items, where
        /// its holder draws it where it would set it down instead of in the hand.
        /// </summary>
        public static bool IsAimedAtSurfaceForItems(ShipItem item)
        {
            if (item == null || item.held == null || item.big || GoPointerFields.PointedAtButton == null)
                return false;

            GoPointerButton button = GoPointerFields.PointedAtButton(item.held);
            return button != null && button != item && button.allowPlacingItems;
        }

        /// <summary>
        /// Records where the look ray of <paramref name="holder"/> hit. Must be called right after the holder casts it.
        /// </summary>
        public static void Record(GoPointer holder)
        {
            if (GoPointerFields.Hit == null)
                return;

            RaycastHit hit = GoPointerFields.Hit(holder);
            surface = hit.collider != null ? hit.collider.transform : null;
            if (surface == null)
                return;

            pointOnSurface = surface.InverseTransformPoint(hit.point);
            normalOnSurface = surface.InverseTransformDirection(hit.normal);
        }

        /// <summary>
        /// Gets the point the look ray hit and the surface normal there, in the visible world, and returns whether it
        /// hit a collider that still exists.
        /// </summary>
        public static bool TryGetHit(out Vector3 point, out Vector3 normal)
        {
            if (!HasHit)
            {
                point = default;
                normal = default;
                return false;
            }

            point = surface.TransformPoint(pointOnSurface);
            normal = surface.TransformDirection(normalOnSurface).normalized;
            return true;
        }
    }
}
