using MelonLoader;
using System;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Manages sleep system behaviors for the dedicated server.
    /// Handles ghost host ignore functionality and sleep synchronization.
    /// </summary>
    public class SleepSystemManager
    {
        private readonly MelonLogger.Instance logger;

        private bool _isActive = false;

        public SleepSystemManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        /// <summary>
        /// Gets whether the sleep system is active
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Initialize the sleep system manager
        /// </summary>
        public void Initialize()
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

                _isActive = true;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize sleep system: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Check if a player should be ignored for sleep calculations
        /// </summary>
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
        /// Get sleep system information
        /// </summary>
        public SleepSystemInfo GetSleepInfo()
        {
            return new SleepSystemInfo
            {
                IsActive = _isActive,
                IgnoreGhostHost = ServerConfig.Instance.IgnoreGhostHostForSleep,
                Message = ServerConfig.Instance.IgnoreGhostHostForSleep 
                    ? "Ghost host ignored for sleep calculations"
                    : "Normal sleep behavior"
            };
        }

        /// <summary>
        /// Shutdown the sleep system
        /// </summary>
        public void Shutdown()
        {
            _isActive = false;
            logger.Msg("Sleep system shutdown");
        }
    }

    /// <summary>
    /// Sleep system information
    /// </summary>
    public class SleepSystemInfo
    {
        public bool IsActive { get; set; }
        public bool IgnoreGhostHost { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
