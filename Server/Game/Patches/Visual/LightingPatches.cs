using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
#if IL2CPP
using BlinkingLightType = Il2CppScheduleOne.Lighting.BlinkingLight;
using FlickeringLightType = Il2CppScheduleOne.Lighting.FlickeringLight;
using PoliceLightType = Il2CppScheduleOne.Lighting.PoliceLight;
using ReflectionProbeUpdaterType = Il2CppScheduleOne.Lighting.ReflectionProbeUpdater;
#else
using BlinkingLightType = ScheduleOne.Lighting.BlinkingLight;
using FlickeringLightType = ScheduleOne.Lighting.FlickeringLight;
using PoliceLightType = ScheduleOne.Lighting.PoliceLight;
using ReflectionProbeUpdaterType = ScheduleOne.Lighting.ReflectionProbeUpdater;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    /// <summary>
    /// Disables decorative flickering-light animation on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(FlickeringLightType), "Start")]
    internal static class FlickeringLightStartPatches
    {
        private static void Postfix(FlickeringLightType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __instance == null)
            {
                return;
            }

            __instance.enabled = false;
        }
    }

    /// <summary>
    /// Disables decorative blinking-light animation on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(BlinkingLightType), "Awake")]
    internal static class BlinkingLightAwakePatches
    {
        private static void Postfix(BlinkingLightType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __instance == null)
            {
                return;
            }

            __instance.enabled = false;
        }
    }

    /// <summary>
    /// Disables police light cycling and siren-driven visual updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(PoliceLightType), "SetIsOn")]
    internal static class PoliceLightSetIsOnPatches
    {
        private static bool Prefix(PoliceLightType __instance, ref bool isOn)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __instance == null)
            {
                return true;
            }

            __instance.IsOn = false;
            __instance.enabled = false;
            isOn = false;
            return false;
        }
    }

    /// <summary>
    /// Disables reflection probe refresh scheduling on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(ReflectionProbeUpdaterType), "Start")]
    internal static class ReflectionProbeUpdaterStartPatches
    {
        private static bool Prefix(ReflectionProbeUpdaterType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __instance == null)
            {
                return true;
            }

            __instance.enabled = false;
            return false;
        }
    }
}
