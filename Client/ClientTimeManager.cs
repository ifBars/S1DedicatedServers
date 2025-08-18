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

namespace DedicatedServerMod.Client
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

                // Patch TimeManager sleep end RPC to ensure proper time sync on clients
                var endSleepRpcMethod = timeManagerType.GetMethod("RpcLogic___EndSleep_2166136261", BindingFlags.NonPublic | BindingFlags.Instance);
                if (endSleepRpcMethod != null)
                {
                    var postfixMethod = typeof(ClientTimeManager).GetMethod(nameof(ClientEndSleepRpcPostfix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(endSleepRpcMethod, postfix: new HarmonyMethod(postfixMethod));
                    logger.Msg("Patched client TimeManager EndSleep RPC for proper time synchronization");
                }

                // Patch FastForwardToWakeTime on clients to ensure time sync
                var fastForwardMethod = timeManagerType.GetMethod("FastForwardToWakeTime", BindingFlags.Public | BindingFlags.Instance);
                if (fastForwardMethod != null)
                {
                    var postfixMethod = typeof(ClientTimeManager).GetMethod(nameof(ClientFastForwardPostfix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(fastForwardMethod, postfix: new HarmonyMethod(postfixMethod));
                    logger.Msg("Patched client FastForwardToWakeTime for dedicated server time sync");
                }

                // CRITICAL: Patch the SetData RPC logic to log when clients receive time updates
                var setDataRpcMethod = timeManagerType.GetMethod("RpcLogic___SetData_2661156041", BindingFlags.NonPublic | BindingFlags.Instance);
                if (setDataRpcMethod != null)
                {
                    var postfixMethod = typeof(ClientTimeManager).GetMethod(nameof(ClientSetDataRpcPostfix), 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(setDataRpcMethod, postfix: new HarmonyMethod(postfixMethod));
                    logger.Msg("Patched client SetData RPC to monitor time synchronization");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to apply client time patches: {ex}");
            }
        }

        /// <summary>
        /// Client-side patch for TimeManager.Tick to prevent 4 AM freezes when connected to dedicated servers
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
                // Request time synchronization from the server
                if (Player.Local != null)
                {
                    // logger.Msg("Client: Detected 4 AM freeze condition, deferring to dedicated server time progression");
                    
                    // Reset the time progression accumulator to prevent client-side advancement
                    __instance.TimeOnCurrentMinute = 0f;
                    
                    // Request time sync from server instead of advancing locally
                    var sendTimeDataMethod = typeof(ScheduleOne.GameTime.TimeManager).GetMethod("SendTimeData", 
                        BindingFlags.Public | BindingFlags.Instance);
                    if (sendTimeDataMethod != null)
                    {
                        sendTimeDataMethod.Invoke(__instance, new object[] { null });
                    }
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
                // Periodically request time sync from dedicated server to ensure accuracy
                // This helps prevent drift between client and server time
                var now = DateTime.Now;
                
                if ((now - _lastSyncRequest).TotalMinutes >= 1.0) // Sync every minute
                {
                    _lastSyncRequest = now;
                    
                    // Request fresh time data from the server
                    var sendTimeDataMethod = typeof(ScheduleOne.GameTime.TimeManager).GetMethod("SendTimeData", 
                        BindingFlags.Public | BindingFlags.Instance);
                    if (sendTimeDataMethod != null)
                    {
                        sendTimeDataMethod.Invoke(__instance, new object[] { null });
                        logger?.Msg("Client: Requested periodic time sync from dedicated server");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in client TimeManager Update postfix: {ex}");
            }
        }

        /// <summary>
        /// Client-side postfix patch for EndSleep RPC to ensure proper time synchronization after sleep
        /// </summary>
        private static void ClientEndSleepRpcPostfix(ScheduleOne.GameTime.TimeManager __instance)
        {
            // Only apply to clients connected to dedicated servers
            if (InstanceFinder.IsHost || !InstanceFinder.IsClient)
            {
                return;
            }

            try
            {
                logger.Msg("Client: Sleep ended, aggressively requesting time synchronization from dedicated server");
                
                // Start a coroutine to repeatedly request time sync until we get updated time
                MelonCoroutines.Start(AggressiveTimeSyncAfterSleep(__instance));
            }
            catch (Exception ex)
            {
                logger.Error($"Error in client EndSleep RPC postfix: {ex}");
            }
        }

        /// <summary>
        /// Aggressively requests time synchronization from server until the client receives updated time
        /// </summary>
        private static IEnumerator AggressiveTimeSyncAfterSleep(ScheduleOne.GameTime.TimeManager timeManager)
        {
            var initialTime = timeManager.CurrentTime;
            var initialDays = timeManager.ElapsedDays;
            
            logger.Msg($"Client: Starting aggressive time sync - initial time: Day {initialDays}, Time {initialTime}");
            
            // Try for up to 5 seconds to get time sync (reduced since server should send data now)
            for (int attempt = 0; attempt < 10; attempt++)
            {
                // CRITICAL FIX: Clients can't call SendTimeData (server-only method)
                // Instead, wait for server to push time data to us
                bool syncRequested = false;
                try
                {
                    logger.Msg($"Client: Time sync attempt {attempt + 1}/10 - waiting for server time data");
                    
                    // Method 1: Force HasChanged to trigger potential internal sync mechanisms
                    var hasChangedField = typeof(ScheduleOne.GameTime.TimeManager).GetProperty("HasChanged", 
                        BindingFlags.Public | BindingFlags.Instance);
                    if (hasChangedField != null)
                    {
                        hasChangedField.SetValue(timeManager, true);
                    }
                    
                    // Method 2: Access time properties to potentially trigger sync
                    var _ = timeManager.CurrentTime;
                    var __ = timeManager.ElapsedDays;
                    
                    syncRequested = true;
                    logger.Msg($"Client: Triggered internal sync mechanisms for attempt {attempt + 1}/10");
                }
                catch (Exception ex)
                {
                    logger.Error($"Error in aggressive time sync attempt {attempt}: {ex}");
                }
                
                // Check if time has been updated (should advance to next day after sleep)
                bool timeUpdated = false;
                try
                {
                    // For sleep, we expect either:
                    // 1. Days to advance (most common case)
                    // 2. If same day, time should be morning wake time (700) and we started after noon
                    bool dayAdvanced = timeManager.ElapsedDays > initialDays;
                    bool timeAdvancedToMorning = (timeManager.ElapsedDays == initialDays && 
                                                 timeManager.CurrentTime >= 700 && 
                                                 initialTime >= 1200); // Only if we started in afternoon/evening
                    
                    timeUpdated = dayAdvanced || timeAdvancedToMorning;
                    
                    logger.Msg($"Client: Time check - Days: {initialDays}→{timeManager.ElapsedDays}, Time: {initialTime}→{timeManager.CurrentTime}, Updated: {timeUpdated}");
                }
                catch (Exception ex)
                {
                    logger.Error($"Error checking time update status in attempt {attempt}: {ex}");
                }
                
                if (timeUpdated)
                {
                    logger.Msg($"Client: Time sync successful! New time: Day {timeManager.ElapsedDays}, Time {timeManager.CurrentTime}");
                    break;
                }
                
                // Wait before next attempt (outside of try-catch)
                yield return new WaitForSeconds(0.5f);
            }
            
            // Final status
            logger.Msg($"Client: Aggressive time sync completed - final time: Day {timeManager.ElapsedDays}, Time {timeManager.CurrentTime}");
        }

        /// <summary>
        /// Client-side postfix patch for FastForwardToWakeTime to ensure time sync
        /// </summary>
        private static void ClientFastForwardPostfix(ScheduleOne.GameTime.TimeManager __instance)
        {
            // Only apply to clients connected to dedicated servers
            if (InstanceFinder.IsHost || !InstanceFinder.IsClient)
            {
                return;
            }

            try
            {
                logger.Msg("Client: FastForwardToWakeTime completed, requesting updated time from server");
                
                // Small delay to let server finish processing, then request sync
                MelonCoroutines.Start(DelayedTimeSync(__instance));
            }
            catch (Exception ex)
            {
                logger.Error($"Error in client FastForwardToWakeTime postfix: {ex}");
            }
        }

        /// <summary>
        /// Delayed time synchronization request to ensure server has finished processing
        /// </summary>
        private static IEnumerator DelayedTimeSync(ScheduleOne.GameTime.TimeManager timeManager)
        {
            yield return new WaitForSeconds(1f);
            
            bool syncRequested = false;
            try
            {
                var sendTimeDataMethod = typeof(ScheduleOne.GameTime.TimeManager).GetMethod("SendTimeData", 
                    BindingFlags.Public | BindingFlags.Instance);
                if (sendTimeDataMethod != null)
                {
                    sendTimeDataMethod.Invoke(timeManager, new object[] { null });
                    syncRequested = true;
                    logger.Msg("Client: Requested time sync from dedicated server after sleep");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in delayed time sync: {ex}");
            }
            
            if (syncRequested)
            {
                logger.Msg("Client: Time sync request completed successfully");
            }
        }

        /// <summary>
        /// CRITICAL: Client-side postfix patch for SetData RPC to monitor time synchronization
        /// This helps debug why clients aren't receiving time updates properly after sleep
        /// </summary>
        private static void ClientSetDataRpcPostfix(ScheduleOne.GameTime.TimeManager __instance, NetworkConnection conn, int _elapsedDays, int _time, float sendTime)
        {
            try
            {
                logger.Msg($"🕐 CLIENT RPC RECEIVED: SetData called with Day {_elapsedDays}, Time {_time}, SendTime {sendTime}");
                logger.Msg($"🕐 CLIENT RPC CONTEXT: IsHost={InstanceFinder.IsHost}, IsClient={InstanceFinder.IsClient}, Connection={conn?.ClientId}");
                logger.Msg($"🕐 CLIENT RPC BEFORE: Instance Day {__instance.ElapsedDays}, Time {__instance.CurrentTime}");
                
                // This runs after the original method, so log the final state
                logger.Msg($"🕐 CLIENT RPC AFTER: Instance Day {__instance.ElapsedDays}, Time {__instance.CurrentTime}");
                
                // Only apply additional logic to clients connected to dedicated servers
                if (!InstanceFinder.IsHost && InstanceFinder.IsClient)
                {
                    // Force UI updates to ensure the time is displayed correctly
                    if (__instance.onTimeChanged != null)
                    {
                        logger.Msg("🕐 CLIENT: Triggering onTimeChanged event for UI updates");
                        __instance.onTimeChanged.Invoke();
                    }
                    
                    // Force HasChanged flag to ensure UI updates
                    __instance.HasChanged = true;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in client SetData RPC postfix: {ex}");
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
