using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Keeps how far in front of its holder an item held like a big item is kept: its intended depth when it started
    /// being held that way, changed by every move closer or farther since, whichever mod made it, but not by the moves
    /// that come with turning it.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class HoldDepthTracker : MonoBehaviour
    {
        private static HoldDepthTracker instance;

        private PickupableItem heldItem;

        /// <summary>Whether <see cref="depth"/> is kept for <see cref="heldItem"/>.</summary>
        private bool isTracking;

        private float depth;

        /// <summary>The holder's intended depth for the item when it was last read.</summary>
        private float lastIntendedDepth;

        /// <summary>The item's rotation relative to its holder when it was last read.</summary>
        private Quaternion lastRotationInHand;

        /// <summary>Depth Deft Hands' own push and pull added since the intended depth was last read.</summary>
        private float ownMoves;

        public static HoldDepthTracker GetInstance()
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("DeftHands_HoldDepthTracker");
                instance = obj.AddComponent<HoldDepthTracker>();
                DontDestroyOnLoad(obj);
            }
            return instance;
        }

        /// <summary>
        /// Registers the currently held item and starts keeping its depth if it is held like a big item.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        public void SetCurrentItem(PickupableItem item)
        {
            heldItem = item;
            isTracking = false;
            ownMoves = 0f;
            if (CanTrack())
                StartTracking();
        }

        /// <summary>
        /// Records that Deft Hands' own push or pull moved the held item <paramref name="distance"/> farther.
        /// </summary>
        public void AddOwnMove(float distance)
        {
            if (isTracking)
                ownMoves += distance;
        }

        /// <summary>
        /// Returns how far in front of its holder the held <paramref name="item"/> is kept: the tracked depth while it
        /// is held like a big item, its hold distance otherwise.
        /// </summary>
        public float GetHoldDepth(PickupableItem item)
        {
            return item == heldItem && isTracking ? depth : item.holdDistance;
        }

        private void LateUpdate()
        {
            if (!CanTrack())
            {
                isTracking = false;
                ownMoves = 0f;
                return;
            }

            if (!isTracking)
            {
                StartTracking();
                return;
            }

            float intendedDepth = ReadIntendedDepth();
            Quaternion rotationInHand = ReadRotationInHand();
            depth += rotationInHand.Equals(lastRotationInHand) ? intendedDepth - lastIntendedDepth : ownMoves;
            lastIntendedDepth = intendedDepth;
            lastRotationInHand = rotationInHand;
            ownMoves = 0f;
        }

        private bool CanTrack()
        {
            return heldItem != null
                && heldItem.held != null
                && GoPointerFields.BigItemLocalPos != null
                && GoPointerFields.BigItemLocalRot != null
                && ExtendedHold.IsHeldLikeBig(heldItem);
        }

        private void StartTracking()
        {
            depth = ReadIntendedDepth();
            lastIntendedDepth = depth;
            lastRotationInHand = ReadRotationInHand();
            ownMoves = 0f;
            isTracking = true;
        }

        private float ReadIntendedDepth()
        {
            return GoPointerFields.BigItemLocalPos(heldItem.held).z;
        }

        private Quaternion ReadRotationInHand()
        {
            return Quaternion.AngleAxis(heldItem.heldRotationOffset, Vector3.right)
                * GoPointerFields.BigItemLocalRot(heldItem.held);
        }
    }
}
