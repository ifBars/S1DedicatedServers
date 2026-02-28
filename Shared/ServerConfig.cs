using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DedicatedServerMod.Shared.Networking.Messaging;
using DedicatedServerMod.Shared.Permissions;
#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Newtonsoft.Json;
#else
using FishNet;
using FishNet.Connection;
using Newtonsoft.Json;
#endif
using MelonLoader;
using MelonLoader.Utils;
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
using Il2CppSteamworks;
#else
using ScheduleOne.PlayerScripts;
using Steamworks;
#endif

namespace DedicatedServerMod.Shared
{
    /// <summary>
    /// Server configuration and permission management (backward compatibility wrapper).
    /// </summary>
    /// <remarks>
    /// DEPRECATED: This class is maintained for backward compatibility.
    /// New code should use:
    /// - <see cref="Configuration.ServerConfig"/> for configuration
    /// - <see cref="Permissions.PermissionManager"/> for permissions
    /// - <see cref="Permissions.PlayerResolver"/> for Steam ID utilities
    /// 
    /// This class provides obsolete wrapper methods that delegate to the new classes.
    /// </remarks>
    [Serializable]
    [Obsolete("Use Configuration.ServerConfig, Permissions.PermissionManager, and Permissions.PlayerResolver instead")]
    public class ServerConfig
    {
        #region Server Settings (delegated to Configuration.ServerConfig)

        [JsonProperty("serverName")]
        public string ServerName
        {
            get => Configuration.ServerConfig.Instance.ServerName;
            set => Configuration.ServerConfig.Instance.ServerName = value;
        }

        [JsonProperty("serverDescription")]
        public string ServerDescription
        {
            get => Configuration.ServerConfig.Instance.ServerDescription;
            set => Configuration.ServerConfig.Instance.ServerDescription = value;
        }

        [JsonProperty("maxPlayers")]
        public int MaxPlayers
        {
            get => Configuration.ServerConfig.Instance.MaxPlayers;
            set => Configuration.ServerConfig.Instance.MaxPlayers = value;
        }

        [JsonProperty("serverPort")]
        public int ServerPort
        {
            get => Configuration.ServerConfig.Instance.ServerPort;
            set => Configuration.ServerConfig.Instance.ServerPort = value;
        }

        [JsonProperty("serverPassword")]
        public string ServerPassword
        {
            get => Configuration.ServerConfig.Instance.ServerPassword;
            set => Configuration.ServerConfig.Instance.ServerPassword = value;
        }

        [JsonProperty("requireAuthentication")]
        public bool RequireAuthentication
        {
            get => Configuration.ServerConfig.Instance.RequireAuthentication;
            set => Configuration.ServerConfig.Instance.RequireAuthentication = value;
        }

        [JsonProperty("authProvider")]
        public Configuration.AuthenticationProvider AuthProvider
        {
            get => Configuration.ServerConfig.Instance.AuthProvider;
            set => Configuration.ServerConfig.Instance.AuthProvider = value;
        }

        [JsonProperty("authTimeoutSeconds")]
        public int AuthTimeoutSeconds
        {
            get => Configuration.ServerConfig.Instance.AuthTimeoutSeconds;
            set => Configuration.ServerConfig.Instance.AuthTimeoutSeconds = value;
        }

        [JsonProperty("authAllowLoopbackBypass")]
        public bool AuthAllowLoopbackBypass
        {
            get => Configuration.ServerConfig.Instance.AuthAllowLoopbackBypass;
            set => Configuration.ServerConfig.Instance.AuthAllowLoopbackBypass = value;
        }

        [JsonProperty("steamGameServerLogOnAnonymous")]
        public bool SteamGameServerLogOnAnonymous
        {
            get => Configuration.ServerConfig.Instance.SteamGameServerLogOnAnonymous;
            set => Configuration.ServerConfig.Instance.SteamGameServerLogOnAnonymous = value;
        }

