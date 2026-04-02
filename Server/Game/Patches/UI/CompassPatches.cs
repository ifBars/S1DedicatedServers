using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using CompassManagerType = Il2CppScheduleOne.UI.Compass.CompassManager;
#else
using CompassManagerType = ScheduleOne.UI.Compass.CompassManager;
#endif

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    /// <summary>
    /// Skips compass presentation updates on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(CompassManagerType), "Update")]
    internal static class CompassManagerUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }
}
