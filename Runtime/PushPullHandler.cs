using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Smooths Push/Pull On Scroll Wheel's distance changes so each scroll tick eases the held
    /// item to its new distance instead of snapping it there. Adds each frame's share of the
    /// movement to the affected fields' current values rather than writing a cached target,
    /// since other code can modify them between frames. Pushing a big item hard against
    /// something overpowers the game's push of it away from colliders (easy cargo placement)
    /// until the push or pull ends.
    /// </summary>
    public class PushPullHandler : MonoBehaviour
    {
        /// <summary>Approximate time the held item takes to ease to the distance requested by scrolling.</summary>
        private const float SmoothTime = 0.12f;

        /// <summary>Remaining distance below which the rest is applied at once and smoothing stops.</summary>
        private const float SettleThreshold = 0.0005f;

        /// <summary>
        /// Rate at which the game moves a held big item toward where its push away from colliders
        /// points, per second.
        /// </summary>
        private const float GamePushOutRate = 3f;

        /// <summary>Time over which the push-back against pushing or pulling is averaged, in seconds.</summary>
        private const float PushBackAveragingTime = 0.2f;

        /// <summary>
        /// Push-back, in units per second taken off the push or pull, from which the game's push
        /// away from colliders is overridden. Pushing fully against something is pushed back at
        /// the push's own speed, and a single scroll tick peaks at about 0.38.
        /// </summary>
        private const float PushOutOverrideStartRate = 0.7f;

        /// <summary>
        /// Push-back below which the override stops, lower than
        /// <see cref="PushOutOverrideStartRate"/> so the dips between ticks of steady scrolling
        /// don't toggle it.
        /// </summary>
        private const float PushOutOverrideStopRate = 0.45f;

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

        /// <summary>
        /// How much of the push or pull the game's push away from colliders tries to take back per
        /// second, averaged over roughly <see cref="PushBackAveragingTime"/>.
        /// </summary>
        private float pushBackRate;

        private bool overridesPushOut;

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
        /// Takes this frame's push of the held item away from colliders into account and returns
        /// whether to cancel it: true while a push or pull is in progress and the game has been
        /// pushing back against it hard enough.
        /// </summary>
        /// <param name="collisionChecker">The collision checker the push comes from.</param>
        /// <param name="pushOut">The push as the game computed it, in the checker's local space.</param>
        public bool UpdatePushOutOverride(PickupableItemCollisionChecker collisionChecker, Vector3 pushOut)
        {
            if (currentItem == null || currentItem.colChecker != collisionChecker || currentItem.held == null)
                return false;

            if (requestedOffset == appliedOffset)
            {
                pushBackRate = 0f;
                overridesPushOut = false;
                return false;
            }

            float pushDirection = Mathf.Sign(requestedOffset - appliedOffset);
            Vector3 pushOutInWorld = currentItem.transform.TransformVector(pushOut);
            float alongPush = Vector3.Dot(pushOutInWorld, currentItem.held.transform.forward) * pushDirection;
            float pushBack = Mathf.Max(0f, -alongPush) * GamePushOutRate;
            pushBackRate = Mathf.Lerp(pushBackRate, pushBack, 1f - Mathf.Exp(-Time.deltaTime / PushBackAveragingTime));

            if (pushBackRate >= PushOutOverrideStartRate)
                overridesPushOut = true;
            else if (pushBackRate < PushOutOverrideStopRate)
                overridesPushOut = false;

            return overridesPushOut;
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
                SetCurrentItem(null);
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
            pushBackRate = 0f;
            overridesPushOut = false;
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
                isWithinLimits = MoveBigItem(step);

            return isWithinLimits;
        }

        /// <summary>
        /// Moves a big item's actual position (the holding GoPointer's decolLocalPos) and brings its
        /// intended position (bigItemLocalPos) to the same depth. Without that, the game pushing
        /// the item back from a collider would leave the intended position beyond the obstacle,
        /// using up the distance limit and making the item jump through the obstacle when the
        /// player looks around.
        /// </summary>
        /// <param name="step">Distance to add to the depth.</param>
        /// <returns>False if a distance limit cut the move short.</returns>
        private bool MoveBigItem(float step)
        {
            Vector3 actual = (Vector3)DecolLocalPosField.GetValue(currentItem.held);
            float requestedDepth = actual.z + step;
            actual.z = ClampTowardRange(actual.z, requestedDepth, MinBigItemDistance, MaxBigItemDistance);
            DecolLocalPosField.SetValue(currentItem.held, actual);

            Vector3 intended = (Vector3)BigItemLocalPosField.GetValue(currentItem.held);
            intended.z = actual.z;
            BigItemLocalPosField.SetValue(currentItem.held, intended);

            return actual.z == requestedDepth;
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
