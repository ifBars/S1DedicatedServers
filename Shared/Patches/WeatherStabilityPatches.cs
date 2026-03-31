using HarmonyLib;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet.Object.Synchronizing;
using EnvironmentManagerType = Il2CppScheduleOne.Weather.EnvironmentManager;
using LandVehicleType = Il2CppScheduleOne.Vehicles.LandVehicle;
using VehicleSettingsType = Il2CppScheduleOne.Experimental.VehicleSettings;
using WeatherConditionsType = Il2CppScheduleOne.Weather.WeatherConditions;
using WeatherVolumeType = Il2CppScheduleOne.Weather.WeatherVolume;
using WheelDataType = Il2CppScheduleOne.Experimental.WheelData;
using WheelOverrideDataType = Il2CppScheduleOne.Experimental.WheelOverrideData;
using WheelType = Il2CppScheduleOne.Vehicles.Wheel;
#else
using FishNet.Object.Synchronizing;
using EnvironmentManagerType = ScheduleOne.Weather.EnvironmentManager;
using LandVehicleType = ScheduleOne.Vehicles.LandVehicle;
using VehicleSettingsType = ScheduleOne.Experimental.VehicleSettings;
using WeatherConditionsType = ScheduleOne.Weather.WeatherConditions;
using WeatherVolumeType = ScheduleOne.Weather.WeatherVolume;
using WheelDataType = ScheduleOne.Experimental.WheelData;
using WheelOverrideDataType = ScheduleOne.Experimental.WheelOverrideData;
using WheelType = ScheduleOne.Vehicles.Wheel;
#endif
using UnityEngine;

namespace DedicatedServerMod.Shared.Patches
{
    internal static class WeatherStabilityLog
    {
        private static readonly HashSet<string> WarningKeys = new HashSet<string>(StringComparer.Ordinal);

        internal static void WarningOnce(string key, string message)
        {
            lock (WarningKeys)
            {
                if (!WarningKeys.Add(key))
                {
                    return;
                }
            }

            DebugLog.Warning(message);
        }

        internal static bool IsHeadlessRuntime()
        {
            return Application.isBatchMode;
        }
    }

    [HarmonyPatch(typeof(WheelType), "Awake")]
    internal static class WheelAwakePatches
    {
#if IL2CPP
        private static void Postfix(WheelType __instance)
        {
            if (__instance == null || __instance._settings != null)
            {
                return;
            }

            __instance._settings = __instance._defaultData?.Settings?.Clone() ?? new VehicleSettingsType();
            WeatherStabilityLog.WarningOnce(
                "wheel-awake-default-settings",
                "Recovered missing wheel settings during Wheel.Awake; vehicle weather friction will use a safe fallback.");
        }
#else
        private static void Postfix(ref VehicleSettingsType ____settings, WheelDataType ____defaultData)
        {
            if (____settings != null)
            {
                return;
            }

            ____settings = ____defaultData?.Settings?.Clone() ?? new VehicleSettingsType();
            WeatherStabilityLog.WarningOnce(
                "wheel-awake-default-settings",
                "Recovered missing wheel settings during Wheel.Awake; vehicle weather friction will use a safe fallback.");
        }
#endif
    }

