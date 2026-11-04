using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Keeps how far in front of its holder the held big item is kept: its intended depth when picked up, changed by
    /// every move closer or farther since, whichever mod made it, but not by the moves that come with turning it.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class HoldDepthTracker : MonoBehaviour
    {
        private static HoldDepthTracker instance;

        /// <summary>The held big item, or null if no big item is held or the holder fields are missing.</summary>
        private PickupableItem currentItem;

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
        /// Starts keeping the depth of <paramref name="item"/> if it is a held big item.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        public void SetCurrentItem(PickupableItem item)
        {
            bool tracksItem = item != null && item.big && item.held != null
                && GoPointerFields.BigItemLocalPos != null && GoPointerFields.BigItemLocalRot != null;
            currentItem = tracksItem ? item : null;
            ownMoves = 0f;
            if (currentItem == null)
                return;

            depth = ReadIntendedDepth();
            lastIntendedDepth = depth;
            lastRotationInHand = ReadRotationInHand();
        }

        /// <summary>
        /// Records that Deft Hands' own push or pull moved the held big item <paramref name="distance"/> farther.
        /// </summary>
        public void AddOwnMove(float distance)
        {
            if (currentItem != null)
                ownMoves += distance;
        }

        /// <summary>
        /// Returns how far in front of its holder the held <paramref name="item"/> is kept: the tracked depth for the
        /// held big item, the hold distance for any other item.
        /// </summary>
        public float GetHoldDepth(PickupableItem item)
        {
            return item == currentItem ? depth : item.holdDistance;
        }

        private void LateUpdate()
        {
            if (currentItem == null || currentItem.held == null)
                return;

            float intendedDepth = ReadIntendedDepth();
            Quaternion rotationInHand = ReadRotationInHand();
            depth += rotationInHand.Equals(lastRotationInHand) ? intendedDepth - lastIntendedDepth : ownMoves;
            lastIntendedDepth = intendedDepth;
            lastRotationInHand = rotationInHand;
            ownMoves = 0f;
        }

        private float ReadIntendedDepth()
        {
            return GoPointerFields.BigItemLocalPos(currentItem.held).z;
        }

        private Quaternion ReadRotationInHand()
        {
            return Quaternion.AngleAxis(currentItem.heldRotationOffset, Vector3.right)
                * GoPointerFields.BigItemLocalRot(currentItem.held);
        }
    }
}
