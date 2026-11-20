using BepInEx.Configuration;
using UnityEngine;

namespace DeftHands.Configuration
{
    /// <summary>
    /// Holds Deft Hands' user-facing settings. <see cref="Bind"/> must run before any setting is read.
    /// </summary>
    internal static class ModConfig
    {
        private const string GeneralFeaturesSection = "General Features";
        private const string WallAttachmentFeaturesSection = "Wall Attachment Features";
        private const string ItemRotationSection = "Item Rotation";
        private const string KeysSection = "Keys";
        private const string AxisInversionSection = "Axis Inversion";
        private const string WeightSimulationSection = "Weight Simulation";

        public static ConfigEntry<bool> MouseRotationEnabled { get; private set; }
        public static ConfigEntry<bool> PushPullScrollEnabled { get; private set; }
        public static ConfigEntry<bool> LegacyRotationEnabled { get; private set; }
        public static ConfigEntry<bool> WallAttachmentRotationEnabled { get; private set; }
        public static ConfigEntry<float> RotationSensitivity { get; private set; }
        public static ConfigEntry<bool> SwitchTurnRollAxes { get; private set; }
        public static ConfigEntry<KeyboardShortcut> RotationModifierKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> AxisSwapKey { get; private set; }
        public static ConfigEntry<bool> InvertVertical { get; private set; }
        public static ConfigEntry<bool> InvertRoll { get; private set; }
        public static ConfigEntry<bool> InvertTurn { get; private set; }
        public static ConfigEntry<bool> RotationInertiaEnabled { get; private set; }
        public static ConfigEntry<RotationWeightPreset> WeightPreset { get; private set; }

        /// <summary>
        /// Binds every setting to <paramref name="config"/>, section by section in the order Configuration Manager shows
        /// the sections, and keeps Push/Pull on Scroll Wheel on only together with Mouse Rotation Control.
        /// </summary>
        public static void Bind(ConfigFile config)
        {
            BindGeneralFeatures(config);
            BindWallAttachmentFeatures(config);
            BindItemRotation(config);
            BindKeys(config);
            BindAxisInversion(config);
            BindWeightSimulation(config);
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

        private static void BindGeneralFeatures(ConfigFile config)
        {
            MouseRotationEnabled = config.Bind(
                GeneralFeaturesSection,
                "Mouse rotation control",
                true,
                new ConfigDescription(
                    "Hold the modifier key and move the mouse to rotate the held item."));

            PushPullScrollEnabled = config.Bind(
                GeneralFeaturesSection,
                "Push/pull on scroll wheel",
                true,
                new ConfigDescription(
                    "Scroll to move the held item closer or farther."));

            LegacyRotationEnabled = config.Bind(
                GeneralFeaturesSection,
                "Legacy rotation control",
                true,
                new ConfigDescription(
                    "This checkbox allows you to disable the game's original scroll-wheel rotation (plain scroll, " +
                    "plus the Q axis) and NANDTweaks' third axis, to rotate items with the mouse-drag method only."));
        }

        private static void BindWallAttachmentFeatures(ConfigFile config)
        {
            WallAttachmentRotationEnabled = config.Bind(
                WallAttachmentFeaturesSection,
                "Wall attachment rotation",
                true,
                new ConfigDescription(
                    "Hold the modifier key and move the mouse left or right to roll the item you hang; it hangs on " +
                    "the wall, ceiling or floor at that angle."));
        }

        private static void BindItemRotation(ConfigFile config)
        {
            RotationSensitivity = config.Bind(
                ItemRotationSection,
                "Rotation sensitivity",
                2.0f,
                new ConfigDescription(
                    "How fast items rotate with mouse movement.",
                    new AcceptableValueRange<float>(0.5f, 10f)));

            SwitchTurnRollAxes = config.Bind(
                ItemRotationSection,
                "Switch turn/roll axes",
                false,
                new ConfigDescription(
                    "Left/right mouse movement turns items around the vertical instead of rolling them, and the " +
                    "other way round while you hold the axis swap key."));
        }

        private static void BindKeys(ConfigFile config)
        {
            RotationModifierKey = config.Bind(
                KeysSection,
                "Rotation modifier key",
                new KeyboardShortcut(KeyCode.LeftAlt),
                new ConfigDescription(
                    "The key or mouse button you hold to rotate the held item with the mouse and in the mod's other " +
                    "key combinations."));

            AxisSwapKey = config.Bind(
                KeysSection,
                "Axis swap key",
                new KeyboardShortcut(KeyCode.Mouse1),
                new ConfigDescription(
                    "Hold it together with the modifier key to switch left/right mouse movement between rolling and " +
                    "turning items."));
        }

        private static void BindAxisInversion(ConfigFile config)
        {
            InvertVertical = config.Bind(
                AxisInversionSection,
                "Invert vertical",
                false,
                new ConfigDescription(
                    "Reverses which way up/down mouse movement tilts items."));

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
                    "Reverses which way left/right mouse movement turns the held item around the vertical, with " +
                    "\"Switch turn/roll axes\" or the axis swap key."));
        }

        private static void BindWeightSimulation(ConfigFile config)
        {
            RotationInertiaEnabled = config.Bind(
                WeightSimulationSection,
                "Rotation inertia",
                true,
                new ConfigDescription(
                    "Makes you feel the weight of the held item: heavier items take a moment to get going and drift " +
                    "to a stop when you rotate them. Turn it off for instant 1:1 response."));

            WeightPreset = config.Bind(
                WeightSimulationSection,
                "Weight preset",
                RotationWeightPreset.Standard,
                new ConfigDescription(
                    "How strongly items feel weighted while rotating. Standard is the full effect; Light is " +
                    "a much subtler version."));
        }
    }
}
