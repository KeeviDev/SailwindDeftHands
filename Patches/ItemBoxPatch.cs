using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;

namespace DeftHands.Patches
{
    /// <summary>
    /// Records every item put in another item's box for <see cref="ItemBoxRiders"/>.
    /// </summary>
    [HarmonyPatch(typeof(ItemRigidbody), nameof(ItemRigidbody.EnterBox))]
    public static class ItemBoxPatch
    {
        public static void Postfix(ItemRigidbody __instance)
        {
            if (!FeatureGuard.IsRunning(FeatureGuard.Core))
                return;

            try
            {
                ItemBoxRiders.Add(__instance);
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(FeatureGuard.Core, error);
            }
        }
    }
}