        [JsonProperty("steamGameServerToken")]
        public string SteamGameServerToken
        {
            get => Configuration.ServerConfig.Instance.SteamGameServerToken;
            set => Configuration.ServerConfig.Instance.SteamGameServerToken = value;
        }

        [JsonProperty("steamGameServerQueryPort")]
        public int SteamGameServerQueryPort
        {
            get => Configuration.ServerConfig.Instance.SteamGameServerQueryPort;
            set => Configuration.ServerConfig.Instance.SteamGameServerQueryPort = value;
        }

        [JsonProperty("steamGameServerVersion")]
        public string SteamGameServerVersion
        {
            get => Configuration.ServerConfig.Instance.SteamGameServerVersion;
            set => Configuration.ServerConfig.Instance.SteamGameServerVersion = value;
        }

        [JsonProperty("steamGameServerMode")]
        public Configuration.SteamGameServerAuthenticationMode SteamGameServerMode
        {
            get => Configuration.ServerConfig.Instance.SteamGameServerMode;
            set => Configuration.ServerConfig.Instance.SteamGameServerMode = value;
        }

        [JsonProperty("messagingBackend")]
        public MessagingBackendType MessagingBackend
        {
            get => Configuration.ServerConfig.Instance.MessagingBackend;
            set => Configuration.ServerConfig.Instance.MessagingBackend = value;
        }

        [JsonProperty("steamP2PAllowRelay")]
        public bool SteamP2PAllowRelay
        {
            get => Configuration.ServerConfig.Instance.SteamP2PAllowRelay;
            set => Configuration.ServerConfig.Instance.SteamP2PAllowRelay = value;
        }

        [JsonProperty("steamP2PChannel")]
        public int SteamP2PChannel
        {
            get => Configuration.ServerConfig.Instance.SteamP2PChannel;
            set => Configuration.ServerConfig.Instance.SteamP2PChannel = value;
        }

        [JsonProperty("steamP2PMaxPayloadBytes")]
        public int SteamP2PMaxPayloadBytes
        {
            get => Configuration.ServerConfig.Instance.SteamP2PMaxPayloadBytes;
            set => Configuration.ServerConfig.Instance.SteamP2PMaxPayloadBytes = value;
        }

        [JsonProperty("steamP2PServerSteamId")]
        public string SteamP2PServerSteamId
        {
            get => Configuration.ServerConfig.Instance.SteamP2PServerSteamId;
            set => Configuration.ServerConfig.Instance.SteamP2PServerSteamId = value;
        }

        [JsonProperty("steamWebApiKey")]
        public string SteamWebApiKey
        {
            get => Configuration.ServerConfig.Instance.SteamWebApiKey;
            set => Configuration.ServerConfig.Instance.SteamWebApiKey = value;
        }

        [JsonProperty("steamWebApiIdentity")]
        public string SteamWebApiIdentity
        {
            get => Configuration.ServerConfig.Instance.SteamWebApiIdentity;
            set => Configuration.ServerConfig.Instance.SteamWebApiIdentity = value;
        }

        [JsonProperty("requireFriends")]
        public bool RequireFriends
        {
            get => Configuration.ServerConfig.Instance.RequireFriends;
            set => Configuration.ServerConfig.Instance.RequireFriends = value;
        }

        [JsonProperty("publicServer")]
        public bool PublicServer
        {
            get => Configuration.ServerConfig.Instance.PublicServer;
            set => Configuration.ServerConfig.Instance.PublicServer = value;
        }

        #endregion

        #region TCP Console Settings

        [JsonProperty("tcpConsoleEnabled")]
        public bool TcpConsoleEnabled
        {
            get => Configuration.ServerConfig.Instance.TcpConsoleEnabled;
            set => Configuration.ServerConfig.Instance.TcpConsoleEnabled = value;
        }

        [JsonProperty("tcpConsoleBindAddress")]
        public string TcpConsoleBindAddress
        {
            get => Configuration.ServerConfig.Instance.TcpConsoleBindAddress;
            set => Configuration.ServerConfig.Instance.TcpConsoleBindAddress = value;
        }

