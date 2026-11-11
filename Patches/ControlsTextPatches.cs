using DeftHands.Runtime;
using HarmonyLib;
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
        /// Adds the compass course line, then <see cref="RotationControlsHint"/> above the held item's controls text.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowHoldText))]
        private static void AfterShowHoldText(LookUI __instance, PickupableItem item, TextMesh ___controlsText)
        {
            CompassCourseHint.AddToHoldText(__instance, ___controlsText, item);
            RotationControlsHint.AddTo(___controlsText, item);
        }

        /// <summary>
        /// Adds the compass course line to the controls text of the item looked at.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
        private static void AfterShowLookText(LookUI __instance, GoPointerButton button, TextMesh ___controlsText)
        {
            CompassCourseHint.AddToLookText(__instance, ___controlsText, button);
        }
    }
}
