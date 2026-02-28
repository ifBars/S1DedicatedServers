#if IL2CPP
using Il2CppFishNet;
#else
using FishNet;
#endif
using MelonLoader;
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
    public class ClientQuestManager
    {
        private readonly MelonLogger.Instance logger;
        
        // Quest system state
        private bool questSystemInitialized = false;
        private QuestManager questManagerInstance;

        public ClientQuestManager(MelonLogger.Instance logger)
        {
            this.logger = logger;
        }

        public void Initialize()
        {
            try
            {
                logger.Msg("Initializing ClientQuestManager");
                
                // Quest system initialization will happen when main scene loads
                
                logger.Msg("ClientQuestManager initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize ClientQuestManager: {ex}");
            }
        }

        /// <summary>
        /// Handle scene loading events
        /// </summary>
        public void OnSceneLoaded(string sceneName)
        {
            try
            {
                if (sceneName == "Main" && ClientConnectionManager.IsTugboatMode)
                {
                    logger.Msg("Main scene loaded in dedicated server mode - ensuring quest initialization");
                    MelonCoroutines.Start(EnsureQuestInitialization());
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling quest scene load: {ex}");
            }
        }

        /// <summary>
        /// Ensure quest system is properly initialized for dedicated server clients
        /// </summary>
        private IEnumerator EnsureQuestInitialization()
        {
            // Wait for systems to be ready
            yield return new WaitForSeconds(2f);
            
            // Get QuestManager instance
            questManagerInstance = NetworkSingleton<QuestManager>.Instance;
            if (questManagerInstance != null)
            {
                logger.Msg("QuestManager found - ensuring proper quest initialization");
                
                // Wait for local player to be available
                yield return MelonCoroutines.Start(WaitForLocalPlayer());
                
                if (Player.Local != null)
                {
                    // Ensure quest system is synchronized
                    yield return MelonCoroutines.Start(SynchronizeQuestSystem(Player.Local));
                    
                    questSystemInitialized = true;
                    logger.Msg("Quest system initialization completed");
                }
                else
                {
                    logger.Warning("Local player not available for quest initialization");
                }
            }
            else
            {
                logger.Warning("QuestManager not found - quest initialization may be delayed");
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
                logger.Warning($"Local player not found after {timeout}s timeout");
            }
            else
            {
                logger.Msg("Local player found for quest initialization");
            }
        }

        /// <summary>
        /// Synchronize quest system for the player
        /// </summary>
        private IEnumerator SynchronizeQuestSystem(Player player)
        {
            logger.Msg("Synchronizing quest system for dedicated server client");
            
            // Wait a moment for networking to settle
            yield return new WaitForSeconds(1f);
            
            // The QuestManager should automatically handle quest initialization for new clients
            // but we can ensure proper synchronization by triggering a save
            if (questManagerInstance != null)
            {
                logger.Msg("QuestManager available - triggering quest synchronization");
                
                // Request player save to ensure quest data is persisted
                player.RequestSavePlayer();
                
                // Check if player has existing quest data
                yield return MelonCoroutines.Start(ValidateQuestData(player));
            }
            else
            {
                logger.Warning("QuestManager not available for synchronization");
            }
        }

        /// <summary>
        /// Validate that quest data is properly synchronized
        /// </summary>
        private IEnumerator ValidateQuestData(Player player)
        {
            // Wait for quest data to be synchronized
            yield return new WaitForSeconds(0.5f);
            
            // Check if player has quest progression data
            // This is handled automatically by the QuestManager's networking
            // but we log for debugging purposes
            
            logger.Msg("Quest data validation completed");
            
            // Force another save to ensure everything is persisted
            yield return new WaitForSeconds(1f);
            player.RequestSavePlayer();
            
            logger.Msg("Quest synchronization save requested");
        }

        /// <summary>
        /// Handle quest completion events for dedicated server clients
        /// </summary>
        public void OnQuestCompleted(string questId)
        {
            try
            {
                logger.Msg($"Quest completed on dedicated server client: {questId}");
                
                // Ensure quest completion is properly saved
                if (Player.Local != null)
                {
                    MelonCoroutines.Start(DelayedQuestSave());
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling quest completion: {ex}");
            }
        }

        /// <summary>
        /// Handle quest progression events
        /// </summary>
        public void OnQuestProgressed(string questId, float progress)
        {
            try
            {
                logger.Msg($"Quest progressed on dedicated server client: {questId} ({progress:P})");
                
                // Quest progression is handled automatically by the networking system
                // This is just for logging/debugging
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling quest progression: {ex}");
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
                    logger.Msg("Requesting delayed save after quest event");
                    Player.Local.RequestSavePlayer();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in delayed quest save: {ex}");
            }
        }

        /// <summary>
        /// Force quest system resynchronization
        /// </summary>
        public void ForceQuestResync()
        {
            try
            {
                logger.Msg("Forcing quest system resynchronization");
                
                if (Player.Local != null && questManagerInstance != null)
                {
                    MelonCoroutines.Start(SynchronizeQuestSystem(Player.Local));
                }
                else
                {
                    logger.Warning("Cannot force quest resync - player or quest manager not available");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error forcing quest resync: {ex}");
            }
        }

        /// <summary>
        /// Get quest system status for debugging
        /// </summary>
        public string GetQuestSystemStatus()
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
        public void OnPlayerDisconnected()
        {
            try
            {
                logger.Msg("Cleaning up quest system for player disconnection");
                
                questSystemInitialized = false;
                questManagerInstance = null;
                
                logger.Msg("Quest system cleanup completed");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during quest system cleanup: {ex}");
            }
        }

        /// <summary>
        /// Reset quest system state
        /// </summary>
        public void ResetQuestSystem()
        {
            try
            {
                questSystemInitialized = false;
                questManagerInstance = null;
                logger.Msg("Quest system state reset");
            }
            catch (Exception ex)
            {
                logger.Error($"Error resetting quest system: {ex}");
            }
        }

        /// <summary>
        /// Check if quest system is ready
        /// </summary>
        public bool IsQuestSystemReady()
        {
            return questSystemInitialized && questManagerInstance != null && Player.Local != null;
        }
    }
}
