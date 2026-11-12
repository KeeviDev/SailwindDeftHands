using DeftHands.Utils;
using HarmonyLib;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Shows a quadrant's body straight while the quadrant is inspected, without ending the inspection or touching its
    /// reading, and puts the body back where the inspection holds it.
    /// </summary>
    internal static class QuadrantInspection
    {
        private static readonly AccessTools.FieldRef<ShipItemQuadrant, bool> Inspecting =
            ReflectionUtils.FindFieldRef<ShipItemQuadrant, bool>("inspecting");

        private static readonly AccessTools.FieldRef<ShipItemQuadrant, bool> Rotating =
            ReflectionUtils.FindFieldRef<ShipItemQuadrant, bool>("rotating");

        private static readonly AccessTools.FieldRef<ShipItemQuadrant, Transform> RotatingParent =
            ReflectionUtils.FindFieldRef<ShipItemQuadrant, Transform>("rotatingParent");

        private static readonly AccessTools.FieldRef<ShipItemQuadrant, Quaternion> InitialRotation =
            ReflectionUtils.FindFieldRef<ShipItemQuadrant, Quaternion>("initialRot");

        private static readonly AccessTools.FieldRef<ShipItemQuadrant, Quaternion> InspectRotation =
            ReflectionUtils.FindFieldRef<ShipItemQuadrant, Quaternion>("inspectRot");

        private static bool IsSupported =>
            Inspecting != null && Rotating != null && RotatingParent != null && InitialRotation != null
            && InspectRotation != null;

        /// <summary>
        /// Turns the body of the quadrant <paramref name="item"/> straight for this frame while it is inspected or
        /// turning toward or away from the eye.
        /// </summary>
        public static void ShowStraight(PickupableItem item)
        {
            if (TryGetTurnedBody(item, out ShipItemQuadrant quadrant, out Transform body))
                body.localRotation = InitialRotation(quadrant);
        }

        /// <summary>
        /// Puts the body of the quadrant <paramref name="item"/> back where its inspection holds it.
        /// </summary>
        public static void Restore(PickupableItem item)
        {
            if (TryGetTurnedBody(item, out ShipItemQuadrant quadrant, out Transform body) && !Rotating(quadrant))
                body.localRotation = InspectRotation(quadrant);
        }

        /// <summary>
        /// Stops the quadrant <paramref name="item"/> turning its body toward or away from the eye and turns the body
        /// straight, so that letting the quadrant go leaves the body straight.
        /// </summary>
        public static void StopTurning(PickupableItem item)
        {
            if (!TryGetTurnedBody(item, out ShipItemQuadrant quadrant, out Transform body))
                return;

            if (Rotating(quadrant))
            {
                quadrant.StopAllCoroutines();
                Rotating(quadrant) = false;
            }

            body.localRotation = InitialRotation(quadrant);
        }

        private static bool TryGetTurnedBody(PickupableItem item, out ShipItemQuadrant quadrant, out Transform body)
        {
            quadrant = item as ShipItemQuadrant;
            body = null;
            if (quadrant == null || !IsSupported || (!Inspecting(quadrant) && !Rotating(quadrant)))
                return false;

            body = RotatingParent(quadrant);
            return body != null;
        }
    }
}
