using System.Diagnostics;
using System.Runtime.InteropServices;
using DedicatedServerMod.Server.Commands;
using DedicatedServerMod.Server.Network;
using DedicatedServerMod.Server.Persistence;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;
using MelonLoader;
using UnityEngine;

namespace DedicatedServerMod.Server.WebPanel
{
    /// <summary>
    /// Coordinates the integrated localhost browser panel lifecycle.
    /// </summary>
    internal sealed class WebPanelManager : IDisposable
    {
        private const float OverviewRefreshIntervalSeconds = 1f;

        private readonly MelonLogger.Instance _logger;
        private readonly PlayerManager _playerManager;
        private readonly ServerPermissionService _permissionService;
        private readonly PersistenceManager _persistenceManager;
        private readonly WebPanelLogBuffer _logBuffer;
        private readonly WebPanelEventStream _eventStream;
        private readonly WebPanelSessionService _sessionService;
        private readonly WebPanelHttpHost _httpHost;
        private readonly WebPanelPerformanceMetrics _performanceMetrics;

        private bool _isActive;
        private float _overviewRefreshElapsedSeconds;

        public WebPanelManager(
            MelonLogger.Instance logger,
            NetworkManager networkManager,
            PlayerManager playerManager,
            ServerPermissionService permissionService,
            CommandManager commandManager,
            PersistenceManager persistenceManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (networkManager == null) throw new ArgumentNullException(nameof(networkManager));
            _playerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _persistenceManager = persistenceManager ?? throw new ArgumentNullException(nameof(persistenceManager));

            _logBuffer = new WebPanelLogBuffer(capacity: 200);
            _eventStream = new WebPanelEventStream();
            _sessionService = new WebPanelSessionService(ServerConfig.Instance.WebPanelSessionMinutes);
            _performanceMetrics = new WebPanelPerformanceMetrics();

            WebPanelSnapshotService snapshotService = new WebPanelSnapshotService(networkManager, _playerManager, _permissionService, _persistenceManager, _performanceMetrics);
            WebPanelStaticFileProvider staticFileProvider = new WebPanelStaticFileProvider();
            WebPanelCommandBridge commandBridge = new WebPanelCommandBridge(commandManager, _eventStream, _logBuffer);
            _httpHost = new WebPanelHttpHost(
                _logger,
                snapshotService,
                _sessionService,
                _eventStream,
                staticFileProvider,
                commandBridge,
                _logBuffer,
                _permissionService,
                _persistenceManager);
        }

        public void Start()
        {
            if (!ServerConfig.Instance.WebPanelEnabled)
            {
                _logger.Msg("Integrated web panel disabled in configuration.");
                return;
            }

            Subscribe();
            try
            {
                _httpHost.Start();
                _isActive = true;
            }
            catch
            {
                Unsubscribe();
                throw;
            }

            PublishLog("info", $"Web panel listening at {_httpHost.LaunchUrl}", "webpanel");
            if (!TryOpenBrowser())
            {
                _logger.Msg($"Open the localhost panel manually: {_httpHost.LaunchUrl}");
            }
        }

        public void Dispose()
        {
            _isActive = false;
            Unsubscribe();
            _httpHost.Dispose();
        }

        public void Tick()
        {
            if (!_isActive)
            {
                return;
            }

            float deltaSeconds = Time.unscaledDeltaTime;
            if (deltaSeconds <= 0f)
            {
                return;
            }

            _performanceMetrics.Tick(deltaSeconds);
            _overviewRefreshElapsedSeconds += deltaSeconds;

            if (_overviewRefreshElapsedSeconds < OverviewRefreshIntervalSeconds)
            {
                return;
            }

            _overviewRefreshElapsedSeconds = 0f;
            _eventStream.Publish("overview.changed", new { reason = "timer" });
        }

        private void Subscribe()
        {
            DebugLog.EntryWritten += OnDebugLogEntryWritten;
            _playerManager.OnPlayerJoined += OnPlayerJoined;
            _playerManager.OnPlayerLeft += OnPlayerLeft;
            _permissionService.StateChanged += OnPermissionStateChanged;
            _persistenceManager.SaveStarted += OnSaveStarted;
            _persistenceManager.SaveCompleted += OnSaveCompleted;
            ServerConfig.Saved += OnConfigSaved;
            ServerConfig.Reloaded += OnConfigReloaded;
        }

