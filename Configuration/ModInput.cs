using BepInEx.Configuration;
using UnityEngine;

namespace DeftHands.Configuration
{
    /// <summary>
    /// Reads the state of the mod's configurable keys. Unlike KeyboardShortcut.IsPressed, a key
    /// counts as held even while other keys are held too.
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
