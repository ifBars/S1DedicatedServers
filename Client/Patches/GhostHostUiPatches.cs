using System;
using HarmonyLib;
using DedicatedServerMod.Client.Patchers;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.PlayerScripts;
#else
using FishNet;
using ScheduleOne.Map;
using ScheduleOne.PlayerScripts;
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
            return ShouldAllowPoiLifecycle(__instance, "OnEnable");
        }

        [HarmonyPatch(typeof(POI), nameof(POI.InitializeUI))]
        [HarmonyPrefix]
        private static bool PoiInitializeUiPrefix(POI __instance)
        {
            return ShouldAllowPoiLifecycle(__instance, nameof(POI.InitializeUI));
        }

        private static bool ShouldFilterGhostHostUi()
        {
            return InstanceFinder.IsClient && !InstanceFinder.IsServer;
        }

        private static bool ShouldAllowPoiLifecycle(POI poi, string stage)
        {
            if (!ShouldFilterGhostHostUi())
                return true;

            try
            {
                if (!ClientLoopbackHandler.TryGetGhostHostOwner(poi, out Player player))
                    return true;

                DebugLog.Debug($"Suppressing ghost host POI during {stage}");
                ClientLoopbackHandler.HideLoopbackPresentation(player);
                return false;
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error filtering ghost host POI during {stage}", ex);
                return true;
            }
        }
    }
}
