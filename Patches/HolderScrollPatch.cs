using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeftHands.Patches
{
    /// <summary>
    /// Lets GoPointer.LateUpdate turn or scroll the held item with the scroll wheel only where
    /// <see cref="HeldItemScroll.LetsHolderReadScroll"/> allows it.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class HolderScrollPatch
    {
        private static PickupableItem heldItem;

        [HarmonyPrefix]
        private static void Prefix(GoPointer __instance)
        {
            heldItem = __instance.GetHeldItem();
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo mask = AccessTools.Method(typeof(HolderScrollPatch), nameof(MaskScrollValue));
            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;

            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i].LoadsConstant(0f) && codes[i - 1].IsLdloc())
                {
                    codes.Insert(i, new CodeInstruction(OpCodes.Call, mask));
                    patched = true;
                    break;
                }
            }

            if (!patched)
            {
                Plugin.Log.LogError(
                    "Could not find the scroll check to patch in GoPointer.LateUpdate - the game may have updated. " +
                    "Legacy Rotation Control will not be able to block scroll-wheel rotation.");
            }

            return codes;
        }

        private static float MaskScrollValue(float input)
        {
            return HeldItemScroll.LetsHolderReadScroll(heldItem) ? input : 0f;
        }
    }
}
