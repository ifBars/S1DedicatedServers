using DedicatedServerMod.API.Configuration;
using DedicatedServerMod.API.Toml;
using DedicatedServerMod.Utils;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Provides server-specific configuration storage orchestration.
    /// </summary>
    internal sealed class ServerConfigStore(string configPath, bool configPathWasExplicit)
    {
        private const string LegacyPermissionsSectionName = "permissions";
        private const string LoggingSectionName = "logging";

        private static readonly string[] LegacyLoggingOptionKeys =
        {
            Utils.Constants.ConfigKeys.DebugMode,
            Utils.Constants.ConfigKeys.VerboseLogging,
            "logPlayerActions",
            "logAdminCommands",
            Utils.Constants.ConfigKeys.LogNetworkingDebug,
            Utils.Constants.ConfigKeys.LogMessageRoutingDebug,
            Utils.Constants.ConfigKeys.LogMessagingBackendDebug,
            Utils.Constants.ConfigKeys.LogStartupDebug,
            Utils.Constants.ConfigKeys.LogServerNetworkDebug,
            Utils.Constants.ConfigKeys.LogPlayerLifecycleDebug,
            Utils.Constants.ConfigKeys.LogAuthenticationDebug,
            nameof(ServerConfig.DebugMode),
            nameof(ServerConfig.VerboseLogging),
            nameof(ServerConfig.LogPlayerActions),
            nameof(ServerConfig.LogAdminCommands),
            nameof(ServerConfig.LogNetworkingDebug),
            nameof(ServerConfig.LogMessageRoutingDebug),
            nameof(ServerConfig.LogMessagingBackendDebug),
            nameof(ServerConfig.LogStartupDebug),
            nameof(ServerConfig.LogServerNetworkDebug),
            nameof(ServerConfig.LogPlayerLifecycleDebug),
            nameof(ServerConfig.LogAuthenticationDebug)
        };

        private readonly string _configPath = Path.GetFullPath(configPath ?? throw new ArgumentNullException(nameof(configPath)));

        public string ConfigFilePath => _configPath;

        public string LegacyCompatibilityConfigFilePath => Path.Combine(MelonEnvironment.UserDataDirectory, Utils.Constants.LegacyConfigFileName);

        public ServerConfigStoreLoadResult Load()
        {
            if (configPathWasExplicit)
            {
                if (File.Exists(_configPath))
                {
                    return LoadFromPath(_configPath);
                }

                return new ServerConfigStoreLoadResult(
                    new ServerConfig(),
                    _configPath,
                    shouldWriteNormalizedFile: true,
                    rewriteReason: "Created new server configuration file");
            }

            if (File.Exists(_configPath))
            {
                return LoadFromPath(_configPath);
            }

            if (File.Exists(LegacyCompatibilityConfigFilePath))
            {
                ServerConfigStoreLoadResult legacyLoadResult = LoadFromPath(LegacyCompatibilityConfigFilePath);
                legacyLoadResult.ShouldWriteNormalizedFile = true;
                legacyLoadResult.RewriteReason = $"Migrated legacy JSON config from {LegacyCompatibilityConfigFilePath} to {_configPath}.";
                return legacyLoadResult;
            }

            return new ServerConfigStoreLoadResult(
                new ServerConfig(),
                _configPath,
                shouldWriteNormalizedFile: true,
                rewriteReason: "Created new server configuration file");
        }

        public ServerConfig LoadSnapshot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Configuration path cannot be null or empty.", nameof(path));
            }

            return LoadFromPath(Path.GetFullPath(path)).Config ?? new ServerConfig();
        }

        public void Save(ServerConfig config)
        {
            string targetPath = _configPath;
            string directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (IsJsonConfigPath(targetPath))
            {
                File.WriteAllText(targetPath, JsonConvert.SerializeObject(config ?? new ServerConfig(), Formatting.Indented));
                return;
            }

            TomlConfigStore<ServerConfig> store = CreateTomlStore(targetPath);
            store.Save(config ?? new ServerConfig());
            RemoveLegacyPermissionsSection(targetPath);
        }

        private ServerConfigStoreLoadResult LoadFromPath(string path)
        {
            if (IsJsonConfigPath(path))
            {
                string configText = File.ReadAllText(path);
                ServerConfig config = JsonConvert.DeserializeObject<ServerConfig>(configText) ?? new ServerConfig();
                string normalizedJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                bool needsRewrite = !AreEquivalentJsonDocuments(configText, normalizedJson);

                return new ServerConfigStoreLoadResult(
                    config,
                    path,
                    shouldWriteNormalizedFile: configPathWasExplicit && needsRewrite,
                    rewriteReason: configPathWasExplicit && needsRewrite
                        ? "Server configuration updated with normalized and newly added options."
                        : null);
            }

            bool containsLegacyPermissionsSection = ContainsLegacyPermissionsSection(path);
            bool containsLegacyLoggingEntries = ContainsLegacyLoggingEntries(path);
            TomlConfigLoadResult<ServerConfig> loadResult = CreateTomlStore(path).Load();
            LogDiagnostics(loadResult);

            bool shouldWriteNormalizedFile = loadResult.RequiresSave || containsLegacyPermissionsSection;
            List<string> rewriteReasons = new List<string>();
            if (containsLegacyPermissionsSection)
            {
                rewriteReasons.Add("Server configuration removed the deprecated permissions section. Permissions now belong in permissions.toml.");
            }

            if (containsLegacyLoggingEntries)
            {
                rewriteReasons.Add($"Server configuration compacted legacy logging flags into '{Utils.Constants.ConfigKeys.EnabledLoggingOptions}'.");
            }

            if (loadResult.MissingManagedKeys.Count > 0)
            {
                rewriteReasons.Add("Server configuration updated with newly added options.");
            }

            return new ServerConfigStoreLoadResult(
                loadResult.Config ?? new ServerConfig(),
                path,
                shouldWriteNormalizedFile: shouldWriteNormalizedFile,
                rewriteReason: rewriteReasons.Count == 0 ? null : string.Join(" ", rewriteReasons));
        }

        private TomlConfigStore<ServerConfig> CreateTomlStore(string path)
        {
            return new TomlConfigStore<ServerConfig>(
                ServerConfigSchema.Instance,
                new TomlConfigStoreOptions<ServerConfig>
                {
                    Path = path,
                    CreateInstance = () => new ServerConfig(),
                    NormalizeDocument = NormalizeTomlDocument
                });
        }

        private void LogDiagnostics(TomlConfigLoadResult<ServerConfig> loadResult)
        {
            foreach (TomlDiagnostic diagnostic in loadResult.Diagnostics)
            {
                string location = diagnostic.LineNumber > 0 ? $" line {diagnostic.LineNumber}" : string.Empty;
                string sectionLabel = string.IsNullOrWhiteSpace(diagnostic.TableName) ? "root" : diagnostic.TableName;
                string keyLabel = string.IsNullOrWhiteSpace(diagnostic.Key) ? string.Empty : $" key '{diagnostic.Key}'";
                DebugLog.Warning($"Config warning in section '{sectionLabel}'{keyLabel}{location}: {diagnostic.Message}");
            }

            foreach (TomlConfigValidationIssue issue in loadResult.ValidationIssues)
            {
                string sectionLabel = string.IsNullOrWhiteSpace(issue.Section) ? "root" : issue.Section;
                string keyLabel = string.IsNullOrWhiteSpace(issue.Key) ? string.Empty : $" key '{issue.Key}'";
                DebugLog.Warning($"Config binding warning in section '{sectionLabel}'{keyLabel}: {issue.Message}");
            }
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

        private static bool ContainsLegacyPermissionsSection(string path)
        {
            if (IsJsonConfigPath(path) || !File.Exists(path))
            {
                return false;
            }

            TomlReadResult readResult = TomlParser.ParseFile(path);
            return readResult.Document.GetTable(LegacyPermissionsSectionName) != null;
        }

        private static bool ContainsLegacyLoggingEntries(string path)
        {
            if (IsJsonConfigPath(path) || !File.Exists(path))
            {
                return false;
            }

            TomlReadResult readResult = TomlParser.ParseFile(path);
            return EnumerateTables(readResult.Document)
                .Any(table => LegacyLoggingOptionKeys.Any(table.ContainsKey));
        }

        private static void RemoveLegacyPermissionsSection(string path)
        {
            if (IsJsonConfigPath(path) || !File.Exists(path))
            {
                return;
            }

            TomlReadResult readResult = TomlParser.ParseFile(path);
            if (!readResult.Document.RemoveTable(LegacyPermissionsSectionName))
            {
                return;
            }

            TomlWriter.WriteFile(readResult.Document, path);
        }

        private static bool NormalizeTomlDocument(TomlDocument document)
        {
            if (document == null)
            {
                return false;
            }

            if (HasCanonicalLoggingOptions(document))
            {
                return RemoveLegacyLoggingEntries(document);
            }

            HashSet<string> enabledLoggingOptions = new HashSet<string>(
                new ServerConfig().EnabledLoggingOptions ?? new List<string>(),
                StringComparer.Ordinal);

            bool foundLegacyLoggingOption = false;
            foreach (TomlTable table in EnumerateTables(document))
            {
                foreach (string key in LegacyLoggingOptionKeys)
                {
                    if (!table.TryGetBoolean(key, out bool isEnabled))
                    {
                        continue;
                    }

                    foundLegacyLoggingOption = true;
                    if (isEnabled)
                    {
                        enabledLoggingOptions.Add(key);
                    }
                    else
                    {
                        enabledLoggingOptions.Remove(key);
                    }
                }
            }

            if (!foundLegacyLoggingOption)
            {
                return false;
            }

            TomlTable loggingTable = document.GetOrAddTable(LoggingSectionName);
            loggingTable.Set(
                Utils.Constants.ConfigKeys.EnabledLoggingOptions,
                TomlValue.FromArray(GetOrderedLoggingOptions(enabledLoggingOptions).Select(TomlValue.FromString)));

            RemoveLegacyLoggingEntries(document);
            return true;
        }

        private static bool HasCanonicalLoggingOptions(TomlDocument document)
        {
            return EnumerateTables(document)
                .Any(table => table.ContainsKey(Utils.Constants.ConfigKeys.EnabledLoggingOptions));
        }

        private static bool RemoveLegacyLoggingEntries(TomlDocument document)
        {
            bool removed = false;
            foreach (TomlTable table in EnumerateTables(document))
            {
                foreach (string key in LegacyLoggingOptionKeys)
                {
                    removed |= table.Remove(key);
                }
            }

            return removed;
        }

        private static IEnumerable<TomlTable> EnumerateTables(TomlDocument document)
        {
            yield return document.Root;

            foreach (TomlTable table in document.Tables)
            {
                yield return table;
            }
        }

        private static IEnumerable<string> GetOrderedLoggingOptions(HashSet<string> enabledLoggingOptions)
        {
            foreach (string key in LegacyLoggingOptionKeys)
            {
                if (enabledLoggingOptions.Contains(key))
                {
                    yield return key;
                }
            }
        }

        internal sealed class ServerConfigStoreLoadResult(
            ServerConfig config,
            string loadedFromPath,
            bool shouldWriteNormalizedFile,
            string rewriteReason)
        {
            public ServerConfig Config { get; } = config;

            public string LoadedFromPath { get; } = loadedFromPath;

            public bool ShouldWriteNormalizedFile { get; set; } = shouldWriteNormalizedFile;

            public string RewriteReason { get; set; } = rewriteReason;
        }
    }
}
