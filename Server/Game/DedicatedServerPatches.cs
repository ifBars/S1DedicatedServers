using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using FishNet;
using FishNet.Component.Scenes;
using FishNet.Connection;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using UnityEngine;

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Centralized Harmony patches migrated from the legacy DedicatedServerHost implementation.
    /// These patches delegate to the new modular systems in ServerBootstrap and managers.
    /// </summary>
    internal static class DedicatedServerPatches
    {
        private static readonly MelonLogger.Instance logger = new MelonLogger.Instance("DedicatedServerPatches");

        // ------- Multipass.Initialize: ensure Tugboat exists and is added to transports -------
        [HarmonyPatch(typeof(Multipass), "Initialize")]
        private static class Multipass_Initialize_Prefix
        {
            private static void Prefix(Multipass __instance)
            {
                try
                {
                    var tugboat = __instance.gameObject.GetComponent<Tugboat>();
                    if (tugboat == null)
                    {
                        tugboat = __instance.gameObject.AddComponent<Tugboat>();
                        logger.Msg("Added Tugboat component to Multipass for server");

                        // Add to internal transports list if present
                        var transportsField = typeof(Multipass).GetField("_transports", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (transportsField?.GetValue(__instance) is List<Transport> transports)
                        {
                            if (!transports.Contains(tugboat))
                                transports.Add(tugboat);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Error in Multipass.Initialize patch: {ex}");
                }
            }
        }

        // ------- LoadManager: keep original flow but ensure transport ready before load -------
        [HarmonyPatch(typeof(LoadManager), "StartGame")]
        private static class LoadManager_StartGame_Prefix
        {
            private static void Prefix(LoadManager __instance)
            {
                try
                {
                    var networkManager = InstanceFinder.NetworkManager;
                    var mp = networkManager?.TransportManager?.Transport as Multipass;
                    if (mp == null) return;

                    var tug = mp.gameObject.GetComponent<Tugboat>();
                    if (tug == null) 
                    {
                        var clientField = typeof(Multipass).GetField("_clientTransport", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (clientField != null)
                            clientField.SetValue(mp, tug);
                        tug = mp.gameObject.AddComponent<Tugboat>();
                    }
                        
                    tug.SetPort((ushort)ServerConfig.Instance.ServerPort);
                }
                catch (Exception ex)
                {
                    logger.Warning($"LoadManager.StartGame transport prep failed: {ex.Message}");
                }
            }
        }

        // ------- Player identity binding when name data arrives -------
        // The ReceivePlayerNameData method has a mangled name; dynamic patching is handled in GamePatchManager.
        public static void BindPlayerIdentityPostfix(ScheduleOne.PlayerScripts.Player __instance, NetworkConnection conn, string playerName, string id)
        {
            try
            {
                if (!InstanceFinder.IsServer) return;
                var targetConn = conn ?? __instance.Owner;
                if (targetConn == null) return;
                DedicatedServerMod.Server.Player.PlayerManager pm = Server.Core.ServerBootstrap.Players;
                pm?.SetPlayerIdentity(targetConn, id, playerName);
            }
            catch (Exception ex)
            {
                logger.Error($"Error binding player identity: {ex}");
            }
        }

        // ------- Player initialization postfix: ensure per-client setup if needed -------
        [HarmonyPatch(typeof(ScheduleOne.PlayerScripts.Player), nameof(ScheduleOne.PlayerScripts.Player.MarkPlayerInitialized))]
        private static class Player_MarkPlayerInitialized_Postfix
        {
            private static void Postfix(ScheduleOne.PlayerScripts.Player __instance)
            {
                // Currently no-op; hook kept for parity and future per-client sync
            }
        }

        // ------- Player disconnect -> trigger save if configured -------
        [HarmonyPatch(typeof(ScheduleOne.PlayerScripts.Player), "OnDestroy")]
        private static class Player_OnDestroy_Prefix
        {
            private static void Prefix(ScheduleOne.PlayerScripts.Player __instance)
            {
                try
                {
                    if (!InstanceFinder.IsServer || !ServerConfig.Instance.AutoSaveOnPlayerLeave) return;
                    if (__instance?.gameObject?.name == "[DedicatedServerHostLoopback]") return;
                    Server.Core.ServerBootstrap.Persistence?.TriggerAutoSave($"player_disconnect_{__instance?.PlayerName}");
                }
                catch (Exception ex)
                {
                    logger.Warning($"Player.OnDestroy save trigger error: {ex.Message}");
                }
            }
        }

        // ------- TimeManager patches: prevent 4AM freeze and sync time -------
        [HarmonyPatch(typeof(TimeManager), "Tick")]
        private static class TimeManager_Tick_PrefixPostfix
        {
            private static bool Prefix(TimeManager __instance)
            {
                if (!InstanceFinder.IsServer || !ServerConfig.Instance.TimeNeverStops)
                    return true;

                try
                {
                    // Skip freeze window by advancing immediately
                    bool wouldFreeze = __instance.CurrentTime == 400 || (__instance.IsCurrentTimeWithinRange(400, 600) && !GameManager.IS_TUTORIAL);
                    if (!wouldFreeze) return true;

                    // Advance time alike original, condensed
                    __instance.TimeOnCurrentMinute = 0f;
                    if (__instance.CurrentTime == 2359)
                    {
                        __instance.ElapsedDays++;
                        __instance.CurrentTime = 0;
                        __instance.DailyMinTotal = 0;
                        __instance.onDayPass?.Invoke();
                        __instance.onHourPass?.Invoke();
                        if (__instance.CurrentDay == EDay.Monday && __instance.onWeekPass != null)
                            __instance.onWeekPass();
                    }
                    else if (__instance.CurrentTime % 100 >= 59)
                    {
                        __instance.CurrentTime += 41;
                        __instance.onHourPass?.Invoke();
                    }
                    else
                    {
                        __instance.CurrentTime++;
                    }
                    __instance.DailyMinTotal = TimeManager.GetMinSumFrom24HourTime(__instance.CurrentTime);
                    __instance.HasChanged = true;
                    return false; // handled
                }
                catch (Exception ex)
                {
                    logger.Warning($"TimeManager.Tick prefix error: {ex.Message}");
                    return true;
                }
            }

            private static void Postfix(TimeManager __instance)
            {
                if (!InstanceFinder.IsServer) return;
                try
                {
                    // Broadcast hourly and critical boundaries to clients
                    int minutes = __instance.CurrentTime % 100;
                    bool isTopOfHour = minutes == 0;
                    bool isCritical = __instance.CurrentTime == 400 || __instance.CurrentTime == 700;
                    if (!isTopOfHour && !isCritical) return;

                    var nm = InstanceFinder.NetworkManager;
                    var sm = nm?.ServerManager;
                    if (sm == null) return;
                    foreach (var kvp in sm.Clients)
                    {
                        var client = kvp.Value;
                        if (client == null || client.IsLocalClient) continue;
                        __instance.SendTimeData(client);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"TimeManager.Tick postfix error: {ex.Message}");
                }
            }
        }

        // ------- Ensure server never treats itself as tutorial when saving -------
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.IsTutorial), MethodType.Getter)]
        private static class GameManager_IsTutorial_Getter_Prefix
        {
            private static bool Prefix(ref bool __result)
            {
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(TimeManager), "Update")]
        private static class TimeManager_Update_Prefix
        {
            private static bool Prefix(TimeManager __instance)
            {
                if (!InstanceFinder.IsServer) return true;
                // Avoid try+yield within this method; keep simple and safe
                if (__instance.SleepInProgress)
                {
                    var sleepEndTimeField = typeof(TimeManager).GetField("sleepEndTime", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (sleepEndTimeField != null)
                    {
                        var sleepEndTime = (int)sleepEndTimeField.GetValue(__instance);
                        if (__instance.IsCurrentTimeWithinRange(sleepEndTime, TimeManager.AddMinutesTo24HourTime(sleepEndTime, 60)))
                        {
                            var endSleep = typeof(TimeManager).GetMethod("EndSleep", BindingFlags.NonPublic | BindingFlags.Instance);
                            endSleep?.Invoke(__instance, null);
                        }
                    }
                }
                else if (ScheduleOne.PlayerScripts.Player.AreAllPlayersReadyToSleep())
                {
                    var startSleep = typeof(TimeManager).GetMethod("StartSleep", BindingFlags.NonPublic | BindingFlags.Instance);
                    startSleep?.Invoke(__instance, null);
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(TimeManager), nameof(TimeManager.FastForwardToWakeTime))]
        private static class TimeManager_FastForwardToWakeTime_Postfix
        {
            private static void Postfix(TimeManager __instance)
            {
                if (!InstanceFinder.IsServer) return;
                try
                {
                    // Save after sleep if enabled
                    if (ServerConfig.Instance.AutoSaveEnabled)
                    {
                        Server.Core.ServerBootstrap.Persistence?.TriggerAutoSave("post_sleep");
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"TimeManager.FastForwardToWakeTime postfix error: {ex.Message}");
                }
            }
        }

        // ------- Console permissions (patched dynamically to avoid overload ambiguity) -------
        public static bool ConsoleSubmitCommand_Prefix(List<string> args)
        {
            if (args == null || args.Count == 0) return true;
            if (!(InstanceFinder.IsServer && !InstanceFinder.IsHost)) return true;

            var local = ScheduleOne.PlayerScripts.Player.Local;
            if (local == null) return true; // server console
            string cmd = args[0]?.ToLower() ?? string.Empty;
            if (!ServerConfig.CanUseConsole(local)) return false;
            if (!ServerConfig.CanUseCommand(local, cmd)) return false;
            return true;
        }

        // ------- DailySummary RPC registration hook -------
        [HarmonyPatch(typeof(DailySummary), "Awake")]
        private static class DailySummary_Awake_Postfix
        {
            private static void Postfix(DailySummary __instance)
            {
                try
                {
                    DedicatedServerMod.Shared.CustomMessaging.DailySummaryAwakePostfix(__instance);
                }
                catch (Exception ex)
                {
                    logger.Warning($"DailySummary.Awake postfix error: {ex.Message}");
                }
            }
        }

        // ------- Sleep system: ignore loopback ghost host when checking readiness -------
        [HarmonyPatch(typeof(ScheduleOne.PlayerScripts.Player), nameof(ScheduleOne.PlayerScripts.Player.AreAllPlayersReadyToSleep))]
        private static class Player_AreAllPlayersReadyToSleep_Prefix
        {
            private static bool Prefix(ref bool __result)
            {
                if (!InstanceFinder.IsServer || !ServerConfig.Instance.IgnoreGhostHostForSleep)
                    return true; // run original

                try
                {
                    var list = ScheduleOne.PlayerScripts.Player.PlayerList;
                    if (list == null || list.Count == 0)
                    {
                        __result = false;
                        return false; // skip original
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        var p = list[i];
                        if (p == null) continue;
                        if (p.gameObject?.name == "[DedicatedServerHostLoopback]")
                            continue; // ignore ghost host
                        if (!p.IsReadyToSleep)
                        {
                            __result = false;
                            return false;
                        }
                    }

                    __result = true;
                    return false; // handled
                }
                catch (Exception ex)
                {
                    logger.Warning($"AreAllPlayersReadyToSleep prefix error: {ex.Message}");
                    return true;
                }
            }
        }
    }
}


