using DeftHands.Runtime;
using DeftHands.Utils;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Patches
{
    /// <summary>
    /// Applies <see cref="WallAttachmentRoll"/> to the attachment pose ShipItem.Update computes for the held item each
    /// frame, and refreshes the placement preview to match. For a hook <see cref="WallAttachmentRoll.AimsHook"/> is true
    /// for, it works out where the hook attaches the way the game does before that.
    /// </summary>
    [HarmonyPatch(typeof(ShipItem), "Update")]
    public static class WallAttachmentRollPatch
    {
        /// <summary>Farthest a surface may lie from where the attachment ray starts, as in the game.</summary>
        private const float AttachmentReach = 1.3f;

        /// <summary>Nearest a surface may lie to where the attachment ray starts, as in the game.</summary>
        private const float MinAttachmentDistance = 0.1f;

        /// <summary>
        /// Smallest up component of a surface normal at which the item faces it with its own up, as in the game.
        /// </summary>
        private const float MinFloorNormalY = 0.8f;

        private static readonly MethodInfo SetUpTargeterMethod = AccessTools.Method(typeof(ShipItem), "SetUpTargeter");

        private static readonly Action<ShipItem, Vector3, Quaternion, Mesh> SetUpTargeter = SetUpTargeterMethod != null
            ? AccessTools.MethodDelegate<Action<ShipItem, Vector3, Quaternion, Mesh>>(SetUpTargeterMethod)
            : null;

        private static readonly AccessTools.FieldRef<GoPointerTargeter, bool> TargeterActive =
            ReflectionUtils.FindFieldRef<GoPointerTargeter, bool>("targeterActive");

        public static void Postfix(
            ShipItem __instance,
            Transform ___itemRigidbody,
            ref bool ___inRangeOfWall,
            ref Vector3 ___attachPos,
            ref Quaternion ___attachRot)
        {
            bool aimsHook = WallAttachmentRoll.AimsHook(__instance) && ___itemRigidbody != null;
            if (aimsHook)
                FindAttachment(__instance, ___itemRigidbody, ref ___inRangeOfWall, ref ___attachPos, ref ___attachRot);

            if (!___inRangeOfWall)
                return;

            bool isTurned = WallAttachmentRoll.TryRollAttachment(__instance, ___attachPos, ref ___attachRot);
            if (aimsHook || isTurned)
                SetUpTargeter?.Invoke(__instance, ___attachPos, ___attachRot, __instance.GetComponent<MeshFilter>().sharedMesh);
        }

        /// <summary>
        /// Works out where the held hook <paramref name="item"/> attaches the way the game does, along the ray of
        /// <see cref="WallAttachmentAim"/> and turned by <see cref="WallAttachmentRoll.GetHookTurn"/>, and hides the
        /// preview another mod asked for this frame when it attaches nowhere.
        /// </summary>
        /// <param name="body">The item's physics twin, where the game's attachment ray starts.</param>
        private static void FindAttachment(
            ShipItem item, Transform body, ref bool inRangeOfWall, ref Vector3 attachPos, ref Quaternion attachRot)
        {
            Ray itemRay = new Ray(body.position, body.forward);
            inRangeOfWall = WallAttachmentAim.Raycast(itemRay, out RaycastHit hit, AttachmentReach, item)
                && hit.distance >= MinAttachmentDistance;
            if (!inRangeOfWall)
            {
                HidePreview(item);
                return;
            }

            attachPos = hit.point;
            Quaternion facingSurface = hit.normal.y > MinFloorNormalY
                ? Quaternion.LookRotation(-hit.normal, item.transform.up)
                : Quaternion.LookRotation(-hit.normal, Vector3.up);
            attachRot = facingSurface * Quaternion.AngleAxis(WallAttachmentRoll.GetHookTurn(item), Vector3.forward);
        }

        private static void HidePreview(ShipItem item)
        {
            GoPointerTargeter targeter = item.held != null ? item.held.GetTargeter() : null;
            if (targeter != null && TargeterActive != null)
                TargeterActive(targeter) = false;
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
            if (GameInput.GetKey(InputName.RotateH))
                WallAttachmentRoll.TakeHookTurn();
        }

        public static void Postfix(GoPointer __instance)
        {
            WallAttachmentRoll.RollHeldItem(__instance.GetHeldItem());
        }
    }
}
