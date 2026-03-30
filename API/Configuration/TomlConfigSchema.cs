using System.Linq.Expressions;
using System.Reflection;
using DedicatedServerMod.Shared.Toml.Binding;
using Newtonsoft.Json;

namespace DedicatedServerMod.API.Configuration
{
    /// <summary>
    /// Represents an immutable TOML schema for a typed configuration object.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    public sealed class TomlConfigSchema<TConfig>
    {
        internal TomlConfigSchema(
            IReadOnlyList<string> fileHeaderComments,
            IReadOnlyList<TomlConfigSectionDefinition<TConfig>> sections,
            Action<TConfig> normalizer,
            Func<TConfig, IEnumerable<TomlConfigValidationIssue>> validator)
        {
            FileHeaderComments = fileHeaderComments ?? Array.Empty<string>();
            SectionNames = sections?.Select(section => section.Name).ToList() ?? new List<string>();
            Sections = sections ?? Array.Empty<TomlConfigSectionDefinition<TConfig>>();
            Normalizer = normalizer;
            Validator = validator;
            ManagedKeys = Sections
                .SelectMany(section => section.Options)
                .SelectMany(option => option.AllManagedKeys)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Gets the generated file header comments for the schema.
        /// </summary>
        public IReadOnlyList<string> FileHeaderComments { get; }

        /// <summary>
        /// Gets the ordered managed section names.
        /// </summary>
        public IReadOnlyList<string> SectionNames { get; }

        internal IReadOnlyList<TomlConfigSectionDefinition<TConfig>> Sections { get; }

        internal Action<TConfig> Normalizer { get; }

        internal Func<TConfig, IEnumerable<TomlConfigValidationIssue>> Validator { get; }

        internal IReadOnlyList<string> ManagedKeys { get; }
    }

    /// <summary>
    /// Provides the entry point for TOML schema builders.
    /// </summary>
    public static class TomlConfigSchemaBuilder
    {
        /// <summary>
        /// Creates a schema builder for <typeparamref name="TConfig"/>.
        /// </summary>
        /// <typeparam name="TConfig">The configuration type.</typeparam>
        /// <returns>A new schema builder.</returns>
        public static TomlConfigSchemaBuilder<TConfig> For<TConfig>()
        {
            return new TomlConfigSchemaBuilder<TConfig>();
        }
    }

    /// <summary>
    /// Builds a typed TOML configuration schema.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    public sealed class TomlConfigSchemaBuilder<TConfig>
    {
        private readonly List<string> _fileHeaderComments = new List<string>();
        private readonly List<TomlConfigSectionDefinition<TConfig>> _sections = new List<TomlConfigSectionDefinition<TConfig>>();
        private Action<TConfig> _normalizer;
        private Func<TConfig, IEnumerable<TomlConfigValidationIssue>> _validator;

        /// <summary>
        /// Adds a generated file header comment line.
        /// </summary>
        /// <param name="comment">The comment text.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigSchemaBuilder<TConfig> FileHeader(string comment)
        {
            if (comment != null)
            {
                _fileHeaderComments.Add(comment);
            }

            return this;
        }

        /// <summary>
        /// Adds generated file header comment lines.
        /// </summary>
        /// <param name="comments">The comment text.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigSchemaBuilder<TConfig> FileHeader(params string[] comments)
        {
            foreach (string comment in comments ?? Array.Empty<string>())
            {
                FileHeader(comment);
            }

            return this;
        }

        /// <summary>
        /// Registers an object-level normalization delegate.
        /// </summary>
        /// <param name="normalizer">The normalization delegate.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigSchemaBuilder<TConfig> Normalize(Action<TConfig> normalizer)
        {
            _normalizer = normalizer;
            return this;
        }

        /// <summary>
        /// Registers an object-level validation delegate.
        /// </summary>
        /// <param name="validator">The validation delegate.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigSchemaBuilder<TConfig> Validate(Func<TConfig, IEnumerable<TomlConfigValidationIssue>> validator)
        {
            _validator = validator;
            return this;
        }

        /// <summary>
        /// Adds a managed section to the schema.
        /// </summary>
        /// <param name="name">The TOML section name.</param>
        /// <param name="configure">The section configuration callback.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigSchemaBuilder<TConfig> Section(string name, Action<TomlConfigSectionBuilder<TConfig>> configure)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Section name cannot be null or whitespace.", nameof(name));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            TomlConfigSectionBuilder<TConfig> builder = new TomlConfigSectionBuilder<TConfig>(name);
            configure(builder);
            _sections.Add(builder.Build());
            return this;
        }

        /// <summary>
        /// Builds the immutable schema.
        /// </summary>
        /// <returns>The compiled schema.</returns>
        public TomlConfigSchema<TConfig> Build()
        {
            HashSet<string> managedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (TomlConfigSectionDefinition<TConfig> section in _sections)
            {
                foreach (TomlConfigOptionDefinition<TConfig> option in section.Options)
                {
                    foreach (string key in option.AllManagedKeys)
                    {
                        if (!managedKeys.Add(key))
                        {
                            throw new InvalidOperationException($"TOML key '{key}' is already managed by this schema.");
                        }
                    }
                }
            }

            return new TomlConfigSchema<TConfig>(
                _fileHeaderComments.ToList(),
                _sections.ToList(),
                _normalizer,
                _validator);
        }
    }

