using System;
using HarmonyLib;
using DedicatedServerMod.Client.Patchers;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
#else
using FishNet;
using ScheduleOne.Map;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
#endif

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Suppresses ghost-host UI elements that are independent from avatar visibility.
    /// </summary>
    [HarmonyPatch]
    internal static class GhostHostUiPatches
    {
        public static void Initialize()
        {
            DebugLog.StartupDebug("Ghost host UI patches initialized");
        }

        [HarmonyPatch(typeof(POI), "OnEnable")]
        [HarmonyPrefix]
        private static bool PoiOnEnablePrefix(POI __instance)
        {
            if (!ShouldFilterGhostHostUi())
                return true;

            try
            {
                if (!ClientLoopbackHandler.TryGetGhostHostOwner(__instance, out Player player))
                    return true;

                ClientLoopbackHandler.HideLoopbackPresentation(player);
                return false;
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error filtering ghost host POI", ex);
                return true;
            }
        }

        [HarmonyPatch(typeof(DeathScreen), "CanRespawn")]
        [HarmonyPrefix]
        private static bool CanRespawnPrefix(ref bool __result)
        {
            if (!ShouldFilterGhostHostUi())
                return true;

            try
            {
                __result = ClientLoopbackHandler.GetVisiblePlayerCount() > 1;
                return false;
            }
            catch (Exception ex)
            {
                DebugLog.Error("Error filtering ghost host respawn state", ex);
                return true;
            }
        }

        private static bool ShouldFilterGhostHostUi()
        {
            return InstanceFinder.IsClient && !InstanceFinder.IsServer;
        }
    }
}
