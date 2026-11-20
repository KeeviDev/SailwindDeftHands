using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;
using UnityEngine;

namespace DeftHands.Patches
{
    /// <summary>
    /// Cancels the game's push of a held big item away from colliders (easy cargo placement)
    /// while Preferred Push/Pull Control presses the item hard against something.
    /// </summary>
    [HarmonyPatch(typeof(PickupableItemCollisionChecker), nameof(PickupableItemCollisionChecker.GetDecollision))]
    public static class CargoPlacementOverridePatch
    {
        public static void Postfix(PickupableItemCollisionChecker __instance, ref Vector3 __result)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                if (PushPullHandler.GetInstance().UpdatePushOutOverride(__instance, __result))
                    __result = Vector3.zero;
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
            }
        }
    }
}
