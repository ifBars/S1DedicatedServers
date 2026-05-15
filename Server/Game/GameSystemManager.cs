using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.PlayerScripts;
#else
#endif
using DedicatedServerMod.Shared.Configuration;

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Coordinates dedicated-server game-system adapters such as time diagnostics, sleep behavior, and Harmony patches.
    /// </summary>
    /// <remarks>
    /// This manager is intentionally an orchestration layer. Feature-specific logic belongs in the
    /// subsystem managers or patch classes so <c>ServerBootstrap</c> can start and stop game systems
    /// without owning gameplay behavior directly.
    /// </remarks>
    public sealed class GameSystemManager
    {
        private readonly MelonLogger.Instance logger;

        private readonly TimeSystemManager timeManager;
        private readonly SleepSystemManager sleepManager;
        private readonly GamePatchManager patchManager;

        internal GameSystemManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            timeManager = new TimeSystemManager();
            sleepManager = new SleepSystemManager(logger);
            patchManager = new GamePatchManager();
        }

        /// <summary>
        /// Gets the time-system diagnostics and utility manager.
        /// </summary>
        public TimeSystemManager TimeSystem => timeManager;

        /// <summary>
        /// Gets the sleep-system behavior manager.
        /// </summary>
        public SleepSystemManager SleepSystem => sleepManager;

        /// <summary>
        /// Gets the Harmony patch manager for dedicated-server game behavior.
        /// </summary>
        public GamePatchManager PatchManager => patchManager;

        /// <summary>
        /// Initialize the game system manager
        /// </summary>
        internal void Initialize()
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
        /// Gets a snapshot of the current game-system state.
        /// </summary>
        /// <returns>Current subsystem and patch statistics for diagnostics and status output.</returns>
        public GameSystemStats GetStats()
        {
            return new GameSystemStats
            {
                IgnoreGhostHostForSleep = ServerConfig.Instance.IgnoreGhostHostForSleep,
                PatchesApplied = patchManager.GetAppliedPatchCount(),
                TimeSystemActive = timeManager.IsActive,
                SleepSystemActive = sleepManager.IsActive
            };
        }

        /// <summary>
        /// Shutdown the game system manager
        /// </summary>
        internal void Shutdown()
        {
            try
            {
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
    /// Reports the active state of dedicated-server game systems.
    /// </summary>
    /// <remarks>
    /// This type is used for diagnostics and status output. It is a snapshot, not a live view into
    /// the underlying managers.
    /// </remarks>
    public class GameSystemStats
    {
        /// <summary>
        /// Gets or sets whether the ghost host is ignored during sleep readiness checks.
        /// </summary>
        public bool IgnoreGhostHostForSleep { get; set; }

        /// <summary>
        /// Gets or sets the number of applied patch labels tracked by the patch manager.
        /// </summary>
        public int PatchesApplied { get; set; }

        /// <summary>
        /// Gets or sets whether the time-system manager is active.
        /// </summary>
        public bool TimeSystemActive { get; set; }

        /// <summary>
        /// Gets or sets whether the sleep-system manager is active.
        /// </summary>
        public bool SleepSystemActive { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Sleep: {(IgnoreGhostHostForSleep ? "Ignore Ghost Host" : "Normal")} | " +
                   $"Patches: {PatchesApplied} | " +
                   $"Systems: Time({(TimeSystemActive ? "Active" : "Inactive")}), Sleep({(SleepSystemActive ? "Active" : "Inactive")})";
        }
    }
}



