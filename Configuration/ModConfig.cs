using BepInEx.Configuration;
using UnityEngine;

namespace DeftHands.Configuration
{
    /// <summary>
    /// Holds Deft Hands' user-facing settings. <see cref="Bind"/> must run before any setting is read.
    /// </summary>
    internal static class ModConfig
    {
        private const string HeldItemControlSection = "Held Item Control";
        private const string HeldItemOrientationSection = "Held Item Orientation";
        private const string AxisInversionSection = "Axis Inversion";
        private const string WeightSimulationSection = "Weight Simulation";

        public static ConfigEntry<bool> MouseRotationEnabled { get; private set; }
        public static ConfigEntry<bool> PushPullScrollEnabled { get; private set; }
        public static ConfigEntry<bool> PushPullWithModifierKey { get; private set; }
        public static ConfigEntry<bool> LegacyRotationEnabled { get; private set; }
        public static ConfigEntry<KeyboardShortcut> RotationModifierKey { get; private set; }
        public static ConfigEntry<float> RotationSensitivity { get; private set; }
        public static ConfigEntry<bool> UseAlternativeRotationAxis { get; private set; }
        public static ConfigEntry<KeyboardShortcut> AxisSwapKey { get; private set; }
        public static ConfigEntry<TiltLock> LockTilt { get; private set; }
        public static ConfigEntry<KeyboardShortcut> ResetOrientationKey { get; private set; }
        public static ConfigEntry<bool> ResetOrientationWithModifierKey { get; private set; }
        public static ConfigEntry<bool> KeepTurningLevel { get; private set; }
        public static ConfigEntry<bool> KeepRollingLevel { get; private set; }
        public static ConfigEntry<bool> KeepTiltingLevel { get; private set; }
        public static ConfigEntry<bool> KeepFacingWhenLookingUpAndDown { get; private set; }
        public static ConfigEntry<bool> KeepFacingWhenTurningAround { get; private set; }
        public static ConfigEntry<bool> WallAttachmentRotationEnabled { get; private set; }
        public static ConfigEntry<bool> EasyWallHanging { get; private set; }
        public static ConfigEntry<bool> ExtendedSmallItemsRotation { get; private set; }
        public static ConfigEntry<bool> CompassCourseAlignment { get; private set; }
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
                    "Rotate held items with mouse movement while holding the Rotation Modifier Key below."));

            PushPullScrollEnabled = config.Bind(
                HeldItemControlSection,
                "Push/pull on scroll wheel",
                true,
                new ConfigDescription(
                    "Scroll the mouse wheel (without holding a modifier key) to move the held item closer or " +
                    "farther, instead of rotating it around its main axis."));

            PushPullWithModifierKey = config.Bind(
                HeldItemControlSection,
                "Push/pull with modifier key",
                false,
                new ConfigDescription(
                    "Scroll pushes and pulls the held item only while you hold the modifier key, so " +
                    "plain scroll keeps the game's scroll rotation. Needs \"Push/pull on scroll wheel\" turned on."));

            LegacyRotationEnabled = config.Bind(
                HeldItemControlSection,
                "Legacy rotation control",
                true,
                new ConfigDescription(
                    "Enable the game's original scroll-wheel rotation (plain scroll, plus the Q axis), and " +
                    "NANDTweaks' third axis if you have that mod installed. Turn this off to rotate items " +
                    "with the mouse-drag method only."));

            RotationModifierKey = config.Bind(
                HeldItemControlSection,
                "Rotation modifier key",
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
                    "Changes which axis left/right mouse movement rotates crates, barrels and other big items " +
                    "around. By default, it rolls them around the direction you're looking. Turn this on to " +
                    "instead yaw them around the vertical axis."));

            AxisSwapKey = config.Bind(
                HeldItemControlSection,
                "Axis swap key",
                new KeyboardShortcut(KeyCode.Mouse1),
                new ConfigDescription(
                    "While held together with the modifier key, temporarily swaps which axis " +
                    "left/right mouse movement rotates big items around, overriding Alternative Rotation " +
                    "Axis for as long as it's held."));

            LockTilt = config.Bind(
                HeldItemControlSection,
                "Lock tilt",
                TiltLock.OnAxisSwap,
                new ConfigDescription(
                    "Up/down mouse movement doesn't tilt big items. Without Axis Swap: while you hold only the " +
                    "modifier key. On Axis Swap: while you also hold the Axis Swap Key."));

            ResetOrientationKey = config.Bind(
                HeldItemControlSection,
                "Reset orientation key",
                new KeyboardShortcut(KeyCode.Mouse2),
                new ConfigDescription(
                    "Press it to reset the orientation of the held item: big items stand upright, facing the way " +
                    "you look, and small items go back into your hand."));

            ResetOrientationWithModifierKey = config.Bind(
                HeldItemControlSection,
                "Reset orientation with modifier key",
                false,
                new ConfigDescription(
                    "The Reset Orientation Key works only while you hold the modifier key."));

            KeepTurningLevel = config.Bind(
                HeldItemOrientationSection,
                "Keep turning level",
                true,
                new ConfigDescription(
                    "Crates, barrels and other big items turn around the vertical, so they stay level when you " +
                    "look up or down at them. On a boat they turn around the deck's vertical."));

            KeepRollingLevel = config.Bind(
                HeldItemOrientationSection,
                "Keep rolling level",
                true,
                new ConfigDescription(
                    "Crates, barrels and other big items roll around a level axis, even when you look up or down " +
                    "at them. On a boat the axis follows the deck."));

            KeepTiltingLevel = config.Bind(
                HeldItemOrientationSection,
                "Keep tilting level",
                true,
                new ConfigDescription(
                    "On a boat, crates, barrels and other big items tilt around an axis level with the deck " +
                    "instead of with the horizon."));

            KeepFacingWhenLookingUpAndDown = config.Bind(
                HeldItemOrientationSection,
                "Keep facing when looking up and down",
                true,
                new ConfigDescription(
                    "Crates, barrels and other big items you carry keep facing the same way when you look up " +
                    "or down, instead of tilting with your view. On a boat they keep facing the same way on deck."));

            KeepFacingWhenTurningAround = config.Bind(
                HeldItemOrientationSection,
                "Keep facing when turning around",
                false,
                new ConfigDescription(
                    "Crates, barrels and other big items you carry keep facing the same way when you turn " +
                    "around, instead of turning with you. On a boat they keep facing the same way on deck."));

            WallAttachmentRotationEnabled = config.Bind(
                HeldItemControlSection,
                "Wall attachment rotation",
                true,
                new ConfigDescription(
                    "Lets you control the angle items are hung on walls, ceilings and floors: " +
                    "roll a wall-attachable item with the mouse and it attaches at that angle."));

            EasyWallHanging = config.Bind(
                HeldItemControlSection,
                "Easy wall hanging",
                true,
                new ConfigDescription(
                    "Makes hanging items easier: they hang where you look, and the item in your hand moves to the " +
                    "lower right of your view so it doesn't cover the spot."));

            ExtendedSmallItemsRotation = config.Bind(
                HeldItemControlSection,
                "Extended small items rotation",
                true,
                new ConfigDescription(
                    "Turn, push or pull a small item and you hold it like a big item."));

            CompassCourseAlignment = config.Bind(
                HeldItemControlSection,
                "Compass course alignment",
                true,
                new ConfigDescription(
                    "On a boat, the right mouse button points a compass along the course: a compass lying on deck turns " +
                    "in place, and a compass you hold with extended rotation stays on course until you press it again."));

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
