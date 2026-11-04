using DeftHands.Configuration;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Finds the spot a held wall-attachable item attaches to along its holder's line of sight instead of the item's own
    /// forward axis, while <see cref="ModConfig.EasyWallHanging"/> is on.
    /// </summary>
    internal static class WallAttachmentAim
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[64];

        private static readonly int AimLayers = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player");

        /// <summary>
        /// Returns the line of sight of <paramref name="holder"/>, in the visible world.
        /// </summary>
        public static Ray LineOfSight(GoPointer holder)
        {
            return new Ray(holder.transform.position, holder.transform.forward);
        }

        /// <summary>
        /// Casts the ray that finds where <paramref name="item"/> attaches and returns whether it hit a surface within
        /// <paramref name="maxDistance"/>. While <see cref="ModConfig.EasyWallHanging"/> is on, the ray is the
        /// holder's <see cref="LineOfSight"/> in attachment space, reaches <paramref name="maxDistance"/> beyond the
        /// item's <see cref="HoldDepthTracker.GetHoldDepth"/> and passes through the item itself, the player and
        /// trigger colliders; otherwise it is <paramref name="itemRay"/>.
        /// </summary>
        public static bool Raycast(Ray itemRay, out RaycastHit hit, float maxDistance, ShipItem item)
        {
            if (!ModConfig.EasyWallHanging.Value || item.held == null)
                return Physics.Raycast(itemRay, out hit, maxDistance);

            Ray sight = LineOfSight(item.held);
            Vector3 origin = AttachmentSpace.ToAttachmentSpace(item, sight.origin);
            Vector3 direction = AttachmentSpace.ToAttachmentSpace(item, item.held.transform.rotation) * Vector3.forward;
            float reach = HoldDepthTracker.GetInstance().GetHoldDepth(item) + maxDistance;
            return RaycastPastItem(new Ray(origin, direction), reach, item, out hit);
        }

        private static bool RaycastPastItem(Ray ray, float maxDistance, ShipItem item, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            ItemRigidbody itemRigidbody = item.GetItemRigidbody();
            Transform body = itemRigidbody != null ? itemRigidbody.transform : null;
            int count = Physics.RaycastNonAlloc(ray, Hits, maxDistance, AimLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hitTransform = Hits[i].transform;
                if (hitTransform.IsChildOf(item.transform) || (body != null && hitTransform.IsChildOf(body)))
                    continue;

                if (!found || Hits[i].distance < nearest.distance)
                {
                    nearest = Hits[i];
                    found = true;
                }
            }

            return found;
        }
    }
}
