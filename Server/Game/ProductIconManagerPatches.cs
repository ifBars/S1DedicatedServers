using HarmonyLib;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Product;
#else
using FishNet;
using ScheduleOne.Product;
#endif

namespace DedicatedServerMod.Server.Game
{
    [HarmonyPatch(typeof(ProductIconManager), "GenerateIcons")]
    internal static class ProductIconManagerPatches
    {
        private static bool Prefix()
        {
            return !InstanceFinder.IsServer && !UnityEngine.Application.isBatchMode;
        }
    }
}
