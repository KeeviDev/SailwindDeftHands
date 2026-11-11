using BepInEx.Configuration;
using DeftHands.Configuration;
using DeftHands.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Shows the controls hint next to a carried big item or a small item held like one, added to the game's controls
    /// text for the held item. A big item gets the rotation modifier key, the key combination that swaps to the other
    /// axis and the scroll wheel's push and pull; while <see cref="WallPlacement.LocksRotationAxis"/>, the modifier key
    /// only rolls it. A small item gets the rotation keys on one line and the key that puts it back into the hand; a
    /// compass locked to the course shows no rotation keys. A tilted quadrant held as usual gets only the key that levels
    /// it.
    /// </summary>
    internal static class RotationControlsHint
    {
        private enum HintKind
        {
            Rotating,
            Placing,
            ReturningToHand,
            CourseLocked,
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
        /// game's controls text is off or the item is being rotated or draws nothing, and for an item held like a big
        /// item, unless mouse rotation is off.
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

            return ExtendedHold.IsHeldLikeBig(item) ? ModConfig.MouseRotationEnabled.Value : IsTiltedQuadrant(item);
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

            return CompassAlignment.IsLockedToCourse(item) ? HintKind.CourseLocked : HintKind.ReturningToHand;
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

            var lines = new List<string>();
            if (kind != HintKind.CourseLocked)
                lines.Add(BuildRotationLine(modifierKey));

            lines.Add(BuildResetLine(modifierKey, "back to hand"));
            return JoinLines(lines);
        }

        private static string BuildBigItemHint(HintKind kind, KeyCode modifierKey)
        {
            var lines = new List<string>
            {
                kind == HintKind.Placing ? NameOf(modifierKey) + " roll" : BuildRotationLines(modifierKey)
            };
            if (ModConfig.PushPullScrollEnabled.Value)
                lines.Add((ModConfig.PushPullWithModifierKey.Value ? NameOf(modifierKey) + " + Scroll" : "Scroll")
                    + " push / pull");

            return JoinLines(lines);
        }

        /// <summary>
        /// Returns one line naming the keys that rotate a small item held like a big item: the rotation modifier key, and
        /// the axis swap key in brackets when one is set.
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
            bool usesAlternativeAxis = ModConfig.UseAlternativeRotationAxis.Value;
            bool locksTiltWithoutSwap = ModConfig.LockTilt.Value == TiltLock.WithoutAxisSwap;

            string text = NameOf(modifierKey) + " " + (locksTiltWithoutSwap ? SideAxisName(usesAlternativeAxis) : "rotate");
            if (swapKey != KeyCode.None)
                text += "\n" + NameOf(modifierKey) + " + " + NameOf(swapKey) + " "
                    + (locksTiltWithoutSwap ? "rotate" : SideAxisName(!usesAlternativeAxis));

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
