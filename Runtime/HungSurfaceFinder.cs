using DeftHands.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Finds the surface a hung item hangs on from the pose of its physics twin alone, by guessing at the planes around
    /// its pivot and looking for each straight below it.
    /// </summary>
    internal static class HungSurfaceFinder
    {
        /// <summary>Metres beyond the deepest an item can hang within which its surface is looked for.</summary>
        private const float SearchMargin = 0.05f;

        /// <summary>
        /// Metres on each side of the item's pivot that the rays guessing at its surface reach along their line.
        /// </summary>
        private const float GuessReach = 1.5f;

        /// <summary>
        /// Degrees by which one surface must fit the item's facing better than another to win over a nearer one.
        /// </summary>
        private const float FacingTieAngle = 2f;

        /// <summary>
        /// Metres a hung item may lie beyond the deepest an item can hang and still count as hanging there.
        /// </summary>
        private const float DepthTolerance = 0.01f;

        /// <summary>Largest angle, in degrees, between two surface normals that count as the same surface.</summary>
        private const float SameSurfaceAngle = 2f;

        /// <summary>
        /// Largest angle, in degrees, between an item's forward axis and the inward normal of a surface at which it
        /// counts as hanging there untilted.
        /// </summary>
        private const float UntiltedAngle = 0.5f;

        /// <summary>Metres two planes with the same normal may lie apart and still count as one.</summary>
        private const float SamePlaneDistance = 0.01f;

        /// <summary>Metres a ray steps past a surface it hit before looking for the next one.</summary>
        private const float StepPastSurface = 0.001f;

        private const int MaxSurfacesPerRay = 8;

        private static readonly RaycastHit[] Hits = new RaycastHit[64];

        private static readonly int SurfaceLayers = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player");

        /// <summary>
        /// The lines through the item's pivot along which surfaces are guessed at besides its own forward axis and the
        /// view: the axes of attachment space and the diagonals of its unit cube's faces and of the cube itself.
        /// </summary>
        private static readonly Vector3[] GuessLines =
        {
            Vector3.right, Vector3.up, Vector3.forward,
            new Vector3(1f, 1f, 0f).normalized, new Vector3(1f, -1f, 0f).normalized,
            new Vector3(1f, 0f, 1f).normalized, new Vector3(1f, 0f, -1f).normalized,
            new Vector3(0f, 1f, 1f).normalized, new Vector3(0f, 1f, -1f).normalized,
            new Vector3(1f, 1f, 1f).normalized, new Vector3(1f, 1f, -1f).normalized,
            new Vector3(1f, -1f, 1f).normalized, new Vector3(1f, -1f, -1f).normalized
        };

        /// <summary>The item being taken off and the items riding in its box, which are no surface.</summary>
        private static readonly ItemParts OwnParts = new ItemParts();

        /// <summary>
        /// The planes already looked for below the item's pivot, each as its normal and its distance from the origin
        /// along it.
        /// </summary>
        private static readonly List<Vector4> ConsideredPlanes = new List<Vector4>();

        /// <summary>The surface found so far for the item being taken off, and how well it fits.</summary>
        private static Surface best;

        /// <summary>
        /// Gets the point on the surface that <paramref name="item"/> hangs on lying straight below the pivot of its
        /// physics twin <paramref name="twin"/>, and that surface's normal, and returns whether one was found. The
        /// surface faces <paramref name="viewPoint"/>, and of several, the one the item's forward axis points into most
        /// squarely wins, then the nearest.
        /// </summary>
        public static bool TryFind(
            ShipItem item,
            Transform twin,
            Vector3 viewPoint,
            out Vector3 point,
            out Vector3 normal)
        {
            OwnParts.CollectWithRiders(item);
            Vector3 toPivot = twin.position - viewPoint;
            best = default;
            ConsideredPlanes.Clear();

            SearchLine(twin, viewPoint, twin.forward);
            if (!best.IsUntilted && toPivot.sqrMagnitude > VectorUtils.MinDirectionSqrMagnitude)
                SearchAlong(twin, viewPoint, new Ray(viewPoint, toPivot.normalized), toPivot.magnitude + GuessReach);

            for (int line = 0; line < GuessLines.Length && !best.IsUntilted; line++)
                SearchLine(twin, viewPoint, GuessLines[line]);

            ConsideredPlanes.Clear();
            OwnParts.Clear();
            point = best.Point;
            normal = best.Normal;
            return best.IsFound;
        }

        /// <summary>
        /// Guesses at the item's surface along the line through its pivot along <paramref name="direction"/>, from both
        /// ends, so that the surfaces facing either way are met.
        /// </summary>
        private static void SearchLine(Transform twin, Vector3 viewPoint, Vector3 direction)
        {
            Vector3 pivot = twin.position;
            SearchAlong(twin, viewPoint, new Ray(pivot - direction * GuessReach, direction), 2f * GuessReach);
            SearchAlong(twin, viewPoint, new Ray(pivot + direction * GuessReach, -direction), 2f * GuessReach);
        }

        /// <summary>
        /// Takes each surface <paramref name="ray"/> meets within <paramref name="length"/> as a guess at the plane the
        /// item hangs on, and looks for each plane not looked for yet straight below the item's pivot, until the item
        /// is found hanging on one untilted.
        /// </summary>
        private static void SearchAlong(Transform twin, Vector3 viewPoint, Ray ray, float length)
        {
            for (int surface = 0; surface < MaxSurfacesPerRay && !best.IsUntilted; surface++)
            {
                if (!TryRaycast(ray, length, out RaycastHit hit))
                    return;

                if (MarkConsidered(hit.point, hit.normal))
                    ConsiderPlane(twin, viewPoint, hit.normal);

                float step = hit.distance + StepPastSurface;
                ray.origin += ray.direction * step;
                length -= step;
            }
        }

        /// <summary>
        /// Looks, from the side <paramref name="normal"/> faces, for a surface with that normal straight below the
        /// item's pivot, and keeps it when it fits better than the surface found so far.
        /// </summary>
        private static void ConsiderPlane(Transform twin, Vector3 viewPoint, Vector3 normal)
        {
            float reach = WallAttachmentAdjustment.MaxDepth + SearchMargin;
            Vector3 pivot = twin.position;
            var ray = new Ray(pivot + normal * reach, -normal);
            float length = 2f * reach;
            for (int surface = 0; surface < MaxSurfacesPerRay; surface++)
            {
                if (!TryRaycast(ray, length, out RaycastHit hit))
                    return;

                if (Vector3.Angle(hit.normal, normal) <= SameSurfaceAngle)
                {
                    Keep(new Surface(hit.point, hit.normal, pivot, twin.forward), viewPoint);
                    return;
                }

                float step = hit.distance + StepPastSurface;
                ray.origin += ray.direction * step;
                length -= step;
            }
        }

        /// <summary>
        /// Records the plane through <paramref name="point"/> facing <paramref name="normal"/> as looked for, and
        /// returns whether it was not looked for before.
        /// </summary>
        private static bool MarkConsidered(Vector3 point, Vector3 normal)
        {
            float distance = Vector3.Dot(point, normal);
            foreach (Vector4 plane in ConsideredPlanes)
            {
                var planeNormal = new Vector3(plane.x, plane.y, plane.z);
                if (Vector3.Angle(planeNormal, normal) <= SameSurfaceAngle
                    && Mathf.Abs(plane.w - distance) <= SamePlaneDistance)
                    return false;
            }

            ConsideredPlanes.Add(new Vector4(normal.x, normal.y, normal.z, distance));
            return true;
        }

        private static void Keep(Surface candidate, Vector3 viewPoint)
        {
            if (Mathf.Abs(candidate.Depth) > WallAttachmentAdjustment.MaxDepth + DepthTolerance
                || Vector3.Dot(candidate.Normal, viewPoint - candidate.Point) <= 0f)
                return;

            bool facesBetter = candidate.FacingAngle < best.FacingAngle - FacingTieAngle;
            bool facesAlike = Mathf.Abs(candidate.FacingAngle - best.FacingAngle) <= FacingTieAngle;
            if (!best.IsFound || facesBetter || (facesAlike && Mathf.Abs(candidate.Depth) < Mathf.Abs(best.Depth)))
                best = candidate;
        }

        /// <summary>
        /// Gets the nearest solid collider <paramref name="ray"/> meets within <paramref name="length"/> that is none
        /// of <see cref="OwnParts"/>, and returns whether there is one.
        /// </summary>
        private static bool TryRaycast(Ray ray, float length, out RaycastHit nearest)
        {
            nearest = default;
            if (length <= 0f)
                return false;

            bool found = false;
            int count = Physics.RaycastNonAlloc(ray, Hits, length, SurfaceLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if ((found && Hits[i].distance >= nearest.distance) || OwnParts.Contains(Hits[i].transform))
                    continue;

                nearest = Hits[i];
                found = true;
            }

            return found;
        }

        private readonly struct Surface
        {
            public Surface(Vector3 point, Vector3 normal, Vector3 pivot, Vector3 forward)
            {
                Point = point;
                Normal = normal;
                Depth = Vector3.Dot(point - pivot, normal);
                FacingAngle = Vector3.Angle(forward, -normal);
                IsFound = true;
            }

            public Vector3 Point { get; }

            public Vector3 Normal { get; }

            /// <summary>
            /// Gets how far the pivot lies into the surface, in metres; negative when it lies out from it.
            /// </summary>
            public float Depth { get; }

            /// <summary>Gets the degrees between the item's forward axis and the surface's inward normal.</summary>
            public float FacingAngle { get; }

            public bool IsUntilted => IsFound && FacingAngle <= UntiltedAngle;

            public bool IsFound { get; }
        }
    }
}
