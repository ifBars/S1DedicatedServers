using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using Il2CppFunly.SkyStudio;
using AvatarImpostorType = Il2CppScheduleOne.AvatarFramework.Impostors.AvatarImpostor;
#else
using Funly.SkyStudio;
using AvatarImpostorType = ScheduleOne.AvatarFramework.Impostors.AvatarImpostor;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    /// <summary>
    /// Skips sky system updates on dedicated servers because they only drive client-side visuals.
    /// </summary>
    [HarmonyPatch(typeof(TimeOfDayController), "Update")]
    internal static class TimeOfDayControllerPatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }

    /// <summary>
    /// Skips weather presentation updates on dedicated servers because they only toggle visual effects.
    /// </summary>
    [HarmonyPatch(typeof(WeatherController), "LateUpdate")]
    internal static class WeatherControllerPatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }

    /// <summary>
    /// Skips avatar impostor billboarding on dedicated servers because it only aligns meshes to a camera.
    /// </summary>
    [HarmonyPatch(typeof(AvatarImpostorType), "LateUpdate")]
    internal static class AvatarImpostorPatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }

}
