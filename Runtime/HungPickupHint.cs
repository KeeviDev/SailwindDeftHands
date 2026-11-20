using DeftHands.Configuration;
using DeftHands.Utils;
using HarmonyLib;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Shows, above the game's pickup line for a wall-attachable item that hangs where it was not hung while the game
    /// runs, the line for picking it up as it hangs: the rotation modifier key's name and a copy of the pickup key's
    /// icon.
    /// </summary>
    internal static class HungPickupHint
    {
        private const string Line = "pick up as hung";

        private const string PickupLine = "pick up\n";

        /// <summary>Gap, in the look text's units, between the modifier key's name and the pickup key's icon.</summary>
        private const float IconGap = 0.015f;

        private static readonly AccessTools.FieldRef<LookUI, TextMesh> PickupKeyText =
            ReflectionUtils.FindFieldRef<LookUI, TextMesh>("textLicon");

        private static readonly AccessTools.FieldRef<LookUI, TextMesh> UseKeyText =
            ReflectionUtils.FindFieldRef<LookUI, TextMesh>("textRIcon");

        private static readonly AccessTools.FieldRef<LookUI, Renderer> PickupKeyIcon =
            ReflectionUtils.FindFieldRef<LookUI, Renderer>("mouseLIcon");

        /// <summary>The look UI the copies below belong to.</summary>
        private static LookUI copiesOwner;

        /// <summary>Whether the pickup key's icon could not be copied, so that the line is never shown.</summary>
        private static bool cannotCopy;

        /// <summary>A copy of the pickup key's name, one line above it.</summary>
        private static TextMesh keyTextCopy;

        /// <summary>A copy of the pickup key's mouse icon, one line above it.</summary>
        private static Renderer keyIconCopy;

        /// <summary>The mesh <see cref="keyIconCopy"/> draws.</summary>
        private static MeshFilter keyIconMesh;

        /// <summary>The renderer of the game's own pickup key name.</summary>
        private static Renderer keyTextRenderer;

        /// <summary>The rotation modifier key's name, left of the pickup key's mouse icon copy.</summary>
        private static TextMesh modifierText;

        /// <summary>
        /// Adds the line for picking up the item <paramref name="button"/> stands for as it hangs above the pickup line
        /// in <paramref name="controlsText"/> of <paramref name="lookUI"/>, with the keys next to it, when
        /// <paramref name="pointer"/> holds nothing, the item <see cref="HungItemPose.HangsSinceLoad"/>, the game shows
        /// its pickup line, and <see cref="WallPlacement.AdjustsHangingPose"/>.
        /// </summary>
        public static void AddToLookText(
            LookUI lookUI,
            TextMesh controlsText,
            GoPointer pointer,
            GoPointerButton button)
        {
            KeyCode modifierKey = ModConfig.RotationModifierKey.Value.MainKey;
            if (!Settings.controlsTextEnabled
                || controlsText == null
                || pointer == null
                || pointer.GetHeldItem() != null
                || modifierKey == KeyCode.None
                || !WallPlacement.AdjustsHangingPose
                || !controlsText.text.StartsWith(PickupLine, StringComparison.Ordinal)
                || !(button is ShipItem item)
                || !HungItemPose.HangsSinceLoad(item)
                || !TryMakeCopies(lookUI))
                return;

            controlsText.text = Line + "\n" + controlsText.text;
            ShowKeys(lookUI, RotationControlsHint.NameOf(modifierKey));
        }

        /// <summary>
        /// Hides the keys of the line for picking an item up as it hangs from <paramref name="lookUI"/>.
        /// </summary>
        public static void Hide(LookUI lookUI)
        {
            if (lookUI == copiesOwner)
                HideKeys();
        }

        /// <summary>
        /// Hides the keys of the line for picking an item up as it hangs.
        /// </summary>
        public static void HideKeys()
        {
            if (keyTextCopy != null)
                keyTextCopy.gameObject.SetActive(false);

            if (modifierText != null)
                modifierText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Makes the copies of the pickup key's name and icon and the modifier key's name for
        /// <paramref name="lookUI"/>, and returns whether they exist.
        /// </summary>
        private static bool TryMakeCopies(LookUI lookUI)
        {
            if (lookUI == copiesOwner && keyTextCopy != null && keyIconCopy != null && modifierText != null)
                return true;

            if (cannotCopy || PickupKeyText == null || UseKeyText == null || PickupKeyIcon == null)
                return false;

            TextMesh keyText = PickupKeyText(lookUI);
            Renderer keyIcon = PickupKeyIcon(lookUI);
            if (keyText == null || keyIcon == null || UseKeyText(lookUI) == null
                || keyIcon.transform.parent != keyText.transform)
                return false;

            Transform parent = keyText.transform.parent;
            keyTextCopy = Object.Instantiate(keyText.gameObject, parent, false).GetComponent<TextMesh>();
            keyTextCopy.name = "DeftHands hung pickup key";
            keyIconCopy = keyTextCopy.transform.GetChild(keyIcon.transform.GetSiblingIndex()).GetComponent<Renderer>();
            if (keyIconCopy == null)
            {
                Object.Destroy(keyTextCopy.gameObject);
                keyTextCopy = null;
                cannotCopy = true;
                Plugin.Log.LogWarning(
                    "Could not copy the pickup key's icon - the game may have updated. " +
                    "Hung items won't show the line for picking them up as they hang.");
                return false;
            }

            keyIconMesh = keyIconCopy.GetComponent<MeshFilter>();
            keyTextRenderer = keyText.GetComponent<Renderer>();

            modifierText = Object.Instantiate(keyText.gameObject, parent, false).GetComponent<TextMesh>();
            modifierText.name = "DeftHands hung pickup modifier";
            foreach (Transform child in modifierText.transform)
            {
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }

            copiesOwner = lookUI;
            return true;
        }

        /// <summary>
        /// Shows the pickup key as the game shows it right now, one line above it, with <paramref name="modifierName"/>
        /// and a plus left of where either of its bindings shows.
        /// </summary>
        private static void ShowKeys(LookUI lookUI, string modifierName)
        {
            TextMesh keyText = PickupKeyText(lookUI);
            Renderer keyIcon = PickupKeyIcon(lookUI);
            Transform space = keyText.transform.parent;
            float lineHeight = keyText.transform.localPosition.y - UseKeyText(lookUI).transform.localPosition.y;
            Vector3 position = keyText.transform.localPosition + Vector3.up * lineHeight;

            keyTextCopy.transform.localPosition = position;
            keyTextCopy.text = keyText.text;
            keyTextCopy.gameObject.SetActive(true);
            keyIconCopy.enabled = keyIcon.enabled;
            keyIconCopy.sharedMaterial = keyIcon.sharedMaterial;

            float leftward = GetLeftward(space);
            float iconReach = keyIconMesh != null && keyIconMesh.sharedMesh != null
                ? GetReach(keyIconMesh.sharedMesh.bounds, keyIconCopy.transform, space, position.x, leftward)
                : 0f;
            float textReach = keyText.text.Length > 0 && keyTextRenderer != null
                ? GetReach(keyTextRenderer.bounds, null, space, position.x, leftward)
                : 0f;
            float modifierX = position.x + leftward * (Mathf.Max(iconReach, textReach) + IconGap);

            modifierText.text = modifierName + " +";
            modifierText.transform.localPosition = new Vector3(modifierX, position.y, position.z);
            modifierText.gameObject.SetActive(true);
        }

        /// <summary>
        /// Returns the sign of the x axis of <paramref name="space"/> that points left on the main camera's screen.
        /// </summary>
        private static float GetLeftward(Transform space)
        {
            Camera camera = Camera.main;
            if (camera == null)
                return -1f;

            float rightOnScreen = camera.WorldToScreenPoint(space.TransformPoint(Vector3.right)).x
                - camera.WorldToScreenPoint(space.position).x;
            return rightOnScreen > 0f ? -1f : 1f;
        }

        /// <summary>
        /// Returns how far the corners of <paramref name="bounds"/> reach left of <paramref name="anchorX"/> on screen,
        /// in the x units of <paramref name="space"/>, or 0 when none does.
        /// </summary>
        /// <param name="boundsSpace">The transform <paramref name="bounds"/> is given in, or null for world
        /// space.</param>
        /// <param name="leftward">The sign of the x axis of <paramref name="space"/> that points left on
        /// screen.</param>
        private static float GetReach(
            Bounds bounds,
            Transform boundsSpace,
            Transform space,
            float anchorX,
            float leftward)
        {
            float reach = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                if (boundsSpace != null)
                    point = boundsSpace.TransformPoint(point);

                reach = Mathf.Max(reach, leftward * (space.InverseTransformPoint(point).x - anchorX));
            }

            return reach;
        }
    }
}
