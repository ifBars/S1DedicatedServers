using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;
using MelonLoader;
using UnityEngine;

namespace DedicatedServerMod.Server.Core
{
    /// <summary>
    /// Applies server runtime behavior derived from configuration without pushing that logic into bootstrap orchestration.
    /// </summary>
    [System.Obsolete(
        "This implementation type is retained for compatibility and will become internal in a future release.",
        false)]
    public sealed class ServerRuntimeConfigurationApplier
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _pendingApplyReasons = new System.Collections.Concurrent.ConcurrentQueue<string>();
        private readonly ServerFramePacer _framePacer;
        private ServerConfig _config;
        private readonly MelonLogger.Instance _logger;
        private bool _isMonitoringConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerRuntimeConfigurationApplier"/> class.
        /// </summary>
        /// <param name="config">Resolved server configuration to apply.</param>
        /// <param name="logger">Logger used for runtime configuration diagnostics.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        public ServerRuntimeConfigurationApplier(ServerConfig config, MelonLogger.Instance logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _framePacer = new ServerFramePacer();
        }

        /// <summary>
        /// Applies runtime settings needed for the dedicated server process.
        /// </summary>
        public void Apply()
        {
            ApplyConfiguredPerformanceSettings("server startup");
            LogResolvedSavePath();
        }

        internal void StartMonitoringConfiguration()
        {
            if (_isMonitoringConfiguration)
            {
                return;
            }

            ServerConfig.Saved += OnConfigurationSaved;
            ServerConfig.Reloaded += OnConfigurationReloaded;
            _isMonitoringConfiguration = true;
        }

        internal void StopMonitoringConfiguration()
        {
            if (!_isMonitoringConfiguration)
            {
                return;
            }

            ServerConfig.Saved -= OnConfigurationSaved;
            ServerConfig.Reloaded -= OnConfigurationReloaded;
            _isMonitoringConfiguration = false;
        }

        internal void Tick()
        {
            string reason = null;
            while (_pendingApplyReasons.TryDequeue(out string pendingReason))
            {
                reason = pendingReason;
            }

            if (reason == null)
            {
                return;
            }

            _config = ServerConfig.Instance;
            ApplyConfiguredPerformanceSettings(reason);
        }

        internal void WaitForNextFrame()
        {
            _framePacer.WaitForNextFrame();
        }

        internal static void ApplyPerformanceSettings(ServerConfig config, string reason)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            QualitySettings.vSyncCount = config.VSyncCount;
            Application.targetFrameRate = config.TargetFrameRate;
            Application.runInBackground = true;

            DebugLog.Info(
                $"Runtime performance settings applied after {reason}: " +
                $"configured Target FPS={config.TargetFrameRate}, configured VSync={config.VSyncCount}, " +
                $"effective Target FPS={Application.targetFrameRate}, effective VSync={QualitySettings.vSyncCount}, " +
                $"software pacing Target FPS={config.TargetFrameRate}, Background={Application.runInBackground}");
        }

        private void ApplyConfiguredPerformanceSettings(string reason)
        {
            _framePacer.SetTargetFrameRate(_config.TargetFrameRate);
            ApplyPerformanceSettings(_config, reason);
        }

        private void LogResolvedSavePath()
        {
            string resolvedSavePath = ServerConfig.GetResolvedSaveGamePath();
            if (string.IsNullOrEmpty(_config.SaveGamePath))
            {
                _logger.Msg($"Using default save location: {resolvedSavePath}");
                _logger.Msg("Tip: You can set a custom 'saveGamePath' in server_config.json to use a different save folder.");
                return;
            }

            _logger.Msg($"Using custom save location: {resolvedSavePath}");
        }

        private void OnConfigurationSaved()
        {
            _pendingApplyReasons.Enqueue("configuration save");
        }

        private void OnConfigurationReloaded()
        {
            _pendingApplyReasons.Enqueue("configuration reload");
        }
    }
}
