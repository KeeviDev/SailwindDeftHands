using BepInEx.Configuration;
using UnityEngine;

namespace DeftHands.Configuration
{
    /// <summary>
    /// Reads the mod's configurable keys and the settings that shape mouse movement. Only each shortcut's main key
    /// counts, whatever other keys are held with it.
    /// </summary>
    internal static class ModInput
    {
        public static bool IsRotationModifierKeyHeld()
        {
            return IsMainKeyHeld(ModConfig.RotationModifierKey);
        }

        public static bool IsAxisSwapKeyHeld()
        {
            return IsMainKeyHeld(ModConfig.AxisSwapKey);
        }

        /// <summary>
        /// Returns whether horizontal movement currently turns big items around the camera's up axis
        /// instead of rolling them: Switch Turn/Roll Axes, flipped while the axis swap key is
        /// held.
        /// </summary>
        public static bool TurnsInsteadOfRolls()
        {
            return ModConfig.SwitchTurnRollAxes.Value ^ IsAxisSwapKeyHeld();
        }

        /// <summary>
        /// Returns whether vertical movement currently leaves big items at their tilt: Lock Tilt is
        /// On Axis Swap and the axis swap key is held, or Without Axis Swap and it isn't.
        /// </summary>
        public static bool IsTiltLocked()
        {
            switch (ModConfig.LockTilt.Value)
            {
                case TiltLock.WithoutAxisSwap: return !IsAxisSwapKeyHeld();
                case TiltLock.OnAxisSwap: return IsAxisSwapKeyHeld();
                default: return false;
            }
        }

        /// <summary>
        /// Returns <paramref name="input"/>, reversed while <paramref name="invert"/> is on.
        /// </summary>
        public static float ApplyInversion(float input, ConfigEntry<bool> invert)
        {
            return invert.Value ? -input : input;
        }

        private static bool IsMainKeyHeld(ConfigEntry<KeyboardShortcut> shortcut)
        {
            return Input.GetKey(shortcut.Value.MainKey);
        }
    }
}
