using DeftHands.Compat;
using DeftHands.Configuration;
using DeftHands.Utils;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DeftHands.Patches
{
    /// <summary>
    /// Blocks GoPointer's built-in scroll-wheel rotation (both axes) when Legacy Rotation
    /// Control is off. Items with their own OnScroll behaviour, NANDTweaks' push/pull and
    /// Push/Pull on Scroll Wheel keep receiving scroll input.
    /// </summary>
    /// <remarks>
    /// Forces the scroll delta GoPointer.LateUpdate checks before its rotation branch to 0,
    /// which skips that branch entirely.
    /// </remarks>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class LegacyRotationBlockPatch
    {
        private static bool shouldBlockRotation;

        [HarmonyPrefix]
        private static void Prefix(GoPointer __instance)
        {
            shouldBlockRotation = ShouldBlockRotation(__instance.GetHeldItem());
        }

        private static bool ShouldBlockRotation(PickupableItem heldItem)
        {
            if (ModConfig.LegacyRotationEnabled.Value || heldItem == null)
                return false;

            if (PickupableItemUtils.HasCustomOnScroll(heldItem) || NandTweaksCompat.IsPushKeyPressed())
                return false;

            bool isRotationKeyHeld = GameInput.GetKey(InputName.RotateH) || NandTweaksCompat.IsRotateKeyPressed();
            return isRotationKeyHeld || !ModConfig.PushPullScrollEnabled.Value;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo mask = AccessTools.Method(typeof(LegacyRotationBlockPatch), nameof(MaskScrollValue));
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

        private static float MaskScrollValue(float original)
        {
            return shouldBlockRotation ? 0f : original;
        }
    }
}
