using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DeftHands.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DeftHands
{
    /// <summary>
    /// Deft Hands' BepInEx entry point: binds configuration and applies Harmony patches.
    /// </summary>
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.keevi.defthands";
        public const string PLUGIN_NAME = "Deft Hands";
        public const string PLUGIN_VERSION = "1.0.0";

        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;

            try
            {
                ModConfig.Bind(Config);
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
        /// Applies every Harmony patch in this assembly. A transpiler that can't find its target
        /// IL doesn't throw; it logs its own error instead.
        /// </summary>
        private static void ApplyHarmonyPatches()
        {
            var harmony = new Harmony(PLUGIN_GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            foreach (MethodBase method in harmony.GetPatchedMethods())
            {
                Log.LogInfo(string.Format("Patched {0}.{1}", method.DeclaringType, method.Name));
            }
        }
    }
}
