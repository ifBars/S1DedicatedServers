using DedicatedServerMod.API.Toml;

namespace DedicatedServerMod.API.Configuration
{
    /// <summary>
    /// Configures a typed TOML configuration store.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    public sealed class TomlConfigStoreOptions<TConfig>
    {
        /// <summary>
        /// Gets or sets the configuration file path.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the instance factory used before binding persisted values.
        /// </summary>
        public Func<TConfig> CreateInstance { get; set; }

        /// <summary>
        /// Gets or sets an additional normalization delegate.
        /// </summary>
        public Action<TConfig> Normalize { get; set; }

        /// <summary>
        /// Gets or sets an additional validation delegate.
        /// </summary>
        public Func<TConfig, IEnumerable<TomlConfigValidationIssue>> Validate { get; set; }

        /// <summary>
        /// Gets or sets an optional TOML document normalization delegate applied before bind and save.
        /// Returns <see langword="true"/> when the document was modified.
        /// </summary>
        public Func<TomlDocument, bool> NormalizeDocument { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="TomlConfigStore{TConfig}.Load"/> should mark the result for save after normalization.
        /// </summary>
        public bool SaveOnNormalize { get; set; }
    }
}
