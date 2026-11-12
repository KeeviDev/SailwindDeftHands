using DeftHands.Configuration;
using DeftHands.Utils;
using HarmonyLib;
using UnityEngine;
using Outline = cakeslice.Outline;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Places the held small item on the table or other surface for items it is aimed at while Neat Item Placement is
    /// on, in a pose the player can turn, and puts it down in that pose. Keeps the item clear of what it would reach
    /// into, or outlines it red and keeps it in the hand when it can't be cleared.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class FurniturePlacement : MonoBehaviour
    {
        /// <summary>Seconds after looking away from a surface during which the item's rotation on it is kept.</summary>
        public const float MemoryTime = 2f;

        /// <summary>
        /// Largest angle, in degrees, between a surface and the level at which an item that stands upright rests along
        /// the surface rather than level.
        /// </summary>
        private const float MaxSurfaceTilt = 20f;

        /// <summary>Smallest angle, in degrees, from its resting rotation at which the item counts as turned.</summary>
        private const float MinTurnAngle = 0.5f;

        /// <summary>Size of the box that stands for the surface, in metres.</summary>
        private const float GroundSize = 20f;

        /// <summary>Depth below the scene at which the colliders used for measuring are kept, in metres.</summary>
        private const float MeasuringDepth = 10000f;

        private const int IgnoreRaycastLayer = 2;

        private const int RedOutlineColor = 2;

        private static readonly AccessTools.FieldRef<GoPointerButton, Outline> ItemOutline =
            ReflectionUtils.FindFieldRef<GoPointerButton, Outline>("outline");

        private static FurniturePlacement instance;

        private ShipItem currentItem;

        private ItemShape currentShape;

        /// <summary>The parent of the colliders used for measuring, kept below the scene.</summary>
        private Transform measuringRoot;

        /// <summary>
        /// A box whose top face lies at its transform's origin, standing for the surface the item is put on.
        /// </summary>
        private BoxCollider ground;

        private bool isPlacing;

        /// <summary>Whether <see cref="PlaceHeldItem"/> posed the item on a surface this frame.</summary>
        private bool isPosed;

        /// <summary>The item's rotation relative to <see cref="surfaceFrame"/>.</summary>
        private Quaternion rotationOnSurface = Quaternion.identity;

        /// <summary>
        /// Whether <see cref="rotationOnSurface"/> is kept for the next surface the item is aimed at.
        /// </summary>
        private bool remembersRotation;

        /// <summary>
        /// The rotation the held item lay at when picked up, where each surface starts it as a small item.
        /// </summary>
        private readonly PickupRotationMemory pickupRotation = new PickupRotationMemory();

        /// <summary>Whether the player turned or reset the item on the surface it is placed on.</summary>
        private bool isRotationChanged;

        private float leftSurfaceTime;

        /// <summary>The rotation whose up is the surface's and whose forward is the player's heading on it.</summary>
        private Quaternion surfaceFrame = Quaternion.identity;

        /// <summary>The item's pose on the surface in the visible world.</summary>
        private Pose shownPose;

        /// <summary>The item's pose on the surface in the space its physics twin moves in.</summary>
        private Pose bodyPose;

        private bool overlaps;

        /// <summary>The item let go on a surface this frame, to be put down in <see cref="placedPose"/>.</summary>
        private ShipItem placedItem;

        private Pose placedPose;

        /// <summary>Gets the up direction of the surface the held item is placed on, in the visible world.</summary>
        public Vector3 SurfaceUp => surfaceFrame * Vector3.up;

        /// <summary>
        /// Returns the axis level with the surface the held item is placed on that runs across the view of
        /// <paramref name="view"/>, in the visible world.
        /// </summary>
        public Vector3 GetTiltAxis(Transform view)
        {
            return Vector3.ProjectOnPlane(view.right, SurfaceUp).normalized;
        }

        /// <summary>Gets whether the item placed on a surface is turned away from its resting rotation.</summary>
        public bool IsTurned =>
            currentShape != null && Quaternion.Angle(rotationOnSurface, currentShape.RestingRotation) > MinTurnAngle;

        public static FurniturePlacement GetInstance()
        {
            if (instance == null)
                instance = PersistentObject.Create<FurniturePlacement>();

            return instance;
        }

        /// <summary>
        /// Returns whether the held <paramref name="item"/> is placed on the table or other surface for items it is
        /// aimed at this frame.
        /// </summary>
        public static bool IsPlacing(PickupableItem item)
        {
            return instance != null && instance.CanPlace(item);
        }

        /// <summary>
        /// Gets whether the held item is placed on the table or other surface for items it is aimed at this frame.
        /// </summary>
        public static bool IsPlacingHeldItem => instance != null && instance.CanPlace(instance.currentItem);

        /// <summary>
        /// Returns whether the game itself shows the held <paramref name="item"/> where it would set it down on the
        /// table or other surface for items it is aimed at: the item is held as a small item and not
        /// <see cref="IsPlacing"/>.
        /// </summary>
        public static bool IsShownByGame(PickupableItem item)
        {
            return item is ShipItem shipItem
                && SurfaceAim.IsAimedAtSurfaceForItems(shipItem)
                && !ExtendedHold.IsHeldLikeBig(item)
                && !IsPlacing(item);
        }

        /// <summary>
        /// Registers the currently held item and forgets the previous one's rotation on a surface. An item just taken
        /// from a table or other surface for items starts the surfaces it is aimed at as a small item at the rotation
        /// it lay at, while <see cref="PickupRotationMemory"/> remembers it.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        public void SetCurrentItem(PickupableItem item)
        {
            if (currentShape != null)
                currentShape.Discard();

            currentItem = item as ShipItem;
            currentShape = null;
            MeasureCurrentItem();
            isPlacing = false;
            remembersRotation = false;
            overlaps = false;
            placedItem = null;
            pickupRotation.OnPickup(currentItem, currentShape);
        }

        /// <summary>
        /// Turns the item placed on a surface by <paramref name="rotation"/>, given in the visible world, as a turn by
        /// the player (<see cref="NoteTurnByPlayer"/>).
        /// </summary>
        public void Turn(Quaternion rotation)
        {
            rotationOnSurface = Quaternion.Inverse(surfaceFrame) * rotation * surfaceFrame * rotationOnSurface;
            NoteTurnByPlayer();
        }

        /// <summary>
        /// Turns the item placed on a surface back to its resting rotation, facing the way the player looks, as a turn
        /// by the player (<see cref="NoteTurnByPlayer"/>).
        /// </summary>
        public void ResetRotation()
        {
            if (currentShape == null)
                return;

            rotationOnSurface = currentShape.RestingRotation;
            NoteTurnByPlayer();
        }

        /// <summary>
        /// Marks the item placed on a surface as turned by the player, forgets the rotation it lay at when picked up,
        /// and restarts the time after which an item held like a big item goes back into the hand.
        /// </summary>
        private void NoteTurnByPlayer()
        {
            isRotationChanged = true;
            pickupRotation.Forget();
            ExtendedHold.GetInstance().NoteActivity(currentItem);
        }

        /// <summary>
        /// Forgets the held item's rotation on the last surface, and the rotation it lay at when picked up unless a
        /// rotation in the hand leaves that remembered, so that it starts from its rotation in the hand on the next
        /// one.
        /// </summary>
        public void ForgetRotation()
        {
            remembersRotation = false;
            pickupRotation.ForgetUnlessKeptInHand();
        }

        /// <summary>
        /// Returns whether the held <paramref name="item"/>, just taken by the surface for items it was clicked on, is
        /// let go: false while it is placed on that surface, would stay where it is let go and can't be cleared of what
        /// it reaches into. When it is let go and stays there, remembers the pose to put it down in. Must be called
        /// before the item is let go.
        /// </summary>
        public bool TryPlace(PickupableItem item)
        {
            placedItem = null;
            if (!CanPlace(item) || !StaysWhereLetGo(currentItem) || !TryUpdatePose())
                return true;

            if (overlaps)
                return false;

            QuadrantInspection.StopTurning(currentItem);
            placedItem = currentItem;
            placedPose = bodyPose;
            return true;
        }

        /// <summary>
        /// Puts the item <see cref="TryPlace"/> let go down in the pose it was shown in, at rest. Must be called once
        /// the item is let go.
        /// </summary>
        public void PutDownPlacedItem()
        {
            ShipItem item = placedItem;
            placedItem = null;
            if (item == null || item.held != null)
                return;

            ItemRigidbody body = item.GetItemRigidbody();
            if (body == null)
                return;

            item.transform.SetPositionAndRotation(
                AttachmentSpace.ToVisibleWorld(item, placedPose.position),
                AttachmentSpace.ToVisibleWorld(item, placedPose.rotation));
            body.transform.SetPositionAndRotation(placedPose.position, placedPose.rotation);
            pickupRotation.NotePlaced(item);

            Rigidbody rigidbody = body.GetBody();
            if (rigidbody == null)
                return;

            rigidbody.position = placedPose.position;
            rigidbody.rotation = placedPose.rotation;
            if (!rigidbody.isKinematic)
            {
                rigidbody.velocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void Awake()
        {
            var measuringObject = new GameObject("DeftHands_PlacementMeasuring");
            measuringObject.layer = IgnoreRaycastLayer;
            measuringRoot = measuringObject.transform;
            measuringRoot.SetParent(transform, false);
            measuringRoot.position = Vector3.down * MeasuringDepth;

            var groundObject = new GameObject("DeftHands_PlacementGround");
            groundObject.layer = IgnoreRaycastLayer;
            groundObject.transform.SetParent(measuringRoot, false);
            ground = groundObject.AddComponent<BoxCollider>();
            ground.isTrigger = true;
            ground.size = Vector3.one * GroundSize;
            ground.center = Vector3.down * (GroundSize / 2f);
        }

        /// <summary>
        /// Poses the held item on the surface for items it is aimed at, or stops placing it when it is aimed at none.
        /// Must be called right after the holder places the held item for the frame.
        /// </summary>
        public void PlaceHeldItem()
        {
            if (currentShape == null && SurfaceAim.IsAimedAtSurfaceForItems(currentItem))
                MeasureCurrentItem();

            isPosed = CanPlace(currentItem) && TryUpdatePose();
            if (!isPosed)
            {
                LeaveSurface();
                return;
            }

            currentItem.transform.SetPositionAndRotation(shownPose.position, shownPose.rotation);
        }

        private void LateUpdate()
        {
            if (!isPosed || currentItem == null)
                return;

            isPosed = false;
            currentItem.transform.SetPositionAndRotation(shownPose.position, shownPose.rotation);
            QuadrantInspection.ShowStraight(currentItem);
            if (overlaps)
                ShowRedOutline();
        }

        private bool CanPlace(PickupableItem item)
        {
            return ModConfig.NeatItemPlacement.Value
                && item != null
                && item == currentItem
                && currentShape != null
                && !HeldItemVisibility.IsHidden(currentItem)
                && SurfaceAim.IsAimedAtSurfaceForItems(currentItem)
                && SurfaceAim.HasHit;
        }

        /// <summary>
        /// Works out the pose the current item rests in on the surface looked at, moved out of what it would reach into
        /// when it would stay where it is let go, and returns whether it can rest there.
        /// </summary>
        private bool TryUpdatePose()
        {
            if (!SurfaceAim.TryGetHit(out Vector3 point, out Vector3 normal))
                return false;

            Vector3 level = AttachmentSpace.UpInWorld(currentItem);
            bool liesOnAnyFace = currentShape.IsFlat && !ExtendedHold.IsHeldLikeBig(currentItem);
            bool restsOnFace = liesOnAnyFace || Vector3.Angle(normal, level) <= MaxSurfaceTilt;
            Vector3 up = restsOnFace ? normal : level;
            Quaternion frame = GetFacingFrame(currentItem.held.transform, up);
            if (!isPlacing)
                EnterSurface(frame);

            surfaceFrame = frame;
            Quaternion rotation = frame * rotationOnSurface;
            if (!currentShape.TryRestAgainst(ground, point, normal, rotation, out Vector3 position))
                return false;

            bodyPose = new Pose(
                AttachmentSpace.ToAttachmentSpace(currentItem, position),
                AttachmentSpace.ToAttachmentSpace(currentItem, rotation));
            Quaternion toSurfaceUp = Quaternion.FromToRotation(Vector3.up, up);
            Vector3 bodyUp = AttachmentSpace.ToAttachmentSpace(currentItem, toSurfaceUp) * Vector3.up;
            if (!restsOnFace)
                bodyPose = currentShape.DropOntoSupport(bodyPose, bodyUp);

            overlaps = StaysWhereLetGo(currentItem) && !currentShape.TrySettle(ref bodyPose, bodyUp);
            shownPose = new Pose(AttachmentSpace.ToVisibleWorld(currentItem, bodyPose.position), rotation);
            return ItemShape.IsUsable(bodyPose.position) && ItemShape.IsUsable(shownPose.position);
        }

        /// <summary>
        /// Starts placing the current item on a surface framed by <paramref name="frame"/>: at the rotation it lay at when
        /// picked up while that is remembered and the item is held as a small item; else at its rotation on the last
        /// surface when that was within <see cref="MemoryTime"/> seconds; or else at <see cref="GetStartingRotation"/>.
        /// </summary>
        private void EnterSurface(Quaternion frame)
        {
            if (!ExtendedHold.IsHeldLikeBig(currentItem) && pickupRotation.TryGet(out Quaternion rotationAtPickup))
                rotationOnSurface = Quaternion.Inverse(frame)
                    * AttachmentSpace.ToVisibleWorld(currentItem, rotationAtPickup);
            else if (!remembersRotation || Time.time - leftSurfaceTime > MemoryTime)
                rotationOnSurface = GetStartingRotation(frame);

            isRotationChanged = false;
            isPlacing = true;
        }

        /// <summary>
        /// Stops placing the current item. An item held like a big item gets the rotation the player gave it on the
        /// surface in the hand; any other is remembered with its rotation on the surface.
        /// </summary>
        private void LeaveSurface()
        {
            overlaps = false;
            if (!isPlacing)
                return;

            QuadrantInspection.Restore(currentItem);
            isPlacing = false;
            if (ExtendedHold.IsHeldLikeBig(currentItem))
            {
                if (isRotationChanged && currentItem.held != null)
                    TurnInHand();
                return;
            }

            remembersRotation = true;
            leftSurfaceTime = Time.time;
        }

        /// <summary>
        /// Gives the current item, held like a big item, its rotation on the surface in the hand from this frame on,
        /// measured against the level and the player's heading.
        /// </summary>
        private void TurnInHand()
        {
            if (GoPointerFields.BigItemLocalRot == null)
                return;

            Transform holder = currentItem.held.transform;
            Quaternion handFrame = GetFacingFrame(holder, AttachmentSpace.UpInWorld(currentItem));
            Quaternion rotationInHand = Quaternion.Inverse(holder.rotation) * handFrame * rotationOnSurface;
            GoPointerFields.BigItemLocalRot(currentItem.held) = rotationInHand;
            currentItem.heldRotationOffset = 0f;
            currentItem.transform.rotation = holder.rotation * rotationInHand;
            ExtendedHold.RememberPlacedRotation(currentItem.held);
        }

        /// <summary>
        /// Returns the rotation, relative to <paramref name="frame"/>, the current item starts with on a surface: its
        /// rotation in the hand while held like a big item, its resting rotation otherwise.
        /// </summary>
        private Quaternion GetStartingRotation(Quaternion frame)
        {
            if (!ExtendedHold.IsHeldLikeBig(currentItem) || GoPointerFields.BigItemLocalRot == null)
                return currentShape.RestingRotation;

            GoPointer holder = currentItem.held;
            Quaternion rotationInHand = Quaternion.AngleAxis(currentItem.heldRotationOffset, Vector3.right)
                * GoPointerFields.BigItemLocalRot(holder);
            return Quaternion.Inverse(frame) * holder.transform.rotation * rotationInHand;
        }

        /// <summary>
        /// Returns the rotation whose up is <paramref name="up"/> and whose forward is the heading of
        /// <paramref name="holder"/> across the plane facing it.
        /// </summary>
        private static Quaternion GetFacingFrame(Transform holder, Vector3 up)
        {
            Vector3 right = Vector3.ProjectOnPlane(holder.right, up);
            return right.sqrMagnitude < VectorUtils.MinDirectionSqrMagnitude
                ? Quaternion.FromToRotation(Vector3.up, up)
                : Quaternion.LookRotation(Vector3.Cross(right.normalized, up), up);
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> stays where it is let go: it is bought, and not attached, stowed or
        /// put in a stove.
        /// </summary>
        private static bool StaysWhereLetGo(ShipItem item)
        {
            ItemRigidbody body = item.GetItemRigidbody();
            return item.sold && body != null && !body.attached && !body.disableCol && !body.inStove;
        }

        private void MeasureCurrentItem()
        {
            if (currentItem == null)
                return;

            currentShape = ItemShape.Measure(currentItem, measuringRoot);
        }

        private void ShowRedOutline()
        {
            Outline outline = ItemOutline != null ? ItemOutline(currentItem) : null;
            if (outline == null)
                return;

            outline.color = RedOutlineColor;
            outline.enabled = true;
        }
    }
}
