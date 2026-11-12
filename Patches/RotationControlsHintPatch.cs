using DeftHands.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DeftHands.Patches
{
    /// <summary>
    /// Adds <see cref="RotationControlsHint"/> after the controls text the game shows for the held item.
    /// </summary>
    [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowHoldText))]
    public static class RotationControlsHintPatch
    {
        public static void Postfix(PickupableItem item, TextMesh ___controlsText)
        {
            RotationControlsHint.AddTo(___controlsText, item);
        }
    }

    /// <summary>
    /// Adds <see cref="RotationControlsHint"/> to the controls text the game shows for the surface looked at while the
    /// held item is placed on it.
    /// </summary>
    [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
    public static class OnSurfaceControlsHintPatch
    {
        public static void Postfix(TextMesh ___controlsText, GoPointer ___pointer)
        {
            if (___pointer != null)
                RotationControlsHint.AddOnSurfaceTo(___controlsText, ___pointer.GetHeldItem());
        }
    }
}
