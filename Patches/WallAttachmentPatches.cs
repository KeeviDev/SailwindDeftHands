using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Patches
{
    /// <summary>
    /// Turns the attachment pose ShipItem.Update computes for the held item each frame by
    /// <see cref="WallAttachmentAdjustment"/>, or else <see cref="WallAttachmentRoll"/>, moves it to the depth
    /// <see cref="WallAttachmentAdjustment"/> keeps, and refreshes the placement preview to match. For a hook
    /// <see cref="WallAttachmentRoll.AimsHook"/> is true for, it works out where the hook attaches the way the game
    /// does before that.
    /// </summary>
    [HarmonyPatch(typeof(ShipItem), "Update")]
    public static class WallAttachmentPosePatch
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

        public static void Postfix(
            ShipItem __instance,
            Transform ___itemRigidbody,
            ref bool ___inRangeOfWall,
            ref Vector3 ___attachPos,
            ref Quaternion ___attachRot)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                AdjustAttachment(
                    __instance, ___itemRigidbody, ref ___inRangeOfWall, ref ___attachPos, ref ___attachRot);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
            }
        }

        private static void AdjustAttachment(
            ShipItem item, Transform body, ref bool inRangeOfWall, ref Vector3 attachPos, ref Quaternion attachRot)
        {
            bool aimsHook = WallAttachmentRoll.AimsHook(item) && body != null;
            if (aimsHook)
                FindAttachment(item, body, ref inRangeOfWall, ref attachPos, ref attachRot);

            if (!inRangeOfWall)
                return;

            Vector3 intoSurface = attachRot * Vector3.forward;
            bool isTurned = WallAttachmentAdjustment.TryOrientAttachment(item, ref attachRot)
                || WallAttachmentRoll.TryRollAttachment(item, attachPos, ref attachRot);
            bool isDeepened = WallAttachmentAdjustment.TryDeepenAttachment(item, intoSurface, ref attachPos);
            if (aimsHook || isTurned || isDeepened)
                SetUpTargeter?.Invoke(item, attachPos, attachRot, item.GetComponent<MeshFilter>().sharedMesh);
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
            if (targeter != null && GoPointerTargeterFields.IsActive != null)
                GoPointerTargeterFields.IsActive(targeter) = false;
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
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                WallAttachmentRoll.UnrollHeldItem(__instance.GetHeldItem());
                if (GameInput.GetKey(InputName.RotateH))
                    WallAttachmentRoll.TakeHookTurn();
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
            }
        }

        public static void Postfix(GoPointer __instance)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                WallAttachmentRoll.RollHeldItem(__instance.GetHeldItem());
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
            }
        }
    }
}
