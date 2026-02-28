using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using MelonLoader;
using MelonLoader.Utils;
using DedicatedServerMod.Shared.Networking.Messaging;
using DedicatedServerMod.Utils;

using JsonProp = Newtonsoft.Json.JsonPropertyAttribute;
using JsonConv = Newtonsoft.Json.JsonConverterAttribute;

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
        [JsonProp(Utils.Constants.ConfigKeys.ServerName)]
        public string ServerName { get; set; } = "Schedule One Dedicated Server";

        /// <summary>
        /// A description of the server displayed in server lists.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.ServerDescription)]
        public string ServerDescription { get; set; } = "A dedicated server for Schedule One";

        /// <summary>
        /// Maximum number of players allowed to connect.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.MaxPlayers)]
        public int MaxPlayers { get; set; } = Utils.Constants.DefaultMaxPlayers;

        /// <summary>
        /// The network port the server listens on.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.ServerPort)]
        public int ServerPort { get; set; } = Utils.Constants.DefaultServerPort;

        /// <summary>
        /// Password required to connect. Empty string = no password.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.ServerPassword)]
        public string ServerPassword { get; set; } = string.Empty;

        /// <summary>
        /// Whether Steam authentication is required.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.RequireAuthentication)]
        public bool RequireAuthentication { get; set; } = false;

        /// <summary>
        /// Authentication provider used when RequireAuthentication is enabled.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.AuthProvider)]
        [JsonConv(typeof(StringEnumConverter))]
        public AuthenticationProvider AuthProvider { get; set; } = AuthenticationProvider.SteamGameServer;

        /// <summary>
        /// Timeout in seconds for authentication handshake completion.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.AuthTimeoutSeconds)]
        public int AuthTimeoutSeconds { get; set; } = Utils.Constants.DefaultAuthTimeoutSeconds;

        /// <summary>
        /// Whether loopback/local ghost connections bypass authentication requirements.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.AuthAllowLoopbackBypass)]
        public bool AuthAllowLoopbackBypass { get; set; } = true;

        /// <summary>
        /// Whether to log in with Steam game server anonymous account mode.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamGameServerLogOnAnonymous)]
        public bool SteamGameServerLogOnAnonymous { get; set; } = true;

        /// <summary>
        /// Steam game server login token. Used only when anonymous login is disabled.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamGameServerToken)]
        public string SteamGameServerToken { get; set; } = string.Empty;

        /// <summary>
        /// Query port used by Steam server browser and status ping.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamGameServerQueryPort)]
        public int SteamGameServerQueryPort { get; set; } = Utils.Constants.DefaultSteamGameServerQueryPort;

        /// <summary>
        /// Game server version string announced to Steam.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamGameServerVersion)]
        public string SteamGameServerVersion { get; set; } = Utils.Constants.ModVersion;

        /// <summary>
        /// Steam game server authentication mode.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamGameServerMode)]
        [JsonConv(typeof(StringEnumConverter))]
        public SteamGameServerAuthenticationMode SteamGameServerMode { get; set; } = SteamGameServerAuthenticationMode.Authentication;

        /// <summary>
        /// Messaging backend used for custom server-client communication.
        /// FishNetRpc uses FishNet custom RPCs.
        /// SteamP2P uses legacy Steam P2P packets.
        /// SteamNetworkingSockets uses modern Steam sockets and is dedicated-server compatible.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.MessagingBackend)]
        [JsonConv(typeof(StringEnumConverter))]
        public MessagingBackendType MessagingBackend { get; set; } = MessagingBackendType.FishNetRpc;

        /// <summary>
        /// Whether to allow Steam relay (SDR) for P2P messaging.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamP2PAllowRelay)]
        public bool SteamP2PAllowRelay { get; set; } = true;

        /// <summary>
        /// Steam P2P channel for messaging.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamP2PChannel)]
        public int SteamP2PChannel { get; set; } = 0;

        /// <summary>
        /// Steam P2P max payload size in bytes.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamP2PMaxPayloadBytes)]
        public int SteamP2PMaxPayloadBytes { get; set; } = 1200;

        /// <summary>
        /// Target server SteamID for client-side Steam P2P message routing.
        /// Optional when server is discovered from inbound packets.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamP2PServerSteamId)]
        public string SteamP2PServerSteamId { get; set; } = string.Empty;

        /// <summary>
        /// Steam Web API key for web API ticket validation mode.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamWebApiKey)]
        public string SteamWebApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Steam Web API identity string used with web API auth tickets.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.SteamWebApiIdentity)]
        public string SteamWebApiIdentity { get; set; } = "DedicatedServerMod";

        /// <summary>
        /// Whether the server should only accept Steam friends of the host.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.RequireFriends)]
        public bool RequireFriends { get; set; } = false;

        /// <summary>
        /// Whether to register with public server lists.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.PublicServer)]
        public bool PublicServer { get; set; } = true;

        #endregion

        #region Master Server Settings

        /// <summary>
        /// Whether to register with a master server.
        /// </summary>
        [JsonProp("registerWithMasterServer")]
        public bool RegisterWithMasterServer { get; set; } = false;

        /// <summary>
        /// The master server URL for server registration.
        /// </summary>
        [JsonProp("masterServerUrl")]
        public string MasterServerUrl { get; set; } = "https://s1-server-list.example.com";

        /// <summary>
        /// API key for master server authentication.
        /// </summary>
        [JsonProp("masterServerApiKey")]
        public string MasterServerApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Server ID for master server registration.
        /// </summary>
        [JsonProp("masterServerServerId")]
        public string MasterServerServerId { get; set; } = string.Empty;

        /// <summary>
        /// Contact information for server owner.
        /// </summary>
        [JsonProp("masterServerOwnerContact")]
        public string MasterServerOwnerContact { get; set; } = string.Empty;

        /// <summary>
        /// Public address to advertise (if behind NAT).
        /// </summary>
        [JsonProp("publicServerAddress")]
        public string PublicServerAddress { get; set; } = string.Empty;

        #endregion

        #region TCP Console Settings

        /// <summary>
        /// Whether the TCP console server is enabled.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.TcpConsoleEnabled)]
        public bool TcpConsoleEnabled { get; set; } = false;

        /// <summary>
        /// IP address to bind the TCP console to.
        /// Use "127.0.0.1" for local-only, "0.0.0.0" for all interfaces.
        /// </summary>
        [JsonProp("tcpConsoleBindAddress")]
        public string TcpConsoleBindAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// Port for the TCP console server.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.TcpConsolePort)]
        public int TcpConsolePort { get; set; } = Utils.Constants.DefaultTcpConsolePort;

        /// <summary>
        /// Whether authentication is required for TCP console.
        /// </summary>
        [JsonProp("tcpConsoleRequirePassword")]
        public bool TcpConsoleRequirePassword { get; set; } = false;

        /// <summary>
        /// Password for TCP console authentication.
        /// </summary>
        [JsonProp("tcpConsolePassword")]
        public string TcpConsolePassword { get; set; } = string.Empty;

        #endregion

        #region Time & Gameplay Settings

        /// <summary>
        /// Whether to ignore the ghost host player when checking sleep readiness.
        /// </summary>
        [JsonProp("ignoreGhostHostForSleep")]
        public bool IgnoreGhostHostForSleep { get; set; } = true;

        /// <summary>
        /// Whether time progression never stops (always advances).
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.TimeNeverStops)]
        public bool TimeNeverStops { get; set; } = false;

        /// <summary>
        /// Multiplier for time progression (1.0 = real-time).
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.TimeMultiplier)]
        public float TimeProgressionMultiplier { get; set; } = Utils.Constants.DefaultTimeMultiplier;

        /// <summary>
        /// Whether players are allowed to sleep to advance time.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.AllowSleeping)]
        public bool AllowSleeping { get; set; } = true;

        /// <summary>
        /// Whether to pause the game when no players are connected.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.PauseEmpty)]
        public bool PauseGameWhenEmpty { get; set; } = false;

        #endregion

        #region Auto-Save Settings

        /// <summary>
        /// Whether automatic saving is enabled.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.AutoSaveEnabled)]
        public bool AutoSaveEnabled { get; set; } = true;

        /// <summary>
        /// Interval in minutes between automatic saves.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.AutoSaveInterval)]
        public float AutoSaveIntervalMinutes { get; set; } = Utils.Constants.DefaultAutoSaveIntervalMinutes;

        /// <summary>
        /// Whether to save when a player joins.
        /// </summary>
        [JsonProp("autoSaveOnPlayerJoin")]
        public bool AutoSaveOnPlayerJoin { get; set; } = true;

        /// <summary>
        /// Whether to save when a player leaves.
        /// </summary>
        [JsonProp("autoSaveOnPlayerLeave")]
        public bool AutoSaveOnPlayerLeave { get; set; } = true;

        #endregion

        #region Admin/Operator System (delegated to PermissionManager, but kept for compatibility)

        /// <summary>
        /// List of Steam IDs with operator privileges.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.Operators)]
        public HashSet<string> Operators { get; set; } = new HashSet<string>();

        /// <summary>
        /// List of Steam IDs with admin privileges.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.Admins)]
        public HashSet<string> Admins { get; set; } = new HashSet<string>();

        /// <summary>
        /// List of banned Steam IDs.
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.BannedPlayers)]
        public HashSet<string> BannedPlayers { get; set; } = new HashSet<string>();

        /// <summary>
        /// Whether operators can use the admin console.
        /// </summary>
        [JsonProp("enableConsoleForOps")]
        public bool EnableConsoleForOps { get; set; } = true;

        /// <summary>
        /// Whether admins can use the admin console.
        /// </summary>
        [JsonProp("enableConsoleForAdmins")]
        public bool EnableConsoleForAdmins { get; set; } = true;

        /// <summary>
        /// Whether regular players can open the admin console.
        /// </summary>
        [JsonProp("enableConsoleForPlayers")]
        public bool EnableConsoleForPlayers { get; set; } = true;

        /// <summary>
        /// Whether to log admin commands to admin_actions.log.
        /// </summary>
        [JsonProp("logAdminCommands")]
        public bool LogAdminCommands { get; set; } = true;

        /// <summary>
        /// Commands that admins can use (without operator restriction).
        /// </summary>
        [JsonProp("allowedCommands")]
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
        [JsonProp("restrictedCommands")]
        public HashSet<string> RestrictedCommands { get; set; } = new HashSet<string>
        {
            "settimescale", "freecam", "disable", "enable",
            "disablenpcasset", "hideui"
        };

        /// <summary>
        /// Commands that regular players can use.
        /// </summary>
        [JsonProp("playerAllowedCommands")]
        public HashSet<string> PlayerAllowedCommands { get; set; } = new HashSet<string>
        {
            "showfps", "hidefps"
        };

        /// <summary>
        /// Commands disabled for everyone.
        /// </summary>
        [JsonProp("globalDisabledCommands")]
        public HashSet<string> GlobalDisabledCommands { get; set; } = new HashSet<string>
        {
            "save", "endtutorial"
        };

        #endregion

        #region MOTD & Welcome Messages

        /// <summary>
        /// Whether to display the message of the day on connect.
        /// </summary>
        [JsonProp("enableMotd")]
        public bool EnableMotd { get; set; } = true;

        /// <summary>
        /// Message of the day content.
        /// </summary>
        [JsonProp("motdMessage")]
        public string MotdMessage { get; set; } = Utils.Constants.DefaultMotdMessage;

        /// <summary>
        /// Welcome message template. Supports {playerName} and {serverName}.
        /// </summary>
        [JsonProp("welcomeMessage")]
        public string WelcomeMessage { get; set; } = Utils.Constants.DefaultWelcomeMessage;

        /// <summary>
        /// Whether to show messages when players join.
        /// </summary>
        [JsonProp("showPlayerJoinMessages")]
        public bool ShowPlayerJoinMessages { get; set; } = true;

        /// <summary>
        /// Whether to show messages when players leave.
        /// </summary>
        [JsonProp("showPlayerLeaveMessages")]
        public bool ShowPlayerLeaveMessages { get; set; } = true;

        #endregion

        #region Debug & Logging

        /// <summary>
        /// Whether debug mode is enabled (additional logging).
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.DebugMode)]
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Whether verbose logging is enabled (trace-level).
        /// </summary>
        [JsonProp(Utils.Constants.ConfigKeys.VerboseLogging)]
        public bool VerboseLogging { get; set; } = false;

        /// <summary>
        /// Whether to log player actions (movement, etc.).
        /// </summary>
        [JsonProp("logPlayerActions")]
        public bool LogPlayerActions { get; set; } = true;

        /// <summary>
        /// Whether to enable performance monitoring.
        /// </summary>
        [JsonProp("enablePerformanceMonitoring")]
        public bool EnablePerformanceMonitoring { get; set; } = false;

        #endregion

        #region Performance Settings

        /// <summary>
        /// Target framerate for the server (limits CPU usage in headless mode).
        /// -1 = unlimited, 30-60 recommended for dedicated servers.
        /// </summary>
        [JsonProp("targetFrameRate")]
        public int TargetFrameRate { get; set; } = 60;

        /// <summary>
        /// VSync setting (0 = off, 1 = every frame, 2 = every other frame).
        /// Should be 0 for dedicated servers.
        /// </summary>
        [JsonProp("vSyncCount")]
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
        [JsonProp("saveGamePath")]
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
            Path.Combine(MelonEnvironment.UserDataDirectory, Utils.Constants.ConfigFileName);

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
                            Instance.MaxPlayers = Math.Min(maxPlayers, Utils.Constants.MaxAllowedPlayers);
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

                    case "--messaging-backend":
                        if (i + 1 < args.Length &&
                            TryParseMessagingBackend(args[i + 1], out MessagingBackendType messagingBackend))
                        {
                            Instance.MessagingBackend = messagingBackend;
                            Logger.Msg($"Messaging backend set to: {Instance.MessagingBackend}");
                        }
                        break;

                    case "--steam-p2p-relay":
                        if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool allowRelay))
                        {
                            Instance.SteamP2PAllowRelay = allowRelay;
                            Logger.Msg($"Steam P2P relay set to: {Instance.SteamP2PAllowRelay}");
                        }
                        break;

                    case "--steam-p2p-channel":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int p2pChannel))
                        {
                            Instance.SteamP2PChannel = p2pChannel;
                            Logger.Msg($"Steam P2P channel set to: {Instance.SteamP2PChannel}");
                        }
                        break;

                    case "--server-steamid":
                    case "--server-steam-id":
                        if (i + 1 < args.Length)
                        {
                            Instance.SteamP2PServerSteamId = args[i + 1];
                            Logger.Msg("Steam P2P target server SteamID set");
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
            info += $"Messaging Backend: {Instance.MessagingBackend}\n";
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
            if (ServerPort < Utils.Constants.MinPort || ServerPort > Utils.Constants.MaxPort)
            {
                Logger.Warning($"Invalid server port {ServerPort}, using default {Utils.Constants.DefaultServerPort}");
                ServerPort = Utils.Constants.DefaultServerPort;
            }

            // Validate max players
            if (MaxPlayers < 1)
            {
                Logger.Warning($"Invalid max players {MaxPlayers}, using default {Utils.Constants.DefaultMaxPlayers}");
                MaxPlayers = Utils.Constants.DefaultMaxPlayers;
            }
            else if (MaxPlayers > Utils.Constants.MaxAllowedPlayers)
            {
                Logger.Warning($"Max players {MaxPlayers} exceeds limit {Utils.Constants.MaxAllowedPlayers}");
                MaxPlayers = Utils.Constants.MaxAllowedPlayers;
            }

            // Validate time multiplier
            if (TimeProgressionMultiplier < 0 || TimeProgressionMultiplier > Utils.Constants.MaxTimeMultiplier)
            {
                Logger.Warning($"Invalid time multiplier {TimeProgressionMultiplier}, using default {Utils.Constants.DefaultTimeMultiplier}");
                TimeProgressionMultiplier = Utils.Constants.DefaultTimeMultiplier;
            }

            // Validate auth timeout
            if (AuthTimeoutSeconds < 1 || AuthTimeoutSeconds > 120)
            {
                Logger.Warning($"Invalid auth timeout {AuthTimeoutSeconds}, using default {Utils.Constants.DefaultAuthTimeoutSeconds}");
                AuthTimeoutSeconds = Utils.Constants.DefaultAuthTimeoutSeconds;
            }

            if (SteamGameServerQueryPort < Utils.Constants.MinPort || SteamGameServerQueryPort > Utils.Constants.MaxPort)
            {
                Logger.Warning($"Invalid steam game server query port {SteamGameServerQueryPort}, using default {Utils.Constants.DefaultSteamGameServerQueryPort}");
                SteamGameServerQueryPort = Utils.Constants.DefaultSteamGameServerQueryPort;
            }

            if (SteamP2PChannel < 0)
            {
                Logger.Warning($"Invalid Steam P2P channel {SteamP2PChannel}, using channel 0");
                SteamP2PChannel = 0;
            }

            if (SteamP2PMaxPayloadBytes < 256 || SteamP2PMaxPayloadBytes > Utils.Constants.MaxMessageSize)
            {
                Logger.Warning($"Invalid Steam P2P max payload {SteamP2PMaxPayloadBytes}, using 1200");
                SteamP2PMaxPayloadBytes = 1200;
            }

            // Validate auto-save interval
            if (AutoSaveIntervalMinutes < 0 || AutoSaveIntervalMinutes > Utils.Constants.MaxAutoSaveIntervalMinutes)
            {
                Logger.Warning($"Invalid auto-save interval {AutoSaveIntervalMinutes}, using default {Utils.Constants.DefaultAutoSaveIntervalMinutes}");
                AutoSaveIntervalMinutes = Utils.Constants.DefaultAutoSaveIntervalMinutes;
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
            if (ServerName.Length > Utils.Constants.MaxServerNameLength)
            {
                Logger.Warning($"Server name exceeds max length, truncating");
                ServerName = ServerName.Substring(0, Utils.Constants.MaxServerNameLength);
            }

            if (ServerDescription.Length > Utils.Constants.MaxServerDescriptionLength)
            {
                Logger.Warning($"Server description exceeds max length, truncating");
                ServerDescription = ServerDescription.Substring(0, Utils.Constants.MaxServerDescriptionLength);
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

        /// <summary>
        /// Parses the configured messaging backend string to an enum value.
        /// </summary>
        /// <param name="backend">The backend string from command line.</param>
        /// <param name="value">The parsed backend value when parsing succeeds.</param>
        /// <returns>True if parsing succeeded; otherwise false.</returns>
        private static bool TryParseMessagingBackend(string backend, out MessagingBackendType value)
        {
            if (string.IsNullOrWhiteSpace(backend))
            {
                value = MessagingBackendType.FishNetRpc;
                return false;
            }

            switch (backend.Trim().ToLowerInvariant())
            {
                case "fishnet":
                case "fishnetrpc":
                case "fishnet_rpc":
                    value = MessagingBackendType.FishNetRpc;
                    return true;
                case "steamp2p":
                case "steam_p2p":
                    value = MessagingBackendType.SteamP2P;
                    return true;
                case "steamsockets":
                case "steam_socket":
                case "steam_sockets":
                case "steamnetworkingsockets":
                case "steam_networking_sockets":
                    value = MessagingBackendType.SteamNetworkingSockets;
                    return true;
                default:
                    Logger.Warning($"Unknown messaging backend '{backend}'. Valid options: fishnet_rpc, steam_p2p, steam_networking_sockets.");
                    value = MessagingBackendType.FishNetRpc;
                    return false;
            }
        }

        #endregion
    }
}
