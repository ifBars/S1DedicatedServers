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

        /// <summary>
        /// Raised when a structured log entry is emitted through this utility.
        /// </summary>
        public static event Action<DebugLogEntry> EntryWritten;

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

        /// <summary>
        /// Gets whether umbrella networking debug logging is enabled.
        /// </summary>
        public static bool IsNetworkingDebugLoggingEnabled
        {
            get
            {
                var config = Config;
                return config?.LogNetworkingDebug ?? false;
            }
        }

        /// <summary>
        /// Gets whether message routing debug logging is enabled.
        /// </summary>
        public static bool IsMessageRoutingDebugLoggingEnabled
        {
            get
            {
                var config = Config;
                return (config?.LogMessageRoutingDebug ?? false) || (config?.LogNetworkingDebug ?? false);
            }
        }

        /// <summary>
        /// Gets whether custom messaging debug logging is enabled.
        /// </summary>
        public static bool IsMessagingBackendDebugLoggingEnabled
        {
            get
            {
                var config = Config;
                return (config?.LogMessagingBackendDebug ?? false) || (config?.LogNetworkingDebug ?? false);
            }
        }

        /// <summary>
        /// Gets whether startup debug logging is enabled.
        /// </summary>
        public static bool IsStartupDebugLoggingEnabled
        {
            get
            {
                var config = Config;
                return config?.LogStartupDebug ?? false;
            }
        }

        /// <summary>
        /// Gets whether server network debug logging is enabled.
        /// </summary>
        public static bool IsServerNetworkDebugLoggingEnabled
        {
            get
            {
                var config = Config;
                return config?.LogServerNetworkDebug ?? false;
            }
        }

        /// <summary>
        /// Gets whether player lifecycle debug logging is enabled.
        /// </summary>
        public static bool IsPlayerLifecycleDebugLoggingEnabled
        {
            get
            {
                var config = Config;
                return config?.LogPlayerLifecycleDebug ?? false;
            }
        }

        /// <summary>
        /// Gets whether authentication debug logging is enabled.
        /// </summary>
        public static bool IsAuthenticationDebugLoggingEnabled
        {
            get
            {
                var config = Config;
                return config?.LogAuthenticationDebug ?? false;
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
            Publish(DebugLogLevel.Info, message);
        }

        /// <summary>
        /// Logs an informational message with a specific color (if supported).
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="color">The console color</param>
        public static void Info(string message, ConsoleColor color)
        {
            Logger.Msg(color, message);
            Publish(DebugLogLevel.Info, message);
        }

        /// <summary>
        /// Logs a formatted informational message.
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void Info(string format, params object[] args)
        {
            string message = string.Format(format, args);
            Logger.Msg(message);
            Publish(DebugLogLevel.Info, message);
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
            Publish(DebugLogLevel.Warning, message);
        }

        /// <summary>
        /// Logs a warning with format arguments.
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void Warning(string format, params object[] args)
        {
            string message = string.Format(format, args);
            Logger.Warning(message);
            Publish(DebugLogLevel.Warning, message);
        }

        /// <summary>
        /// Logs a warning with an exception.
        /// </summary>
        /// <param name="message">The warning message</param>
        /// <param name="exception">The exception that occurred</param>
        public static void Warning(string message, Exception exception)
        {
            string rendered = $"{message}: {exception.Message}";
            Logger.Warning(rendered);
            Publish(DebugLogLevel.Warning, rendered);
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
            Publish(DebugLogLevel.Error, message);
        }

        /// <summary>
        /// Logs an error with format arguments.
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void Error(string format, params object[] args)
        {
            string message = string.Format(format, args);
            Logger.Error(message);
            Publish(DebugLogLevel.Error, message);
        }

        /// <summary>
        /// Logs an error with an exception.
        /// </summary>
        /// <param name="message">The error context message</param>
        /// <param name="exception">The exception that occurred</param>
        public static void Error(string message, Exception exception)
        {
            string rendered = $"{message}: {exception}";
            Logger.Error(rendered);
            Publish(DebugLogLevel.Error, rendered);
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
                Publish(DebugLogLevel.Error, exception.ToString());
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
                string rendered = $"[DEBUG] {message}";
                Logger.Msg(rendered);
                Publish(DebugLogLevel.Debug, rendered);
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
                string rendered = $"[DEBUG] {string.Format(format, args)}";
                Logger.Msg(rendered);
                Publish(DebugLogLevel.Debug, rendered);
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
                string rendered = $"[DEBUG] → {methodName}{argsStr}";
                Logger.Msg(rendered);
                Publish(DebugLogLevel.Debug, rendered);
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
                string rendered = $"[VERBOSE] {message}";
                Logger.Msg(rendered);
                Publish(DebugLogLevel.Verbose, rendered);
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
                string rendered = $"[VERBOSE] {string.Format(format, args)}";
                Logger.Msg(rendered);
                Publish(DebugLogLevel.Verbose, rendered);
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
                string rendered = $"[VERBOSE] {variableName} = {value}";
                Logger.Msg(rendered);
                Publish(DebugLogLevel.Verbose, rendered);
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
            Publish(DebugLogLevel.Info, message);
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
            Publish(DebugLogLevel.Info, $"[MESSAGE] {direction} cmd='{command}' len={dataLength}{targetStr}");
        }

        /// <summary>
        /// Logs a message routing debug entry when routing debug logging is enabled.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void MessageRoutingDebug(string message)
        {
            if (IsMessageRoutingDebugLoggingEnabled)
            {
                Logger.Msg($"[DEBUG][NETWORK][ROUTING] {message}");
                Publish(DebugLogLevel.Debug, $"[DEBUG][NETWORK][ROUTING] {message}");
            }
        }

        /// <summary>
        /// Logs a custom messaging debug entry when custom messaging debug logging is enabled.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void MessagingBackendDebug(string message)
        {
            if (IsMessagingBackendDebugLoggingEnabled)
            {
                Logger.Msg($"[DEBUG][NETWORK][BACKEND] {message}");
                Publish(DebugLogLevel.Debug, $"[DEBUG][NETWORK][BACKEND] {message}");
            }
        }

        /// <summary>
        /// Logs a startup debug entry when startup debug logging is enabled.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void StartupDebug(string message)
        {
            if (IsStartupDebugLoggingEnabled)
            {
                Logger.Msg($"[DEBUG][STARTUP] {message}");
                Publish(DebugLogLevel.Debug, $"[DEBUG][STARTUP] {message}");
            }
        }

        /// <summary>
        /// Logs a server network debug entry when transport/network debug logging is enabled.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void ServerNetworkDebug(string message)
        {
            if (IsServerNetworkDebugLoggingEnabled)
            {
                Logger.Msg($"[DEBUG][SERVER][NETWORK] {message}");
                Publish(DebugLogLevel.Debug, $"[DEBUG][SERVER][NETWORK] {message}");
            }
        }

        /// <summary>
        /// Logs a player lifecycle debug entry when player lifecycle debug logging is enabled.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void PlayerLifecycleDebug(string message)
        {
            if (IsPlayerLifecycleDebugLoggingEnabled)
            {
                Logger.Msg($"[DEBUG][PLAYER] {message}");
                Publish(DebugLogLevel.Debug, $"[DEBUG][PLAYER] {message}");
            }
        }

        /// <summary>
        /// Logs an authentication debug entry when authentication debug logging is enabled.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void AuthenticationDebug(string message)
        {
            if (IsAuthenticationDebugLoggingEnabled)
            {
                Logger.Msg($"[DEBUG][AUTH] {message}");
                Publish(DebugLogLevel.Debug, $"[DEBUG][AUTH] {message}");
            }
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
                Publish(DebugLogLevel.Debug, $"[PERF] {operation}: {elapsedMs}ms");
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
                Publish(DebugLogLevel.Verbose, $"[MEMORY] {context}: {usedMemory / 1024 / 1024:F2}MB");
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
            string rendered = $"[PROGRESS] {operation}: {current}/{total} ({percent:F1}%)";
            Logger.Msg(rendered);
            Publish(DebugLogLevel.Info, rendered);
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

        private static void Publish(DebugLogLevel level, string message)
        {
            EntryWritten?.Invoke(new DebugLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Level = level,
                Message = message ?? string.Empty
            });
        }

        #endregion
    }
}