        private void Unsubscribe()
        {
            DebugLog.EntryWritten -= OnDebugLogEntryWritten;
            _playerManager.OnPlayerJoined -= OnPlayerJoined;
            _playerManager.OnPlayerLeft -= OnPlayerLeft;
            _permissionService.StateChanged -= OnPermissionStateChanged;
            _persistenceManager.SaveStarted -= OnSaveStarted;
            _persistenceManager.SaveCompleted -= OnSaveCompleted;
            ServerConfig.Saved -= OnConfigSaved;
            ServerConfig.Reloaded -= OnConfigReloaded;
        }

        private void OnDebugLogEntryWritten(DebugLogEntry entry)
        {
            if (entry == null || (!ServerConfig.Instance.WebPanelExposeLogs && !string.Equals(entry.Level.ToString(), "Error", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            PublishLog(MapLevel(entry.Level), entry.Message, "runtime", entry.TimestampUtc);
        }

        private void OnPlayerJoined(ConnectedPlayerInfo player)
        {
            if (player == null)
            {
                return;
            }

            _eventStream.Publish("players.changed", new { reason = "joined", playerId = player.TrustedUniqueId ?? player.UniqueId });
            _eventStream.Publish("overview.changed", new { });
            PublishLog("info", $"Player joined: {player.DisplayName}", "player");
        }

        private void OnPlayerLeft(ConnectedPlayerInfo player)
        {
            if (player == null)
            {
                return;
            }

            _eventStream.Publish("players.changed", new { reason = "left", playerId = player.TrustedUniqueId ?? player.UniqueId });
            _eventStream.Publish("overview.changed", new { });
            PublishLog("warning", $"Player left: {player.DisplayName}", "player");
        }

        private void OnPermissionStateChanged()
        {
            _eventStream.Publish("players.changed", new { reason = "permissions" });
            _eventStream.Publish("overview.changed", new { });
            PublishLog("info", "Permission state updated.", "permissions");
        }

        private void OnSaveStarted(string reason, bool isAutoSave)
        {
            _eventStream.Publish("save.changed", new { status = "started", reason, isAutoSave });
            _eventStream.Publish("overview.changed", new { });
            PublishLog("info", $"Save started: {reason}", isAutoSave ? "autosave" : "save");
        }

        private void OnSaveCompleted(string reason, bool isAutoSave, bool succeeded)
        {
            _eventStream.Publish("save.changed", new { status = succeeded ? "completed" : "failed", reason, isAutoSave });
            _eventStream.Publish("overview.changed", new { });
            PublishLog(succeeded ? "info" : "error", $"Save {(succeeded ? "completed" : "failed")}: {reason}", isAutoSave ? "autosave" : "save");
        }

        private void OnConfigSaved()
        {
            _eventStream.Publish("config.changed", new { reason = "saved" });
            _eventStream.Publish("overview.changed", new { });
            PublishLog("info", "Configuration saved.", "config");
        }

        private void OnConfigReloaded()
        {
            _eventStream.Publish("config.changed", new { reason = "reloaded" });
            _eventStream.Publish("overview.changed", new { });
            PublishLog("warning", "Configuration reloaded from disk.", "config");
        }

        private void PublishLog(string level, string message, string source, DateTime? timestampUtc = null)
        {
            WebPanelLogEntry entry = new WebPanelLogEntry
            {
                TimestampUtc = timestampUtc ?? DateTime.UtcNow,
                Level = string.IsNullOrWhiteSpace(level) ? "info" : level,
                Message = message ?? string.Empty,
                Source = string.IsNullOrWhiteSpace(source) ? "runtime" : source
            };

            _logBuffer.Add(entry);
            _eventStream.Publish("log.append", entry);
        }

        private bool TryOpenBrowser()
        {
            if (!ServerConfig.Instance.WebPanelOpenBrowserOnStart)
            {
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _httpHost.LaunchUrl,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to open web panel in default browser: {ex.Message}");
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", _httpHost.LaunchUrl);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static string MapLevel(DebugLogLevel level)
        {
            switch (level)
            {
                case DebugLogLevel.Warning:
                    return "warning";
                case DebugLogLevel.Error:
                    return "error";
                case DebugLogLevel.Debug:
                case DebugLogLevel.Verbose:
                    return "debug";
                default:
                    return "info";
            }
        }
    }
}
