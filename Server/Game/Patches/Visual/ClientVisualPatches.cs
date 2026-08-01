using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
#if IL2CPP
using AvatarImpostorType = Il2CppScheduleOne.AvatarFramework.Impostors.AvatarImpostor;
#else
using AvatarImpostorType = ScheduleOne.AvatarFramework.Impostors.AvatarImpostor;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    internal static class OptionalClientVisualPatchTargets
    {
        internal static MethodBase ResolveFirst(string methodName, params string[] typeNames)
        {
            for (int i = 0; i < typeNames.Length; i++)
            {
                var type = GetLoadedType(typeNames[i]);
                if (type == null)
                {
                    continue;
                }

                var method = AccessTools.Method(type, methodName);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        internal static IEnumerable<MethodBase> Resolve(string methodName, params string[] typeNames)
        {
            for (int i = 0; i < typeNames.Length; i++)
            {
                var type = GetLoadedType(typeNames[i]);
                if (type == null)
                {
                    continue;
                }

                var method = AccessTools.Method(type, methodName);
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        private static Type GetLoadedType(string typeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Skips sky system updates on dedicated servers because they only drive client-side visuals.
    /// </summary>
    [HarmonyPatch]
    internal static class TimeOfDayControllerPatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return OptionalClientVisualPatchTargets.ResolveFirst(
                "Update",
                "Il2CppFunly.SkyStudio.TimeOfDayController",
                "Funly.SkyStudio.TimeOfDayController") != null;
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return OptionalClientVisualPatchTargets.Resolve(
                "Update",
                "Il2CppFunly.SkyStudio.TimeOfDayController",
                "Funly.SkyStudio.TimeOfDayController");
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }

    /// <summary>
    /// Skips weather presentation updates on dedicated servers because they only toggle visual effects.
    /// </summary>
    [HarmonyPatch]
    internal static class WeatherControllerPatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return OptionalClientVisualPatchTargets.ResolveFirst(
                "LateUpdate",
                "Il2CppFunly.SkyStudio.WeatherController",
                "Funly.SkyStudio.WeatherController") != null;
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return OptionalClientVisualPatchTargets.Resolve(
                "LateUpdate",
                "Il2CppFunly.SkyStudio.WeatherController",
                "Funly.SkyStudio.WeatherController");
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }

    /// <summary>
    /// Skips avatar impostor billboarding on dedicated servers because it only aligns meshes to a camera.
    /// </summary>
    [HarmonyPatch]
    internal static class AvatarImpostorPatches
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            return typeof(AvatarImpostorType).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return typeof(AvatarImpostorType).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }

}
