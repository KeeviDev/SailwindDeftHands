using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeftHands.Configuration
{
    /// <summary>
    /// Keeps a part of Deft Hands running through occasional errors, and turns it off until the game restarts once its
    /// code keeps throwing for <see cref="StreakToStop"/> seconds, putting back what that part changed. A part is a
    /// feature, named by its setting, the controls hints, or <see cref="Core"/>, whose stop turns every part off.
    /// </summary>
    internal static class FeatureGuard
    {
        /// <summary>The part every feature relies on.</summary>
        public const string Core = "Deft Hands";

        /// <summary>The lines Deft Hands adds to the game's controls text.</summary>
        public const string ControlsHints = "the controls hints";

        /// <summary>Seconds a part has to keep throwing before it is turned off.</summary>
        private const float StreakToStop = 5f;

        /// <summary>Longest gap, in seconds, between two errors of a part that still continues their streak.</summary>
        private const float MaxStreakGap = 1f;

        private static readonly HashSet<object> Stopped = new HashSet<object>();

        private static readonly Dictionary<object, ErrorStreak> Streaks = new Dictionary<object, ErrorStreak>();

        private static readonly Dictionary<object, List<Action>> Undos = new Dictionary<object, List<Action>>();

        /// <summary>
        /// Returns whether <paramref name="part"/> runs: neither it nor <see cref="Core"/> has been turned off.
        /// </summary>
        public static bool IsRunning(string part)
        {
            return IsPartRunning(part);
        }

        /// <summary>
        /// Returns whether <paramref name="feature"/> runs: neither it nor <see cref="Core"/> has been turned off.
        /// </summary>
        public static bool IsRunning(ConfigEntry<bool> feature)
        {
            return IsPartRunning(feature);
        }

        /// <summary>Returns whether <paramref name="feature"/> is turned on and runs.</summary>
        public static bool IsOn(ConfigEntry<bool> feature)
        {
            return feature.Value && IsPartRunning(feature);
        }

        /// <summary>
        /// Registers <paramref name="undo"/> to run once when <paramref name="part"/> or <see cref="Core"/> is turned
        /// off.
        /// </summary>
        public static void OnStop(string part, Action undo)
        {
            AddUndo(part, undo);
        }

        /// <summary>
        /// Registers <paramref name="undo"/> to run once when <paramref name="feature"/> or <see cref="Core"/> is
        /// turned off.
        /// </summary>
        public static void OnStop(ConfigEntry<bool> feature, Action undo)
        {
            AddUndo(feature, undo);
        }

        /// <summary>
        /// Records that <paramref name="part"/> threw <paramref name="error"/> and logs the first error of each streak.
        /// Once the part's errors have gone on for <see cref="StreakToStop"/> seconds, with no gap longer than
        /// <see cref="MaxStreakGap"/> seconds, turns the part off until the game restarts and runs the undos registered
        /// for it, or for every part when it is <see cref="Core"/>.
        /// </summary>
        public static void ReportError(string part, Exception error)
        {
            ReportPartError(part, part, error);
        }

        /// <summary>
        /// Records that <paramref name="feature"/> threw <paramref name="error"/>, as <see cref="ReportError(string,
        /// Exception)"/> does for a part.
        /// </summary>
        public static void ReportError(ConfigEntry<bool> feature, Exception error)
        {
            ReportPartError(feature, "\"" + feature.Definition.Key + "\"", error);
        }

        private static bool IsPartRunning(object part)
        {
            return Stopped.Count == 0 || (!Stopped.Contains(Core) && !Stopped.Contains(part));
        }

        private static void AddUndo(object part, Action undo)
        {
            if (!Undos.TryGetValue(part, out List<Action> undos))
            {
                undos = new List<Action>();
                Undos[part] = undos;
            }

            undos.Add(undo);
        }

        private static void ReportPartError(object part, string name, Exception error)
        {
            if (!IsPartRunning(part))
                return;

            float now = Time.unscaledTime;
            if (!Streaks.TryGetValue(part, out ErrorStreak streak) || now - streak.Last > MaxStreakGap)
            {
                streak = new ErrorStreak { Start = now };
                Streaks[part] = streak;
                Plugin.Log.LogError(string.Format(
                    "{0} ran into an error and keeps running; it is turned off if errors go on for {1} seconds: {2}",
                    name,
                    StreakToStop,
                    error));
            }

            streak.Last = now;
            if (now - streak.Start >= StreakToStop)
                StopPart(part, name, error);
        }

        private static void StopPart(object part, string name, Exception error)
        {
            Stopped.Add(part);
            Streaks.Remove(part);
            Plugin.Log.LogError(string.Format(
                "Turned off {0} until the game restarts after {1} seconds of errors. The last one: {2}",
                name,
                StreakToStop,
                error));

            if (Core.Equals(part))
            {
                foreach (object stoppedPart in new List<object>(Undos.Keys))
                    RunUndos(stoppedPart, name);
            }
            else
            {
                RunUndos(part, name);
            }
        }

        private static void RunUndos(object part, string name)
        {
            if (!Undos.TryGetValue(part, out List<Action> undos))
                return;

            Undos.Remove(part);
            foreach (Action undo in undos)
            {
                try
                {
                    undo();
                }
                catch (Exception error)
                {
                    Plugin.Log.LogError(string.Format("Could not put back what {0} changed: {1}", name, error));
                }
            }
        }

        /// <summary>The time span of a part's errors that follow each other closely.</summary>
        private sealed class ErrorStreak
        {
            /// <summary>Time.unscaledTime of the first error.</summary>
            public float Start;

            /// <summary>Time.unscaledTime of the latest error.</summary>
            public float Last;
        }
    }
}
