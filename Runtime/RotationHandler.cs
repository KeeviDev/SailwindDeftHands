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
        /// Quadrant offsets within this many degrees of zero snap to exactly zero.
        /// </summary>
        private const float QuadrantZeroDeadzoneDegrees = 2f;

        /// <summary>
        /// Rotation speed of single-axis rotations (small items, quadrant, wall-attachment roll)
        /// relative to big items.
        /// </summary>
        private const float SingleAxisSpeedMultiplier = 2f;

        /// <summary>Mouse movement below this on both axes is ignored.</summary>
        private const float MinAppliedInput = 0.001f;

        /// <summary>Mouse movement is ignored for this many seconds after rotating starts.</summary>
        private const float RotationStartDelay = 0.1f;

        private static RotationHandler instance;

        private PickupableItem currentItem;

        private bool isCurrentItemRotatable;

        private bool wasRotating;

        private float rotationStartTime;

        private readonly RotationInertia inertia = new RotationInertia();

        /// <summary>
        /// The quadrant's accumulated rotation before the zero-position deadzone is applied.
        /// </summary>
        private float quadrantRawOffset;

        /// <summary>
        /// The last value written to the quadrant's heldRotationOffset, used to detect when
        /// other code changed it.
        /// </summary>
        private float lastAppliedQuadrantOffset;

        /// <summary>
        /// Gets whether the player is currently mouse-rotating the held item, with Mouse Rotation Control on or a
        /// wall-attachable item while <see cref="WallAttachmentRoll.IsActive"/>; never true for items with their own
        /// OnScroll behaviour.
        /// </summary>
        public bool IsRotating =>
            ModInput.IsRotationModifierKeyHeld()
            && IsHoldingRotatableItem
            && (ModConfig.MouseRotationEnabled.Value || WallAttachmentRoll.IsActive);

        private bool IsHoldingRotatableItem => isCurrentItemRotatable && currentItem != null && currentItem.held != null;

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
            isCurrentItemRotatable = item != null && !PickupableItemUtils.HasCustomOnScroll(item);
            inertia.Reset();

            float offset = item != null ? item.heldRotationOffset : 0f;
            quadrantRawOffset = offset;
            lastAppliedQuadrantOffset = offset;
        }

        private void Update()
        {
            if (ModInput.IsResetOrientationPressed() && IsHoldingRotatableItem)
                ResetHeldItemOrientation();

            if (!IsRotating)
            {
                wasRotating = false;
                inertia.Reset();
                WallAttachmentRoll.RecenterDeadzone();
                return;
            }

            if (!wasRotating)
            {
                wasRotating = true;
                rotationStartTime = Time.unscaledTime;
            }
            if (Time.unscaledTime - rotationStartTime < RotationStartDelay)
                return;

            Vector2 input = ReadMouseInput();
            if (Mathf.Abs(input.x) < MinAppliedInput && Mathf.Abs(input.y) < MinAppliedInput)
                return;

            RotateHeldItem(input.x, input.y);
        }

        /// <summary>
        /// Returns this frame's mouse movement, smoothed according to the held item's weight when rotation inertia is
        /// enabled.
        /// </summary>
        private Vector2 ReadMouseInput()
        {
            return inertia.Apply(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")), currentItem);
        }

        /// <summary>
        /// Resets the held item's orientation in the hand: a small item gets the tilt its prefab is held at, a big item
        /// stands upright in <see cref="RotationFrame"/>, facing the player's heading.
        /// </summary>
        private void ResetHeldItemOrientation()
        {
            inertia.Reset();
            WallAttachmentRoll.ResetRoll();
            float offset = currentItem.big ? 0f : PickupableItemUtils.GetPrefabHeldRotationOffset(currentItem);
            currentItem.heldRotationOffset = offset;
            quadrantRawOffset = offset;
            lastAppliedQuadrantOffset = offset;
            if (currentItem.big)
                BigItemRotation.StandUpright(currentItem);
        }

        /// <summary>
        /// Applies this frame's input to the held item's rotation. Horizontal movement on the roll
        /// axis also rolls wall-attachable small items for attaching; while Mouse Rotation Control is off, that roll is
        /// all the input does. While <see cref="WallPlacement.LocksRotationAxis"/>, only horizontal movement turns the
        /// item.
        /// </summary>
        private void RotateHeldItem(float horizontalInput, float verticalInput)
        {
            if (WallPlacement.LocksRotationAxis(currentItem))
            {
                TurnAroundSurfaceAxis(horizontalInput);
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            float sensitivity = ModConfig.RotationSensitivity.Value;
            float singleAxisSensitivity = sensitivity * SingleAxisSpeedMultiplier;
            bool turnsInHand = ModConfig.MouseRotationEnabled.Value;
            float tiltInput = turnsInHand ? ModInput.ApplyInversion(verticalInput, ModConfig.InvertVertical) : 0f;
            bool turnsInsteadOfRolling = turnsInHand && ModInput.TurnsInsteadOfRolls();

            if (WallAttachmentRoll.IsActive && !currentItem.big)
            {
                float roll = -ModInput.ApplyInversion(horizontalInput, ModConfig.InvertRoll);
                WallAttachmentRoll.AddRollInput(roll * singleAxisSensitivity);
            }

            if (currentItem.big)
                BigItemRotation.Rotate(
                    currentItem, mainCamera, horizontalInput, tiltInput, sensitivity, turnsInsteadOfRolling);
            else if (currentItem is ShipItemQuadrant)
                RotateQuadrant(tiltInput * singleAxisSensitivity);
            else
                currentItem.heldRotationOffset += tiltInput * singleAxisSensitivity;
        }

        /// <summary>
        /// Turns the held item being placed around its own axis into the surface by
        /// <paramref name="horizontalInput"/>: a small item through its attachment roll, a big item in
        /// the hand, pausing at upright while turning through it.
        /// </summary>
        private void TurnAroundSurfaceAxis(float horizontalInput)
        {
            float sensitivity = ModConfig.RotationSensitivity.Value;
            float roll = -ModInput.ApplyInversion(horizontalInput, ModConfig.InvertRoll);
            if (!currentItem.big)
            {
                WallAttachmentRoll.AddRollInput(roll * sensitivity * SingleAxisSpeedMultiplier);
                return;
            }

            Transform item = currentItem.transform;
            BigItemRotation.TurnToHolderPose(currentItem);
            WallAttachmentRoll.RollBigItem(item, item.forward, roll * sensitivity);
            WallAttachmentRoll.HoldBigItemUpright(item);
            BigItemRotation.StoreRotation(currentItem);
        }

        /// <summary>
        /// Rotates the quadrant around its single axis, snapping to zero within
        /// <see cref="QuadrantZeroDeadzoneDegrees"/> of it, and picks up changes other code made to
        /// its offset in the meantime.
        /// </summary>
        /// <param name="angle">Rotation to add, in degrees.</param>
        private void RotateQuadrant(float angle)
        {
            if (!Mathf.Approximately(currentItem.heldRotationOffset, lastAppliedQuadrantOffset))
                quadrantRawOffset = currentItem.heldRotationOffset;

            quadrantRawOffset += angle;
            lastAppliedQuadrantOffset = ApplyDeadzone(quadrantRawOffset, QuadrantZeroDeadzoneDegrees);
            currentItem.heldRotationOffset = lastAppliedQuadrantOffset;
        }

        /// <summary>
        /// Returns 0 when <paramref name="raw"/> is within <paramref name="zone"/> of it;
        /// otherwise returns <paramref name="raw"/> unchanged.
        /// </summary>
        private static float ApplyDeadzone(float raw, float zone)
        {
            return Mathf.Abs(raw) <= zone ? 0f : raw;
        }
    }
}
