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
    /// is held or placed.
    /// </summary>
    internal static class RotationControlsHint
    {
        private enum HintKind
        {
            Rotating,
            Placing,
            ReturningToHand,
            CourseLocked,
            Leveling,
            OnSurface,
            TurnedOnSurface
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

            AddAbove(controlsText, GetHint(GetKind(item)));
        }

        /// <summary>
        /// Adds the hint for the held <paramref name="item"/> placed on a surface for items above the text in
        /// <paramref name="controlsText"/>, unless the game's controls text is off, or the item is being rotated or is
        /// a compass locked to the course.
        /// </summary>
        public static void AddOnSurfaceTo(TextMesh controlsText, PickupableItem item)
        {
            if (controlsText == null
                || !Settings.controlsTextEnabled
                || !FurniturePlacement.IsPlacing(item)
                || RotationHandler.GetInstance().IsRotating
                || CompassAlignment.IsLockedToCourse(item))
                return;

            HintKind kind = FurniturePlacement.GetInstance().IsTurned ? HintKind.TurnedOnSurface : HintKind.OnSurface;
            AddAbove(controlsText, GetHint(kind));
        }

        private static void AddAbove(TextMesh controlsText, string hint)
        {
            if (hint.Length == 0)
                return;

            string text = controlsText.text;
            controlsText.text = text.Length == 0 ? hint : hint + "\n" + text;
        }

        private static bool ShowsHintFor(PickupableItem item)
        {
            if (!Settings.controlsTextEnabled
                || item == null
                || RotationHandler.GetInstance().IsRotating
                || HeldItemVisibility.IsHidden(item))
                return false;

            return ExtendedHold.IsHeldLikeBig(item) || ShowsPlacingHint(item) || IsTiltedQuadrant(item);
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> gets the hint for an item being hung: it
        /// <see cref="WallPlacement.TurnsOnWall"/>, or it is held in the hand, not like a big item, and
        /// <see cref="WallPlacement.IsBeingHung"/> with Wall Attachment Rotation or Depth on.
        /// </summary>
        private static bool ShowsPlacingHint(PickupableItem item)
        {
            return WallPlacement.TurnsOnWall(item)
                || (!ExtendedHold.IsHeldLikeBig(item)
                    && WallPlacement.IsBeingHung(item)
                    && (ModConfig.WallAttachmentRotationEnabled.Value || ModConfig.WallAttachmentDepth.Value));
        }

        private static bool IsTiltedQuadrant(PickupableItem item)
        {
            return item is ShipItemQuadrant
                && !Mathf.Approximately(item.heldRotationOffset, PickupableItemUtils.GetPrefabHeldRotationOffset(item));
        }

        private static HintKind GetKind(PickupableItem item)
        {
            if (ShowsPlacingHint(item))
                return HintKind.Placing;

            if (!ExtendedHold.IsHeldLikeBig(item))
                return HintKind.Leveling;

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

            if (kind == HintKind.Placing)
                return BuildPlacingHint(modifierKey);

            if (kind == HintKind.Rotating)
                return BuildBigItemHint(modifierKey);

            var lines = new List<string>();
            if (kind != HintKind.CourseLocked)
                lines.Add(BuildRotationLine(modifierKey));

            if (kind == HintKind.TurnedOnSurface)
                lines.Add(BuildResetLine(modifierKey, "reset"));
            else if (kind != HintKind.OnSurface)
                lines.Add(BuildResetLine(modifierKey, "back to hand"));

            return JoinLines(lines);
        }

        private static string BuildBigItemHint(KeyCode modifierKey)
        {
            var lines = new List<string>();
            if (ModConfig.MouseRotationEnabled.Value)
                lines.Add(BuildRotationLines(modifierKey));

            if (ModConfig.PushPullEnabled)
            {
                string scroll = ModInput.PushPullNeedsModifierKey ? NameOf(modifierKey) + " + Scroll" : "Scroll";
                lines.Add(scroll + " push / pull");
            }

            return JoinLines(lines);
        }

        /// <summary>
        /// Returns the lines naming the keys that roll, tilt, set the depth of and reset an item being hung.
        /// </summary>
        private static string BuildPlacingHint(KeyCode modifierKey)
        {
            var lines = new List<string>();
            if (ModConfig.WallAttachmentRotationEnabled.Value)
                lines.Add(NameOf(modifierKey) + " roll");

            KeyCode swapKey = ModConfig.AxisSwapKey.Value.MainKey;
            if (FeatureGuard.IsOn(ModConfig.EasyWallHanging) && ModConfig.WallAttachmentTilt.Value
                && swapKey != KeyCode.None)
                lines.Add(NameOf(modifierKey) + " + " + NameOf(swapKey) + " tilt");

            if (ModConfig.WallAttachmentDepth.Value)
                lines.Add(NameOf(modifierKey) + " + Scroll depth");

            lines.Add(BuildResetLine(modifierKey, "reset"));
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

        /// <summary>Returns the short name the hints give <paramref name="key"/>.</summary>
        public static string NameOf(KeyCode key)
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
