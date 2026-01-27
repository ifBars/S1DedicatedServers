using System;
using System.Collections;
using DedicatedServerMod;
using DedicatedServerMod.Shared.Configuration;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
using UnityEngine;

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Manages server time system behaviors including time loops and time progression.
    /// </summary>
    public class TimeSystemManager
    {
        private readonly MelonLogger.Instance logger;

        private bool _timeLoopsStarted = false;
        private bool _isActive = false;

        public TimeSystemManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
        }

        /// <summary>
        /// Gets whether the time system is active
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Initialize the time system manager
        /// </summary>
        public void Initialize()
        {
            try
            {
                if (ServerConfig.Instance.TimeNeverStops)
                {
                    StartTimeLoops();
                    logger.Msg("Time system initialized with 'time never stops' enabled");
                }
                else
                {
                    logger.Msg("Time system initialized with normal behavior");
                }

                _isActive = true;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize time system: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Start the time loops for dedicated server
        /// </summary>
        private void StartTimeLoops()
        {
            if (_timeLoopsStarted)
            {
                logger.Warning("Time loops already started");
                return;
            }

            try
            {
                // Start the time loop coroutine
                MelonCoroutines.Start(TimeLoop());
                
                // Start the tick loop coroutine
                MelonCoroutines.Start(TickLoop());
                
                _timeLoopsStarted = true;
                logger.Msg("Time loops started for dedicated server");
            }
            catch (Exception ex)
            {
                logger.Error($"Error starting time loops: {ex}");
            }
        }

        /// <summary>
        /// Main time loop that keeps time progressing
        /// </summary>
        private IEnumerator TimeLoop()
        {
            while (ServerConfig.Instance.TimeNeverStops && _isActive)
            {
                // Ensure time keeps progressing even when players are asleep
                var tm = NetworkSingleton<TimeManager>.Instance;
                bool canCheck = tm != null;

                if (canCheck)
                {
                    // Small wait so we don't spam checks
                    yield return new WaitForSeconds(1f);

                    // Check if time is stuck at 4:00 and advance it slightly
                    int current = tm.CurrentTime;
                    if (current == 400)
                    {
                        // Advance one minute by invoking a Tick-like nudge
                        // Setting time directly to 4:01
                        tm.SetTimeAndSync(401);
                    }
                }

                yield return new WaitForSeconds(10f); // Check every 10 seconds
            }
        }

        /// <summary>
        /// Tick loop for maintaining server state
        /// </summary>
        private IEnumerator TickLoop()
        {
            while (_isActive)
            {
                // Perform periodic server maintenance tasks
                yield return new WaitForSeconds(30f); // Every 30 seconds

                try { PerformServerTick(); }
                catch (Exception ex) { logger.Warning($"Error in tick loop: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Perform periodic server maintenance
        /// </summary>
        private void PerformServerTick()
        {
            try
            {
                                    // Log current time periodically for debugging
                var tm = NetworkSingleton<TimeManager>.Instance;
                if (tm != null && ServerConfig.Instance.DebugMode)
                {
                    int hour = tm.CurrentTime / 100;
                    int minute = tm.CurrentTime % 100;
                    logger.Msg($"Server tick - Game time: {hour:D2}:{minute:D2}");
                }

                // Additional server maintenance tasks could go here
            }
            catch (Exception ex)
            {
                logger.Warning($"Error during server tick: {ex.Message}");
            }
        }

        /// <summary>
        /// Force advance time to prevent freeze
        /// </summary>
        public void ForceAdvanceTime(TimeSpan amount)
        {
            try
            {
                var tm = NetworkSingleton<TimeManager>.Instance;
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
        /// Get current game time information
        /// </summary>
        public GameTimeInfo GetTimeInfo()
        {
            try
            {
                var tm = NetworkSingleton<TimeManager>.Instance;
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
        /// Shutdown the time system
        /// </summary>
        public void Shutdown()
        {
            _isActive = false;
            _timeLoopsStarted = false;
            logger.Msg("Time system shutdown");
        }
    }

    /// <summary>
    /// Game time information
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
