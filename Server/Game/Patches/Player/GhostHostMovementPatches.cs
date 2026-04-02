using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using PlayerMovementType = Il2CppScheduleOne.PlayerScripts.PlayerMovement;
#else
using PlayerMovementType = ScheduleOne.PlayerScripts.PlayerMovement;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Player
{
    /// <summary>
    /// Disables local movement work for the hidden loopback host on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(PlayerMovementType), "Update")]
    internal static class GhostHostMovementUpdatePatches
    {
        private static bool Prefix(PlayerMovementType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __instance == null)
            {
                return true;
            }

            return !DedicatedServerPatchCommon.IsGhostOrLoopbackPlayer(__instance.Player);
        }
    }

    /// <summary>
    /// Disables fixed-step grounded checks for the hidden loopback host on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(PlayerMovementType), "FixedUpdate")]
    internal static class GhostHostMovementFixedUpdatePatches
    {
        private static bool Prefix(PlayerMovementType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __instance == null)
            {
                return true;
            }

            return !DedicatedServerPatchCommon.IsGhostOrLoopbackPlayer(__instance.Player);
        }
    }
}
