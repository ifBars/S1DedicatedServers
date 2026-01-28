using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using MelonLoader;
using MelonLoader.Utils;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Server configuration management system.
    /// Handles all server settings, persistence, and command-line overrides.
    /// </summary>
    /// <remarks>
    /// This class only handles configuration settings. For permission management,
    /// see <see cref="Shared.Permissions.PermissionManager"/> and
    /// <see cref="Shared.Permissions.PlayerResolver"/> for Steam ID utilities.
    /// </remarks>
    [Serializable]
    public sealed class ServerConfig
    {
        #region Server Settings

        /// <summary>
        /// The public name of the server as displayed in server lists.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SERVER_NAME)]
        public string ServerName { get; set; } = "Schedule One Dedicated Server";

        /// <summary>
        /// A description of the server displayed in server lists.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SERVER_DESCRIPTION)]
        public string ServerDescription { get; set; } = "A dedicated server for Schedule One";

        /// <summary>
        /// Maximum number of players allowed to connect.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.MAX_PLAYERS)]
        public int MaxPlayers { get; set; } = Constants.DEFAULT_MAX_PLAYERS;

        /// <summary>
        /// The network port the server listens on.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SERVER_PORT)]
        public int ServerPort { get; set; } = Constants.DEFAULT_SERVER_PORT;

        /// <summary>
        /// Password required to connect. Empty string = no password.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SERVER_PASSWORD)]
        public string ServerPassword { get; set; } = string.Empty;

        /// <summary>
        /// Whether Steam authentication is required.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.REQUIRE_AUTHENTICATION)]
        public bool RequireAuthentication { get; set; } = false;

        /// <summary>
        /// Whether the server should only accept Steam friends of the host.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.REQUIRE_FRIENDS)]
        public bool RequireFriends { get; set; } = false;

        /// <summary>
        /// Whether to register with public server lists.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.PUBLIC_SERVER)]
        public bool PublicServer { get; set; } = true;

        #endregion

        #region Master Server Settings

        /// <summary>
        /// Whether to register with a master server.
        /// </summary>
        [JsonProperty("registerWithMasterServer")]
        public bool RegisterWithMasterServer { get; set; } = false;

        /// <summary>
        /// The master server URL for server registration.
        /// </summary>
        [JsonProperty("masterServerUrl")]
        public string MasterServerUrl { get; set; } = "https://s1-server-list.example.com";

        /// <summary>
        /// API key for master server authentication.
        /// </summary>
        [JsonProperty("masterServerApiKey")]
        public string MasterServerApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Server ID for master server registration.
        /// </summary>
        [JsonProperty("masterServerServerId")]
        public string MasterServerServerId { get; set; } = string.Empty;

        /// <summary>
        /// Contact information for server owner.
        /// </summary>
        [JsonProperty("masterServerOwnerContact")]
        public string MasterServerOwnerContact { get; set; } = string.Empty;

        /// <summary>
        /// Public address to advertise (if behind NAT).
        /// </summary>
        [JsonProperty("publicServerAddress")]
        public string PublicServerAddress { get; set; } = string.Empty;

        #endregion

        #region TCP Console Settings

        /// <summary>
        /// Whether the TCP console server is enabled.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.TCP_CONSOLE_ENABLED)]
        public bool TcpConsoleEnabled { get; set; } = false;

        /// <summary>
        /// IP address to bind the TCP console to.
        /// Use "127.0.0.1" for local-only, "0.0.0.0" for all interfaces.
        /// </summary>
        [JsonProperty("tcpConsoleBindAddress")]
        public string TcpConsoleBindAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// Port for the TCP console server.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.TCP_CONSOLE_PORT)]
        public int TcpConsolePort { get; set; } = Constants.DEFAULT_TCP_CONSOLE_PORT;

        /// <summary>
        /// Whether authentication is required for TCP console.
        /// </summary>
        [JsonProperty("tcpConsoleRequirePassword")]
        public bool TcpConsoleRequirePassword { get; set; } = false;

        /// <summary>
        /// Password for TCP console authentication.
        /// </summary>
        [JsonProperty("tcpConsolePassword")]
        public string TcpConsolePassword { get; set; } = string.Empty;

        #endregion

        #region Time & Gameplay Settings

        /// <summary>
        /// Whether to ignore the ghost host player when checking sleep readiness.
        /// </summary>
        [JsonProperty("ignoreGhostHostForSleep")]
        public bool IgnoreGhostHostForSleep { get; set; } = true;

        /// <summary>
        /// Whether time progression never stops (always advances).
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.TIME_NEVER_STOPS)]
        public bool TimeNeverStops { get; set; } = true;

        /// <summary>
        /// Multiplier for time progression (1.0 = real-time).
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.TIME_MULTIPLIER)]
        public float TimeProgressionMultiplier { get; set; } = Constants.DEFAULT_TIME_MULTIPLIER;

        /// <summary>
        /// Whether players are allowed to sleep to advance time.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.ALLOW_SLEEPING)]
        public bool AllowSleeping { get; set; } = true;

        /// <summary>
        /// Whether to pause the game when no players are connected.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.PAUSE_EMPTY)]
        public bool PauseGameWhenEmpty { get; set; } = false;

        #endregion

        #region Auto-Save Settings

        /// <summary>
        /// Whether automatic saving is enabled.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.AUTO_SAVE_ENABLED)]
        public bool AutoSaveEnabled { get; set; } = true;

        /// <summary>
        /// Interval in minutes between automatic saves.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.AUTO_SAVE_INTERVAL)]
        public float AutoSaveIntervalMinutes { get; set; } = Constants.DEFAULT_AUTO_SAVE_INTERVAL_MINUTES;

        /// <summary>
        /// Whether to save when a player joins.
        /// </summary>
        [JsonProperty("autoSaveOnPlayerJoin")]
        public bool AutoSaveOnPlayerJoin { get; set; } = true;

        /// <summary>
        /// Whether to save when a player leaves.
        /// </summary>
        [JsonProperty("autoSaveOnPlayerLeave")]
        public bool AutoSaveOnPlayerLeave { get; set; } = true;

        #endregion

        #region Admin/Operator System (delegated to PermissionManager, but kept for compatibility)

        /// <summary>
        /// List of Steam IDs with operator privileges.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.OPERATORS)]
        public HashSet<string> Operators { get; set; } = new HashSet<string>();

        /// <summary>
        /// List of Steam IDs with admin privileges.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.ADMINS)]
        public HashSet<string> Admins { get; set; } = new HashSet<string>();

        /// <summary>
        /// List of banned Steam IDs.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.BANNED_PLAYERS)]
        public HashSet<string> BannedPlayers { get; set; } = new HashSet<string>();

        /// <summary>
        /// Whether operators can use the admin console.
        /// </summary>
        [JsonProperty("enableConsoleForOps")]
        public bool EnableConsoleForOps { get; set; } = true;

        /// <summary>
        /// Whether admins can use the admin console.
        /// </summary>
        [JsonProperty("enableConsoleForAdmins")]
        public bool EnableConsoleForAdmins { get; set; } = true;

        /// <summary>
        /// Whether regular players can open the admin console.
        /// </summary>
        [JsonProperty("enableConsoleForPlayers")]
        public bool EnableConsoleForPlayers { get; set; } = true;

        /// <summary>
        /// Whether to log admin commands to admin_actions.log.
        /// </summary>
        [JsonProperty("logAdminCommands")]
        public bool LogAdminCommands { get; set; } = true;

        /// <summary>
        /// Commands that admins can use (without operator restriction).
        /// </summary>
        [JsonProperty("allowedCommands")]
        public HashSet<string> AllowedCommands { get; set; } = new HashSet<string>
        {
            "settime", "teleport", "give", "clearinventory",
            "changecash", "changebalance", "addxp", "spawnvehicle",
            "setmovespeed", "setjumpforce", "setowned", "sethealth",
            "setenergy", "setvar", "setqueststate", "setquestentrystate",
            "setemotion", "setunlocked", "setrelationship", "addemployee",
            "setdiscovered", "growplants", "setlawintensity", "setquality",
            "cleartrash", "raisewanted", "lowerwanted", "clearwanted",
            "packageproduct", "setstaminareserve"
        };

        /// <summary>
        /// Commands that only operators can use.
        /// </summary>
        [JsonProperty("restrictedCommands")]
        public HashSet<string> RestrictedCommands { get; set; } = new HashSet<string>
        {
            "settimescale", "freecam", "disable", "enable",
            "disablenpcasset", "hideui"
        };

        /// <summary>
        /// Commands that regular players can use.
        /// </summary>
        [JsonProperty("playerAllowedCommands")]
        public HashSet<string> PlayerAllowedCommands { get; set; } = new HashSet<string>
        {
            "showfps", "hidefps"
        };

        /// <summary>
        /// Commands disabled for everyone.
        /// </summary>
        [JsonProperty("globalDisabledCommands")]
        public HashSet<string> GlobalDisabledCommands { get; set; } = new HashSet<string>
        {
            "save", "endtutorial"
        };

        #endregion

        #region MOTD & Welcome Messages

        /// <summary>
        /// Whether to display the message of the day on connect.
        /// </summary>
        [JsonProperty("enableMotd")]
        public bool EnableMotd { get; set; } = true;

        /// <summary>
        /// Message of the day content.
        /// </summary>
        [JsonProperty("motdMessage")]
        public string MotdMessage { get; set; } = Constants.DEFAULT_MOTD_MESSAGE;

        /// <summary>
        /// Welcome message template. Supports {playerName} and {serverName}.
        /// </summary>
        [JsonProperty("welcomeMessage")]
        public string WelcomeMessage { get; set; } = Constants.DEFAULT_WELCOME_MESSAGE;

        /// <summary>
        /// Whether to show messages when players join.
        /// </summary>
        [JsonProperty("showPlayerJoinMessages")]
        public bool ShowPlayerJoinMessages { get; set; } = true;

        /// <summary>
        /// Whether to show messages when players leave.
        /// </summary>
        [JsonProperty("showPlayerLeaveMessages")]
        public bool ShowPlayerLeaveMessages { get; set; } = true;

        #endregion

        #region Debug & Logging

        /// <summary>
        /// Whether debug mode is enabled (additional logging).
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.DEBUG_MODE)]
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Whether verbose logging is enabled (trace-level).
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.VERBOSE_LOGGING)]
        public bool VerboseLogging { get; set; } = false;

        /// <summary>
        /// Whether to log player actions (movement, etc.).
        /// </summary>
        [JsonProperty("logPlayerActions")]
        public bool LogPlayerActions { get; set; } = true;

        /// <summary>
        /// Whether to enable performance monitoring.
        /// </summary>
        [JsonProperty("enablePerformanceMonitoring")]
        public bool EnablePerformanceMonitoring { get; set; } = false;

        #endregion

        #region Performance Settings

        /// <summary>
        /// Target framerate for the server (limits CPU usage in headless mode).
        /// -1 = unlimited, 30-60 recommended for dedicated servers.
        /// </summary>
        [JsonProperty("targetFrameRate")]
        public int TargetFrameRate { get; set; } = 60;

        /// <summary>
        /// VSync setting (0 = off, 1 = every frame, 2 = every other frame).
        /// Should be 0 for dedicated servers.
        /// </summary>
        [JsonProperty("vSyncCount")]
        public int VSyncCount { get; set; } = 0;

        #endregion

        #region Save Path (Server)

        /// <summary>
        /// Custom path for save files. Empty = uses default "DedicatedServerSave" folder in UserData.
        /// </summary>
        /// <remarks>
        /// When empty, the server will create a save folder at:
        /// UserData/DedicatedServerSave
        /// 
        /// The folder will be initialized with the DefaultSave template from StreamingAssets
        /// if it doesn't exist or is missing required files.
        /// </remarks>
        [JsonProperty("saveGamePath")]
        public string SaveGamePath { get; set; } = string.Empty;

        #endregion

        #region Static Instance & Management

        /// <summary>
        /// The singleton instance of the server configuration.
        /// </summary>
        private static ServerConfig _instance;

        /// <summary>
        /// The MelonLogger instance for configuration logging.
        /// </summary>
        private static MelonLogger.Instance _logger;

        /// <summary>
        /// The path to the configuration file.
        /// </summary>
        private static string _configPath;

        /// <summary>
        /// Gets the resolved configuration file path.
        /// </summary>
        public static string ConfigFilePath => _configPath ?? 
            Path.Combine(MelonEnvironment.UserDataDirectory, Constants.CONFIG_FILE_NAME);

        /// <summary>
        /// Gets the current server configuration instance.
        /// Loads the configuration if not already loaded.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if configuration not initialized</exception>
        public static ServerConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    LoadConfig();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initializes the server configuration system.
        /// Should be called during server startup.
        /// </summary>
        /// <param name="loggerInstance">The logger instance to use</param>
        /// <param name="configFilePath">Optional custom path for config file</param>
        public static void Initialize(MelonLogger.Instance loggerInstance, string configFilePath = null)
        {
            _logger = loggerInstance;
            _configPath = configFilePath ?? ConfigFilePath;
            LoadConfig();
        }

        /// <summary>
        /// Gets the logger instance for configuration operations.
        /// </summary>
        private static MelonLogger.Instance Logger => _logger ?? 
            new MelonLogger.Instance("ServerConfig");

        #endregion

        #region Config File Management

        /// <summary>
        /// Loads the server configuration from disk.
        /// Creates default configuration if file doesn't exist.
        /// </summary>
        public static void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    _instance = JsonConvert.DeserializeObject<ServerConfig>(json);
                    Logger.Msg("Server configuration loaded successfully");
                }
                else
                {
                    _instance = new ServerConfig();
                    SaveConfig();
                    Logger.Msg("Created new server configuration file");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load server config: {ex}");
                _instance = new ServerConfig();
                SaveConfig();
            }
        }

        /// <summary>
        /// Saves the current configuration to disk.
        /// </summary>
        public static void SaveConfig()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_instance, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
                Logger.Msg("Server configuration saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save server config: {ex}");
            }
        }

        /// <summary>
        /// Reloads the configuration from disk.
        /// </summary>
        public static void ReloadConfig()
        {
            Logger.Msg("Reloading server configuration...");
            LoadConfig();
        }

        /// <summary>
        /// Resets the configuration (primarily for testing).
        /// </summary>
        public static void Reset()
        {
            _instance = null;
            _logger = null;
            _configPath = null;
        }

        #endregion

        #region Command Line Integration

        /// <summary>
        /// Parses command line arguments and applies them to the configuration.
        /// </summary>
        /// <param name="args">The command line arguments</param>
        public static void ParseCommandLineArgs(string[] args)
        {
            Logger.Msg("Parsing command line arguments for server config...");

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--server-name":
                        if (i + 1 < args.Length)
                        {
                            Instance.ServerName = args[i + 1];
                            Logger.Msg($"Server name set to: {Instance.ServerName}");
                        }
                        break;

                    case "--max-players":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int maxPlayers))
                        {
                            Instance.MaxPlayers = Math.Min(maxPlayers, Constants.MAX_ALLOWED_PLAYERS);
                            Logger.Msg($"Max players set to: {Instance.MaxPlayers}");
                        }
                        break;

                    case "--server-password":
                        if (i + 1 < args.Length)
                        {
                            Instance.ServerPassword = args[i + 1];
                            Logger.Msg("Server password set");
                        }
                        break;

                    case "--add-operator":
                        if (i + 1 < args.Length)
                        {
                            Instance.Operators.Add(args[i + 1]);
                            Logger.Msg($"Added operator: {args[i + 1]}");
                        }
                        break;

                    case "--add-admin":
                        if (i + 1 < args.Length)
                        {
                            Instance.Admins.Add(args[i + 1]);
                            Logger.Msg($"Added admin: {args[i + 1]}");
                        }
                        break;

                    case "--debug":
                        Instance.DebugMode = true;
                        Logger.Msg("Debug mode enabled");
                        break;

                    case "--verbose":
                        Instance.VerboseLogging = true;
                        Logger.Msg("Verbose logging enabled");
                        break;

                    case "--tcp-console":
                        Instance.TcpConsoleEnabled = true;
                        Logger.Msg("TCP console enabled via CLI");
                        break;

                    case "--tcp-console-port":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int tcpPort))
                        {
                            Instance.TcpConsolePort = tcpPort;
                            Logger.Msg($"TCP console port set to: {Instance.TcpConsolePort}");
                        }
                        break;

                    case "--tcp-console-bind":
                        if (i + 1 < args.Length)
                        {
                            Instance.TcpConsoleBindAddress = args[i + 1];
                            Logger.Msg($"TCP console bind address set to: {Instance.TcpConsoleBindAddress}");
                        }
                        break;

                    case "--tcp-console-password":
                        if (i + 1 < args.Length)
                        {
                            Instance.TcpConsolePassword = args[i + 1];
                            Instance.TcpConsoleRequirePassword = true;
                            Logger.Msg("TCP console password set via CLI and requirement enabled");
                        }
                        break;

                    case "--target-framerate":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int fps))
                        {
                            Instance.TargetFrameRate = fps;
                            Logger.Msg($"Target framerate set to: {fps}");
                        }
                        break;

                    case "--vsync":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int vsync))
                        {
                            Instance.VSyncCount = Math.Clamp(vsync, 0, 2);
                            Logger.Msg($"VSync set to: {vsync}");
                        }
                        break;
                }
            }

            SaveConfig();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets the resolved save game path (either custom or default).
        /// </summary>
        /// <returns>The absolute path to the save game folder</returns>
        public static string GetResolvedSaveGamePath()
        {
            if (!string.IsNullOrEmpty(Instance.SaveGamePath))
            {
                return Instance.SaveGamePath;
            }

            // Use default save location in UserData
            return Path.Combine(MelonEnvironment.UserDataDirectory, "DedicatedServerSave");
        }

        /// <summary>
        /// Gets a formatted server information string.
        /// </summary>
        /// <returns>A string containing key server information</returns>
        public static string GetServerInfo()
        {
            var info = "=== Server Configuration ===\n";
            info += $"Server Name: {Instance.ServerName}\n";
            info += $"Max Players: {Instance.MaxPlayers}\n";
            info += $"Server Port: {Instance.ServerPort}\n";
            info += $"Password Protected: {!string.IsNullOrEmpty(Instance.ServerPassword)}\n";
            info += $"Public Server: {Instance.PublicServer}\n";
            info += $"Operators: {Instance.Operators.Count}\n";
            info += $"Admins: {Instance.Admins.Count}\n";
            info += $"Auto-Save: {Instance.AutoSaveEnabled} ({Instance.AutoSaveIntervalMinutes}min)\n";
            info += $"Time Never Stops: {Instance.TimeNeverStops}\n";
            info += $"Debug Mode: {Instance.DebugMode}\n";

            return info;
        }

        /// <summary>
        /// Validates the configuration settings.
        /// Logs warnings for invalid values.
        /// </summary>
        public void Validate()
        {
            // Validate port
            if (ServerPort < Constants.MIN_PORT || ServerPort > Constants.MAX_PORT)
            {
                Logger.Warning($"Invalid server port {ServerPort}, using default {Constants.DEFAULT_SERVER_PORT}");
                ServerPort = Constants.DEFAULT_SERVER_PORT;
            }

            // Validate max players
            if (MaxPlayers < 1)
            {
                Logger.Warning($"Invalid max players {MaxPlayers}, using default {Constants.DEFAULT_MAX_PLAYERS}");
                MaxPlayers = Constants.DEFAULT_MAX_PLAYERS;
            }
            else if (MaxPlayers > Constants.MAX_ALLOWED_PLAYERS)
            {
                Logger.Warning($"Max players {MaxPlayers} exceeds limit {Constants.MAX_ALLOWED_PLAYERS}");
                MaxPlayers = Constants.MAX_ALLOWED_PLAYERS;
            }

            // Validate time multiplier
            if (TimeProgressionMultiplier < 0 || TimeProgressionMultiplier > Constants.MAX_TIME_MULTIPLIER)
            {
                Logger.Warning($"Invalid time multiplier {TimeProgressionMultiplier}, using default {Constants.DEFAULT_TIME_MULTIPLIER}");
                TimeProgressionMultiplier = Constants.DEFAULT_TIME_MULTIPLIER;
            }

            // Validate auto-save interval
            if (AutoSaveIntervalMinutes < 0 || AutoSaveIntervalMinutes > Constants.MAX_AUTO_SAVE_INTERVAL_MINUTES)
            {
                Logger.Warning($"Invalid auto-save interval {AutoSaveIntervalMinutes}, using default {Constants.DEFAULT_AUTO_SAVE_INTERVAL_MINUTES}");
                AutoSaveIntervalMinutes = Constants.DEFAULT_AUTO_SAVE_INTERVAL_MINUTES;
            }

            // Validate target framerate
            if (TargetFrameRate < -1 || TargetFrameRate > 300)
            {
                Logger.Warning($"Invalid target framerate {TargetFrameRate}, using default 60");
                TargetFrameRate = 60;
            }

            // Validate VSync
            if (VSyncCount < 0 || VSyncCount > 2)
            {
                Logger.Warning($"Invalid VSync count {VSyncCount}, using default 0");
                VSyncCount = 0;
            }

            // Validate names
            if (ServerName.Length > Constants.MAX_SERVER_NAME_LENGTH)
            {
                Logger.Warning($"Server name exceeds max length, truncating");
                ServerName = ServerName.Substring(0, Constants.MAX_SERVER_NAME_LENGTH);
            }

            if (ServerDescription.Length > Constants.MAX_SERVER_DESCRIPTION_LENGTH)
            {
                Logger.Warning($"Server description exceeds max length, truncating");
                ServerDescription = ServerDescription.Substring(0, Constants.MAX_SERVER_DESCRIPTION_LENGTH);
            }
        }

        #endregion
    }
}
