using DeftHands.Runtime;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DeftHands.Patches
{
    /// <summary>
    /// Makes GoPointer.LateUpdate read the pick-up key as not pressed during the click that picked the held item up
    /// with the rotation modifier key held, so that click neither lets the item go on its release nor winds up a throw.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class ModifierPickupClickPatch
    {
        /// <summary>Whether the pick-up key reads as not pressed until it is released.</summary>
        private static bool ignoresPickupKey;

        /// <summary>
        /// Registers the currently held item: the click that picked it up is ignored when it was picked up by pointing
        /// at it with the rotation modifier key held.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        public static void SetCurrentItem(PickupableItem item)
        {
            ignoresPickupKey = item != null && ExtendedHold.IsPickedUpWithModifierKey(item);
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getKey = AccessTools.Method(
                typeof(GameInput), nameof(GameInput.GetKey), new[] { typeof(InputName) });
            MethodInfo getKeyUp = AccessTools.Method(
                typeof(GameInput), nameof(GameInput.GetKeyUp), new[] { typeof(InputName) });
            MethodInfo readKey = AccessTools.Method(typeof(ModifierPickupClickPatch), nameof(ReadKey));
            MethodInfo readKeyUp = AccessTools.Method(typeof(ModifierPickupClickPatch), nameof(ReadKeyUp));
            var codes = new List<CodeInstruction>(instructions);
            bool readsKeyUp = false;

            foreach (CodeInstruction code in codes)
            {
                if (code.Calls(getKey))
                {
                    code.operand = readKey;
                }
                else if (code.Calls(getKeyUp))
                {
                    code.operand = readKeyUp;
                    readsKeyUp = true;
                }
            }

            if (!readsKeyUp)
            {
                Plugin.Log.LogError(
                    "Could not find the key checks in GoPointer.LateUpdate - the game may have updated. " +
                    "Releasing the click that picked an item up with the modifier key held may let it go.");
            }

            return codes;
        }

        /// <summary>
        /// Stops ignoring the pick-up key once it is up, also after a LateUpdate that threw, and passes on what it
        /// threw.
        /// </summary>
        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            if (!GameInput.GetKey(InputName.PickUp))
                ignoresPickupKey = false;

            return __exception;
        }

        private static bool ReadKey(InputName input)
        {
            return GameInput.GetKey(input) && !IsIgnored(input);
        }

        private static bool ReadKeyUp(InputName input)
        {
            return GameInput.GetKeyUp(input) && !IsIgnored(input);
        }

        private static bool IsIgnored(InputName input)
        {
            return ignoresPickupKey && input == InputName.PickUp;
        }
    }
}