    [HarmonyPatch(typeof(WheelType), nameof(WheelType.OnWeatherChange))]
    internal static class WheelOnWeatherChangePatches
    {
#if IL2CPP
        private static bool Prefix(
            WheelType __instance,
            WeatherConditionsType newConditions)
        {
            if (__instance == null)
            {
                return false;
            }

            WheelDataType defaultData = __instance._defaultData;
            WheelOverrideDataType rainOverrideData = __instance._rainOverrideData;
            LandVehicleType resolvedVehicle = __instance.vehicle ?? __instance.GetComponentInParent<LandVehicleType>();
            VehicleSettingsType resolvedSettings = defaultData?.Settings?.Clone()
                ?? __instance._settings?.Clone()
                ?? new VehicleSettingsType();

            if (newConditions == null)
            {
                __instance._settings = resolvedSettings;
                WeatherStabilityLog.WarningOnce(
                    "wheel-null-weather-conditions",
                    "Wheel.OnWeatherChange received null weather conditions; keeping default wheel settings.");
                return false;
            }

            bool canApplyRainOverride = newConditions.Rainy > 0f
                && resolvedVehicle != null
                && !resolvedVehicle.IsUnderCover
                && rainOverrideData?.Settings != null;

            if (canApplyRainOverride)
            {
                resolvedSettings = resolvedSettings.Blend(rainOverrideData.Settings, newConditions.Rainy);
            }
            else if (newConditions.Rainy > 0f && rainOverrideData?.Settings == null)
            {
                WeatherStabilityLog.WarningOnce(
                    "wheel-missing-rain-override",
                    "A wheel is missing rain override data after the weather update; using default friction settings.");
            }

            if (resolvedVehicle == null && !WeatherStabilityLog.IsHeadlessRuntime())
            {
                WeatherStabilityLog.WarningOnce(
                    "wheel-missing-vehicle",
                    "A wheel could not resolve its parent vehicle during weather updates; using default friction settings.");
            }

            __instance._settings = resolvedSettings;
            return false;
        }
#else
        private static bool Prefix(
            WheelType __instance,
            WeatherConditionsType newConditions,
            WheelDataType ____defaultData,
            WheelOverrideDataType ____rainOverrideData,
            LandVehicleType ___vehicle,
            ref VehicleSettingsType ____settings)
        {
            VehicleSettingsType resolvedSettings = ____defaultData?.Settings?.Clone()
                ?? ____settings?.Clone()
                ?? new VehicleSettingsType();
            LandVehicleType resolvedVehicle = ___vehicle ?? __instance?.GetComponentInParent<LandVehicleType>();

            if (newConditions == null)
            {
                ____settings = resolvedSettings;
                WeatherStabilityLog.WarningOnce(
                    "wheel-null-weather-conditions",
                    "Wheel.OnWeatherChange received null weather conditions; keeping default wheel settings.");
                return false;
            }

            bool canApplyRainOverride = newConditions.Rainy > 0f
                && resolvedVehicle != null
                && !resolvedVehicle.IsUnderCover
                && ____rainOverrideData?.Settings != null;

            if (canApplyRainOverride)
            {
                resolvedSettings = resolvedSettings.Blend(____rainOverrideData.Settings, newConditions.Rainy);
            }
            else if (newConditions.Rainy > 0f && ____rainOverrideData?.Settings == null)
            {
                WeatherStabilityLog.WarningOnce(
                    "wheel-missing-rain-override",
                    "A wheel is missing rain override data after the weather update; using default friction settings.");
            }

            if (resolvedVehicle == null && !WeatherStabilityLog.IsHeadlessRuntime())
            {
                WeatherStabilityLog.WarningOnce(
                    "wheel-missing-vehicle",
                    "A wheel could not resolve its parent vehicle during weather updates; using default friction settings.");
            }

            ____settings = resolvedSettings;
            return false;
        }
#endif
    }

