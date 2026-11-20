using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Finds whether an item just picked up lay on a table or other surface for items, directly or on other items
    /// there.
    /// </summary>
    internal static class PickupSurface
    {
        /// <summary>The layers the holder's look ray hits.</summary>
        private const int LookLayers = -604165;

        /// <summary>Distance beyond the item within which what it lies on is found, in metres.</summary>
        private const float SupportGap = 0.05f;

        /// <summary>
        /// Distance from the item's middle within which the surface for items it lies on is found through other items,
        /// in metres.
        /// </summary>
        private const float SurfaceReach = 0.5f;

        private const string SubcolliderTag = "ItemSubcollider";

        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        private static readonly IComparer<RaycastHit> ByDistance =
            Comparer<RaycastHit>.Create((first, second) => first.distance.CompareTo(second.distance));

        /// <summary>
        /// The item being checked and the items riding in its box, whose colliders are none of what it lay on.
        /// </summary>
        private static readonly ItemParts OwnParts = new ItemParts();

        /// <summary>
        /// Returns whether <paramref name="item"/>, just picked up, lay on a table or other surface for items it can be
        /// put down on, directly or on other items lying there. Must be called before its physics twin leaves where it
        /// lay.
        /// </summary>
        public static bool LayOnSurfaceForItems(ShipItem item, ItemShape shape)
        {
            ItemRigidbody body = item.GetItemRigidbody();
            if (body == null)
                return false;

            var pose = new Pose(
                AttachmentSpace.ToVisibleWorld(item, body.transform.position),
                AttachmentSpace.ToVisibleWorld(item, body.transform.rotation));
            OwnParts.CollectWithRiders(item);
            bool liesOnSurface = LiesOnSurface(item, shape, pose);
            OwnParts.Clear();
            return liesOnSurface;
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> in <paramref name="pose"/> lies on a surface for items. A flat item
        /// lies on what is right under it in its resting rotation when that leads to such a surface, and any item on
        /// what is below its middle otherwise.
        /// </summary>
        private static bool LiesOnSurface(ShipItem item, ItemShape shape, Pose pose)
        {
            Vector3 middle = shape.GetMiddle(pose);
            var flatDown = new Ray(middle, pose.rotation * shape.RestingDown);
            var down = new Ray(middle, -AttachmentSpace.UpInWorld(item));
            return (shape.IsFlat && LiesOnSurfaceAlong(item, shape, pose, flatDown))
                || LiesOnSurfaceAlong(item, shape, pose, down);
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> in <paramref name="pose"/> lies along <paramref name="ray"/>, cast
        /// from its middle, on a surface for items it can be put down on: right there, within <see cref="SupportGap"/>
        /// of the item, or under other items it lies on, within <see cref="SurfaceReach"/> of its middle.
        /// </summary>
        private static bool LiesOnSurfaceAlong(ShipItem item, ItemShape shape, Pose pose, Ray ray)
        {
            float supportReach = shape.GetReachAlong(pose.rotation, ray.direction) + SupportGap;
            int count = Physics.RaycastNonAlloc(ray, Hits, Mathf.Max(supportReach, SurfaceReach), LookLayers);
            Array.Sort(Hits, 0, count, ByDistance);
            bool isSupported = false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                if (collider == null || OwnParts.Contains(collider.transform) || IsBlankTrigger(collider))
                    continue;

                if (!isSupported && Hits[i].distance > supportReach)
                    return false;

                isSupported = true;
                if (IsSurfaceForItems(item, collider))
                    return true;

                if (!IsItemCollider(collider))
                    return false;
            }

            return false;
        }

        /// <summary>
        /// Returns whether <paramref name="collider"/> belongs to a button <paramref name="item"/> can be put down on.
        /// </summary>
        private static bool IsSurfaceForItems(ShipItem item, Collider collider)
        {
            GoPointerButton button = GetButton(collider);
            return button != null
                && button != item
                && !button.unclickable
                && button.allowPlacingItems
                && item.AllowOnItemClick(button);
        }

        /// <summary>
        /// Returns the button the look ray points at when it hits <paramref name="collider"/>, or null when it belongs
        /// to none.
        /// </summary>
        private static GoPointerButton GetButton(Collider collider)
        {
            if (collider.CompareTag(SubcolliderTag) && collider.transform.parent != null)
                collider = collider.transform.parent.GetComponent<Collider>();

            return collider != null ? collider.GetComponent<GoPointerButton>() : null;
        }

        private static bool IsBlankTrigger(Collider collider)
        {
            return collider.isTrigger && GetButton(collider) == null;
        }

        private static bool IsItemCollider(Collider collider)
        {
            return collider.GetComponentInParent<PickupableItem>() != null;
        }
    }
}
