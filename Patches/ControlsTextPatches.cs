using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;
using UnityEngine;

namespace DeftHands.Patches
{
    /// <summary>
    /// Adds Deft Hands' lines to the controls text the game shows for the held item or the item looked at.
    /// </summary>
    [HarmonyPatch]
    public static class ControlsTextPatches
    {
        /// <summary>
        /// Hides the keys of <see cref="HungPickupHint"/>, then adds the compass course line and
        /// <see cref="RotationControlsHint"/> above the held item's controls text.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowHoldText))]
        private static void AfterShowHoldText(LookUI __instance, PickupableItem item, TextMesh ___controlsText)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.ControlsHints))
                return;

            try
            {
                HungPickupHint.Hide(__instance);
                CompassCourseHint.AddToHoldText(__instance, ___controlsText, item);
                RotationControlsHint.AddTo(___controlsText, item);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.ControlsHints, error);
            }
        }

        /// <summary>
        /// Adds the compass course line and the line for picking an item up as it hangs to the controls text of the
        /// item looked at, then <see cref="RotationControlsHint"/> for a held item placed on the surface looked at.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
        private static void AfterShowLookText(
            LookUI __instance,
            GoPointerButton button,
            TextMesh ___controlsText,
            GoPointer ___pointer)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.ControlsHints))
                return;

            try
            {
                CompassCourseHint.AddToLookText(__instance, ___controlsText, button);
                HungPickupHint.AddToLookText(__instance, ___controlsText, ___pointer, button);
                if (___pointer != null)
                    RotationControlsHint.AddOnSurfaceTo(___controlsText, ___pointer.GetHeldItem());
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.ControlsHints, error);
            }
        }

        /// <summary>
        /// Hides the keys of <see cref="HungPickupHint"/> along with the game's own key icons.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LookUI), "HideIcons")]
        private static void AfterHideIcons(LookUI __instance)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.ControlsHints))
                return;

            try
            {
                HungPickupHint.Hide(__instance);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.ControlsHints, error);
            }
        }
    }
}
