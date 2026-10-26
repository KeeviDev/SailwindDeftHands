using BepInEx.Configuration;
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
        /// Maps ShipItem.mass to a 0..1 heaviness factor: no effect at or below
        /// <see cref="WeightEffectThreshold"/>, full effect at or above
        /// <see cref="WeightEffectCap"/>, with <see cref="WeightCurvePower"/> shaping the ramp.
        /// </summary>
        private const float WeightEffectThreshold = 0f;
        private const float WeightEffectCap = 900f;
        private const float WeightCurvePower = 0.4f;

        private const float StandardSmoothTime = 0.25f;
        private const float LightSmoothTime = 0.1f;

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

        /// <summary>Weight-smoothed replacement for this frame's raw mouse delta.</summary>
        private float smoothedX;
        private float smoothedY;

        /// <summary>Mathf.SmoothDamp's own internal velocity state for smoothedX/Y.</summary>
        private float dampVelocityX;
        private float dampVelocityY;

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
            ResetWeightState();

            float offset = item != null ? item.heldRotationOffset : 0f;
            quadrantRawOffset = offset;
            lastAppliedQuadrantOffset = offset;
        }

        private void Update()
        {
            if (!IsRotating)
            {
                ResetWeightState();
                WallAttachmentRoll.RecenterDeadzone();
                return;
            }

            Vector2 input = ReadMouseInput();
            if (Mathf.Abs(input.x) < MinAppliedInput && Mathf.Abs(input.y) < MinAppliedInput)
                return;

            RotateHeldItem(input.x, input.y);
        }

        /// <summary>
        /// Returns this frame's mouse movement, smoothed according to the held item's weight
        /// when rotation inertia is enabled. Smoothing keeps decaying after the mouse stops.
        /// </summary>
        private Vector2 ReadMouseInput()
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            if (!ModConfig.RotationInertiaEnabled.Value)
            {
                ResetWeightState();
                return new Vector2(mouseX, mouseY);
            }

            float smoothTime = GetHeaviness() * GetPresetSmoothTime();
            smoothedX = Mathf.SmoothDamp(smoothedX, mouseX, ref dampVelocityX, smoothTime);
            smoothedY = Mathf.SmoothDamp(smoothedY, mouseY, ref dampVelocityY, smoothTime);
            return new Vector2(smoothedX, smoothedY);
        }

        private void ResetWeightState()
        {
            smoothedX = 0f;
            smoothedY = 0f;
            dampVelocityX = 0f;
            dampVelocityY = 0f;
        }

        private static float GetPresetSmoothTime()
        {
            return ModConfig.WeightPreset.Value == RotationWeightPreset.Light ? LightSmoothTime : StandardSmoothTime;
        }

        /// <summary>
        /// Returns how heavy the held item feels, from 0 (no smoothing) to 1 (full preset
        /// strength). Items that aren't a ShipItem are treated as weightless.
        /// </summary>
        private float GetHeaviness()
        {
            if (!(currentItem is ShipItem shipItem))
                return 0f;

            float t = Mathf.InverseLerp(WeightEffectThreshold, WeightEffectCap, shipItem.mass);
            return Mathf.Pow(t, WeightCurvePower);
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
            float tiltInput = ApplyInversion(verticalInput, ModConfig.InvertVertical);
            bool useAlternativeAxis = IsAlternativeAxisActive();

            if (WallAttachmentRoll.IsActive && !currentItem.big)
                WallAttachmentRoll.AddRollInput(-ApplyInversion(horizontalInput, ModConfig.InvertRoll) * singleAxisSensitivity);

            if (currentItem.big)
                RotateBigItem(currentItem, mainCamera, horizontalInput, tiltInput, sensitivity, useAlternativeAxis);
            else if (currentItem is ShipItemQuadrant)
                RotateQuadrant(tiltInput * singleAxisSensitivity);
            else
                currentItem.heldRotationOffset += tiltInput * singleAxisSensitivity;
        }

        /// <summary>
        /// Returns whether horizontal movement currently turns big items around the camera's up axis
        /// instead of rolling them: Alternative Rotation Axis, flipped while the axis swap key is
        /// held.
        /// </summary>
        private static bool IsAlternativeAxisActive()
        {
            return ModConfig.UseAlternativeRotationAxis.Value ^ ModInput.IsAxisSwapKeyHeld();
        }

        /// <summary>
        /// Returns whether vertical movement currently leaves big items at their tilt: Lock Tilt is
        /// On Axis Swap and the axis swap key is held, or Without Axis Swap and it isn't.
        /// </summary>
        private static bool IsTiltLocked()
        {
            switch (ModConfig.LockTilt.Value)
            {
                case TiltLock.WithoutAxisSwap: return !ModInput.IsAxisSwapKeyHeld();
                case TiltLock.OnAxisSwap: return ModInput.IsAxisSwapKeyHeld();
                default: return false;
            }
        }

        /// <summary>
        /// Rotates a big item freely relative to the camera. Vertical movement tilts it around the
        /// camera's right axis; horizontal movement rolls it around the camera's forward axis, or
        /// turns it around the camera's up axis on the alternative axis. Keep Turning Level, Keep
        /// Rolling Level and Keep Tilting Level replace these axes with level ones in
        /// <see cref="RotationFrame"/>. Vertical movement is ignored while
        /// <see cref="IsTiltLocked"/>.
        /// Wall-attachable items pause at upright while rolling through it.
        /// </summary>
        /// <param name="horizontalInput">Raw horizontal input; inverted here per the active axis's setting.</param>
        /// <param name="tiltInput">Vertical input with vertical inversion already applied.</param>
        private static void RotateBigItem(PickupableItem item, Camera mainCamera, float horizontalInput, float tiltInput, float sensitivity, bool useAlternativeAxis)
        {
            Transform camera = mainCamera.transform;
            TurnToHolderPose(item);

            if (useAlternativeAxis)
                item.transform.Rotate(GetTurnAxis(camera), ApplyInversion(horizontalInput, ModConfig.InvertTurn) * sensitivity, Space.World);
            else
                RollBigItem(item, GetRollAxis(camera), -ApplyInversion(horizontalInput, ModConfig.InvertRoll) * sensitivity);

            if (!IsTiltLocked())
                item.transform.Rotate(GetTiltAxis(camera), tiltInput * sensitivity, Space.World);

            if (WallAttachmentRoll.IsActive)
                WallAttachmentRoll.HoldBigItemUpright(item.transform);

            StoreBigItemRotation(item);
        }

        private static Vector3 GetTurnAxis(Transform camera)
        {
            return ModConfig.KeepTurningLevel.Value ? RotationFrame.Up : camera.up;
        }

        private static Vector3 GetRollAxis(Transform camera)
        {
            return ModConfig.KeepRollingLevel.Value ? Vector3.Cross(RotationFrame.Flatten(camera.right), RotationFrame.Up) : camera.forward;
        }

        private static Vector3 GetTiltAxis(Transform camera)
        {
            return ModConfig.KeepTiltingLevel.Value ? RotationFrame.Flatten(camera.right) : camera.right;
        }

        private static void RollBigItem(PickupableItem item, Vector3 axis, float degrees)
        {
            if (WallAttachmentRoll.IsActive)
                WallAttachmentRoll.RollBigItem(item.transform, axis, degrees);
            else
                item.transform.Rotate(axis, degrees, Space.World);
        }

        private static float ApplyInversion(float input, ConfigEntry<bool> invert)
        {
            return invert.Value ? -input : input;
        }

        /// <summary>
        /// Sets the big item's rotation to the one its holding GoPointer gives it this frame, with
        /// <see cref="BigItemFacing"/> applied first.
        /// </summary>
        private static void TurnToHolderPose(PickupableItem item)
        {
            if (GoPointerFields.BigItemLocalRot == null)
                return;

            BigItemFacing.KeepFacing(item.held);
            Quaternion rotationInHand = GoPointerFields.BigItemLocalRot(item.held);
            Quaternion offset = Quaternion.AngleAxis(item.heldRotationOffset, Vector3.right);
            item.transform.rotation = item.held.transform.rotation * offset * rotationInHand;
        }

        /// <summary>
        /// Stores the item's current rotation as the holding GoPointer's bigItemLocalRot, so it
        /// persists on following frames, and resets the item's heldRotationOffset.
        /// </summary>
        private static void StoreBigItemRotation(PickupableItem item)
        {
            if (GoPointerFields.BigItemLocalRot == null)
                return;

            Quaternion holderRotation = item.held.transform.rotation;
            GoPointerFields.BigItemLocalRot(item.held) = Quaternion.Inverse(holderRotation) * item.transform.rotation;
            item.heldRotationOffset = 0f;
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
