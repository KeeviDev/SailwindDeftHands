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
        public static bool IsRotationActivationKeyHeld()
        {
            return IsMainKeyHeld(ModConfig.RotationActivationKey);
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
