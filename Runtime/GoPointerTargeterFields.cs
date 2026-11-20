using DeftHands.Utils;
using HarmonyLib;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Accessors for private <see cref="GoPointerTargeter"/> fields. Each is null when the game has no field of that
    /// name and type.
    /// </summary>
    internal static class GoPointerTargeterFields
    {
        /// <summary>The object that draws the placement preview.</summary>
        public static readonly AccessTools.FieldRef<GoPointerTargeter, GameObject> Preview =
            ReflectionUtils.FindFieldRef<GoPointerTargeter, GameObject>("targeter");

        public static readonly AccessTools.FieldRef<GoPointerTargeter, MeshFilter> PreviewFilter =
            ReflectionUtils.FindFieldRef<GoPointerTargeter, MeshFilter>("filter");

        public static readonly AccessTools.FieldRef<GoPointerTargeter, MeshRenderer> PreviewRenderer =
            ReflectionUtils.FindFieldRef<GoPointerTargeter, MeshRenderer>("renderer");

        /// <summary>
        /// Whether a pose was stored for the preview since the targeter's last Update, so that its next Update shows
        /// the preview; false makes it hide the preview instead.
        /// </summary>
        public static readonly AccessTools.FieldRef<GoPointerTargeter, bool> IsActive =
            ReflectionUtils.FindFieldRef<GoPointerTargeter, bool>("targeterActive");

        /// <summary>The position last stored for the preview.</summary>
        public static readonly AccessTools.FieldRef<GoPointerTargeter, Vector3> PreviewPosition =
            ReflectionUtils.FindFieldRef<GoPointerTargeter, Vector3>("activePos");
    }
}
