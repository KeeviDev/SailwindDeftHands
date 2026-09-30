using DeftHands.Compat;
using DeftHands.Configuration;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Lets the player roll a held wall-attachable item around the axis pointing into the wall,
    /// so it attaches at that angle instead of always upright. Roll input passes through a dead
    /// zone at upright: the item stays upright while the input crosses it, then keeps turning.
    /// Small items keep their roll here, on top of how the game holds them; big items rotate
    /// freely, so their roll is read from their actual orientation. Items HooksHangMore manages
    /// keep their roll in HooksHangMore instead, within its own range.
    /// </summary>
    internal static class WallAttachmentRoll
    {
        /// <summary>Degrees of roll input the item stays upright for while turning through upright.</summary>
        private const float UprightDeadzoneDegrees = 18f;

        /// <summary>
        /// Items facing more steeply up or down than this face a ceiling, floor or tabletop rather
        /// than a wall, where there's no upright to measure roll against.
        /// </summary>
        private const float MaxWallFacingY = 0.8f;

        /// <summary>Roll, in degrees, within which an item counts as upright.</summary>
        private const float UprightTolerance = 0.01f;

        private const float HalfDeadzone = UprightDeadzoneDegrees / 2f;
        private const float InputPeriod = 360f + UprightDeadzoneDegrees;

        private static ShipItem currentItem;

        /// <summary>
        /// A small item's accumulated roll input, wrapped to one full turn centred on upright. One
        /// full turn spans <see cref="InputPeriod"/> degrees, the extra ones being the dead zone.
        /// </summary>
        private static float rollInput;

        /// <summary>Whether a big item is being held upright by the dead zone.</summary>
        private static bool isBigItemUpright;

        /// <summary>A big item's roll input within the dead zone while it's held upright.</summary>
        private static float uprightInput;

        /// <summary>
        /// The roll <see cref="RollHeldItem"/> last added to the small held item's rotation. An item
        /// taken off a surface counts as already carrying its roll, so it eases into the hand from
        /// exactly where it hung.
        /// </summary>
        private static float heldItemRoll;

        /// <summary>The component HooksHangMore keeps the held item's roll on, or null if it doesn't manage it.</summary>
        private static Component hookSwingOwner;

        /// <summary>
        /// The roll last written to HooksHangMore, used to detect when its own controls changed it.
        /// </summary>
        private static float lastWrittenSwing;

        /// <summary>
        /// Whether the held item takes roll input: it's wall-attachable and Wall Attachment
        /// Rotation is on, or HooksHangMore manages its roll.
        /// </summary>
        public static bool IsActive =>
            currentItem != null && (hookSwingOwner != null || ModConfig.WallAttachmentRotationEnabled.Value);

        /// <summary>
        /// Starts tracking the held item if it's wall-attachable. A small item taken off a surface
        /// starts at the roll that would attach it back the same way, other small items start
        /// upright; a big item keeps its own orientation. The previously tracked item gets back
        /// any roll taken off it earlier in the frame, so a dropped item doesn't show a frame
        /// without it.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        /// <param name="wasAttached">Whether the item was attached to a surface when picked up.</param>
        public static void SetCurrentItem(PickupableItem item, bool wasAttached)
        {
            if (currentItem != null)
                RollHeldItem(currentItem);

            currentItem = item is ShipItem shipItem && shipItem.wallAttachment ? shipItem : null;
            rollInput = 0f;
            uprightInput = 0f;
            isBigItemUpright = false;
            hookSwingOwner = null;
            heldItemRoll = 0f;

            if (currentItem == null)
                return;

            if (currentItem.big)
            {
                Vector3 up = AttachmentSpace.UpInWorld(currentItem);
                isBigItemUpright = FacesWall(currentItem.transform.forward, up) && IsUpright(MeasureRoll(currentItem.transform.rotation, up));
                return;
            }

            hookSwingOwner = HooksHangMoreCompat.GetSwingOwner(currentItem);
            if (hookSwingOwner != null)
            {
                lastWrittenSwing = HooksHangMoreCompat.GetSwing(hookSwingOwner);
                rollInput = RollToInput(lastWrittenSwing);
            }
            else if (wasAttached)
            {
                rollInput = RollToInput(ReadAttachedRoll(currentItem));
                heldItemRoll = InputToRoll(rollInput);
            }
        }

        /// <summary>
        /// Adds roll input for a small item: a full turn either way, or HooksHangMore's own range
        /// for items it manages.
        /// </summary>
        /// <param name="degrees">Roll input to add; positive turns counterclockwise as seen facing the wall.</param>
        public static void AddRollInput(float degrees)
        {
            if (hookSwingOwner != null)
                AddHookSwingInput(degrees);
            else
                rollInput = Mathf.Repeat(rollInput + degrees + InputPeriod / 2f, InputPeriod) - InputPeriod / 2f;
        }

        /// <summary>
        /// Drops roll input left inside the dead zone, so the next rotation starts from its centre.
        /// Input beyond the dead zone is kept.
        /// </summary>
        public static void RecenterDeadzone()
        {
            if (Mathf.Abs(rollInput) <= HalfDeadzone)
                rollInput = 0f;

            uprightInput = 0f;
        }

        /// <summary>
        /// Rotates a big item by <paramref name="degrees"/> around <paramref name="axis"/>, holding
        /// it upright while the rotation crosses the dead zone.
        /// </summary>
        public static void RollBigItem(Transform item, Vector3 axis, float degrees)
        {
            Vector3 up = AttachmentSpace.UpInWorld(currentItem);
            if (!FacesWall(item.forward, up))
            {
                isBigItemUpright = false;
                item.Rotate(axis, degrees, Space.World);
                return;
            }

            if (isBigItemUpright)
            {
                float input = uprightInput + degrees;
                if (Mathf.Abs(input) <= HalfDeadzone)
                {
                    uprightInput = input;
                    return;
                }

                isBigItemUpright = false;
                item.Rotate(axis, input - Mathf.Sign(input) * HalfDeadzone, Space.World);
                return;
            }

            float rollBefore = MeasureRoll(item.rotation, up);
            item.Rotate(axis, degrees, Space.World);
            float rollAfter = MeasureRoll(item.rotation, up);
            if (CrossedUpright(rollBefore, rollAfter))
                EnterDeadzone(item, up, rollBefore, Mathf.Abs(rollAfter));
        }

        /// <summary>
        /// Undoes any roll a big item picked up from other rotations while the dead zone holds it
        /// upright. Must be called after all of the frame's rotations.
        /// </summary>
        public static void HoldBigItemUpright(Transform item)
        {
            if (!isBigItemUpright)
                return;

            Vector3 up = AttachmentSpace.UpInWorld(currentItem);
            if (FacesWall(item.forward, up))
                SetRoll(item, up, 0f);
            else
                isBigItemUpright = false;
        }

        /// <summary>
        /// Applies the held item's roll to <paramref name="attachRot"/>, the rotation the game
        /// computed for attaching <paramref name="item"/> at <paramref name="attachPos"/>, both in
        /// <see cref="AttachmentSpace"/>. On a ceiling or floor the item instead attaches turned
        /// the way it looks in the player's hand.
        /// </summary>
        /// <returns>True if <paramref name="attachRot"/> was changed.</returns>
        public static bool TryRollAttachment(ShipItem item, Vector3 attachPos, ref Quaternion attachRot)
        {
            if (!IsActive || item != currentItem || hookSwingOwner != null)
                return false;

            Quaternion heldRotation = AttachmentSpace.ToAttachmentSpace(item, item.transform.rotation);
            Vector3 intoSurface = attachRot * Vector3.forward;
            if (!FacesWall(intoSurface, Vector3.up))
            {
                Camera camera = Camera.main;
                if (camera == null)
                    return false;

                Vector3 cameraPosition = AttachmentSpace.ToAttachmentSpace(item, camera.transform.position);
                attachRot = FaceSurfaceAsSeen(heldRotation, intoSurface, attachPos - cameraPosition);
                return true;
            }

            float roll = item.big ? MeasureRoll(heldRotation, Vector3.up) : InputToRoll(rollInput);
            if (roll == 0f)
                return false;

            attachRot *= Quaternion.AngleAxis(roll, Vector3.forward);
            return true;
        }

        /// <summary>
        /// Takes the roll <see cref="RollHeldItem"/> added last frame back off the held item, so
        /// the holding GoPointer eases it into the hand from its unrolled pose instead of
        /// compounding the roll during the pickup animation. Must be called before the GoPointer
        /// positions it for the frame.
        /// </summary>
        public static void UnrollHeldItem(PickupableItem item)
        {
            if (currentItem == null || item != currentItem || heldItemRoll == 0f)
                return;

            item.transform.rotation *= Quaternion.AngleAxis(-heldItemRoll, Vector3.forward);
            heldItemRoll = 0f;
        }

        /// <summary>
        /// Rolls a small <paramref name="item"/> in the player's hand to match its attachment roll,
        /// adding only what it doesn't already carry. Must be called after the holding GoPointer
        /// has positioned it for the frame.
        /// </summary>
        public static void RollHeldItem(PickupableItem item)
        {
            if (currentItem == null || item != currentItem)
                return;

            bool rollsInHand = IsActive && !currentItem.big && hookSwingOwner == null;
            float roll = rollsInHand ? InputToRoll(rollInput) : 0f;
            if (roll != heldItemRoll)
                item.transform.rotation *= Quaternion.AngleAxis(roll - heldItemRoll, Vector3.forward);

            heldItemRoll = roll;
        }

        /// <summary>
        /// Adds roll input for an item HooksHangMore manages, clamped so the roll stays within
        /// its range. Picks up any change HooksHangMore's own controls made in the meantime.
        /// </summary>
        private static void AddHookSwingInput(float degrees)
        {
            float swing = HooksHangMoreCompat.GetSwing(hookSwingOwner);
            if (swing != lastWrittenSwing)
                rollInput = RollToInput(swing);

            float maxInput = HooksHangMoreCompat.MaxSwing + HalfDeadzone;
            rollInput = Mathf.Clamp(rollInput + degrees, -maxInput, maxInput);
            lastWrittenSwing = InputToRoll(rollInput);
            HooksHangMoreCompat.SetSwing(hookSwingOwner, lastWrittenSwing);
        }

        /// <summary>
        /// Handles a big item's roll crossing upright: holds it there if the roll that went past
        /// upright is still within the dead zone, otherwise sets it to the roll left beyond it.
        /// </summary>
        /// <param name="rollBefore">The roll before crossing, which gives the side it came from.</param>
        /// <param name="overshoot">How far the roll went past upright.</param>
        /// <param name="up">Visible-world direction that's up in attachment space.</param>
        private static void EnterDeadzone(Transform item, Vector3 up, float rollBefore, float overshoot)
        {
            float input = Mathf.Sign(rollBefore) * (HalfDeadzone - overshoot);
            if (Mathf.Abs(input) <= HalfDeadzone)
            {
                SetRoll(item, up, 0f);
                isBigItemUpright = true;
                uprightInput = input;
            }
            else
            {
                SetRoll(item, up, -Mathf.Sign(rollBefore) * (overshoot - UprightDeadzoneDegrees));
            }
        }

        private static bool CrossedUpright(float rollBefore, float rollAfter)
        {
            bool nearUpright = Mathf.Abs(rollBefore) < 90f && Mathf.Abs(rollAfter) < 90f;
            return nearUpright && (IsUpright(rollAfter) || Mathf.Sign(rollBefore) != Mathf.Sign(rollAfter));
        }

        /// <summary>
        /// Returns the roll a small item taken off a surface should be held at to attach back the
        /// way it was. On a ceiling or floor that's relative to the current view. Works in
        /// attachment space, where the item's physics body sits.
        /// </summary>
        private static float ReadAttachedRoll(ShipItem item)
        {
            Transform body = item.GetItemRigidbody().transform;
            if (FacesWall(body.forward, Vector3.up))
                return MeasureRoll(body.rotation, Vector3.up);

            Camera camera = Camera.main;
            if (camera == null)
                return 0f;

            Quaternion heldInWorld = item.held.transform.rotation * Quaternion.Euler(item.heldRotationOffset, 0f, 0f);
            Quaternion heldRotation = AttachmentSpace.ToAttachmentSpace(item, heldInWorld);
            Vector3 heldForward = heldRotation * Vector3.forward;
            Vector3 viewDirection = body.position - AttachmentSpace.ToAttachmentSpace(item, camera.transform.position);
            Vector3 heldUp = SeenFromSurface(body.up, viewDirection, heldForward);
            return heldUp == Vector3.zero ? 0f : Vector3.SignedAngle(heldRotation * Vector3.up, heldUp, heldForward);
        }

        /// <summary>
        /// Returns <paramref name="heldRotation"/> turned to face <paramref name="intoSurface"/>,
        /// with its up direction chosen to lie along the same line on screen as it does in hand.
        /// </summary>
        /// <param name="viewDirection">Direction from the camera to where the item attaches.</param>
        private static Quaternion FaceSurfaceAsSeen(Quaternion heldRotation, Vector3 intoSurface, Vector3 viewDirection)
        {
            Vector3 up = SeenFromSurface(heldRotation * Vector3.up, viewDirection, intoSurface);
            if (up == Vector3.zero)
                return Quaternion.FromToRotation(heldRotation * Vector3.forward, intoSurface) * heldRotation;

            return Quaternion.LookRotation(intoSurface, up);
        }

        /// <summary>
        /// Returns the direction perpendicular to <paramref name="normal"/> that lies along the same
        /// line on screen as <paramref name="direction"/>, pointing the same way, or
        /// <see cref="Vector3.zero"/> if there's no such direction.
        /// </summary>
        /// <param name="viewDirection">Direction from the camera to the point both directions start from.</param>
        private static Vector3 SeenFromSurface(Vector3 direction, Vector3 viewDirection, Vector3 normal)
        {
            Vector3 screenLinePlaneNormal = Vector3.Cross(viewDirection, direction);
            Vector3 result = Vector3.Cross(normal, screenLinePlaneNormal);
            if (result.sqrMagnitude < 1e-8f)
                return Vector3.zero;

            return Vector3.Dot(result, direction) < 0f ? -result.normalized : result.normalized;
        }

        /// <summary>
        /// Returns how far <paramref name="rotation"/> is rolled around its forward axis from the
        /// upright orientation the game would attach it at, or 0 if it doesn't face a wall.
        /// </summary>
        /// <param name="up">The up direction upright is measured against, in the rotation's space.</param>
        private static float MeasureRoll(Quaternion rotation, Vector3 up)
        {
            Vector3 intoWall = rotation * Vector3.forward;
            if (!FacesWall(intoWall, up))
                return 0f;

            Vector3 uprightUp = Quaternion.LookRotation(intoWall, up) * Vector3.up;
            return Vector3.SignedAngle(uprightUp, rotation * Vector3.up, intoWall);
        }

        private static void SetRoll(Transform item, Vector3 up, float roll)
        {
            item.Rotate(item.forward, roll - MeasureRoll(item.rotation, up), Space.World);
        }

        private static bool FacesWall(Vector3 forward, Vector3 up)
        {
            return Mathf.Abs(Vector3.Dot(forward, up)) <= MaxWallFacingY;
        }

        private static bool IsUpright(float roll)
        {
            return Mathf.Abs(roll) < UprightTolerance;
        }

        private static float InputToRoll(float input)
        {
            return Mathf.Abs(input) <= HalfDeadzone ? 0f : input - Mathf.Sign(input) * HalfDeadzone;
        }

        private static float RollToInput(float roll)
        {
            return IsUpright(roll) ? 0f : roll + Mathf.Sign(roll) * HalfDeadzone;
        }
    }
}
