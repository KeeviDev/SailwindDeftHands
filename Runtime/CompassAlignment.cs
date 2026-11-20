using DeftHands.Configuration;
using DeftHands.Utils;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Turns compasses so that they read the course of the boat they are on: a compass lying on a boat turns in place,
    /// and a held compass is locked to the course until it is unlocked, leaves the hand or the player leaves that boat.
    /// In the usual hold, a compass locked to the course keeps its pose there and only turns within its own plane.
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
        /// Returns whether the held <paramref name="item"/> can be locked to the course: Compass Course Alignment is
        /// on and runs, it is a compass that draws something, and the player is on a boat.
        /// </summary>
        public static bool CanLockToCourse(PickupableItem item)
        {
            return FeatureGuard.IsOn(ModConfig.CompassCourseAlignment)
                && IsCompass(item)
                && item.held != null
                && !HeldItemVisibility.IsHidden(item)
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
        /// Gets the world rotation, with its up axis along <paramref name="up"/>, at which the compass locked to the
        /// course reads it, and returns whether <paramref name="item"/> is that compass and the boat has a heading.
        /// </summary>
        public static bool TryGetLockedCourse(PickupableItem item, Vector3 up, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            return IsLockedToCourse(item) && lockedBoat != null && TryGetCourseRotation(lockedBoat, up, out rotation);
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
                Unlock(item);
                return true;
            }

            return TryLockToCourse(item);
        }

        /// <summary>
        /// Unlocks <paramref name="item"/> from the course when it is the compass locked to it.
        /// </summary>
        public static void Unlock(PickupableItem item)
        {
            if (IsLockedToCourse(item))
                lockedCompass = null;
        }

        /// <summary>
        /// Unlocks the compass locked to the course.
        /// </summary>
        public static void UnlockCourse()
        {
            lockedCompass = null;
            lockedBoat = null;
        }

        /// <summary>
        /// Locks the held compass <paramref name="item"/> to the course and returns whether it could be locked.
        /// </summary>
        public static bool TryLockToCourse(PickupableItem item)
        {
            if (!CanLockToCourse(item))
                return false;

            lockedCompass = item;
            lockedBoat = RotationFrame.Boat;
            CompassCourseKeeper.GetInstance();
            if (ExtendedHold.IsHeldLikeBig(item))
            {
                item.heldRotationOffset = 0f;
                WriteCourseInHand(item.held);
            }

            return true;
        }

        /// <summary>
        /// Unlocks the compass locked to the course once <paramref name="holder"/> no longer carries it where it can be
        /// locked, and keeps it pointing along the course while it is held like a big item. Returns whether it keeps it
        /// so. Must be called before the holder places the item for the frame.
        /// </summary>
        public static bool KeepCourse(GoPointer holder)
        {
            if (lockedCompass == null)
                return false;

            if (holder.GetHeldItem() != lockedCompass || !CanLockToCourse(lockedCompass)
                || RotationFrame.Boat != lockedBoat)
            {
                lockedCompass = null;
                return false;
            }

            if (!ExtendedHold.IsHeldLikeBig(lockedCompass))
                return false;

            lockedCompass.heldRotationOffset = 0f;
            WriteCourseInHand(holder);
            return true;
        }

        /// <summary>
        /// Turns the compass locked to the course, held in the usual hold and aimed at no surface for items, within its
        /// own plane so that it reads the course. Must be called once the holder and the compass's own code have posed
        /// it for the frame.
        /// </summary>
        public static void KeepCourseInUsualHold()
        {
            if (lockedCompass == null || lockedBoat == null || !IsInUsualHold(lockedCompass))
                return;

            Transform compass = lockedCompass.transform;
            if (TryGetCourseRotation(lockedBoat, compass.up, out Quaternion rotation))
                compass.rotation = rotation;
        }

        private static bool IsInUsualHold(PickupableItem item)
        {
            return item.held != null
                && !ExtendedHold.IsHeldLikeBig(item)
                && !SurfaceAim.IsAimedAtSurfaceForItems(item as ShipItem);
        }

        /// <summary>
        /// Returns whether <see cref="TryAlignLyingCompass"/> would turn the compass <paramref name="button"/> stands
        /// for.
        /// </summary>
        public static bool CanAlignLyingCompass(GoPointerButton button)
        {
            return TryGetLyingCompass(button, out ShipItem compass, out _)
                && TryGetCourseRotation(compass.currentActualBoat, compass.transform.up, out _);
        }

        /// <summary>
        /// Turns the compass <paramref name="button"/> stands for, lying unnailed on a boat, around its own up axis so
        /// that it reads that boat's course, at rest, and returns whether it did.
        /// </summary>
        public static bool TryAlignLyingCompass(GoPointerButton button)
        {
            if (!TryGetLyingCompass(button, out ShipItem compass, out ItemRigidbody twin)
                || !TryGetCourseRotation(compass.currentActualBoat, compass.transform.up, out Quaternion rotation))
                return false;

            Quaternion twinRotation = compass.currentWalkCol.rotation
                * Quaternion.Inverse(compass.currentActualBoat.rotation) * rotation;
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
        /// Gets the compass <paramref name="button"/> stands for and the physics twin it follows, and returns whether
        /// it is a compass lying unnailed and loose on a boat while Compass Course Alignment is on and runs.
        /// </summary>
        private static bool TryGetLyingCompass(GoPointerButton button, out ShipItem compass, out ItemRigidbody twin)
        {
            compass = button as ShipItem;
            twin = null;
            if (!FeatureGuard.IsOn(ModConfig.CompassCourseAlignment)
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
        /// Gets the world rotation, with its up axis along <paramref name="up"/>, at which a compass reads the course
        /// of <paramref name="boat"/>, and returns whether the boat's bow has a heading.
        /// </summary>
        private static bool TryGetCourseRotation(Transform boat, Vector3 up, out Quaternion rotation)
        {
            Vector3 heading = Vector3.ProjectOnPlane(GetHullRotation(boat) * Vector3.forward, Vector3.up);
            bool hasHeading = heading.sqrMagnitude >= VectorUtils.MinDirectionSqrMagnitude;
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
