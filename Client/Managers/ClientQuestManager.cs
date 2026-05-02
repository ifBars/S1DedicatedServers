#if IL2CPP
using Il2CppFishNet;
#else
using FishNet;
#endif
using MelonLoader;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Quests;
#else
using ScheduleOne.PlayerScripts;
using ScheduleOne.Quests;
#endif
using System;
using System.Collections;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
#else
using ScheduleOne.DevUtilities;
#endif
using UnityEngine;

namespace DedicatedServerMod.Client.Managers
{
    /// <summary>
    /// Manages quest system initialization for dedicated server clients.
    /// Ensures proper quest synchronization when connecting to dedicated servers.
    /// </summary>
    public sealed class ClientQuestManager
    {
        private const float QuestManagerInitialDelaySeconds = 2f;
        private const float QuestManagerRetryIntervalSeconds = 0.5f;
        private const float QuestManagerResolveTimeoutSeconds = 15f;

        // Quest system state
        private bool questSystemInitialized = false;
        private QuestManager questManagerInstance;

        internal ClientQuestManager()
        {
        }

        internal void Initialize()
        {
            try
            {
                DebugLog.Info("Initializing ClientQuestManager");
                
                // Quest system initialization will happen when main scene loads
                
                DebugLog.Info("ClientQuestManager initialized");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to initialize ClientQuestManager: {ex}");
            }
        }

        /// <summary>
        /// Handle scene loading events
        /// </summary>
        internal void OnSceneLoaded(string sceneName)
        {
            try
            {
                if (sceneName == "Main" && ClientConnectionManager.IsTugboatMode)
                {
                    DebugLog.Info("Main scene loaded in dedicated server mode - ensuring quest initialization");
                    MelonCoroutines.Start(EnsureQuestInitialization());
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error handling quest scene load: {ex}");
            }
        }

        /// <summary>
        /// Ensure quest system is properly initialized for dedicated server clients
        /// </summary>
        private IEnumerator EnsureQuestInitialization()
        {
            // Wait for systems to be ready
            yield return new WaitForSeconds(QuestManagerInitialDelaySeconds);
            
            // Get QuestManager instance
            float elapsed = 0f;
            while ((questManagerInstance = NetworkSingleton<QuestManager>.Instance) == null
                && elapsed < QuestManagerResolveTimeoutSeconds)
            {
                yield return new WaitForSeconds(QuestManagerRetryIntervalSeconds);
                elapsed += QuestManagerRetryIntervalSeconds;
            }

            if (questManagerInstance != null)
            {
                // Wait for local player to be available
                yield return MelonCoroutines.Start(WaitForLocalPlayer());
                
                if (Player.Local != null)
                {
                    // Ensure quest system is synchronized
                    yield return MelonCoroutines.Start(SynchronizeQuestSystem(Player.Local));
                    
                    questSystemInitialized = true;
                    DebugLog.Info("Quest system initialization completed");
                }
                else
                {
                    DebugLog.Warning("Local player not available for quest initialization");
                }
            }
            else
            {
                DebugLog.Warning("QuestManager not found - quest initialization may be delayed");
            }
        }

        /// <summary>
        /// Wait for local player to be available
        /// </summary>
        private IEnumerator WaitForLocalPlayer()
        {
            float timeout = 10f;
            float elapsed = 0f;
            
            while (Player.Local == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (Player.Local == null)
            {
                DebugLog.Warning($"Local player not found after {timeout}s timeout");
            }
        }

        /// <summary>
        /// Synchronize quest system for the player
        /// </summary>
        private IEnumerator SynchronizeQuestSystem(Player player)
        {
            // Wait a moment for networking to settle
            yield return new WaitForSeconds(1f);
            
            // The QuestManager should automatically handle quest initialization for new clients
            // but we can ensure proper synchronization by triggering a save
            if (questManagerInstance != null)
            {
                // Request player save to ensure quest data is persisted
                player.RequestSavePlayer();
            }
            else
            {
                DebugLog.Warning("QuestManager not available for synchronization");
            }
        }

        /// <summary>
        /// Handle quest completion events for dedicated server clients
        /// </summary>
        internal void OnQuestCompleted(string questId)
        {
            try
            {
                DebugLog.Info($"Quest completed on dedicated server client: {questId}");
                
                // Ensure quest completion is properly saved
                if (Player.Local != null)
                {
                    MelonCoroutines.Start(DelayedQuestSave());
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error handling quest completion: {ex}");
            }
        }

        /// <summary>
        /// Handle quest progression events
        /// </summary>
        internal void OnQuestProgressed(string questId, float progress)
        {
            try
            {
                DebugLog.Info($"Quest progressed on dedicated server client: {questId} ({progress:P})");
                
                // Quest progression is handled automatically by the networking system
                // This is just for logging/debugging
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error handling quest progression: {ex}");
            }
        }

        /// <summary>
        /// Delayed save after quest events to ensure persistence
        /// </summary>
        private IEnumerator DelayedQuestSave()
        {
            yield return new WaitForSeconds(2f);
            
            try
            {
                if (Player.Local != null)
                {
                    DebugLog.Info("Requesting delayed save after quest event");
                    Player.Local.RequestSavePlayer();
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error in delayed quest save: {ex}");
            }
        }

        /// <summary>
        /// Force quest system resynchronization
        /// </summary>
        internal void ForceQuestResync()
        {
            try
            {
                DebugLog.Info("Forcing quest system resynchronization");
                
                if (Player.Local != null && questManagerInstance != null)
                {
                    MelonCoroutines.Start(SynchronizeQuestSystem(Player.Local));
                }
                else
                {
                    DebugLog.Warning("Cannot force quest resync - player or quest manager not available");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error forcing quest resync: {ex}");
            }
        }

        /// <summary>
        /// Get quest system status for debugging
        /// </summary>
        internal string GetQuestSystemStatus()
        {
            try
            {
                var status = "=== Quest System Status ===\n";
                status += $"Quest System Initialized: {questSystemInitialized}\n";
                status += $"QuestManager Available: {questManagerInstance != null}\n";
                status += $"Local Player Available: {Player.Local != null}\n";
                status += $"In Tugboat Mode: {ClientConnectionManager.IsTugboatMode}\n";
                
                if (questManagerInstance != null)
                {
                    status += $"QuestManager Type: {questManagerInstance.GetType().Name}\n";
                    // Could add more quest-specific status here if needed
                }
                
                return status;
            }
            catch (Exception ex)
            {
                return $"Error getting quest system status: {ex.Message}";
            }
        }

        /// <summary>
        /// Handle player disconnection cleanup
        /// </summary>
        internal void OnPlayerDisconnected()
        {
            try
            {
                DebugLog.Info("Cleaning up quest system for player disconnection");
                
                questSystemInitialized = false;
                questManagerInstance = null;
                
                DebugLog.Info("Quest system cleanup completed");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error during quest system cleanup: {ex}");
            }
        }

        /// <summary>
        /// Reset quest system state
        /// </summary>
        internal void ResetQuestSystem()
        {
            try
            {
                questSystemInitialized = false;
                questManagerInstance = null;
                DebugLog.Info("Quest system state reset");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error resetting quest system: {ex}");
            }
        }

        /// <summary>
        /// Check if quest system is ready
        /// </summary>
        internal bool IsQuestSystemReady()
        {
            return questSystemInitialized && questManagerInstance != null && Player.Local != null;
        }
    }
}
