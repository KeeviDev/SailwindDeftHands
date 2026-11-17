using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Holds a rotation on one axis at rest for a span of input while it turns through rest, then lets it turn on from
    /// rest by whatever input is left.
    /// </summary>
    internal sealed class Deadzone
    {
        /// <summary>Angle, in degrees, from rest within which a rotation counts as at rest.</summary>
        private const float RestTolerance = 0.01f;

        /// <summary>Angle, in degrees, from rest beyond which turning across it doesn't count as passing it.</summary>
        private const float NearRestAngle = 90f;

        private readonly float halfSpan;

        /// <summary>The input taken while the rotation is held at rest, from the middle of the span.</summary>
        private float inputInside;

        /// <param name="spanDegrees">Degrees of input the rotation stays at rest for while turning through it.</param>
        public Deadzone(float spanDegrees)
        {
            halfSpan = spanDegrees / 2f;
        }

        /// <summary>Gets whether the rotation is held at rest.</summary>
        public bool IsHolding { get; private set; }

        /// <summary>Returns whether <paramref name="angle"/>, in degrees from rest, counts as at rest.</summary>
        public static bool IsAtRest(float angle)
        {
            return !float.IsNaN(angle) && Mathf.Abs(angle) < RestTolerance;
        }

        /// <summary>
        /// Holds the rotation at rest, from the middle of the span, when <paramref name="isAtRest"/>, and lets it turn
        /// otherwise.
        /// </summary>
        public void Restart(bool isAtRest)
        {
            IsHolding = isAtRest;
            inputInside = 0f;
        }

        /// <summary>Moves the input taken while the rotation is held at rest back to the middle of the span.</summary>
        public void Recenter()
        {
            inputInside = 0f;
        }

        /// <summary>
        /// Returns the degrees the rotation turns by for <paramref name="degrees"/> of input: none while it stays held
        /// at rest, and what is left beyond the span once the input leaves it.
        /// </summary>
        /// <param name="leftRest">Whether the input just left the span, which needs no <see cref="Settle"/>.</param>
        public float Take(float degrees, out bool leftRest)
        {
            leftRest = false;
            if (!IsHolding)
                return degrees;

            float input = inputInside + degrees;
            if (Mathf.Abs(input) <= halfSpan)
            {
                inputInside = input;
                return 0f;
            }

            IsHolding = false;
            inputInside = 0f;
            leftRest = true;
            return input - Mathf.Sign(input) * halfSpan;
        }

        /// <summary>
        /// Returns the degrees to turn the rotation by after it turned from <paramref name="before"/> to
        /// <paramref name="after"/> degrees from rest: back to rest, held there, when it passed rest by no more than
        /// the span, by the span when it passed rest by more, and none when it didn't pass rest. Either angle is NaN
        /// where it can't be measured.
        /// </summary>
        public float Settle(float before, float after)
        {
            if (!PassedRest(before, after))
                return 0f;

            float overshoot = Mathf.Abs(after);
            float input = Mathf.Sign(before) * (halfSpan - overshoot);
            if (Mathf.Abs(input) > halfSpan)
                return Mathf.Sign(before) * 2f * halfSpan;

            IsHolding = true;
            inputInside = input;
            return -after;
        }

        private static bool PassedRest(float before, float after)
        {
            if (float.IsNaN(before)
                || float.IsNaN(after)
                || Mathf.Abs(before) < RestTolerance
                || Mathf.Abs(before) >= NearRestAngle
                || Mathf.Abs(after) >= NearRestAngle)
                return false;

            return Mathf.Abs(after) < RestTolerance || Mathf.Sign(before) != Mathf.Sign(after);
        }
    }
}
