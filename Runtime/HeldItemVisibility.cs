using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Tells whether the held item draws nothing: it has renderers and none of them is on.
    /// </summary>
    internal static class HeldItemVisibility
    {
        private static PickupableItem currentItem;

        private static Renderer[] renderers = new Renderer[0];

        private static int checkedFrame = -1;

        private static bool isHidden;

        /// <summary>
        /// Registers the currently held item.
        /// </summary>
        /// <param name="item">The item now held, or null if nothing is held.</param>
        public static void SetCurrentItem(PickupableItem item)
        {
            currentItem = item;
            renderers = item != null ? item.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            checkedFrame = -1;
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> is the held item and draws nothing this frame.
        /// </summary>
        public static bool IsHidden(PickupableItem item)
        {
            if (item == null || item != currentItem || renderers.Length == 0)
                return false;

            if (checkedFrame != Time.frameCount)
            {
                checkedFrame = Time.frameCount;
                isHidden = !IsAnyDrawn();
            }

            return isHidden;
        }

        private static bool IsAnyDrawn()
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    return true;
            }

            return false;
        }
    }
}
