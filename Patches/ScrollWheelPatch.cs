using DeftHands.Compat;
using DeftHands.Configuration;
using DeftHands.Runtime;
using DeftHands.Utils;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeftHands.Patches
{
    /// <summary>
    /// Decides what the scroll wheel does with the held item: push or pull it, let the item's own scroll behaviour run,
    /// or neither.
    /// </summary>
    internal static class HeldItemScroll
    {
        /// <summary>Scroll input per unit of hold distance.</summary>
        private const float ScrollInputPerDistance = 40f;

        /// <summary>
        /// Pushes or pulls <paramref name="item"/> by <paramref name="input"/> when the scroll wheel does that right
        /// now, and returns whether the item's own scroll behaviour runs instead. A held item that draws nothing keeps
        /// its own scroll behaviour. An item with a scroll behaviour of its own keeps it only while not held like a big
        /// item, and is pushed and pulled only while held like a big item or with the rotation modifier key held. Any
        /// other item is pushed and pulled while <see cref="ModInput.IsPushPullScrollActive"/>, and keeps the game's
        /// scroll rotation while Legacy Rotation Control is on, it isn't mouse-rotated and
        /// <see cref="WallPlacement.LocksRotationAxis"/> is false.
        /// </summary>
        public static bool LetsItemScroll(PickupableItem item, float input)
        {
            if (HeldItemVisibility.IsHidden(item))
                return true;

            if (PickupableItemUtils.HasCustomOnScroll(item))
                return LetsOwnScrollRun(item, input);

            if (ModInput.IsPushPullScrollActive() && !NandTweaksCompat.IsAnyKeyPressed())
            {
                PushPullHandler.GetInstance().Nudge(input / ScrollInputPerDistance);
                return false;
            }

            if (RotationHandler.GetInstance().IsRotating)
                return false;

            return ModConfig.LegacyRotationEnabled.Value && !WallPlacement.LocksRotationAxis(item);
        }

        private static bool LetsOwnScrollRun(PickupableItem item, float input)
        {
            bool isHeldLikeBig = ExtendedHold.IsHeldLikeBig(item);
            bool pushesOrPulls = isHeldLikeBig
                ? ModInput.IsPushPullScrollActive()
                : ModConfig.PushPullScrollEnabled.Value && ModInput.IsRotationModifierKeyHeld() && ExtendedHold.CanHoldLikeBig(item);
            if (pushesOrPulls && !NandTweaksCompat.IsAnyKeyPressed())
            {
                PushPullHandler.GetInstance().Nudge(input / ScrollInputPerDistance);
                return false;
            }

            return !isHeldLikeBig;
        }
    }

    /// <summary>
    /// Runs <see cref="HeldItemScroll"/> before the scroll behaviour items get from PickupableItem itself.
    /// </summary>
    [HarmonyPatch(typeof(PickupableItem), "OnScroll")]
    public static class ScrollWheelPatch
    {
        public static bool Prefix(float input, PickupableItem __instance)
        {
            return HeldItemScroll.LetsItemScroll(__instance, input);
        }
    }

    /// <summary>
    /// Runs <see cref="HeldItemScroll"/> when GoPointer.LateUpdate passes the scroll wheel to a held item whose class
    /// overrides PickupableItem.OnScroll.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class OwnScrollPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo onScroll = AccessTools.Method(typeof(PickupableItem), nameof(PickupableItem.OnScroll));
            MethodInfo scrollHeldItem = AccessTools.Method(typeof(OwnScrollPatch), nameof(ScrollHeldItem));
            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;

            foreach (CodeInstruction code in codes)
            {
                if (!code.Calls(onScroll))
                    continue;

                code.opcode = OpCodes.Call;
                code.operand = scrollHeldItem;
                patched = true;
            }

            if (!patched)
            {
                Plugin.Log.LogError(
                    "Could not find the held item's scroll call in GoPointer.LateUpdate - the game may have updated. " +
                    "Compasses, spyglasses and other items with their own scroll use won't be pushed or pulled.");
            }

            return codes;
        }

        private static void ScrollHeldItem(PickupableItem item, float input)
        {
            if (!PickupableItemUtils.OverridesOnScroll(item) || HeldItemScroll.LetsItemScroll(item, input))
                item.OnScroll(input);
        }
    }
}
