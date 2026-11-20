using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;

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
            if (!FeatureGuard.IsRunning(ModConfig.WallAttachmentTopArrow))
                return;

            try
            {
                PreviewTopArrow.Follow(__instance);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.WallAttachmentTopArrow, error);
            }
        }
    }
}