    /// <summary>
    /// Builds a managed TOML section.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    public sealed class TomlConfigSectionBuilder<TConfig>
    {
        private readonly string _name;
        private readonly List<string> _comments = new List<string>();
        private readonly List<TomlConfigOptionDefinition<TConfig>> _options = new List<TomlConfigOptionDefinition<TConfig>>();

        internal TomlConfigSectionBuilder(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Adds a generated comment to the section.
        /// </summary>
        /// <param name="comment">The comment text.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigSectionBuilder<TConfig> Comment(string comment)
        {
            if (comment != null)
            {
                _comments.Add(comment);
            }

            return this;
        }

        /// <summary>
        /// Adds generated comments to the section.
        /// </summary>
        /// <param name="comments">The comment text.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigSectionBuilder<TConfig> Comments(params string[] comments)
        {
            foreach (string comment in comments ?? Array.Empty<string>())
            {
                Comment(comment);
            }

            return this;
        }

        /// <summary>
        /// Adds a managed option to the section.
        /// </summary>
        /// <typeparam name="TValue">The option value type.</typeparam>
        /// <param name="propertyExpression">The bound property expression.</param>
        /// <param name="configure">The option configuration callback.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigSectionBuilder<TConfig> Option<TValue>(
            Expression<Func<TConfig, TValue>> propertyExpression,
            Action<TomlConfigOptionBuilder<TConfig, TValue>> configure = null)
        {
            TomlConfigOptionBuilder<TConfig, TValue> builder = new TomlConfigOptionBuilder<TConfig, TValue>(GetPropertyInfo(propertyExpression));
            configure?.Invoke(builder);
            _options.Add(builder.Build());
            return this;
        }

        internal TomlConfigSectionDefinition<TConfig> Build()
        {
            return new TomlConfigSectionDefinition<TConfig>(_name, _comments.ToList(), _options.ToList());
        }

        private static PropertyInfo GetPropertyInfo<TValue>(Expression<Func<TConfig, TValue>> propertyExpression)
        {
            if (propertyExpression == null)
            {
                throw new ArgumentNullException(nameof(propertyExpression));
            }

            if (!(propertyExpression.Body is MemberExpression memberExpression) || !(memberExpression.Member is PropertyInfo propertyInfo))
            {
                throw new ArgumentException("Option expressions must reference a readable and writable instance property.", nameof(propertyExpression));
            }

            if (!propertyInfo.CanRead || !propertyInfo.CanWrite)
            {
                throw new InvalidOperationException($"Property '{propertyInfo.Name}' must be readable and writable.");
            }

            return propertyInfo;
        }
    }

    /// <summary>
    /// Builds a managed TOML option binding.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    /// <typeparam name="TValue">The option value type.</typeparam>
    public sealed class TomlConfigOptionBuilder<TConfig, TValue>
    {
        private readonly PropertyInfo _propertyInfo;
        private readonly List<string> _aliases = new List<string>();
        private readonly List<string> _comments = new List<string>();
        private string _key;
        private bool _required;
        private bool _hasDefaultValue;
        private TValue _defaultValue;
        private Func<TValue, string> _validator;

        internal TomlConfigOptionBuilder(PropertyInfo propertyInfo)
        {
            _propertyInfo = propertyInfo ?? throw new ArgumentNullException(nameof(propertyInfo));
            _key = ResolveDefaultKey(propertyInfo);
        }

        /// <summary>
        /// Overrides the TOML key used for this property.
        /// </summary>
        /// <param name="key">The canonical TOML key.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigOptionBuilder<TConfig, TValue> Key(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Option key cannot be null or whitespace.", nameof(key));
            }

