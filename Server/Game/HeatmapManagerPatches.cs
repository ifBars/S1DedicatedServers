using HarmonyLib;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Heatmap;
#else
using FishNet;
using ScheduleOne.Heatmap;
#endif

namespace DedicatedServerMod.Server.Game
{
    [HarmonyPatch(typeof(HeatmapManager), "Start")]
    internal static class HeatmapManagerPatches
    {
        private static bool Prefix()
        {
            return !InstanceFinder.IsServer && !UnityEngine.Application.isBatchMode;
        }
    }
}
