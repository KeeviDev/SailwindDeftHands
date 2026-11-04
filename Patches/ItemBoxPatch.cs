using DeftHands.Runtime;
using HarmonyLib;

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
            ItemBoxRiders.Add(__instance);
        }
    }
}
