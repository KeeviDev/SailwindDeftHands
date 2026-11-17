using DeftHands.Configuration;
using DeftHands.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Draws the held wall-attachable item, with the items riding in its box, to the lower right of the main camera's
    /// view while it shows where it will attach, unless it is being placed on a table or other surface for items or the
    /// throw key is held. Their real poses are back in place as soon as that camera has rendered.
    /// </summary>
    public class HeldItemAside : MonoBehaviour
    {
        /// <summary>Seconds the item takes to move aside or back.</summary>
        private const float BlendTime = 0.2f;

        /// <summary>Gap between the preview's centre and the item's left edge, as a share of the view height.</summary>
        private const float SideGap = 0.15f;

        /// <summary>Gap between the preview's centre and the item's top edge, as a share of the view height.</summary>
        private const float TopGap = 0.17f;

        /// <summary>The least depth in front of the camera at which a point is put on the view.</summary>
        private const float MinViewDepth = 0.01f;

        /// <summary>Most vertices <see cref="vertices"/> keeps room for between measurements.</summary>
        private const int MaxKeptVertices = 4096;

        private static HeldItemAside instance;

        private ShipItem currentItem;

        /// <summary>Corners of the boxes around the item's meshes, in the item's own space.</summary>
        private readonly List<Vector3> meshCorners = new List<Vector3>();

        /// <summary>
        /// Whether <see cref="meshCorners"/> holds the current item's meshes as they were when it last began to move
        /// aside.
        /// </summary>
        private bool isMeasured;

        /// <summary>The mesh filters of the item being measured.</summary>
        private readonly List<MeshFilter> filters = new List<MeshFilter>();

        /// <summary>The vertices of the mesh being measured.</summary>
        private readonly List<Vector3> vertices = new List<Vector3>();

        /// <summary>How far the item is moved aside, from 0 (in the hand) to 1 (fully aside).</summary>
        private float blend;

        /// <summary>The item's rotation relative to its holder before the player tilts or rolls it.</summary>
        private Quaternion restingRotation;

        /// <summary>
        /// The distance along the holder's line of sight to the preview when the preview was last shown.
        /// </summary>
        private float previewDistance;

        /// <summary>Whether the item is drawn aside right now instead of having its real pose.</summary>
        private bool isDrawnAside;

        /// <summary>The camera the item is drawn aside for.</summary>
        private Camera drawingCamera;

        /// <summary>
        /// The rotation, relative to its holder, the item is drawn at while it is being placed: facing the player at
        /// its preview's roll.
        /// </summary>
        private Quaternion placingRotation;

        /// <summary>Whether the item is drawn turned towards <see cref="placingRotation"/>.</summary>
        private bool isTurnedForPlacing;

        private Vector3 realPosition;

        private Quaternion realRotation;

        /// <summary>The items riding in the current item's box, drawn aside with it.</summary>
        private readonly List<ShipItem> riders = new List<ShipItem>();

        /// <summary>Whether <see cref="riders"/> holds the items riding in the current item's box.</summary>
        private bool areRidersFound;

        /// <summary>The real pose of each of <see cref="riders"/> while they are drawn aside.</summary>
        private readonly List<Pose> realRiderPoses = new List<Pose>();

        public static HeldItemAside GetInstance()
        {
            if (instance == null)
                instance = PersistentObject.Create<HeldItemAside>();

            return instance;
        }

        /// <summary>
        /// Registers the currently held item; the previous item gets its real pose back.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        public void SetCurrentItem(PickupableItem item)
        {
            RestoreRealPose();
            currentItem = item is ShipItem shipItem && shipItem.wallAttachment ? shipItem : null;
            blend = 0f;
            previewDistance = 0f;
            isTurnedForPlacing = false;
            restingRotation = currentItem != null
                ? Quaternion.Euler(PickupableItemUtils.GetPrefabHeldRotationOffset(currentItem), 0f, 0f)
                : Quaternion.identity;
            isMeasured = false;
            areRidersFound = false;
        }

        private void OnEnable()
        {
            Camera.onPreCull += DrawAside;
            Camera.onPostRender += RestoreAfterRender;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= DrawAside;
            Camera.onPostRender -= RestoreAfterRender;
            RestoreRealPose();
        }

        private void Update()
        {
            RestoreRealPose();
            blend = SurfaceAim.IsAimedAtSurfaceForItems(currentItem) || GameInput.GetKey(InputName.Throw)
                ? 0f
                : Mathf.MoveTowards(blend, ShowsPreview() ? 1f : 0f, Time.deltaTime / BlendTime);
            if (blend > 0f)
                return;

            isTurnedForPlacing = false;
            areRidersFound = false;
            isMeasured = false;
        }

        private bool ShowsPreview()
        {
            return ModConfig.EasyWallHanging.Value && WallPlacement.ShowsPreview(currentItem);
        }

        /// <summary>
        /// Collects the corners of the boxes around the vertices of the current item's meshes that are drawn right now.
        /// </summary>
        private void MeasureMeshes()
        {
            meshCorners.Clear();
            isMeasured = true;
            if (currentItem == null)
                return;

            Transform item = currentItem.transform;
            currentItem.GetComponentsInChildren(filters);
            foreach (MeshFilter filter in filters)
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled)
                    continue;

                Matrix4x4 toItem = ToItemSpace(filter.transform, item);
                Bounds bounds = MeasureMesh(filter.sharedMesh);
                for (int index = 0; index < 8; index++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, VectorUtils.CornerSign(index));
                    meshCorners.Add(toItem.MultiplyPoint3x4(corner));
                }
            }

            filters.Clear();
            ReleaseLargeVertexBuffer();
        }

        /// <summary>
        /// Empties <see cref="vertices"/> and frees its room when it holds more than <see cref="MaxKeptVertices"/>.
        /// </summary>
        private void ReleaseLargeVertexBuffer()
        {
            if (vertices.Capacity <= MaxKeptVertices)
                return;

            vertices.Clear();
            vertices.TrimExcess();
        }

        /// <summary>
        /// Returns the box around the vertices of <paramref name="mesh"/>, or the bounds stored with it when its
        /// vertices can't be read.
        /// </summary>
        private Bounds MeasureMesh(Mesh mesh)
        {
            if (!mesh.isReadable)
                return mesh.bounds;

            mesh.GetVertices(vertices);
            if (vertices.Count == 0)
                return mesh.bounds;

            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 vertex in vertices)
                bounds.Encapsulate(vertex);
            return bounds;
        }

        /// <summary>
        /// Returns the matrix that takes points from the space of <paramref name="part"/> to the space of its ancestor
        /// <paramref name="item"/>.
        /// </summary>
        private static Matrix4x4 ToItemSpace(Transform part, Transform item)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            for (Transform current = part; current != item; current = current.parent)
                matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
            return matrix;
        }

        /// <summary>
        /// Moves the held item, with the items riding in its box, for the main camera, as far as <see cref="blend"/>
        /// has come, so that in its resting pose its upper left corner would sit below and right of the preview on the
        /// holder's line of sight. While <see cref="WallPlacement.TurnsOnWall"/>, it also turns the item to face the
        /// player at its preview's roll, and the resting pose faces the player without that roll.
        /// </summary>
        private void DrawAside(Camera camera)
        {
            if (blend <= 0f || isDrawnAside || !camera.CompareTag("MainCamera") || currentItem == null
                || currentItem.held == null)
                return;

            Transform view = camera.transform;
            Transform item = currentItem.transform;
            Vector3 pivot = view.InverseTransformPoint(item.position);
            if (pivot.z < MinViewDepth)
                return;

            Transform holder = currentItem.held.transform;
            if (WallPlacement.TurnsOnWall(currentItem))
            {
                placingRotation = Quaternion.Inverse(holder.rotation) * FacePlayerAtPreviewRoll(holder);
                isTurnedForPlacing = true;
            }

            Quaternion resting = isTurnedForPlacing ? holder.rotation : holder.rotation * restingRotation;
            Ray previewRay = WallAttachmentAim.LineOfSight(currentItem.held);
            if (ShowsPreview() && GoPointerTargeterFields.PreviewPosition != null)
                previewDistance = Vector3.Distance(
                    previewRay.origin, GoPointerTargeterFields.PreviewPosition(currentItem.held.GetTargeter()));

            Vector3 previewInView = view.InverseTransformPoint(previewRay.GetPoint(previewDistance));
            float viewHeight = 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            if (!isMeasured)
                MeasureMeshes();

            Rect itemOnView = MeasureOnView(view, item, resting, pivot);
            Vector2 anchor = previewInView.z >= MinViewDepth ? OnView(previewInView) : OnView(pivot);
            float shiftRight = Mathf.Max(0f, anchor.x + SideGap * viewHeight - itemOnView.xMin);
            float shiftUp = Mathf.Min(0f, anchor.y - TopGap * viewHeight - itemOnView.yMax);

            float weight = Mathf.SmoothStep(0f, 1f, blend);
            realPosition = item.position;
            realRotation = item.rotation;
            isDrawnAside = true;
            drawingCamera = camera;
            item.position = realPosition + (view.right * shiftRight + view.up * shiftUp) * (pivot.z * weight);
            if (isTurnedForPlacing)
                item.rotation = Quaternion.Slerp(realRotation, holder.rotation * placingRotation, weight);

            DrawRidersAside(item);
        }

        /// <summary>
        /// Moves the items riding in the current item's box along with the move from its real pose to where
        /// <paramref name="item"/> is drawn now.
        /// </summary>
        private void DrawRidersAside(Transform item)
        {
            if (!areRidersFound)
            {
                ItemBoxRiders.Collect(currentItem, riders);
                areRidersFound = true;
            }

            Quaternion turn = item.rotation * Quaternion.Inverse(realRotation);
            realRiderPoses.Clear();
            foreach (ShipItem rider in riders)
            {
                if (rider == null)
                {
                    realRiderPoses.Add(default);
                    continue;
                }

                Transform riderTransform = rider.transform;
                realRiderPoses.Add(new Pose(riderTransform.position, riderTransform.rotation));
                riderTransform.SetPositionAndRotation(
                    item.position + turn * (riderTransform.position - realPosition),
                    turn * riderTransform.rotation);
            }
        }

        private void RestoreRiders()
        {
            for (int i = 0; i < realRiderPoses.Count; i++)
            {
                if (riders[i] != null)
                    riders[i].transform.SetPositionAndRotation(realRiderPoses[i].position, realRiderPoses[i].rotation);
            }

            realRiderPoses.Clear();
        }

        /// <summary>
        /// Returns the rotation that faces the item along the <paramref name="holder"/>'s line of sight, with its up
        /// direction where its preview's up looks on screen.
        /// </summary>
        private Quaternion FacePlayerAtPreviewRoll(Transform holder)
        {
            Quaternion preview = AttachmentSpace.ToVisibleWorld(currentItem, currentItem.GetAttachRot());
            Vector3 up = Vector3.ProjectOnPlane(preview * Vector3.up, holder.forward);
            return up.sqrMagnitude < VectorUtils.MinDirectionSqrMagnitude
                ? holder.rotation
                : Quaternion.LookRotation(holder.forward, up);
        }

        private void RestoreAfterRender(Camera camera)
        {
            if (isDrawnAside && camera == drawingCamera)
                RestoreRealPose();
        }

        private void RestoreRealPose()
        {
            if (!isDrawnAside)
                return;

            isDrawnAside = false;
            drawingCamera = null;
            RestoreRiders();
            if (currentItem == null)
                return;

            currentItem.transform.SetPositionAndRotation(realPosition, realRotation);
        }

        /// <summary>
        /// Returns the box around the item's meshes as seen from <paramref name="view"/> while the item is turned to
        /// <paramref name="rotation"/>, every corner taken at the depth of the item's <paramref name="pivot"/>, in the
        /// units of <see cref="OnView"/>.
        /// </summary>
        private Rect MeasureOnView(Transform view, Transform item, Quaternion rotation, Vector3 pivot)
        {
            Vector2 min = OnView(pivot);
            Vector2 max = min;
            Vector3 scale = item.lossyScale;
            foreach (Vector3 corner in meshCorners)
            {
                Vector3 point = view.InverseTransformPoint(item.position + rotation * Vector3.Scale(scale, corner));
                Vector2 onView = new Vector2(point.x, point.y) / pivot.z;
                min = Vector2.Min(min, onView);
                max = Vector2.Max(max, onView);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// Returns where a point in the camera's space appears on a view plane one unit in front of the camera.
        /// </summary>
        private static Vector2 OnView(Vector3 pointInView)
        {
            return new Vector2(pointInView.x / pointInView.z, pointInView.y / pointInView.z);
        }
    }
}
