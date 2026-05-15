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
    /// <remarks>
    /// The dedicated server now keeps the base game's authoritative time path intact. This manager
    /// reports state and invokes native synchronization methods for explicit operator actions instead
    /// of running a parallel clock.
    /// </remarks>
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
        /// Advances time through the game's built-in synchronized time path.
        /// </summary>
        /// <param name="amount">The amount of game time to add to the current server time.</param>
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
        /// <returns>A diagnostic snapshot of the current game clock, or an unavailable result with an explanatory message.</returns>
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
    /// Describes a point-in-time diagnostic snapshot of the server's authoritative game clock.
    /// </summary>
    public class GameTimeInfo
    {
        /// <summary>
        /// Gets or sets whether the time manager was available when the snapshot was created.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets the current hour in 24-hour game time.
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// Gets or sets the current minute in game time.
        /// </summary>
        public int Minute { get; set; }

        /// <summary>
        /// Gets or sets the elapsed in-game day count.
        /// </summary>
        public int Day { get; set; }

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


