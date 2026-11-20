using DeftHands.Compat;
using DeftHands.Configuration;
using DeftHands.Runtime;
using DeftHands.Utils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeftHands.Patches
{
    /// <summary>
    /// Decides what the scroll wheel does with the held item: push or pull it, let the item's own scroll behaviour or
    /// the game's rotation run, turn it on a surface for items, or nothing.
    /// </summary>
    internal static class HeldItemScroll
    {
        /// <summary>Scroll input per unit of hold distance.</summary>
        private const float ScrollInputPerDistance = 40f;

        /// <summary>
        /// Returns whether the holder of <paramref name="item"/> reads this frame's scroll <paramref name="input"/>, to
        /// turn the item with the game's rotate key held or to pass the input to the item's scroll behaviour. With the
        /// rotate key held, an item placed on a surface for items is turned around the surface's up instead, while
        /// <see cref="GameRotation.TurnsOnFurniture"/>.
        /// </summary>
        public static bool LetsHolderReadScroll(PickupableItem item, float input)
        {
            if (item == null)
                return true;

            if (WallAttachmentAdjustment.TakesDepthInput(item))
                return !GameInput.GetKey(InputName.RotateH);

            if (FurniturePlacement.IsPlacing(item))
                return GameInput.GetKey(InputName.RotateH)
                    ? TurnPlacedItem(item, input)
                    : LetsScrollReachPlacedItem(item);

            if (PickupableItemUtils.HasCustomOnScroll(item) || NandTweaksCompat.IsPushKeyPressed())
                return true;

            bool isRotateKeyHeld = GameInput.GetKey(InputName.RotateH) || NandTweaksCompat.IsRotateKeyPressed();
            return GameRotation.TurnsInHand(item) || (!isRotateKeyHeld && ModConfig.PushPullEnabled);
        }

        /// <summary>
        /// Turns the held <paramref name="item"/> placed on a surface for items around the surface's up by scroll
        /// <paramref name="input"/> while <see cref="GameRotation.TurnsOnFurniture"/>, and returns false.
        /// </summary>
        private static bool TurnPlacedItem(PickupableItem item, float input)
        {
            if (GameRotation.TurnsOnFurniture(item))
                GameRotation.TurnOnFurniture(input);

            return false;
        }

        private static bool LetsScrollReachPlacedItem(PickupableItem item)
        {
            return (PickupableItemUtils.HasCustomOnScroll(item) && !ExtendedHold.IsHeldLikeBig(item))
                || TiltsPlacedItem(item);
        }

        private static bool TiltsPlacedItem(PickupableItem item)
        {
            return GameRotation.TurnsOnFurniture(item) && !NandTweaksCompat.IsAnyKeyPressed();
        }

        /// <summary>
        /// Returns whether the scroll behaviour of <paramref name="item"/>, the game's tilt included, runs for this
        /// scroll <paramref name="input"/>. When it doesn't, the wheel may push or pull the item, set how deep it
        /// hangs, or tilt it on the surface for items it is placed on.
        /// </summary>
        public static bool LetsItemScroll(PickupableItem item, float input)
        {
            if (HeldItemVisibility.IsHidden(item))
                return true;

            if (WallAttachmentAdjustment.TakesDepthInput(item) && !NandTweaksCompat.IsAnyKeyPressed())
            {
                WallAttachmentAdjustment.AddScrollInput(input);
                return false;
            }

            if (FurniturePlacement.IsPlacing(item))
                return LetsPlacedItemScroll(item, input);

            if (PickupableItemUtils.HasCustomOnScroll(item))
                return LetsOwnScrollRun(item, input);

            if (ModInput.IsPushPullScrollActive() && !NandTweaksCompat.IsAnyKeyPressed()
                && PushPullHandler.CanMove(item))
            {
                PushPullHandler.GetInstance().Nudge(input / ScrollInputPerDistance);
                return false;
            }

            if (RotationHandler.GetInstance().IsRotating)
                return false;

            return GameRotation.TurnsInHand(item);
        }

        private static bool LetsPlacedItemScroll(PickupableItem item, float input)
        {
            if (PickupableItemUtils.HasCustomOnScroll(item) && !ExtendedHold.IsHeldLikeBig(item))
                return true;

            if (TiltsPlacedItem(item))
                GameRotation.TiltOnFurniture(input);

            return false;
        }

        private static bool LetsOwnScrollRun(PickupableItem item, float input)
        {
            bool isHeldLikeBig = ExtendedHold.IsHeldLikeBig(item);
            bool pushesOrPulls = isHeldLikeBig
                ? ModInput.IsPushPullScrollActive()
                : ModConfig.PushPullEnabled
                    && ModInput.IsRotationModifierKeyHeld()
                    && ExtendedHold.CanHoldLikeBig(item);
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
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return true;

            try
            {
                return HeldItemScroll.LetsItemScroll(__instance, input);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
                return true;
            }
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
            if (LetsHeldItemScroll(item, input))
                item.OnScroll(input);
        }

        private static bool LetsHeldItemScroll(PickupableItem item, float input)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return true;

            try
            {
                return !PickupableItemUtils.OverridesOnScroll(item) || HeldItemScroll.LetsItemScroll(item, input);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
                return true;
            }
        }
    }
}
