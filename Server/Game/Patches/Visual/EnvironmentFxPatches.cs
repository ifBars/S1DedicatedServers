using System.Reflection;
using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Utils;
#if IL2CPP
using EnvironmentFxType = Il2CppScheduleOne.FX.EnvironmentFX;
#else
using EnvironmentFxType = ScheduleOne.FX.EnvironmentFX;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    /// <summary>
    /// Skips environment shader scroll updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(EnvironmentFxType), "Update")]
    internal static class EnvironmentFxUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

    /// <summary>
    /// Skips environment visual refresh work on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(EnvironmentFxType), "UpdateVisuals")]
    internal static class EnvironmentFxUpdateVisualsPatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

    /// <summary>
    /// Skips third-party volumetric fog rendering updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch]
    internal static class VolumetricFogLateUpdatePatches
    {
        private static MethodBase _targetMethod;
        private static bool _loggedMissingTarget;

        private static bool Prepare()
        {
            _targetMethod = SafeReflection.FindMethod(GetFogType(), "LateUpdate");
            if (_targetMethod != null)
            {
                return true;
            }

            if (!_loggedMissingTarget)
            {
                DebugLog.Info("Volumetric fog type was not found during dedicated patch setup. Skipping fog LateUpdate patch.");
                _loggedMissingTarget = true;
            }

            return false;
        }

        private static MethodBase TargetMethod()
        {
            return _targetMethod;
        }

        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }

        private static Type GetFogType()
        {
#if IL2CPP
            return SafeReflection.FindType("Il2CppVolumetricFogAndMist2.VolumetricFog")
                ?? SafeReflection.FindType("VolumetricFogAndMist2.VolumetricFog");
#else
            return SafeReflection.FindType("VolumetricFogAndMist2.VolumetricFog");
#endif
        }
    }
}
