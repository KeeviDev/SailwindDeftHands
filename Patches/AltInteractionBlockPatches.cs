using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;
using UnityEngine;

namespace DeftHands.Patches
{
    /// <summary>
    /// Suppresses the game's alt interaction with the held item while it's being mouse-rotated,
    /// whichever key the alt interaction is bound to. A press that starts during rotation stays
    /// suppressed until it's released.
    /// </summary>
    internal static class AltInteractionBlocker
    {
        private static bool isPressSuppressed;

        /// <summary>
        /// Returns <paramref name="isDown"/>, or false for a press that starts while the held item
        /// is being mouse-rotated; such a press stays suppressed until it's released.
        /// </summary>
        /// <param name="isDown">GoPointer's "alt button pressed this frame" result.</param>
        public static bool FilterDown(bool isDown)
        {
            if (isDown)
                isPressSuppressed = RotationHandler.GetInstance().IsRotating;

            return isDown && !isPressSuppressed;
        }

        /// <summary>
        /// Returns <paramref name="isHeld"/>, or false while the held item is being mouse-rotated
        /// or a suppressed press is still held.
        /// </summary>
        /// <param name="isHeld">GoPointer's "alt button held" result.</param>
        public static bool FilterHeld(bool isHeld)
        {
            if (!isHeld)
            {
                isPressSuppressed = false;
                return false;
            }

            return !isPressSuppressed && !RotationHandler.GetInstance().IsRotating;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "AltButtonDown")]
    public static class AltButtonDownPatch
    {
        private static int lastHandledFrame = -1;

        /// <summary>
        /// Filters the press through <see cref="AltInteractionBlocker"/>. A press that passes, once a frame, locks a
        /// held compass to the course or unlocks it, or else eases the held item back into the hand from
        /// <see cref="ExtendedHold"/>; with nothing held, it points a compass looked at along the course.
        /// </summary>
        public static void Postfix(GoPointer __instance, ref bool __result)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                __result = AltInteractionBlocker.FilterDown(__result);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
                return;
            }

            if (!__result || Time.frameCount == lastHandledFrame)
                return;

            lastHandledFrame = Time.frameCount;
            PickupableItem heldItem = __instance.GetHeldItem();
            if (heldItem == null)
                AlignCompassLookedAt(__instance);
            else if (!ToggleCourseLock(heldItem))
                ReturnToHand(heldItem);
        }

        private static void AlignCompassLookedAt(GoPointer pointer)
        {
            if (!FeatureGuard.IsRunning(ModConfig.CompassCourseAlignment))
                return;

            try
            {
                if (GoPointerFields.PointedAtButton != null)
                    CompassAlignment.TryAlignLyingCompass(GoPointerFields.PointedAtButton(pointer));
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.CompassCourseAlignment, error);
            }
        }

        private static bool ToggleCourseLock(PickupableItem heldItem)
        {
            if (!FeatureGuard.IsRunning(ModConfig.CompassCourseAlignment))
                return false;

            try
            {
                return CompassAlignment.TryToggleCourseLock(heldItem);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.CompassCourseAlignment, error);
                return false;
            }
        }

        private static void ReturnToHand(PickupableItem heldItem)
        {
            if (!FeatureGuard.IsRunning(ModConfig.ExtendedSmallItemsRotation))
                return;

            try
            {
                ExtendedHold.GetInstance().ReturnToHand(heldItem);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.ExtendedSmallItemsRotation, error);
            }
        }
    }

    [HarmonyPatch(typeof(GoPointer), "AltButtonHeld")]
    public static class AltButtonHeldPatch
    {
        public static void Postfix(ref bool __result)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                __result = AltInteractionBlocker.FilterHeld(__result);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
            }
        }
    }
}
