using UnityEngine;

namespace DeftHands.Utils
{
    internal static class VectorUtils
    {
        /// <summary>The smallest squared length a vector can have and still give a direction.</summary>
        public const float MinDirectionSqrMagnitude = 1e-6f;

        /// <summary>
        /// Returns the sign, -1 or 1, on each axis of the corner numbered <paramref name="index"/>, from 0 to 7, of a
        /// box centred on the origin.
        /// </summary>
        public static Vector3 CornerSign(int index)
        {
            return new Vector3(AxisSign(index, 1), AxisSign(index, 2), AxisSign(index, 4));
        }

        private static float AxisSign(int index, int bit)
        {
            return (index & bit) == 0 ? -1f : 1f;
        }
    }
}
