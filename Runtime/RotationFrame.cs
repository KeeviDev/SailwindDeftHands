using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Gives the frame big items are kept level and facing in: the boat the player is on, or the world while the
    /// player isn't on a boat.
    /// </summary>
    internal static class RotationFrame
    {
        /// <summary>Gets the boat the player is on, or null while the player isn't on a boat.</summary>
        public static Transform Boat => GameState.currentBoat;

        /// <summary>Gets the frame's up direction in the visible world.</summary>
        public static Vector3 Up => RotationOf(Boat) * Vector3.up;

        /// <param name="boat">A boat, or null for the world.</param>
        public static Quaternion RotationOf(Transform boat)
        {
            return boat != null ? boat.rotation : Quaternion.identity;
        }

        /// <summary>Returns <paramref name="direction"/> projected onto the frame's horizontal plane, of unit length.</summary>
        public static Vector3 Flatten(Vector3 direction)
        {
            return Vector3.ProjectOnPlane(direction, Up).normalized;
        }
    }
}
