using System.Collections.Generic;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Holds the transforms of an item, its physics twin and, when asked for, the items riding in its box, so that rays
    /// cast around the item can pass through them.
    /// </summary>
    internal sealed class ItemParts
    {
        private readonly List<Transform> parts = new List<Transform>();

        private readonly List<ShipItem> riders = new List<ShipItem>();

        /// <summary>
        /// Replaces the collected parts with <paramref name="item"/> and its physics twin.
        /// </summary>
        public void Collect(ShipItem item)
        {
            parts.Clear();
            Add(item);
        }

        /// <summary>
        /// Replaces the collected parts with <paramref name="item"/>, the items riding in its box, and their physics
        /// twins.
        /// </summary>
        public void CollectWithRiders(ShipItem item)
        {
            Collect(item);
            ItemBoxRiders.Collect(item, riders);
            foreach (ShipItem rider in riders)
                Add(rider);

            riders.Clear();
        }

        /// <summary>
        /// Forgets the collected parts.
        /// </summary>
        public void Clear()
        {
            parts.Clear();
        }

        /// <summary>
        /// Returns whether <paramref name="transform"/> belongs to one of the collected parts.
        /// </summary>
        public bool Contains(Transform transform)
        {
            foreach (Transform part in parts)
            {
                if (part != null && transform.IsChildOf(part))
                    return true;
            }

            return false;
        }

        private void Add(ShipItem item)
        {
            parts.Add(item.transform);
            ItemRigidbody body = item.GetItemRigidbody();
            if (body != null)
                parts.Add(body.transform);
        }
    }
}
