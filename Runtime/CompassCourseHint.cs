using DeftHands.Utils;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Shows compass course alignment in the game's controls text the way the game shows an item's use, as a line next
    /// to the icon of the use key: "lock to course" or "unlock course" for a held compass held like a big item or
    /// placed on a surface for items, and "align to course" for a compass lying on deck.
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
        /// key's icon next to it when the held compass <paramref name="item"/> is held like a big item or placed on a
        /// surface for items, can be locked to the course, isn't being rotated and the game shows no use of its own for
        /// it.
        /// </summary>
        public static void AddToHoldText(LookUI lookUI, TextMesh controlsText, PickupableItem item)
        {
            if (!CanShowIn(controlsText)
                || controlsText.text.Length > 0
                || RotationHandler.GetInstance().IsRotating
                || !IsOutOfUsualHold(item)
                || !CompassAlignment.CanLockToCourse(item))
                return;

            controlsText.text = CompassAlignment.IsLockedToCourse(item) ? UnlockLine : LockLine;
            ShowUseKeyIcon(lookUI);
        }

        private static bool IsOutOfUsualHold(PickupableItem item)
        {
            return item != null && (ExtendedHold.IsHeldLikeBig(item) || FurniturePlacement.IsPlacing(item));
        }

        /// <summary>
        /// Adds a course line to <paramref name="controlsText"/> of <paramref name="lookUI"/> and shows the use key's
        /// icon next to it when the game shows no use of its own: the lock line while the held compass is placed on the
        /// surface looked at, can be locked to the course and isn't being rotated, or the align line when the player
        /// holds nothing and <paramref name="button"/> is a compass that can be turned to the course where it lies.
        /// </summary>
        public static void AddToLookText(LookUI lookUI, TextMesh controlsText, GoPointerButton button)
        {
            if (!CanShowIn(controlsText) || Pointer == null)
                return;

            GoPointer pointer = Pointer(lookUI);
            string text = controlsText.text;
            if (pointer == null || !HasFreeUseLine(text))
                return;

            string line = GetLookLine(pointer.GetHeldItem(), button);
            if (line == null)
                return;

            controlsText.text = (text.Length == 0 ? "\n" : text) + line;
            ShowUseKeyIcon(lookUI);
        }

        /// <summary>
        /// Returns the course line for the look text while <paramref name="heldItem"/> is held and
        /// <paramref name="button"/> is looked at, or null when there is none.
        /// </summary>
        private static string GetLookLine(PickupableItem heldItem, GoPointerButton button)
        {
            if (heldItem == null)
                return CompassAlignment.CanAlignLyingCompass(button) ? AlignLine : null;

            if (!FurniturePlacement.IsPlacing(heldItem)
                || RotationHandler.GetInstance().IsRotating
                || !CompassAlignment.CanLockToCourse(heldItem))
                return null;

            return CompassAlignment.IsLockedToCourse(heldItem) ? UnlockLine : LockLine;
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
