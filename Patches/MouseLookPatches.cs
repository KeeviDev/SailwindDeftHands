using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace DeftHands.Patches
{
    /// <summary>
    /// Blocks camera rotation while the player is mouse-rotating a held item, so dragging the
    /// mouse rotates the item instead of also turning the camera.
    /// </summary>
    [HarmonyPatch(typeof(MouseLook), "Update")]
    public static class DisableCameraRotationPatch
    {
        public static bool Prefix()
        {
            return !RotationHandler.GetInstance().IsRotating;
        }
    }

    /// <summary>
    /// Lets the camera turn while the built-in Q axis key is held once Legacy Rotation Control
    /// is off and Q no longer rotates items.
    /// </summary>
    /// <remarks>
    /// Forces MouseLook.Update's Q-key check, which otherwise returns early, to false.
    /// </remarks>
    [HarmonyPatch(typeof(MouseLook), "Update")]
    public static class MouseLookRotateHUnblockPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var getKeyMethod = AccessTools.Method(typeof(GameInput), "GetKey", new[] { typeof(InputName) });
            var mask = AccessTools.Method(typeof(MouseLookRotateHUnblockPatch), nameof(MaskRotateHCheck));

            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(getKeyMethod))
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, mask));
                    patched = true;
                    break;
                }
            }

            if (!patched)
            {
                Plugin.Log.LogError(
                    "Could not find the RotateH check to patch in MouseLook.Update - the game may have updated. " +
                    "The camera will keep ignoring input while Q is held even with Legacy Rotation Control off.");
            }

            return codes;
        }

        private static bool MaskRotateHCheck(bool original)
        {
            return ModConfig.LegacyRotationEnabled.Value && original;
        }
    }
}
