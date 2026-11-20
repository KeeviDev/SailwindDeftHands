using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DeftHands.Configuration;
using DeftHands.Runtime;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DeftHands
{
    /// <summary>
    /// Binds Deft Hands' configuration and applies its Harmony patches when BepInEx loads the
    /// plugin.
    /// </summary>
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.keevi.defthands";
        public const string PLUGIN_NAME = "Deft Hands";
        public const string PLUGIN_VERSION = "1.2.1";

        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;

            try
            {
                ModConfig.Bind(Config);
                RegisterUndos();
                RotationControlsHint.WatchSettings(Config);
                LogSettings();
                ApplyHarmonyPatches();
            }
            catch (Exception ex)
            {
                Log.LogError(string.Format("{0} failed to load and will not activate: {1}", PLUGIN_NAME, ex));
                return;
            }

            Log.LogInfo(string.Format("{0} v{1} loaded.", PLUGIN_NAME, PLUGIN_VERSION));
        }

        private void LogSettings()
        {
            Log.LogInfo("Configuration:");
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> setting in Config)
            {
                Log.LogInfo(string.Format("  {0}: {1}", setting.Key.Key, setting.Value.BoxedValue));
            }
        }

        /// <summary>
        /// Registers with <see cref="FeatureGuard"/> what each part of Deft Hands puts back when it stops.
        /// </summary>
        private static void RegisterUndos()
        {
            FeatureGuard.OnStop(ModConfig.ExtendedSmallItemsRotation, ExtendedHold.Disable);
            FeatureGuard.OnStop(ModConfig.NeatItemPlacement, FurniturePlacement.Disable);
            FeatureGuard.OnStop(ModConfig.CompassCourseAlignment, CompassAlignment.UnlockCourse);
            FeatureGuard.OnStop(ModConfig.EasyWallHanging, HeldItemAside.Disable);
            FeatureGuard.OnStop(ModConfig.WallAttachmentTopArrow, PreviewTopArrow.HideArrows);
            FeatureGuard.OnStop(FeatureGuard.ControlsHints, HungPickupHint.HideKeys);
            FeatureGuard.OnStop(FeatureGuard.Core, RotationHandler.Disable);
            FeatureGuard.OnStop(FeatureGuard.Core, PushPullHandler.Disable);
        }

        /// <summary>
        /// Applies every Harmony patch class in this assembly, logs each class that fails to apply, and logs each
        /// patched method.
        /// </summary>
        private static void ApplyHarmonyPatches()
        {
            var harmony = new Harmony(PLUGIN_GUID);
            foreach (Type patchClass in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
            {
                try
                {
                    harmony.CreateClassProcessor(patchClass).Patch();
                }
                catch (Exception ex)
                {
                    Log.LogError(string.Format(
                        "Could not apply the patches in {0} - the game may have updated. The feature may not work: {1}",
                        patchClass.Name,
                        ex));
                }
            }

            foreach (MethodBase method in harmony.GetPatchedMethods())
            {
                Log.LogInfo(string.Format("Patched {0}.{1}", method.DeclaringType, method.Name));
            }
        }
    }
}
