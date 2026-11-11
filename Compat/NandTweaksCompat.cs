using BepInEx.Configuration;
using DeftHands.Utils;
using System;
using System.Reflection;

namespace DeftHands.Compat
{
    /// <summary>
    /// Reads whether NANDTweaks' "rotate held item" and "push/pull held item" keybinds are
    /// pressed, as the player has bound them. Every key reads as not pressed when NANDTweaks
    /// isn't installed.
    /// </summary>
    internal static class NandTweaksCompat
    {
        private const string PluginTypeName = "NANDTweaks.Plugin";
        private const BindingFlags FieldFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool initialized;
        private static ConfigEntry<KeyboardShortcut> rotateKey;
        private static ConfigEntry<KeyboardShortcut> pushKey;

        public static bool IsRotateKeyPressed()
        {
            EnsureInitialized();
            return IsShortcutPressed(rotateKey);
        }

        public static bool IsPushKeyPressed()
        {
            EnsureInitialized();
            return IsShortcutPressed(pushKey);
        }

        public static bool IsAnyKeyPressed()
        {
            return IsRotateKeyPressed() || IsPushKeyPressed();
        }

        /// <summary>
        /// Looks NANDTweaks up on the first call only, keeping the result whether it was found or
        /// not. Must only be called after every plugin has loaded.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;

            Type pluginType = ReflectionUtils.FindType(PluginTypeName);
            if (pluginType == null)
                return;

            rotateKey = pluginType.GetField("rotateItemKey", FieldFlags)?.GetValue(null) as ConfigEntry<KeyboardShortcut>;
            pushKey = pluginType.GetField("pushItemKey", FieldFlags)?.GetValue(null) as ConfigEntry<KeyboardShortcut>;
        }

        private static bool IsShortcutPressed(ConfigEntry<KeyboardShortcut> shortcut)
        {
            return shortcut != null && shortcut.Value.IsPressed();
        }
    }
}
