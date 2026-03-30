using DedicatedServerMod.API.Configuration;

namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Provides the compiled TOML schema for <see cref="ServerConfig"/>.
    /// </summary>
    internal static class ServerConfigSchema
    {
        public static TomlConfigSchema<ServerConfig> Instance { get; } = Build();

        private static TomlConfigSchema<ServerConfig> Build()
        {
            return TomlConfigSchemaBuilder
                .For<ServerConfig>()
                .FileHeader(
                    "DedicatedServerMod server configuration.",
                    "This file is grouped and commented for easier editing.",
                    "Command-line arguments still override these values at runtime.",
                    $"Legacy {Utils.Constants.LegacyConfigFileName} is imported automatically if this TOML file does not exist.")
                .Section("server", section => section
                    .Comment("Core server identity and connection settings.")
                    .Option(config => config.ServerName, option => option.Comment("Public server name shown in server browsers."))
                    .Option(config => config.ServerDescription, option => option.Comment("Short description displayed to players."))
                    .Option(config => config.MaxPlayers, option => option.Comment("Maximum number of simultaneous players."))
                    .Option(config => config.ServerPort, option => option.Comment("Game server port to listen on."))
                    .Option(config => config.ServerPassword, option => option.Comment("Connection password. Leave empty to disable.")))
                .Section("authentication", section => section
                    .Comment("Dedicated server authentication and client mod verification.")
                    .Option(config => config.AuthProvider, option => option.Comment("Authentication provider: 'None', 'SteamGameServer', or 'SteamWebApi'."))
                    .Option(config => config.AuthTimeoutSeconds, option => option.Comment("Authentication handshake timeout in seconds."))
                    .Option(config => config.AuthAllowLoopbackBypass, option => option.Comment("Allow the local loopback host connection to bypass authentication."))
                    .Option(config => config.ModVerificationEnabled, option => option.Comment("Require the dedicated client mod verification handshake."))
                    .Option(config => config.ModVerificationTimeoutSeconds, option => option.Comment("Client mod verification timeout in seconds."))
                    .Option(config => config.BlockKnownRiskyClientMods, option => option.Comment("Reject known risky client-only mods even when unpaired mods are allowed."))
                    .Option(config => config.AllowUnpairedClientMods, option => option.Comment("Allow client-only mods that do not have a paired server mod."))
                    .Option(config => config.StrictClientModMode, option => option.Comment("Require strict hash pinning for approved client mods."))
                    .Option(config => config.SteamGameServerLogOnAnonymous, option => option.Comment("Use anonymous Steam game server login. Disable to use a server token."))
                    .Option(config => config.SteamGameServerToken, option => option.Comment("Steam game server login token used when anonymous login is disabled."))
                    .Option(config => config.SteamGameServerQueryPort, option => option.Comment("Steam query/status port."))
                    .Option(config => config.SteamGameServerVersion, option => option.Comment("Version string announced to Steam."))
                    .Option(config => config.SteamGameServerMode, option => option.Comment("Steam game server mode: 'NoAuthentication', 'Authentication', or 'AuthenticationAndSecure'."))
                    .Option(config => config.SteamWebApiKey, option => option.Comment("Steam Web API key for SteamWebApi auth mode."))
                    .Option(config => config.SteamWebApiIdentity, option => option.Comment("Optional Steam Web API identity string.")))
                .Section("messaging", section => section
                    .Comment("Custom server-client messaging backend selection.")
                    .Option(config => config.MessagingBackend, option => option.Comment("Messaging backend: 'FishNetRpc', 'SteamP2P', or 'SteamNetworkingSockets'. Use 'FishNetRpc' on Mono and prefer 'SteamNetworkingSockets' on IL2CPP."))
                    .Option(config => config.SteamP2PAllowRelay, option => option.Comment("Allow Steam relay for SteamP2P messaging."))
                    .Option(config => config.SteamP2PChannel, option => option.Comment("SteamP2P channel number."))
                    .Option(config => config.SteamP2PMaxPayloadBytes, option => option.Comment("Maximum SteamP2P payload size in bytes."))
                    .Option(config => config.SteamP2PServerSteamId, option => option.Comment("Optional target server SteamID for client-side SteamP2P routing.")))
                .Section("tcpConsole", section => section
                    .Comment("Remote and local host console controls.")
                    .Option(config => config.TcpConsoleEnabled, option => option.Comment("Enable the TCP admin console."))
                    .Option(config => config.TcpConsoleBindAddress, option => option.Comment("Bind address for the TCP console. Use '127.0.0.1' for local-only access."))
                    .Option(config => config.TcpConsolePort, option => option.Comment("TCP console port."))
                    .Option(config => config.TcpConsoleMaxConnections, option => option.Comment("Maximum concurrent TCP console clients."))
                    .Option(config => config.TcpConsoleRequirePassword, option => option.Comment("Require a password for the TCP console."))
                    .Option(config => config.TcpConsolePassword, option => option.Comment("TCP console password."))
                    .Option(config => config.StdioConsoleMode, option => option.Comment("Host stdio console mode: 'Disabled', 'Auto', or 'Enabled'.")))
                .Section("webPanel", section => section
                    .Comment("Integrated localhost browser panel for server owners.")
                    .Option(config => config.WebPanelEnabled, option => option.Comment("Enable the integrated localhost web panel. Disabled by default for hosted or service-style deployments."))
                    .Option(config => config.WebPanelBindAddress, option => option.Comment("Bind address for the web panel. Use '127.0.0.1' for local-only access."))
                    .Option(config => config.WebPanelPort, option => option.Comment("HTTP port for the integrated web panel."))
                    .Option(config => config.WebPanelOpenBrowserOnStart, option => option.Comment("Attempt to open the web panel in the default browser on startup."))
                    .Option(config => config.WebPanelSessionMinutes, option => option.Comment("Session lifetime in minutes for localhost browser access."))
                    .Option(config => config.WebPanelExposeLogs, option => option.Comment("Expose recent runtime logs to the localhost web panel.")))
                .Section("gameplay", section => section
                    .Comment("Gameplay and simulation behavior on dedicated servers.")
                    .Option(config => config.IgnoreGhostHostForSleep, option => option.Comment("Ignore the loopback ghost host when checking sleep readiness."))
                    .Option(config => config.TimeProgressionMultiplier, option => option.Comment("Time progression multiplier. 1.0 is the default game speed."))
                    .Option(config => config.AllowSleeping, option => option.Comment("Allow players to sleep to advance time."))
                    .Option(config => config.PauseGameWhenEmpty, option => option.Comment("Pause the game simulation when no players are connected.")))
                .Section("autosave", section => section
                    .Comment("Automatic save behavior.")
                    .Option(config => config.AutoSaveEnabled, option => option.Comment("Enable timed auto-saving."))
                    .Option(config => config.AutoSaveIntervalMinutes, option => option.Comment("Minutes between automatic saves."))
                    .Option(config => config.AutoSaveOnPlayerJoin, option => option.Comment("Trigger a save when a player joins."))
                    .Option(config => config.AutoSaveOnPlayerLeave, option => option.Comment("Trigger a save when a player leaves.")))
                .Section("logging", section => section
                    .Comment("Diagnostic and debug logging controls.")
                    .Option(config => config.DebugMode, option => option.Comment("Enable general debug logging."))
                    .Option(config => config.VerboseLogging, option => option.Comment("Enable verbose trace logging."))
                    .Option(config => config.LogPlayerActions, option => option.Comment("Log player action details."))
                    .Option(config => config.LogAdminCommands, option => option.Comment("Write privileged action usage to admin_actions.log."))
                    .Option(config => config.EnablePerformanceMonitoring, option => option.Comment("Enable performance monitoring instrumentation."))
                    .Option(config => config.LogNetworkingDebug, option => option.Comment("Enable shared networking debug logging."))
                    .Option(config => config.LogMessageRoutingDebug, option => option.Comment("Enable message routing debug logging."))
                    .Option(config => config.LogMessagingBackendDebug, option => option.Comment("Enable messaging backend debug logging."))
                    .Option(config => config.LogStartupDebug, option => option.Comment("Enable startup orchestration debug logging."))
                    .Option(config => config.LogServerNetworkDebug, option => option.Comment("Enable server network lifecycle debug logging."))
                    .Option(config => config.LogPlayerLifecycleDebug, option => option.Comment("Enable player lifecycle debug logging."))
                    .Option(config => config.LogAuthenticationDebug, option => option.Comment("Enable authentication debug logging.")))
                .Section("performance", section => section
                    .Comment("Headless performance tuning.")
                    .Option(config => config.TargetFrameRate, option => option.Comment("Target frame rate. Use -1 for unlimited."))
                    .Option(config => config.VSyncCount, option => option.Comment("VSync count. Dedicated servers should usually keep this at 0.")))
                .Section("storage", section => section
                    .Comment("Save-file location settings.")
                    .Option(config => config.SaveGamePath, option => option.Comment("Optional custom save path. Empty uses UserData/DedicatedServerSave.")))
                .Build();
        }
    }
}
