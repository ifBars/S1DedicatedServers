using DedicatedServerMod.API.Configuration;
using DedicatedServerMod.API.Toml;

namespace DedicatedServerMod.Shared.Toml.Binding
{
    /// <summary>
    /// Binds typed configuration schemas from TOML documents.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    internal sealed class TomlConfigBinder<TConfig>(TomlConfigSchema<TConfig> schema)
    {
        private readonly TomlConfigSchema<TConfig> _schema = schema ?? throw new ArgumentNullException(nameof(schema));

        public TomlConfigBindingResult Bind(TomlDocument document, TConfig config)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            TomlConfigBindingResult result = new TomlConfigBindingResult();

            foreach (TomlConfigSectionDefinition<TConfig> section in _schema.Sections)
            {
                foreach (TomlConfigOptionDefinition<TConfig> option in section.Options)
                {
                    if (!TryFindEntry(document, section, option, out TomlEntry entry, out string matchedKey))
                    {
                        if (option.HasDefaultValue)
                        {
                            option.Accessor.SetValue(config, option.DefaultValue);
                        }

                        result.MissingManagedKeys.Add(option.Key);
                        if (option.Required)
                        {
                            result.ValidationIssues.Add(new TomlConfigValidationIssue(section.Name, option.Key, $"Required key '{option.Key}' is missing."));
                        }

                        continue;
                    }

                    if (!TomlValueCodec.TryConvertToClr(entry.Value, option.Accessor.DeclaredType, out object convertedValue, out string error))
                    {
                        result.ValidationIssues.Add(new TomlConfigValidationIssue(section.Name, option.Key, string.IsNullOrWhiteSpace(error)
                            ? $"Key '{option.Key}' could not be converted to '{option.Accessor.DeclaredType.Name}'."
                            : error));
                        continue;
                    }

                    option.Accessor.SetValue(config, convertedValue);
                    if (!string.Equals(matchedKey, option.Key, StringComparison.Ordinal))
                    {
                        result.UsedAliases.Add(matchedKey);
                    }

                    if (option.Validator != null)
                    {
                        string validationMessage = option.Validator(convertedValue);
                        if (!string.IsNullOrWhiteSpace(validationMessage))
                        {
                            result.ValidationIssues.Add(new TomlConfigValidationIssue(section.Name, option.Key, validationMessage));
                        }
                    }
                }
            }

            return result;
        }

        private static bool TryFindEntry(
            TomlDocument document,
            TomlConfigSectionDefinition<TConfig> section,
            TomlConfigOptionDefinition<TConfig> option,
            out TomlEntry entry,
            out string matchedKey)
        {
            TomlTable expectedTable = document.GetTable(section.Name);
            if (TryFindEntryInTable(expectedTable, option, out entry, out matchedKey))
            {
                return true;
            }

            if (TryFindEntryInTable(document.Root, option, out entry, out matchedKey))
            {
                return true;
            }

            foreach (TomlTable table in document.Tables)
            {
                if (ReferenceEquals(table, expectedTable))
                {
                    continue;
                }

                if (TryFindEntryInTable(table, option, out entry, out matchedKey))
                {
                    return true;
                }
            }

            entry = null;
            matchedKey = string.Empty;
            return false;
        }

        private static bool TryFindEntryInTable(
            TomlTable table,
            TomlConfigOptionDefinition<TConfig> option,
            out TomlEntry entry,
            out string matchedKey)
        {
            if (table != null)
            {
                foreach (string key in option.AllManagedKeys)
                {
                    entry = table.GetEntry(key);
                    if (entry != null)
                    {
                        matchedKey = key;
                        return true;
                    }
                }
            }

            entry = null;
            matchedKey = string.Empty;
            return false;
        }

        internal sealed class TomlConfigBindingResult
        {
            public List<string> MissingManagedKeys { get; } = new List<string>();

            public List<string> UsedAliases { get; } = new List<string>();

            public List<TomlConfigValidationIssue> ValidationIssues { get; } = new List<TomlConfigValidationIssue>();
        }
    }
}
