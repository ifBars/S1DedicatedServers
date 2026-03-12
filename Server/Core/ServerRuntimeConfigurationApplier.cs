using System;
using DedicatedServerMod.Shared.Configuration;
using MelonLoader;
using UnityEngine;

namespace DedicatedServerMod.Server.Core
{
    /// <summary>
    /// Applies server runtime behavior derived from configuration without pushing that logic into bootstrap orchestration.
    /// </summary>
    public sealed class ServerRuntimeConfigurationApplier
    {
        private readonly ServerConfig _config;
        private readonly MelonLogger.Instance _logger;

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
        }

        /// <summary>
        /// Applies runtime settings needed for the dedicated server process.
        /// </summary>
        public void Apply()
        {
            ApplyPerformanceSettings();
            LogResolvedSavePath();
        }

        private void ApplyPerformanceSettings()
        {
            Application.targetFrameRate = _config.TargetFrameRate;
            QualitySettings.vSyncCount = _config.VSyncCount;
            Application.runInBackground = true;

            _logger.Msg($"✓ Performance settings applied: Target FPS={Application.targetFrameRate}, VSync={QualitySettings.vSyncCount}, Background={Application.runInBackground}");
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
    }
}
