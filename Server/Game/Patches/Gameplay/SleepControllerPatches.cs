using DedicatedServerMod.Server.Game.Patches.Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
#if IL2CPP
using Il2CppFishNet;
#else
using FishNet;
#endif
#if IL2CPP
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.SleepMessage;
using Il2CppScheduleOne.SpecialCustomers.UI;
using PlayerType = Il2CppScheduleOne.PlayerScripts.Player;
#else
using ScheduleOne.GameTime;
using ScheduleOne.UI;
using ScheduleOne.UI.SleepMessage;
using ScheduleOne.SpecialCustomers.UI;
using PlayerType = ScheduleOne.PlayerScripts.Player;
#endif

namespace DedicatedServerMod.Server.Game.Patches.Gameplay
{
    /// <summary>
    /// Starts at most one sleep cycle per set of player readiness votes, excluding an empty server.
    /// </summary>
    [HarmonyPatch]
    internal static class SleepControllerStartPatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SleepController), nameof(SleepController.StartSleep));
            // IL2CPP can inline the public wrapper. The generated implementation is also
            // reached by native callers, so readiness must be consumed at this boundary.
            foreach (var method in typeof(SleepController).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name.StartsWith("RpcLogic___StartSleep_", System.StringComparison.Ordinal)
                    && method.GetParameters().Length == 0)
                {
                    yield return method;
                }
            }
        }

        private static bool Prefix(SleepController __instance)
        {
            if (!InstanceFinder.IsServer)
            {
                return true;
            }

            if (__instance.IsSleepInProgress || DedicatedServerPatchCommon.CountSleepEligiblePlayers() == 0)
            {
                return false;
            }

            // The headless host finishes before clients dismiss their sleep screens. Consume the
            // synchronized votes now so CheckSleepStart cannot immediately advance another day.
            foreach (var player in PlayerType.PlayerList)
            {
                if (player != null)
                {
                    player.IsReadyToSleep = false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Prevents the headless host from waiting for input on sleep presentation screens.
    /// Native sleep processing still advances time, saves, and runs gameplay events.
    /// </summary>
    [HarmonyPatch]
    internal static class SleepControllerPresentationPatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SleepController), nameof(SleepController.AddSleepEvent));
            yield return AccessTools.Method(typeof(SleepController), nameof(SleepController.AddPostSleepEvent));
        }

        private static bool Prefix(ISleepEvent sleepEvent)
        {
            if (!DedicatedServerPatchCommon.IsDedicatedHeadlessServer() || sleepEvent == null)
            {
                return true;
            }

#if IL2CPP
            // Interop interface wrappers require a native cast to identify the implementing type.
            return sleepEvent.TryCast<DailySummary>() == null
                && sleepEvent.TryCast<RankUpCanvas>() == null
                && sleepEvent.TryCast<RegionUnlockedCanvas>() == null
                && sleepEvent.TryCast<IncomingSpecialCustomerInfoPopup>() == null
                && sleepEvent.TryCast<SleepMessage>() == null;
#else
            return sleepEvent is not DailySummary
                && sleepEvent is not RankUpCanvas
                && sleepEvent is not RegionUnlockedCanvas
                && sleepEvent is not IncomingSpecialCustomerInfoPopup
                && sleepEvent is not SleepMessage;
#endif
        }
    }
}