        [JsonProperty("tcpConsolePort")]
        public int TcpConsolePort
        {
            get => Configuration.ServerConfig.Instance.TcpConsolePort;
            set => Configuration.ServerConfig.Instance.TcpConsolePort = value;
        }

        [JsonProperty("tcpConsoleRequirePassword")]
        public bool TcpConsoleRequirePassword
        {
            get => Configuration.ServerConfig.Instance.TcpConsoleRequirePassword;
            set => Configuration.ServerConfig.Instance.TcpConsoleRequirePassword = value;
        }

        [JsonProperty("tcpConsolePassword")]
        public string TcpConsolePassword
        {
            get => Configuration.ServerConfig.Instance.TcpConsolePassword;
            set => Configuration.ServerConfig.Instance.TcpConsolePassword = value;
        }

        #endregion

        #region Time & Gameplay Settings

        [JsonProperty("ignoreGhostHostForSleep")]
        public bool IgnoreGhostHostForSleep
        {
            get => Configuration.ServerConfig.Instance.IgnoreGhostHostForSleep;
            set => Configuration.ServerConfig.Instance.IgnoreGhostHostForSleep = value;
        }

        [JsonProperty("timeNeverStops")]
        public bool TimeNeverStops
        {
            get => Configuration.ServerConfig.Instance.TimeNeverStops;
            set => Configuration.ServerConfig.Instance.TimeNeverStops = value;
        }

        [JsonProperty("timeProgressionMultiplier")]
        public float TimeProgressionMultiplier
        {
            get => Configuration.ServerConfig.Instance.TimeProgressionMultiplier;
            set => Configuration.ServerConfig.Instance.TimeProgressionMultiplier = value;
        }

        [JsonProperty("allowSleeping")]
        public bool AllowSleeping
        {
            get => Configuration.ServerConfig.Instance.AllowSleeping;
            set => Configuration.ServerConfig.Instance.AllowSleeping = value;
        }

        [JsonProperty("pauseGameWhenEmpty")]
        public bool PauseGameWhenEmpty
        {
            get => Configuration.ServerConfig.Instance.PauseGameWhenEmpty;
            set => Configuration.ServerConfig.Instance.PauseGameWhenEmpty = value;
        }

        #endregion

        #region Auto-Save Settings

        [JsonProperty("autoSaveEnabled")]
        public bool AutoSaveEnabled
        {
            get => Configuration.ServerConfig.Instance.AutoSaveEnabled;
            set => Configuration.ServerConfig.Instance.AutoSaveEnabled = value;
        }

        [JsonProperty("autoSaveIntervalMinutes")]
        public float AutoSaveIntervalMinutes
        {
            get => Configuration.ServerConfig.Instance.AutoSaveIntervalMinutes;
            set => Configuration.ServerConfig.Instance.AutoSaveIntervalMinutes = value;
        }

        [JsonProperty("autoSaveOnPlayerJoin")]
        public bool AutoSaveOnPlayerJoin
        {
            get => Configuration.ServerConfig.Instance.AutoSaveOnPlayerJoin;
            set => Configuration.ServerConfig.Instance.AutoSaveOnPlayerJoin = value;
        }

        [JsonProperty("autoSaveOnPlayerLeave")]
        public bool AutoSaveOnPlayerLeave
        {
            get => Configuration.ServerConfig.Instance.AutoSaveOnPlayerLeave;
            set => Configuration.ServerConfig.Instance.AutoSaveOnPlayerLeave = value;
        }

        #endregion

        #region Admin/Operator System (delegated to PermissionManager)

        [JsonProperty("operators")]
        public HashSet<string> Operators
        {
            get => PermissionManager.Config?.Operators ?? new HashSet<string>();
            set => PermissionManager.Config.Operators = value;
        }

        [JsonProperty("admins")]
        public HashSet<string> Admins
        {
            get => PermissionManager.Config?.Admins ?? new HashSet<string>();
            set => PermissionManager.Config.Admins = value;
        }

