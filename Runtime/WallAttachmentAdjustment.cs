using DeftHands.Configuration;
using DeftHands.Utils;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Keeps the rotation the player gives the held wall-attachable item on its surface while
    /// <see cref="WallPlacement.TurnsOnWall"/>, and the depth it hangs at while it
    /// <see cref="WallPlacement.IsBeingHung"/>, and applies them to the pose it attaches in. Every rotation turns the
    /// item around an axis of the surface, its normal or the axes running up and across it, through a dead zone where
    /// the item is upright or flat on that axis.
    /// </summary>
    internal static class WallAttachmentAdjustment
    {
        /// <summary>Degrees of input the item stays upright or flat on an axis for while turning through it.</summary>
        private const float DeadzoneDegrees = 18f;

        /// <summary>Farthest the item hangs into or out from its surface, in metres.</summary>
        public const float MaxDepth = 0.8f;

        /// <summary>Smallest depth, in metres, read from a hung item that counts as hanging in or out.</summary>
        private const float MinReadDepth = 0.0005f;

        /// <summary>Metres the item moves into its surface per unit of the holder's scroll input.</summary>
        private const float DepthPerScrollInput = 0.002f;

        private const int TiltAxis = 0;

        private const int TurnAxis = 1;

        private const int RollAxis = 2;

        /// <summary>The axes of the surface frame: across the surface, up it, and into it.</summary>
        private static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };

        /// <summary>The dead zone at upright or flat of each axis.</summary>
        private static readonly Deadzone[] Deadzones =
        {
            new Deadzone(DeadzoneDegrees), new Deadzone(DeadzoneDegrees), new Deadzone(DeadzoneDegrees)
        };

        private static ShipItem currentItem;

        /// <summary>The item's rotation relative to the frame of its surface.</summary>
        private static Quaternion orientation = Quaternion.identity;

        /// <summary>
        /// How far the item hangs into its surface, in metres; a negative depth hangs it out from it.
        /// </summary>
        private static float depth;

        /// <summary>
        /// The frame in which <see cref="TryOrientAttachment"/> last turned the held item's attachment pose.
        /// </summary>
        private static int orientedFrame = -1;

        /// <summary>
        /// The frame in which <see cref="TryDeepenAttachment"/> last moved the held item's attachment pose.
        /// </summary>
        private static int deepenedFrame = -1;

        /// <summary>Gets the held item's rotation relative to the frame of its surface.</summary>
        public static Quaternion Orientation => orientation;

        /// <summary>
        /// Gets how far the held item hangs into its surface, in metres; a negative depth hangs it out from it.
        /// </summary>
        public static float Depth => depth;

        /// <summary>
        /// Registers the currently held item and starts it upright and flat against its surface at no depth.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        public static void SetCurrentItem(PickupableItem item)
        {
            currentItem = item as ShipItem;
            Reset();
        }

        /// <summary>
        /// Returns whether mouse movement tilts the held <paramref name="item"/>: Wall Attachment Tilt is on, the axis
        /// swap key is held and <see cref="WallPlacement.TurnsOnWall"/>.
        /// </summary>
        public static bool TakesTiltInput(PickupableItem item)
        {
            return ModConfig.WallAttachmentTilt.Value && ModInput.IsAxisSwapKeyHeld()
                && WallPlacement.TurnsOnWall(item);
        }

        /// <summary>
        /// Returns whether the scroll wheel sets how deep the held <paramref name="item"/> hangs: Wall Attachment Depth
        /// is on, the rotation modifier key is held and the item <see cref="WallPlacement.IsBeingHung"/>.
        /// </summary>
        public static bool TakesDepthInput(PickupableItem item)
        {
            return ModConfig.WallAttachmentDepth.Value
                && ModInput.IsRotationModifierKeyHeld()
                && WallPlacement.IsBeingHung(item);
        }

        /// <param name="degrees">Degrees to roll the item around its surface's normal; positive turns counterclockwise
        /// as seen facing the surface.</param>
        public static void AddRoll(float degrees)
        {
            Rotate(RollAxis, degrees);
        }

        /// <param name="turnDegrees">Degrees to turn the item around the axis that runs up its surface.</param>
        /// <param name="tiltDegrees">Degrees to tilt the item around the axis that runs across its surface.</param>
        public static void AddTilt(float turnDegrees, float tiltDegrees)
        {
            Rotate(TurnAxis, turnDegrees);
            Rotate(TiltAxis, tiltDegrees);
        }

        /// <summary>
        /// Moves the item deeper into its surface by the holder's scroll <paramref name="input"/>, or out from it for a
        /// negative input, within the depth limits.
        /// </summary>
        public static void AddScrollInput(float input)
        {
            depth = Mathf.Clamp(depth + input * DepthPerScrollInput, -MaxDepth, MaxDepth);
        }

        /// <summary>
        /// Gives the held item the rotation relative to its surface's frame and the depth into it, in metres, it hangs
        /// with, keeping only the roll while Wall Attachment Tilt is off.
        /// </summary>
        public static void RestorePose(Quaternion rotationOnSurface, float depthMetres)
        {
            orientation = Quaternion.Normalize(rotationOnSurface);
            if (!ModConfig.WallAttachmentTilt.Value)
                orientation = Quaternion.AngleAxis(MeasureOrZero(RollAxis), Vector3.forward);

            depth = Mathf.Abs(depthMetres) < MinReadDepth ? 0f : Mathf.Clamp(depthMetres, -MaxDepth, MaxDepth);
            RestartDeadzones();
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> is the held item this rotation and depth belong to.
        /// </summary>
        public static bool IsTracking(ShipItem item)
        {
            return item != null && item == currentItem;
        }

        /// <summary>
        /// Returns whether <see cref="TryOrientAttachment"/> turned the attachment pose of <paramref name="item"/> in
        /// this frame.
        /// </summary>
        public static bool IsOrientedThisFrame(ShipItem item)
        {
            return IsTracking(item) && orientedFrame == Time.frameCount;
        }

        /// <summary>
        /// Returns whether <see cref="TryDeepenAttachment"/> moved the attachment pose of <paramref name="item"/> in
        /// this frame.
        /// </summary>
        public static bool IsDeepenedThisFrame(ShipItem item)
        {
            return IsTracking(item) && deepenedFrame == Time.frameCount;
        }

        /// <summary>Turns the item back upright and flat against its surface at no depth.</summary>
        public static void Reset()
        {
            orientation = Quaternion.identity;
            depth = 0f;
            RestartDeadzones();
        }

        /// <summary>
        /// Drops input left inside the dead zones, so the next rotation starts from their centre.
        /// </summary>
        public static void RecenterDeadzone()
        {
            foreach (Deadzone deadzone in Deadzones)
                deadzone.Recenter();
        }

        /// <summary>
        /// Turns <paramref name="attachRot"/>, the rotation the game computed for attaching <paramref name="item"/>, in
        /// <see cref="AttachmentSpace"/>, to the rotation the player gave it on that surface, and returns whether it
        /// did. The surface's frame looks into it, with its up running up a wall, or its right running along the
        /// player's view right across a ceiling or floor.
        /// </summary>
        public static bool TryOrientAttachment(ShipItem item, ref Quaternion attachRot)
        {
            if (item != currentItem || !WallPlacement.TurnsOnWall(item))
                return false;

            attachRot = GetSurfaceFrame(item, attachRot * Vector3.forward) * orientation;
            orientedFrame = Time.frameCount;
            return true;
        }

        /// <summary>
        /// Moves <paramref name="attachPos"/>, where the game attaches <paramref name="item"/> in
        /// <see cref="AttachmentSpace"/>, to the depth the player gave it, while Wall Attachment Depth is on and the
        /// item <see cref="WallPlacement.IsBeingHung"/>, and returns whether it moved.
        /// </summary>
        /// <param name="intoSurface">The unit direction into the surface the item attaches to.</param>
        public static bool TryDeepenAttachment(ShipItem item, Vector3 intoSurface, ref Vector3 attachPos)
        {
            if (item != currentItem
                || depth == 0f
                || !ModConfig.WallAttachmentDepth.Value
                || !WallPlacement.IsBeingHung(item))
                return false;

            attachPos += intoSurface * depth;
            deepenedFrame = Time.frameCount;
            return true;
        }

        /// <summary>
        /// Returns the rotation facing <paramref name="intoSurface"/> whose up runs up a wall, or whose right is the
        /// player's view right across a ceiling or floor.
        /// </summary>
        public static Quaternion GetSurfaceFrame(ShipItem item, Vector3 intoSurface)
        {
            if (WallAttachmentRoll.FacesWall(intoSurface, Vector3.up))
                return Quaternion.LookRotation(intoSurface, Vector3.up);

            Camera camera = Camera.main;
            Vector3 viewRight = camera != null
                ? AttachmentSpace.ToAttachmentSpace(item, camera.transform.rotation) * Vector3.right
                : Vector3.right;
            Vector3 right = Vector3.ProjectOnPlane(viewRight, intoSurface);
            return right.sqrMagnitude < VectorUtils.MinDirectionSqrMagnitude
                ? Quaternion.FromToRotation(Vector3.forward, intoSurface)
                : Quaternion.LookRotation(intoSurface, Vector3.Cross(intoSurface, right.normalized));
        }

        /// <summary>
        /// Turns the item by <paramref name="degrees"/> of input around the surface axis <paramref name="axis"/>,
        /// holding it at rest on that axis while the turn crosses the axis's dead zone.
        /// </summary>
        private static void Rotate(int axis, float degrees)
        {
            Deadzone deadzone = Deadzones[axis];
            float turn = deadzone.Take(degrees, out bool leftRest);
            if (turn == 0f)
                return;

            float before = Measure(axis);
            TurnAround(axis, turn);
            if (leftRest)
                return;

            float correction = deadzone.Settle(before, Measure(axis));
            if (correction != 0f)
                TurnAround(axis, correction);
        }

        private static void TurnAround(int axis, float degrees)
        {
            orientation = Quaternion.Normalize(Quaternion.AngleAxis(degrees, Axes[axis]) * orientation);
        }

        private static void RestartDeadzones()
        {
            for (int axis = 0; axis < Axes.Length; axis++)
                Deadzones[axis].Restart(Deadzone.IsAtRest(Measure(axis)));
        }

        /// <summary>
        /// Returns how far the item is turned from upright or flat around the surface axis <paramref name="axis"/>, in
        /// degrees, or NaN where the item's axis it is measured by lies along that surface axis.
        /// </summary>
        private static float Measure(int axis)
        {
            Vector3 surfaceAxis = Axes[axis];
            Vector3 reference = axis == RollAxis ? Vector3.up : Vector3.forward;
            Vector3 itemDirection = Vector3.ProjectOnPlane(orientation * reference, surfaceAxis);
            return itemDirection.sqrMagnitude < VectorUtils.MinDirectionSqrMagnitude
                ? float.NaN
                : Vector3.SignedAngle(reference, itemDirection, surfaceAxis);
        }

        private static float MeasureOrZero(int axis)
        {
            float angle = Measure(axis);
            return float.IsNaN(angle) ? 0f : angle;
        }
    }
}
