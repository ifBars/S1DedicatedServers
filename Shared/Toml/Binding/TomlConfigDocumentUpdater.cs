using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod.API.Configuration;
using DedicatedServerMod.API.Toml;

namespace DedicatedServerMod.Shared.Toml.Binding
{
    /// <summary>
    /// Synchronizes typed configuration schemas into TOML documents.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    internal sealed class TomlConfigDocumentUpdater<TConfig>
    {
        private readonly TomlConfigSchema<TConfig> _schema;

        public TomlConfigDocumentUpdater(TomlConfigSchema<TConfig> schema)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public void Update(TomlDocument document, TConfig config)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (_schema.FileHeaderComments.Count > 0)
            {
                TomlCommentFormatter.Replace(document.FileHeaderComments, _schema.FileHeaderComments);
            }

            RemoveManagedEntries(document);

            int managedTableIndex = 0;
            foreach (TomlConfigSectionDefinition<TConfig> section in _schema.Sections)
            {
                TomlTable table = document.GetOrAddTable(section.Name);
                document.MoveTableToIndex(section.Name, managedTableIndex++);
                TomlCommentFormatter.Replace(table.Comments, section.Comments);

                for (int optionIndex = 0; optionIndex < section.Options.Count; optionIndex++)
                {
                    TomlConfigOptionDefinition<TConfig> option = section.Options[optionIndex];
                    if (!TomlValueCodec.TryConvertFromClr(option.Accessor.GetValue(config), option.Accessor.DeclaredType, out TomlValue value, out string error))
                    {
                        throw new InvalidOperationException($"Failed to serialize TOML option '{option.Key}': {error}");
                    }

                    TomlEntry entry = table.Set(option.Key, value);
                    TomlCommentFormatter.Replace(entry.Comments, option.Comments);
                    table.MoveEntryToIndex(option.Key, optionIndex);
                }
            }
        }

        private void RemoveManagedEntries(TomlDocument document)
        {
            RemoveManagedEntries(document.Root);
            foreach (TomlTable table in document.Tables.ToList())
            {
                RemoveManagedEntries(table);
            }
        }

        private void RemoveManagedEntries(TomlTable table)
        {
            foreach (string key in _schema.ManagedKeys)
            {
                table.Remove(key);
            }
        }
    }
}
