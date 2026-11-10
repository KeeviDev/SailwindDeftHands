using DeftHands.Runtime;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeftHands.Patches
{
    /// <summary>
    /// Makes GoPointer.LateUpdate treat the small item <see cref="ExtendedHold"/> holds like a big item as big: it
    /// places the item the way it places big items and doesn't draw it on a table or other surface for items;
    /// <see cref="ExtendedHold"/> keeps the rotation it gives the item.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class HeldLikeBigPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        private static void Postfix(GoPointer __instance)
        {
            ExtendedHold.RememberPlacedRotation(__instance);
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo big = AccessTools.Field(typeof(PickupableItem), nameof(PickupableItem.big));
            MethodInfo isHeldLikeBig = AccessTools.Method(typeof(ExtendedHold), nameof(ExtendedHold.IsHeldLikeBig));
            var codes = new List<CodeInstruction>(instructions);
            int replaced = 0;

            foreach (CodeInstruction code in codes)
            {
                if (!code.LoadsField(big))
                    continue;

                code.opcode = OpCodes.Call;
                code.operand = isHeldLikeBig;
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
    }
}
