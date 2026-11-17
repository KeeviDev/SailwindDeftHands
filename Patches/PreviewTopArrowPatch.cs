using DeftHands.Runtime;
using HarmonyLib;

namespace DeftHands.Patches
{
    /// <summary>
    /// Keeps <see cref="PreviewTopArrow"/> beside the preview each time GoPointerTargeter places or hides it.
    /// </summary>
    [HarmonyPatch(typeof(GoPointerTargeter), "Update")]
    internal static class PreviewTopArrowPatch
    {
        private static void Postfix(GoPointerTargeter __instance)
        {
            PreviewTopArrow.Follow(__instance);
        }
    }
}
