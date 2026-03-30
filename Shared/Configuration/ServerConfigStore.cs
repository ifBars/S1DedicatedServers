using System;
using System.IO;
using DedicatedServerMod.API.Configuration;
using DedicatedServerMod.API.Toml;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DedicatedServerMod.Shared.Configuration
{
    /// <summary>
    /// Provides server-specific configuration storage orchestration.
    /// </summary>
    internal sealed class ServerConfigStore
    {
        private const string LegacyPermissionsSectionName = "permissions";

        private readonly MelonLogger.Instance _logger;
        private readonly string _configPath;
        private readonly bool _configPathWasExplicit;

        public ServerConfigStore(MelonLogger.Instance logger, string configPath, bool configPathWasExplicit)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configPath = Path.GetFullPath(configPath ?? throw new ArgumentNullException(nameof(configPath)));
            _configPathWasExplicit = configPathWasExplicit;
        }

        public string ConfigFilePath => _configPath;

        public string LegacyCompatibilityConfigFilePath => Path.Combine(MelonEnvironment.UserDataDirectory, Utils.Constants.LegacyConfigFileName);

        public ServerConfigStoreLoadResult Load()
        {
            if (_configPathWasExplicit)
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
                    shouldWriteNormalizedFile: _configPathWasExplicit && needsRewrite,
                    rewriteReason: _configPathWasExplicit && needsRewrite
                        ? "Server configuration updated with normalized and newly added options."
                        : null);
            }

            bool containsLegacyPermissionsSection = ContainsLegacyPermissionsSection(path);
            TomlConfigLoadResult<ServerConfig> loadResult = CreateTomlStore(path).Load();
            LogDiagnostics(loadResult);

            bool shouldWriteNormalizedFile = loadResult.MissingManagedKeys.Count > 0 || containsLegacyPermissionsSection;
            string rewriteReason = null;
            if (containsLegacyPermissionsSection)
            {
                rewriteReason = "Server configuration removed the deprecated permissions section. Permissions now belong in permissions.toml.";
            }
            else if (loadResult.MissingManagedKeys.Count > 0)
            {
                rewriteReason = "Server configuration updated with newly added options.";
            }

            return new ServerConfigStoreLoadResult(
                loadResult.Config ?? new ServerConfig(),
                path,
                shouldWriteNormalizedFile: shouldWriteNormalizedFile,
                rewriteReason: rewriteReason);
        }

        private TomlConfigStore<ServerConfig> CreateTomlStore(string path)
        {
            return new TomlConfigStore<ServerConfig>(
                ServerConfigSchema.Instance,
                new TomlConfigStoreOptions<ServerConfig>
                {
                    Path = path,
                    CreateInstance = () => new ServerConfig()
                });
        }

        private void LogDiagnostics(TomlConfigLoadResult<ServerConfig> loadResult)
        {
            foreach (TomlDiagnostic diagnostic in loadResult.Diagnostics)
            {
                string location = diagnostic.LineNumber > 0 ? $" line {diagnostic.LineNumber}" : string.Empty;
                string sectionLabel = string.IsNullOrWhiteSpace(diagnostic.TableName) ? "root" : diagnostic.TableName;
                string keyLabel = string.IsNullOrWhiteSpace(diagnostic.Key) ? string.Empty : $" key '{diagnostic.Key}'";
                _logger.Warning($"Config warning in section '{sectionLabel}'{keyLabel}{location}: {diagnostic.Message}");
            }

            foreach (TomlConfigValidationIssue issue in loadResult.ValidationIssues)
            {
                string sectionLabel = string.IsNullOrWhiteSpace(issue.Section) ? "root" : issue.Section;
                string keyLabel = string.IsNullOrWhiteSpace(issue.Key) ? string.Empty : $" key '{issue.Key}'";
                _logger.Warning($"Config binding warning in section '{sectionLabel}'{keyLabel}: {issue.Message}");
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

        internal sealed class ServerConfigStoreLoadResult
        {
            public ServerConfigStoreLoadResult(ServerConfig config, string loadedFromPath, bool shouldWriteNormalizedFile, string rewriteReason)
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
    }
}
