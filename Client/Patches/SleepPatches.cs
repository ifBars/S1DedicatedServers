using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
#else
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
#endif
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Client.Patches
{
    /// <summary>
    /// Harmony patches for the sleep system to support dedicated servers.
    /// Filters out the ghost loopback host player from sleep readiness checks
    /// and enforces the server's AllowSleeping configuration.
    /// Uses centralized ghost host detection for dedicated-server loopback filtering.
    /// </summary>
    internal static class SleepPatches
    {
        private static bool _ignoreGhostHostForSleep = true;

        internal static void Initialize()
        {
            DebugLog.StartupDebug("Sleep patches initialized");
        }

        internal static bool IgnoreGhostHostForSleep
        {
            get => _ignoreGhostHostForSleep;
            set
            {
                _ignoreGhostHostForSleep = value;
                DebugLog.Debug($"SleepPatches: Ignore ghost host set to: {_ignoreGhostHostForSleep}");
            }
        }

        [HarmonyPatch]
        private static class AreAllPlayersReadyToSleepPatch
        {
            private static readonly MethodBase SleepReadinessMethod = ResolveTargetMethod();

            [HarmonyPrepare]
            private static bool Prepare()
            {
                return SleepReadinessMethod != null;
            }

            [HarmonyTargetMethod]
            private static MethodBase TargetMethod()
            {
                return SleepReadinessMethod;
            }

            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                if (!_ignoreGhostHostForSleep || !FishNet.InstanceFinder.IsClient)
                {
                    return true;
                }

                try
                {
                    var playerList = Player.PlayerList;
                    if (playerList.Count == 0)
                    {
                        __result = false;
                        return false;
                    }

                    int eligiblePlayers = 0;

                    for (int i = 0; i < playerList.Count; i++)
                    {
                        var player = playerList[i];
                        if (player == null)
                        {
                            continue;
                        }

                        if (player.IsGhostHost())
                        {
                            continue;
                        }

                        eligiblePlayers++;

                        if (!player.IsReadyToSleep)
                        {
                            __result = false;
                            return false;
                        }
                    }

                    if (eligiblePlayers == 0)
                    {
                        __result = false;
                        return false;
                    }

                    __result = true;
                    return false;
                }
                catch (Exception ex)
                {
                    DebugLog.Error($"Error in AreAllPlayersReadyToSleep patch: {ex}");
                    return true;
                }
            }

            private static MethodBase ResolveTargetMethod()
            {
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                return typeof(PlayerManager).GetMethod("AreAllPlayersReadyToSleep", flags)
                    ?? typeof(Player).GetMethod("AreAllPlayersReadyToSleep", flags);
            }
        }

        [HarmonyPatch]
        private static class SleepCanvasOpenPatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> TargetMethods()
            {
                var setIsOpen = AccessTools.Method(typeof(SleepCanvas), "SetIsOpen");
                if (setIsOpen != null)
                {
                    yield return setIsOpen;
                }

                var openMenu = AccessTools.Method(typeof(SleepCanvas), "OpenMenu");
                if (openMenu != null)
                {
                    yield return openMenu;
                }
            }

            [HarmonyPrefix]
            private static bool Prefix(MethodBase __originalMethod, object[] __args)
            {
                bool opensCanvas = __originalMethod.Name != "SetIsOpen"
                    || (__args.Length > 0 && __args[0] is bool open && open);

                if (!opensCanvas)
                {
                    return true;
                }

                return SleepCanvasOpenPrefix();
            }
        }

        private static bool SleepCanvasOpenPrefix()
        {
            try
            {
                if (FishNet.InstanceFinder.IsClient && !FishNet.InstanceFinder.IsHost)
                {
                    if (!Managers.ServerDataStore.AllowSleeping)
                    {
                        DebugLog.Debug("Server has disabled sleeping; suppressing SleepCanvas open");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"SleepCanvas_SetIsOpen_Prefix error: {ex.Message}");
                return true;
            }
        }
    }
}
