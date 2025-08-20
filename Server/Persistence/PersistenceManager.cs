using MelonLoader;
using System;
using System.Collections;
using FishNet;
using ScheduleOne.Persistence;
using UnityEngine;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Persistence
{
    /// <summary>
    /// Manages game persistence including auto-saves and manual saves.
    /// Handles save triggers and coordinates with the game's save system.
    /// </summary>
    public class PersistenceManager
    {
        private readonly MelonLogger.Instance logger;

        private DateTime _lastAutoSave = DateTime.MinValue;
        private bool _saveInProgress = false;

        public PersistenceManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        /// <summary>
        /// Gets the time of the last auto-save
        /// </summary>
        public DateTime LastAutoSave => _lastAutoSave;

        /// <summary>
        /// Gets whether a save operation is currently in progress
        /// </summary>
        public bool SaveInProgress => _saveInProgress;

        /// <summary>
        /// Initialize the persistence manager
        /// </summary>
        public void Initialize()
        {
            try
            {
                if (ServerConfig.Instance.AutoSaveEnabled)
                {
                    StartAutoSaveLoop();
                    logger.Msg($"Auto-save enabled with {ServerConfig.Instance.AutoSaveIntervalMinutes} minute interval");
                }
                else
                {
                    logger.Msg("Auto-save disabled");
                }

                logger.Msg("Persistence manager initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize persistence manager: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Start the auto-save loop
        /// </summary>
        private void StartAutoSaveLoop()
        {
            MelonCoroutines.Start(AutoSaveLoop());
        }

        /// <summary>
        /// Auto-save coroutine that runs periodically
        /// </summary>
        private IEnumerator AutoSaveLoop()
        {
            while (ServerConfig.Instance.AutoSaveEnabled)
            {
                yield return new WaitForSeconds(ServerConfig.Instance.AutoSaveIntervalMinutes * 60f);
                
                if (ServerConfig.Instance.AutoSaveEnabled && InstanceFinder.IsServer)
                {
                    TriggerAutoSave("periodic_auto_save");
                }
            }
        }

        /// <summary>
        /// Trigger an auto-save with the given reason
        /// </summary>
        public void TriggerAutoSave(string reason)
        {
            if (!ServerConfig.Instance.AutoSaveEnabled)
            {
                logger.Msg("Auto-save disabled, skipping save request");
                return;
            }

            if (_saveInProgress)
            {
                logger.Warning("Save already in progress, skipping auto-save");
                return;
            }

            if (!InstanceFinder.IsServer)
            {
                logger.Warning("Auto-save can only be triggered on server");
                return;
            }

            MelonCoroutines.Start(PerformSave(reason, true));
        }

        /// <summary>
        /// Trigger a manual save
        /// </summary>
        public void TriggerManualSave(string reason)
        {
            if (_saveInProgress)
            {
                logger.Warning("Save already in progress, queuing manual save");
                MelonCoroutines.Start(WaitAndTriggerSave(reason, false));
                return;
            }

            if (!InstanceFinder.IsServer)
            {
                logger.Warning("Manual save can only be triggered on server");
                return;
            }

            MelonCoroutines.Start(PerformSave(reason, false));
        }

        /// <summary>
        /// Wait for current save to complete and then trigger another save
        /// </summary>
        private IEnumerator WaitAndTriggerSave(string reason, bool isAutoSave)
        {
            while (_saveInProgress)
            {
                yield return new WaitForSeconds(1f);
            }

            yield return PerformSave(reason, isAutoSave);
        }

        /// <summary>
        /// Perform the actual save operation
        /// </summary>
        private IEnumerator PerformSave(string reason, bool isAutoSave)
        {
            _saveInProgress = true;

            logger.Msg($"Starting {(isAutoSave ? "auto" : "manual")} save: {reason}");

            if (SaveManager.Instance == null)
            {
                logger.Error("SaveManager instance not available");
                _saveInProgress = false;
                yield break;
            }

            bool saveStarted = false;
            try
            {
                SaveManager.Instance.Save();
                saveStarted = true;
                logger.Msg("Save operation initiated through SaveManager");
            }
            catch (Exception ex)
            {
                logger.Error($"Error calling SaveManager.Save(): {ex}");
                _saveInProgress = false;
                yield break;
            }

            if (saveStarted)
            {
                float timeout = 30f;
                float elapsed = 0f;

                while (elapsed < timeout)
                {
                    // Might be better if we check SaveManager save status; for now, assume complete within 5s
                    yield return new WaitForSeconds(0.5f);
                    elapsed += 0.5f;
                    if (elapsed >= 5f)
                        break;
                }

                if (isAutoSave)
                {
                    _lastAutoSave = DateTime.Now;
                }

                logger.Msg($"Save completed: {reason} (elapsed: {elapsed:F1}s)");
            }

            _saveInProgress = false;
        }

        /// <summary>
        /// Handle player join save trigger
        /// </summary>
        public void OnPlayerJoined(string playerName)
        {
            if (ServerConfig.Instance.AutoSaveOnPlayerJoin)
            {
                TriggerAutoSave($"player_joined_{playerName}");
            }
        }

        /// <summary>
        /// Handle player leave save trigger
        /// </summary>
        public void OnPlayerLeft(string playerName)
        {
            if (ServerConfig.Instance.AutoSaveOnPlayerLeave)
            {
                TriggerAutoSave($"player_left_{playerName}");
            }
        }

        /// <summary>
        /// Get persistence statistics
        /// </summary>
        public PersistenceStats GetStats()
        {
            return new PersistenceStats
            {
                AutoSaveEnabled = ServerConfig.Instance.AutoSaveEnabled,
                LastAutoSave = _lastAutoSave,
                SaveInProgress = _saveInProgress,
                AutoSaveIntervalMinutes = ServerConfig.Instance.AutoSaveIntervalMinutes,
                SaveOnPlayerJoin = ServerConfig.Instance.AutoSaveOnPlayerJoin,
                SaveOnPlayerLeave = ServerConfig.Instance.AutoSaveOnPlayerLeave
            };
        }

        /// <summary>
        /// Check if it's time for an auto-save
        /// </summary>
        public bool IsAutoSaveDue()
        {
            if (!ServerConfig.Instance.AutoSaveEnabled)
                return false;
                
            if (_lastAutoSave == DateTime.MinValue)
                return true; // Never saved before
                
            var timeSinceLastSave = DateTime.Now - _lastAutoSave;
            return timeSinceLastSave.TotalMinutes >= ServerConfig.Instance.AutoSaveIntervalMinutes;
        }

        /// <summary>
        /// Shutdown the persistence manager
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // Trigger final save before shutdown
                if (InstanceFinder.IsServer && !_saveInProgress)
                {
                    TriggerManualSave("server_shutdown");
                    
                    var waitTime = DateTime.Now.AddSeconds(5);
                    while (_saveInProgress && DateTime.Now < waitTime)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }

                logger.Msg("Persistence manager shutdown");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during persistence manager shutdown: {ex}");
            }
        }
    }

    /// <summary>
    /// Persistence statistics information
    /// </summary>
    public class PersistenceStats
    {
        public bool AutoSaveEnabled { get; set; }
        public DateTime LastAutoSave { get; set; }
        public bool SaveInProgress { get; set; }
        public float AutoSaveIntervalMinutes { get; set; }
        public bool SaveOnPlayerJoin { get; set; }
        public bool SaveOnPlayerLeave { get; set; }

        public TimeSpan TimeSinceLastSave => LastAutoSave == DateTime.MinValue 
            ? TimeSpan.Zero 
            : DateTime.Now - LastAutoSave;

        public override string ToString()
        {
            var status = SaveInProgress ? "In Progress" : "Idle";
            var lastSave = LastAutoSave == DateTime.MinValue ? "Never" : TimeSinceLastSave.ToString(@"mm\:ss") + " ago";
            
            return $"Auto-Save: {(AutoSaveEnabled ? "Enabled" : "Disabled")} | " +
                   $"Status: {status} | " +
                   $"Last Save: {lastSave} | " +
                   $"Interval: {AutoSaveIntervalMinutes}m";
        }
    }
}
