using DeftHands.Configuration;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Turns compasses so that they read the course of the boat they are on: a compass lying on a boat turns in place,
    /// and a held compass held like a big item is locked to the course, level with the deck, until it is unlocked, eases
    /// back into the hand, leaves the hand or the player leaves that boat.
    /// </summary>
    internal static class CompassAlignment
    {
        private static PickupableItem lockedCompass;

        private static Transform lockedBoat;

        /// <summary>
        /// Returns whether <paramref name="item"/> is a compass: any compass item without a latitude scale, the game's
        /// own and other mods' alike.
        /// </summary>
        public static bool IsCompass(PickupableItem item)
        {
            return item is ShipItemCompass compass && compass.chronoLatitude == null;
        }

        /// <summary>
        /// Returns whether the held <paramref name="item"/> can be locked to the course: Compass Course Alignment is on,
        /// it is a compass held like a big item and the player is on a boat.
        /// </summary>
        public static bool CanLockToCourse(PickupableItem item)
        {
            return ModConfig.CompassCourseAlignment.Value
                && IsCompass(item)
                && item.held != null
                && ExtendedHold.IsHeldLikeBig(item)
                && RotationFrame.Boat != null
                && GoPointerFields.BigItemLocalRot != null;
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> is the compass locked to the course.
        /// </summary>
        public static bool IsLockedToCourse(PickupableItem item)
        {
            return item != null && item == lockedCompass;
        }

        /// <summary>
        /// Locks the held compass <paramref name="item"/> to the course, or unlocks it when it is locked, and returns
        /// whether it could be locked to the course at all.
        /// </summary>
        public static bool TryToggleCourseLock(PickupableItem item)
        {
            if (!CanLockToCourse(item))
                return false;

            if (item == lockedCompass)
            {
                lockedCompass = null;
                return true;
            }

            lockedCompass = item;
            lockedBoat = RotationFrame.Boat;
            item.heldRotationOffset = 0f;
            WriteCourseInHand(item.held);
            return true;
        }

        /// <summary>
        /// Keeps the compass <paramref name="holder"/> carries pointing along the course while it is locked to it, and
        /// returns whether it does. Must be called before the holder places the item for the frame.
        /// </summary>
        public static bool KeepCourse(GoPointer holder)
        {
            if (lockedCompass == null)
                return false;

            if (holder.GetHeldItem() != lockedCompass || !CanLockToCourse(lockedCompass) || RotationFrame.Boat != lockedBoat)
            {
                lockedCompass = null;
                return false;
            }

            lockedCompass.heldRotationOffset = 0f;
            WriteCourseInHand(holder);
            return true;
        }

        /// <summary>
        /// Returns whether <see cref="TryAlignLyingCompass"/> would turn the compass <paramref name="button"/> stands for.
        /// </summary>
        public static bool CanAlignLyingCompass(GoPointerButton button)
        {
            return TryGetLyingCompass(button, out ShipItem compass, out _)
                && TryGetCourseRotation(compass.currentActualBoat, compass.transform.up, out _);
        }

        /// <summary>
        /// Turns the compass <paramref name="button"/> stands for, lying unnailed on a boat, around its own up axis so that
        /// it reads that boat's course, at rest, and returns whether it did.
        /// </summary>
        public static bool TryAlignLyingCompass(GoPointerButton button)
        {
            if (!TryGetLyingCompass(button, out ShipItem compass, out ItemRigidbody twin)
                || !TryGetCourseRotation(compass.currentActualBoat, compass.transform.up, out Quaternion rotation))
                return false;

            Quaternion twinRotation = compass.currentWalkCol.rotation * Quaternion.Inverse(compass.currentActualBoat.rotation) * rotation;
            twin.transform.rotation = twinRotation;
            Rigidbody body = twin.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.rotation = twinRotation;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
            }

            compass.transform.rotation = rotation;
            return true;
        }

        /// <summary>
        /// Gets the compass <paramref name="button"/> stands for and the physics twin it follows, and returns whether it
        /// is a compass lying unnailed and loose on a boat while Compass Course Alignment is on.
        /// </summary>
        private static bool TryGetLyingCompass(GoPointerButton button, out ShipItem compass, out ItemRigidbody twin)
        {
            compass = button as ShipItem;
            twin = null;
            if (!ModConfig.CompassCourseAlignment.Value
                || compass == null
                || !IsCompass(compass)
                || compass.held != null
                || !compass.sold
                || compass.nailed
                || compass.currentActualBoat == null
                || compass.currentWalkCol == null)
                return false;

            twin = compass.GetItemRigidbody();
            return twin != null && !twin.attached;
        }

        /// <summary>
        /// Gets the world rotation, with its up axis along <paramref name="up"/>, at which a compass reads the course of
        /// <paramref name="boat"/>, and returns whether the boat's bow has a heading.
        /// </summary>
        private static bool TryGetCourseRotation(Transform boat, Vector3 up, out Quaternion rotation)
        {
            Vector3 heading = Vector3.ProjectOnPlane(GetHullRotation(boat) * Vector3.forward, Vector3.up);
            bool hasHeading = heading.sqrMagnitude >= 1e-6f;
            rotation = hasHeading
                ? Quaternion.LookRotation(Quaternion.FromToRotation(Vector3.up, up) * heading, up)
                : Quaternion.identity;
            return hasHeading;
        }

        private static void WriteCourseInHand(GoPointer holder)
        {
            Vector3 deckUp = GetHullRotation(lockedBoat) * Vector3.up;
            if (TryGetCourseRotation(lockedBoat, deckUp, out Quaternion course))
                GoPointerFields.BigItemLocalRot(holder) = Quaternion.Inverse(holder.transform.rotation) * course;
        }

        /// <summary>
        /// Returns the rotation of the body that carries <paramref name="boat"/>, whose forward axis points at the bow.
        /// </summary>
        private static Quaternion GetHullRotation(Transform boat)
        {
            Rigidbody hull = boat.GetComponentInParent<Rigidbody>();
            return hull != null ? hull.transform.rotation : boat.rotation;
        }
    }
}
