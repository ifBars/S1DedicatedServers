using HarmonyLib;
using DedicatedServerMod.Server.Game.Patches.Common;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.UI;
#else
using FishNet;
using ScheduleOne.UI;
#endif

namespace DedicatedServerMod.Server.Game.Patches.UI
{
    [HarmonyPatch(typeof(DailySummary), "Awake")]
    internal static class DailySummaryAwakeMessagingPatch
    {
        private static void Postfix(DailySummary __instance)
        {
            try
            {
                Shared.Networking.CustomMessaging.DailySummaryAwakePostfix(__instance);
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"DailySummary.Awake postfix error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(DailySummary), "Awake")]
    internal static class DailySummaryAwakeCanvasPatch
    {
        private static void Postfix(DailySummary __instance)
        {
            if (InstanceFinder.IsServer && __instance.Canvas != null)
            {
                __instance.Canvas.enabled = false;
                DebugLog.Info("Disabled DailySummary canvas on dedicated server (RPCs still registered)");
            }
        }
    }

    [HarmonyPatch(typeof(DailySummary), "Open")]
    internal static class DailySummaryOpenPatches
    {
        private static bool Prefix(DailySummary __instance)
        {
            return !InstanceFinder.IsServer && !UnityEngine.Application.isBatchMode;
        }
    }
}
