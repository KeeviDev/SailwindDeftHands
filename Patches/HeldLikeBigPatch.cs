using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeftHands.Patches
{
    /// <summary>
    /// Makes GoPointer.LateUpdate treat the small item <see cref="ExtendedHold"/> holds like a big item as big, except
    /// while <see cref="FurniturePlacement"/> places it on a table or other surface for items: it places the item the
    /// way it places big items, and <see cref="ExtendedHold"/> keeps the rotation it gives the item.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class HeldLikeBigPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        private static void Postfix(GoPointer __instance)
        {
            if (!FeatureGuard.IsRunning(ModConfig.ExtendedSmallItemsRotation))
                return;

            try
            {
                ExtendedHold.RememberPlacedRotation(__instance);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.ExtendedSmallItemsRotation, error);
            }
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo big = AccessTools.Field(typeof(PickupableItem), nameof(PickupableItem.big));
            MethodInfo isCarriedLikeBig = AccessTools.Method(typeof(HeldLikeBigPatch), nameof(IsCarriedLikeBig));
            var codes = new List<CodeInstruction>(instructions);
            int replaced = 0;

            foreach (CodeInstruction code in codes)
            {
                if (!code.LoadsField(big))
                    continue;

                code.opcode = OpCodes.Call;
                code.operand = isCarriedLikeBig;
                replaced++;
            }

            ExtendedHold.IsPlacementPatched = replaced > 0;
            if (replaced == 0)
            {
                Plugin.Log.LogError(
                    "Could not find the big item checks in GoPointer.LateUpdate - the game may have updated. " +
                    "Small items will keep their usual hold when rotated, pushed or pulled.");
            }

            return codes;
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> is big, or is the small item held like a big item and not placed on
        /// a table or other surface for items.
        /// </summary>
        private static bool IsCarriedLikeBig(PickupableItem item)
        {
            if (!FeatureGuard.IsRunning(ModConfig.ExtendedSmallItemsRotation))
                return item.big;

            try
            {
                return ExtendedHold.IsHeldLikeBig(item) && !FurniturePlacement.IsPlacing(item);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.ExtendedSmallItemsRotation, error);
                return item.big;
            }
        }
    }
}
