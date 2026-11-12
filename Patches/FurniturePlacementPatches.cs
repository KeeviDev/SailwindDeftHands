using DeftHands.Runtime;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeftHands.Patches
{
    /// <summary>
    /// Records where the look ray hit each time GoPointer casts it.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "FixedUpdate")]
    public static class SurfaceAimPatch
    {
        public static void Postfix(GoPointer __instance)
        {
            SurfaceAim.Record(__instance);
        }
    }

    /// <summary>
    /// Poses the held item through <see cref="FurniturePlacement"/> right after GoPointer places it for the frame.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class PlacementPosePatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void Postfix()
        {
            FurniturePlacement.GetInstance().PlaceHeldItem();
        }
    }

    /// <summary>
    /// Lets a surface for items take the held item only where <see cref="FurniturePlacement"/> allows it to be put
    /// down.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class PlacementClickPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo onItemClick = AccessTools.Method(typeof(GoPointerButton), nameof(GoPointerButton.OnItemClick));
            MethodInfo clickWithItem = AccessTools.Method(typeof(PlacementClickPatch), nameof(ClickWithItem));
            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;

            foreach (CodeInstruction code in codes)
            {
                if (!code.Calls(onItemClick))
                    continue;

                code.opcode = OpCodes.Call;
                code.operand = clickWithItem;
                patched = true;
            }

            if (!patched)
            {
                Plugin.Log.LogError(
                    "Could not find the click with the held item in GoPointer.LateUpdate - the game may have " +
                    "updated. Items will be put down on furniture where the game puts them.");
            }

            return codes;
        }

        /// <summary>
        /// Clicks <paramref name="button"/> with <paramref name="heldItem"/> and returns whether the item is let go.
        /// </summary>
        private static bool ClickWithItem(GoPointerButton button, PickupableItem heldItem)
        {
            return button.OnItemClick(heldItem) && FurniturePlacement.GetInstance().TryPlace(heldItem);
        }
    }
}
