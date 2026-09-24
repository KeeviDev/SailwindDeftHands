using DeftHands.Runtime;
using HarmonyLib;

namespace DeftHands.Patches
{
    /// <summary>
    /// Suppresses the game's alt interaction with the held item while it's being mouse-rotated,
    /// whichever key the alt interaction is bound to. A press that starts during rotation stays
    /// suppressed until it's released.
    /// </summary>
    internal static class AltInteractionBlocker
    {
        private static bool isPressSuppressed;

        /// <summary>Filters GoPointer's "alt button pressed this frame" result.</summary>
        public static bool FilterDown(bool isDown)
        {
            if (isDown)
                isPressSuppressed = RotationHandler.GetInstance().IsRotating;

            return isDown && !isPressSuppressed;
        }

        /// <summary>Filters GoPointer's "alt button held" result.</summary>
        public static bool FilterHeld(bool isHeld)
        {
            if (!isHeld)
            {
                isPressSuppressed = false;
                return false;
            }

            return !isPressSuppressed && !RotationHandler.GetInstance().IsRotating;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "AltButtonDown")]
    public static class AltButtonDownPatch
    {
        public static void Postfix(ref bool __result)
        {
            __result = AltInteractionBlocker.FilterDown(__result);
        }
    }

    [HarmonyPatch(typeof(GoPointer), "AltButtonHeld")]
    public static class AltButtonHeldPatch
    {
        public static void Postfix(ref bool __result)
        {
            __result = AltInteractionBlocker.FilterHeld(__result);
        }
    }
}
