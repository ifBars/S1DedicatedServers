using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
        [JsonProperty(Constants.ConfigKeys.ServerName)]
        public string ServerName { get; set; } = "Schedule One Dedicated Server";

        /// <summary>
        /// A description of the server displayed in server lists.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.ServerDescription)]
        public string ServerDescription { get; set; } = "A dedicated server for Schedule One";

        /// <summary>
        /// Maximum number of players allowed to connect.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.MaxPlayers)]
        public int MaxPlayers { get; set; } = Constants.DefaultMaxPlayers;

        /// <summary>
        /// The network port the server listens on.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.ServerPort)]
        public int ServerPort { get; set; } = Constants.DefaultServerPort;

        /// <summary>
        /// Password required to connect. Empty string = no password.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.ServerPassword)]
        public string ServerPassword { get; set; } = string.Empty;

        /// <summary>
        /// Whether Steam authentication is required.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.RequireAuthentication)]
        public bool RequireAuthentication { get; set; } = false;

        /// <summary>
        /// Authentication provider used when RequireAuthentication is enabled.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.AuthProvider)]
        [JsonConverter(typeof(StringEnumConverter))]
        public AuthenticationProvider AuthProvider { get; set; } = AuthenticationProvider.SteamGameServer;

        /// <summary>
        /// Timeout in seconds for authentication handshake completion.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.AuthTimeoutSeconds)]
        public int AuthTimeoutSeconds { get; set; } = Constants.DefaultAuthTimeoutSeconds;

        /// <summary>
        /// Whether loopback/local ghost connections bypass authentication requirements.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.AuthAllowLoopbackBypass)]
        public bool AuthAllowLoopbackBypass { get; set; } = true;

        /// <summary>
        /// Whether to log in with Steam game server anonymous account mode.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SteamGameServerLogOnAnonymous)]
        public bool SteamGameServerLogOnAnonymous { get; set; } = true;

        /// <summary>
        /// Steam game server login token. Used only when anonymous login is disabled.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SteamGameServerToken)]
        public string SteamGameServerToken { get; set; } = string.Empty;

        /// <summary>
        /// Query port used by Steam server browser and status ping.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SteamGameServerQueryPort)]
        public int SteamGameServerQueryPort { get; set; } = Constants.DefaultSteamGameServerQueryPort;

        /// <summary>
        /// Game server version string announced to Steam.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SteamGameServerVersion)]
        public string SteamGameServerVersion { get; set; } = Constants.ModVersion;

        /// <summary>
        /// Steam game server authentication mode.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SteamGameServerMode)]
        [JsonConverter(typeof(StringEnumConverter))]
        public SteamGameServerAuthenticationMode SteamGameServerMode { get; set; } = SteamGameServerAuthenticationMode.Authentication;

        /// <summary>
        /// Steam Web API key for web API ticket validation mode.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SteamWebApiKey)]
        public string SteamWebApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Steam Web API identity string used with web API auth tickets.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.SteamWebApiIdentity)]
        public string SteamWebApiIdentity { get; set; } = "DedicatedServerMod";

        /// <summary>
        /// Whether the server should only accept Steam friends of the host.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.RequireFriends)]
        public bool RequireFriends { get; set; } = false;

        /// <summary>
        /// Whether to register with public server lists.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.PublicServer)]
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
        [JsonProperty(Constants.ConfigKeys.TcpConsoleEnabled)]
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
        [JsonProperty(Constants.ConfigKeys.TcpConsolePort)]
        public int TcpConsolePort { get; set; } = Constants.DefaultTcpConsolePort;

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
        [JsonProperty(Constants.ConfigKeys.TimeNeverStops)]
        public bool TimeNeverStops { get; set; } = true;

        /// <summary>
        /// Multiplier for time progression (1.0 = real-time).
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.TimeMultiplier)]
        public float TimeProgressionMultiplier { get; set; } = Constants.DefaultTimeMultiplier;

        /// <summary>
        /// Whether players are allowed to sleep to advance time.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.AllowSleeping)]
        public bool AllowSleeping { get; set; } = true;

        /// <summary>
        /// Whether to pause the game when no players are connected.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.PauseEmpty)]
        public bool PauseGameWhenEmpty { get; set; } = false;

        #endregion

        #region Auto-Save Settings

        /// <summary>
        /// Whether automatic saving is enabled.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.AutoSaveEnabled)]
        public bool AutoSaveEnabled { get; set; } = true;

        /// <summary>
        /// Interval in minutes between automatic saves.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.AutoSaveInterval)]
        public float AutoSaveIntervalMinutes { get; set; } = Constants.DefaultAutoSaveIntervalMinutes;

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
        [JsonProperty(Constants.ConfigKeys.Operators)]
        public HashSet<string> Operators { get; set; } = new HashSet<string>();

        /// <summary>
        /// List of Steam IDs with admin privileges.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.Admins)]
        public HashSet<string> Admins { get; set; } = new HashSet<string>();

        /// <summary>
        /// List of banned Steam IDs.
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.BannedPlayers)]
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
        public string MotdMessage { get; set; } = Constants.DefaultMotdMessage;

        /// <summary>
        /// Welcome message template. Supports {playerName} and {serverName}.
        /// </summary>
        [JsonProperty("welcomeMessage")]
        public string WelcomeMessage { get; set; } = Constants.DefaultWelcomeMessage;

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
        [JsonProperty(Constants.ConfigKeys.DebugMode)]
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Whether verbose logging is enabled (trace-level).
        /// </summary>
        [JsonProperty(Constants.ConfigKeys.VerboseLogging)]
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
            Path.Combine(MelonEnvironment.UserDataDirectory, Constants.ConfigFileName);

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
                            Instance.MaxPlayers = Math.Min(maxPlayers, Constants.MaxAllowedPlayers);
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

                    case "--require-authentication":
                    case "--require-auth":
                        Instance.RequireAuthentication = true;
                        Logger.Msg("Authentication requirement enabled");
                        break;

                    case "--auth-provider":
                        if (i + 1 < args.Length &&
                            TryParseAuthenticationProvider(args[i + 1], out AuthenticationProvider authProvider))
                        {
                            Instance.AuthProvider = authProvider;
                            Logger.Msg($"Authentication provider set to: {Instance.AuthProvider}");
                        }
                        break;

                    case "--auth-timeout":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int authTimeoutSeconds))
                        {
                            Instance.AuthTimeoutSeconds = authTimeoutSeconds;
                            Logger.Msg($"Authentication timeout set to: {Instance.AuthTimeoutSeconds}s");
                        }
                        break;

                    case "--steam-gs-anonymous":
                        Instance.SteamGameServerLogOnAnonymous = true;
                        Logger.Msg("Steam game server anonymous login enabled");
                        break;

                    case "--steam-gs-token":
                        if (i + 1 < args.Length)
                        {
                            Instance.SteamGameServerToken = args[i + 1];
                            Instance.SteamGameServerLogOnAnonymous = false;
                            Logger.Msg("Steam game server token set and anonymous login disabled");
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
            info += $"Authentication Required: {Instance.RequireAuthentication}\n";
            info += $"Auth Provider: {Instance.AuthProvider}\n";
            info += $"Auth Timeout: {Instance.AuthTimeoutSeconds}s\n";
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
            if (ServerPort < Constants.MinPort || ServerPort > Constants.MaxPort)
            {
                Logger.Warning($"Invalid server port {ServerPort}, using default {Constants.DefaultServerPort}");
                ServerPort = Constants.DefaultServerPort;
            }

            // Validate max players
            if (MaxPlayers < 1)
            {
                Logger.Warning($"Invalid max players {MaxPlayers}, using default {Constants.DefaultMaxPlayers}");
                MaxPlayers = Constants.DefaultMaxPlayers;
            }
            else if (MaxPlayers > Constants.MaxAllowedPlayers)
            {
                Logger.Warning($"Max players {MaxPlayers} exceeds limit {Constants.MaxAllowedPlayers}");
                MaxPlayers = Constants.MaxAllowedPlayers;
            }

            // Validate time multiplier
            if (TimeProgressionMultiplier < 0 || TimeProgressionMultiplier > Constants.MaxTimeMultiplier)
            {
                Logger.Warning($"Invalid time multiplier {TimeProgressionMultiplier}, using default {Constants.DefaultTimeMultiplier}");
                TimeProgressionMultiplier = Constants.DefaultTimeMultiplier;
            }

            // Validate auth timeout
            if (AuthTimeoutSeconds < 1 || AuthTimeoutSeconds > 120)
            {
                Logger.Warning($"Invalid auth timeout {AuthTimeoutSeconds}, using default {Constants.DefaultAuthTimeoutSeconds}");
                AuthTimeoutSeconds = Constants.DefaultAuthTimeoutSeconds;
            }

            if (SteamGameServerQueryPort < Constants.MinPort || SteamGameServerQueryPort > Constants.MaxPort)
            {
                Logger.Warning($"Invalid steam game server query port {SteamGameServerQueryPort}, using default {Constants.DefaultSteamGameServerQueryPort}");
                SteamGameServerQueryPort = Constants.DefaultSteamGameServerQueryPort;
            }

            // Validate auto-save interval
            if (AutoSaveIntervalMinutes < 0 || AutoSaveIntervalMinutes > Constants.MaxAutoSaveIntervalMinutes)
            {
                Logger.Warning($"Invalid auto-save interval {AutoSaveIntervalMinutes}, using default {Constants.DefaultAutoSaveIntervalMinutes}");
                AutoSaveIntervalMinutes = Constants.DefaultAutoSaveIntervalMinutes;
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
            if (ServerName.Length > Constants.MaxServerNameLength)
            {
                Logger.Warning($"Server name exceeds max length, truncating");
                ServerName = ServerName.Substring(0, Constants.MaxServerNameLength);
            }

            if (ServerDescription.Length > Constants.MaxServerDescriptionLength)
            {
                Logger.Warning($"Server description exceeds max length, truncating");
                ServerDescription = ServerDescription.Substring(0, Constants.MaxServerDescriptionLength);
            }

            if (RequireAuthentication)
            {
                if (AuthProvider == AuthenticationProvider.SteamWebApi && string.IsNullOrWhiteSpace(SteamWebApiKey))
                {
                    Logger.Warning("Auth provider is SteamWebApi but steamWebApiKey is empty. Authentication will fail until configured.");
                }

                if (AuthProvider == AuthenticationProvider.SteamGameServer &&
                    !SteamGameServerLogOnAnonymous &&
                    string.IsNullOrWhiteSpace(SteamGameServerToken))
                {
                    Logger.Warning("Auth provider is SteamGameServer with anonymous login disabled, but steamGameServerToken is empty.");
                }
            }
        }

        /// <summary>
        /// Parses the configured authentication provider string to an enum value.
        /// </summary>
        /// <param name="provider">The provider string from command line.</param>
        /// <param name="value">The parsed provider value when parsing succeeds.</param>
        /// <returns>True if parsing succeeded; otherwise false.</returns>
        private static bool TryParseAuthenticationProvider(string provider, out AuthenticationProvider value)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                value = AuthenticationProvider.SteamGameServer;
                return false;
            }

            switch (provider.Trim().ToLowerInvariant())
            {
                case "none":
                    value = AuthenticationProvider.None;
                    return true;
                case "steamwebapi":
                case "steam_web_api":
                    value = AuthenticationProvider.SteamWebApi;
                    return true;
                case "steamgameserver":
                case "steam_game_server":
                    value = AuthenticationProvider.SteamGameServer;
                    return true;
                default:
                    value = AuthenticationProvider.SteamGameServer;
                    Logger.Warning($"Unknown auth provider '{provider}'. Valid options: none, steam_web_api, steam_game_server.");
                    return false;
            }
        }

        #endregion
    }
}
