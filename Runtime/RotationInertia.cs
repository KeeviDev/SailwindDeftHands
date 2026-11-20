using DeftHands.Configuration;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Smooths mouse movement by the weight of the held item while Rotation Inertia is on, so that heavier items take a
    /// moment to get going and drift to a stop.
    /// </summary>
    internal sealed class RotationInertia
    {
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

        /// <summary>Weight-smoothed replacement for this frame's raw mouse delta.</summary>
        private float smoothedX;
        private float smoothedY;

        /// <summary>Mathf.SmoothDamp's own internal velocity state for smoothedX/Y.</summary>
        private float dampVelocityX;
        private float dampVelocityY;

        /// <summary>
        /// Returns <paramref name="input"/>, this frame's mouse movement, smoothed according to the weight of
        /// <paramref name="item"/> while Rotation Inertia is on. The smoothing keeps decaying after the mouse stops.
        /// </summary>
        public Vector2 Apply(Vector2 input, PickupableItem item)
        {
            if (!ModConfig.RotationInertiaEnabled.Value)
            {
                Reset();
                return input;
            }

            float smoothTime = GetHeaviness(item) * GetPresetSmoothTime();
            smoothedX = Mathf.SmoothDamp(smoothedX, input.x, ref dampVelocityX, smoothTime);
            smoothedY = Mathf.SmoothDamp(smoothedY, input.y, ref dampVelocityY, smoothTime);
            return new Vector2(smoothedX, smoothedY);
        }

        /// <summary>
        /// Stops any movement still drifting.
        /// </summary>
        public void Reset()
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
        /// Returns how heavy <paramref name="item"/> feels, from 0 (no smoothing) to 1 (full preset strength). Items
        /// that aren't a ShipItem are treated as weightless.
        /// </summary>
        private static float GetHeaviness(PickupableItem item)
        {
            if (!(item is ShipItem shipItem))
                return 0f;

            float t = Mathf.InverseLerp(WeightEffectThreshold, WeightEffectCap, shipItem.mass);
            return Mathf.Pow(t, WeightCurvePower);
        }
    }
}
