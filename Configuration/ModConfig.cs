using BepInEx.Configuration;
using UnityEngine;

namespace DeftHands.Configuration
{
    /// <summary>
    /// Deft Hands' user-facing settings. <see cref="Bind"/> must run before any setting is read.
    /// </summary>
    internal static class ModConfig
    {
        private const string HeldItemControlSection = "Held Item Control";
        private const string AxisInversionSection = "Axis Inversion";
        private const string WeightSimulationSection = "Weight Simulation";

        public static ConfigEntry<bool> MouseRotationEnabled { get; private set; }
        public static ConfigEntry<bool> PushPullScrollEnabled { get; private set; }
        public static ConfigEntry<bool> LegacyRotationEnabled { get; private set; }
        public static ConfigEntry<KeyboardShortcut> RotationActivationKey { get; private set; }
        public static ConfigEntry<float> RotationSensitivity { get; private set; }
        public static ConfigEntry<bool> UseAlternativeRotationAxis { get; private set; }
        public static ConfigEntry<KeyboardShortcut> AxisSwapKey { get; private set; }
        public static ConfigEntry<bool> InvertVertical { get; private set; }
        public static ConfigEntry<bool> InvertRoll { get; private set; }
        public static ConfigEntry<bool> InvertTurn { get; private set; }
        public static ConfigEntry<bool> RotationInertiaEnabled { get; private set; }
        public static ConfigEntry<RotationWeightPreset> WeightPreset { get; private set; }

        public static void Bind(ConfigFile config)
        {
            MouseRotationEnabled = config.Bind(
                HeldItemControlSection,
                "Mouse rotation control",
                true,
                new ConfigDescription(
                    "Rotate held items with mouse movement while holding the Rotation Activation Key below."));

            PushPullScrollEnabled = config.Bind(
                HeldItemControlSection,
                "Push/pull on scroll wheel",
                true,
                new ConfigDescription(
                    "Scroll the mouse wheel (without holding a modifier key) to move the held item closer or " +
                    "farther, instead of rotating it around its main axis."));

            LegacyRotationEnabled = config.Bind(
                HeldItemControlSection,
                "Legacy rotation control",
                true,
                new ConfigDescription(
                    "Enable the game's original scroll-wheel rotation (plain scroll, plus the Q axis), and " +
                    "NANDTweaks' third axis if you have that mod installed. Turn this off to rotate items " +
                    "with the mouse-drag method only."));

            RotationActivationKey = config.Bind(
                HeldItemControlSection,
                "Rotation activation key",
                new KeyboardShortcut(KeyCode.LeftAlt),
                new ConfigDescription(
                    "The key or mouse button you hold down while moving the mouse to rotate the held item."));

            RotationSensitivity = config.Bind(
                HeldItemControlSection,
                "Rotation sensitivity",
                2.0f,
                new ConfigDescription(
                    "How fast items rotate with mouse movement.",
                    new AcceptableValueRange<float>(0.5f, 10f)));

            UseAlternativeRotationAxis = config.Bind(
                HeldItemControlSection,
                "Alternative rotation axis",
                false,
                new ConfigDescription(
                    "Changes which axis left/right mouse movement rotates the item around. By default, it " +
                    "rolls the item around the direction you're looking. Turn this on to instead yaw it " +
                    "around the vertical axis."));

            AxisSwapKey = config.Bind(
                HeldItemControlSection,
                "Axis swap key",
                new KeyboardShortcut(KeyCode.Mouse1),
                new ConfigDescription(
                    "While held together with the Rotation Activation Key, temporarily swaps which axis " +
                    "left/right mouse movement rotates big items around, overriding Alternative Rotation " +
                    "Axis for as long as it's held."));

            InvertVertical = config.Bind(
                AxisInversionSection,
                "Invert vertical",
                false,
                new ConfigDescription(
                    "Reverses which way up/down mouse movement tilts the held item."));

            InvertRoll = config.Bind(
                AxisInversionSection,
                "Invert roll",
                false,
                new ConfigDescription(
                    "Reverses which way left/right mouse movement rolls items."));

            InvertTurn = config.Bind(
                AxisInversionSection,
                "Invert turn",
                false,
                new ConfigDescription(
                    "Reverses which way left/right mouse movement turns items around the vertical axis " +
                    "(Alternative Rotation Axis, or while holding the Axis Swap Key)."));

            RotationInertiaEnabled = config.Bind(
                WeightSimulationSection,
                "Rotation inertia",
                true,
                new ConfigDescription(
                    "Makes held items feel like they have weight when you rotate them: heavier items take a " +
                    "moment to speed up, and keep drifting briefly after you stop moving the mouse. Turn off " +
                    "for instant, 1:1 mouse response."));

            WeightPreset = config.Bind(
                WeightSimulationSection,
                "Weight preset",
                RotationWeightPreset.Standard,
                new ConfigDescription(
                    "How strongly items feel weighted while rotating. Standard is the full effect; Light is " +
                    "a much subtler version."));

            KeepPushPullDependentOnMouseRotation();
        }

        /// <summary>
        /// Keeps Push/Pull on Scroll Wheel enabled only together with mouse rotation control:
        /// turning mouse rotation off turns push/pull off, and turning push/pull on turns mouse
        /// rotation back on.
        /// </summary>
        private static void KeepPushPullDependentOnMouseRotation()
        {
            DisablePushPullWithoutMouseRotation();

            MouseRotationEnabled.SettingChanged += (sender, args) => DisablePushPullWithoutMouseRotation();

            PushPullScrollEnabled.SettingChanged += (sender, args) =>
            {
                if (PushPullScrollEnabled.Value && !MouseRotationEnabled.Value)
                    MouseRotationEnabled.Value = true;
            };
        }

        private static void DisablePushPullWithoutMouseRotation()
        {
            if (!MouseRotationEnabled.Value && PushPullScrollEnabled.Value)
                PushPullScrollEnabled.Value = false;
        }
    }
}