        [JsonProperty("bannedPlayers")]
        public HashSet<string> BannedPlayers
        {
            get => PermissionManager.Config?.BannedPlayers ?? new HashSet<string>();
            set => PermissionManager.Config.BannedPlayers = value;
        }

        [JsonProperty("enableConsoleForOps")]
        public bool EnableConsoleForOps
        {
            get => PermissionManager.Config?.EnableConsoleForOps ?? true;
            set => PermissionManager.Config.EnableConsoleForOps = value;
        }

        [JsonProperty("enableConsoleForAdmins")]
        public bool EnableConsoleForAdmins
        {
            get => PermissionManager.Config?.EnableConsoleForAdmins ?? true;
            set => PermissionManager.Config.EnableConsoleForAdmins = value;
        }

        [JsonProperty("enableConsoleForPlayers")]
        public bool EnableConsoleForPlayers
        {
            get => PermissionManager.Config?.EnableConsoleForPlayers ?? true;
            set => PermissionManager.Config.EnableConsoleForPlayers = value;
        }

        [JsonProperty("logAdminCommands")]
        public bool LogAdminCommands
        {
            get => PermissionManager.Config?.LogAdminCommands ?? true;
            set => PermissionManager.Config.LogAdminCommands = value;
        }

        [JsonProperty("allowedCommands")]
        public HashSet<string> AllowedCommands
        {
            get => PermissionManager.Config?.AllowedCommands ?? new HashSet<string>();
            set => PermissionManager.Config.AllowedCommands = value;
        }

        [JsonProperty("restrictedCommands")]
        public HashSet<string> RestrictedCommands
        {
            get => PermissionManager.Config?.RestrictedCommands ?? new HashSet<string>();
            set => PermissionManager.Config.RestrictedCommands = value;
        }

        [JsonProperty("playerAllowedCommands")]
        public HashSet<string> PlayerAllowedCommands
        {
            get => PermissionManager.Config?.PlayerAllowedCommands ?? new HashSet<string>();
            set => PermissionManager.Config.PlayerAllowedCommands = value;
        }

        [JsonProperty("globalDisabledCommands")]
        public HashSet<string> GlobalDisabledCommands
        {
            get => PermissionManager.Config?.GlobalDisabledCommands ?? new HashSet<string>();
            set => PermissionManager.Config.GlobalDisabledCommands = value;
        }

        #endregion

        #region Debug & Logging

        [JsonProperty("debugMode")]
        public bool DebugMode
        {
            get => Configuration.ServerConfig.Instance.DebugMode;
            set => Configuration.ServerConfig.Instance.DebugMode = value;
        }

        [JsonProperty("verboseLogging")]
        public bool VerboseLogging
        {
            get => Configuration.ServerConfig.Instance.VerboseLogging;
            set => Configuration.ServerConfig.Instance.VerboseLogging = value;
        }

        [JsonProperty("logPlayerActions")]
        public bool LogPlayerActions
        {
            get => Configuration.ServerConfig.Instance.LogPlayerActions;
            set => Configuration.ServerConfig.Instance.LogPlayerActions = value;
        }

        [JsonProperty("enablePerformanceMonitoring")]
        public bool EnablePerformanceMonitoring
        {
            get => Configuration.ServerConfig.Instance.EnablePerformanceMonitoring;
            set => Configuration.ServerConfig.Instance.EnablePerformanceMonitoring = value;
        }

        #endregion

        #region Save Path

        [JsonProperty("saveGamePath")]
        public string SaveGamePath
        {
            get => Configuration.ServerConfig.Instance.SaveGamePath;
            set => Configuration.ServerConfig.Instance.SaveGamePath = value;
        }

        #endregion

        #region MOTD & Welcome Messages

        [JsonProperty("enableMotd")]
        public bool EnableMotd
        {
            get => Configuration.ServerConfig.Instance.EnableMotd;
            set => Configuration.ServerConfig.Instance.EnableMotd = value;
        }

