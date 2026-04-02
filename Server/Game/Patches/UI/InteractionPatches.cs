using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using InteractionManagerType = Il2CppScheduleOne.Interaction.InteractionManager;
#else
using InteractionManagerType = ScheduleOne.Interaction.InteractionManager;
#endif

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    /// <summary>
    /// Skips local interaction polling on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(InteractionManagerType), "LateUpdate")]
    internal static class InteractionManagerLateUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }

    /// <summary>
    /// Skips local interaction input polling on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(InteractionManagerType), "Update")]
    internal static class InteractionManagerUpdatePatches
    {
        private static bool Prefix()
        {
            return !DedicatedServerPatchCommon.IsDedicatedHeadlessServer();
        }
    }
}
