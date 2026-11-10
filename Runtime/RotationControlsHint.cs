using BepInEx.Configuration;
using DeftHands.Configuration;
using DeftHands.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Adds the keys that rotate, push, pull and reset the held item to the game's controls text, for the way the item
    /// is held.
    /// </summary>
    internal static class RotationControlsHint
    {
        private enum HintKind
        {
            Rotating,
            Placing,
            ReturningToHand,
            Leveling
        }

        private static readonly Dictionary<HintKind, string> Hints = new Dictionary<HintKind, string>();

        /// <summary>
        /// Rebuilds the hint after any setting in <paramref name="config"/> changes.
        /// </summary>
        public static void WatchSettings(ConfigFile config)
        {
            config.SettingChanged += (sender, args) => Hints.Clear();
        }

        /// <summary>
        /// Adds the hint for <paramref name="item"/> above the text in <paramref name="controlsText"/>, unless the
        /// game's controls text is off or the item is being rotated or draws nothing. Each line names keys whose
        /// setting is on.
        /// </summary>
        public static void AddTo(TextMesh controlsText, PickupableItem item)
        {
            if (controlsText == null || !ShowsHintFor(item))
                return;

            string itemHint = GetHint(GetKind(item));
            if (itemHint.Length == 0)
                return;

            string text = controlsText.text;
            controlsText.text = text.Length == 0 ? itemHint : itemHint + "\n" + text;
        }

        private static bool ShowsHintFor(PickupableItem item)
        {
            if (!Settings.controlsTextEnabled
                || item == null
                || RotationHandler.GetInstance().IsRotating
                || HeldItemVisibility.IsHidden(item))
                return false;

            return ExtendedHold.IsHeldLikeBig(item) || IsTiltedQuadrant(item);
        }

        private static bool IsTiltedQuadrant(PickupableItem item)
        {
            return item is ShipItemQuadrant
                && !Mathf.Approximately(item.heldRotationOffset, PickupableItemUtils.GetPrefabHeldRotationOffset(item));
        }

        private static HintKind GetKind(PickupableItem item)
        {
            if (!ExtendedHold.IsHeldLikeBig(item))
                return HintKind.Leveling;

            if (WallPlacement.LocksRotationAxis(item))
                return HintKind.Placing;

            if (item.big)
                return HintKind.Rotating;

            return HintKind.ReturningToHand;
        }

        private static string GetHint(HintKind kind)
        {
            if (!Hints.TryGetValue(kind, out string hint))
            {
                hint = Build(kind);
                Hints[kind] = hint;
            }

            return hint;
        }

        private static string Build(HintKind kind)
        {
            KeyCode modifierKey = ModConfig.RotationModifierKey.Value.MainKey;
            if (kind == HintKind.Leveling)
                return BuildResetLine(modifierKey, "level");

            if (modifierKey == KeyCode.None)
                return "";

            if (kind == HintKind.Rotating || kind == HintKind.Placing)
                return BuildBigItemHint(kind, modifierKey);

            return JoinLines(new List<string>
            {
                BuildRotationLine(modifierKey),
                BuildResetLine(modifierKey, "back to hand")
            });
        }

        private static string BuildBigItemHint(HintKind kind, KeyCode modifierKey)
        {
            var lines = new List<string>();
            if (kind == HintKind.Placing)
                lines.Add(NameOf(modifierKey) + " roll");
            else if (ModConfig.MouseRotationEnabled.Value)
                lines.Add(BuildRotationLines(modifierKey));

            if (ModConfig.PushPullEnabled)
            {
                string scroll = ModInput.PushPullNeedsModifierKey ? NameOf(modifierKey) + " + Scroll" : "Scroll";
                lines.Add(scroll + " push / pull");
            }

            return JoinLines(lines);
        }

        /// <summary>
        /// Returns one line naming the keys that rotate a small item held like a big item: the rotation modifier key,
        /// and the axis swap key in brackets when one is set.
        /// </summary>
        private static string BuildRotationLine(KeyCode modifierKey)
        {
            KeyCode swapKey = ModConfig.AxisSwapKey.Value.MainKey;
            string swap = swapKey == KeyCode.None ? "" : " (+" + NameOf(swapKey) + ")";
            return NameOf(modifierKey) + swap + " rotate";
        }

        private static string JoinLines(List<string> lines)
        {
            return string.Join("\n", lines.Where(line => line.Length > 0));
        }

        private static string BuildRotationLines(KeyCode modifierKey)
        {
            KeyCode swapKey = ModConfig.AxisSwapKey.Value.MainKey;
            bool switchesTurnAndRoll = ModConfig.SwitchTurnRollAxes.Value;
            bool locksTiltWithoutSwap = ModConfig.LockTilt.Value == TiltLock.WithoutAxisSwap;

            string action = locksTiltWithoutSwap ? SideAxisName(switchesTurnAndRoll) : "rotate";
            string text = NameOf(modifierKey) + " " + action;
            if (swapKey != KeyCode.None)
                text += "\n" + NameOf(modifierKey) + " + " + NameOf(swapKey) + " "
                    + (locksTiltWithoutSwap ? "rotate" : SideAxisName(!switchesTurnAndRoll));

            return text;
        }

        /// <summary>
        /// Returns a line naming the keys that reset the held item's orientation and <paramref name="action"/>, or an
        /// empty string when those keys can't be pressed.
        /// </summary>
        private static string BuildResetLine(KeyCode modifierKey, string action)
        {
            KeyCode resetKey = ModConfig.ResetOrientationKey.Value.MainKey;
            bool needsModifierKey = ModConfig.ResetOrientationWithModifierKey.Value;
            if (resetKey == KeyCode.None || (needsModifierKey && modifierKey == KeyCode.None))
                return "";

            return (needsModifierKey ? NameOf(modifierKey) + " + " : "") + NameOf(resetKey) + " " + action;
        }

        private static string SideAxisName(bool turns)
        {
            return turns ? "turn" : "roll";
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
