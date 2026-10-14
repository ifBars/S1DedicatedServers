using MelonLoader;
using System.Collections;
#if IL2CPP
using Il2CppFishNet;
using Il2CppScheduleOne.Persistence;
#else
using FishNet;
using ScheduleOne.Persistence;
#endif
using UnityEngine;
using UnityEngine.Events;
using DedicatedServerMod.Shared.Configuration;

namespace DedicatedServerMod.Server.Persistence
{
    /// <summary>
    /// Manages game persistence including auto-saves and manual saves.
    /// Handles save triggers and coordinates with the game's save system.
    /// </summary>
    public sealed class PersistenceManager
    {
        private readonly MelonLogger.Instance logger;

        private DateTime lastAutoSave = DateTime.MinValue;
        private bool saveInProgress = false;
        private bool shuttingDown;
        private object autoSaveCoroutine;

        /// <summary>
        /// Raised when a save starts.
        /// </summary>
        public event Action<string, bool> SaveStarted;

        /// <summary>
        /// Raised when a save completes.
        /// </summary>
        /// <remarks>The final argument is true only when the native save completes without a reported save error.</remarks>
        public event Action<string, bool, bool> SaveCompleted;

        internal PersistenceManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        /// <summary>
        /// Gets the time of the last auto-save
        /// </summary>
        public DateTime LastAutoSave => lastAutoSave;

        /// <summary>
        /// Gets whether a save operation is currently in progress
        /// </summary>
        public bool SaveInProgress => saveInProgress;

        /// <summary>
        /// Initialize the persistence manager
        /// </summary>
        internal void Initialize()
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
            autoSaveCoroutine = MelonCoroutines.Start(AutoSaveLoop());
        }

        /// <summary>
        /// Auto-save coroutine that runs periodically
        /// </summary>
        private IEnumerator AutoSaveLoop()
        {
            while (!shuttingDown && ServerConfig.Instance.AutoSaveEnabled)
            {
                yield return new WaitForSeconds(ServerConfig.Instance.AutoSaveIntervalMinutes * 60f);
                
                if (!shuttingDown && ServerConfig.Instance.AutoSaveEnabled && InstanceFinder.IsServer)
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
            if (shuttingDown)
                return;

            if (!ServerConfig.Instance.AutoSaveEnabled)
            {
                logger.Msg("Auto-save disabled, skipping save request");
                return;
            }

            if (saveInProgress)
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
            if (shuttingDown)
                return;

            if (saveInProgress)
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
            while (saveInProgress)
            {
                yield return null;
            }

            if (!shuttingDown)
                yield return PerformSave(reason, isAutoSave);
        }

        /// <summary>
        /// Perform the actual save operation
        /// </summary>
        private IEnumerator PerformSave(string reason, bool isAutoSave, Action<bool> completed = null)
        {
            while (saveInProgress)
                yield return null;

            saveInProgress = true;
            bool success = false;
            bool nativeSaveStarted = false;
            bool nativeSaveCompleted = false;
            var saveManager = SaveManager.Instance;
            UnityAction onStarted = (UnityAction)delegate { nativeSaveStarted = true; };
            UnityAction onCompleted = (UnityAction)delegate { nativeSaveCompleted = true; };
            float startedAt = Time.realtimeSinceStartup;
            try
            {
                if (saveManager == null || LoadManager.Instance == null ||
                    !LoadManager.Instance.IsGameLoaded || string.IsNullOrEmpty(LoadManager.Instance.LoadedGameFolderPath))
                {
                    logger.Error("No loaded game available to save");
                    yield break;
                }

                // A save started by the game must finish before we request our own.
                while (saveManager != null && saveManager.IsSaving)
                    yield return null;

                bool initiated = false;
                try
                {
                    SaveStarted?.Invoke(reason, isAutoSave);
                    logger.Msg($"Starting {(isAutoSave ? "auto" : "manual")} save: {reason}");
                    saveManager.onSaveStart?.AddListener(onStarted);
                    saveManager.onSaveComplete?.AddListener(onCompleted);
                    API.ModManager.NotifyBeforeSave();
                    saveManager.Save();
                    nativeSaveStarted |= saveManager.IsSaving;
                    initiated = true;
                }
                catch (Exception ex)
                {
                    logger.Error($"Error calling SaveManager.Save(): {ex}");
                }

                if (!initiated || !nativeSaveStarted)
                {
                    logger.Error($"Save did not start: {reason}");
                    yield break;
                }

                bool warned = false;
                while (saveManager != null && saveManager.IsSaving)
                {
                    if (!warned && Time.realtimeSinceStartup - startedAt >= 30f)
                    {
                        logger.Warning($"Still waiting for native save completion: {reason}. Keeping the world running.");
                        warned = true;
                    }
                    yield return null;
                }

                success = saveManager != null && nativeSaveCompleted && !SaveManager.SaveError;
                if (!success)
                {
                    logger.Error($"Save failed or did not report completion: {reason}");
                    yield break;
                }

                if (isAutoSave)
                    lastAutoSave = DateTime.Now;

                API.ModManager.NotifyAfterSave();
                logger.Msg($"Save completed: {reason} (elapsed: {Time.realtimeSinceStartup - startedAt:F1}s)");
            }
            finally
            {
                if (saveManager != null)
                {
                    saveManager.onSaveStart?.RemoveListener(onStarted);
                    saveManager.onSaveComplete?.RemoveListener(onCompleted);
                }
                saveInProgress = false;
                completed?.Invoke(success);
                SaveCompleted?.Invoke(reason, isAutoSave, success);
            }
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
                LastAutoSave = lastAutoSave,
                SaveInProgress = saveInProgress,
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
                
            if (lastAutoSave == DateTime.MinValue)
                return true; // Never saved before
                
            var timeSinceLastSave = DateTime.Now - lastAutoSave;
            return timeSinceLastSave.TotalMinutes >= ServerConfig.Instance.AutoSaveIntervalMinutes;
        }

        /// <summary>
        /// Drains in-flight saves and performs the final save while the world is still running.
        /// </summary>
        internal IEnumerator SaveForShutdown(Action<bool> completed)
        {
            shuttingDown = true;
            if (autoSaveCoroutine != null)
            {
                MelonCoroutines.Stop(autoSaveCoroutine);
                autoSaveCoroutine = null;
            }
            while (saveInProgress)
                yield return null;

            // Startup can be stopped before there is a world to save.
            if (!InstanceFinder.IsServer || LoadManager.Instance == null || !LoadManager.Instance.IsGameLoaded)
            {
                completed(true);
                yield break;
            }

            yield return PerformSave("server_shutdown", false, completed);
        }

        internal void CancelShutdown()
        {
            shuttingDown = false;
            if (ServerConfig.Instance.AutoSaveEnabled)
                StartAutoSaveLoop();
        }

        internal void Shutdown()
        {
            shuttingDown = true;
            if (autoSaveCoroutine != null)
            {
                MelonCoroutines.Stop(autoSaveCoroutine);
                autoSaveCoroutine = null;
            }
            logger.Msg("Persistence manager shutdown");
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
