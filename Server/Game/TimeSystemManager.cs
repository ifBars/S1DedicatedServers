using System;
using DedicatedServerMod.Shared.Configuration;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Exposes server time diagnostics and admin utilities without overriding native time flow.
    /// </summary>
    public class TimeSystemManager
    {
        private readonly MelonLogger.Instance logger;
        private bool isActive;

        public TimeSystemManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        /// <summary>
        /// Gets whether the time system manager is active.
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Initializes time handling in native-game mode.
        /// </summary>
        public void Initialize()
        {
            try
            {
                isActive = true;

                if (ServerConfig.Instance.TimeNeverStops)
                {
                    logger.Warning("timeNeverStops is enabled but native time mode is active; dedicated server will use base-game time behavior.");
                }

                logger.Msg("Time system initialized with native game behavior (4 AM freeze preserved).");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize time system: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Force advances time using the game's built-in SetTimeAndSync path.
        /// </summary>
        public void ForceAdvanceTime(TimeSpan amount)
        {
            try
            {
                TimeManager tm = NetworkSingleton<TimeManager>.Instance;
                if (tm != null)
                {
                    int addMins = (int)Math.Round(amount.TotalMinutes);
                    int newTime = TimeManager.AddMinutesTo24HourTime(tm.CurrentTime, addMins);
                    tm.SetTimeAndSync(newTime);
                    logger.Msg($"Forced time advancement: {addMins} minutes");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error forcing time advancement: {ex}");
            }
        }

        /// <summary>
        /// Gets current game time information.
        /// </summary>
        public GameTimeInfo GetTimeInfo()
        {
            try
            {
                TimeManager tm = NetworkSingleton<TimeManager>.Instance;
                if (tm == null)
                {
                    return new GameTimeInfo
                    {
                        IsAvailable = false,
                        Message = "GameDateTime not available"
                    };
                }

                return new GameTimeInfo
                {
                    IsAvailable = true,
                    Hour = tm.CurrentTime / 100,
                    Minute = tm.CurrentTime % 100,
                    Day = tm.ElapsedDays,
                    TimeNeverStops = ServerConfig.Instance.TimeNeverStops,
                    Message = $"{tm.CurrentTime / 100:D2}:{tm.CurrentTime % 100:D2} Day {tm.ElapsedDays}"
                };
            }
            catch (Exception ex)
            {
                return new GameTimeInfo
                {
                    IsAvailable = false,
                    Message = $"Error getting time info: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Shuts down the time system manager.
        /// </summary>
        public void Shutdown()
        {
            isActive = false;
            logger.Msg("Time system shutdown");
        }
    }

    /// <summary>
    /// Game time information.
    /// </summary>
    public class GameTimeInfo
    {
        public bool IsAvailable { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public int Day { get; set; }
        public bool TimeNeverStops { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}
