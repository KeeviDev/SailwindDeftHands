using System.Collections.Generic;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Starts an item taken off a wall, ceiling or floor with the rotation and depth it hangs with on its surface:
    /// remembered from when it was hung, for as long as the game runs, or worked out from its pose and the surface it
    /// hangs on when it is picked up with the rotation modifier key.
    /// </summary>
    internal static class HungItemPose
    {
        /// <summary>
        /// Farthest, in metres, a hung item may lie from where it was hung and still count as hanging there.
        /// </summary>
        private const float MaxRememberedOffset = 0.01f;

        /// <summary>
        /// Largest angle, in degrees, a hung item may be turned from how it was hung and still count as hanging so.
        /// </summary>
        private const float MaxRememberedTurn = 1f;

        /// <summary>How each item hung while the game runs was hung, by its save instance ID.</summary>
        private static readonly Dictionary<int, HungItem> HungItems = new Dictionary<int, HungItem>();

        /// <summary>
        /// Remembers how <paramref name="item"/>, just let go, was hung when it attached to a surface where it was
        /// aimed, and forgets it otherwise. Must be called before the item stops being tracked as held.
        /// </summary>
        public static void Remember(ShipItem item)
        {
            int id = GetInstanceId(item);
            if (id == 0)
                return;

            ItemRigidbody body = item.GetItemRigidbody();
            var pose = new Pose(item.GetAttachPos(), item.GetAttachRot());
            if (body == null || !body.attached || !WallAttachmentAdjustment.IsTracking(item)
                || !IsAt(body.transform, pose))
            {
                HungItems.Remove(id);
                return;
            }

            float depth = WallAttachmentAdjustment.IsDeepenedThisFrame(item) ? WallAttachmentAdjustment.Depth : 0f;
            HungItems[id] = new HungItem(pose, GetIntoSurface(item, pose.rotation), depth);
        }

        /// <summary>
        /// Returns the direction into the surface <paramref name="item"/>, just let go, attached to at
        /// <paramref name="attachRot"/>, in attachment space.
        /// </summary>
        private static Vector3 GetIntoSurface(ShipItem item, Quaternion attachRot)
        {
            Quaternion facingSurface = WallAttachmentAdjustment.IsOrientedThisFrame(item)
                ? attachRot * Quaternion.Inverse(WallAttachmentAdjustment.Orientation)
                : attachRot;
            return facingSurface * Vector3.forward;
        }

        /// <summary>
        /// Returns whether the held <paramref name="item"/>, just taken off a surface, starts with the rotation and
        /// depth it hangs with: it was hung while the game runs and still hangs there, or it was picked up with the
        /// rotation modifier key. Must be called before its physics twin leaves where it hangs.
        /// </summary>
        public static bool TakesHungPose(ShipItem item)
        {
            return item != null
                && item.wallAttachment
                && (IsRemembered(item) || ExtendedHold.IsPickedUpWithModifierKey(item));
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> hangs where it was hung while the game runs. Must be called before
        /// its physics twin leaves where it hangs.
        /// </summary>
        public static bool HangsWhereHung(ShipItem item)
        {
            return item != null && IsRemembered(item);
        }

        /// <summary>
        /// Returns whether the bought <paramref name="item"/> hangs on a surface it was not hung on while the game
        /// runs.
        /// </summary>
        public static bool HangsSinceLoad(ShipItem item)
        {
            ItemRigidbody body = item != null && item.wallAttachment && item.sold ? item.GetItemRigidbody() : null;
            return body != null && IsHanging(body) && !IsRemembered(item);
        }

        /// <summary>
        /// Returns whether the physics twin <paramref name="body"/> hangs on a surface: it is attached, and neither
        /// stored, cooking, hung on a hook nor seated in a display.
        /// </summary>
        public static bool IsHanging(ItemRigidbody body)
        {
            return body.attached && !body.inStove && !body.disableCol;
        }

        /// <summary>
        /// Starts the held <paramref name="item"/>, just taken off a surface, with the rotation and depth it was hung
        /// with when that is remembered, or else with those it hangs with on the surface found below it. Must be called
        /// after the item starts being tracked as held and before its physics twin leaves where it hangs.
        /// </summary>
        public static void TakeOff(ShipItem item)
        {
            ItemRigidbody body = item != null && item.wallAttachment ? item.GetItemRigidbody() : null;
            if (body == null || TryRecall(item, body))
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            Transform twin = body.transform;
            Vector3 viewPoint = AttachmentSpace.ToAttachmentSpace(item, camera.transform.position);
            if (!HungSurfaceFinder.TryFind(item, twin, viewPoint, out Vector3 point, out Vector3 normal))
                return;

            Quaternion frame = WallAttachmentAdjustment.GetSurfaceFrame(item, -normal);
            WallAttachmentAdjustment.RestorePose(
                Quaternion.Inverse(frame) * twin.rotation,
                Vector3.Dot(point - twin.position, normal));
        }

        private static bool TryRecall(ShipItem item, ItemRigidbody body)
        {
            int id = GetInstanceId(item);
            if (id == 0 || !HungItems.TryGetValue(id, out HungItem hung))
                return false;

            HungItems.Remove(id);
            if (!IsAt(body.transform, hung.Pose))
                return false;

            Quaternion frame = WallAttachmentAdjustment.GetSurfaceFrame(item, hung.IntoSurface);
            WallAttachmentAdjustment.RestorePose(Quaternion.Inverse(frame) * hung.Pose.rotation, hung.Depth);
            return true;
        }

        private static bool IsRemembered(ShipItem item)
        {
            int id = GetInstanceId(item);
            ItemRigidbody body = item.GetItemRigidbody();
            return id != 0 && body != null && HungItems.TryGetValue(id, out HungItem hung)
                && IsAt(body.transform, hung.Pose);
        }

        private static int GetInstanceId(ShipItem item)
        {
            SaveablePrefab saveable = item != null ? item.GetComponent<SaveablePrefab>() : null;
            return saveable != null ? saveable.instanceId : 0;
        }

        private static bool IsAt(Transform body, Pose pose)
        {
            return (body.position - pose.position).sqrMagnitude <= MaxRememberedOffset * MaxRememberedOffset
                && Quaternion.Angle(body.rotation, pose.rotation) <= MaxRememberedTurn;
        }

        private readonly struct HungItem
        {
            public HungItem(Pose pose, Vector3 intoSurface, float depth)
            {
                Pose = pose;
                IntoSurface = intoSurface;
                Depth = depth;
            }

            /// <summary>Gets the pose the item's physics twin was hung in, in attachment space.</summary>
            public Pose Pose { get; }

            /// <summary>Gets the direction into the surface the item hangs on, in attachment space.</summary>
            public Vector3 IntoSurface { get; }

            public float Depth { get; }
        }
    }
}
