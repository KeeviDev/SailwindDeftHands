using DeftHands.Configuration;
using DeftHands.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Holds a small item the way the game holds big items, from when the player starts rotating, pushing or pulling it,
    /// or picks it up with the rotation modifier key held, until it has been left alone for <see cref="IdleTime"/>
    /// seconds or is used, when it eases back into the hand. A small item put down while held this way is held this way
    /// again when picked up within <see cref="ResumeTime"/> seconds. While it is held this way, it keeps the rotation its
    /// holder gives it, whatever its own per-frame code does to it.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class ExtendedHold : MonoBehaviour
    {
        /// <summary>
        /// Seconds without rotating, pushing or pulling after which the item goes back into the hand.
        /// </summary>
        private const float IdleTime = 5f;

        /// <summary>
        /// Seconds after being put down within which an item is picked up held like a big item again.
        /// </summary>
        private const float ResumeTime = 20f;

        private static ExtendedHold instance;

        /// <summary>When each small item put down while held like a big item was put down, by Time.time.</summary>
        private readonly Dictionary<PickupableItem, float> putDownTimes = new Dictionary<PickupableItem, float>();

        private PickupableItem currentItem;

        private bool isExtended;

        /// <summary>The item's heldRotationOffset from before it was held like a big item.</summary>
        private float savedHeldRotationOffset;

        /// <summary>Time.time of the last rotation, push or pull of the item while held like a big item.</summary>
        private float lastActivityTime;

        /// <summary>The rotation the holder gave the item held like a big item, in the frame <see cref="placedFrame"/>.</summary>
        private Quaternion placedRotation;

        private int placedFrame = -1;

        /// <summary>
        /// Gets whether GoPointer.LateUpdate places the small item held like a big item as a big item.
        /// </summary>
        public static bool IsPlacementPatched { get; internal set; }

        public static ExtendedHold GetInstance()
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("DeftHands_ExtendedHold");
                instance = obj.AddComponent<ExtendedHold>();
                DontDestroyOnLoad(obj);
            }
            return instance;
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> is big or is the small item held like a big item.
        /// </summary>
        public static bool IsHeldLikeBig(PickupableItem item)
        {
            return item.big || (instance != null && instance.isExtended && instance.currentItem == item);
        }

        /// <summary>
        /// Returns whether the held small <paramref name="item"/> can be held like a big item: Extended Small Items
        /// Rotation is on, and the item doesn't attach to walls and draws something.
        /// </summary>
        public static bool CanHoldLikeBig(PickupableItem item)
        {
            return ModConfig.ExtendedSmallItemsRotation.Value
                && IsPlacementPatched
                && GoPointerFields.BigItemLocalPos != null
                && GoPointerFields.DecolLocalPos != null
                && GoPointerFields.BigItemLocalRot != null
                && GoPointerFields.TimerAfterPickup != null
                && item is ShipItem shipItem
                && !shipItem.big
                && !shipItem.wallAttachment
                && shipItem.held != null
                && !HeldItemVisibility.IsHidden(shipItem);
        }

        /// <summary>
        /// Registers the currently held item. A previous item held like a big item gets its own tilt back and, when put
        /// down in the world, is remembered. The new item is held like a big item where it is if it was put down that
        /// way within <see cref="ResumeTime"/> seconds, or if it was picked up by pointing at it with the rotation
        /// modifier key held.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        public void SetCurrentItem(PickupableItem item)
        {
            if (item == currentItem)
                return;

            if (isExtended)
                PutDown();

            currentItem = item;
            isExtended = false;
            if (item == null)
                return;

            bool resumes = WasPutDownRecently(item);
            if ((resumes || IsPickedUpWithModifierKey(item)) && CanHoldLikeBig(item))
                Begin();
        }

        /// <summary>
        /// Holds <paramref name="item"/>, the currently held item, like a big item if it can be, and returns whether it
        /// is held like a big item.
        /// </summary>
        public bool TryHoldLikeBig(PickupableItem item)
        {
            if (item == null)
                return false;

            if (IsHeldLikeBig(item))
                return true;

            if (item != currentItem || !CanHoldLikeBig(item))
                return false;

            Begin();
            return true;
        }

        /// <summary>
        /// Restarts the time after which the item held like a big item goes back into the hand.
        /// </summary>
        public void NoteActivity()
        {
            lastActivityTime = Time.time;
        }

        /// <summary>
        /// Eases <paramref name="item"/> back into the hand, with the tilt it had before, when it is the small item
        /// held like a big item.
        /// </summary>
        public void ReturnToHand(PickupableItem item)
        {
            if (isExtended && item != null && item == currentItem)
                EaseBackIntoHand();
        }

        /// <summary>
        /// Remembers the rotation <paramref name="holder"/> gave the small item held like a big item this frame. Must be
        /// called after the holder places the item for the frame.
        /// </summary>
        public static void RememberPlacedRotation(GoPointer holder)
        {
            if (instance == null || !instance.isExtended || holder.GetHeldItem() != instance.currentItem)
                return;

            instance.placedRotation = instance.currentItem.transform.rotation;
            instance.placedFrame = Time.frameCount;
        }

        private void Update()
        {
            if (!isExtended)
                return;

            if (currentItem == null || currentItem.held == null)
            {
                PutDown();
                return;
            }

            if (!ModConfig.ExtendedSmallItemsRotation.Value || HeldItemVisibility.IsHidden(currentItem))
            {
                EaseBackIntoHand();
                return;
            }

            if (RotationHandler.GetInstance().IsRotating || PushPullHandler.GetInstance().IsMoving)
                NoteActivity();

            if (Time.time - lastActivityTime >= IdleTime)
                EaseBackIntoHand();
        }

        private void LateUpdate()
        {
            if (isExtended && currentItem != null && placedFrame == Time.frameCount)
                currentItem.transform.rotation = placedRotation;
        }

        private static bool IsPickedUpWithModifierKey(PickupableItem item)
        {
            return ModInput.IsRotationModifierKeyHeld()
                && item.held != null
                && GoPointerFields.PointedAtButton != null
                && GoPointerFields.PointedAtButton(item.held) == item;
        }

        /// <summary>
        /// Starts holding the current item like a big item where it is now.
        /// </summary>
        private void Begin()
        {
            GoPointer holder = currentItem.held;
            Transform hand = holder.transform;
            Transform item = currentItem.transform;
            Vector3 positionInHand = hand.InverseTransformPoint(item.position);
            GoPointerFields.BigItemLocalPos(holder) = positionInHand;
            GoPointerFields.DecolLocalPos(holder) = positionInHand;
            GoPointerFields.BigItemLocalRot(holder) = Quaternion.Inverse(hand.rotation) * item.rotation;

            savedHeldRotationOffset = currentItem.heldRotationOffset;
            currentItem.heldRotationOffset = 0f;
            isExtended = true;
            NoteActivity();
        }

        /// <summary>
        /// Stops holding the current item like a big item, drops any push or pull still easing it, and lets its holder ease
        /// it back into the hand.
        /// </summary>
        private void EaseBackIntoHand()
        {
            isExtended = false;
            PushPullHandler.GetInstance().Stop();
            currentItem.heldRotationOffset = savedHeldRotationOffset;
            GoPointerFields.TimerAfterPickup(currentItem.held) = 0f;
        }

        /// <summary>
        /// Stops holding the current item, which was just let go, like a big item, and remembers it when it stays in
        /// the world.
        /// </summary>
        private void PutDown()
        {
            isExtended = false;
            if (currentItem == null)
                return;

            currentItem.heldRotationOffset = savedHeldRotationOffset;
            if (IsInWorld(currentItem))
            {
                ForgetOldPutDowns();
                putDownTimes[currentItem] = Time.time;
            }
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> was put down while held like a big item within
        /// <see cref="ResumeTime"/> seconds, and forgets when it was put down.
        /// </summary>
        private bool WasPutDownRecently(PickupableItem item)
        {
            if (!putDownTimes.TryGetValue(item, out float putDownTime))
                return false;

            putDownTimes.Remove(item);
            return Time.time - putDownTime <= ResumeTime;
        }

        private void ForgetOldPutDowns()
        {
            foreach (PickupableItem item in putDownTimes.Keys.ToList())
            {
                if (item == null || Time.time - putDownTimes[item] > ResumeTime)
                    putDownTimes.Remove(item);
            }
        }

        /// <summary>
        /// Returns whether <paramref name="item"/>, just let go, lies in the world rather than in an inventory slot, a
        /// crate or a cargo hold.
        /// </summary>
        private static bool IsInWorld(PickupableItem item)
        {
            if (item is ShipItem shipItem && shipItem.GetCurrentInventorySlot() > -1)
                return false;

            return item.gameObject.activeInHierarchy && (item.transform.localScale - Vector3.one).sqrMagnitude < 1e-4f;
        }
    }
}
