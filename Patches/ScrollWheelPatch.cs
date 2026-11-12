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
    /// Decides what the scroll wheel does with the held item: push or pull it, let the item's own scroll behaviour or
    /// the game's rotation run, turn it on a surface for items, or nothing.
    /// </summary>
    internal static class HeldItemScroll
    {
        /// <summary>Scroll input per unit of hold distance.</summary>
        private const float ScrollInputPerDistance = 40f;

        /// <summary>
        /// Returns whether the holder of <paramref name="item"/> reads this frame's scroll <paramref name="input"/>,
        /// turning the item with the game's rotate key held or passing the input to the item's scroll behaviour
        /// otherwise. An item placed on a surface for items is turned around the surface's up instead with the rotate
        /// key held, while <see cref="GameRotation.TurnsOnSurface"/>, and passes the input on only to a scroll behaviour
        /// of its own or the game's tilt on the surface. For any other item, the holder reads it while
        /// <see cref="GameRotation.TurnsInHand"/>, for an item with a scroll behaviour of its own, while the scroll
        /// drives NANDTweaks' push/pull, and, without a rotate key, while Preferred Push/Pull Control is on.
        /// </summary>
        public static bool LetsHolderReadScroll(PickupableItem item, float input)
        {
            if (item == null)
                return true;

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
        /// Pushes or pulls <paramref name="item"/> by <paramref name="input"/> when the scroll wheel does that right
        /// now, and returns whether the item's own scroll behaviour, the game's tilt included, runs instead. A held item
        /// that draws nothing keeps its own scroll behaviour. An item placed on a surface for items is never pushed or
        /// pulled: it keeps only a scroll behaviour of its own, while not held like a big item, and is tilted on the
        /// surface otherwise while <see cref="GameRotation.TurnsOnSurface"/> and no NANDTweaks key is held. An item with a
        /// scroll behaviour of its own keeps it only while not held like a big item, and is pushed and pulled only while
        /// held like a big item or with the rotation modifier key held. Any other item is pushed and pulled while
        /// <see cref="ModInput.IsPushPullScrollActive"/> and <see cref="PushPullHandler.CanMove"/>, and keeps the game's
        /// tilt otherwise while it isn't mouse-rotated and <see cref="GameRotation.TurnsInHand"/>.
        /// </summary>
        public static bool LetsItemScroll(PickupableItem item, float input)
        {
            if (HeldItemVisibility.IsHidden(item))
                return true;

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
