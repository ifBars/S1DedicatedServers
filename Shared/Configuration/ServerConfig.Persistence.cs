using MelonLoader;
using MelonLoader.Utils;

namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Persistence and file-format handling for <see cref="ServerConfig"/>.
    /// </summary>
    public sealed partial class ServerConfig
    {
        /// <summary>
        /// The MelonLogger instance for configuration logging.
        /// </summary>
        private static MelonLogger.Instance _logger;

        /// <summary>
        /// The configured path to the configuration file.
        /// </summary>
        private static string _configPath;

        /// <summary>
        /// Whether the config path was explicitly provided by the caller.
        /// </summary>
        private static bool _configPathWasExplicit;

        /// <summary>
        /// Gets the resolved configuration file path.
        /// </summary>
        public static string ConfigFilePath => _configPath ?? GetDefaultConfigFilePath();

        /// <summary>
        /// Gets the legacy JSON compatibility path used during transition releases.
        /// </summary>
        public static string LegacyCompatibilityConfigFilePath => Path.Combine(MelonEnvironment.UserDataDirectory, Utils.Constants.LegacyConfigFileName);

        /// <summary>
        /// Gets the path the current configuration instance was loaded from.
        /// </summary>
        public static string LastLoadedFromPath { get; private set; }

        /// <summary>
        /// Gets whether the current configuration instance originated from the legacy JSON format.
        /// </summary>
        public static bool LastLoadedFromLegacyJson { get; private set; }

        /// <summary>
        /// Raised after the active configuration is saved to disk.
        /// </summary>
        public static event Action Saved;

        /// <summary>
        /// Raised after the active configuration is reloaded from disk.
        /// </summary>
        public static event Action Reloaded;

        /// <summary>
        /// Gets the current server configuration instance.
        /// Loads the configuration if not already loaded.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if configuration not initialized</exception>
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

        /// <summary>
        /// Initializes the server configuration system.
        /// Should be called during server startup.
        /// </summary>
        /// <param name="loggerInstance">The logger instance to use.</param>
        /// <param name="configFilePath">Optional custom path for the config file.</param>
        public static void Initialize(MelonLogger.Instance loggerInstance, string configFilePath = null)
        {
            _logger = loggerInstance;
            _configPathWasExplicit = !string.IsNullOrWhiteSpace(configFilePath);
            _configPath = _configPathWasExplicit
                ? Path.GetFullPath(configFilePath)
                : GetDefaultConfigFilePath();

            LoadConfig();
        }

        /// <summary>
        /// Gets the logger instance for configuration operations.
        /// </summary>
        private static MelonLogger.Instance Logger => _logger ?? new MelonLogger.Instance("ServerConfig");

        /// <summary>
        /// Loads the server configuration from disk.
        /// Creates a default configuration if no file exists.
        /// </summary>
        public static void LoadConfig()
        {
            try
            {
                ServerConfigStore.ServerConfigStoreLoadResult loadResult = CreateStore().Load();
                _instance = loadResult.Config ?? new ServerConfig();
                _instance.NormalizeAuthenticationConfiguration();
                _instance.Validate();
                LastLoadedFromPath = loadResult.LoadedFromPath;
                LastLoadedFromLegacyJson = IsJsonConfigPath(loadResult.LoadedFromPath);

                Logger.Msg($"Server configuration loaded successfully from {loadResult.LoadedFromPath}");

                if (loadResult.ShouldWriteNormalizedFile)
                {
                    SaveConfig();

                    if (!string.IsNullOrWhiteSpace(loadResult.RewriteReason))
                    {
                        Logger.Msg(loadResult.RewriteReason);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load server config: {ex}");
                _instance = new ServerConfig();
                SaveConfig();
            }
        }

        /// <summary>
        /// Saves the current configuration to disk.
        /// </summary>
        public static void SaveConfig()
        {
            try
            {
                string configPath = ConfigFilePath;
                CreateStore().Save(_instance ?? new ServerConfig());
                Logger.Msg($"Server configuration saved successfully to {configPath}");
                Saved?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save server config: {ex}");
            }
        }

        /// <summary>
        /// Reloads the configuration from disk.
        /// </summary>
        public static void ReloadConfig()
        {
            Logger.Msg("Reloading server configuration...");
            LoadConfig();
            Reloaded?.Invoke();
        }

        /// <summary>
        /// Resets the configuration state (primarily for testing).
        /// </summary>
        public static void Reset()
        {
            _instance = null;
            _logger = null;
            _configPath = null;
            _configPathWasExplicit = false;
            LastLoadedFromPath = null;
            LastLoadedFromLegacyJson = false;
        }

        /// <summary>
        /// Loads a configuration snapshot from an explicit path without replacing the active singleton instance.
        /// </summary>
        /// <param name="path">The configuration file path to load.</param>
        /// <returns>The loaded configuration snapshot.</returns>
        internal static ServerConfig LoadConfigSnapshot(string path)
        {
            return CreateStore(path, configPathWasExplicit: true).LoadSnapshot(path);
        }

        private static ServerConfigStore CreateStore()
        {
            return CreateStore(ConfigFilePath, _configPathWasExplicit);
        }

        private static ServerConfigStore CreateStore(string configPath, bool configPathWasExplicit)
        {
            return new ServerConfigStore(Logger, configPath, configPathWasExplicit);
        }

        private static string GetDefaultConfigFilePath()
        {
            return Path.Combine(MelonEnvironment.UserDataDirectory, Utils.Constants.ConfigFileName);
        }

        private static bool IsJsonConfigPath(string path)
        {
            return string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
