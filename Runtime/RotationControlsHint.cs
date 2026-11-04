using BepInEx.Configuration;
using DeftHands.Configuration;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Shows the controls hint next to a carried big item: the rotation modifier key, the key combination that
    /// swaps to the other axis and the scroll wheel's push and pull, added to the game's controls text for the held
    /// item. While <see cref="WallPlacement.LocksRotationAxis"/>, the modifier key only rolls the item.
    /// </summary>
    internal static class RotationControlsHint
    {
        private static string hint;

        private static string placingHint;

        /// <summary>
        /// Rebuilds the hint after any setting in <paramref name="config"/> changes.
        /// </summary>
        public static void WatchSettings(ConfigFile config)
        {
            config.SettingChanged += (sender, args) =>
            {
                hint = null;
                placingHint = null;
            };
        }

        /// <summary>
        /// Adds the hint for <paramref name="item"/> after the text in <paramref name="controlsText"/>, unless the
        /// game's controls text is off, the item isn't big, mouse rotation is off or the item is being rotated.
        /// </summary>
        public static void AddTo(TextMesh controlsText, PickupableItem item)
        {
            if (controlsText == null || !ShowsHintFor(item))
                return;

            string itemHint = WallPlacement.LocksRotationAxis(item)
                ? placingHint ?? (placingHint = Build(true))
                : hint ?? (hint = Build(false));
            if (itemHint.Length == 0)
                return;

            string text = controlsText.text;
            controlsText.text = text.Length == 0 ? itemHint : text + "\n" + itemHint;
        }

        private static bool ShowsHintFor(PickupableItem item)
        {
            return Settings.controlsTextEnabled
                && item != null
                && item.big
                && ModConfig.MouseRotationEnabled.Value
                && !RotationHandler.GetInstance().IsRotating;
        }

        private static string Build(bool placing)
        {
            KeyCode modifierKey = ModConfig.RotationModifierKey.Value.MainKey;
            if (modifierKey == KeyCode.None)
                return "";

            string text = placing ? NameOf(modifierKey) + " roll" : BuildRotationLines(modifierKey);
            if (ModConfig.PushPullScrollEnabled.Value)
                text += "\n" + (ModConfig.PushPullWithModifierKey.Value ? NameOf(modifierKey) + " + Scroll" : "Scroll")
                    + " push / pull";

            return text;
        }

        private static string BuildRotationLines(KeyCode modifierKey)
        {
            KeyCode swapKey = ModConfig.AxisSwapKey.Value.MainKey;
            bool usesAlternativeAxis = ModConfig.UseAlternativeRotationAxis.Value;
            bool locksTiltWithoutSwap = ModConfig.LockTilt.Value == TiltLock.WithoutAxisSwap;

            string text = NameOf(modifierKey) + " " + (locksTiltWithoutSwap ? SideAxisName(usesAlternativeAxis) : "rotate");
            if (swapKey != KeyCode.None)
                text += "\n" + NameOf(modifierKey) + " + " + NameOf(swapKey) + " "
                    + (locksTiltWithoutSwap ? "rotate" : SideAxisName(!usesAlternativeAxis));

            return text;
        }

        private static string SideAxisName(bool usesAlternativeAxis)
        {
            return usesAlternativeAxis ? "turn" : "roll";
        }

        private static string NameOf(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt: return "Alt";
                case KeyCode.LeftControl:
                case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftShift:
                case KeyCode.RightShift: return "Shift";
                default: return key.ToString();
            }
        }
    }
}
