using HarmonyLib;
#if IL2CPP
using Il2CppCorgiGodRays;
using Il2CppFishNet;
#else
using CorgiGodRays;
using FishNet;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    [HarmonyPatch(typeof(GodRaysRenderPass), "Initialize")]
    internal static class GodRaysRenderPassPatches
    {
        private static bool Prefix()
        {
            return !InstanceFinder.IsServer && !UnityEngine.Application.isBatchMode;
        }
    }
}
