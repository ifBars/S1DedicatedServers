using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using EnvironmentManagerType = Il2CppScheduleOne.Weather.EnvironmentManager;
using MaskControllerType = Il2CppScheduleOne.Weather.MaskController;
using ActionType = Il2CppSystem.Action;
#else
using FishNet;
using EnvironmentManagerType = ScheduleOne.Weather.EnvironmentManager;
using MaskControllerType = ScheduleOne.Weather.MaskController;
using ActionType = System.Action;
#endif
using UnityEngine;

namespace DedicatedServerMod.Server.Game.Patches.Weather
{
    /// <summary>
    /// Skips beta mask modifications after headless GPU initialization was bypassed.
    /// </summary>
    [HarmonyPatch]
    internal static class MaskControllerModificationPatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MaskControllerType), nameof(MaskControllerType.UpdateMaskMap));
            yield return AccessTools.Method(typeof(MaskControllerType), nameof(MaskControllerType.SetModificationState));
            yield return AccessTools.Method(typeof(MaskControllerType), nameof(MaskControllerType.ApplyModifications));
            yield return AccessTools.Method(typeof(MaskControllerType), nameof(MaskControllerType.AddHippieModification));
            yield return AccessTools.Method(typeof(MaskControllerType), nameof(MaskControllerType.RemoveHippieModification));
        }

        private static bool Prefix()
        {
            return !DedicatedHeadlessWeatherCompatibility.ShouldBypassHeadlessWeatherMask();
        }
    }

    /// <summary>
    /// Completes weather initialization without building a texture array on the headless host.
    /// </summary>
    [HarmonyPatch(typeof(MaskControllerType), nameof(MaskControllerType.BuildTextureArrayAsync))]
    internal static class MaskControllerBuildTextureArrayPatches
    {
        private static bool Prefix(ActionType onComplete)
        {
            if (!DedicatedHeadlessWeatherCompatibility.ShouldBypassHeadlessWeatherMask())
            {
                return true;
            }

            onComplete?.Invoke();
            return false;
        }
    }

    /// <summary>
    /// Releases only allocated mask resources when the headless host skipped compute initialization.
    /// </summary>
    [HarmonyPatch(typeof(MaskControllerType), "OnDestroy")]
    internal static class MaskControllerOnDestroyPatches
    {
        private static bool Prefix(MaskControllerType __instance)
        {
            if (!Application.isBatchMode)
            {
                return true;
            }

            __instance._wetMaskTexture?.Release();
            __instance._wetMaskTexture = null;
            __instance._maskRenderTexture?.Release();
            __instance._maskRenderTexture = null;
            __instance._modificationStatesBuffer?.Release();
            __instance._modificationStatesBuffer = null;
            __instance._modificationDataBuffer?.Release();
            __instance._modificationDataBuffer = null;
            return false;
        }
    }
    /// <summary>
    /// Disables GPU-backed weather mask generation on dedicated servers because
    /// headless and nographics mode cannot reliably run the compute shader pipeline.
    /// </summary>
    [HarmonyPatch(typeof(MaskControllerType), nameof(MaskControllerType.Initialise))]
    internal static class MaskControllerInitialisePatches
    {
        private static bool Prefix()
        {
            if (!DedicatedHeadlessWeatherCompatibility.ShouldBypassHeadlessWeatherMask())
            {
                return true;
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