        [JsonProperty("motdMessage")]
        public string MotdMessage
        {
            get => Configuration.ServerConfig.Instance.MotdMessage;
            set => Configuration.ServerConfig.Instance.MotdMessage = value;
        }

        [JsonProperty("welcomeMessage")]
        public string WelcomeMessage
        {
            get => Configuration.ServerConfig.Instance.WelcomeMessage;
            set => Configuration.ServerConfig.Instance.WelcomeMessage = value;
        }

        [JsonProperty("showPlayerJoinMessages")]
        public bool ShowPlayerJoinMessages
        {
            get => Configuration.ServerConfig.Instance.ShowPlayerJoinMessages;
            set => Configuration.ServerConfig.Instance.ShowPlayerJoinMessages = value;
        }

        [JsonProperty("showPlayerLeaveMessages")]
        public bool ShowPlayerLeaveMessages
        {
            get => Configuration.ServerConfig.Instance.ShowPlayerLeaveMessages;
            set => Configuration.ServerConfig.Instance.ShowPlayerLeaveMessages = value;
        }

        #endregion

        #region Static Instance & Management

        private static ServerConfig _instance;
        private static MelonLogger.Instance _logger;
        private static string _configPath;

        public static string ConfigFilePath => _configPath ?? 
            Path.Combine(MelonEnvironment.UserDataDirectory, "server_config.json");

        [Obsolete("Use Configuration.ServerConfig.Instance instead")]
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

        [Obsolete("Use Configuration.ServerConfig.Initialize() instead")]
        public static void Initialize(MelonLogger.Instance loggerInstance)
        {
            _logger = loggerInstance;
            _configPath = Path.Combine(MelonEnvironment.UserDataDirectory, "server_config.json");
            Configuration.ServerConfig.Initialize(loggerInstance, _configPath);
            PermissionManager.Initialize(loggerInstance);
            PlayerResolver.Initialize(loggerInstance);
            LoadConfig();
        }

        #endregion

        #region Config File Management

