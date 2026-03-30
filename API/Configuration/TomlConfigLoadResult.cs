using DedicatedServerMod.API.Toml;

namespace DedicatedServerMod.API.Configuration
{
    /// <summary>
    /// Represents the result of loading a typed TOML configuration file.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    public sealed class TomlConfigLoadResult<TConfig>
    {
        internal TomlConfigLoadResult(
            TConfig config,
            string path,
            bool wasCreated,
            bool wasNormalized,
            bool requiresSave,
            IReadOnlyList<string> missingManagedKeys,
            IReadOnlyList<string> usedAliases,
            IReadOnlyList<TomlDiagnostic> diagnostics,
            IReadOnlyList<TomlConfigValidationIssue> validationIssues)
        {
            Config = config;
            Path = path ?? string.Empty;
            WasCreated = wasCreated;
            WasNormalized = wasNormalized;
            RequiresSave = requiresSave;
            MissingManagedKeys = missingManagedKeys ?? Array.Empty<string>();
            UsedAliases = usedAliases ?? Array.Empty<string>();
            Diagnostics = diagnostics ?? Array.Empty<TomlDiagnostic>();
            ValidationIssues = validationIssues ?? Array.Empty<TomlConfigValidationIssue>();
        }

        /// <summary>
        /// Gets the loaded configuration object.
        /// </summary>
        public TConfig Config { get; }

        /// <summary>
        /// Gets the file path used for the load operation.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets whether the file had to be created.
        /// </summary>
        public bool WasCreated { get; }

        /// <summary>
        /// Gets whether configured normalization ran.
        /// </summary>
        public bool WasNormalized { get; }

        /// <summary>
        /// Gets whether the document should be saved to persist managed changes.
        /// </summary>
        public bool RequiresSave { get; }

        /// <summary>
        /// Gets the canonical managed keys that were missing from the source document.
        /// </summary>
        public IReadOnlyList<string> MissingManagedKeys { get; }

        /// <summary>
        /// Gets the alias keys that were used during binding.
        /// </summary>
        public IReadOnlyList<string> UsedAliases { get; }

        /// <summary>
        /// Gets parse diagnostics collected during load.
        /// </summary>
        public IReadOnlyList<TomlDiagnostic> Diagnostics { get; }

        /// <summary>
        /// Gets binding or validation issues collected during load.
        /// </summary>
        public IReadOnlyList<TomlConfigValidationIssue> ValidationIssues { get; }
    }
}
