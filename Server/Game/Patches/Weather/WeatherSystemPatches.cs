using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using EnvironmentManagerType = Il2CppScheduleOne.Weather.EnvironmentManager;
using MaskControllerType = Il2CppScheduleOne.Weather.MaskController;
#else
using FishNet;
using EnvironmentManagerType = ScheduleOne.Weather.EnvironmentManager;
using MaskControllerType = ScheduleOne.Weather.MaskController;
#endif
using UnityEngine;

namespace DedicatedServerMod.Server.Game.Patches.Weather
{
    /// <summary>
    /// Disables GPU-backed weather mask generation on dedicated servers because
    /// headless and nographics mode cannot reliably run the compute shader pipeline.
    /// </summary>
    [HarmonyPatch(typeof(MaskControllerType), nameof(MaskControllerType.Initialise))]
    internal static class MaskControllerInitialisePatches
    {
        private static bool _loggedSkip;

        private static bool Prefix()
        {
            if (!DedicatedHeadlessWeatherCompatibility.ShouldBypassHeadlessWeatherMask())
            {
                return true;
            }

            if (!_loggedSkip)
            {
                DebugLog.Info("Skipping weather mask compute initialization on dedicated headless server.");
                _loggedSkip = true;
            }

            return false;
        }
    }

    /// <summary>
    /// Prevents the height-map GPU readback routine from running on dedicated servers.
    /// Cover checks fall back to an uncovered result when no height map is available.
    /// </summary>
    [HarmonyPatch(typeof(MaskControllerType), nameof(MaskControllerType.ConvertHeightToArray))]
    internal static class MaskControllerConvertHeightToArrayPatches
    {
        private static bool Prefix()
        {
            return !DedicatedHeadlessWeatherCompatibility.ShouldBypassHeadlessWeatherMask();
        }
    }

    /// <summary>
    /// Skips wet mask compute updates on dedicated servers because they are purely visual.
    /// </summary>
    [HarmonyPatch(typeof(MaskControllerType), nameof(MaskControllerType.RunWetMaskShader))]
    internal static class MaskControllerRunWetMaskShaderPatches
    {
        private static bool Prefix()
        {
            return !DedicatedHeadlessWeatherCompatibility.ShouldBypassHeadlessWeatherMask();
        }
    }

    /// <summary>
    /// Skips sky and shader updates on dedicated servers because they only drive client visuals.
    /// </summary>
    [HarmonyPatch(typeof(EnvironmentManagerType), "UpdateWeather")]
    internal static class EnvironmentManagerUpdateWeatherPatches
    {
        private static bool Prefix()
        {
            return !DedicatedHeadlessWeatherCompatibility.ShouldBypassHeadlessWeatherMask();
        }
    }

    /// <summary>
    /// Prevents null dereferences in the new weather system when dedicated servers have no mask data.
    /// Returning false preserves server-side weather entity updates while treating unknown cover as outdoors.
    /// </summary>
    [HarmonyPatch(typeof(EnvironmentManagerType), nameof(EnvironmentManagerType.IsPositionUnderCover))]
    internal static class EnvironmentManagerIsPositionUnderCoverPatches
    {
        private static bool _loggedFallback;

        private static bool Prefix(EnvironmentManagerType __instance, ref bool __result)
        {
            if (!DedicatedHeadlessWeatherCompatibility.ShouldBypassHeadlessWeatherMask())
            {
                return true;
            }

            MaskControllerType maskController = __instance._maskController;
            if (maskController != null &&
                maskController.HeightMap != null &&
                maskController.HeightMap.Length > 0 &&
                maskController.HeightMapResolution > 0)
            {
                return true;
            }

            if (!_loggedFallback)
            {
                DebugLog.Info("Dedicated headless server is running weather cover checks without a generated height map. Weather entities will be treated as uncovered.");
                _loggedFallback = true;
            }

            __result = false;
            return false;
        }
    }

    internal static class DedicatedHeadlessWeatherCompatibility
    {
        internal static bool ShouldBypassHeadlessWeatherMask()
        {
            return InstanceFinder.IsServer && Application.isBatchMode;
        }
    }
}
