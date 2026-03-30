using DedicatedServerMod.API.Toml;
using DedicatedServerMod.Shared.Toml.Binding;

namespace DedicatedServerMod.API.Configuration
{
    /// <summary>
    /// Provides typed load/save orchestration for TOML-backed configuration objects.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    public sealed class TomlConfigStore<TConfig>
    {
        private readonly TomlConfigSchema<TConfig> _schema;
        private readonly TomlConfigStoreOptions<TConfig> _options;
        private readonly TomlConfigBinder<TConfig> _binder;
        private readonly TomlConfigDocumentUpdater<TConfig> _updater;

        /// <summary>
        /// Initializes a typed TOML configuration store.
        /// </summary>
        /// <param name="schema">The compiled configuration schema.</param>
        /// <param name="options">The store options.</param>
        public TomlConfigStore(TomlConfigSchema<TConfig> schema, TomlConfigStoreOptions<TConfig> options)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(_options.Path))
            {
                throw new ArgumentException("Configuration path cannot be null or whitespace.", nameof(options));
            }

            _binder = new TomlConfigBinder<TConfig>(_schema);
            _updater = new TomlConfigDocumentUpdater<TConfig>(_schema);
        }

        /// <summary>
        /// Gets the resolved file path for this store.
        /// </summary>
        public string Path => System.IO.Path.GetFullPath(_options.Path);

        /// <summary>
        /// Loads an existing configuration file.
        /// </summary>
        /// <returns>The load result.</returns>
        public TomlConfigLoadResult<TConfig> Load()
        {
            if (!File.Exists(Path))
            {
                throw new FileNotFoundException($"Configuration file was not found: {Path}", Path);
            }

            return LoadInternal(writeIfMissing: false);
        }

        /// <summary>
        /// Loads a configuration file or creates it when missing.
        /// </summary>
        /// <returns>The load result.</returns>
        public TomlConfigLoadResult<TConfig> LoadOrCreate()
        {
            return LoadInternal(writeIfMissing: true);
        }

        /// <summary>
        /// Saves a configuration object to disk.
        /// </summary>
        /// <param name="config">The configuration to save.</param>
        public void Save(TConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            TomlDocument document = File.Exists(Path)
                ? TomlParser.ParseFile(Path).Document
                : new TomlDocument();

            ApplyNormalization(config);
            _updater.Update(document, config);
            TomlWriter.WriteFile(document, Path);
        }

        private TomlConfigLoadResult<TConfig> LoadInternal(bool writeIfMissing)
        {
            bool fileExists = File.Exists(Path);
            bool wasCreated = false;
            List<TomlDiagnostic> diagnostics = new List<TomlDiagnostic>();
            TomlDocument document;

            if (fileExists)
            {
                TomlReadResult readResult = TomlParser.ParseFile(Path);
                document = readResult.Document;
                diagnostics.AddRange(readResult.Diagnostics);
            }
            else
            {
                document = new TomlDocument();
                wasCreated = true;
            }

            TConfig config = CreateInstance();
            TomlConfigBinder<TConfig>.TomlConfigBindingResult bindResult = _binder.Bind(document, config);
            bool wasNormalized = ApplyNormalization(config);
            List<TomlConfigValidationIssue> validationIssues = bindResult.ValidationIssues.ToList();
            validationIssues.AddRange(Validate(config));

            bool requiresSave = wasCreated || bindResult.MissingManagedKeys.Count > 0 || (_options.SaveOnNormalize && wasNormalized);
            if (wasCreated && writeIfMissing)
            {
                _updater.Update(document, config);
                TomlWriter.WriteFile(document, Path);
            }

            return new TomlConfigLoadResult<TConfig>(
                config,
                Path,
                wasCreated,
                wasNormalized,
                requiresSave,
                bindResult.MissingManagedKeys.ToList(),
                bindResult.UsedAliases.ToList(),
                diagnostics,
                validationIssues);
        }

        private TConfig CreateInstance()
        {
            if (_options.CreateInstance != null)
            {
                return _options.CreateInstance();
            }

            return (TConfig)Activator.CreateInstance(typeof(TConfig));
        }

        private bool ApplyNormalization(TConfig config)
        {
            _schema.Normalizer?.Invoke(config);
            _options.Normalize?.Invoke(config);
            return _schema.Normalizer != null || _options.Normalize != null;
        }

        private IEnumerable<TomlConfigValidationIssue> Validate(TConfig config)
        {
            IEnumerable<TomlConfigValidationIssue> schemaIssues = _schema.Validator?.Invoke(config) ?? Array.Empty<TomlConfigValidationIssue>();
            IEnumerable<TomlConfigValidationIssue> optionIssues = _options.Validate?.Invoke(config) ?? Array.Empty<TomlConfigValidationIssue>();
            return schemaIssues.Concat(optionIssues).Where(issue => issue != null);
        }
    }
}
