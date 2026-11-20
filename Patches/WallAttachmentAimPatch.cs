using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace DeftHands.Patches
{
    /// <summary>
    /// Makes ShipItem.Update find where a held wall-attachable item attaches through
    /// <see cref="WallAttachmentAim.Raycast"/>, or through the game's own raycast once errors turned Easy Wall Hanging
    /// off.
    /// </summary>
    [HarmonyPatch(typeof(ShipItem), "Update")]
    public static class WallAttachmentAimPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo vanillaRaycast = AccessTools.Method(
                typeof(Physics),
                nameof(Physics.Raycast),
                new[] { typeof(Ray), typeof(RaycastHit).MakeByRefType(), typeof(float) });
            MethodInfo aimRaycast = AccessTools.Method(typeof(WallAttachmentAimPatch), nameof(Raycast));
            var codes = new List<CodeInstruction>(instructions);

            int index = codes.FindIndex(code => code.Calls(vanillaRaycast));
            if (index < 0)
            {
                Plugin.Log.LogError(
                    "Could not find the wall attachment raycast in ShipItem.Update - the game may have updated. " +
                    "Items will hang where they point instead of where you look.");
                return codes;
            }

            var loadItem = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[index]);
            codes[index] = new CodeInstruction(OpCodes.Call, aimRaycast);
            codes.Insert(index, loadItem);
            return codes;
        }

        private static bool Raycast(Ray itemRay, out RaycastHit hit, float maxDistance, ShipItem item)
        {
            if (FeatureGuard.IsRunning(ModConfig.EasyWallHanging))
            {
                try
                {
                    return WallAttachmentAim.Raycast(itemRay, out hit, maxDistance, item);
                }
                catch (Exception error)
                {
                    FeatureGuard.ReportError(ModConfig.EasyWallHanging, error);
                }
            }

            return Physics.Raycast(itemRay, out hit, maxDistance);
        }
    }
}
