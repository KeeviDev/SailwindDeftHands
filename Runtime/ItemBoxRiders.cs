using System.Collections.Generic;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Keeps track of the items that ride in another item's box, the way the game keeps items in a basket.
    /// </summary>
    internal static class ItemBoxRiders
    {
        private static readonly HashSet<ItemRigidbody> BoxedBodies = new HashSet<ItemRigidbody>();

        private static readonly List<ItemRigidbody> LeftBodies = new List<ItemRigidbody>();

        /// <summary>
        /// Records that the item whose physics twin is <paramref name="body"/> was put in a box.
        /// </summary>
        public static void Add(ItemRigidbody body)
        {
            if (body != null)
                BoxedBodies.Add(body);
        }

        /// <summary>
        /// Fills <paramref name="riders"/> with the items that ride in a box of <paramref name="item"/> or of one of
        /// its parts.
        /// </summary>
        public static void Collect(ShipItem item, List<ShipItem> riders)
        {
            riders.Clear();
            LeftBodies.Clear();
            foreach (ItemRigidbody body in BoxedBodies)
            {
                Transform box = body != null ? body.GetCurrentBox() : null;
                if (box == null)
                {
                    LeftBodies.Add(body);
                    continue;
                }

                ShipItem rider = body.GetShipItem();
                if (rider != null && rider != item && box.IsChildOf(item.transform))
                    riders.Add(rider);
            }

            foreach (ItemRigidbody body in LeftBodies)
                BoxedBodies.Remove(body);
        }
    }
}
