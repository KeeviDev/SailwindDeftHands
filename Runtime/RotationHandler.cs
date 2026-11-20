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

        private static RotationHandler instance;

        private PickupableItem currentItem;

        private bool isCurrentItemRotatable;

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
        /// Gets whether the player is currently mouse-rotating the held item; never true for items
        /// with their own OnScroll behaviour.
        /// </summary>
        public bool IsRotating =>
            ModConfig.MouseRotationEnabled.Value
            && isCurrentItemRotatable
            && ModInput.IsRotationModifierKeyHeld()
            && currentItem != null
            && currentItem.held != null;

        public static RotationHandler GetInstance()
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("DeftHands_RotationHandler");
                instance = obj.AddComponent<RotationHandler>();
                DontDestroyOnLoad(obj);
            }
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
            if (!IsRotating)
            {
                inertia.Reset();
                WallAttachmentRoll.RecenterDeadzone();
                return;
            }

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
        /// Applies this frame's input to the held item's rotation. Horizontal movement on the roll
        /// axis also rolls wall-attachable small items for attaching.
        /// </summary>
        private void RotateHeldItem(float horizontalInput, float verticalInput)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            float sensitivity = ModConfig.RotationSensitivity.Value;
            float singleAxisSensitivity = sensitivity * SingleAxisSpeedMultiplier;
            float tiltInput = ModInput.ApplyInversion(verticalInput, ModConfig.InvertVertical);
            bool turnsInsteadOfRolling = ModInput.TurnsInsteadOfRolls();

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
