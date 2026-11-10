using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Remembers when items were put down, so that picking one up again within <see cref="ResumeTime"/> seconds can
    /// resume how it was held.
    /// </summary>
    internal sealed class PutDownMemory
    {
        /// <summary>
        /// Seconds after being put down within which picking an item up again resumes how it was held.
        /// </summary>
        public const float ResumeTime = 20f;

        /// <summary>When each remembered item was put down, by Time.time.</summary>
        private readonly Dictionary<PickupableItem, float> putDownTimes = new Dictionary<PickupableItem, float>();

        /// <summary>
        /// Remembers that <paramref name="item"/> was put down now.
        /// </summary>
        public void Remember(PickupableItem item)
        {
            ForgetOldPutDowns();
            putDownTimes[item] = Time.time;
        }

        /// <summary>
        /// Returns whether <paramref name="item"/> was put down within <see cref="ResumeTime"/> seconds, and forgets
        /// when it was put down.
        /// </summary>
        public bool WasPutDownRecently(PickupableItem item)
        {
            if (!putDownTimes.TryGetValue(item, out float putDownTime))
                return false;

            putDownTimes.Remove(item);
            return Time.time - putDownTime <= ResumeTime;
        }

        private void ForgetOldPutDowns()
        {
            foreach (PickupableItem item in putDownTimes.Keys.ToList())
            {
                if (item == null || Time.time - putDownTimes[item] > ResumeTime)
                    putDownTimes.Remove(item);
            }
        }
    }
}
