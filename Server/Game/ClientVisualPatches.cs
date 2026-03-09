using HarmonyLib;
#if IL2CPP
using Il2CppFunly.SkyStudio;
using AvatarType = Il2CppScheduleOne.AvatarFramework.Avatar;
using AvatarImpostorType = Il2CppScheduleOne.AvatarFramework.Impostors.AvatarImpostor;
using NpcAnimationType = Il2CppScheduleOne.NPCs.NPCAnimation;
#else
using Funly.SkyStudio;
using AvatarType = ScheduleOne.AvatarFramework.Avatar;
using AvatarImpostorType = ScheduleOne.AvatarFramework.Impostors.AvatarImpostor;
using NpcAnimationType = ScheduleOne.NPCs.NPCAnimation;
#endif

namespace DedicatedServerMod.Server.Game
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

    /// <summary>
    /// Skips per-frame avatar shape-key updates on dedicated servers because they only depend on local camera LOD.
    /// </summary>
    [HarmonyPatch(typeof(AvatarType), "LateUpdate")]
    internal static class AvatarPatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }

    /// <summary>
    /// Skips NPC movement animation updates on dedicated servers because they only feed animator parameters.
    /// </summary>
    [HarmonyPatch(typeof(NpcAnimationType), "LateUpdate")]
    internal static class NpcAnimationPatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientVisuals();
        }
    }
}
