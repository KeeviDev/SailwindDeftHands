using BepInEx.Configuration;
using DeftHands.Compat;
using DeftHands.Configuration;
using DeftHands.Utils;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Rotates the held item with mouse movement while the rotation activation key is held,
    /// optionally smoothed to make heavier items feel weighted.
    /// </summary>
    public class RotationHandler : MonoBehaviour
    {
        /// <summary>
        /// Quadrant offsets within this many degrees of zero, its star-sighting calibration
        /// point, snap to exactly zero.
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
        /// Rotation speed of single-axis rotations (small items, quadrant, hook swing) relative
        /// to big items.
        /// </summary>
        private const float SingleAxisSpeedMultiplier = 2f;

        /// <summary>Mouse movement below this on both axes is ignored.</summary>
        private const float MinAppliedInput = 0.001f;

        private static readonly FieldInfo BigItemLocalRotField = AccessTools.Field(typeof(GoPointer), "bigItemLocalRot");

        private static RotationHandler instance;

        private PickupableItem currentItem;

        /// <summary>Weight-smoothed replacement for this frame's raw mouse delta.</summary>
        private float smoothedX;
        private float smoothedY;

        /// <summary>Mathf.SmoothDamp's own internal velocity state for smoothedX/Y.</summary>
        private float dampVelocityX;
        private float dampVelocityY;

        /// <summary>
        /// The quadrant's accumulated rotation before the zero-position deadzone is applied, so
        /// the deadzone doesn't lose how far into it the cursor actually is.
        /// </summary>
        private float quadrantRawOffset;

        /// <summary>
        /// The last value written to the quadrant's heldRotationOffset, used to detect when
        /// other code changed it.
        /// </summary>
        private float lastAppliedQuadrantOffset;

        /// <summary>Whether the player is currently mouse-rotating a held item.</summary>
        public bool IsRotating =>
            ModConfig.MouseRotationEnabled.Value
            && ModInput.IsRotationActivationKeyHeld()
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
        /// Applies this frame's input to the held item's rotation. Leaves items with their own
        /// OnScroll behaviour alone.
        /// </summary>
        private void RotateHeldItem(float horizontalInput, float verticalInput)
        {
            if (PickupableItemUtils.HasCustomOnScroll(currentItem))
                return;

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            float sensitivity = ModConfig.RotationSensitivity.Value;
            float singleAxisSensitivity = sensitivity * SingleAxisSpeedMultiplier;
            float tiltInput = ApplyInversion(verticalInput, ModConfig.InvertVertical);
            float hookSwingInput = ApplyInversion(horizontalInput, ModConfig.InvertRoll);

            HooksHangMoreCompat.AddHookSwing(currentItem, -hookSwingInput * singleAxisSensitivity);

            if (currentItem.big)
                RotateBigItem(currentItem, mainCamera, horizontalInput, tiltInput, sensitivity);
            else if (currentItem is ShipItemQuadrant)
                RotateQuadrant(tiltInput * singleAxisSensitivity);
            else
                currentItem.heldRotationOffset += tiltInput * singleAxisSensitivity;
        }

        /// <summary>
        /// Rotates a big item freely relative to the camera. Vertical movement pitches around
        /// the camera's right axis; horizontal movement rolls around its forward axis, or turns
        /// around its up axis when Alternative Rotation Axis is on. Holding the axis swap key
        /// flips that choice.
        /// </summary>
        /// <param name="horizontalInput">Raw horizontal input; inverted here per the chosen axis's setting.</param>
        /// <param name="tiltInput">Vertical input with vertical inversion already applied.</param>
        private static void RotateBigItem(PickupableItem item, Camera mainCamera, float horizontalInput, float tiltInput, float sensitivity)
        {
            bool useAlternativeAxis = ModConfig.UseAlternativeRotationAxis.Value ^ ModInput.IsAxisSwapKeyHeld();
            Vector3 horizontalAxis = useAlternativeAxis ? mainCamera.transform.up : mainCamera.transform.forward;
            ConfigEntry<bool> invertHorizontal = useAlternativeAxis ? ModConfig.InvertTurn : ModConfig.InvertRoll;

            item.transform.Rotate(horizontalAxis, -ApplyInversion(horizontalInput, invertHorizontal) * sensitivity, Space.World);
            item.transform.Rotate(mainCamera.transform.right, tiltInput * sensitivity, Space.World);
            StoreBigItemRotation(item);
        }

        private static float ApplyInversion(float input, ConfigEntry<bool> invert)
        {
            return invert.Value ? -input : input;
        }

        /// <summary>
        /// Stores the item's current rotation as the holding GoPointer's bigItemLocalRot, the
        /// same way the game's own Q-axis rotation does, so it persists on following frames.
        /// </summary>
        private static void StoreBigItemRotation(PickupableItem item)
        {
            if (BigItemLocalRotField == null)
                return;

            Quaternion holderRotation = item.held.transform.rotation;
            BigItemLocalRotField.SetValue(item.held, Quaternion.Inverse(holderRotation) * item.transform.rotation);
            item.heldRotationOffset = 0f;
        }

        /// <summary>
        /// Rotates the quadrant around its single axis, snapping to zero near its calibration
        /// point (see <see cref="QuadrantZeroDeadzoneDegrees"/>).
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
