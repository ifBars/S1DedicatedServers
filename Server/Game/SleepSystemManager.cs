using DedicatedServerMod.Shared.Configuration;
using MelonLoader;

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Reports and coordinates dedicated-server sleep behavior.
    /// </summary>
    /// <remarks>
    /// Sleep readiness is primarily enforced by Harmony patches. This manager exposes the configured
    /// state and diagnostic helpers used by server status output.
    /// </remarks>
    public class SleepSystemManager
    {
        private readonly MelonLogger.Instance logger;

        private bool isActive = false;

        internal SleepSystemManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        /// <summary>
        /// Gets whether the sleep system manager is active.
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Initialize the sleep system manager
        /// </summary>
        internal void Initialize()
        {
            try
            {
                if (ServerConfig.Instance.IgnoreGhostHostForSleep)
                {
                    logger.Msg("Sleep system initialized with ghost host ignore enabled");
                }
                else
                {
                    logger.Msg("Sleep system initialized with normal behavior");
                }

                isActive = true;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize sleep system: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Checks whether a player should be ignored for sleep calculations.
        /// </summary>
        /// <param name="player">The player to evaluate.</param>
        /// <returns><see langword="true"/> when the player should be excluded from sleep readiness checks; otherwise <see langword="false"/>.</returns>
        public bool ShouldIgnorePlayerForSleep(ScheduleOne.PlayerScripts.Player player)
        {
            if (!ServerConfig.Instance.IgnoreGhostHostForSleep)
                return false;

            try
            {
                // Check if this is the ghost host (usually the server owner or dedicated server)
                // Logic here would depend on how the game identifies the host
                // For now, we'll use a simple heuristic
                
                if (player == null || player.Owner == null)
                    return true; // Ignore null players
                
                // If this is a dedicated server, we might want to ignore certain types of connections
                // This would need to be refined based on actual game behavior
                
                return false; // Placeholder - actual implementation would depend on game internals
            }
            catch (Exception ex)
            {
                logger.Warning($"Error checking if player should be ignored for sleep: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a snapshot of the current sleep-system diagnostic state.
        /// </summary>
        /// <returns>Current sleep-system information.</returns>
        public SleepSystemInfo GetSleepInfo()
        {
            return new SleepSystemInfo
            {
                IsActive = isActive,
                IgnoreGhostHost = ServerConfig.Instance.IgnoreGhostHostForSleep,
                Message = ServerConfig.Instance.IgnoreGhostHostForSleep 
                    ? "Ghost host ignored for sleep calculations"
                    : "Normal sleep behavior"
            };
        }

        /// <summary>
        /// Shutdown the sleep system
        /// </summary>
        internal void Shutdown()
        {
            isActive = false;
            logger.Msg("Sleep system shutdown");
        }
    }

    /// <summary>
    /// Describes the current diagnostic state of the dedicated-server sleep system.
    /// </summary>
    public class SleepSystemInfo
    {
        /// <summary>
        /// Gets or sets whether the sleep manager is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets whether ghost-host loopback players are ignored for sleep readiness.
        /// </summary>
        public bool IgnoreGhostHost { get; set; }

        /// <summary>
        /// Gets or sets the human-readable diagnostic message for this snapshot.
        /// </summary>
        public string Message { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Message;
        }
    }
}
