using System;
using System.Collections.Generic;
using System.IO;
using DedicatedServerMod.API.Toml;
using DedicatedServerMod.Utils;
using MelonLoader;
using MelonLoader.Utils;

namespace DedicatedServerMod.Shared.ModVerification
{
    /// <summary>
    /// Reads and writes the dedicated client mod policy file.
    /// </summary>
    internal sealed class ClientModPolicyStore
    {
        private readonly MelonLogger.Instance _logger;

        /// <summary>
        /// Initializes a new client mod policy store.
        /// </summary>
        /// <param name="logger">The logger to use for diagnostics.</param>
        public ClientModPolicyStore(MelonLogger.Instance logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the full client mod policy file path.
        /// </summary>
        public string FilePath => Path.Combine(MelonEnvironment.UserDataDirectory, Constants.ClientModPolicyFileName);

        /// <summary>
        /// Gets whether the policy file exists on disk.
        /// </summary>
        public bool Exists => File.Exists(FilePath);

        /// <summary>
        /// Loads the client mod policy from disk.
        /// </summary>
        /// <returns>The loaded client mod policy.</returns>
        public ClientModPolicy Load()
        {
            if (!Exists)
            {
                return new ClientModPolicy();
            }

            TomlReadResult readResult = TomlParser.ParseFile(FilePath);
            List<TomlDiagnostic> diagnostics = new List<TomlDiagnostic>(readResult.Diagnostics);
            ClientModPolicy policy = ClientModPolicyTomlMapper.Read(readResult.Document, diagnostics);
            policy.Normalize();
            LogDiagnostics(diagnostics);
            return policy;
        }

        /// <summary>
        /// Saves the client mod policy to disk.
        /// </summary>
        /// <param name="policy">The policy to save.</param>
        public void Save(ClientModPolicy policy)
        {
            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            policy.Normalize();

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

            ClientModPolicyTomlMapper.Write(document, policy);
            TomlWriter.WriteFile(document, FilePath);
            _logger.Msg($"Client mod policy saved to {FilePath}");
        }

        private void LogDiagnostics(IEnumerable<TomlDiagnostic> diagnostics)
        {
            foreach (TomlDiagnostic diagnostic in diagnostics ?? Array.Empty<TomlDiagnostic>())
            {
                string location = diagnostic.LineNumber > 0 ? $" line {diagnostic.LineNumber}" : string.Empty;
                string table = string.IsNullOrWhiteSpace(diagnostic.TableName) ? "root" : diagnostic.TableName;
                string key = string.IsNullOrWhiteSpace(diagnostic.Key) ? string.Empty : $" key '{diagnostic.Key}'";
                _logger.Warning($"Client mod policy TOML warning in section '{table}'{key}{location}: {diagnostic.Message}");
            }
        }
    }
}
