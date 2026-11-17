using DeftHands.Compat;
using DeftHands.Configuration;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Lets the player roll a held wall-attachable item around the axis pointing into the wall,
    /// so it attaches at that angle. Roll input passes through a dead zone at upright: the item
    /// stays upright while the input crosses it, then keeps turning.
    /// </summary>
    internal static class WallAttachmentRoll
    {
        /// <summary>Degrees of roll input the item stays upright for while turning through upright.</summary>
        private const float UprightDeadzoneDegrees = 18f;

        /// <summary>
        /// Items facing more steeply up or down than this face a ceiling, floor or tabletop rather
        /// than a wall.
        /// </summary>
        private const float MaxWallFacingY = 0.8f;

        private const float HalfDeadzone = UprightDeadzoneDegrees / 2f;
        private const float InputPeriod = 360f + UprightDeadzoneDegrees;

        private static ShipItem currentItem;

        /// <summary>
        /// A small item's accumulated roll input, wrapped to one full turn centred on upright. One
        /// full turn spans <see cref="InputPeriod"/> degrees, the extra ones being the dead zone.
        /// </summary>
        private static float rollInput;

        /// <summary>The dead zone that holds a big item upright while its roll turns through upright.</summary>
        private static readonly Deadzone BigItemDeadzone = new Deadzone(UprightDeadzoneDegrees);

        /// <summary>
        /// The roll <see cref="RollHeldItem"/> last added to the small held item's rotation. An item
        /// taken off a surface starts out counted as already carrying its roll.
        /// </summary>
        private static float heldItemRoll;

        /// <summary>
        /// The component HooksHangMore keeps the held hook's own turn on, or null if it doesn't turn it.
        /// </summary>
        private static Component hookTurnOwner;

        /// <summary>
        /// Gets whether the held item currently takes roll input: it's wall-attachable and Wall Attachment Rotation is
        /// on.
        /// </summary>
        public static bool IsActive =>
            currentItem != null
            && currentItem.wallAttachment
            && ModConfig.WallAttachmentRotationEnabled.Value;

        /// <summary>
        /// Starts tracking the held item. A small item that's wall-attachable when picked up starts at the roll that
        /// would attach it back the same way if it was taken off a surface, and upright otherwise. The previously tracked
        /// item gets back any roll taken off it earlier in the frame.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        /// <param name="wasAttached">Whether the item was attached to a surface when picked up.</param>
        public static void SetCurrentItem(PickupableItem item, bool wasAttached)
        {
            if (currentItem != null)
                RollHeldItem(currentItem);

            currentItem = item as ShipItem;
            rollInput = 0f;
            BigItemDeadzone.Restart(false);
            hookTurnOwner = null;
            heldItemRoll = 0f;

            if (currentItem == null || !currentItem.wallAttachment)
                return;

            if (currentItem.big)
            {
                Vector3 up = AttachmentSpace.UpInWorld(currentItem);
                BigItemDeadzone.Restart(
                    FacesWall(currentItem.transform.forward, up)
                    && Deadzone.IsAtRest(MeasureRoll(currentItem.transform.rotation, up)));
                return;
            }

            hookTurnOwner = HooksHangMoreCompat.GetTurnOwner(currentItem);
            if (TakesOverHook(currentItem))
                HooksHangMoreCompat.ClearTurn(hookTurnOwner);
            if (wasAttached)
            {
                rollInput = RollToInput(ReadAttachedRoll(currentItem));
                heldItemRoll = InputToRoll(rollInput);
            }
        }

        /// <summary>
        /// Adds roll input for a small item, a full turn either way.
        /// </summary>
        /// <param name="degrees">Roll input to add; positive turns counterclockwise as seen facing the wall.</param>
        public static void AddRollInput(float degrees)
        {
            rollInput = Mathf.Repeat(rollInput + degrees + InputPeriod / 2f, InputPeriod) - InputPeriod / 2f;
        }

        /// <summary>
        /// Returns whether Deft Hands turns the held <paramref name="item"/> itself in place of HooksHangMore's turn:
        /// it is a hook HooksHangMore turns, and Wall Attachment Rotation is on or
        /// <see cref="WallPlacement.TurnsItemsOnWall"/>.
        /// </summary>
        public static bool TakesOverHook(ShipItem item)
        {
            return IsTurnedHook(item)
                && (ModConfig.WallAttachmentRotationEnabled.Value || WallPlacement.TurnsItemsOnWall);
        }

        /// <summary>
        /// Returns whether Deft Hands works out where the held <paramref name="item"/> attaches itself: it is a hook
        /// HooksHangMore turns, and Easy Wall Hanging is on or <see cref="TakesOverHook"/>.
        /// </summary>
        public static bool AimsHook(ShipItem item)
        {
            return IsTurnedHook(item) && (ModConfig.EasyWallHanging.Value || TakesOverHook(item));
        }

        /// <summary>
        /// Returns the turn, in degrees, HooksHangMore's own controls gave the held hook <paramref name="item"/>, or
        /// none while <see cref="TakesOverHook"/>.
        /// </summary>
        /// <param name="item">A hook <see cref="AimsHook"/> is true for.</param>
        public static float GetHookTurn(ShipItem item)
        {
            return TakesOverHook(item) ? 0f : HooksHangMoreCompat.GetTurn(hookTurnOwner);
        }

        private static bool IsTurnedHook(ShipItem item)
        {
            return ReferenceEquals(item, currentItem) && !ReferenceEquals(hookTurnOwner, null);
        }

        /// <summary>
        /// Moves the turn HooksHangMore's own controls gave the held hook into the hook's roll, on its surface while
        /// <see cref="WallPlacement.TurnsItemsOnWall"/>, and sets HooksHangMore's turn back to none, while
        /// <see cref="TakesOverHook"/>.
        /// </summary>
        public static void TakeHookTurn()
        {
            if (!TakesOverHook(currentItem))
                return;

            float turn = HooksHangMoreCompat.GetTurn(hookTurnOwner);
            if (turn == 0f)
                return;

            HooksHangMoreCompat.ClearTurn(hookTurnOwner);
            if (WallPlacement.TurnsItemsOnWall)
                WallAttachmentAdjustment.AddRoll(turn);
            else
                AddRollInput(turn);
        }

        /// <summary>
        /// Resets the held item's roll to upright.
        /// </summary>
        public static void ResetRoll()
        {
            rollInput = 0f;
            BigItemDeadzone.Restart(false);
        }

        /// <summary>
        /// Drops roll input left inside the dead zone, so the next rotation starts from its centre.
        /// </summary>
        public static void RecenterDeadzone()
        {
            if (Mathf.Abs(rollInput) <= HalfDeadzone)
                rollInput = 0f;

            BigItemDeadzone.Recenter();
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
                BigItemDeadzone.Restart(false);
                item.Rotate(axis, degrees, Space.World);
                return;
            }

            float turn = BigItemDeadzone.Take(degrees, out bool leftUpright);
            if (turn == 0f)
                return;

            float rollBefore = MeasureRoll(item.rotation, up);
            item.Rotate(axis, turn, Space.World);
            if (leftUpright)
                return;

            float correction = BigItemDeadzone.Settle(rollBefore, MeasureRoll(item.rotation, up));
            if (correction != 0f)
                item.Rotate(item.forward, correction, Space.World);
        }

        /// <summary>
        /// Undoes any roll a big item picked up from other rotations while the dead zone holds it
        /// upright. Must be called after all of the frame's rotations.
        /// </summary>
        public static void HoldBigItemUpright(Transform item)
        {
            if (!BigItemDeadzone.IsHolding)
                return;

            Vector3 up = AttachmentSpace.UpInWorld(currentItem);
            if (FacesWall(item.forward, up))
                SetRoll(item, up, 0f);
            else
                BigItemDeadzone.Restart(false);
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
            if (!IsActive || item != currentItem)
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
        /// Takes the roll <see cref="RollHeldItem"/> added last frame back off the held item. Must
        /// be called before the holding GoPointer positions it for the frame.
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

            bool rollsInHand = IsActive && !currentItem.big;
            float roll = rollsInHand ? InputToRoll(rollInput) : 0f;
            if (roll != heldItemRoll)
                item.transform.rotation *= Quaternion.AngleAxis(roll - heldItemRoll, Vector3.forward);

            heldItemRoll = roll;
        }

        /// <summary>
        /// Returns the roll a small item taken off a surface should be held at to attach back the
        /// way it was. On a ceiling or floor that's relative to the current view.
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

        /// <summary>
        /// Returns whether an item facing <paramref name="forward"/> faces a wall rather than a ceiling, floor or
        /// tabletop, with <paramref name="up"/> as up.
        /// </summary>
        public static bool FacesWall(Vector3 forward, Vector3 up)
        {
            return Mathf.Abs(Vector3.Dot(forward, up)) <= MaxWallFacingY;
        }

        private static float InputToRoll(float input)
        {
            return Mathf.Abs(input) <= HalfDeadzone ? 0f : input - Mathf.Sign(input) * HalfDeadzone;
        }

        private static float RollToInput(float roll)
        {
            return Deadzone.IsAtRest(roll) ? 0f : roll + Mathf.Sign(roll) * HalfDeadzone;
        }
    }
}
