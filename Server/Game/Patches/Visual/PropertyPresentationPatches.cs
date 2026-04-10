using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using TapType = Il2CppScheduleOne.Property.Tap;
#else
using TapType = ScheduleOne.Property.Tap;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    /// <summary>
    /// Skips tap particle and handle visual updates on dedicated headless servers while preserving flow-rate state.
    /// </summary>
    [HarmonyPatch(typeof(TapType), "UpdateTapVisuals")]
    internal static class TapUpdateTapVisualsPatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

    /// <summary>
    /// Skips tap running-water audio updates on dedicated headless servers while preserving flow-rate state.
    /// </summary>
    [HarmonyPatch(typeof(TapType), "UpdateWaterSound")]
    internal static class TapUpdateWaterSoundPatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }
}
