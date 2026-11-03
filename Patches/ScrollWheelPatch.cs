using DeftHands.Compat;
using DeftHands.Configuration;
using DeftHands.Runtime;
using DeftHands.Utils;
using HarmonyLib;

namespace DeftHands.Patches
{
    /// <summary>
    /// Decides what the scroll wheel does with the held item: push or pull it, let the item's own scroll behaviour or the
    /// game's rotation run, or nothing.
    /// </summary>
    internal static class HeldItemScroll
    {
        /// <summary>Scroll input per unit of hold distance.</summary>
        private const float ScrollInputPerDistance = 40f;

        /// <summary>
        /// Returns whether the holder of <paramref name="item"/> reads this frame's scroll input, turning the item with the
        /// game's rotate key held or passing the input to the item's scroll behaviour otherwise. The holder reads it while
        /// <see cref="GameRotation.TurnsInHand"/>, for an item with a scroll behaviour of its own, while the scroll drives
        /// NANDTweaks' push/pull, and, without a rotate key, while Preferred Push/Pull Control is on.
        /// </summary>
        public static bool LetsHolderReadScroll(PickupableItem item)
        {
            if (item == null)
                return true;

            if (PickupableItemUtils.HasCustomOnScroll(item) || NandTweaksCompat.IsPushKeyPressed())
                return true;

            bool isRotateKeyHeld = GameInput.GetKey(InputName.RotateH) || NandTweaksCompat.IsRotateKeyPressed();
            return GameRotation.TurnsInHand(item) || (!isRotateKeyHeld && ModConfig.PushPullEnabled);
        }

        /// <summary>
        /// Pushes or pulls <paramref name="item"/> by <paramref name="input"/> when the scroll wheel does that right
        /// now, and returns whether the item's own scroll behaviour, the game's tilt included, runs instead. An item with a
        /// scroll behaviour of its own keeps it. Any other item is pushed and pulled while
        /// <see cref="ModInput.IsPushPullScrollActive"/>, and keeps the game's tilt otherwise while it isn't mouse-rotated
        /// and <see cref="GameRotation.TurnsInHand"/>.
        /// </summary>
        public static bool LetsItemScroll(PickupableItem item, float input)
        {
            if (PickupableItemUtils.HasCustomOnScroll(item))
                return true;

            if (ModInput.IsPushPullScrollActive() && !NandTweaksCompat.IsAnyKeyPressed())
            {
                PushPullHandler.GetInstance().Nudge(input / ScrollInputPerDistance);
                return false;
            }

            if (RotationHandler.GetInstance().IsRotating)
                return false;

            return GameRotation.TurnsInHand(item);
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
}
