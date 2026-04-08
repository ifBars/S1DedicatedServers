using DedicatedServerMod.Utils;
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
#else
using ScheduleOne.DevUtilities;
using ScheduleOne.GameTime;
#endif

namespace DedicatedServerMod.Server.Game
{
    /// <summary>
    /// Exposes server time diagnostics and admin utilities without overriding native time flow.
    /// </summary>
    public class TimeSystemManager
    {
        private bool isActive;

        internal TimeSystemManager()
        {
        }

        /// <summary>
        /// Gets whether the time system manager is active.
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Initializes time handling in native-game mode.
        /// </summary>
        internal void Initialize()
        {
            try
            {
                isActive = true;

                DebugLog.StartupDebug("Time system initialized with native game behavior.");
            }
            catch (Exception ex)
            {
                DebugLog.Error("Failed to initialize time system", ex);
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
                    DebugLog.Verbose($"Forced time advancement: {addMins} minutes");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error forcing time advancement: {ex}");
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
        internal void Shutdown()
        {
            isActive = false;
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
        public string Message { get; set; }

        public override string ToString()
        {
            return Message;
        }
    }
}


