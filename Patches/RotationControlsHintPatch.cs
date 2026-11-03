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
}
