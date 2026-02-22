using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;

namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Centralized logging utility for DedicatedServerMod.
    /// Provides consistent logging with configurable verbosity levels.
    /// All logging should go through this utility rather than direct MelonLogger calls.
    /// </summary>
    /// <remarks>
    /// Logging levels:
    /// - Info: Normal operational messages
    /// - Warning: Recoverable issues or unexpected states
    /// - Error: Failures that may affect functionality
    /// - Debug: Detailed information for troubleshooting (requires debugMode)
    /// - Verbose: Very detailed trace information (requires verboseLogging)
    /// </remarks>
    public static class DebugLog
    {
        #region Private Fields

        /// <summary>
        /// The underlying MelonLogger instance. Initialized lazily.
        /// </summary>
        private static MelonLogger.Instance _logger;

        /// <summary>
        /// Logger name for MelonLoader integration.
        /// </summary>
        private const string LoggerName = "DedicatedServerMod";

        /// <summary>
        /// Cached reference to ServerConfig for conditional logging.
        /// </summary>
        private static Shared.Configuration.ServerConfig _config;

        /// <summary>
        /// Indicates whether the logger has been initialized.
        /// </summary>
        private static bool _initialized;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the debug log system with a logger instance.
        /// Should be called during mod initialization.
        /// </summary>
        /// <param name="logger">The MelonLogger instance to use</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
        public static void Initialize(MelonLogger.Instance logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _logger = logger;
            _initialized = true;

            Info("DebugLog system initialized");
        }

        /// <summary>
        /// Initializes the debug log system with a default logger.
        /// Called automatically if not explicitly initialized.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                _logger = new MelonLogger.Instance(LoggerName);
                _initialized = true;
            }
        }

        /// <summary>
        /// Gets the current logger instance, creating one if necessary.
        /// </summary>
        private static MelonLogger.Instance Logger
        {
            get
            {
                EnsureInitialized();
                return _logger;
            }
        }

        /// <summary>
        /// Gets the current configuration for conditional logging.
        /// </summary>
        private static Shared.Configuration.ServerConfig Config
        {
            get
            {
                if (_config == null)
                {
                    try
                    {
                        _config = Shared.Configuration.ServerConfig.Instance;
                    }
                    catch
                    {
                        // Config not available yet, use defaults
                    }
                }
                return _config;
            }
        }

        #endregion

        #region Configuration Properties

        /// <summary>
        /// Gets whether debug mode is enabled (for Debug logging).
        /// </summary>
        public static bool IsDebugEnabled
        {
            get
            {
                var config = Config;
                return config?.DebugMode ?? false;
            }
        }

        /// <summary>
        /// Gets whether verbose logging is enabled.
        /// </summary>
        public static bool IsVerboseEnabled
        {
            get
            {
                var config = Config;
                return config?.VerboseLogging ?? false;
            }
        }

        /// <summary>
        /// Gets whether admin command logging is enabled.
        /// </summary>
        public static bool IsAdminCommandLoggingEnabled
        {
            get
            {
                var config = Config;
                return config?.LogAdminCommands ?? true;
            }
        }

        #endregion

        #region Info Logging

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Info(string message)
        {
            Logger.Msg(message);
        }

        /// <summary>
        /// Logs an informational message with a specific color (if supported).
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">The console color</param>
        public static void Info(string message, ConsoleColor color)
        {
            Logger.Msg(color, message);
        }

        /// <summary>
        /// Logs a formatted informational message.
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void Info(string format, params object[] args)
        {
            Logger.Msg(string.Format(format, args));
        }

        #endregion

        #region Warning Logging

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message</param>
        public static void Warning(string message)
        {
            Logger.Warning(message);
        }

        /// <summary>
        /// Logs a warning with format arguments.
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void Warning(string format, params object[] args)
        {
            Logger.Warning(string.Format(format, args));
        }

        /// <summary>
        /// Logs a warning with an exception.
        /// </summary>
        /// <param name="message">The warning message</param>
        /// <param name="exception">The exception that occurred</param>
        public static void Warning(string message, Exception exception)
        {
            Logger.Warning($"{message}: {exception.Message}");
        }

        #endregion

        #region Error Logging

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message</param>
        public static void Error(string message)
        {
            Logger.Error(message);
        }

        /// <summary>
        /// Logs an error with format arguments.
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void Error(string format, params object[] args)
        {
            Logger.Error(string.Format(format, args));
        }

        /// <summary>
        /// Logs an error with an exception.
        /// </summary>
        /// <param name="message">The error context message</param>
        /// <param name="exception">The exception that occurred</param>
        public static void Error(string message, Exception exception)
        {
            Logger.Error($"{message}: {exception}");
        }

        /// <summary>
        /// Logs an exception with full stack trace.
        /// </summary>
        /// <param name="exception">The exception to log</param>
        public static void Error(Exception exception)
        {
            if (exception != null)
            {
                Logger.Error(exception.ToString());
            }
        }

        #endregion

        #region Debug Logging

        /// <summary>
        /// Logs a debug message (only if debugMode is enabled in config).
        /// </summary>
        /// <param name="message">The debug message</param>
        public static void Debug(string message)
        {
            if (IsDebugEnabled)
            {
                Logger.Msg($"[DEBUG] {message}");
            }
        }

        /// <summary>
        /// Logs a formatted debug message (only if debugMode is enabled).
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void Debug(string format, params object[] args)
        {
            if (IsDebugEnabled)
            {
                Logger.Msg($"[DEBUG] {string.Format(format, args)}");
            }
        }

        /// <summary>
        /// Logs debug information about a method call.
        /// </summary>
        /// <param name="methodName">The method being entered</param>
        /// <param name="args">The method arguments</param>
        public static void DebugEnter(string methodName, params object[] args)
        {
            if (IsDebugEnabled)
            {
                var argsStr = args != null && args.Length > 0
                    ? $"({string.Join(", ", args)})"
                    : "()";
                Logger.Msg($"[DEBUG] → {methodName}{argsStr}");
            }
        }

        #endregion

        #region Verbose Logging

        /// <summary>
        /// Logs a verbose message (only if verboseLogging is enabled).
        /// Verbose logging is for trace-level information.
        /// </summary>
        /// <param name="message">The verbose message</param>
        public static void Verbose(string message)
        {
            if (IsVerboseEnabled)
            {
                Logger.Msg($"[VERBOSE] {message}");
            }
        }

        /// <summary>
        /// Logs a formatted verbose message (only if verboseLogging is enabled).
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void Verbose(string format, params object[] args)
        {
            if (IsVerboseEnabled)
            {
                Logger.Msg($"[VERBOSE] {string.Format(format, args)}");
            }
        }

        /// <summary>
        /// Logs detailed variable state for debugging.
        /// </summary>
        /// <param name="variableName">The variable name</param>
        /// <param name="value">The variable value</param>
        public static void Verbose(string variableName, object value)
        {
            if (IsVerboseEnabled)
            {
                Logger.Msg($"[VERBOSE] {variableName} = {value}");
            }
        }

        #endregion

        #region Admin Action Logging

        /// <summary>
        /// Logs an admin action to both console and admin log file.
        /// </summary>
        /// <param name="playerName">The player who performed the action</param>
        /// <param name="steamId">The player's Steam ID</param>
        /// <param name="command">The command executed</param>
        /// <param name="args">The command arguments</param>
        public static void LogAdminAction(string playerName, string steamId, string command, string args = "")
        {
            if (!IsAdminCommandLoggingEnabled)
                return;

            var logMessage = $"Admin Action - Player: {playerName} ({steamId}) | Command: {command}";

            if (!string.IsNullOrEmpty(args))
            {
                logMessage += $" | Args: {args}";
            }

            Info(logMessage);

            // Also write to admin log file
            WriteToAdminLog(logMessage);
        }

        /// <summary>
        /// Logs an admin action using a player object.
        /// </summary>
        /// <param name="player">The player who performed the action</param>
        /// <param name="command">The command executed</param>
        /// <param name="args">The command arguments</param>
        public static void LogAdminAction(ScheduleOne.PlayerScripts.Player player, string command, string args = "")
        {
            if (player == null)
            {
                Warning("LogAdminAction: Player is null");
                return;
            }

            var steamId = Shared.Permissions.PlayerResolver.GetSteamId(player);
            var playerName = player.PlayerName ?? "Unknown";

            LogAdminAction(playerName, steamId ?? "Unknown", command, args);
        }

        /// <summary>
        /// Writes a message to the admin actions log file.
        /// </summary>
        /// <param name="message">The message to write</param>
        internal static void WriteToAdminLog(string message)
        {
            try
            {
                var logPath = Path.Combine(MelonEnvironment.UserDataDirectory, Constants.AdminLOGFileName);
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch (Exception ex)
            {
                Warning($"Failed to write to admin log: {ex.Message}");
            }
        }

        #endregion

        #region Network Logging

        /// <summary>
        /// Logs a network-related message.
        /// </summary>
        /// <param name="connectionId">The connection ID</param>
        /// <param name="action">The action being performed</param>
        /// <param name="details">Additional details</param>
        public static void LogNetwork(int connectionId, string action, string details = "")
        {
            var message = $"[NETWORK] Connection {connectionId}: {action}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            Logger.Msg(message);
        }

        /// <summary>
        /// Logs a custom message send operation.
        /// </summary>
        /// <param name="direction">"Send" or "Receive"</param>
        /// <param name="command">The message command</param>
        /// <param name="dataLength">The data length in bytes</param>
        /// <param name="target">The target connection (if applicable)</param>
        public static void LogMessage(string direction, string command, int dataLength, int? target = null)
        {
            var targetStr = target.HasValue ? $" to={target.Value}" : "";
            Logger.Msg($"[MESSAGE] {direction} cmd='{command}' len={dataLength}{targetStr}");
        }

        #endregion

        #region Performance Logging

        /// <summary>
        /// Logs performance metric information.
        /// </summary>
        /// <param name="operation">The operation being measured</param>
        /// <param name="elapsedMs">Elapsed time in milliseconds</param>
        public static void LogPerformance(string operation, long elapsedMs)
        {
            if (IsDebugEnabled || IsVerboseEnabled)
            {
                Logger.Msg($"[PERF] {operation}: {elapsedMs}ms");
            }
        }

        /// <summary>
        /// Logs memory usage information.
        /// </summary>
        /// <param name="context">The context/memory area</param>
        public static void LogMemory(string context)
        {
            if (IsVerboseEnabled)
            {
                var usedMemory = GC.GetTotalMemory(false);
                Logger.Msg($"[MEMORY] {context}: {usedMemory / 1024 / 1024:F2}MB");
            }
        }

        #endregion

        #region Progress Logging

        /// <summary>
        /// Logs a progress update (for long-running operations).
        /// </summary>
        /// <param name="operation">The operation name</param>
        /// <param name="current">Current progress value</param>
        /// <param name="total">Total progress value</param>
        public static void LogProgress(string operation, int current, int total)
        {
            var percent = total > 0 ? (double)current / total * 100 : 0;
            Logger.Msg($"[PROGRESS] {operation}: {current}/{total} ({percent:F1}%)");
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Resets the debug log system (primarily for testing).
        /// </summary>
        public static void Reset()
        {
            _logger = null;
            _config = null;
            _initialized = false;
        }

        #endregion
    }
}
