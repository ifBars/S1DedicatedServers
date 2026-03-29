using System;
using System.Collections.Generic;
using System.IO;
using DedicatedServerMod.API.Toml;
using DedicatedServerMod.Shared.Permissions;
using DedicatedServerMod.Utils;
using MelonLoader;
using MelonLoader.Utils;

namespace DedicatedServerMod.Server.Permissions
{
    /// <summary>
    /// Reads and writes the dedicated permissions file.
    /// </summary>
    internal sealed class PermissionStore
    {
        private readonly MelonLogger.Instance _logger;

        /// <summary>
        /// Initializes a new permissions store.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public PermissionStore(MelonLogger.Instance logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the full permissions file path.
        /// </summary>
        public string FilePath => Path.Combine(MelonEnvironment.UserDataDirectory, Constants.PermissionsFileName);

        /// <summary>
        /// Gets whether the permissions file already exists.
        /// </summary>
        public bool Exists => File.Exists(FilePath);

        /// <summary>
        /// Loads the permissions data from disk.
        /// </summary>
        /// <returns>The loaded data.</returns>
        public PermissionStoreData Load()
        {
            if (!Exists)
            {
                return new PermissionStoreData();
            }

            TomlReadResult readResult = TomlParser.ParseFile(FilePath);
            List<TomlDiagnostic> diagnostics = new List<TomlDiagnostic>(readResult.Diagnostics);
            PermissionStoreData data = PermissionTomlMapper.Read(readResult.Document, diagnostics);
            LogDiagnostics(diagnostics);
            return data;
        }

        /// <summary>
        /// Saves the permissions data to disk.
        /// </summary>
        /// <param name="data">The data to save.</param>
        public void Save(PermissionStoreData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            TomlDocument document;
            if (Exists)
            {
                TomlReadResult existingResult = TomlParser.ParseFile(FilePath);
                LogDiagnostics(existingResult.Diagnostics);
                document = existingResult.Document;
            }
            else
            {
                document = new TomlDocument();
            }

            PermissionTomlMapper.Write(document, data);
            TomlWriter.WriteFile(document, FilePath);
            _logger.Msg($"Permissions saved to {FilePath}");
        }

        private void LogDiagnostics(IEnumerable<TomlDiagnostic> diagnostics)
        {
            foreach (TomlDiagnostic diagnostic in diagnostics ?? Array.Empty<TomlDiagnostic>())
            {
                string location = diagnostic.LineNumber > 0 ? $" line {diagnostic.LineNumber}" : string.Empty;
                string table = string.IsNullOrWhiteSpace(diagnostic.TableName) ? "root" : diagnostic.TableName;
                string key = string.IsNullOrWhiteSpace(diagnostic.Key) ? string.Empty : $" key '{diagnostic.Key}'";
                _logger.Warning($"Permissions TOML warning in section '{table}'{key}{location}: {diagnostic.Message}");
            }
        }
    }
}
