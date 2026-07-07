using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
using System.Collections.Generic;
using System.Reflection;
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
    [HarmonyPatch]
    internal static class PlayerFixedUpdatePatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase movementVisuals = typeof(PlayerType).GetMethod(
                "ApplyMovementVisuals",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (movementVisuals != null)
            {
                yield return movementVisuals;
                yield break;
            }

            MethodBase fixedUpdate = typeof(PlayerType).GetMethod(
                "FixedUpdate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fixedUpdate != null)
            {
                yield return fixedUpdate;
            }
        }

        [HarmonyPrefix]
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
