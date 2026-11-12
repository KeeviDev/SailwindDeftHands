using DeftHands.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Holds the solid shape of an item as its physics twin collides, and finds where the item rests against a surface,
    /// how it rests there by default and how it gets clear of other colliders.
    /// </summary>
    internal sealed class ItemShape
    {
        /// <summary>Gap left between the item and what it rests against or is moved away from, in metres.</summary>
        private const float Gap = 0.002f;

        /// <summary>Distance beyond the item's bounds within which other colliders are checked, in metres.</summary>
        private const float NearbyMargin = 0.05f;

        /// <summary>
        /// Depth, in metres, the item may reach into other colliders and still count as clear of them.
        /// </summary>
        private const float ClearDepth = 0.003f;

        /// <summary>
        /// Depth, in metres, the item may reach into other colliders it can't be moved out of and still be put down.
        /// </summary>
        private const float AllowedOverlap = 0.02f;

        /// <summary>Highest the item is lifted onto what it overlaps, in metres.</summary>
        private const float MaxLift = 0.2f;

        /// <summary>Farthest the item is slid out of what it overlaps, in metres.</summary>
        private const float MaxSlide = 0.09f;

        /// <summary>Farthest the item is lowered onto something to rest on, in metres.</summary>
        private const float MaxDrop = 0.6f;

        private const int SearchSteps = 8;

        private const int SlideSteps = 4;

        /// <summary>
        /// Largest ratio of the item's thinnest size to its middle size at which it counts as flat and rests on its
        /// widest side.
        /// </summary>
        private const float FlatRatio = 0.65f;

        private static readonly Collider[] Nearby = new Collider[64];

        private static readonly int SolidLayers = ~LayerMask.GetMask("Player", "UI");

        private readonly ShipItem item;

        private readonly Transform twin;

        private readonly GameObject copyRoot;

        private readonly Collider[] copies;

        /// <summary>The pose of each of <see cref="copies"/> relative to the item.</summary>
        private readonly Pose[] posesInItem;

        /// <summary>The middle of the box around the colliders, in the item's space.</summary>
        private readonly Vector3 middle;

        /// <summary>Half the size of the box around the colliders, in the item's space.</summary>
        private readonly Vector3 extents;

        /// <summary>The distance from <see cref="middle"/> to the corners of the box around the colliders.</summary>
        private readonly float reach;

        private ItemShape(
            ShipItem item,
            Transform twin,
            GameObject copyRoot,
            List<Collider> copies,
            List<Pose> posesInItem,
            Bounds box)
        {
            this.item = item;
            this.twin = twin;
            this.copyRoot = copyRoot;
            this.copies = copies.ToArray();
            this.posesInItem = posesInItem.ToArray();
            middle = box.center;
            extents = box.extents;
            reach = box.extents.magnitude;
            RestingRotation = GetRestingRotation(box.size);
        }

        /// <summary>
        /// Gets the item's rotation, relative to upright and facing away from the player, in which it rests on a
        /// surface: upright, or on its widest side when it is flat.
        /// </summary>
        public Quaternion RestingRotation { get; }

        /// <summary>Gets whether the item is flat and rests on its widest side.</summary>
        public bool IsFlat => RestingRotation != Quaternion.identity;

        /// <summary>
        /// Gets the direction, in the item's space, that points into the surface it rests on in its resting rotation.
        /// </summary>
        public Vector3 RestingDown => Quaternion.Inverse(RestingRotation) * Vector3.down;

        /// <summary>
        /// Measures the shape of <paramref name="item"/> at its full size into hidden colliders under
        /// <paramref name="parent"/>, or returns null when its physics twin has no collider to measure. The colliders
        /// stay until <see cref="Discard"/>.
        /// </summary>
        public static ItemShape Measure(ShipItem item, Transform parent)
        {
            ItemRigidbody body = item.GetItemRigidbody();
            if (body == null)
                return null;

            Transform twin = body.transform;
            var copyRoot = new GameObject("DeftHands_ItemShape");
            copyRoot.layer = parent.gameObject.layer;
            copyRoot.transform.SetParent(parent, false);
            var copies = new List<Collider>();
            var poses = new List<Pose>();
            Bounds box = default;
            foreach (Collider collider in twin.GetComponentsInChildren<Collider>(true))
            {
                Matrix4x4 inItem = GetMatrixInTwin(collider.transform, twin);
                Vector3 scale = inItem.lossyScale;
                if (!IsUsable(scale) || scale.x * scale.y * scale.z == 0f)
                    continue;

                var pose = new Pose(inItem.GetColumn(3), inItem.rotation);
                if (!IsUsable(pose.position) || !IsUsable(pose.rotation * Vector3.one))
                    continue;

                Collider copy = CopyShape(collider, copyRoot.transform, scale);
                if (copy == null)
                    continue;

                AddCorners(ref box, copies.Count == 0, pose, scale, GetLocalBounds(copy));
                copies.Add(copy);
                poses.Add(pose);
            }

            if (copies.Count == 0 || !IsUsable(box.center) || !IsUsable(box.size))
            {
                Object.Destroy(copyRoot);
                return null;
            }

            return new ItemShape(item, twin, copyRoot, copies, poses, box);
        }

        /// <summary>Returns whether every component of <paramref name="value"/> is a finite number.</summary>
        public static bool IsUsable(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
                && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }

        /// <summary>
        /// Returns the middle of the item in <paramref name="pose"/>, in the space the pose is given in.
        /// </summary>
        public Vector3 GetMiddle(Pose pose)
        {
            return pose.position + pose.rotation * middle;
        }

        /// <summary>
        /// Returns how far the item, turned to <paramref name="rotation"/>, reaches from its middle along
        /// <paramref name="direction"/>, a unit vector.
        /// </summary>
        public float GetReachAlong(Quaternion rotation, Vector3 direction)
        {
            Vector3 inItem = Quaternion.Inverse(rotation) * direction;
            return Mathf.Abs(inItem.x) * extents.x + Mathf.Abs(inItem.y) * extents.y + Mathf.Abs(inItem.z) * extents.z;
        }

        /// <summary>Destroys the hidden colliders the shape was measured into.</summary>
        public void Discard()
        {
            if (copyRoot != null)
                Object.Destroy(copyRoot);
        }

        /// <summary>
        /// Gets the position at which the item, turned to <paramref name="rotation"/>, rests against the plane through
        /// <paramref name="point"/> facing <paramref name="normal"/>, with its middle on the line through that point
        /// along the normal, and returns whether it could be worked out.
        /// </summary>
        /// <param name="ground">
        /// A box collider whose top face lies at its transform's origin, wider and deeper than any item.
        /// </param>
        public bool TryRestAgainst(
            BoxCollider ground,
            Vector3 point,
            Vector3 normal,
            Quaternion rotation,
            out Vector3 position)
        {
            Vector3 sunk = point - rotation * middle - normal * reach;
            Quaternion groundRotation = Quaternion.FromToRotation(Vector3.up, normal);
            float rise = 0f;
            bool isMeasured = false;
            for (int i = 0; i < copies.Length; i++)
            {
                Pose part = PlacePart(i, new Pose(sunk, rotation));
                if (!Physics.ComputePenetration(copies[i], part.position, part.rotation, ground, point, groundRotation,
                        out Vector3 direction, out float depth))
                    continue;

                rise = Mathf.Max(rise, depth * Vector3.Dot(direction, normal));
                isMeasured = true;
            }

            position = sunk + normal * (rise + Gap);
            return isMeasured && IsUsable(position);
        }

        /// <summary>
        /// Lowers <paramref name="pose"/>, given in the space the item's physics twin moves in, against
        /// <paramref name="up"/> onto the first solid collider within <see cref="MaxDrop"/> below it, and leaves it
        /// where it is when it already touches something or nothing lies within reach.
        /// </summary>
        public Pose DropOntoSupport(Pose pose, Vector3 up)
        {
            if (Overlaps(pose, ClearDepth) || !Overlaps(Shift(pose, -up * MaxDrop), ClearDepth))
                return pose;

            float clear = 0f;
            float blocked = MaxDrop;
            for (int step = 0; step < SearchSteps; step++)
            {
                float drop = (clear + blocked) / 2f;
                if (Overlaps(Shift(pose, -up * drop), ClearDepth))
                    blocked = drop;
                else
                    clear = drop;
            }

            return Shift(pose, -up * clear);
        }

        /// <summary>
        /// Moves <paramref name="pose"/>, given in the space the item's physics twin moves in, out of every solid
        /// collider but the item's own: slides it across <paramref name="up"/> by at most <see cref="MaxSlide"/> to
        /// where it reaches least into them, and when it still reaches deeper than <see cref="AllowedOverlap"/>, lifts
        /// it along <paramref name="up"/> clear of them instead, by at most <see cref="MaxLift"/>. Returns whether it
        /// reaches no deeper than <see cref="AllowedOverlap"/> into them there.
        /// </summary>
        public bool TrySettle(ref Pose pose, Vector3 up)
        {
            float depth = GetDeepestOverlap(pose);
            if (depth <= ClearDepth)
                return true;

            Pose slid = Slide(pose, up);
            float slidDepth = GetDeepestOverlap(slid);
            Pose start = pose;
            if (slidDepth < depth)
            {
                pose = slid;
                depth = slidDepth;
            }

            if (depth <= AllowedOverlap)
                return true;

            if (!TryLift(start, up, out Pose lifted))
                return false;

            pose = lifted;
            return true;
        }

        private bool TryLift(Pose pose, Vector3 up, out Pose lifted)
        {
            lifted = pose;
            if (Overlaps(Shift(pose, up * MaxLift), ClearDepth))
                return false;

            float blocked = 0f;
            float clear = MaxLift;
            for (int step = 0; step < SearchSteps; step++)
            {
                float height = (blocked + clear) / 2f;
                if (Overlaps(Shift(pose, up * height), ClearDepth))
                    blocked = height;
                else
                    clear = height;
            }

            lifted = Shift(pose, up * (clear + Gap));
            return true;
        }

        /// <summary>
        /// Returns <paramref name="pose"/> slid across <paramref name="up"/> out of what it reaches into, by at most
        /// <see cref="MaxSlide"/>.
        /// </summary>
        private Pose Slide(Pose pose, Vector3 up)
        {
            Vector3 offset = Vector3.zero;
            for (int step = 0; step < SlideSteps; step++)
            {
                Vector3 push = Vector3.ProjectOnPlane(GetPushOut(Shift(pose, offset)), up);
                if (push.sqrMagnitude < 1e-10f)
                    break;

                offset = Vector3.ClampMagnitude(offset + push + push.normalized * Gap, MaxSlide);
            }

            return Shift(pose, offset);
        }

        /// <summary>
        /// Returns how deep the item in <paramref name="pose"/>, given in the space its physics twin moves in, reaches
        /// into the solid collider it reaches deepest into, its own left out, or 0 when it reaches into none.
        /// </summary>
        private float GetDeepestOverlap(Pose pose)
        {
            float deepest = 0f;
            int count = FindNearby(pose);
            for (int n = 0; n < count; n++)
            {
                Collider other = Nearby[n];
                Transform otherTransform = other.transform;
                for (int i = 0; i < copies.Length; i++)
                {
                    Pose part = PlacePart(i, pose);
                    if (Physics.ComputePenetration(copies[i], part.position, part.rotation, other,
                            otherTransform.position, otherTransform.rotation, out _, out float depth))
                        deepest = Mathf.Max(deepest, depth);
                }
            }

            return deepest;
        }

        /// <summary>
        /// Returns whether the item in <paramref name="pose"/>, given in the space its physics twin moves in, reaches
        /// deeper than <paramref name="allowedDepth"/> into any solid collider but its own.
        /// </summary>
        private bool Overlaps(Pose pose, float allowedDepth)
        {
            int count = FindNearby(pose);
            for (int n = 0; n < count; n++)
            {
                Collider other = Nearby[n];
                Transform otherTransform = other.transform;
                for (int i = 0; i < copies.Length; i++)
                {
                    Pose part = PlacePart(i, pose);
                    if (Physics.ComputePenetration(copies[i], part.position, part.rotation, other,
                            otherTransform.position, otherTransform.rotation, out _, out float depth)
                        && depth > allowedDepth)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the sum of the moves that take the item in <paramref name="pose"/> out of each solid collider it
        /// reaches into, but its own.
        /// </summary>
        private Vector3 GetPushOut(Pose pose)
        {
            Vector3 push = Vector3.zero;
            int count = FindNearby(pose);
            for (int n = 0; n < count; n++)
            {
                Collider other = Nearby[n];
                Transform otherTransform = other.transform;
                for (int i = 0; i < copies.Length; i++)
                {
                    Pose part = PlacePart(i, pose);
                    if (Physics.ComputePenetration(copies[i], part.position, part.rotation, other,
                            otherTransform.position, otherTransform.rotation, out Vector3 direction, out float depth))
                        push += direction * depth;
                }
            }

            return push;
        }

        /// <summary>
        /// Fills <see cref="Nearby"/> with the solid colliders around the item in <paramref name="pose"/>, leaving out
        /// its own, and returns how many there are.
        /// </summary>
        private int FindNearby(Pose pose)
        {
            Vector3 centre = pose.position + pose.rotation * middle;
            int found = Physics.OverlapSphereNonAlloc(
                centre, reach + NearbyMargin, Nearby, SolidLayers, QueryTriggerInteraction.Ignore);
            int kept = 0;
            for (int n = 0; n < found; n++)
            {
                if (!IsOwn(Nearby[n]))
                    Nearby[kept++] = Nearby[n];
            }

            return kept;
        }

        private Pose PlacePart(int index, Pose pose)
        {
            Pose inItem = posesInItem[index];
            return new Pose(pose.position + pose.rotation * inItem.position, pose.rotation * inItem.rotation);
        }

        private static Pose Shift(Pose pose, Vector3 offset)
        {
            return new Pose(pose.position + offset, pose.rotation);
        }

        private bool IsOwn(Collider collider)
        {
            Transform part = collider.transform;
            return (twin != null && part.IsChildOf(twin)) || (item != null && part.IsChildOf(item.transform))
                || (copyRoot != null && part.IsChildOf(copyRoot.transform));
        }

        /// <summary>
        /// Returns the matrix that takes points from the space of <paramref name="part"/> to the space of its ancestor
        /// <paramref name="twin"/> at the twin's full size.
        /// </summary>
        private static Matrix4x4 GetMatrixInTwin(Transform part, Transform twin)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            for (Transform current = part; current != null && current != twin; current = current.parent)
                matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
            return matrix;
        }

        /// <summary>
        /// Returns the rotation that rests an item whose colliders span <paramref name="size"/> on its widest side when
        /// it is flat, and keeps it upright otherwise.
        /// </summary>
        private static Quaternion GetRestingRotation(Vector3 size)
        {
            float thinnest = Mathf.Min(size.x, size.y, size.z);
            float widest = Mathf.Max(size.x, size.y, size.z);
            float middleSize = size.x + size.y + size.z - thinnest - widest;
            if (thinnest >= FlatRatio * middleSize || size.y <= thinnest)
                return Quaternion.identity;

            return size.z <= thinnest ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, 0f, 90f);
        }

        /// <summary>
        /// Adds a hidden trigger collider with the shape of <paramref name="source"/> at <paramref name="scale"/> under
        /// <paramref name="parent"/> and returns it, or null for a shape it can't copy. A mesh that can't be read is
        /// copied as the box around it.
        /// </summary>
        private static Collider CopyShape(Collider source, Transform parent, Vector3 scale)
        {
            var copyObject = new GameObject(source.name);
            copyObject.layer = parent.gameObject.layer;
            copyObject.transform.SetParent(parent, false);
            copyObject.transform.localScale = scale;
            Collider copy = AddShape(copyObject, source);
            if (copy == null)
            {
                Object.Destroy(copyObject);
                return null;
            }

            copy.isTrigger = true;
            return copy;
        }

        private static Collider AddShape(GameObject target, Collider source)
        {
            switch (source)
            {
                case BoxCollider box:
                    return AddBox(target, box.center, box.size);
                case SphereCollider sphere:
                    SphereCollider sphereCopy = target.AddComponent<SphereCollider>();
                    sphereCopy.center = sphere.center;
                    sphereCopy.radius = sphere.radius;
                    return sphereCopy;
                case CapsuleCollider capsule:
                    CapsuleCollider capsuleCopy = target.AddComponent<CapsuleCollider>();
                    capsuleCopy.center = capsule.center;
                    capsuleCopy.radius = capsule.radius;
                    capsuleCopy.height = capsule.height;
                    capsuleCopy.direction = capsule.direction;
                    return capsuleCopy;
                case MeshCollider mesh when mesh.sharedMesh != null && mesh.sharedMesh.isReadable:
                    MeshCollider meshCopy = target.AddComponent<MeshCollider>();
                    meshCopy.convex = true;
                    meshCopy.sharedMesh = mesh.sharedMesh;
                    return meshCopy;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    Bounds bounds = mesh.sharedMesh.bounds;
                    return AddBox(target, bounds.center, bounds.size);
                default:
                    return null;
            }
        }

        private static BoxCollider AddBox(GameObject target, Vector3 center, Vector3 size)
        {
            BoxCollider box = target.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            return box;
        }

        /// <summary>Returns the box around <paramref name="collider"/> in its own transform's space.</summary>
        private static Bounds GetLocalBounds(Collider collider)
        {
            switch (collider)
            {
                case BoxCollider box:
                    return new Bounds(box.center, box.size);
                case SphereCollider sphere:
                    return new Bounds(sphere.center, Vector3.one * (sphere.radius * 2f));
                case CapsuleCollider capsule:
                    Vector3 size = Vector3.one * (capsule.radius * 2f);
                    size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                    return new Bounds(capsule.center, size);
                case MeshCollider mesh:
                    return mesh.sharedMesh.bounds;
                default:
                    return default;
            }
        }

        /// <summary>
        /// Grows <paramref name="box"/>, given in the item's space, to take in the corners of <paramref name="bounds"/>
        /// of a part in <paramref name="pose"/> at <paramref name="scale"/>, or makes it the box around them when
        /// <paramref name="isFirst"/>.
        /// </summary>
        private static void AddCorners(ref Bounds box, bool isFirst, Pose pose, Vector3 scale, Bounds bounds)
        {
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = bounds.center + Vector3.Scale(bounds.extents, VectorUtils.CornerSign(corner));
                Vector3 inItem = pose.position + pose.rotation * Vector3.Scale(scale, local);
                if (isFirst && corner == 0)
                    box = new Bounds(inItem, Vector3.zero);
                else
                    box.Encapsulate(inItem);
            }
        }
    }
}
