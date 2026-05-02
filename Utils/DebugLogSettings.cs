using MelonLoader;

namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Provides the runtime logging switches consumed by <see cref="DebugLog"/>.
    /// </summary>
    internal interface IDebugLogSettings
    {
        bool DebugMode { get; }

        bool VerboseLogging { get; }

        bool LogAdminCommands { get; }

        bool LogNetworkingDebug { get; }

        bool LogMessageRoutingDebug { get; }

        bool LogMessagingBackendDebug { get; }

        bool LogStartupDebug { get; }

        bool LogServerNetworkDebug { get; }

        bool LogPlayerLifecycleDebug { get; }

        bool LogAuthenticationDebug { get; }
    }

    /// <summary>
    /// Reads logging switches from the active server configuration.
    /// </summary>
    internal sealed class ServerConfigDebugLogSettings : IDebugLogSettings
    {
        public bool DebugMode => Config?.DebugMode ?? false;

        public bool VerboseLogging => Config?.VerboseLogging ?? false;

        public bool LogAdminCommands => Config?.LogAdminCommands ?? true;

        public bool LogNetworkingDebug => Config?.LogNetworkingDebug ?? false;

        public bool LogMessageRoutingDebug => Config?.LogMessageRoutingDebug ?? false;

        public bool LogMessagingBackendDebug => Config?.LogMessagingBackendDebug ?? false;

        public bool LogStartupDebug => Config?.LogStartupDebug ?? false;

        public bool LogServerNetworkDebug => Config?.LogServerNetworkDebug ?? false;

        public bool LogPlayerLifecycleDebug => Config?.LogPlayerLifecycleDebug ?? false;

        public bool LogAuthenticationDebug => Config?.LogAuthenticationDebug ?? false;

        private static Shared.Configuration.ServerConfig Config
        {
            get
            {
                try
                {
                    return Shared.Configuration.ServerConfig.Instance;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

#if CLIENT
    /// <summary>
    /// Reads client logging switches from MelonPreferences.cfg.
    /// </summary>
    internal sealed class ClientMelonPreferencesDebugLogSettings : IDebugLogSettings
    {
        private const string CategoryIdentifier = "DedicatedServerClient";
        private const string CategoryDisplayName = "Dedicated Server Client Logging";

        private readonly MelonPreferences_Entry<bool> _debugMode;
        private readonly MelonPreferences_Entry<bool> _verboseLogging;
        private readonly MelonPreferences_Entry<bool> _logNetworkingDebug;
        private readonly MelonPreferences_Entry<bool> _logMessageRoutingDebug;
        private readonly MelonPreferences_Entry<bool> _logMessagingBackendDebug;
        private readonly MelonPreferences_Entry<bool> _logStartupDebug;
        private readonly MelonPreferences_Entry<bool> _logServerNetworkDebug;
        private readonly MelonPreferences_Entry<bool> _logPlayerLifecycleDebug;
        private readonly MelonPreferences_Entry<bool> _logAuthenticationDebug;

        public ClientMelonPreferencesDebugLogSettings()
        {
            MelonPreferences_Category category = MelonPreferences.CreateCategory(CategoryIdentifier, CategoryDisplayName);

            _debugMode = CreateEntry(category, Constants.ConfigKeys.DebugMode, "Debug Mode", "Enable general Dedicated Server client debug logs.");
            _verboseLogging = CreateEntry(category, Constants.ConfigKeys.VerboseLogging, "Verbose Logging", "Enable very detailed Dedicated Server client trace logs.");
            _logNetworkingDebug = CreateEntry(category, Constants.ConfigKeys.LogNetworkingDebug, "Networking Debug", "Enable all Dedicated Server client networking debug logs.");
            _logMessageRoutingDebug = CreateEntry(category, Constants.ConfigKeys.LogMessageRoutingDebug, "Message Routing Debug", "Enable client custom-message routing debug logs.");
            _logMessagingBackendDebug = CreateEntry(category, Constants.ConfigKeys.LogMessagingBackendDebug, "Messaging Backend Debug", "Enable client messaging backend send and receive debug logs.");
            _logStartupDebug = CreateEntry(category, Constants.ConfigKeys.LogStartupDebug, "Startup Debug", "Enable client startup and initialization debug logs.");
            _logServerNetworkDebug = CreateEntry(category, Constants.ConfigKeys.LogServerNetworkDebug, "Server Network Debug", "Enable transport and network lifecycle debug logs.");
            _logPlayerLifecycleDebug = CreateEntry(category, Constants.ConfigKeys.LogPlayerLifecycleDebug, "Player Lifecycle Debug", "Enable player lifecycle debug logs.");
            _logAuthenticationDebug = CreateEntry(category, Constants.ConfigKeys.LogAuthenticationDebug, "Authentication Debug", "Enable client authentication handshake debug logs.");
        }

        public bool DebugMode => _debugMode.Value;

        public bool VerboseLogging => _verboseLogging.Value;

        public bool LogAdminCommands => true;

        public bool LogNetworkingDebug => _logNetworkingDebug.Value;

        public bool LogMessageRoutingDebug => _logMessageRoutingDebug.Value;

        public bool LogMessagingBackendDebug => _logMessagingBackendDebug.Value;

        public bool LogStartupDebug => _logStartupDebug.Value;

        public bool LogServerNetworkDebug => _logServerNetworkDebug.Value;

        public bool LogPlayerLifecycleDebug => _logPlayerLifecycleDebug.Value;

        public bool LogAuthenticationDebug => _logAuthenticationDebug.Value;

        private static MelonPreferences_Entry<bool> CreateEntry(
            MelonPreferences_Category category,
            string identifier,
            string displayName,
            string description)
        {
            return category.CreateEntry(identifier, false, displayName, description, false, false);
        }
    }
#endif
}
