using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
#if IL2CPP
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
#else
using PlayerType = ScheduleOne.PlayerScripts.Player;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Player
{
    /// <summary>
    /// Disables movement animation and grounded visual updates for the hidden loopback host on dedicated headless servers.
    /// </summary>
    [HarmonyPatch(typeof(PlayerType), "FixedUpdate")]
    internal static class PlayerFixedUpdatePatches
    {
        private static bool Prefix(PlayerType __instance)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || __instance == null)
            {
                return true;
            }

            return !DedicatedServerPatchCommon.IsGhostOrLoopbackPlayer(__instance);
        }
    }
}
