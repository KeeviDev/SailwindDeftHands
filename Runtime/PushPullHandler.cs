using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Smooths Push/Pull On Scroll Wheel's distance changes so each scroll tick eases the held
    /// item to its new distance instead of snapping it there. Adds each frame's share of the
    /// movement to the affected fields' current values rather than writing a cached target,
    /// since other code can modify them between frames.
    /// </summary>
    public class PushPullHandler : MonoBehaviour
    {
        /// <summary>Approximate time the held item takes to ease to the distance requested by scrolling.</summary>
        private const float SmoothTime = 0.12f;

        /// <summary>Remaining distance below which the rest is applied at once and smoothing stops.</summary>
        private const float SettleThreshold = 0.0005f;

        private const float MinHoldDistance = 0.5f;
        private const float MaxHoldDistance = 2f;
        private const float MinBigItemDistance = 1f;
        private const float MaxBigItemDistance = 3f;

        private static readonly FieldInfo BigItemLocalPosField = AccessTools.Field(typeof(GoPointer), "bigItemLocalPos");
        private static readonly FieldInfo DecolLocalPosField = AccessTools.Field(typeof(GoPointer), "decolLocalPos");

        private static PushPullHandler instance;

        private PickupableItem currentItem;

        /// <summary>Total distance requested by scrolling since smoothing last settled.</summary>
        private float requestedOffset;

        /// <summary>Portion of <see cref="requestedOffset"/> already applied to the held item.</summary>
        private float appliedOffset;

        /// <summary>Mathf.SmoothDamp's own internal velocity state for appliedOffset.</summary>
        private float smoothVelocity;

        public static PushPullHandler GetInstance()
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("DeftHands_PushPullHandler");
                instance = obj.AddComponent<PushPullHandler>();
                DontDestroyOnLoad(obj);
            }
            return instance;
        }

        /// <summary>
        /// Registers the currently held item and discards any movement still in progress, so a
        /// freshly picked up item doesn't inherit motion left over from the previous one.
        /// </summary>
        public void SetCurrentItem(PickupableItem item)
        {
            currentItem = item;
            ResetSmoothing();
        }

        /// <summary>
        /// Queues a distance change for the held item, eased in over the following frames on
        /// top of any movement still in progress.
        /// </summary>
        /// <param name="delta">Distance to add; positive moves the item farther away.</param>
        public void Nudge(float delta)
        {
            if (currentItem == null)
                return;

            requestedOffset += delta;
        }

        /// <summary>
        /// Applies this frame's share of the queued movement. Discards whatever remains once a
        /// distance limit is reached, so scrolling back responds immediately.
        /// </summary>
        private void Update()
        {
            if (currentItem == null || currentItem.held == null)
            {
                ResetSmoothing();
                return;
            }

            if (requestedOffset == appliedOffset)
                return;

            float nextOffset = Mathf.Abs(requestedOffset - appliedOffset) < SettleThreshold
                ? requestedOffset
                : Mathf.SmoothDamp(appliedOffset, requestedOffset, ref smoothVelocity, SmoothTime);

            bool isWithinLimits = MoveHeldItem(nextOffset - appliedOffset);
            appliedOffset = nextOffset;

            if (!isWithinLimits || appliedOffset == requestedOffset)
                ResetSmoothing();
        }

        private void ResetSmoothing()
        {
            requestedOffset = 0f;
            appliedOffset = 0f;
            smoothVelocity = 0f;
        }

        /// <summary>
        /// Moves the held item closer or farther, within the distance limits for its kind of item.
        /// </summary>
        /// <param name="step">Distance to add; positive moves the item farther away.</param>
        /// <returns>False if a distance limit cut the move short.</returns>
        private bool MoveHeldItem(float step)
        {
            float requestedHoldDistance = currentItem.holdDistance + step;
            currentItem.holdDistance = ClampTowardRange(currentItem.holdDistance, requestedHoldDistance, MinHoldDistance, MaxHoldDistance);
            bool isWithinLimits = currentItem.holdDistance == requestedHoldDistance;

            if (currentItem.big && BigItemLocalPosField != null && DecolLocalPosField != null)
                isWithinLimits = MoveBigItemFields(step);

            return isWithinLimits;
        }

        /// <summary>
        /// Moves bigItemLocalPos and decolLocalPos independently, each from its own current
        /// value, so the offset between them is preserved.
        /// </summary>
        /// <param name="step">Distance to add to each field's Z component.</param>
        /// <returns>False if bigItemLocalPos hit a distance limit.</returns>
        private bool MoveBigItemFields(float step)
        {
            bool isWithinLimits = AddToLocalZ(BigItemLocalPosField, step);
            AddToLocalZ(DecolLocalPosField, step);
            return isWithinLimits;
        }

        /// <summary>
        /// Adds <paramref name="step"/> to the Z component of a Vector3 field on the holding GoPointer.
        /// </summary>
        /// <returns>False if a distance limit cut the move short.</returns>
        private bool AddToLocalZ(FieldInfo localPosField, float step)
        {
            Vector3 localPos = (Vector3)localPosField.GetValue(currentItem.held);
            float requestedZ = localPos.z + step;
            localPos.z = ClampTowardRange(localPos.z, requestedZ, MinBigItemDistance, MaxBigItemDistance);
            localPosField.SetValue(currentItem.held, localPos);
            return localPos.z == requestedZ;
        }

        /// <summary>
        /// Clamps <paramref name="requested"/> to [<paramref name="min"/>, <paramref name="max"/>],
        /// widened to include <paramref name="current"/>, so a value already outside the range can
        /// move back toward it without jumping to the nearest limit.
        /// </summary>
        private static float ClampTowardRange(float current, float requested, float min, float max)
        {
            return Mathf.Clamp(requested, Mathf.Min(current, min), Mathf.Max(current, max));
        }
    }
}
