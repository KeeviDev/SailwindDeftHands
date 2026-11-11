using DeftHands.Utils;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Shows compass course alignment in the game's controls text the way the game shows an item's use, as a line next
    /// to the icon of the use key: "lock to course" or "unlock course" for a held compass and "align to course" for a
    /// compass lying on deck.
    /// </summary>
    internal static class CompassCourseHint
    {
        private const string LockLine = "lock to course";

        private const string UnlockLine = "unlock course";

        private const string AlignLine = "align to course";

        private static readonly Action<LookUI> ShowUseKeyIcon = FindShowUseKeyIcon();

        private static readonly AccessTools.FieldRef<LookUI, GoPointer> Pointer =
            ReflectionUtils.FindFieldRef<LookUI, GoPointer>("pointer");

        /// <summary>
        /// Writes the course line into <paramref name="controlsText"/> of <paramref name="lookUI"/> and shows the use
        /// key's icon next to it when the held compass <paramref name="item"/> can be locked to the course, isn't being
        /// rotated and the game shows no use of its own for it.
        /// </summary>
        public static void AddToHoldText(LookUI lookUI, TextMesh controlsText, PickupableItem item)
        {
            if (!CanShowIn(controlsText)
                || controlsText.text.Length > 0
                || RotationHandler.GetInstance().IsRotating
                || !CompassAlignment.CanLockToCourse(item))
                return;

            controlsText.text = CompassAlignment.IsLockedToCourse(item) ? UnlockLine : LockLine;
            ShowUseKeyIcon(lookUI);
        }

        /// <summary>
        /// Adds the align line to <paramref name="controlsText"/> of <paramref name="lookUI"/> and shows the use key's
        /// icon next to it when the player holds nothing, <paramref name="button"/> is a compass that can be turned to
        /// the course where it lies, and the game shows no use of its own for it.
        /// </summary>
        public static void AddToLookText(LookUI lookUI, TextMesh controlsText, GoPointerButton button)
        {
            if (!CanShowIn(controlsText) || Pointer == null)
                return;

            GoPointer pointer = Pointer(lookUI);
            string text = controlsText.text;
            if (pointer == null
                || pointer.GetHeldItem() != null
                || !HasFreeUseLine(text)
                || !CompassAlignment.CanAlignLyingCompass(button))
                return;

            controlsText.text = (text.Length == 0 ? "\n" : text) + AlignLine;
            ShowUseKeyIcon(lookUI);
        }

        private static bool CanShowIn(TextMesh controlsText)
        {
            return Settings.controlsTextEnabled && controlsText != null && ShowUseKeyIcon != null;
        }

        /// <summary>
        /// Returns whether the look text <paramref name="text"/> has no line for the use key yet: it is empty or ends
        /// after the line for the pickup key.
        /// </summary>
        private static bool HasFreeUseLine(string text)
        {
            return text.Length == 0 || text.EndsWith("\n");
        }

        private static Action<LookUI> FindShowUseKeyIcon()
        {
            MethodInfo showRicon = AccessTools.Method(typeof(LookUI), "ShowRicon");
            if (showRicon != null)
                return AccessTools.MethodDelegate<Action<LookUI>>(showRicon);

            Plugin.Log.LogWarning(
                "Could not find the use key icon of the controls text - the game may have updated. " +
                "Compasses won't show their course hints.");
            return null;
        }
    }
}
