using System;

namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Centralized constants for DedicatedServerMod.
    /// Provides a single source of truth for all magic values and strings.
    /// </summary>
    public static class Constants
    {
        #region Mod Information

        /// <summary>
        /// The display name of the mod.
        /// </summary>
        public const string ModName = "DedicatedServerMod";

        /// <summary>
        /// The full version string of the mod (semantic versioning with prerelease tag).
        /// </summary>
        public const string ModVersion = "0.2.1-beta";

        /// <summary>
        /// The API version for compatibility checking.
        /// </summary>
        public const string APIVersion = "0.2.0";

        /// <summary>
        /// Gets the full version string including API version.
        /// </summary>
        public static string FullVersion => $"{ModVersion} (API {APIVersion})";

        /// <summary>
        /// The author/creator of the mod.
        /// </summary>
        public const string Author = "Ghost";

        #endregion

        #region Network Configuration

        /// <summary>
        /// Default port for the dedicated server to listen on.
        /// </summary>
        public const int DefaultServerPort = 38465;

        /// <summary>
        /// Default port for the TCP console server.
        /// </summary>
        public const int DefaultTcpConsolePort = 4050;

        /// <summary>
        /// Default maximum number of players.
        /// </summary>
        public const int DefaultMaxPlayers = 16;

        /// <summary>
        /// Maximum allowed players (hard limit).
        /// </summary>
        public const int MaxAllowedPlayers = 64;

        /// <summary>
        /// Default heartbeat interval in seconds for master server registration.
        /// </summary>
        public const int HeartbeatIntervalSeconds = 30;

        /// <summary>
        /// Timeout for player authentication in milliseconds.
        /// </summary>
        public const int AuthTimeoutMS = 5000;

        /// <summary>
        /// Default timeout for player authentication in seconds.
        /// </summary>
        public const int DefaultAuthTimeoutSeconds = 15;

        /// <summary>
        /// Default Steam game server query port.
        /// </summary>
        public const int DefaultSteamGameServerQueryPort = 27016;

        #endregion

        #region Custom Messaging

        /// <summary>
        /// RPC message ID used for custom messaging between server and client.
        /// Must not conflict with game RPC IDs.
        /// </summary>
        public const uint CustomMessageID = 105u;

        /// <summary>
        /// Maximum message payload size in bytes (excluding header).
        /// </summary>
        public const int MaxMessageSize = 65536;

        /// <summary>
        /// Default timeout for message acknowledgment in milliseconds.
        /// </summary>
        public const int MessageAckTimeoutMS = 3000;

        #endregion

        #region File Paths

        /// <summary>
        /// Default configuration file name.
        /// </summary>
        public const string ConfigFileName = "server_config.json";

        /// <summary>
        /// Default admin actions log file name.
        /// </summary>
        public const string AdminLOGFileName = "admin_actions.log";

        /// <summary>
        /// Default save file name pattern (without extension).
        /// </summary>
        public const string SaveFileName = "DedicatedServerSave";

        /// <summary>
        /// Maximum number of backup saves to retain.
        /// </summary>
        public const int MaxBackupSaves = 5;

        #endregion

        #region Time and Gameplay

        /// <summary>
        /// Default auto-save interval in minutes.
        /// </summary>
        public const float DefaultAutoSaveIntervalMinutes = 10f;

        /// <summary>
        /// Maximum auto-save interval in minutes.
        /// </summary>
        public const float MaxAutoSaveIntervalMinutes = 1440f;

        /// <summary>
        /// Default time progression multiplier (1.0 = real-time).
        /// </summary>
        public const float DefaultTimeMultiplier = 1.0f;

        /// <summary>
        /// Maximum time progression multiplier.
        /// </summary>
        public const float MaxTimeMultiplier = 100.0f;

        /// <summary>
        /// Grace period (seconds) before considering a player disconnected.
        /// </summary>
        public const int PlayerDisconnectGracePeriod = 30;

        #endregion

        #region Message Command Names

        /// <summary>
        /// Command name for executing console commands on client.
        /// </summary>
        public static class Messages
        {
            /// <summary>
            /// Execute console command (server → client).
            /// </summary>
            public const string ExecConsole = "exec_console";

            /// <summary>
            /// Admin console command execution (client → server).
            /// </summary>
            public const string AdminConsole = "admin_console";

            /// <summary>
            /// Request server data snapshot (client → server).
            /// </summary>
            public const string RequestServerData = "request_server_data";

            /// <summary>
            /// Server data response (server → client).
            /// </summary>
            public const string ServerData = "server_data";

            /// <summary>
            /// Authentication hello message (client → server).
            /// </summary>
            public const string AuthHello = "auth_hello";

            /// <summary>
            /// Authentication challenge message (server → client).
            /// </summary>
            public const string AuthChallenge = "auth_challenge";

            /// <summary>
            /// Authentication ticket submission (client → server).
            /// </summary>
            public const string AuthTicket = "auth_ticket";

            /// <summary>
            /// Authentication result message (server → client).
            /// </summary>
            public const string AuthResult = "auth_result";

            /// <summary>
            /// Welcome message (server → client on connect).
            /// </summary>
            public const string WelcomeMessage = "welcome_message";

            /// <summary>
            /// Server status broadcast (server → all clients).
            /// </summary>
            public const string ServerStatus = "server_status";

            /// <summary>
            /// Chat message (bidirectional).
            /// </summary>
            public const string ChatMessage = "chat_message";

            /// <summary>
            /// SteamNetworkLib dedicated compatibility register request (client -> server).
            /// </summary>
            public const string SnlDedicatedRegister = "snl_dedicated_register";

            /// <summary>
            /// SteamNetworkLib dedicated compatibility snapshot (server -> client).
            /// </summary>
            public const string SnlDedicatedSnapshot = "snl_dedicated_snapshot";

            /// <summary>
            /// SteamNetworkLib dedicated member joined notification (server -> client).
            /// </summary>
            public const string SnlDedicatedMemberJoined = "snl_dedicated_member_joined";

            /// <summary>
            /// SteamNetworkLib dedicated member left notification (server -> client).
            /// </summary>
            public const string SnlDedicatedMemberLeft = "snl_dedicated_member_left";

            /// <summary>
            /// SteamNetworkLib dedicated lobby data change event (server -> client).
            /// </summary>
            public const string SnlDedicatedLobbyDataChanged = "snl_dedicated_lobby_data_changed";

            /// <summary>
            /// SteamNetworkLib dedicated member data change event (server -> client).
            /// </summary>
            public const string SnlDedicatedMemberDataChanged = "snl_dedicated_member_data_changed";

            /// <summary>
            /// SteamNetworkLib dedicated set lobby data request (client -> server).
            /// </summary>
            public const string SnlDedicatedSetLobbyData = "snl_dedicated_set_lobby_data";

            /// <summary>
            /// SteamNetworkLib dedicated set member data request (client -> server).
            /// </summary>
            public const string SnlDedicatedSetMemberData = "snl_dedicated_set_member_data";

            /// <summary>
            /// SteamNetworkLib dedicated P2P send request (client -> server).
            /// </summary>
            public const string SnlDedicatedP2PSend = "snl_dedicated_p2p_send";

            /// <summary>
            /// SteamNetworkLib dedicated P2P message delivery (server -> client).
            /// </summary>
            public const string SnlDedicatedP2PMessage = "snl_dedicated_p2p_message";
        }

        #endregion

        #region Console Command Names

        /// <summary>
        /// Standard console command names for reference.
        /// </summary>
        public static class Commands
        {
            public const string Save = "save";
            public const string SetTime = "settime";
            public const string SetTimeScale = "settimescale";
            public const string Give = "give";
            public const string ClearInventory = "clearinventory";
            public const string ChangeCash = "changecash";
            public const string ChangeBalance = "changebalance";
            public const string AddXp = "addxp";
            public const string SpawnVehicle = "spawnvehicle";
            public const string SetMoveSpeed = "setmovespeed";
            public const string SetJumpForce = "setjumpforce";
            public const string Teleport = "teleport";
            public const string SetOwned = "setowned";
            public const string SetHealth = "sethealth";
            public const string SetEnergy = "setenergy";
            public const string SetVar = "setvar";
            public const string SetQuestState = "setqueststate";
            public const string SetQuestEntryState = "setquestentrystate";
            public const string SetEmotion = "setemotion";
            public const string SetUnlocked = "setunlocked";
            public const string SetRelationship = "setrelationship";
            public const string AddEmployee = "addemployee";
            public const string SetDiscovered = "setdiscovered";
            public const string GrowPlants = "growplants";
            public const string SetLawIntensity = "setlawintensity";
            public const string SetQuality = "setquality";
            public const string PackageProduct = "packageproduct";
            public const string SetStaminaReserve = "setstaminareserve";
            public const string RaiseWanted = "raisewanted";
            public const string LowerWanted = "lowerwanted";
            public const string ClearWanted = "clearwanted";
            public const string Bind = "bind";
            public const string Unbind = "unbind";
            public const string ClearBinds = "clearbinds";
            public const string HideUI = "hideui";
            public const string Disable = "disable";
            public const string Enable = "enable";
            public const string EndTutorial = "endtutorial";
            public const string DisableNpcAsset = "disablenpcasset";
            public const string ShowFPS = "showfps";
            public const string HideFPS = "hidefps";
            public const string ClearTrash = "cleartrash";
            public const string FreeCam = "freecam";
        }

        #endregion

        #region Game Object Names

        /// <summary>
        /// Game object name used for the ghost loopback host player.
        /// This player represents the server's local player on dedicated servers.
        /// </summary>
        public const string GhostHostObjectName = "[DedicatedServerHostLoopback]";

        #endregion

        #region TCP Console Commands

        /// <summary>
        /// TCP console-specific command names.
        /// </summary>
        public static class TcpCommands
        {
            public const string Help = "help";
            public const string List = "list";
            public const string Save = "save";
            public const string Shutdown = "shutdown";
            public const string Reload = "reload";
            public const string Serverinfo = "serverinfo";
            public const string Status = "status";
            public const string Op = "op";
            public const string Deop = "deop";
            public const string Admin = "admin";
            public const string Deadmin = "deadmin";
            public const string Kick = "kick";
            public const string Ban = "ban";
            public const string Unban = "unban";
            public const string ListOps = "listops";
            public const string ListAdmins = "listadmins";
            public const string Clear = "clear";
            public const string Exit = "exit";
            public const string Quit = "quit";
        }

        #endregion

        #region Configuration Keys

        /// <summary>
        /// JSON property names for server configuration.
        /// Centralized to ensure consistency between config and code.
        /// </summary>
        public static class ConfigKeys
        {
            public const string ServerName = "serverName";
            public const string ServerDescription = "serverDescription";
            public const string MaxPlayers = "maxPlayers";
            public const string ServerPort = "serverPort";
            public const string ServerPassword = "serverPassword";
            public const string RequireAuthentication = "requireAuthentication";
            public const string AuthProvider = "authProvider";
            public const string AuthTimeoutSeconds = "authTimeoutSeconds";
            public const string AuthAllowLoopbackBypass = "authAllowLoopbackBypass";
            public const string SteamGameServerLogOnAnonymous = "steamGameServerLogOnAnonymous";
            public const string SteamGameServerToken = "steamGameServerToken";
            public const string SteamGameServerQueryPort = "steamGameServerQueryPort";
            public const string SteamGameServerVersion = "steamGameServerVersion";
            public const string SteamGameServerMode = "steamGameServerMode";
            public const string MessagingBackend = "messagingBackend";
            public const string SteamP2PAllowRelay = "steamP2PAllowRelay";
            public const string SteamP2PChannel = "steamP2PChannel";
            public const string SteamP2PMaxPayloadBytes = "steamP2PMaxPayloadBytes";
            public const string SteamP2PServerSteamId = "steamP2PServerSteamId";
            public const string SteamWebApiKey = "steamWebApiKey";
            public const string SteamWebApiIdentity = "steamWebApiIdentity";
            public const string PublicServer = "publicServer";
            public const string RegisterWithMasterServer = "registerWithMasterServer";
            public const string MasterServerURL = "masterServerUrl";
            public const string TcpConsoleEnabled = "tcpConsoleEnabled";
            public const string TcpConsolePort = "tcpConsolePort";
            public const string TimeNeverStops = "timeNeverStops";
            public const string TimeMultiplier = "timeProgressionMultiplier";
            public const string AllowSleeping = "allowSleeping";
            public const string PauseEmpty = "pauseGameWhenEmpty";
            public const string AutoSaveEnabled = "autoSaveEnabled";
            public const string AutoSaveInterval = "autoSaveIntervalMinutes";
            public const string Operators = "operators";
            public const string Admins = "admins";
            public const string BannedPlayers = "bannedPlayers";
            public const string DebugMode = "debugMode";
            public const string VerboseLogging = "verboseLogging";
        }

        #endregion

        #region Validation Constants

        /// <summary>
        /// Maximum server name length.
        /// </summary>
        public const int MaxServerNameLength = 64;

        /// <summary>
        /// Maximum server description length.
        /// </summary>
        public const int MaxServerDescriptionLength = 256;

        /// <summary>
        /// Minimum valid port number.
        /// </summary>
        public const int MinPort = 1024;

        /// <summary>
        /// Maximum valid port number.
        /// </summary>
        public const int MaxPort = 65535;

        /// <summary>
        /// Maximum Steam ID string length.
        /// </summary>
        public const int MaxSteamIDLength = 32;

        #endregion

        #region Timeout Constants

        /// <summary>
        /// Default connection timeout in milliseconds.
        /// </summary>
        public const int ConnectionTimeoutMS = 30000;

        /// <summary>
        /// Maximum time to wait for player spawn in milliseconds.
        /// </summary>
        public const int PlayerSpawnTimeoutMS = 10000;

        /// <summary>
        /// Interval for retrying failed operations (milliseconds).
        /// </summary>
        public const int RetryIntervalMS = 1000;

        /// <summary>
        /// Maximum number of retry attempts for critical operations.
        /// </summary>
        public const int MaxRetryAttempts = 3;

        #endregion

        #region UI and Display

        /// <summary>
        /// Default admin console title.
        /// </summary>
        public const string AdminConsoleTitle = "Server Admin Console";

        /// <summary>
        /// Default welcome message template.
        /// </summary>
        public const string DefaultWelcomeMessage = "Welcome {playerName} to {serverName}!";

        /// <summary>
        /// Default MOTD message.
        /// </summary>
        public const string DefaultMotdMessage = "Welcome to the server! Type /help for commands.";

        #endregion
    }
}
