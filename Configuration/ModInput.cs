using BepInEx.Configuration;
using UnityEngine;

namespace DeftHands.Configuration
{
    /// <summary>
    /// Reads whether the mod's configurable keys are held. Only each shortcut's main key counts,
    /// whatever other keys are held with it.
    /// </summary>
    internal static class ModInput
    {
        public static bool IsRotationModifierKeyHeld()
        {
            return IsMainKeyHeld(ModConfig.RotationModifierKey);
        }

        /// <summary>
        /// Returns whether the scroll wheel pushes and pulls the held item right now: Push/Pull on Scroll Wheel is
        /// on, and the Rotation Modifier Key is held when Push/Pull with Modifier Key is on.
        /// </summary>
        public static bool IsPushPullScrollActive()
        {
            return ModConfig.PushPullScrollEnabled.Value
                && (!ModConfig.PushPullWithModifierKey.Value || IsRotationModifierKeyHeld());
        }

        public static bool IsAxisSwapKeyHeld()
        {
            return IsMainKeyHeld(ModConfig.AxisSwapKey);
        }

        private static bool IsMainKeyHeld(ConfigEntry<KeyboardShortcut> shortcut)
        {
            return Input.GetKey(shortcut.Value.MainKey);
        }
    }
}
