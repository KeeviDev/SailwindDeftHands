using DeftHands.Compat;
using DeftHands.Configuration;
using DeftHands.Runtime;
using DeftHands.Utils;
using HarmonyLib;

namespace DeftHands.Patches
{
    /// <summary>
    /// Turns the scroll wheel into push/pull distance control while <see cref="ModInput.IsPushPullScrollActive"/>,
    /// and blocks scroll-based rotation while mouse-rotating or when Legacy Rotation Control is off. Applies to
    /// items without their own OnScroll behaviour.
    /// </summary>
    [HarmonyPatch(typeof(PickupableItem), "OnScroll")]
    public static class ScrollWheelPatch
    {
        /// <summary>Scroll input per unit of hold distance.</summary>
        private const float ScrollInputPerDistance = 40f;

        public static bool Prefix(float input, PickupableItem __instance)
        {
            if (PickupableItemUtils.HasCustomOnScroll(__instance))
                return true;

            if (ModInput.IsPushPullScrollActive() && !NandTweaksCompat.IsAnyKeyPressed())
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
