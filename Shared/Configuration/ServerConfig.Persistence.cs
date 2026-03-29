using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DedicatedServerMod.Shared.Networking.Messaging;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Persistence and file-format handling for <see cref="ServerConfig"/>.
    /// </summary>
    public sealed partial class ServerConfig
    {
        private static readonly TomlSectionDefinition[] TomlSections =
        {
            new TomlSectionDefinition(
                "server",
                "Core server identity and connection settings.",
                new[]
                {
                    new TomlEntryDefinition(nameof(ServerName), "Public server name shown in server browsers."),
                    new TomlEntryDefinition(nameof(ServerDescription), "Short description displayed to players."),
                    new TomlEntryDefinition(nameof(MaxPlayers), "Maximum number of simultaneous players."),
                    new TomlEntryDefinition(nameof(ServerPort), "Game server port to listen on."),
                    new TomlEntryDefinition(nameof(ServerPassword), "Connection password. Leave empty to disable."),
                }),
            new TomlSectionDefinition(
                "authentication",
                "Dedicated server authentication and client mod verification.",
                new[]
                {
                    new TomlEntryDefinition(nameof(AuthProvider), "Authentication provider: 'None', 'SteamGameServer', or 'SteamWebApi'."),
                    new TomlEntryDefinition(nameof(AuthTimeoutSeconds), "Authentication handshake timeout in seconds."),
                    new TomlEntryDefinition(nameof(AuthAllowLoopbackBypass), "Allow the local loopback host connection to bypass authentication."),
                    new TomlEntryDefinition(nameof(ModVerificationEnabled), "Require the dedicated client mod verification handshake."),
                    new TomlEntryDefinition(nameof(ModVerificationTimeoutSeconds), "Client mod verification timeout in seconds."),
                    new TomlEntryDefinition(nameof(BlockKnownRiskyClientMods), "Reject known risky client-only mods even when unpaired mods are allowed."),
                    new TomlEntryDefinition(nameof(AllowUnpairedClientMods), "Allow client-only mods that do not have a paired server mod."),
                    new TomlEntryDefinition(nameof(StrictClientModMode), "Require strict hash pinning for approved client mods."),
                    new TomlEntryDefinition(nameof(SteamGameServerLogOnAnonymous), "Use anonymous Steam game server login. Disable to use a server token."),
                    new TomlEntryDefinition(nameof(SteamGameServerToken), "Steam game server login token used when anonymous login is disabled."),
                    new TomlEntryDefinition(nameof(SteamGameServerQueryPort), "Steam query/status port."),
                    new TomlEntryDefinition(nameof(SteamGameServerVersion), "Version string announced to Steam."),
                    new TomlEntryDefinition(nameof(SteamGameServerMode), "Steam game server mode: 'NoAuthentication', 'Authentication', or 'AuthenticationAndSecure'."),
                    new TomlEntryDefinition(nameof(SteamWebApiKey), "Steam Web API key for SteamWebApi auth mode."),
                    new TomlEntryDefinition(nameof(SteamWebApiIdentity), "Optional Steam Web API identity string."),
                }),
            new TomlSectionDefinition(
                "messaging",
                "Custom server-client messaging backend selection.",
                new[]
                {
                    new TomlEntryDefinition(nameof(MessagingBackend), "Messaging backend: 'FishNetRpc', 'SteamP2P', or 'SteamNetworkingSockets'. Use 'FishNetRpc' on Mono and prefer 'SteamNetworkingSockets' on IL2CPP."),
                    new TomlEntryDefinition(nameof(SteamP2PAllowRelay), "Allow Steam relay for SteamP2P messaging."),
                    new TomlEntryDefinition(nameof(SteamP2PChannel), "SteamP2P channel number."),
                    new TomlEntryDefinition(nameof(SteamP2PMaxPayloadBytes), "Maximum SteamP2P payload size in bytes."),
                    new TomlEntryDefinition(nameof(SteamP2PServerSteamId), "Optional target server SteamID for client-side SteamP2P routing."),
                }),
            new TomlSectionDefinition(
                "tcpConsole",
                "Remote and local host console controls.",
                new[]
                {
                    new TomlEntryDefinition(nameof(TcpConsoleEnabled), "Enable the TCP admin console."),
                    new TomlEntryDefinition(nameof(TcpConsoleBindAddress), "Bind address for the TCP console. Use '127.0.0.1' for local-only access."),
                    new TomlEntryDefinition(nameof(TcpConsolePort), "TCP console port."),
                    new TomlEntryDefinition(nameof(TcpConsoleMaxConnections), "Maximum concurrent TCP console clients."),
                    new TomlEntryDefinition(nameof(TcpConsoleRequirePassword), "Require a password for the TCP console."),
                    new TomlEntryDefinition(nameof(TcpConsolePassword), "TCP console password."),
                    new TomlEntryDefinition(nameof(StdioConsoleMode), "Host stdio console mode: 'Disabled', 'Auto', or 'Enabled'."),
                }),
            new TomlSectionDefinition(
                "gameplay",
                "Gameplay and simulation behavior on dedicated servers.",
                new[]
                {
                    new TomlEntryDefinition(nameof(IgnoreGhostHostForSleep), "Ignore the loopback ghost host when checking sleep readiness."),
                    new TomlEntryDefinition(nameof(TimeProgressionMultiplier), "Time progression multiplier. 1.0 is the default game speed."),
                    new TomlEntryDefinition(nameof(AllowSleeping), "Allow players to sleep to advance time."),
                    new TomlEntryDefinition(nameof(PauseGameWhenEmpty), "Pause the game simulation when no players are connected."),
                }),
            new TomlSectionDefinition(
                "autosave",
                "Automatic save behavior.",
                new[]
                {
                    new TomlEntryDefinition(nameof(AutoSaveEnabled), "Enable timed auto-saving."),
                    new TomlEntryDefinition(nameof(AutoSaveIntervalMinutes), "Minutes between automatic saves."),
                    new TomlEntryDefinition(nameof(AutoSaveOnPlayerJoin), "Trigger a save when a player joins."),
                    new TomlEntryDefinition(nameof(AutoSaveOnPlayerLeave), "Trigger a save when a player leaves."),
                }),
            new TomlSectionDefinition(
                "permissions",
                "Permission lists and console command access rules.",
                new[]
                {
                    new TomlEntryDefinition(nameof(Operators), "SteamID64 values with operator privileges."),
                    new TomlEntryDefinition(nameof(Admins), "SteamID64 values with admin privileges."),
                    new TomlEntryDefinition(nameof(BannedPlayers), "SteamID64 values blocked from joining."),
                    new TomlEntryDefinition(nameof(EnableConsoleForOps), "Allow operators to open the in-game admin console."),
                    new TomlEntryDefinition(nameof(EnableConsoleForAdmins), "Allow admins to open the in-game admin console."),
                    new TomlEntryDefinition(nameof(EnableConsoleForPlayers), "Allow regular players to open the in-game admin console."),
                    new TomlEntryDefinition(nameof(LogAdminCommands), "Write admin command usage to admin_actions.log."),
                    new TomlEntryDefinition(nameof(AllowedCommands), "Commands admins may use without operator status."),
                    new TomlEntryDefinition(nameof(RestrictedCommands), "Commands reserved for operators only."),
                    new TomlEntryDefinition(nameof(PlayerAllowedCommands), "Commands regular players may use."),
                    new TomlEntryDefinition(nameof(GlobalDisabledCommands), "Commands disabled for everyone."),
                }),
            new TomlSectionDefinition(
                "logging",
                "Diagnostic and debug logging controls.",
                new[]
                {
                    new TomlEntryDefinition(nameof(DebugMode), "Enable general debug logging."),
                    new TomlEntryDefinition(nameof(VerboseLogging), "Enable verbose trace logging."),
                    new TomlEntryDefinition(nameof(LogPlayerActions), "Log player action details."),
                    new TomlEntryDefinition(nameof(EnablePerformanceMonitoring), "Enable performance monitoring instrumentation."),
                    new TomlEntryDefinition(nameof(LogNetworkingDebug), "Enable shared networking debug logging."),
                    new TomlEntryDefinition(nameof(LogMessageRoutingDebug), "Enable message routing debug logging."),
                    new TomlEntryDefinition(nameof(LogMessagingBackendDebug), "Enable messaging backend debug logging."),
                    new TomlEntryDefinition(nameof(LogStartupDebug), "Enable startup orchestration debug logging."),
                    new TomlEntryDefinition(nameof(LogServerNetworkDebug), "Enable server network lifecycle debug logging."),
                    new TomlEntryDefinition(nameof(LogPlayerLifecycleDebug), "Enable player lifecycle debug logging."),
                    new TomlEntryDefinition(nameof(LogAuthenticationDebug), "Enable authentication debug logging."),
                }),
            new TomlSectionDefinition(
                "performance",
                "Headless performance tuning.",
                new[]
                {
                    new TomlEntryDefinition(nameof(TargetFrameRate), "Target frame rate. Use -1 for unlimited."),
                    new TomlEntryDefinition(nameof(VSyncCount), "VSync count. Dedicated servers should usually keep this at 0."),
                }),
            new TomlSectionDefinition(
                "storage",
                "Save-file location settings.",
                new[]
                {
                    new TomlEntryDefinition(nameof(SaveGamePath), "Optional custom save path. Empty uses UserData/DedicatedServerSave."),
                }),
        };

        private static readonly Dictionary<string, TomlEntryDefinition> TomlEntriesByKey =
            TomlSections
                .SelectMany(section => section.Entries)
                .ToDictionary(entry => entry.Key, entry => entry, StringComparer.Ordinal);

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

        private static string LegacyConfigFilePath => Path.Combine(MelonEnvironment.UserDataDirectory, Utils.Constants.LegacyConfigFileName);

        /// <summary>
        /// Gets the legacy JSON compatibility path used during transition releases.
        /// </summary>
        public static string LegacyCompatibilityConfigFilePath => LegacyConfigFilePath;

        /// <summary>
        /// Gets the path the current configuration instance was loaded from.
        /// </summary>
        public static string LastLoadedFromPath { get; private set; }

        /// <summary>
        /// Gets whether the current configuration instance originated from the legacy JSON format.
        /// </summary>
        public static bool LastLoadedFromLegacyJson { get; private set; }

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
                ServerConfigStorageLoadResult loadResult = LoadConfigFromDisk();
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
                string directory = Path.GetDirectoryName(configPath);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string serializedConfig = SerializeConfig(_instance ?? new ServerConfig(), configPath);
                File.WriteAllText(configPath, serializedConfig);
                Logger.Msg($"Server configuration saved successfully to {configPath}");
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
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Configuration path cannot be null or empty.", nameof(path));
            }

            return LoadConfigFromPath(Path.GetFullPath(path)).Config ?? new ServerConfig();
        }

        private static string GetDefaultConfigFilePath()
        {
            return Path.Combine(MelonEnvironment.UserDataDirectory, Utils.Constants.ConfigFileName);
        }

        private static ServerConfigStorageLoadResult LoadConfigFromDisk()
        {
            string configuredPath = ConfigFilePath;

            if (_configPathWasExplicit)
            {
                if (File.Exists(configuredPath))
                {
                    return LoadConfigFromPath(configuredPath);
                }

                return new ServerConfigStorageLoadResult(
                    new ServerConfig(),
                    configuredPath,
                    shouldWriteNormalizedFile: true,
                    rewriteReason: "Created new server configuration file");
            }

            if (File.Exists(configuredPath))
            {
                return LoadConfigFromPath(configuredPath);
            }

            if (File.Exists(LegacyConfigFilePath))
            {
                ServerConfigStorageLoadResult legacyLoadResult = LoadConfigFromPath(LegacyConfigFilePath);
                legacyLoadResult.ShouldWriteNormalizedFile = true;
                legacyLoadResult.RewriteReason =
                    $"Migrated legacy JSON config from {LegacyConfigFilePath} to {configuredPath}.";
                return legacyLoadResult;
            }

            return new ServerConfigStorageLoadResult(
                new ServerConfig(),
                configuredPath,
                shouldWriteNormalizedFile: true,
                rewriteReason: "Created new server configuration file");
        }

        private static ServerConfigStorageLoadResult LoadConfigFromPath(string path)
        {
            string configText = File.ReadAllText(path);

            if (IsJsonConfigPath(path))
            {
                ServerConfig config = JsonConvert.DeserializeObject<ServerConfig>(configText) ?? new ServerConfig();
                string normalizedJson = SerializeJson(config);
                bool needsRewrite = !AreEquivalentJsonDocuments(configText, normalizedJson);

                return new ServerConfigStorageLoadResult(
                    config,
                    path,
                    shouldWriteNormalizedFile: _configPathWasExplicit && needsRewrite,
                    rewriteReason: _configPathWasExplicit && needsRewrite
                        ? "Server configuration updated with normalized and newly added options."
                        : null);
            }

            ServerConfigTomlLoadResult tomlLoadResult = DeserializeToml(configText);
            return new ServerConfigStorageLoadResult(
                tomlLoadResult.Config,
                path,
                shouldWriteNormalizedFile: tomlLoadResult.MissingKnownKeys,
                rewriteReason: tomlLoadResult.MissingKnownKeys
                    ? "Server configuration updated with newly added options."
                    : null);
        }

        private static string SerializeConfig(ServerConfig config, string path)
        {
            return IsJsonConfigPath(path)
                ? SerializeJson(config)
                : SerializeToml(config);
        }

        private static string SerializeJson(ServerConfig config)
        {
            return JsonConvert.SerializeObject(config, Formatting.Indented);
        }

        private static bool AreEquivalentJsonDocuments(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                return true;
            }

            try
            {
                JToken leftToken = JToken.Parse(left ?? string.Empty);
                JToken rightToken = JToken.Parse(right ?? string.Empty);
                return JToken.DeepEquals(leftToken, rightToken);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsJsonConfigPath(string path)
        {
            return string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);
        }

        private static string SerializeToml(ServerConfig config)
        {
            StringBuilder builder = new StringBuilder();

            WriteCommentBlock(
                builder,
                "DedicatedServerMod server configuration.",
                "This file is grouped and commented for easier editing.",
                "Command-line arguments still override these values at runtime.",
                $"Legacy {Utils.Constants.LegacyConfigFileName} is imported automatically if this TOML file does not exist.");
            builder.AppendLine();

            foreach (TomlSectionDefinition section in TomlSections)
            {
                WriteCommentBlock(builder, section.Description);
                builder.Append('[').Append(section.Name).AppendLine("]");

                foreach (TomlEntryDefinition entry in section.Entries)
                {
                    WriteCommentBlock(builder, entry.Description);
                    builder.Append(entry.Key).Append(" = ");
                    AppendTomlValue(builder, entry.Property.GetValue(config), entry.Property.PropertyType);
                    builder.AppendLine();
                    builder.AppendLine();
                }
            }

            return builder.ToString().TrimEnd() + Environment.NewLine;
        }

        private static void WriteCommentBlock(StringBuilder builder, params string[] lines)
        {
            foreach (string line in lines)
            {
                builder.Append("# ").AppendLine(line);
            }
        }

        private static void AppendTomlValue(StringBuilder builder, object value, Type propertyType)
        {
            if (propertyType == typeof(string))
            {
                builder.Append(FormatTomlString(value as string ?? string.Empty));
                return;
            }

            if (propertyType == typeof(bool))
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (propertyType == typeof(int))
            {
                builder.Append(((int)value).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (propertyType == typeof(float))
            {
                builder.Append(((float)value).ToString("0.0###", CultureInfo.InvariantCulture));
                return;
            }

            if (propertyType.IsEnum)
            {
                builder.Append(FormatTomlString(value.ToString()));
                return;
            }

            if (propertyType == typeof(HashSet<string>))
            {
                IReadOnlyList<string> items =
                    ((HashSet<string>)value ?? new HashSet<string>())
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (items.Count == 0)
                {
                    builder.Append("[]");
                    return;
                }

                builder.AppendLine("[");

                foreach (string item in items)
                {
                    builder.Append("    ")
                        .Append(FormatTomlString(item))
                        .AppendLine(",");
                }

                builder.Append("]");
                return;
            }

            throw new NotSupportedException($"Unsupported TOML config value type '{propertyType.FullName}'.");
        }

        private static string FormatTomlString(string value)
        {
            if (!value.Contains('\'') && !value.Contains('\r') && !value.Contains('\n'))
            {
                return $"'{value}'";
            }

            StringBuilder escaped = new StringBuilder(value.Length + 2);

            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\':
                        escaped.Append("\\\\");
                        break;
                    case '\"':
                        escaped.Append("\\\"");
                        break;
                    case '\r':
                        escaped.Append("\\r");
                        break;
                    case '\n':
                        escaped.Append("\\n");
                        break;
                    case '\t':
                        escaped.Append("\\t");
                        break;
                    default:
                        escaped.Append(character);
                        break;
                }
            }

            return $"\"{escaped}\"";
        }

        private static ServerConfigTomlLoadResult DeserializeToml(string toml)
        {
            ServerConfig config = new ServerConfig();
            HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);
            string currentSection = string.Empty;
            string currentKey = null;
            int currentValueStartLine = 0;
            StringBuilder currentValue = null;
            string[] lines = toml.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (int index = 0; index < lines.Length; index++)
            {
                int lineNumber = index + 1;
                string rawLine = lines[index];
                string lineWithoutComment = StripInlineComment(rawLine);
                string trimmedLine = lineWithoutComment.Trim();

                if (currentKey != null)
                {
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        currentValue.AppendLine(trimmedLine);
                    }

                    if (IsTomlValueComplete(currentValue.ToString()))
                    {
                        ApplyTomlValue(config, currentSection, currentKey, currentValue.ToString(), currentValueStartLine, seenKeys);
                        currentKey = null;
                        currentValue = null;
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                if (trimmedLine.StartsWith("[", StringComparison.Ordinal) &&
                    trimmedLine.EndsWith("]", StringComparison.Ordinal) &&
                    trimmedLine.Length > 2)
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                    continue;
                }

                int equalsIndex = trimmedLine.IndexOf('=');

                if (equalsIndex <= 0)
                {
                    Logger.Warning($"Ignoring malformed config line {lineNumber} in {ConfigFilePath}: {rawLine}");
                    continue;
                }

                string key = trimmedLine.Substring(0, equalsIndex).Trim();
                string valueText = trimmedLine.Substring(equalsIndex + 1).Trim();

                if (!IsTomlValueComplete(valueText))
                {
                    currentKey = key;
                    currentValueStartLine = lineNumber;
                    currentValue = new StringBuilder();
                    currentValue.AppendLine(valueText);
                    continue;
                }

                ApplyTomlValue(config, currentSection, key, valueText, lineNumber, seenKeys);
            }

            if (currentKey != null)
            {
                Logger.Warning($"Config key '{currentKey}' in {ConfigFilePath} started at line {currentValueStartLine} but was not completed.");
            }

            bool missingKnownKeys = TomlEntriesByKey.Keys.Any(key => !seenKeys.Contains(key));
            return new ServerConfigTomlLoadResult(config, missingKnownKeys);
        }

        private static string StripInlineComment(string line)
        {
            bool inBasicString = false;
            bool inLiteralString = false;
            bool isEscaped = false;

            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];

                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (inBasicString)
                {
                    if (character == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (character == '"')
                    {
                        inBasicString = false;
                    }

                    continue;
                }

                if (inLiteralString)
                {
                    if (character == '\'')
                    {
                        inLiteralString = false;
                    }

                    continue;
                }

                if (character == '"')
                {
                    inBasicString = true;
                    continue;
                }

                if (character == '\'')
                {
                    inLiteralString = true;
                    continue;
                }

                if (character == '#')
                {
                    return line.Substring(0, index);
                }
            }

            return line;
        }

        private static bool IsTomlValueComplete(string value)
        {
            int bracketDepth = 0;
            bool inBasicString = false;
            bool inLiteralString = false;
            bool isEscaped = false;

            foreach (char character in value)
            {
                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (inBasicString)
                {
                    if (character == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (character == '"')
                    {
                        inBasicString = false;
                    }

                    continue;
                }

                if (inLiteralString)
                {
                    if (character == '\'')
                    {
                        inLiteralString = false;
                    }

                    continue;
                }

                switch (character)
                {
                    case '"':
                        inBasicString = true;
                        break;
                    case '\'':
                        inLiteralString = true;
                        break;
                    case '[':
                        bracketDepth++;
                        break;
                    case ']':
                        bracketDepth--;
                        break;
                }
            }

            return bracketDepth <= 0 && !inBasicString && !inLiteralString;
        }

        private static void ApplyTomlValue(
            ServerConfig config,
            string section,
            string key,
            string rawValue,
            int lineNumber,
            HashSet<string> seenKeys)
        {
            if (!TomlEntriesByKey.TryGetValue(key, out TomlEntryDefinition entry))
            {
                string sectionLabel = string.IsNullOrWhiteSpace(section) ? "root" : section;
                Logger.Warning($"Ignoring unknown config key '{key}' in section '{sectionLabel}' at line {lineNumber}.");
                return;
            }

            if (seenKeys.Contains(key))
            {
                Logger.Warning($"Config key '{key}' is defined more than once. Using the last value from line {lineNumber}.");
            }

            if (TryParseTomlValue(entry.Property.PropertyType, rawValue, key, lineNumber, out object parsedValue))
            {
                entry.Property.SetValue(config, parsedValue);
                seenKeys.Add(key);
            }
        }

        private static bool TryParseTomlValue(Type propertyType, string rawValue, string key, int lineNumber, out object parsedValue)
        {
            string trimmedValue = rawValue.Trim();

            if (propertyType == typeof(string))
            {
                parsedValue = ParseTomlString(trimmedValue);
                return true;
            }

            if (propertyType == typeof(bool))
            {
                if (bool.TryParse(trimmedValue, out bool boolValue))
                {
                    parsedValue = boolValue;
                    return true;
                }

                Logger.Warning($"Invalid boolean value for '{key}' at line {lineNumber}: {trimmedValue}");
                parsedValue = null;
                return false;
            }

            if (propertyType == typeof(int))
            {
                if (int.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                {
                    parsedValue = intValue;
                    return true;
                }

                Logger.Warning($"Invalid integer value for '{key}' at line {lineNumber}: {trimmedValue}");
                parsedValue = null;
                return false;
            }

            if (propertyType == typeof(float))
            {
                if (float.TryParse(trimmedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                {
                    parsedValue = floatValue;
                    return true;
                }

                Logger.Warning($"Invalid floating-point value for '{key}' at line {lineNumber}: {trimmedValue}");
                parsedValue = null;
                return false;
            }

            if (propertyType.IsEnum)
            {
                string enumValue = ParseTomlString(trimmedValue);

                try
                {
                    parsedValue = Enum.Parse(propertyType, enumValue, ignoreCase: true);
                    return true;
                }
                catch (Exception)
                {
                    Logger.Warning($"Invalid enum value for '{key}' at line {lineNumber}: {trimmedValue}");
                    parsedValue = null;
                    return false;
                }
            }

            if (propertyType == typeof(HashSet<string>))
            {
                parsedValue = ParseTomlStringSet(trimmedValue, key, lineNumber);
                return true;
            }

            Logger.Warning($"Unsupported config value type '{propertyType.FullName}' for key '{key}'.");
            parsedValue = null;
            return false;
        }

        private static string ParseTomlString(string token)
        {
            string trimmed = token.Trim();

            if (trimmed.Length >= 2 && trimmed.StartsWith("'", StringComparison.Ordinal) && trimmed.EndsWith("'", StringComparison.Ordinal))
            {
                return trimmed.Substring(1, trimmed.Length - 2);
            }

            if (trimmed.Length >= 2 && trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
            {
                return UnescapeTomlBasicString(trimmed.Substring(1, trimmed.Length - 2));
            }

            return trimmed;
        }

        private static string UnescapeTomlBasicString(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];

                if (character != '\\' || index == value.Length - 1)
                {
                    builder.Append(character);
                    continue;
                }

                char escapeCode = value[++index];

                switch (escapeCode)
                {
                    case '\\':
                        builder.Append('\\');
                        break;
                    case '"':
                        builder.Append('"');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        builder.Append(ParseUnicodeEscape(value, ref index, 4));
                        break;
                    case 'U':
                        builder.Append(ParseUnicodeEscape(value, ref index, 8));
                        break;
                    default:
                        builder.Append(escapeCode);
                        break;
                }
            }

            return builder.ToString();
        }

        private static string ParseUnicodeEscape(string value, ref int index, int length)
        {
            if (index + length >= value.Length)
            {
                return string.Empty;
            }

            string hex = value.Substring(index + 1, length);
            index += length;

            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint))
            {
                return string.Empty;
            }

            return char.ConvertFromUtf32(codePoint);
        }

        private static HashSet<string> ParseTomlStringSet(string value, string key, int lineNumber)
        {
            string trimmed = value.Trim();

            if (trimmed == "[]")
            {
                return new HashSet<string>();
            }

            if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                Logger.Warning($"Expected array value for '{key}' at line {lineNumber}: {value}");
                return new HashSet<string>();
            }

            string inner = trimmed.Substring(1, trimmed.Length - 2);
            List<string> items = SplitTomlArrayItems(inner);
            return new HashSet<string>(items.Select(ParseTomlString), StringComparer.Ordinal);
        }

        private static List<string> SplitTomlArrayItems(string inner)
        {
            List<string> items = new List<string>();
            StringBuilder currentItem = new StringBuilder();
            bool inBasicString = false;
            bool inLiteralString = false;
            bool isEscaped = false;

            foreach (char character in inner)
            {
                if (isEscaped)
                {
                    currentItem.Append(character);
                    isEscaped = false;
                    continue;
                }

                if (inBasicString)
                {
                    currentItem.Append(character);

                    if (character == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (character == '"')
                    {
                        inBasicString = false;
                    }

                    continue;
                }

                if (inLiteralString)
                {
                    currentItem.Append(character);

                    if (character == '\'')
                    {
                        inLiteralString = false;
                    }

                    continue;
                }

                if (character == '"')
                {
                    inBasicString = true;
                    currentItem.Append(character);
                    continue;
                }

                if (character == '\'')
                {
                    inLiteralString = true;
                    currentItem.Append(character);
                    continue;
                }

                if (character == ',')
                {
                    string item = currentItem.ToString().Trim();

                    if (item.Length > 0)
                    {
                        items.Add(item);
                    }

                    currentItem.Clear();
                    continue;
                }

                currentItem.Append(character);
            }

            string finalItem = currentItem.ToString().Trim();

            if (finalItem.Length > 0)
            {
                items.Add(finalItem);
            }

            return items;
        }

        private sealed class ServerConfigStorageLoadResult
        {
            public ServerConfigStorageLoadResult(ServerConfig config, string loadedFromPath, bool shouldWriteNormalizedFile, string rewriteReason)
            {
                Config = config;
                LoadedFromPath = loadedFromPath;
                ShouldWriteNormalizedFile = shouldWriteNormalizedFile;
                RewriteReason = rewriteReason;
            }

            public ServerConfig Config { get; }

            public string LoadedFromPath { get; }

            public bool ShouldWriteNormalizedFile { get; set; }

            public string RewriteReason { get; set; }
        }

        private sealed class ServerConfigTomlLoadResult
        {
            public ServerConfigTomlLoadResult(ServerConfig config, bool missingKnownKeys)
            {
                Config = config;
                MissingKnownKeys = missingKnownKeys;
            }

            public ServerConfig Config { get; }

            public bool MissingKnownKeys { get; }
        }

        private sealed class TomlSectionDefinition
        {
            public TomlSectionDefinition(string name, string description, IReadOnlyList<TomlEntryDefinition> entries)
            {
                Name = name;
                Description = description;
                Entries = entries;
            }

            public string Name { get; }

            public string Description { get; }

            public IReadOnlyList<TomlEntryDefinition> Entries { get; }
        }

        private sealed class TomlEntryDefinition
        {
            public TomlEntryDefinition(string propertyName, string description)
            {
                Description = description;
                Property = typeof(ServerConfig).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                    ?? throw new InvalidOperationException($"Unknown ServerConfig property '{propertyName}'.");
                Key = Property.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? propertyName;
            }

            public string Description { get; }

            public string Key { get; }

            public PropertyInfo Property { get; }
        }
    }
}
