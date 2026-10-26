using DeftHands.Runtime;
using HarmonyLib;

namespace DeftHands.Patches
{
    /// <summary>
    /// Applies <see cref="BigItemFacing"/> before GoPointer places the held big item for the frame.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class BigItemFacingPatch
    {
        public static void Prefix(GoPointer __instance)
        {
            BigItemFacing.KeepFacing(__instance);
        }
    }
}
