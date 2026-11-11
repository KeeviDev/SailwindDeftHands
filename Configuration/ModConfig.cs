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
        private const string ItemCarryingSection = "Item Carrying";
        private const string ItemRotationSection = "Item Rotation";
        private const string KeysSection = "Keys";
        private const string AxisInversionSection = "Axis Inversion";
        private const string WeightSimulationSection = "Weight Simulation";

        public static ConfigEntry<bool> MouseRotationEnabled { get; private set; }
        public static ConfigEntry<PushPullMode> PreferredPushPullControl { get; private set; }
        public static ConfigEntry<bool> LegacyRotationEnabled { get; private set; }
        public static ConfigEntry<bool> ExtendedSmallItemsRotation { get; private set; }
        public static ConfigEntry<bool> NeatItemPlacement { get; private set; }
        public static ConfigEntry<bool> CompassCourseAlignment { get; private set; }
        public static ConfigEntry<bool> EasyWallHanging { get; private set; }
        public static ConfigEntry<bool> WallAttachmentRotationEnabled { get; private set; }
        public static ConfigEntry<bool> HorizontalStabilization { get; private set; }
        public static ConfigEntry<bool> HeadingStabilization { get; private set; }
        public static ConfigEntry<float> RotationSensitivity { get; private set; }
        public static ConfigEntry<bool> SwitchTurnRollAxes { get; private set; }
        public static ConfigEntry<TiltLock> LockTilt { get; private set; }
        public static ConfigEntry<bool> KeepTurningLevel { get; private set; }
        public static ConfigEntry<bool> KeepRollingLevel { get; private set; }
        public static ConfigEntry<bool> KeepTiltingLevel { get; private set; }
        public static ConfigEntry<KeyboardShortcut> RotationModifierKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> AxisSwapKey { get; private set; }
        public static ConfigEntry<KeyboardShortcut> ResetOrientationKey { get; private set; }
        public static ConfigEntry<bool> ResetOrientationWithModifierKey { get; private set; }
        public static ConfigEntry<bool> InvertVertical { get; private set; }
        public static ConfigEntry<bool> InvertRoll { get; private set; }
        public static ConfigEntry<bool> InvertTurn { get; private set; }
        public static ConfigEntry<bool> RotationInertiaEnabled { get; private set; }
        public static ConfigEntry<RotationWeightPreset> WeightPreset { get; private set; }

        /// <summary>Gets whether Preferred Push/Pull Control is set to any use of the scroll wheel.</summary>
        public static bool PushPullEnabled => PreferredPushPullControl.Value != PushPullMode.Off;

        /// <summary>
        /// Binds every setting to <paramref name="config"/>, section by section in the order Configuration Manager
        /// shows the sections, and keeps Extended Small Items Rotation off while Mouse Rotation Control is off.
        /// </summary>
        public static void Bind(ConfigFile config)
        {
            BindGeneralFeatures(config);
            BindWallAttachmentFeatures(config);
            BindItemCarrying(config);
            BindItemRotation(config);
            BindKeys(config);
            BindAxisInversion(config);
            BindWeightSimulation(config);
            TieExtendedRotationToMouseRotation();
        }

        /// <summary>
        /// Turns Extended Small Items Rotation off now and whenever Mouse Rotation Control is turned off, and turns
        /// Mouse Rotation Control on whenever Extended Small Items Rotation is turned on.
        /// </summary>
        private static void TieExtendedRotationToMouseRotation()
        {
            TurnExtendedRotationOffWithoutMouseRotation();
            MouseRotationEnabled.SettingChanged += (sender, args) => TurnExtendedRotationOffWithoutMouseRotation();
            ExtendedSmallItemsRotation.SettingChanged += (sender, args) =>
            {
                if (ExtendedSmallItemsRotation.Value)
                    MouseRotationEnabled.Value = true;
            };
        }

        private static void TurnExtendedRotationOffWithoutMouseRotation()
        {
            if (!MouseRotationEnabled.Value)
                ExtendedSmallItemsRotation.Value = false;
        }

        private static void BindGeneralFeatures(ConfigFile config)
        {
            MouseRotationEnabled = config.Bind(
                GeneralFeaturesSection,
                "Mouse rotation control",
                true,
                new ConfigDescription(
                    "Hold the modifier key and move the mouse to rotate the item in your hand."));

            PreferredPushPullControl = config.Bind(
                GeneralFeaturesSection,
                "Preferred push/pull control",
                PushPullMode.Scroll,
                new ConfigDescription(
                    "Scroll to move the held item closer or farther. \"Scroll\" uses plain scroll when that doesn't " +
                    "take rotation away from you, and otherwise may need the modifier key held; \"Scroll With " +
                    "Modifier Key Only\" always needs the modifier key."));

            LegacyRotationEnabled = config.Bind(
                GeneralFeaturesSection,
                "Legacy rotation control",
                true,
                new ConfigDescription(
                    "This checkbox allows you to disable the game's original scroll-wheel rotation (plain scroll, " +
                    "plus the Q axis) and NANDTweaks' third axis, to rotate items with the mouse-drag method only."));

            ExtendedSmallItemsRotation = config.Bind(
                GeneralFeaturesSection,
                "Extended small items rotation",
                true,
                new ConfigDescription(
                    "Turn, push or pull a small item to switch it to the extended state, where it gets full freedom " +
                    "of movement, like big items. Needs \"Mouse rotation control\" turned on."));

            NeatItemPlacement = config.Bind(
                GeneralFeaturesSection,
                "Neat item placement",
                true,
                new ConfigDescription(
                    "Turns on a reworked, more precise way to place items on tables, shelves and other furniture, " +
                    "with extended rotation."));

            CompassCourseAlignment = config.Bind(
                GeneralFeaturesSection,
                "Compass course alignment",
                true,
                new ConfigDescription(
                    "Right-click a compass you hold to keep it on the boat's course, or right-click a compass lying " +
                    "on deck to turn it to the boat's course."));
        }

        private static void BindWallAttachmentFeatures(ConfigFile config)
        {
            EasyWallHanging = config.Bind(
                WallAttachmentFeaturesSection,
                "Easy wall hanging",
                true,
                new ConfigDescription(
                    "The item in your hand moves to the lower right of your view so it doesn't cover the spot, and " +
                    "items hang exactly at the point where you look."));

            WallAttachmentRotationEnabled = config.Bind(
                WallAttachmentFeaturesSection,
                "Wall attachment rotation",
                true,
                new ConfigDescription(
                    "Hold the modifier key and move the mouse left or right to roll the item you hang; it hangs on " +
                    "the wall, ceiling or floor at that angle."));
        }

        private static void BindItemCarrying(ConfigFile config)
        {
            HorizontalStabilization = config.Bind(
                ItemCarryingSection,
                "Horizontal stabilization",
                true,
                new ConfigDescription(
                    "Items you carry keep facing the same way when you look up or down, in the world or on the deck " +
                    "of your boat."));

            HeadingStabilization = config.Bind(
                ItemCarryingSection,
                "Heading stabilization",
                false,
                new ConfigDescription(
                    "Items you carry keep facing the same way when you turn around, in the world or on the deck of " +
                    "your boat."));
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

            LockTilt = config.Bind(
                ItemRotationSection,
                "Lock tilt",
                TiltLock.OnAxisSwap,
                new ConfigDescription(
                    "Up/down mouse movement doesn't tilt items: with \"Without Axis Swap\" while you hold only the " +
                    "modifier key, with \"On Axis Swap\" while you also hold the axis swap key; with \"Off\", " +
                    "tilting works in both cases."));

            KeepTurningLevel = config.Bind(
                ItemRotationSection,
                "Keep turning level",
                true,
                new ConfigDescription(
                    "Items turn around the vertical, or the deck's vertical on a boat, so turning doesn't depend on " +
                    "where you look."));

            KeepRollingLevel = config.Bind(
                ItemRotationSection,
                "Keep rolling level",
                true,
                new ConfigDescription(
                    "Items roll around a level axis, following the deck on a boat, so rolling doesn't depend on " +
                    "where you look."));

            KeepTiltingLevel = config.Bind(
                ItemRotationSection,
                "Keep tilting level",
                true,
                new ConfigDescription(
                    "On a boat, items tilt around an axis level with the deck instead of with the horizon."));
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

            ResetOrientationKey = config.Bind(
                KeysSection,
                "Reset orientation key",
                new KeyboardShortcut(KeyCode.Mouse2),
                new ConfigDescription(
                    "Press it to reset the orientation of the held item, including while placing it on furniture or " +
                    "hanging it on a wall; a small item returns to its vanilla state in your hand."));

            ResetOrientationWithModifierKey = config.Bind(
                KeysSection,
                "Reset orientation with modifier key",
                false,
                new ConfigDescription(
                    "The reset key works only while you hold the modifier key."));
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
