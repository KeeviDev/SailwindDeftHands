using DeftHands.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Shows an arrow beside the preview of where a held item will hang, while
    /// <see cref="ModConfig.WallAttachmentTopArrow"/> is on, pointing the way the item's top faces and turning with the
    /// preview.
    /// </summary>
    internal static class PreviewTopArrow
    {
        private const string ArrowName = "Deft Hands top arrow";

        /// <summary>Metres between the top of the preview and the base of the arrow.</summary>
        private const float Gap = 0.03f;

        /// <summary>Metres from the base of the arrow to its tip.</summary>
        private const float Length = 0.1f;

        /// <summary>Metres from the base of the arrow's head to its tip.</summary>
        private const float HeadLength = 0.045f;

        /// <summary>Width of the arrow's head, in metres.</summary>
        private const float HeadWidth = 0.07f;

        /// <summary>Width of the arrow's shaft, in metres.</summary>
        private const float ShaftWidth = 0.025f;

        private static Mesh arrowMesh;

        /// <summary>
        /// Shows the arrow beside the preview of <paramref name="targeter"/> while the preview shows and
        /// <see cref="ModConfig.WallAttachmentTopArrow"/> is on, and hides it otherwise. Must be called after the
        /// targeter places its preview for the frame.
        /// </summary>
        public static void Follow(GoPointerTargeter targeter)
        {
            GameObject preview = GoPointerTargeterFields.Preview != null
                ? GoPointerTargeterFields.Preview(targeter)
                : null;
            MeshRenderer previewRenderer = GoPointerTargeterFields.PreviewRenderer != null
                ? GoPointerTargeterFields.PreviewRenderer(targeter)
                : null;
            if (preview == null || previewRenderer == null)
                return;

            bool shows = previewRenderer.enabled && ModConfig.WallAttachmentTopArrow.Value;
            Transform arrow = preview.transform.Find(ArrowName);
            if (arrow == null)
            {
                if (!shows)
                    return;

                arrow = CreateArrow(preview, previewRenderer.sharedMaterial);
            }

            if (shows)
                arrow.localPosition = GetArrowBase(targeter);

            arrow.gameObject.SetActive(shows);
        }

        /// <summary>
        /// Returns where the base of the arrow sits in the preview's space: centred over the top of the preview's mesh.
        /// </summary>
        private static Vector3 GetArrowBase(GoPointerTargeter targeter)
        {
            MeshFilter previewFilter = GoPointerTargeterFields.PreviewFilter != null
                ? GoPointerTargeterFields.PreviewFilter(targeter)
                : null;
            Mesh previewMesh = previewFilter != null ? previewFilter.sharedMesh : null;
            Bounds bounds = previewMesh != null ? previewMesh.bounds : default;
            return new Vector3(bounds.center.x, bounds.max.y + Gap, bounds.center.z);
        }

        /// <summary>
        /// Creates the arrow as a child of <paramref name="preview"/>, drawn with <paramref name="material"/>.
        /// </summary>
        private static Transform CreateArrow(GameObject preview, Material material)
        {
            var arrow = new GameObject(ArrowName) { layer = preview.layer };
            arrow.transform.SetParent(preview.transform, false);
            arrow.AddComponent<MeshFilter>().sharedMesh = GetArrowMesh();
            MeshRenderer renderer = arrow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return arrow.transform;
        }

        /// <summary>
        /// Returns the flat arrow mesh: it lies across the preview's up and right, points up from its base at the
        /// origin, and shows from both sides.
        /// </summary>
        private static Mesh GetArrowMesh()
        {
            if (arrowMesh != null)
                return arrowMesh;

            float halfShaft = ShaftWidth / 2f;
            float halfHead = HeadWidth / 2f;
            float neck = Length - HeadLength;
            Vector3[] outline =
            {
                new Vector3(-halfShaft, 0f), new Vector3(halfShaft, 0f), new Vector3(halfShaft, neck),
                new Vector3(-halfShaft, neck), new Vector3(-halfHead, neck), new Vector3(halfHead, neck),
                new Vector3(0f, Length)
            };
            int[] front = { 0, 3, 2, 0, 2, 1, 4, 6, 5 };

            int count = outline.Length;
            var vertices = new Vector3[count * 2];
            var normals = new Vector3[count * 2];
            var uv = new Vector2[count * 2];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = vertices[i + count] = outline[i];
                normals[i] = Vector3.back;
                normals[i + count] = Vector3.forward;
                uv[i] = uv[i + count] = new Vector2(outline[i].x / HeadWidth + 0.5f, outline[i].y / Length);
            }

            var triangles = new int[front.Length * 2];
            for (int i = 0; i < front.Length; i += 3)
            {
                triangles[i] = front[i];
                triangles[i + 1] = front[i + 1];
                triangles[i + 2] = front[i + 2];
                triangles[front.Length + i] = front[i] + count;
                triangles[front.Length + i + 1] = front[i + 2] + count;
                triangles[front.Length + i + 2] = front[i + 1] + count;
            }

            arrowMesh = new Mesh { name = ArrowName, vertices = vertices, normals = normals, uv = uv };
            arrowMesh.triangles = triangles;
            arrowMesh.RecalculateBounds();
            return arrowMesh;
        }
    }
}
