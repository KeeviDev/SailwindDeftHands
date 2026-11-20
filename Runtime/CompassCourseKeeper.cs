using DeftHands.Configuration;
using DeftHands.Utils;
using System;
using UnityEngine;

namespace DeftHands.Runtime
{
    /// <summary>
    /// Runs <see cref="CompassAlignment.KeepCourseInUsualHold"/> at the end of every frame, after the holder and the
    /// compass's own code have posed the held compass.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class CompassCourseKeeper : MonoBehaviour
    {
        private static CompassCourseKeeper instance;

        public static CompassCourseKeeper GetInstance()
        {
            if (instance == null)
                instance = PersistentObject.Create<CompassCourseKeeper>();

            return instance;
        }

        private void LateUpdate()
        {
            try
            {
                CompassAlignment.KeepCourseInUsualHold();
            }
            catch (Exception error)
            {
                FeatureGuard.ReportError(ModConfig.CompassCourseAlignment, error);
            }
        }
    }
}
