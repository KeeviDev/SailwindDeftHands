using DeftHands.Configuration;
using DeftHands.Utils;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Rotates the held item with mouse movement while the rotation modifier key is held,
    /// optionally smoothed to make heavier items feel weighted.
    /// </summary>
    public class RotationHandler : MonoBehaviour
    {
        /// <summary>
        /// Rotation speed of single-axis rotations (small items, wall-attachment roll)
        /// relative to big items.
        /// </summary>
        private const float SingleAxisSpeedMultiplier = 2f;

        /// <summary>Mouse movement below this on both axes is ignored.</summary>
        private const float MinAppliedInput = 0.001f;

        /// <summary>Mouse movement is ignored for this many seconds after rotating starts.</summary>
        private const float RotationStartDelay = 0.1f;

        private static RotationHandler instance;

        private PickupableItem currentItem;

        private bool wasRotating;

        private float rotationStartTime;

        /// <summary>Whether the current rotation started while the item was placed on a surface for items.</summary>
        private bool rotatesOnSurface;

        /// <summary>
        /// Whether mouse rotation waits for the rotation modifier key to be released, after a reset made while holding
        /// it.
        /// </summary>
        private bool waitsForModifierRelease;

        private readonly RotationInertia inertia = new RotationInertia();

        /// <summary>
        /// Gets whether the player is currently mouse-rotating the held item where <see cref="TakesMouseRotation"/>;
        /// never true for items with their own OnScroll behaviour that can't be held like a big item, unless placed on
        /// a surface for items, or for a compass locked to the course.
        /// </summary>
        public bool IsRotating =>
            ModInput.IsRotationModifierKeyHeld()
            && IsHoldingRotatableItem
            && TakesMouseRotation(currentItem)
            && !CompassAlignment.IsLockedToCourse(currentItem);

        private bool IsHoldingRotatableItem =>
            currentItem != null
            && currentItem.held != null
            && !HeldItemVisibility.IsHidden(currentItem)
            && (FurniturePlacement.IsPlacing(currentItem)
                || !PickupableItemUtils.HasCustomOnScroll(currentItem)
                || ExtendedHold.IsHeldLikeBig(currentItem)
                || ExtendedHold.CanHoldLikeBig(currentItem));

        /// <summary>
        /// Returns whether mouse movement with the rotation modifier key turns the held <paramref name="item"/> where
        /// it is now: always on a surface for items it is placed on; on its wall while
        /// <see cref="WallPlacement.TurnsOnWall"/>, with Wall Attachment Rotation on or
        /// <see cref="WallAttachmentAdjustment.TakesTiltInput"/>; in the hand while Mouse Rotation Control is on, or
        /// for an item being hung while <see cref="WallAttachmentRoll.IsActive"/>.
        /// </summary>
        private static bool TakesMouseRotation(PickupableItem item)
        {
            if (FurniturePlacement.IsPlacing(item))
                return true;

            if (WallPlacement.TurnsOnWall(item))
                return ModConfig.WallAttachmentRotationEnabled.Value || WallAttachmentAdjustment.TakesTiltInput(item);

            return ModConfig.MouseRotationEnabled.Value
                || (WallAttachmentRoll.IsActive && WallPlacement.IsBeingHung(item));
        }

        public static RotationHandler GetInstance()
        {
            if (instance == null)
                instance = PersistentObject.Create<RotationHandler>();

            return instance;
        }

        /// <summary>
        /// Registers the currently held item and resets per-item rotation state.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        public void SetCurrentItem(PickupableItem item)
        {
            currentItem = item;
            inertia.Reset();
        }

        private void Update()
        {
            if (ModInput.IsResetOrientationPressed()
                && (IsHoldingRotatableItem || CompassAlignment.IsLockedToCourse(currentItem)))
                ResetHeldItemOrientation();

            if (!IsRotating)
            {
                EndRotation();
                return;
            }

            if (waitsForModifierRelease)
                return;

            if (!wasRotating)
                BeginRotation();

            if (Time.unscaledTime - rotationStartTime < RotationStartDelay)
                return;

            Vector2 input = ReadMouseInput();
            if (Mathf.Abs(input.x) < MinAppliedInput && Mathf.Abs(input.y) < MinAppliedInput)
                return;

            if (FurniturePlacement.IsPlacing(currentItem))
            {
                TurnOnSurface(input.x, input.y);
            }
            else if (!rotatesOnSurface)
            {
                FurniturePlacement.GetInstance().ForgetRotation();
                ExtendedHold.GetInstance().TryHoldLikeBig(currentItem);
                RotateHeldItem(input.x, input.y);
            }
        }

        /// <summary>
        /// Returns this frame's mouse movement, smoothed according to the held item's weight when rotation inertia is
        /// enabled.
        /// </summary>
        private Vector2 ReadMouseInput()
        {
            return inertia.Apply(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")), currentItem);
        }

        private void BeginRotation()
        {
            wasRotating = true;
            rotationStartTime = Time.unscaledTime;
            rotatesOnSurface = FurniturePlacement.IsPlacing(currentItem);
        }

        private void EndRotation()
        {
            wasRotating = false;
            waitsForModifierRelease = false;
            inertia.Reset();
            WallAttachmentRoll.RecenterDeadzone();
            WallAttachmentAdjustment.RecenterDeadzone();
        }

        /// <summary>
        /// Resets the held item's orientation, and unlocks it from the course when it is a compass locked to it. An
        /// item placed on a surface for items turns back to its resting rotation there, facing the player's heading,
        /// and stays held the way it was; in the hand, a big item stands upright in <see cref="RotationFrame"/>, facing
        /// the player's heading, and a small item stops being held like a big item and eases back into the usual hold,
        /// at the tilt it rests at there (<see cref="PickupableItemUtils.GetRestingHeldRotationOffset"/>).
        /// </summary>
        private void ResetHeldItemOrientation()
        {
            inertia.Reset();
            waitsForModifierRelease = ModInput.IsRotationModifierKeyHeld();
            CompassAlignment.Unlock(currentItem);
            if (FurniturePlacement.IsPlacing(currentItem))
            {
                FurniturePlacement.GetInstance().ResetRotation();
                return;
            }

            ExtendedHold.GetInstance().ReturnToHand(currentItem);
            FurniturePlacement.GetInstance().ForgetRotation();
            WallAttachmentRoll.ResetRoll();
            WallAttachmentAdjustment.Reset();
            if (currentItem.big)
            {
                currentItem.heldRotationOffset = 0f;
                BigItemRotation.StandUpright(currentItem);
            }
            else
            {
                currentItem.heldRotationOffset = PickupableItemUtils.GetRestingHeldRotationOffset(currentItem);
            }
        }

        /// <summary>
        /// Applies this frame's input to the held item's rotation, as to a big item for an item held like one.
        /// Horizontal movement on the roll axis also rolls wall-attachable small items for attaching; while Mouse
        /// Rotation Control is off, that roll is all the input does. While <see cref="WallPlacement.TurnsOnWall"/>, the
        /// input tilts the item to its surface while <see cref="WallAttachmentAdjustment.TakesTiltInput"/>, and
        /// otherwise only horizontal movement rolls it around its surface's normal while Wall Attachment Rotation is
        /// on.
        /// </summary>
        private void RotateHeldItem(float horizontalInput, float verticalInput)
        {
            if (WallAttachmentAdjustment.TakesTiltInput(currentItem))
            {
                TiltToSurface(horizontalInput, verticalInput);
                return;
            }

            if (WallPlacement.TurnsOnWall(currentItem))
            {
                if (ModConfig.WallAttachmentRotationEnabled.Value)
                    TurnAroundSurfaceAxis(horizontalInput);
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            bool turnsInHand = ModConfig.MouseRotationEnabled.Value;
            float sensitivity = ModConfig.RotationSensitivity.Value;
            float singleAxisSensitivity = sensitivity * SingleAxisSpeedMultiplier;
            float tiltInput = turnsInHand ? ModInput.ApplyInversion(verticalInput, ModConfig.InvertVertical) : 0f;
            bool turnsInsteadOfRolling = turnsInHand && ModInput.TurnsInsteadOfRolls();

            if (WallAttachmentRoll.IsActive && !currentItem.big)
            {
                float roll = -ModInput.ApplyInversion(horizontalInput, ModConfig.InvertRoll);
                WallAttachmentRoll.AddRollInput(roll * singleAxisSensitivity);
            }

            if (ExtendedHold.IsHeldLikeBig(currentItem))
                BigItemRotation.Rotate(
                    currentItem, mainCamera, horizontalInput, tiltInput, sensitivity, turnsInsteadOfRolling);
            else
                currentItem.heldRotationOffset += tiltInput * singleAxisSensitivity;
        }

        /// <summary>
        /// Tilts the held item being hung to its surface: <paramref name="horizontalInput"/> around the axis running up
        /// the surface, <paramref name="verticalInput"/> around the axis running across it.
        /// </summary>
        private static void TiltToSurface(float horizontalInput, float verticalInput)
        {
            float sensitivity = ModConfig.RotationSensitivity.Value;
            WallAttachmentAdjustment.AddTilt(
                -ModInput.ApplyInversion(horizontalInput, ModConfig.InvertHangingTurn) * sensitivity,
                ModInput.ApplyInversion(verticalInput, ModConfig.InvertVertical) * sensitivity);
        }

        /// <summary>
        /// Rolls the held item being hung around its surface's normal by <paramref name="horizontalInput"/>, a small
        /// item at the single-axis speed.
        /// </summary>
        private void TurnAroundSurfaceAxis(float horizontalInput)
        {
            float speed = currentItem.big ? 1f : SingleAxisSpeedMultiplier;
            float roll = -ModInput.ApplyInversion(horizontalInput, ModConfig.InvertRoll);
            WallAttachmentAdjustment.AddRoll(roll * ModConfig.RotationSensitivity.Value * speed);
        }

        /// <summary>
        /// Turns the item placed on a surface for items by this frame's input the way a big item turns, around axes
        /// level with the surface.
        /// </summary>
        private static void TurnOnSurface(float horizontalInput, float verticalInput)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            FurniturePlacement placement = FurniturePlacement.GetInstance();
            Vector3 up = placement.SurfaceUp;
            Vector3 tiltAxis = placement.GetTiltAxis(mainCamera.transform);
            float sensitivity = ModConfig.RotationSensitivity.Value;
            Vector3 rollAxis = Vector3.Cross(tiltAxis, up);
            float turnAngle = ModInput.ApplyInversion(horizontalInput, ModConfig.InvertTurn) * sensitivity;
            float rollAngle = -ModInput.ApplyInversion(horizontalInput, ModConfig.InvertRoll) * sensitivity;
            Quaternion turn = ModInput.TurnsInsteadOfRolls()
                ? Quaternion.AngleAxis(turnAngle, up)
                : Quaternion.AngleAxis(rollAngle, rollAxis);
            if (!ModInput.IsTiltLocked())
            {
                float tilt = ModInput.ApplyInversion(verticalInput, ModConfig.InvertVertical) * sensitivity;
                turn = Quaternion.AngleAxis(tilt, tiltAxis) * turn;
            }

            placement.Turn(turn);
        }
    }
}
