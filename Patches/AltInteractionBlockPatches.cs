using DeftHands.Runtime;
using HarmonyLib;
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
        /// Filters the press through <see cref="AltInteractionBlocker"/>. A press that passes, once a frame, locks a held
        /// compass to the course or unlocks it, or else eases the held item back into the hand from
        /// <see cref="ExtendedHold"/>; with nothing held, it points a compass looked at along the course.
        /// </summary>
        public static void Postfix(GoPointer __instance, ref bool __result)
        {
            __result = AltInteractionBlocker.FilterDown(__result);
            if (!__result || Time.frameCount == lastHandledFrame)
                return;

            lastHandledFrame = Time.frameCount;
            PickupableItem heldItem = __instance.GetHeldItem();
            if (heldItem == null)
            {
                if (GoPointerFields.PointedAtButton != null)
                    CompassAlignment.TryAlignLyingCompass(GoPointerFields.PointedAtButton(__instance));
            }
            else if (!CompassAlignment.TryToggleCourseLock(heldItem))
            {
                ExtendedHold.GetInstance().ReturnToHand(heldItem);
            }
        }
    }

    [HarmonyPatch(typeof(GoPointer), "AltButtonHeld")]
    public static class AltButtonHeldPatch
    {
        public static void Postfix(ref bool __result)
        {
            __result = AltInteractionBlocker.FilterHeld(__result);
        }
    }
}
