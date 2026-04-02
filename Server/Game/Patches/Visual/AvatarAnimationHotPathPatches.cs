using HarmonyLib;
#if IL2CPP
using AvatarFootstepDetectorType = Il2CppScheduleOne.AvatarFramework.Animation.AvatarFootstepDetector;
#else
using AvatarFootstepDetectorType = ScheduleOne.AvatarFramework.Animation.AvatarFootstepDetector;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    /// <summary>
    /// Skips avatar footstep detection and audio trigger work on dedicated servers.
    /// </summary>
    [HarmonyPatch(typeof(AvatarFootstepDetectorType), "LateUpdate")]
    internal static class AvatarFootstepDetectorLateUpdatePatches
    {
        private static bool Prefix()
        {
            return false;
        }
    }
}
