using DeftHands.Compat;
using DeftHands.Configuration;
using DeftHands.Runtime;
using DeftHands.Utils;
using HarmonyLib;

namespace DeftHands.Patches
{
    /// <summary>
    /// Repurposes the plain scroll wheel for push/pull distance control, and blocks scroll-based
    /// rotation while mouse-rotating or when Legacy Rotation Control is off. Items with their own
    /// OnScroll behaviour are left alone. Also blocks legacy rotation in case
    /// <see cref="LegacyRotationBlockPatch"/> fails to apply.
    /// </summary>
    [HarmonyPatch(typeof(PickupableItem), "OnScroll")]
    public static class ScrollWheelPatch
    {
        /// <summary>Scroll input per unit of hold distance, the same ratio NANDTweaks' push/pull uses.</summary>
        private const float ScrollInputPerDistance = 40f;

        public static bool Prefix(float input, PickupableItem __instance)
        {
            if (PickupableItemUtils.HasCustomOnScroll(__instance))
                return true;

            if (ModConfig.PushPullScrollEnabled.Value && !NandTweaksCompat.IsAnyKeyPressed())
            {
                PushPullHandler.GetInstance().Nudge(input / ScrollInputPerDistance);
                return false;
            }

            if (RotationHandler.GetInstance().IsRotating)
                return false;

            return ModConfig.LegacyRotationEnabled.Value;
        }
    }
}
