using MelonLoader;
using HarmonyLib;
using System;
using System.Reflection;
#if IL2CPP
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.PlayerScripts;
#else
using ScheduleOne.GameTime;
using ScheduleOne.PlayerScripts;
#endif
using UnityEngine;
using DedicatedServerMod;
using DedicatedServerMod.Shared.Configuration;

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Manages game-specific systems, patches, and behaviors for the dedicated server.
    /// Handles time management, sleep systems, and other game mechanics modifications.
    /// </summary>
    public class GameSystemManager
    {
        private readonly MelonLogger.Instance logger;

        private readonly TimeSystemManager timeManager;
        private readonly SleepSystemManager sleepManager;
        private readonly GamePatchManager patchManager;

        public GameSystemManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            timeManager = new TimeSystemManager(logger);
            sleepManager = new SleepSystemManager(logger);
            patchManager = new GamePatchManager(logger);
        }

        /// <summary>
        /// Gets the time system manager
        /// </summary>
        public TimeSystemManager TimeSystem => timeManager;

        /// <summary>
        /// Gets the sleep system manager
        /// </summary>
        public SleepSystemManager SleepSystem => sleepManager;

        /// <summary>
        /// Gets the game patch manager
        /// </summary>
        public GamePatchManager PatchManager => patchManager;

        /// <summary>
        /// Initialize the game system manager
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Initialize subsystems
                timeManager.Initialize();
                sleepManager.Initialize();
                patchManager.Initialize();

                logger.Msg("Game system manager initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize game system manager: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Get game system statistics
        /// </summary>
        public GameSystemStats GetStats()
        {
            return new GameSystemStats
            {
                TimeNeverStops = ServerConfig.Instance.TimeNeverStops,
                IgnoreGhostHostForSleep = ServerConfig.Instance.IgnoreGhostHostForSleep,
                PatchesApplied = patchManager.GetAppliedPatchCount(),
                TimeSystemActive = timeManager.IsActive,
                SleepSystemActive = sleepManager.IsActive
            };
        }

        /// <summary>
        /// Shutdown the game system manager
        /// </summary>
        public void Shutdown()
        {
            try
            {
                patchManager.Shutdown();
                sleepManager.Shutdown();
                timeManager.Shutdown();

                logger.Msg("Game system manager shutdown");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during game system manager shutdown: {ex}");
            }
        }
    }

    /// <summary>
    /// Game system statistics
    /// </summary>
    public class GameSystemStats
    {
        public bool TimeNeverStops { get; set; }
        public bool IgnoreGhostHostForSleep { get; set; }
        public int PatchesApplied { get; set; }
        public bool TimeSystemActive { get; set; }
        public bool SleepSystemActive { get; set; }

        public override string ToString()
        {
            return $"Time: {(TimeNeverStops ? "Never Stops" : "Normal")} | " +
                   $"Sleep: {(IgnoreGhostHostForSleep ? "Ignore Ghost Host" : "Normal")} | " +
                   $"Patches: {PatchesApplied} | " +
                   $"Systems: Time({(TimeSystemActive ? "Active" : "Inactive")}), Sleep({(SleepSystemActive ? "Active" : "Inactive")})";
        }
    }
}