            _key = key.Trim();
            return this;
        }

        /// <summary>
        /// Adds an alias key that will be accepted during load.
        /// </summary>
        /// <param name="alias">The alias key.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigOptionBuilder<TConfig, TValue> Alias(string alias)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                _aliases.Add(alias.Trim());
            }

            return this;
        }

        /// <summary>
        /// Adds a generated comment for this option.
        /// </summary>
        /// <param name="comment">The comment text.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigOptionBuilder<TConfig, TValue> Comment(string comment)
        {
            if (comment != null)
            {
                _comments.Add(comment);
            }

            return this;
        }

        /// <summary>
        /// Adds generated comments for this option.
        /// </summary>
        /// <param name="comments">The comment text.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigOptionBuilder<TConfig, TValue> Comments(params string[] comments)
        {
            foreach (string comment in comments ?? Array.Empty<string>())
            {
                Comment(comment);
            }

            return this;
        }

        /// <summary>
        /// Marks the option as required.
        /// </summary>
        /// <returns>The current builder.</returns>
        public TomlConfigOptionBuilder<TConfig, TValue> Required()
        {
            _required = true;
            return this;
        }

        /// <summary>
        /// Sets an explicit default value used when the key is missing.
        /// </summary>
        /// <param name="value">The default value.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigOptionBuilder<TConfig, TValue> Default(TValue value)
        {
            _defaultValue = value;
            _hasDefaultValue = true;
            return this;
        }

        /// <summary>
        /// Registers a per-value validation delegate.
        /// </summary>
        /// <param name="validator">Returns an error message, or null/empty when valid.</param>
        /// <returns>The current builder.</returns>
        public TomlConfigOptionBuilder<TConfig, TValue> Validate(Func<TValue, string> validator)
        {
            _validator = validator;
            return this;
        }

        internal TomlConfigOptionDefinition<TConfig> Build()
        {
            TomlPropertyAccessor accessor = new TomlPropertyAccessor(_propertyInfo);
            Func<object, string> validator = null;
            if (_validator != null)
            {
                validator = value => _validator(value is TValue typedValue ? typedValue : default);
            }

            List<string> aliases = _aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.Ordinal)
                .Where(alias => !string.Equals(alias, _key, StringComparison.Ordinal))
                .ToList();

            return new TomlConfigOptionDefinition<TConfig>(
                accessor,
                _key,
                aliases,
                _comments.ToList(),
                _required,
                _hasDefaultValue,
                _defaultValue,
                validator);
        }

        private static string ResolveDefaultKey(PropertyInfo propertyInfo)
        {
            JsonPropertyAttribute attribute = propertyInfo.GetCustomAttributes(typeof(JsonPropertyAttribute), inherit: true)
                .OfType<JsonPropertyAttribute>()
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(attribute?.PropertyName))
            {
                return attribute.PropertyName;
            }

            return propertyInfo.Name;
        }
    }

    internal sealed class TomlConfigSectionDefinition<TConfig>(
        string name,
        IReadOnlyList<string> comments,
        IReadOnlyList<TomlConfigOptionDefinition<TConfig>> options)
    {
        public string Name { get; } = name;

        public IReadOnlyList<string> Comments { get; } = comments ?? Array.Empty<string>();

        public IReadOnlyList<TomlConfigOptionDefinition<TConfig>> Options { get; } = options ?? Array.Empty<TomlConfigOptionDefinition<TConfig>>();
    }

    internal sealed class TomlConfigOptionDefinition<TConfig>
    {
        public TomlConfigOptionDefinition(
            TomlPropertyAccessor accessor,
            string key,
            IReadOnlyList<string> aliases,
            IReadOnlyList<string> comments,
            bool required,
            bool hasDefaultValue,
            object defaultValue,
            Func<object, string> validator)
        {
            Accessor = accessor;
            Key = key;
            Aliases = aliases ?? Array.Empty<string>();
            Comments = comments ?? Array.Empty<string>();
            Required = required;
            HasDefaultValue = hasDefaultValue;
            DefaultValue = defaultValue;
            Validator = validator;
            AllManagedKeys = new[] { Key }.Concat(Aliases).Distinct(StringComparer.Ordinal).ToList();
        }

        public TomlPropertyAccessor Accessor { get; }

        public string Key { get; }

        public IReadOnlyList<string> Aliases { get; }

        public IReadOnlyList<string> Comments { get; }

        public bool Required { get; }

        public bool HasDefaultValue { get; }

        public object DefaultValue { get; }

        public Func<object, string> Validator { get; }

        public IReadOnlyList<string> AllManagedKeys { get; }
    }
}
