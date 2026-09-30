using DeftHands.Runtime;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Patches
{
    /// <summary>
    /// Applies <see cref="WallAttachmentRoll"/> to the attachment rotation ShipItem.Update computes
    /// for the held item each frame, and refreshes the placement preview to match. Runs as a
    /// postfix so it still applies when another mod's prefix replaces ShipItem.Update.
    /// </summary>
    [HarmonyPatch(typeof(ShipItem), "Update")]
    public static class WallAttachmentRollPatch
    {
        private static readonly MethodInfo SetUpTargeterMethod = AccessTools.Method(typeof(ShipItem), "SetUpTargeter");

        private static readonly Action<ShipItem, Vector3, Quaternion, Mesh> SetUpTargeter = SetUpTargeterMethod != null
            ? AccessTools.MethodDelegate<Action<ShipItem, Vector3, Quaternion, Mesh>>(SetUpTargeterMethod)
            : null;

        public static void Postfix(ShipItem __instance, bool ___inRangeOfWall, Vector3 ___attachPos, ref Quaternion ___attachRot)
        {
            if (!___inRangeOfWall || !WallAttachmentRoll.IsActive)
                return;

            if (WallAttachmentRoll.TryRollAttachment(__instance, ___attachPos, ref ___attachRot))
                SetUpTargeter?.Invoke(__instance, ___attachPos, ___attachRot, __instance.GetComponent<MeshFilter>().sharedMesh);
        }
    }

    /// <summary>
    /// Rolls the held wall-attachable item in the player's hand to match how it will attach.
    /// </summary>
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    public static class HeldItemRollPatch
    {
        public static void Prefix(GoPointer __instance)
        {
            WallAttachmentRoll.UnrollHeldItem(__instance.GetHeldItem());
        }

        public static void Postfix(GoPointer __instance)
        {
            WallAttachmentRoll.RollHeldItem(__instance.GetHeldItem());
        }
    }
}
