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
        private static FieldInfo rotateKeyField;
        private static FieldInfo pushKeyField;

        public static bool IsRotateKeyPressed()
        {
            EnsureInitialized();
            return IsShortcutPressed(rotateKeyField);
        }

        public static bool IsPushKeyPressed()
        {
            EnsureInitialized();
            return IsShortcutPressed(pushKeyField);
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

            rotateKeyField = pluginType.GetField("rotateItemKey", FieldFlags);
            pushKeyField = pluginType.GetField("pushItemKey", FieldFlags);
        }

        private static bool IsShortcutPressed(FieldInfo configEntryField)
        {
            if (configEntryField == null)
                return false;

            if (!(configEntryField.GetValue(null) is ConfigEntry<KeyboardShortcut> configEntry))
                return false;

            return configEntry.Value.IsPressed();
        }
    }
}
