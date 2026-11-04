using DeftHands.Configuration;
using DeftHands.Utils;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Draws the held wall-attachable item to the lower right of the view while it shows where it will attach, unless
    /// it is being placed on a table or other surface for items. How far it moves depends only on the item's size and
    /// the preview's distance. While <see cref="WallPlacement.LocksRotationAxis"/>, it is also drawn facing the player at
    /// its preview's roll. Only the main camera's picture changes: the item's real pose is back in place as soon as that
    /// camera has rendered.
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

        private static readonly AccessTools.FieldRef<GoPointerTargeter, Vector3> PreviewPosition =
            ReflectionUtils.FindFieldRef<GoPointerTargeter, Vector3>("activePos");

        private static HeldItemAside instance;

        private ShipItem currentItem;

        /// <summary>Corners of the boxes around the item's meshes, in the item's own space.</summary>
        private readonly List<Vector3> meshCorners = new List<Vector3>();

        /// <summary>Whether <see cref="meshCorners"/> holds the current item's meshes.</summary>
        private bool isMeasured;

        /// <summary>How far the item is moved aside, from 0 (in the hand) to 1 (fully aside).</summary>
        private float blend;

        /// <summary>The item's rotation relative to its holder before the player tilts or rolls it.</summary>
        private Quaternion restingRotation;

        /// <summary>The distance along the holder's line of sight to the preview when the preview was last shown.</summary>
        private float previewDistance;

        /// <summary>Whether the item is drawn aside right now instead of having its real pose.</summary>
        private bool isDrawnAside;

        /// <summary>The camera the item is drawn aside for.</summary>
        private Camera drawingCamera;

        /// <summary>
        /// The rotation, relative to its holder, the item is drawn at while it is being placed: facing the player at its
        /// preview's roll.
        /// </summary>
        private Quaternion placingRotation;

        /// <summary>Whether the item is drawn turned towards <see cref="placingRotation"/>.</summary>
        private bool isTurnedForPlacing;

        private Vector3 realPosition;

        private Quaternion realRotation;

        public static HeldItemAside GetInstance()
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("DeftHands_HeldItemAside");
                instance = obj.AddComponent<HeldItemAside>();
                DontDestroyOnLoad(obj);
            }
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
            blend = WallPlacement.IsPlacedOnSurface(currentItem)
                ? 0f
                : Mathf.MoveTowards(blend, ShowsPreview() ? 1f : 0f, Time.deltaTime / BlendTime);
            if (blend <= 0f)
                isTurnedForPlacing = false;
        }

        private bool ShowsPreview()
        {
            return ModConfig.EasyWallHanging.Value && WallPlacement.ShowsPreview(currentItem);
        }

        /// <summary>
        /// Collects the box corners of the current item's meshes that are drawn right now.
        /// </summary>
        private void MeasureMeshes()
        {
            meshCorners.Clear();
            isMeasured = true;
            if (currentItem == null)
                return;

            Transform item = currentItem.transform;
            foreach (MeshFilter filter in currentItem.GetComponentsInChildren<MeshFilter>())
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled)
                    continue;

                Matrix4x4 toItem = ToItemSpace(filter.transform, item);
                Bounds bounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    meshCorners.Add(toItem.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, sign)));
                }
            }
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
        /// Moves the held item for the main camera, as far as <see cref="blend"/> has come, so that in its resting pose
        /// its upper left corner would sit below and right of the preview on the holder's line of sight. While
        /// <see cref="WallPlacement.LocksRotationAxis"/>, it also turns the item to face the player at its preview's
        /// roll, and the resting pose faces the player without that roll.
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
            if (WallPlacement.LocksRotationAxis(currentItem))
            {
                placingRotation = Quaternion.Inverse(holder.rotation) * FacePlayerAtPreviewRoll(holder);
                isTurnedForPlacing = true;
            }

            Quaternion resting = isTurnedForPlacing ? holder.rotation : holder.rotation * restingRotation;
            Ray previewRay = WallAttachmentAim.LineOfSight(currentItem.held);
            if (ShowsPreview() && PreviewPosition != null)
                previewDistance = Vector3.Distance(previewRay.origin, PreviewPosition(currentItem.held.GetTargeter()));

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
        }

        /// <summary>
        /// Returns the rotation that faces the item along the <paramref name="holder"/>'s line of sight, with its up
        /// direction where its preview's up looks on screen.
        /// </summary>
        private Quaternion FacePlayerAtPreviewRoll(Transform holder)
        {
            Quaternion preview = AttachmentSpace.ToVisibleWorld(currentItem, currentItem.GetAttachRot());
            Vector3 up = Vector3.ProjectOnPlane(preview * Vector3.up, holder.forward);
            return up.sqrMagnitude < 1e-6f ? holder.rotation : Quaternion.LookRotation(holder.forward, up);
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
