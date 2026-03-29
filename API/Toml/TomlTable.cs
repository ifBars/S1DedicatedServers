using System;
using System.Collections.Generic;
using System.Linq;

namespace DedicatedServerMod.API.Toml
{
    /// <summary>
    /// Represents a TOML table containing ordered key/value entries.
    /// </summary>
    public sealed class TomlTable
    {
        private readonly List<TomlEntry> _entries = new List<TomlEntry>();
        private readonly Dictionary<string, TomlEntry> _entriesByKey = new Dictionary<string, TomlEntry>(StringComparer.Ordinal);

        internal TomlTable(string name, bool isRoot)
        {
            Name = name ?? string.Empty;
            IsRoot = isRoot;
            Comments = new List<string>();
        }

        /// <summary>
        /// Gets the table name. The root table is represented by an empty string.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the leading comments for the table header.
        /// </summary>
        public IList<string> Comments { get; }

        /// <summary>
        /// Gets whether this is the root table.
        /// </summary>
        public bool IsRoot { get; }

        /// <summary>
        /// Gets the ordered entries in the table.
        /// </summary>
        public IReadOnlyList<TomlEntry> Entries => _entries;

        /// <summary>
        /// Gets the entry for a key when present.
        /// </summary>
        public TomlEntry GetEntry(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _entriesByKey.TryGetValue(key, out TomlEntry entry)
                ? entry
                : null;
        }

        /// <summary>
        /// Gets whether the table contains a key.
        /// </summary>
        public bool ContainsKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _entriesByKey.ContainsKey(key);
        }

        /// <summary>
        /// Sets an entry value in the table.
        /// </summary>
        public TomlEntry Set(string key, TomlValue value, IEnumerable<string> comments = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Table entry key cannot be null or whitespace.", nameof(key));
            }

            if (!_entriesByKey.TryGetValue(key, out TomlEntry entry))
            {
                entry = new TomlEntry(key, value ?? throw new ArgumentNullException(nameof(value)));
                _entries.Add(entry);
                _entriesByKey[key] = entry;
            }
            else
            {
                entry.Value = value ?? throw new ArgumentNullException(nameof(value));
            }

            if (comments != null)
            {
                entry.Comments.Clear();
                foreach (string comment in comments.Where(line => line != null))
                {
                    entry.Comments.Add(comment);
                }
            }

            return entry;
        }

        /// <summary>
        /// Removes an entry from the table.
        /// </summary>
        public bool Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !_entriesByKey.TryGetValue(key, out TomlEntry entry))
            {
                return false;
            }

            _entriesByKey.Remove(key);
            return _entries.Remove(entry);
        }

        /// <summary>
        /// Attempts to read a string entry.
        /// </summary>
        public bool TryGetString(string key, out string value)
        {
            TomlEntry entry = GetEntry(key);
            if (entry?.Value != null && entry.Value.TryGetString(out value))
            {
                return true;
            }

            value = string.Empty;
            return false;
        }

        /// <summary>
        /// Attempts to read a boolean entry.
        /// </summary>
        public bool TryGetBoolean(string key, out bool value)
        {
            TomlEntry entry = GetEntry(key);
            if (entry?.Value != null && entry.Value.TryGetBoolean(out value))
            {
                return true;
            }

            value = false;
            return false;
        }

        /// <summary>
        /// Attempts to read an integer entry.
        /// </summary>
        public bool TryGetInt64(string key, out long value)
        {
            TomlEntry entry = GetEntry(key);
            if (entry?.Value != null && entry.Value.TryGetInt64(out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Attempts to read a floating-point entry.
        /// </summary>
        public bool TryGetDouble(string key, out double value)
        {
            TomlEntry entry = GetEntry(key);
            if (entry?.Value != null)
            {
                if (entry.Value.TryGetDouble(out value))
                {
                    return true;
                }

                if (entry.Value.TryGetInt64(out long integerValue))
                {
                    value = integerValue;
                    return true;
                }
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Attempts to read an array entry.
        /// </summary>
        public bool TryGetArray(string key, out IReadOnlyList<TomlValue> values)
        {
            TomlEntry entry = GetEntry(key);
            if (entry?.Value != null && entry.Value.TryGetArray(out values))
            {
                return true;
            }

            values = Array.Empty<TomlValue>();
            return false;
        }

        internal void MoveEntryToIndex(string key, int index)
        {
            if (!_entriesByKey.TryGetValue(key, out TomlEntry entry))
            {
                return;
            }

            _entries.Remove(entry);
            if (index < 0)
            {
                index = 0;
            }

            if (index > _entries.Count)
            {
                index = _entries.Count;
            }

            _entries.Insert(index, entry);
        }
    }
}