        [Obsolete("Use Configuration.ServerConfig.LoadConfig() instead")]
        public static void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    _instance = JsonConvert.DeserializeObject<ServerConfig>(json);
                    Logger?.Msg("Server configuration loaded successfully");
                }
                else
                {
                    _instance = new ServerConfig();
                    SaveConfig();
                    Logger?.Msg("Created new server configuration file");
                }
            }
            catch (Exception ex)
            {
                Logger?.Error($"Failed to load server config: {ex}");
                _instance = new ServerConfig();
                SaveConfig();
            }
        }

        [Obsolete("Use Configuration.ServerConfig.SaveConfig() instead")]
        public static void SaveConfig()
        {
            try
            {
                // Sync to new config system
                Configuration.ServerConfig.SaveConfig();
                Logger?.Msg("Server configuration saved successfully");
            }
            catch (Exception ex)
            {
                Logger?.Error($"Failed to save server config: {ex}");
            }
        }

        [Obsolete("Use Configuration.ServerConfig.ReloadConfig() instead")]
        public static void ReloadConfig()
        {
            Logger?.Msg("Reloading server configuration...");
            Configuration.ServerConfig.ReloadConfig();
        }

        #endregion

        #region Admin/Operator Management (delegated to PermissionManager)

        [Obsolete("Use PermissionManager.IsOperator() instead")]
        public static bool IsOperator(string steamId)
        {
            return PermissionManager.IsOperator(steamId);
        }

        [Obsolete("Use PermissionManager.IsAdmin() instead")]
        public static bool IsAdmin(string steamId)
        {
            return PermissionManager.IsAdmin(steamId);
        }

        [Obsolete("Use PermissionManager.IsOperator() instead")]
        public static bool IsOperator(Player player)
        {
            return PermissionManager.IsOperator(player);
        }

        [Obsolete("Use PermissionManager.IsAdmin() instead")]
        public static bool IsAdmin(Player player)
        {
            return PermissionManager.IsAdmin(player);
        }

        [Obsolete("Use PermissionManager.AddOperator() instead")]
        public static bool AddOperator(string steamId)
        {
            return PermissionManager.AddOperator(steamId);
        }

        [Obsolete("Use PermissionManager.RemoveOperator() instead")]
        public static bool RemoveOperator(string steamId)
        {
            return PermissionManager.RemoveOperator(steamId);
        }

        [Obsolete("Use PermissionManager.AddAdmin() instead")]
        public static bool AddAdmin(string steamId)
        {
            return PermissionManager.AddAdmin(steamId);
        }

        [Obsolete("Use PermissionManager.RemoveAdmin() instead")]
        public static bool RemoveAdmin(string steamId)
        {
            return PermissionManager.RemoveAdmin(steamId);
        }

        [Obsolete("Use PermissionManager.AddBan() instead")]
        public static bool AddBan(string steamId)
        {
            return PermissionManager.AddBan(steamId);
        }

        [Obsolete("Use PermissionManager.RemoveBan() instead")]
        public static bool RemoveBan(string steamId)
        {
            return PermissionManager.RemoveBan(steamId);
        }

        [Obsolete("Use PermissionManager.GetAllOperators() instead")]
        public static List<string> GetAllOperators()
        {
            return PermissionManager.GetAllOperators() as List<string>;
        }

        [Obsolete("Use PermissionManager.GetAllAdmins() instead")]
        public static List<string> GetAllAdmins()
        {
            return PermissionManager.GetAllAdmins() as List<string>;
        }

        #endregion

        #region Permission Checking (delegated to PermissionManager)

        [Obsolete("Use PermissionManager.CanUseConsole() instead")]
        public static bool CanUseConsole(Player player)
        {
            return PermissionManager.CanUseConsole(player);
        }

        [Obsolete("Use PermissionManager.CanUseCommand() instead")]
        public static bool CanUseCommand(Player player, string command)
        {
            return PermissionManager.CanUseCommand(player, command);
        }

        [Obsolete("Use PermissionManager.CanUseCommand() instead")]
        public static bool CanUseCommand(string steamId, string command)
        {
            return PermissionManager.CanUseCommand(steamId, command);
        }

        #endregion

        #region Utility Methods (delegated to PlayerResolver)

        [Obsolete("Use PlayerResolver.GetSteamId() instead")]
        public static string GetPlayerSteamId(Player player)
        {
            return PlayerResolver.GetSteamId(player);
        }

        [Obsolete("Use PlayerResolver.GetPlayerBySteamId() instead")]
        public static Player GetPlayerBySteamId(string steamId)
        {
            return PlayerResolver.GetPlayerBySteamId(steamId);
        }

        [Obsolete("Use PlayerResolver.LogAdminAction() instead")]
        public static void LogAdminAction(Player player, string command, string args = "")
        {
            PlayerResolver.LogAdminAction(player, command, args);
        }

        [Obsolete("Use PlayerResolver.GetLocalPlayerSteamId() instead")]
        public static string GetLocalPlayerSteamId()
        {
            return PlayerResolver.GetLocalPlayerSteamId();
        }

        [Obsolete("Use PlayerResolver.AddLocalPlayerAsOperator() instead")]
        public static bool AddLocalPlayerAsOperator()
        {
            return PlayerResolver.AddLocalPlayerAsOperator();
        }

        #endregion

        #region Command Line Integration

        [Obsolete("Use Configuration.ServerConfig.ParseCommandLineArgs() instead")]
        public static void ParseCommandLineArgs(string[] args)
        {
            Configuration.ServerConfig.ParseCommandLineArgs(args);
        }

        #endregion

        #region Server Info

        [Obsolete("Use Configuration.ServerConfig.GetServerInfo() instead")]
        public static string GetServerInfo()
        {
            return Configuration.ServerConfig.GetServerInfo();
        }

        #endregion

        #region Private Methods

        private static MelonLogger.Instance Logger => _logger ?? 
            new MelonLogger.Instance("ServerConfig");

        #endregion
    }
}
