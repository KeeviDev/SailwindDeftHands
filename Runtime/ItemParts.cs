using System.Collections.Generic;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Holds the transforms of an item and its physics twin, so that rays cast around the item can pass through them.
    /// </summary>
    internal sealed class ItemParts
    {
        private readonly List<Transform> parts = new List<Transform>();

        /// <summary>
        /// Replaces the collected parts with <paramref name="item"/> and its physics twin.
        /// </summary>
        public void Collect(ShipItem item)
        {
            parts.Clear();
            Add(item);
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
