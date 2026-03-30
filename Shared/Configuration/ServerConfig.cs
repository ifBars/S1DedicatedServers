using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
    public sealed partial class ServerConfig
    {
        #region Server Settings

        /// <summary>
        /// The public name of the server as displayed in server lists.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.ServerName)]
        public string ServerName { get; set; } = "Schedule One Dedicated Server";

        /// <summary>
        /// A description of the server displayed in server lists.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.ServerDescription)]
        public string ServerDescription { get; set; } = "A dedicated server for Schedule One";

        /// <summary>
        /// Maximum number of players allowed to connect.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.MaxPlayers)]
        public int MaxPlayers { get; set; } = Constants.DefaultMaxPlayers;

        /// <summary>
        /// The network port the server listens on.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.ServerPort)]
        public int ServerPort { get; set; } = Constants.DefaultServerPort;

        /// <summary>
        /// Password required to connect. Empty string = no password.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.ServerPassword)]
        public string ServerPassword { get; set; } = string.Empty;

        /// <summary>
        /// Authentication provider used for dedicated-server client validation.
        /// Set to <see cref="AuthenticationProvider.None"/> to disable authentication entirely.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.AuthProvider)]
        [JsonConv(typeof(StringEnumConverter))]
        public AuthenticationProvider AuthProvider { get; set; } = AuthenticationProvider.None;

        /// <summary>
        /// Legacy compatibility shim for old configs that still persist <c>requireAuthentication</c>.
        /// This is no longer serialized and only influences <see cref="AuthProvider"/> during load.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.RequireAuthentication)]
        private bool? LegacyRequireAuthentication
        {
            set => _legacyRequireAuthentication = value;
        }

        /// <summary>
        /// Whether authentication is currently enabled.
        /// </summary>
        [JsonIgnore]
        public bool AuthenticationEnabled => AuthProvider != AuthenticationProvider.None;

        /// <summary>
        /// Legacy compatibility alias for older code paths.
        /// </summary>
        [JsonIgnore]
        public bool RequireAuthentication
        {
            get => AuthenticationEnabled;
            set
            {
                if (!value)
                {
                    AuthProvider = AuthenticationProvider.None;
                    return;
                }

                if (AuthProvider == AuthenticationProvider.None)
                {
                    AuthProvider = AuthenticationProvider.SteamGameServer;
                }
            }
        }

        /// <summary>
        /// Timeout in seconds for authentication handshake completion.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.AuthTimeoutSeconds)]
        public int AuthTimeoutSeconds { get; set; } = Constants.DefaultAuthTimeoutSeconds;

        /// <summary>
        /// Whether loopback/local ghost connections bypass authentication requirements.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.AuthAllowLoopbackBypass)]
        public bool AuthAllowLoopbackBypass { get; set; } = true;

        /// <summary>
        /// Whether the client mod verification handshake is enabled.
        /// </summary>
        /// <remarks>
        /// Enabled by default. When enabled, authenticated players must complete the dedicated
        /// client mod verification handshake before their join flow is finalized.
        /// </remarks>
        [JsonProp(Constants.ConfigKeys.ModVerificationEnabled)]
        public bool ModVerificationEnabled { get; set; } = true;

        /// <summary>
        /// Timeout in seconds for client mod verification.
        /// </summary>
        /// <remarks>
        /// This timeout begins after authentication succeeds or is bypassed. Clients that do not
        /// return a verification report in time are disconnected before normal gameplay traffic is allowed.
        /// </remarks>
        [JsonProp(Constants.ConfigKeys.ModVerificationTimeoutSeconds)]
        public int ModVerificationTimeoutSeconds { get; set; } = Constants.DefaultModVerificationTimeoutSeconds;

        /// <summary>
        /// Whether to reject clients that match the embedded risky client mod catalog.
        /// </summary>
        /// <remarks>
        /// This is enabled by default and is intended to catch known high-risk tools such as
        /// runtime inspectors or mutation utilities even when unpaired client-only mods are otherwise allowed.
        /// </remarks>
        [JsonProp(Constants.ConfigKeys.BlockKnownRiskyClientMods)]
        public bool BlockKnownRiskyClientMods { get; set; } = true;

        /// <summary>
        /// Whether unpaired client-only mods are allowed.
        /// </summary>
        /// <remarks>
        /// This defaults to <see langword="true"/> for usability. Disable it only when you want
        /// the server to reject client-only mods that are not paired with an installed server mod.
        /// </remarks>
        [JsonProp(Constants.ConfigKeys.AllowUnpairedClientMods)]
        public bool AllowUnpairedClientMods { get; set; } = true;

        /// <summary>
        /// Whether strict client mod mode is enabled.
        /// </summary>
        /// <remarks>
        /// Strict mode hardens verification by requiring exact hash pins for paired companions and
        /// approved unpaired client-only mods. It is intended for hardened or private servers and
        /// can cause startup validation failures when required companions do not provide strict hashes.
        /// </remarks>
        [JsonProp(Constants.ConfigKeys.StrictClientModMode)]
        public bool StrictClientModMode { get; set; } = false;

        /// <summary>
        /// Whether to log in with Steam game server anonymous account mode.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamGameServerLogOnAnonymous)]
        public bool SteamGameServerLogOnAnonymous { get; set; } = true;

        /// <summary>
        /// Steam game server login token. Used only when anonymous login is disabled.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamGameServerToken)]
        public string SteamGameServerToken { get; set; } = string.Empty;

        /// <summary>
        /// Query port used by Steam server browser and status ping.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamGameServerQueryPort)]
        public int SteamGameServerQueryPort { get; set; } = Constants.DefaultSteamGameServerQueryPort;

        /// <summary>
        /// Game server version string announced to Steam.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamGameServerVersion)]
        public string SteamGameServerVersion { get; set; } = Constants.ModVersion;

        /// <summary>
        /// Steam game server API mode announced to Steam.
        /// This does not disable DedicatedServerMod auth by itself; use <see cref="AuthProvider"/> = <see cref="AuthenticationProvider.None"/> for that.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamGameServerMode)]
        [JsonConv(typeof(StringEnumConverter))]
        public SteamGameServerAuthenticationMode SteamGameServerMode { get; set; } = SteamGameServerAuthenticationMode.Authentication;

        /// <summary>
        /// Messaging backend used for custom server-client communication.
        /// FishNetRpc uses FishNet custom RPCs.
        /// SteamP2P uses legacy Steam P2P packets.
        /// SteamNetworkingSockets uses modern Steam sockets and is dedicated-server compatible.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.MessagingBackend)]
        [JsonConv(typeof(StringEnumConverter))]
        public MessagingBackendType MessagingBackend { get; set; } = MessagingBackendType.FishNetRpc;

        /// <summary>
        /// Whether to allow Steam relay (SDR) for P2P messaging.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamP2PAllowRelay)]
        public bool SteamP2PAllowRelay { get; set; } = true;

        /// <summary>
        /// Steam P2P channel for messaging.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamP2PChannel)]
        public int SteamP2PChannel { get; set; } = 0;

        /// <summary>
        /// Steam P2P max payload size in bytes.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamP2PMaxPayloadBytes)]
        public int SteamP2PMaxPayloadBytes { get; set; } = 1200;

        /// <summary>
        /// Target server SteamID for client-side Steam P2P message routing.
        /// Optional when server is discovered from inbound packets.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamP2PServerSteamId)]
        public string SteamP2PServerSteamId { get; set; } = string.Empty;

        /// <summary>
        /// Steam Web API key for web API ticket validation mode.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamWebApiKey)]
        public string SteamWebApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Steam Web API identity string used with web API auth tickets.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.SteamWebApiIdentity)]
        public string SteamWebApiIdentity { get; set; } = "DedicatedServerMod";

        #endregion

        #region TCP Console Settings

        /// <summary>
        /// Whether the TCP console server is enabled.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.TcpConsoleEnabled)]
        public bool TcpConsoleEnabled { get; set; } = false;

        /// <summary>
        /// IP address to bind the TCP console to.
        /// Use "127.0.0.1" for local-only, "0.0.0.0" for all interfaces.
        /// </summary>
        [JsonProp("tcpConsoleBindAddress")]
        public string TcpConsoleBindAddress { get; set; } = Constants.DefaultTcpConsoleBindAddress;

        /// <summary>
        /// Port for the TCP console server.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.TcpConsolePort)]
        public int TcpConsolePort { get; set; } = Constants.DefaultTcpConsolePort;

        /// <summary>
        /// Maximum number of concurrent TCP console clients.
        /// Keep this low because the console is intended for trusted administration rather than broad remote access.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.TcpConsoleMaxConnections)]
        public int TcpConsoleMaxConnections { get; set; } = Constants.DefaultTcpConsoleMaxConnections;

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

        /// <summary>
        /// Whether the integrated localhost web panel is enabled.
        /// Defaults to <see langword="false"/> so hosted and service-style deployments do not expose it unless explicitly opted in.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.WebPanelEnabled)]
        public bool WebPanelEnabled { get; set; } = false;

        /// <summary>
        /// IP address to bind the web panel to.
        /// Use "127.0.0.1" for local-only access.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.WebPanelBindAddress)]
        public string WebPanelBindAddress { get; set; } = Constants.DefaultTcpConsoleBindAddress;

        /// <summary>
        /// Port for the integrated web panel.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.WebPanelPort)]
        public int WebPanelPort { get; set; } = Constants.DefaultWebPanelPort;

        /// <summary>
        /// Whether the server should attempt to open the web panel in the default browser on startup.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.WebPanelOpenBrowserOnStart)]
        public bool WebPanelOpenBrowserOnStart { get; set; } = true;

        /// <summary>
        /// Duration in minutes for localhost web panel sessions.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.WebPanelSessionMinutes)]
        public int WebPanelSessionMinutes { get; set; } = Constants.DefaultWebPanelSessionMinutes;

        /// <summary>
        /// Whether recent logs are exposed to the localhost web panel.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.WebPanelExposeLogs)]
        public bool WebPanelExposeLogs { get; set; } = true;

        /// <summary>
        /// Controls activation of the stdio host console transport.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.StdioConsoleMode)]
        [JsonConv(typeof(StringEnumConverter))]
        public StdioConsoleMode StdioConsoleMode { get; set; } = StdioConsoleMode.Auto;

        #endregion

        #region Time & Gameplay Settings

        /// <summary>
        /// Whether to ignore the ghost host player when checking sleep readiness.
        /// </summary>
        [JsonProp("ignoreGhostHostForSleep")]
        public bool IgnoreGhostHostForSleep { get; set; } = true;

        /// <summary>
        /// Multiplier for time progression (1.0 = real-time).
        /// </summary>
        [JsonProp(Constants.ConfigKeys.TimeMultiplier)]
        public float TimeProgressionMultiplier { get; set; } = Constants.DefaultTimeMultiplier;

        /// <summary>
        /// Whether players are allowed to sleep to advance time.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.AllowSleeping)]
        public bool AllowSleeping { get; set; } = true;

        /// <summary>
        /// Whether to pause the game when no players are connected.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.PauseEmpty)]
        public bool PauseGameWhenEmpty { get; set; } = false;

        #endregion

        #region Auto-Save Settings

        /// <summary>
        /// Whether automatic saving is enabled.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.AutoSaveEnabled)]
        public bool AutoSaveEnabled { get; set; } = true;

        /// <summary>
        /// Interval in minutes between automatic saves.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.AutoSaveInterval)]
        public float AutoSaveIntervalMinutes { get; set; } = Constants.DefaultAutoSaveIntervalMinutes;

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
        [JsonProp(Constants.ConfigKeys.Operators)]
        public HashSet<string> Operators { get; set; } = new HashSet<string>();

        /// <summary>
        /// List of Steam IDs with admin privileges.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.Admins)]
        public HashSet<string> Admins { get; set; } = new HashSet<string>();

        /// <summary>
        /// List of banned Steam IDs.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.BannedPlayers)]
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

        #region Debug & Logging

        /// <summary>
        /// Whether debug mode is enabled (additional logging).
        /// </summary>
        [JsonProp(Constants.ConfigKeys.DebugMode)]
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Whether verbose logging is enabled (trace-level).
        /// </summary>
        [JsonProp(Constants.ConfigKeys.VerboseLogging)]
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

        /// <summary>
        /// Whether to enable additional debug logging across shared networking systems.
        /// This acts as an umbrella switch for routing and transport/backend debug logs.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.LogNetworkingDebug)]
        public bool LogNetworkingDebug { get; set; } = false;

        /// <summary>
        /// Whether to enable detailed debug logging for message routing decisions.
        /// When false, normal warnings and errors still log.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.LogMessageRoutingDebug)]
        public bool LogMessageRoutingDebug { get; set; } = false;

        /// <summary>
        /// Whether to enable detailed debug logging for messaging backend send/receive activity.
        /// When false, normal warnings and errors still log.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.LogMessagingBackendDebug)]
        public bool LogMessagingBackendDebug { get; set; } = false;

        /// <summary>
        /// Whether to enable detailed startup orchestration and initialization tracing.
        /// When false, only important startup milestones, warnings, and errors remain visible.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.LogStartupDebug)]
        public bool LogStartupDebug { get; set; } = false;

        /// <summary>
        /// Whether to enable detailed server transport and network lifecycle tracing.
        /// When false, only important online/offline and failure logs remain visible.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.LogServerNetworkDebug)]
        public bool LogServerNetworkDebug { get; set; } = false;

        /// <summary>
        /// Whether to enable detailed player lifecycle tracing for connections, spawns, identities, and shutdown cleanup.
        /// When false, important join, leave, kick, and ban logs remain visible.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.LogPlayerLifecycleDebug)]
        public bool LogPlayerLifecycleDebug { get; set; } = false;

        /// <summary>
        /// Whether to enable detailed authentication tracing for handshake and auth state transitions.
        /// When false, only common authentication failures remain visible.
        /// </summary>
        [JsonProp(Constants.ConfigKeys.LogAuthenticationDebug)]
        public bool LogAuthenticationDebug { get; set; } = false;

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
        /// Cached legacy requireAuthentication value observed during deserialization.
        /// </summary>
        private bool? _legacyRequireAuthentication;

        #endregion

        #region Command Line Integration

        /// <summary>
        /// Parses command line arguments and applies them to the configuration.
        /// </summary>
        /// <param name="args">The command line arguments</param>
        /// <param name="persistChanges">
        /// <see langword="true"/> to save the updated configuration after applying command-line overrides;
        /// otherwise, <see langword="false"/>.
        /// </param>
        public static void ParseCommandLineArgs(string[] args, bool persistChanges = false)
        {
            DebugLog.StartupDebug("Parsing command line arguments for server config...");

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--server-name":
                        if (i + 1 < args.Length)
                        {
                            Instance.ServerName = args[i + 1];
                            DebugLog.Info($"Server name set to: {Instance.ServerName}");
                        }
                        break;

                    case "--max-players":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int maxPlayers))
                        {
                            Instance.MaxPlayers = Math.Min(maxPlayers, Constants.MaxAllowedPlayers);
                            DebugLog.Info($"Max players set to: {Instance.MaxPlayers}");
                        }
                        break;

                    case "--server-password":
                        if (i + 1 < args.Length)
                        {
                            Instance.ServerPassword = args[i + 1];
                            DebugLog.Info("Server password set");
                        }
                        break;

                    case "--require-authentication":
                    case "--require-auth":
                        if (Instance.AuthProvider == AuthenticationProvider.None)
                        {
                            Instance.AuthProvider = AuthenticationProvider.SteamGameServer;
                        }
                        DebugLog.Info("Authentication enabled");
                        break;

                    case "--disable-authentication":
                    case "--disable-auth":
                    case "--no-auth":
                        Instance.AuthProvider = AuthenticationProvider.None;
                        DebugLog.Info("Authentication disabled via command line");
                        break;

                    case "--auth-provider":
                        if (i + 1 < args.Length &&
                            TryParseAuthenticationProvider(args[i + 1], out AuthenticationProvider authProvider))
                        {
                            Instance.AuthProvider = authProvider;
                            DebugLog.Info($"Authentication provider set to: {Instance.AuthProvider}");
                        }
                        break;

                    case "--auth-timeout":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int authTimeoutSeconds))
                        {
                            Instance.AuthTimeoutSeconds = authTimeoutSeconds;
                            DebugLog.Info($"Authentication timeout set to: {Instance.AuthTimeoutSeconds}s");
                        }
                        break;

                    case "--mod-verification":
                        Instance.ModVerificationEnabled = true;
                        DebugLog.Info("Client mod verification enabled via command line");
                        break;

                    case "--no-mod-verification":
                        Instance.ModVerificationEnabled = false;
                        DebugLog.Info("Client mod verification disabled via command line");
                        break;

                    case "--mod-verification-timeout":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int modVerificationTimeoutSeconds))
                        {
                            Instance.ModVerificationTimeoutSeconds = modVerificationTimeoutSeconds;
                            DebugLog.Info($"Mod verification timeout set to: {Instance.ModVerificationTimeoutSeconds}s");
                        }
                        break;

                    case "--strict-client-mod-mode":
                        Instance.StrictClientModMode = true;
                        DebugLog.Info("Strict client mod mode enabled via command line");
                        break;

                    case "--allow-unpaired-client-mods":
                        if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool allowUnpairedClientMods))
                        {
                            Instance.AllowUnpairedClientMods = allowUnpairedClientMods;
                            DebugLog.Info($"Allow unpaired client mods set to: {Instance.AllowUnpairedClientMods}");
                        }
                        break;

                    case "--block-known-risky-client-mods":
                        if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool blockKnownRiskyClientMods))
                        {
                            Instance.BlockKnownRiskyClientMods = blockKnownRiskyClientMods;
                            DebugLog.Info($"Block known risky client mods set to: {Instance.BlockKnownRiskyClientMods}");
                        }
                        break;

                    case "--steam-gs-anonymous":
                        Instance.SteamGameServerLogOnAnonymous = true;
                        DebugLog.Info("Steam game server anonymous login enabled");
                        break;

                    case "--steam-gs-token":
                        if (i + 1 < args.Length)
                        {
                            Instance.SteamGameServerToken = args[i + 1];
                            Instance.SteamGameServerLogOnAnonymous = false;
                            DebugLog.Info("Steam game server token set and anonymous login disabled");
                        }
                        break;

                    case "--messaging-backend":
                        if (i + 1 < args.Length &&
                            TryParseMessagingBackend(args[i + 1], out MessagingBackendType messagingBackend))
                        {
                            Instance.MessagingBackend = messagingBackend;
                            DebugLog.Info($"Messaging backend set to: {Instance.MessagingBackend}");
                        }
                        break;

                    case "--steam-p2p-relay":
                        if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool allowRelay))
                        {
                            Instance.SteamP2PAllowRelay = allowRelay;
                            DebugLog.Info($"Steam P2P relay set to: {Instance.SteamP2PAllowRelay}");
                        }
                        break;

                    case "--steam-p2p-channel":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int p2pChannel))
                        {
                            Instance.SteamP2PChannel = p2pChannel;
                            DebugLog.Info($"Steam P2P channel set to: {Instance.SteamP2PChannel}");
                        }
                        break;

                    case "--server-steamid":
                    case "--server-steam-id":
                        if (i + 1 < args.Length)
                        {
                            Instance.SteamP2PServerSteamId = args[i + 1];
                            DebugLog.Info("Steam P2P target server SteamID set");
                        }
                        break;

                    case "--add-operator":
                        if (i + 1 < args.Length)
                        {
                            Instance.Operators.Add(args[i + 1]);
                            DebugLog.Info($"Added operator: {args[i + 1]}");
                        }
                        break;

                    case "--add-admin":
                        if (i + 1 < args.Length)
                        {
                            Instance.Admins.Add(args[i + 1]);
                            DebugLog.Info($"Added admin: {args[i + 1]}");
                        }
                        break;

                    case "--debug":
                        Instance.DebugMode = true;
                        DebugLog.Info("Debug mode enabled");
                        break;

                    case "--verbose":
                        Instance.VerboseLogging = true;
                        DebugLog.Info("Verbose logging enabled");
                        break;

                    case "--tcp-console":
                        Instance.TcpConsoleEnabled = true;
                        DebugLog.Info("TCP console enabled via CLI");
                        break;

                    case "--tcp-console-port":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int tcpPort))
                        {
                            Instance.TcpConsolePort = tcpPort;
                            DebugLog.Info($"TCP console port set to: {Instance.TcpConsolePort}");
                        }
                        break;

                    case "--tcp-console-max-connections":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int tcpConsoleMaxConnections))
                        {
                            Instance.TcpConsoleMaxConnections = tcpConsoleMaxConnections;
                            DebugLog.Info($"TCP console max connections set to: {Instance.TcpConsoleMaxConnections}");
                        }
                        break;

                    case "--tcp-console-bind":
                        if (i + 1 < args.Length)
                        {
                            Instance.TcpConsoleBindAddress = args[i + 1];
                            DebugLog.Info($"TCP console bind address set to: {Instance.TcpConsoleBindAddress}");
                        }
                        break;

                    case "--tcp-console-password":
                        if (i + 1 < args.Length)
                        {
                            Instance.TcpConsolePassword = args[i + 1];
                            Instance.TcpConsoleRequirePassword = true;
                            DebugLog.Info("TCP console password set via CLI and requirement enabled");
                        }
                        break;

                    case "--stdio-console":
                        Instance.StdioConsoleMode = StdioConsoleMode.Enabled;
                        DebugLog.Info("stdio host console enabled via CLI");
                        break;

                    case "--no-stdio-console":
                        Instance.StdioConsoleMode = StdioConsoleMode.Disabled;
                        DebugLog.Info("stdio host console disabled via CLI");
                        break;

                    case "--stdio-console-auto":
                        Instance.StdioConsoleMode = StdioConsoleMode.Auto;
                        DebugLog.Info("stdio host console auto mode enabled via CLI");
                        break;

                    case "--target-framerate":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int fps))
                        {
                            Instance.TargetFrameRate = fps;
                            DebugLog.Info($"Target framerate set to: {fps}");
                        }
                        break;

                    case "--vsync":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int vsync))
                        {
                            Instance.VSyncCount = Math.Clamp(vsync, 0, 2);
                            DebugLog.Info($"VSync set to: {vsync}");
                        }
                        break;
                }
            }

            if (persistChanges)
            {
                SaveConfig();
            }
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
            info += $"Authentication: {(Instance.AuthenticationEnabled ? Instance.AuthProvider.ToString() : "Disabled")}\n";
            info += $"Auth Timeout: {Instance.AuthTimeoutSeconds}s\n";
            info += $"Mod Verification Enabled: {Instance.ModVerificationEnabled}\n";
            info += $"Mod Verification Timeout: {Instance.ModVerificationTimeoutSeconds}s\n";
            info += $"Strict Client Mod Mode: {Instance.StrictClientModMode}\n";
            info += $"Messaging Backend: {Instance.MessagingBackend}\n";
            info += $"Operators: {Instance.Operators.Count}\n";
            info += $"Admins: {Instance.Admins.Count}\n";
            info += $"Auto-Save: {Instance.AutoSaveEnabled} ({Instance.AutoSaveIntervalMinutes}min)\n";
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
                DebugLog.Warning($"Invalid server port {ServerPort}, using default {Constants.DefaultServerPort}");
                ServerPort = Constants.DefaultServerPort;
            }

            // Validate max players
            if (MaxPlayers < 1)
            {
                DebugLog.Warning($"Invalid max players {MaxPlayers}, using default {Constants.DefaultMaxPlayers}");
                MaxPlayers = Constants.DefaultMaxPlayers;
            }
            else if (MaxPlayers > Constants.MaxAllowedPlayers)
            {
                DebugLog.Warning($"Max players {MaxPlayers} exceeds limit {Constants.MaxAllowedPlayers}");
                MaxPlayers = Constants.MaxAllowedPlayers;
            }

            // Validate time multiplier
            if (TimeProgressionMultiplier < 0 || TimeProgressionMultiplier > Constants.MaxTimeMultiplier)
            {
                DebugLog.Warning($"Invalid time multiplier {TimeProgressionMultiplier}, using default {Constants.DefaultTimeMultiplier}");
                TimeProgressionMultiplier = Constants.DefaultTimeMultiplier;
            }

            // Validate auth timeout
            if (AuthTimeoutSeconds < 1 || AuthTimeoutSeconds > 120)
            {
                DebugLog.Warning($"Invalid auth timeout {AuthTimeoutSeconds}, using default {Constants.DefaultAuthTimeoutSeconds}");
                AuthTimeoutSeconds = Constants.DefaultAuthTimeoutSeconds;
            }

            if (ModVerificationTimeoutSeconds < 1 || ModVerificationTimeoutSeconds > 120)
            {
                DebugLog.Warning($"Invalid mod verification timeout {ModVerificationTimeoutSeconds}, using default {Constants.DefaultModVerificationTimeoutSeconds}");
                ModVerificationTimeoutSeconds = Constants.DefaultModVerificationTimeoutSeconds;
            }

            if (SteamGameServerQueryPort < Constants.MinPort || SteamGameServerQueryPort > Constants.MaxPort)
            {
                DebugLog.Warning($"Invalid steam game server query port {SteamGameServerQueryPort}, using default {Constants.DefaultSteamGameServerQueryPort}");
                SteamGameServerQueryPort = Constants.DefaultSteamGameServerQueryPort;
            }

            if (SteamP2PChannel < 0)
            {
                DebugLog.Warning($"Invalid Steam P2P channel {SteamP2PChannel}, using channel 0");
                SteamP2PChannel = 0;
            }

            if (SteamP2PMaxPayloadBytes < 256 || SteamP2PMaxPayloadBytes > Constants.MaxMessageSize)
            {
                DebugLog.Warning($"Invalid Steam P2P max payload {SteamP2PMaxPayloadBytes}, using 1200");
                SteamP2PMaxPayloadBytes = 1200;
            }

            // Validate auto-save interval
            if (AutoSaveIntervalMinutes < 0 || AutoSaveIntervalMinutes > Constants.MaxAutoSaveIntervalMinutes)
            {
                DebugLog.Warning($"Invalid auto-save interval {AutoSaveIntervalMinutes}, using default {Constants.DefaultAutoSaveIntervalMinutes}");
                AutoSaveIntervalMinutes = Constants.DefaultAutoSaveIntervalMinutes;
            }

            // Validate target framerate
            if (TargetFrameRate < -1 || TargetFrameRate > 300)
            {
                DebugLog.Warning($"Invalid target framerate {TargetFrameRate}, using default 60");
                TargetFrameRate = 60;
            }

            // Validate VSync
            if (VSyncCount < 0 || VSyncCount > 2)
            {
                DebugLog.Warning($"Invalid VSync count {VSyncCount}, using default 0");
                VSyncCount = 0;
            }

            // Validate TCP console max connections
            if (TcpConsoleMaxConnections < 1)
            {
                DebugLog.Warning($"Invalid TCP console max connections {TcpConsoleMaxConnections}, using default {Constants.DefaultTcpConsoleMaxConnections}");
                TcpConsoleMaxConnections = Constants.DefaultTcpConsoleMaxConnections;
            }

            if (WebPanelPort < Constants.MinPort || WebPanelPort > Constants.MaxPort)
            {
                DebugLog.Warning($"Invalid web panel port {WebPanelPort}, using default {Constants.DefaultWebPanelPort}");
                WebPanelPort = Constants.DefaultWebPanelPort;
            }

            if (WebPanelSessionMinutes < 1 || WebPanelSessionMinutes > 1440)
            {
                DebugLog.Warning($"Invalid web panel session duration {WebPanelSessionMinutes}, using default {Constants.DefaultWebPanelSessionMinutes}");
                WebPanelSessionMinutes = Constants.DefaultWebPanelSessionMinutes;
            }

            // Validate names
            if (ServerName.Length > Constants.MaxServerNameLength)
            {
                DebugLog.Warning("Server name exceeds max length, truncating");
                ServerName = ServerName.Substring(0, Constants.MaxServerNameLength);
            }

            if (ServerDescription.Length > Constants.MaxServerDescriptionLength)
            {
                DebugLog.Warning("Server description exceeds max length, truncating");
                ServerDescription = ServerDescription.Substring(0, Constants.MaxServerDescriptionLength);
            }

            NormalizeAuthenticationConfiguration();

            if (AuthenticationEnabled)
            {
                if (AuthProvider == AuthenticationProvider.SteamWebApi && string.IsNullOrWhiteSpace(SteamWebApiKey))
                {
                    DebugLog.Warning("Auth provider is SteamWebApi but steamWebApiKey is empty. Authentication will fail until configured.");
                }

                if (AuthProvider == AuthenticationProvider.SteamGameServer &&
                    !SteamGameServerLogOnAnonymous &&
                    string.IsNullOrWhiteSpace(SteamGameServerToken))
                {
                    DebugLog.Warning("Auth provider is SteamGameServer with anonymous login disabled, but steamGameServerToken is empty.");
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
                value = AuthenticationProvider.None;
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
                    value = AuthenticationProvider.None;
                    DebugLog.Warning($"Unknown auth provider '{provider}'. Valid options: none, steam_web_api, steam_game_server.");
                    return false;
            }
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            NormalizeAuthenticationConfiguration();
        }

        private void NormalizeAuthenticationConfiguration()
        {
            if (_legacyRequireAuthentication.HasValue)
            {
                if (!_legacyRequireAuthentication.Value)
                {
                    AuthProvider = AuthenticationProvider.None;
                }
                else if (AuthProvider == AuthenticationProvider.None)
                {
                    AuthProvider = AuthenticationProvider.SteamGameServer;
                }
            }
        }

        public bool ShouldSerializeLegacyRequireAuthentication()
        {
            return false;
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
                    DebugLog.Warning($"Unknown messaging backend '{backend}'. Valid options: fishnet_rpc, steam_p2p, steam_networking_sockets.");
                    value = MessagingBackendType.FishNetRpc;
                    return false;
            }
        }

        #endregion
    }
}

