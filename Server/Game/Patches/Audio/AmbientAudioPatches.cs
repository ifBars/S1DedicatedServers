using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Utils;
#if IL2CPP
using AmbientOneShotType = Il2CppScheduleOne.Audio.AmbientOneShot;
#else
using AmbientOneShotType = ScheduleOne.Audio.AmbientOneShot;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Audio
{
    /// <summary>
    /// Prevents ambient one-shot audio from subscribing to minute callbacks on dedicated servers.
    /// The native component depends on a local player camera, which does not exist in headless runtime.
    /// </summary>
    [HarmonyPatch(typeof(AmbientOneShotType), "Start")]
    internal static class AmbientOneShotStartPatches
    {
        private static bool _loggedSkip;

        private static bool Prefix()
        {
            if (DedicatedServerPatchCommon.ShouldRunClientPresentationAudio())
            {
                return true;
            }

            if (!_loggedSkip)
            {
                DebugLog.Info("Skipping AmbientOneShot startup on dedicated headless server.");
                _loggedSkip = true;
            }

            return false;
        }
    }

    /// <summary>
    /// Defensively suppresses ambient one-shot minute processing on dedicated servers.
    /// This covers any native execution path that reaches the callback without going through Start().
    /// </summary>
    [HarmonyPatch(typeof(AmbientOneShotType), "OnUncappedMinPass")]
    internal static class AmbientOneShotMinutePassPatches
    {
        private static bool Prefix()
        {
            return DedicatedServerPatchCommon.ShouldRunClientPresentationAudio();
        }
    }
}
