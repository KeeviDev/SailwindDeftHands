using DeftHands.Runtime;
using HarmonyLib;

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
            if (!CompassAlignment.KeepCourse(__instance))
                BigItemFacing.KeepFacing(__instance);
        }
    }
}
