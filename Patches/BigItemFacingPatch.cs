using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;

namespace DeftHands.Patches
{
    /// <summary>
    /// Keeps a compass locked to the course on it, and any other held big item or small item held like one facing the
    /// way <see cref="BigItemFacing"/> keeps it, before GoPointer places it for the frame.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class BigItemFacingPatch
    {
        public static void Prefix(GoPointer __instance)
        {
            if (KeepsCourse(__instance))
                return;

            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                BigItemFacing.KeepFacing(__instance);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
            }
        }

        private static bool KeepsCourse(GoPointer holder)
        {
            if (!FeatureGuard.IsRunning(ModConfig.CompassCourseAlignment))
                return false;

            try
            {
                return CompassAlignment.KeepCourse(holder);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.CompassCourseAlignment, error);
                return false;
            }
        }
    }
}
