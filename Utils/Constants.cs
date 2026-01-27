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
        public const string MOD_NAME = "DedicatedServerMod";

        /// <summary>
        /// The full version string of the mod (semantic versioning with prerelease tag).
        /// </summary>
        public const string MOD_VERSION = "0.2.1-beta";

        /// <summary>
        /// The API version for compatibility checking.
        /// </summary>
        public const string API_VERSION = "0.2.0";

        /// <summary>
        /// Gets the full version string including API version.
        /// </summary>
        public static string FullVersion => $"{MOD_VERSION} (API {API_VERSION})";

        /// <summary>
        /// The author/creator of the mod.
        /// </summary>
        public const string AUTHOR = "Ghost";

        #endregion

        #region Network Configuration

        /// <summary>
        /// Default port for the dedicated server to listen on.
        /// </summary>
        public const int DEFAULT_SERVER_PORT = 38465;

        /// <summary>
        /// Default port for the TCP console server.
        /// </summary>
        public const int DEFAULT_TCP_CONSOLE_PORT = 4050;

        /// <summary>
        /// Default maximum number of players.
        /// </summary>
        public const int DEFAULT_MAX_PLAYERS = 16;

        /// <summary>
        /// Maximum allowed players (hard limit).
        /// </summary>
        public const int MAX_ALLOWED_PLAYERS = 64;

        /// <summary>
        /// Default heartbeat interval in seconds for master server registration.
        /// </summary>
        public const int HEARTBEAT_INTERVAL_SECONDS = 30;

        /// <summary>
        /// Timeout for player authentication in milliseconds.
        /// </summary>
        public const int AUTH_TIMEOUT_MS = 5000;

        #endregion

        #region Custom Messaging

        /// <summary>
        /// RPC message ID used for custom messaging between server and client.
        /// Must not conflict with game RPC IDs.
        /// </summary>
        public const uint CUSTOM_MESSAGE_ID = 105u;

        /// <summary>
        /// Maximum message payload size in bytes (excluding header).
        /// </summary>
        public const int MAX_MESSAGE_SIZE = 65536;

        /// <summary>
        /// Default timeout for message acknowledgment in milliseconds.
        /// </summary>
        public const int MESSAGE_ACK_TIMEOUT_MS = 3000;

        #endregion

        #region File Paths

        /// <summary>
        /// Default configuration file name.
        /// </summary>
        public const string CONFIG_FILE_NAME = "server_config.json";

        /// <summary>
        /// Default admin actions log file name.
        /// </summary>
        public const string ADMIN_LOG_FILE_NAME = "admin_actions.log";

        /// <summary>
        /// Default save file name pattern (without extension).
        /// </summary>
        public const string SAVE_FILE_NAME = "DedicatedServerSave";

        /// <summary>
        /// Maximum number of backup saves to retain.
        /// </summary>
        public const int MAX_BACKUP_SAVES = 5;

        #endregion

        #region Time and Gameplay

        /// <summary>
        /// Default auto-save interval in minutes.
        /// </summary>
        public const float DEFAULT_AUTO_SAVE_INTERVAL_MINUTES = 10f;

        /// <summary>
        /// Maximum auto-save interval in minutes.
        /// </summary>
        public const float MAX_AUTO_SAVE_INTERVAL_MINUTES = 1440f;

        /// <summary>
        /// Default time progression multiplier (1.0 = real-time).
        /// </summary>
        public const float DEFAULT_TIME_MULTIPLIER = 1.0f;

        /// <summary>
        /// Maximum time progression multiplier.
        /// </summary>
        public const float MAX_TIME_MULTIPLIER = 100.0f;

        /// <summary>
        /// Grace period (seconds) before considering a player disconnected.
        /// </summary>
        public const int PLAYER_DISCONNECT_GRACE_PERIOD = 30;

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
            public const string EXEC_CONSOLE = "exec_console";

            /// <summary>
            /// Admin console command execution (client → server).
            /// </summary>
            public const string ADMIN_CONSOLE = "admin_console";

            /// <summary>
            /// Request server data snapshot (client → server).
            /// </summary>
            public const string REQUEST_SERVER_DATA = "request_server_data";

            /// <summary>
            /// Server data response (server → client).
            /// </summary>
            public const string SERVER_DATA = "server_data";

            /// <summary>
            /// Welcome message (server → client on connect).
            /// </summary>
            public const string WELCOME_MESSAGE = "welcome_message";

            /// <summary>
            /// Server status broadcast (server → all clients).
            /// </summary>
            public const string SERVER_STATUS = "server_status";

            /// <summary>
            /// Chat message (bidirectional).
            /// </summary>
            public const string CHAT_MESSAGE = "chat_message";
        }

        #endregion

        #region Console Command Names

        /// <summary>
        /// Standard console command names for reference.
        /// </summary>
        public static class Commands
        {
            public const string SAVE = "save";
            public const string SET_TIME = "settime";
            public const string SET_TIME_SCALE = "settimescale";
            public const string GIVE = "give";
            public const string CLEAR_INVENTORY = "clearinventory";
            public const string CHANGE_CASH = "changecash";
            public const string CHANGE_BALANCE = "changebalance";
            public const string ADD_XP = "addxp";
            public const string SPAWN_VEHICLE = "spawnvehicle";
            public const string SET_MOVE_SPEED = "setmovespeed";
            public const string SET_JUMP_FORCE = "setjumpforce";
            public const string TELEPORT = "teleport";
            public const string SET_OWNED = "setowned";
            public const string SET_HEALTH = "sethealth";
            public const string SET_ENERGY = "setenergy";
            public const string SET_VAR = "setvar";
            public const string SET_QUEST_STATE = "setqueststate";
            public const string SET_QUEST_ENTRY_STATE = "setquestentrystate";
            public const string SET_EMOTION = "setemotion";
            public const string SET_UNLOCKED = "setunlocked";
            public const string SET_RELATIONSHIP = "setrelationship";
            public const string ADD_EMPLOYEE = "addemployee";
            public const string SET_DISCOVERED = "setdiscovered";
            public const string GROW_PLANTS = "growplants";
            public const string SET_LAW_INTENSITY = "setlawintensity";
            public const string SET_QUALITY = "setquality";
            public const string PACKAGE_PRODUCT = "packageproduct";
            public const string SET_STAMINA_RESERVE = "setstaminareserve";
            public const string RAISE_WANTED = "raisewanted";
            public const string LOWER_WANTED = "lowerwanted";
            public const string CLEAR_WANTED = "clearwanted";
            public const string BIND = "bind";
            public const string UNBIND = "unbind";
            public const string CLEAR_BINDS = "clearbinds";
            public const string HIDE_UI = "hideui";
            public const string DISABLE = "disable";
            public const string ENABLE = "enable";
            public const string END_TUTORIAL = "endtutorial";
            public const string DISABLE_NPC_ASSET = "disablenpcasset";
            public const string SHOW_FPS = "showfps";
            public const string HIDE_FPS = "hidefps";
            public const string CLEAR_TRASH = "cleartrash";
            public const string FREE_CAM = "freecam";
        }

        #endregion

        #region Game Object Names

        /// <summary>
        /// Game object name used for the ghost loopback host player.
        /// This player represents the server's local player on dedicated servers.
        /// </summary>
        public const string GHOST_HOST_OBJECT_NAME = "[DedicatedServerHostLoopback]";

        #endregion

        #region TCP Console Commands

        /// <summary>
        /// TCP console-specific command names.
        /// </summary>
        public static class TcpCommands
        {
            public const string HELP = "help";
            public const string LIST = "list";
            public const string SAVE = "save";
            public const string SHUTDOWN = "shutdown";
            public const string RELOAD = "reload";
            public const string SERVERINFO = "serverinfo";
            public const string STATUS = "status";
            public const string OP = "op";
            public const string DEOP = "deop";
            public const string ADMIN = "admin";
            public const string DEADMIN = "deadmin";
            public const string KICK = "kick";
            public const string BAN = "ban";
            public const string UNBAN = "unban";
            public const string LIST_OPS = "listops";
            public const string LIST_ADMINS = "listadmins";
            public const string CLEAR = "clear";
            public const string EXIT = "exit";
            public const string QUIT = "quit";
        }

        #endregion

        #region Configuration Keys

        /// <summary>
        /// JSON property names for server configuration.
        /// Centralized to ensure consistency between config and code.
        /// </summary>
        public static class ConfigKeys
        {
            public const string SERVER_NAME = "serverName";
            public const string SERVER_DESCRIPTION = "serverDescription";
            public const string MAX_PLAYERS = "maxPlayers";
            public const string SERVER_PORT = "serverPort";
            public const string SERVER_PASSWORD = "serverPassword";
            public const string REQUIRE_AUTHENTICATION = "requireAuthentication";
            public const string REQUIRE_FRIENDS = "requireFriends";
            public const string PUBLIC_SERVER = "publicServer";
            public const string REGISTER_WITH_MASTER_SERVER = "registerWithMasterServer";
            public const string MASTER_SERVER_URL = "masterServerUrl";
            public const string TCP_CONSOLE_ENABLED = "tcpConsoleEnabled";
            public const string TCP_CONSOLE_PORT = "tcpConsolePort";
            public const string TIME_NEVER_STOPS = "timeNeverStops";
            public const string TIME_MULTIPLIER = "timeProgressionMultiplier";
            public const string ALLOW_SLEEPING = "allowSleeping";
            public const string PAUSE_EMPTY = "pauseGameWhenEmpty";
            public const string AUTO_SAVE_ENABLED = "autoSaveEnabled";
            public const string AUTO_SAVE_INTERVAL = "autoSaveIntervalMinutes";
            public const string OPERATORS = "operators";
            public const string ADMINS = "admins";
            public const string BANNED_PLAYERS = "bannedPlayers";
            public const string DEBUG_MODE = "debugMode";
            public const string VERBOSE_LOGGING = "verboseLogging";
        }

        #endregion

        #region Validation Constants

        /// <summary>
        /// Maximum server name length.
        /// </summary>
        public const int MAX_SERVER_NAME_LENGTH = 64;

        /// <summary>
        /// Maximum server description length.
        /// </summary>
        public const int MAX_SERVER_DESCRIPTION_LENGTH = 256;

        /// <summary>
        /// Minimum valid port number.
        /// </summary>
        public const int MIN_PORT = 1024;

        /// <summary>
        /// Maximum valid port number.
        /// </summary>
        public const int MAX_PORT = 65535;

        /// <summary>
        /// Maximum Steam ID string length.
        /// </summary>
        public const int MAX_STEAM_ID_LENGTH = 32;

        #endregion

        #region Timeout Constants

        /// <summary>
        /// Default connection timeout in milliseconds.
        /// </summary>
        public const int CONNECTION_TIMEOUT_MS = 30000;

        /// <summary>
        /// Maximum time to wait for player spawn in milliseconds.
        /// </summary>
        public const int PLAYER_SPAWN_TIMEOUT_MS = 10000;

        /// <summary>
        /// Interval for retrying failed operations (milliseconds).
        /// </summary>
        public const int RETRY_INTERVAL_MS = 1000;

        /// <summary>
        /// Maximum number of retry attempts for critical operations.
        /// </summary>
        public const int MAX_RETRY_ATTEMPTS = 3;

        #endregion

        #region UI and Display

        /// <summary>
        /// Default admin console title.
        /// </summary>
        public const string ADMIN_CONSOLE_TITLE = "Server Admin Console";

        /// <summary>
        /// Default welcome message template.
        /// </summary>
        public const string DEFAULT_WELCOME_MESSAGE = "Welcome {playerName} to {serverName}!";

        /// <summary>
        /// Default MOTD message.
        /// </summary>
        public const string DEFAULT_MOTD_MESSAGE = "Welcome to the server! Type /help for commands.";

        #endregion
    }
}
