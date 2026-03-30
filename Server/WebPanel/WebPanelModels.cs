namespace DedicatedServerMod.Server.WebPanel
{
    /// <summary>
    /// Represents the top-level dashboard snapshot shown in the browser panel.
    /// </summary>
    internal sealed class ServerDashboardSnapshot
    {
        public string ServerName { get; set; } = string.Empty;
        public string ServerDescription { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ServerPort { get; set; }
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public double FramesPerSecond { get; set; }
        public double FrameTimeMilliseconds { get; set; }
        public double UptimeSeconds { get; set; }
        public string UptimeDisplay { get; set; } = string.Empty;
        public string AuthProvider { get; set; } = string.Empty;
        public bool AutoSaveEnabled { get; set; }
        public double AutoSaveIntervalMinutes { get; set; }
        public bool SaveInProgress { get; set; }
        public DateTime? LastSaveUtc { get; set; }
        public int TotalGroups { get; set; }
        public int TotalUsers { get; set; }
        public int TotalBans { get; set; }
        public int TotalOperators { get; set; }
        public int TotalAdministrators { get; set; }
    }

    /// <summary>
    /// Represents a player row for the browser panel.
    /// </summary>
    internal sealed class WebPanelPlayerRow
    {
        public int ClientId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string SteamId { get; set; } = string.Empty;
        public string TrustedUniqueId { get; set; } = string.Empty;
        public bool IsLoopback { get; set; }
        public bool IsConnected { get; set; }
        public bool IsSpawned { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsAuthenticationPending { get; set; }
        public bool IsModVerificationComplete { get; set; }
        public bool IsModVerificationPending { get; set; }
        public string RoleSummary { get; set; } = string.Empty;
        public DateTime ConnectedAtUtc { get; set; }
        public string ConnectionDuration { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a structured browser-panel log entry.
    /// </summary>
    internal sealed class WebPanelLogEntry
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string Level { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = "runtime";
    }

    /// <summary>
    /// Request payload for browser panel command execution.
    /// </summary>
    internal sealed class WebPanelCommandRequest
    {
        public string CommandLine { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a single console output line returned to the browser panel.
    /// </summary>
    internal sealed class WebPanelCommandOutputLine
    {
        public string Level { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response payload for browser panel command execution.
    /// </summary>
    internal sealed class WebPanelCommandResult
    {
        public bool Succeeded { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CommandWord { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<WebPanelCommandOutputLine> Output { get; set; } = new List<WebPanelCommandOutputLine>();
    }

    /// <summary>
    /// Initial bootstrap payload for the browser panel.
    /// </summary>
    internal sealed class WebPanelBootstrapPayload
    {
        public string Version { get; set; } = string.Empty;
        public string ConfigFilePath { get; set; } = string.Empty;
        public string PermissionsFilePath { get; set; } = string.Empty;
        public string UserDataPath { get; set; } = string.Empty;
        public DateTime? SessionExpiresAtUtc { get; set; }
        public ServerDashboardSnapshot Overview { get; set; } = new ServerDashboardSnapshot();
        public WebPanelConfigSnapshot Config { get; set; } = new WebPanelConfigSnapshot();
        public List<WebPanelLogEntry> RecentLogs { get; set; } = new List<WebPanelLogEntry>();
    }

    /// <summary>
    /// Represents the editable browser-panel configuration payload.
    /// </summary>
    internal sealed class WebPanelConfigSnapshot
    {
        public WebPanelServerSection Server { get; set; } = new WebPanelServerSection();
        public WebPanelAuthenticationSection Authentication { get; set; } = new WebPanelAuthenticationSection();
        public WebPanelTcpConsoleSection TcpConsole { get; set; } = new WebPanelTcpConsoleSection();
        public WebPanelLocalPanelSection WebPanel { get; set; } = new WebPanelLocalPanelSection();
        public WebPanelGameplaySection Gameplay { get; set; } = new WebPanelGameplaySection();
        public WebPanelAutosaveSection Autosave { get; set; } = new WebPanelAutosaveSection();
        public WebPanelLoggingSection Logging { get; set; } = new WebPanelLoggingSection();
        public WebPanelPerformanceSection Performance { get; set; } = new WebPanelPerformanceSection();
        public WebPanelStorageSection Storage { get; set; } = new WebPanelStorageSection();
    }

    internal sealed class WebPanelServerSection
    {
        public string ServerName { get; set; } = string.Empty;
        public string ServerDescription { get; set; } = string.Empty;
        public int MaxPlayers { get; set; }
        public int ServerPort { get; set; }
        public string ServerPassword { get; set; } = string.Empty;
    }

    internal sealed class WebPanelAuthenticationSection
    {
        public string AuthProvider { get; set; } = string.Empty;
        public int AuthTimeoutSeconds { get; set; }
        public bool AuthAllowLoopbackBypass { get; set; }
        public bool ModVerificationEnabled { get; set; }
        public int ModVerificationTimeoutSeconds { get; set; }
        public bool BlockKnownRiskyClientMods { get; set; }
        public bool AllowUnpairedClientMods { get; set; }
        public bool StrictClientModMode { get; set; }
        public bool SteamGameServerLogOnAnonymous { get; set; }
        public string SteamGameServerToken { get; set; } = string.Empty;
        public int SteamGameServerQueryPort { get; set; }
        public string SteamGameServerVersion { get; set; } = string.Empty;
        public string SteamGameServerMode { get; set; } = string.Empty;
        public string SteamWebApiKey { get; set; } = string.Empty;
        public string SteamWebApiIdentity { get; set; } = string.Empty;
    }

    internal sealed class WebPanelTcpConsoleSection
    {
        public bool TcpConsoleEnabled { get; set; }
        public string TcpConsoleBindAddress { get; set; } = string.Empty;
        public int TcpConsolePort { get; set; }
        public int TcpConsoleMaxConnections { get; set; }
        public bool TcpConsoleRequirePassword { get; set; }
        public string TcpConsolePassword { get; set; } = string.Empty;
        public string StdioConsoleMode { get; set; } = string.Empty;
    }

    internal sealed class WebPanelLocalPanelSection
    {
        public bool WebPanelEnabled { get; set; }
        public string WebPanelBindAddress { get; set; } = string.Empty;
        public int WebPanelPort { get; set; }
        public bool WebPanelOpenBrowserOnStart { get; set; }
        public int WebPanelSessionMinutes { get; set; }
        public bool WebPanelExposeLogs { get; set; }
    }

    internal sealed class WebPanelGameplaySection
    {
        public bool IgnoreGhostHostForSleep { get; set; }
        public double TimeProgressionMultiplier { get; set; }
        public bool AllowSleeping { get; set; }
        public bool PauseGameWhenEmpty { get; set; }
    }

    internal sealed class WebPanelAutosaveSection
    {
        public bool AutoSaveEnabled { get; set; }
        public double AutoSaveIntervalMinutes { get; set; }
        public bool AutoSaveOnPlayerJoin { get; set; }
        public bool AutoSaveOnPlayerLeave { get; set; }
    }

    internal sealed class WebPanelLoggingSection
    {
        public bool DebugMode { get; set; }
        public bool VerboseLogging { get; set; }
        public bool LogPlayerActions { get; set; }
        public bool LogAdminCommands { get; set; }
        public bool EnablePerformanceMonitoring { get; set; }
        public bool LogNetworkingDebug { get; set; }
        public bool LogMessageRoutingDebug { get; set; }
        public bool LogMessagingBackendDebug { get; set; }
        public bool LogStartupDebug { get; set; }
        public bool LogServerNetworkDebug { get; set; }
        public bool LogPlayerLifecycleDebug { get; set; }
        public bool LogAuthenticationDebug { get; set; }
    }

    internal sealed class WebPanelPerformanceSection
    {
        public int TargetFrameRate { get; set; }
        public int VSyncCount { get; set; }
    }

    internal sealed class WebPanelStorageSection
    {
        public string SaveGamePath { get; set; } = string.Empty;
        public string ResolvedSaveGamePath { get; set; } = string.Empty;
    }
}
