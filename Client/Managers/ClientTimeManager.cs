using System;
using System.Collections;
using System.Reflection;
using FishNet;
using FishNet.Connection;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using ScheduleOne.PlayerScripts;
using UnityEngine;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Client-side time management patches to ensure proper synchronization with dedicated servers
    /// and prevent 4 AM freezes when time progression should continue.
    /// </summary>
    public static class ClientTimeManager
    {
        private static MelonLogger.Instance logger;
        private static bool _timeNeverStopsOnClient = true; // Configurable option
        
        public static void Initialize(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            ApplyClientTimePatches();
        }

        private static void ApplyClientTimePatches()
        {
            try
            {
                var harmony = new HarmonyLib.Harmony("DedicatedServerMod.Client.TimeManager");
                
                // Patch TimeManager.Tick to prevent 4 AM freezes on clients when connected to dedicated servers
                var timeManagerType = typeof(ScheduleOne.GameTime.TimeManager);
                var tickMethod = timeManagerType.GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                if (tickMethod != null)
                {
                    var prefixMethod = typeof(ClientTimeManager).GetMethod(nameof(ClientTimeManagerTickPrefix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(tickMethod, new HarmonyMethod(prefixMethod));
                    logger.Msg("Patched client TimeManager.Tick to prevent 4 AM freezes on dedicated servers");
                }

                // Patch TimeManager.Update to ensure proper time synchronization requests
                var updateMethod = timeManagerType.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
                if (updateMethod != null)
                {
                    var postfixMethod = typeof(ClientTimeManager).GetMethod(nameof(ClientTimeManagerUpdatePostfix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(updateMethod, postfix: new HarmonyMethod(postfixMethod));
                    logger.Msg("Patched client TimeManager.Update for dedicated server time sync");
                }

                // Patch TimeManager StartSleep RPC to ensure proper time sync on clients
                var startSleepRpcMethod = timeManagerType.GetMethod("RpcLogic___StartSleep_2166136261", BindingFlags.NonPublic | BindingFlags.Instance);
                if (startSleepRpcMethod != null)
                {
                    var postfixMethod = typeof(ClientTimeManager).GetMethod(nameof(ClientStartSleepRpcPostfix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(startSleepRpcMethod, postfix: new HarmonyMethod(postfixMethod));
                    logger.Msg("Patched client TimeManager StartSleep RPC for proper time synchronization");
                }

                // CRITICAL: Patch the SetTimeData_Client RPC logic to log when clients receive time updates
                var setDataRpcMethod = timeManagerType.GetMethod("RpcLogic___SetTimeData_Client_1794730778", BindingFlags.NonPublic | BindingFlags.Instance);
                if (setDataRpcMethod != null)
                {
                    var postfixMethod = typeof(ClientTimeManager).GetMethod(nameof(ClientSetDataRpcPostfix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(setDataRpcMethod, postfix: new HarmonyMethod(postfixMethod));
                    logger.Msg("Patched client SetTimeData_Client RPC to monitor time synchronization");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply client time patches: {ex}");
            }
        }

        /// <summary>
        /// Client-side patch for TimeManager.Tick to prevent 4 AM freezes when connected to dedicated servers
        /// This stopped working in a game update and should probably be removed
        /// </summary>
        private static bool ClientTimeManagerTickPrefix(ScheduleOne.GameTime.TimeManager __instance)
        {
            // Only apply this patch when connected to a dedicated server (not host) and time never stops is enabled
            if (InstanceFinder.IsHost || !InstanceFinder.IsClient || !_timeNeverStopsOnClient)
            {
                return true; // Let original method run
            }

            try
            {
                // Check if this is a 4 AM freeze situation that should be bypassed
                bool wouldFreeze = (__instance.CurrentTime == 400) || 
                                   (__instance.IsCurrentTimeWithinRange(400, 600) && !GameManager.IS_TUTORIAL);

                if (!wouldFreeze)
                {
                    return true; // Let normal tick happen - no freeze would occur
                }

                // Instead of freezing at 4 AM, let the server handle time progression
                // The server will automatically send time updates via SetTimeData_Client RPC
                // NOTE: v0.4.3+ removed SendTimeData, server now pushes time automatically
                if (Player.Local != null)
                {
                    // logger.Msg("Client: Detected 4 AM freeze condition, waiting for server time update");
                }

                return false; // Skip original method - server will handle time advancement
            }
            catch (Exception ex)
            {
                logger.Error($"Error in client TimeManager Tick patch: {ex}");
                return true; // Let original method run as fallback
            }
        }

        // Static field to track last sync request time (CRITICAL FIX)
        private static DateTime _lastSyncRequest = DateTime.MinValue;

        /// <summary>
        /// Client-side postfix for TimeManager.Update to ensure proper time synchronization
        /// </summary>
        private static void ClientTimeManagerUpdatePostfix(ScheduleOne.GameTime.TimeManager __instance)
        {
            // Only apply to clients connected to dedicated servers
            if (InstanceFinder.IsHost || !InstanceFinder.IsClient)
            {
                return;
            }

            try
            {
                // v0.4.3+: Server automatically pushes time data to clients
                // No need to manually request sync - server sends SetTimeData_Client RPC automatically
                // Keeping this method for potential future use
            }
            catch (Exception ex)
            {
                logger.Error($"Error in client TimeManager Update postfix: {ex}");
            }
        }

        /// <summary>
        /// Client-side postfix patch for StartSleep RPC to ensure proper time synchronization after sleep
        /// </summary>
        private static void ClientStartSleepRpcPostfix(ScheduleOne.GameTime.TimeManager __instance)
        {
            // Only apply to clients connected to dedicated servers
            if (InstanceFinder.IsHost || !InstanceFinder.IsClient)
            {
                return;
            }

            try
            {
                logger.Msg("Client: Sleep started, will wait for server time update when sleep ends");
                
                // v0.4.3+: Server automatically sends time updates after sleep via SetTimeData_Client RPC
                // No need to manually request sync
            }
            catch (Exception ex)
            {
                logger.Error($"Error in client StartSleep RPC postfix: {ex}");
            }
        }

        /// <summary>
        /// CRITICAL: Client-side postfix patch for SetTimeData_Client RPC to monitor time synchronization
        /// This helps debug why clients aren't receiving time updates properly after sleep
        /// </summary>
        private static void ClientSetDataRpcPostfix(ScheduleOne.GameTime.TimeManager __instance, NetworkConnection conn, int elapsedDays, int time, uint serverTick)
        {
            try
            {
                logger.Msg($"🕐 CLIENT RPC RECEIVED: SetTimeData_Client called with Day {elapsedDays}, Time {time}, ServerTick {serverTick}");
                logger.Msg($"🕐 CLIENT RPC CONTEXT: IsHost={InstanceFinder.IsHost}, IsClient={InstanceFinder.IsClient}, Connection={conn?.ClientId}");
                logger.Msg($"🕐 CLIENT RPC BEFORE: Instance Day {__instance.ElapsedDays}, Time {__instance.CurrentTime}");
                
                // This runs after the original method, so log the final state
                logger.Msg($"🕐 CLIENT RPC AFTER: Instance Day {__instance.ElapsedDays}, Time {__instance.CurrentTime}");
                
                // Only apply additional logic to clients connected to dedicated servers
                if (!InstanceFinder.IsHost && InstanceFinder.IsClient)
                {
                    // Force HasChanged flag to ensure UI updates
                    __instance.HasChanged = true;
                    logger.Msg("🕐 CLIENT: Set HasChanged=true for UI updates");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in client SetTimeData_Client RPC postfix: {ex}");
            }
        }

        /// <summary>
        /// Gets or sets whether time never stops at 4 AM on clients connected to dedicated servers
        /// </summary>
        public static bool TimeNeverStopsOnClient
        {
            get => _timeNeverStopsOnClient;
            set
            {
                _timeNeverStopsOnClient = value;
                logger?.Msg($"Client time never stops set to: {_timeNeverStopsOnClient}");
            }
        }
    }
}
