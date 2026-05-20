using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
#if IL2CPP
using GrowContainerType = Il2CppScheduleOne.Growing.GrowContainer;
#else
using GrowContainerType = ScheduleOne.Growing.GrowContainer;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Visual
{
    /// <summary>
    /// Skips grow-container soil mesh and material refreshes on dedicated headless servers.
    /// Authoritative soil state still changes and replicates; only server-local presentation work is bypassed.
    /// </summary>
    [HarmonyPatch(typeof(GrowContainerType), "RefreshSoilVisuals")]
    internal static class GrowContainerPresentationPatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }
}
