using DeftHands.Runtime;
using HarmonyLib;
using UnityEngine;

namespace DeftHands.Patches
{
    /// <summary>
    /// Cancels the game's push of a held big item away from colliders (easy cargo placement)
    /// while Push/Pull on Scroll Wheel presses the item hard against something.
    /// </summary>
    [HarmonyPatch(typeof(PickupableItemCollisionChecker), nameof(PickupableItemCollisionChecker.GetDecollision))]
    public static class CargoPlacementOverridePatch
    {
        public static void Postfix(PickupableItemCollisionChecker __instance, ref Vector3 __result)
        {
            if (PushPullHandler.GetInstance().UpdatePushOutOverride(__instance, __result))
                __result = Vector3.zero;
        }
    }
}
