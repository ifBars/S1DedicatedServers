using MelonLoader.Utils;
using MelonLoader;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Runtime lifecycle helpers for <see cref="ServerConfig"/>.
    /// </summary>
    public sealed partial class ServerConfig
    {
        private const string InMemoryConfigSourceLabel = "[in-memory]";

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
        /// Raised after the active configuration is saved.
        /// </summary>
        public static event Action Saved;

        /// <summary>
        /// Raised after the active configuration is reloaded.
        /// </summary>
        public static event Action Reloaded;

        /// <summary>
        /// Gets the current server configuration instance.
        /// Loads the configuration if not already loaded.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if configuration not initialized.</exception>
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
        /// Initializes the server configuration system for the active runtime.
        /// </summary>
        /// <param name="loggerInstance">Unused legacy logger parameter retained for compatibility.</param>
        /// <param name="configFilePath">Optional custom path for the config file.</param>
        public static void Initialize(MelonLogger.Instance loggerInstance, string configFilePath = null)
        {
            Initialize(configFilePath);
        }

        /// <summary>
        /// Initializes the server configuration system for the active runtime.
        /// </summary>
        /// <param name="configFilePath">Optional custom path for the config file.</param>
        public static void Initialize(string configFilePath = null)
        {
            _configPathWasExplicit = !string.IsNullOrWhiteSpace(configFilePath);
            _configPath = _configPathWasExplicit
                ? Path.GetFullPath(configFilePath)
                : GetDefaultConfigFilePath();

            LoadConfig();
        }

        /// <summary>
        /// Loads the active configuration for the current runtime.
        /// Server builds persist to disk. Client builds keep the configuration in memory only.
        /// </summary>
        public static void LoadConfig()
        {
#if SERVER
            LoadPersistentConfig();
#else
            LoadInMemoryConfig();
#endif
        }

        /// <summary>
        /// Saves the active configuration for the current runtime.
        /// Server builds persist to disk. Client builds update their in-memory snapshot only.
        /// </summary>
        public static void SaveConfig()
        {
#if SERVER
            SavePersistentConfig();
#else
            SaveInMemoryConfig();
#endif
        }

        /// <summary>
        /// Reloads the active configuration state.
        /// </summary>
        public static void ReloadConfig()
        {
            LoadConfig();
            Reloaded?.Invoke();
        }

        /// <summary>
        /// Resets the configuration state (primarily for testing).
        /// </summary>
        public static void Reset()
        {
            _instance = null;
            _configPath = null;
            _configPathWasExplicit = false;
            LastLoadedFromPath = null;
            LastLoadedFromLegacyJson = false;
        }

        private static string GetDefaultConfigFilePath()
        {
            return Path.Combine(MelonEnvironment.UserDataDirectory, Utils.Constants.ConfigFileName);
        }

        private static bool IsJsonConfigPath(string path)
        {
            return string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadInMemoryConfig()
        {
            try
            {
                ApplyLoadedConfig(TryLoadInMemorySeedConfig(), ResolveInMemorySourcePath());
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to initialize client server config in memory: {ex}");
                ApplyLoadedConfig(new ServerConfig(), ResolveInMemorySourcePath());
            }
        }

        private static void SaveInMemoryConfig()
        {
            try
            {
                ApplyLoadedConfig(_instance, LastLoadedFromPath ?? ResolveInMemorySourcePath());
                Saved?.Invoke();
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to update client server config in memory: {ex}");
            }
        }

        private static ServerConfig TryLoadInMemorySeedConfig()
        {
            return new ServerConfig();
        }

        private static string ResolveInMemorySourcePath()
        {
            return InMemoryConfigSourceLabel;
        }

        private static void ApplyLoadedConfig(ServerConfig config, string sourcePath)
        {
            _instance = config ?? new ServerConfig();
            _instance.NormalizeAuthenticationConfiguration();
            _instance.Validate();
            LastLoadedFromPath = sourcePath;
            LastLoadedFromLegacyJson = IsJsonConfigPath(sourcePath);
        }
    }
}
