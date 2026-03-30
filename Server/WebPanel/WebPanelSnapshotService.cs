using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod.Server.Network;
using DedicatedServerMod.Server.Persistence;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.Permissions;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.WebPanel
{
    /// <summary>
    /// Builds browser-panel DTOs from the existing dedicated server subsystems.
    /// </summary>
    internal sealed class WebPanelSnapshotService
    {
        private readonly NetworkManager _networkManager;
        private readonly PlayerManager _playerManager;
        private readonly ServerPermissionService _permissionService;
        private readonly PersistenceManager _persistenceManager;

        public WebPanelSnapshotService(NetworkManager networkManager, PlayerManager playerManager, ServerPermissionService permissionService, PersistenceManager persistenceManager)
        {
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            _playerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _persistenceManager = persistenceManager ?? throw new ArgumentNullException(nameof(persistenceManager));
        }

        public ServerDashboardSnapshot CreateOverview()
        {
            ServerConfig config = ServerConfig.Instance;
            PlayerStats playerStats = _playerManager.GetPlayerStats();
            DedicatedServerMod.Shared.Permissions.PermissionSummary permissionSummary = _permissionService.GetSummary();
            DateTime lastSave = _persistenceManager.LastAutoSave;
            TimeSpan uptime = _networkManager.Uptime;

            return new ServerDashboardSnapshot
            {
                ServerName = config.ServerName,
                ServerDescription = config.ServerDescription,
                IsRunning = _networkManager.IsServerRunning,
                Status = _networkManager.IsServerRunning ? "Running" : "Stopped",
                ServerPort = config.ServerPort,
                CurrentPlayers = playerStats.ConnectedPlayers,
                MaxPlayers = config.MaxPlayers,
                UptimeSeconds = uptime.TotalSeconds,
                UptimeDisplay = uptime.ToString(@"dd\.hh\:mm\:ss"),
                AuthProvider = config.AuthenticationEnabled ? config.AuthProvider.ToString() : "Disabled",
                AutoSaveEnabled = config.AutoSaveEnabled,
                AutoSaveIntervalMinutes = config.AutoSaveIntervalMinutes,
                SaveInProgress = _persistenceManager.SaveInProgress,
                LastSaveUtc = lastSave == DateTime.MinValue ? (DateTime?)null : lastSave.ToUniversalTime(),
                TotalGroups = permissionSummary.TotalGroups,
                TotalUsers = permissionSummary.TotalUsers,
                TotalBans = permissionSummary.TotalBans,
                TotalOperators = permissionSummary.TotalOperators,
                TotalAdministrators = permissionSummary.TotalAdministrators
            };
        }

        public List<WebPanelPlayerRow> CreatePlayers()
        {
            List<ConnectedPlayerInfo> players = _playerManager.GetConnectedPlayers();
            return players
                .Where(player => player != null)
                .OrderByDescending(player => player.IsConnected)
                .ThenBy(player => player.IsLoopbackConnection)
                .ThenBy(player => player.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(CreatePlayerRow)
                .ToList();
        }

        public WebPanelConfigSnapshot CreateConfigSnapshot()
        {
            ServerConfig config = ServerConfig.Instance;
            return new WebPanelConfigSnapshot
            {
                Server = new WebPanelServerSection
                {
                    ServerName = config.ServerName,
                    ServerDescription = config.ServerDescription,
                    MaxPlayers = config.MaxPlayers,
                    ServerPort = config.ServerPort,
                    ServerPassword = config.ServerPassword
                },
                Authentication = new WebPanelAuthenticationSection
                {
                    AuthProvider = config.AuthProvider.ToString(),
                    AuthTimeoutSeconds = config.AuthTimeoutSeconds,
                    AuthAllowLoopbackBypass = config.AuthAllowLoopbackBypass,
                    ModVerificationEnabled = config.ModVerificationEnabled,
                    ModVerificationTimeoutSeconds = config.ModVerificationTimeoutSeconds,
                    BlockKnownRiskyClientMods = config.BlockKnownRiskyClientMods,
                    AllowUnpairedClientMods = config.AllowUnpairedClientMods,
                    StrictClientModMode = config.StrictClientModMode,
                    SteamGameServerLogOnAnonymous = config.SteamGameServerLogOnAnonymous,
                    SteamGameServerToken = config.SteamGameServerToken,
                    SteamGameServerQueryPort = config.SteamGameServerQueryPort,
                    SteamGameServerVersion = config.SteamGameServerVersion,
                    SteamGameServerMode = config.SteamGameServerMode.ToString(),
                    SteamWebApiKey = config.SteamWebApiKey,
                    SteamWebApiIdentity = config.SteamWebApiIdentity
                },
                TcpConsole = new WebPanelTcpConsoleSection
                {
                    TcpConsoleEnabled = config.TcpConsoleEnabled,
                    TcpConsoleBindAddress = config.TcpConsoleBindAddress,
                    TcpConsolePort = config.TcpConsolePort,
                    TcpConsoleMaxConnections = config.TcpConsoleMaxConnections,
                    TcpConsoleRequirePassword = config.TcpConsoleRequirePassword,
                    TcpConsolePassword = config.TcpConsolePassword,
                    StdioConsoleMode = config.StdioConsoleMode.ToString()
                },
                WebPanel = new WebPanelLocalPanelSection
                {
                    WebPanelEnabled = config.WebPanelEnabled,
                    WebPanelBindAddress = config.WebPanelBindAddress,
                    WebPanelPort = config.WebPanelPort,
                    WebPanelOpenBrowserOnStart = config.WebPanelOpenBrowserOnStart,
                    WebPanelSessionMinutes = config.WebPanelSessionMinutes,
                    WebPanelExposeLogs = config.WebPanelExposeLogs
                },
                Gameplay = new WebPanelGameplaySection
                {
                    IgnoreGhostHostForSleep = config.IgnoreGhostHostForSleep,
                    TimeProgressionMultiplier = config.TimeProgressionMultiplier,
                    AllowSleeping = config.AllowSleeping,
                    PauseGameWhenEmpty = config.PauseGameWhenEmpty
                },
                Autosave = new WebPanelAutosaveSection
                {
                    AutoSaveEnabled = config.AutoSaveEnabled,
                    AutoSaveIntervalMinutes = config.AutoSaveIntervalMinutes,
                    AutoSaveOnPlayerJoin = config.AutoSaveOnPlayerJoin,
                    AutoSaveOnPlayerLeave = config.AutoSaveOnPlayerLeave
                },
                Logging = new WebPanelLoggingSection
                {
                    DebugMode = config.DebugMode,
                    VerboseLogging = config.VerboseLogging,
                    LogPlayerActions = config.LogPlayerActions,
                    LogAdminCommands = config.LogAdminCommands,
                    EnablePerformanceMonitoring = config.EnablePerformanceMonitoring,
                    LogNetworkingDebug = config.LogNetworkingDebug,
                    LogMessageRoutingDebug = config.LogMessageRoutingDebug,
                    LogMessagingBackendDebug = config.LogMessagingBackendDebug,
                    LogStartupDebug = config.LogStartupDebug,
                    LogServerNetworkDebug = config.LogServerNetworkDebug,
                    LogPlayerLifecycleDebug = config.LogPlayerLifecycleDebug,
                    LogAuthenticationDebug = config.LogAuthenticationDebug
                },
                Performance = new WebPanelPerformanceSection
                {
                    TargetFrameRate = config.TargetFrameRate,
                    VSyncCount = config.VSyncCount
                },
                Storage = new WebPanelStorageSection
                {
                    SaveGamePath = config.SaveGamePath,
                    ResolvedSaveGamePath = ServerConfig.GetResolvedSaveGamePath()
                }
            };
        }

        public WebPanelConfigSnapshot ApplyConfigSnapshot(WebPanelConfigSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ServerConfig config = ServerConfig.Instance;
            config.ServerName = snapshot.Server.ServerName ?? string.Empty;
            config.ServerDescription = snapshot.Server.ServerDescription ?? string.Empty;
            config.MaxPlayers = snapshot.Server.MaxPlayers;
            config.ServerPort = snapshot.Server.ServerPort;
            config.ServerPassword = snapshot.Server.ServerPassword ?? string.Empty;

            config.AuthProvider = ParseEnum(snapshot.Authentication.AuthProvider, config.AuthProvider);
            config.AuthTimeoutSeconds = snapshot.Authentication.AuthTimeoutSeconds;
            config.AuthAllowLoopbackBypass = snapshot.Authentication.AuthAllowLoopbackBypass;
            config.ModVerificationEnabled = snapshot.Authentication.ModVerificationEnabled;
            config.ModVerificationTimeoutSeconds = snapshot.Authentication.ModVerificationTimeoutSeconds;
            config.BlockKnownRiskyClientMods = snapshot.Authentication.BlockKnownRiskyClientMods;
            config.AllowUnpairedClientMods = snapshot.Authentication.AllowUnpairedClientMods;
            config.StrictClientModMode = snapshot.Authentication.StrictClientModMode;
            config.SteamGameServerLogOnAnonymous = snapshot.Authentication.SteamGameServerLogOnAnonymous;
            config.SteamGameServerToken = snapshot.Authentication.SteamGameServerToken ?? string.Empty;
            config.SteamGameServerQueryPort = snapshot.Authentication.SteamGameServerQueryPort;
            config.SteamGameServerVersion = snapshot.Authentication.SteamGameServerVersion ?? string.Empty;
            config.SteamGameServerMode = ParseEnum(snapshot.Authentication.SteamGameServerMode, config.SteamGameServerMode);
            config.SteamWebApiKey = snapshot.Authentication.SteamWebApiKey ?? string.Empty;
            config.SteamWebApiIdentity = snapshot.Authentication.SteamWebApiIdentity ?? string.Empty;

            config.TcpConsoleEnabled = snapshot.TcpConsole.TcpConsoleEnabled;
            config.TcpConsoleBindAddress = snapshot.TcpConsole.TcpConsoleBindAddress ?? string.Empty;
            config.TcpConsolePort = snapshot.TcpConsole.TcpConsolePort;
            config.TcpConsoleMaxConnections = snapshot.TcpConsole.TcpConsoleMaxConnections;
            config.TcpConsoleRequirePassword = snapshot.TcpConsole.TcpConsoleRequirePassword;
            config.TcpConsolePassword = snapshot.TcpConsole.TcpConsolePassword ?? string.Empty;
            config.StdioConsoleMode = ParseEnum(snapshot.TcpConsole.StdioConsoleMode, config.StdioConsoleMode);

            config.WebPanelEnabled = snapshot.WebPanel.WebPanelEnabled;
            config.WebPanelBindAddress = snapshot.WebPanel.WebPanelBindAddress ?? string.Empty;
            config.WebPanelPort = snapshot.WebPanel.WebPanelPort;
            config.WebPanelOpenBrowserOnStart = snapshot.WebPanel.WebPanelOpenBrowserOnStart;
            config.WebPanelSessionMinutes = snapshot.WebPanel.WebPanelSessionMinutes;
            config.WebPanelExposeLogs = snapshot.WebPanel.WebPanelExposeLogs;

            config.IgnoreGhostHostForSleep = snapshot.Gameplay.IgnoreGhostHostForSleep;
            config.TimeProgressionMultiplier = (float)snapshot.Gameplay.TimeProgressionMultiplier;
            config.AllowSleeping = snapshot.Gameplay.AllowSleeping;
            config.PauseGameWhenEmpty = snapshot.Gameplay.PauseGameWhenEmpty;

            config.AutoSaveEnabled = snapshot.Autosave.AutoSaveEnabled;
            config.AutoSaveIntervalMinutes = (float)snapshot.Autosave.AutoSaveIntervalMinutes;
            config.AutoSaveOnPlayerJoin = snapshot.Autosave.AutoSaveOnPlayerJoin;
            config.AutoSaveOnPlayerLeave = snapshot.Autosave.AutoSaveOnPlayerLeave;

            config.DebugMode = snapshot.Logging.DebugMode;
            config.VerboseLogging = snapshot.Logging.VerboseLogging;
            config.LogPlayerActions = snapshot.Logging.LogPlayerActions;
            config.LogAdminCommands = snapshot.Logging.LogAdminCommands;
            config.EnablePerformanceMonitoring = snapshot.Logging.EnablePerformanceMonitoring;
            config.LogNetworkingDebug = snapshot.Logging.LogNetworkingDebug;
            config.LogMessageRoutingDebug = snapshot.Logging.LogMessageRoutingDebug;
            config.LogMessagingBackendDebug = snapshot.Logging.LogMessagingBackendDebug;
            config.LogStartupDebug = snapshot.Logging.LogStartupDebug;
            config.LogServerNetworkDebug = snapshot.Logging.LogServerNetworkDebug;
            config.LogPlayerLifecycleDebug = snapshot.Logging.LogPlayerLifecycleDebug;
            config.LogAuthenticationDebug = snapshot.Logging.LogAuthenticationDebug;

            config.TargetFrameRate = snapshot.Performance.TargetFrameRate;
            config.VSyncCount = snapshot.Performance.VSyncCount;
            config.SaveGamePath = snapshot.Storage.SaveGamePath ?? string.Empty;

            config.Validate();
            ServerConfig.SaveConfig();
            return CreateConfigSnapshot();
        }

        private WebPanelPlayerRow CreatePlayerRow(ConnectedPlayerInfo player)
        {
            string trustedId = player.TrustedUniqueId ?? string.Empty;
            IReadOnlyList<string> roles = string.IsNullOrWhiteSpace(trustedId)
                ? Array.Empty<string>()
                : _permissionService.GetEffectiveGroups(trustedId);

            return new WebPanelPlayerRow
            {
                ClientId = player.ClientId,
                DisplayName = player.DisplayName,
                SteamId = player.SteamId ?? string.Empty,
                TrustedUniqueId = trustedId,
                IsLoopback = player.IsLoopbackConnection,
                IsConnected = player.IsConnected,
                IsSpawned = player.IsSpawned,
                IsAuthenticated = player.IsAuthenticated,
                IsAuthenticationPending = player.IsAuthenticationPending,
                IsModVerificationComplete = player.IsModVerificationComplete,
                IsModVerificationPending = player.IsModVerificationPending,
                RoleSummary = roles.Count == 0 ? "default" : string.Join(", ", roles),
                ConnectedAtUtc = player.ConnectTime.ToUniversalTime(),
                ConnectionDuration = player.ConnectionDuration.ToString(@"dd\.hh\:mm\:ss")
            };
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback)
            where TEnum : struct
        {
            if (Enum.TryParse(value ?? string.Empty, ignoreCase: true, out TEnum parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