    [HarmonyPatch(typeof(LandVehicleType), nameof(LandVehicleType.OnWeatherChange))]
    internal static class LandVehicleOnWeatherChangePatches
    {
        private static bool Prefix(LandVehicleType __instance, WeatherConditionsType newConditions)
        {
            if (__instance?.wheels == null || __instance.wheels.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < __instance.wheels.Count; i++)
            {
                WheelType wheel = __instance.wheels[i];
                if (wheel == null)
                {
                    continue;
                }

                try
                {
                    wheel.OnWeatherChange(newConditions);
                }
                catch (Exception ex)
                {
                    WeatherStabilityLog.WarningOnce(
                        "land-vehicle-wheel-weather-exception",
                        $"A vehicle wheel threw during weather updates and was isolated to keep the weather loop alive: {ex.GetType().Name}: {ex.Message}");
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(EnvironmentManagerType), "BlendWeatherProfiles")]
    internal static class EnvironmentManagerBlendWeatherProfilesPatches
    {
        private static readonly AnimationCurve FallbackBlendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

#if IL2CPP
        private static bool Prefix(EnvironmentManagerType __instance)
        {
            if (__instance == null)
            {
                return false;
            }

            SyncList<WeatherVolumeType> activeWeatherVolumes = __instance._activeWeatherVolumes;
            int targetWeatherVolumeIndex = __instance._targetWeatherVolumeIndex;
            int neighbourWeatherVolumeIndex = __instance._neighbourWeatherVolumeIndex;
            bool hasWeatherVolumeNeighbour = __instance._hasWeatherVolumeNeighbour;
            float targetWeatherBlendValue = __instance._targetWeatherBlendValue;
            float neighbourWeatherBlendValue = __instance._neighbourWeatherBlendValue;
            AnimationCurve blendCurve = __instance._blendCurve ?? FallbackBlendCurve;

            if (activeWeatherVolumes == null || activeWeatherVolumes.Count == 0)
            {
                return false;
            }

            if (targetWeatherVolumeIndex < 0 || targetWeatherVolumeIndex >= activeWeatherVolumes.Count)
            {
                for (int i = 0; i < activeWeatherVolumes.Count; i++)
                {
                    WeatherVolumeType volume = activeWeatherVolumes[i];
                    if (volume == null)
                    {
                        continue;
                    }

                    volume.SetNeighbourVolume(null);
                    volume.BlendEffects(0f, blendCurve);
                }

                return false;
            }

            WeatherVolumeType targetVolume = activeWeatherVolumes[targetWeatherVolumeIndex];
            WeatherVolumeType neighbourVolume = null;
            bool hasValidNeighbour = hasWeatherVolumeNeighbour
                && neighbourWeatherVolumeIndex >= 0
                && neighbourWeatherVolumeIndex < activeWeatherVolumes.Count;

            if (hasValidNeighbour)
            {
                neighbourVolume = activeWeatherVolumes[neighbourWeatherVolumeIndex];
                hasValidNeighbour = neighbourVolume != null;
            }

            for (int i = 0; i < activeWeatherVolumes.Count; i++)
            {
                WeatherVolumeType volume = activeWeatherVolumes[i];
                if (volume == null)
                {
                    continue;
                }

                if (i == targetWeatherVolumeIndex)
                {
                    volume.SetNeighbourVolume(hasValidNeighbour ? neighbourVolume : null);
                    volume.BlendEffects(hasValidNeighbour ? Mathf.Clamp01(targetWeatherBlendValue) : 1f, blendCurve);
                    continue;
                }

                if (hasValidNeighbour && i == neighbourWeatherVolumeIndex)
                {
                    volume.SetNeighbourVolume(targetVolume);
                    volume.BlendEffects(Mathf.Clamp01(neighbourWeatherBlendValue), blendCurve);
                    continue;
                }

                volume.SetNeighbourVolume(null);
                volume.BlendEffects(0f, blendCurve);
            }

            return false;
        }
#else
        private static bool Prefix(
            SyncList<WeatherVolumeType> ____activeWeatherVolumes,
            int ____targetWeatherVolumeIndex,
            int ____neighbourWeatherVolumeIndex,
            bool ____hasWeatherVolumeNeighbour,
            float ____targetWeatherBlendValue,
            float ____neighbourWeatherBlendValue,
            AnimationCurve ____blendCurve)
        {
            if (____activeWeatherVolumes == null || ____activeWeatherVolumes.Count == 0)
            {
                return false;
            }

            if (____targetWeatherVolumeIndex < 0 || ____targetWeatherVolumeIndex >= ____activeWeatherVolumes.Count)
            {
                for (int i = 0; i < ____activeWeatherVolumes.Count; i++)
                {
                    WeatherVolumeType volume = ____activeWeatherVolumes[i];
                    if (volume == null)
                    {
                        continue;
                    }

                    volume.SetNeighbourVolume(null);
                    volume.BlendEffects(0f, ____blendCurve ?? FallbackBlendCurve);
                }

                return false;
            }

            WeatherVolumeType targetVolume = ____activeWeatherVolumes[____targetWeatherVolumeIndex];
            WeatherVolumeType neighbourVolume = null;
            bool hasValidNeighbour = ____hasWeatherVolumeNeighbour
                && ____neighbourWeatherVolumeIndex >= 0
                && ____neighbourWeatherVolumeIndex < ____activeWeatherVolumes.Count;

            if (hasValidNeighbour)
            {
                neighbourVolume = ____activeWeatherVolumes[____neighbourWeatherVolumeIndex];
                hasValidNeighbour = neighbourVolume != null;
            }

            AnimationCurve blendCurve = ____blendCurve ?? FallbackBlendCurve;

            for (int i = 0; i < ____activeWeatherVolumes.Count; i++)
            {
                WeatherVolumeType volume = ____activeWeatherVolumes[i];
                if (volume == null)
                {
                    continue;
                }

                if (i == ____targetWeatherVolumeIndex)
                {
                    volume.SetNeighbourVolume(hasValidNeighbour ? neighbourVolume : null);
                    volume.BlendEffects(hasValidNeighbour ? Mathf.Clamp01(____targetWeatherBlendValue) : 1f, blendCurve);
                    continue;
                }

                if (hasValidNeighbour && i == ____neighbourWeatherVolumeIndex)
                {
                    volume.SetNeighbourVolume(targetVolume);
                    volume.BlendEffects(Mathf.Clamp01(____neighbourWeatherBlendValue), blendCurve);
                    continue;
                }

                volume.SetNeighbourVolume(null);
                volume.BlendEffects(0f, blendCurve);
            }

            return false;
        }
#endif
    }
}
