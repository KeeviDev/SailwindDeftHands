using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Remembers the rotation an item lay at on a table or other surface for items when it was picked up, so that the
    /// surfaces it is aimed at next can start it there.
    /// </summary>
    internal sealed class PickupRotationMemory
    {
        /// <summary>
        /// Seconds after an item that isn't flat is picked up from a surface for items during which its rotation there
        /// is remembered.
        /// </summary>
        private const float NonFlatItemMemoryTime = 20f;

        /// <summary>The items <see cref="FurniturePlacement"/> put down.</summary>
        private readonly PutDownMemory placedItems = new PutDownMemory();

        /// <summary>
        /// The rotation the held item lay at when it was picked up, in the space its physics twin moves in.
        /// </summary>
        private Quaternion rotationAtPickup = Quaternion.identity;

        /// <summary>The time until which <see cref="rotationAtPickup"/> is remembered.</summary>
        private float rememberedUntil = float.NegativeInfinity;

        /// <summary>
        /// Whether a rotation or a reset in the hand leaves <see cref="rotationAtPickup"/> remembered.
        /// </summary>
        private bool keepsThroughHandRotation;

        /// <summary>
        /// Remembers that <paramref name="item"/> was just put down on a surface for items by
        /// <see cref="FurniturePlacement"/>.
        /// </summary>
        public void NotePlaced(ShipItem item)
        {
            placedItems.Remember(item);
        }

        /// <summary>
        /// Forgets the rotation of the previous item, and remembers the rotation <paramref name="item"/>, just picked
        /// up, lay at on a table or other surface for items, when it was put down there within
        /// <see cref="PutDownMemory.ResumeTime"/> seconds or picked up with the rotation modifier key: for
        /// <see cref="FurniturePlacement.MemoryTime"/> seconds, or for <see cref="NonFlatItemMemoryTime"/> seconds and
        /// through rotations and resets in the hand when the item isn't flat. Must be called before its physics twin
        /// leaves where it lay.
        /// </summary>
        /// remembered.</param>
        public void OnPickup(ShipItem item, ItemShape shape)
        {
            Forget();
            if (item == null)
                return;

            bool wasPlacedRecently = placedItems.WasPutDownRecently(item);
            if (!(wasPlacedRecently || ExtendedHold.IsPickedUpWithModifierKey(item)))
                return;

            ItemRigidbody body = item.GetItemRigidbody();
            if (body == null || shape == null || !PickupSurface.LayOnSurfaceForItems(item, shape))
                return;

            rotationAtPickup = body.transform.rotation;
            keepsThroughHandRotation = !shape.IsFlat;
            float memoryTime = keepsThroughHandRotation ? NonFlatItemMemoryTime : FurniturePlacement.MemoryTime;
            rememberedUntil = Time.time + memoryTime;
        }

        /// <summary>
        /// Gets the rotation the item lay at when picked up, in the space its physics twin moves in, and returns
        /// whether it is still remembered.
        /// </summary>
        public bool TryGet(out Quaternion rotation)
        {
            rotation = rotationAtPickup;
            return Time.time <= rememberedUntil;
        }

        /// <summary>
        /// Forgets the rotation the item lay at when picked up.
        /// </summary>
        public void Forget()
        {
            rememberedUntil = float.NegativeInfinity;
            keepsThroughHandRotation = false;
        }

        /// <summary>
        /// Forgets the rotation the item lay at when picked up, unless rotations in the hand leave it remembered.
        /// </summary>
        public void ForgetUnlessKeptInHand()
        {
            if (!keepsThroughHandRotation)
                Forget();
        }
    }
}
