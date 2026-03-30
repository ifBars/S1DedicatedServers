namespace DedicatedServerMod.API.Toml
{
    /// <summary>
    /// Represents a TOML document with ordered tables and root entries.
    /// </summary>
    public sealed class TomlDocument
    {
        private readonly List<TomlTable> _tables = new List<TomlTable>();
        private readonly Dictionary<string, TomlTable> _tablesByName = new Dictionary<string, TomlTable>(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new TOML document.
        /// </summary>
        public TomlDocument()
        {
            FileHeaderComments = new List<string>();
            Root = new TomlTable(string.Empty, isRoot: true);
        }

        /// <summary>
        /// Gets the file header comments emitted before the first root entry or table.
        /// </summary>
        public IList<string> FileHeaderComments { get; }

        /// <summary>
        /// Gets the root table.
        /// </summary>
        public TomlTable Root { get; }

        /// <summary>
        /// Gets the ordered named tables.
        /// </summary>
        public IReadOnlyList<TomlTable> Tables => _tables;

        /// <summary>
        /// Gets a named table when present.
        /// </summary>
        public TomlTable GetTable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Root;
            }

            return _tablesByName.TryGetValue(name, out TomlTable table) ? table : null;
        }

        /// <summary>
        /// Gets or adds a named table.
        /// </summary>
        public TomlTable GetOrAddTable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Root;
            }

            if (_tablesByName.TryGetValue(name, out TomlTable existing))
            {
                return existing;
            }

            TomlTable table = new TomlTable(name, isRoot: false);
            _tables.Add(table);
            _tablesByName[name] = table;
            return table;
        }

        /// <summary>
        /// Removes a named table.
        /// </summary>
        public bool RemoveTable(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !_tablesByName.TryGetValue(name, out TomlTable table))
            {
                return false;
            }

            _tablesByName.Remove(name);
            return _tables.Remove(table);
        }

        internal void MoveTableToIndex(string name, int index)
        {
            if (string.IsNullOrWhiteSpace(name) || !_tablesByName.TryGetValue(name, out TomlTable table))
            {
                return;
            }

            _tables.Remove(table);
            if (index < 0)
            {
                index = 0;
            }

            if (index > _tables.Count)
            {
                index = _tables.Count;
            }

            _tables.Insert(index, table);
        }

        internal void RemoveTablesWhere(Func<TomlTable, bool> predicate)
        {
            List<TomlTable> tablesToRemove = _tables.Where(table => predicate(table)).ToList();
            foreach (TomlTable table in tablesToRemove)
            {
                _tables.Remove(table);
                _tablesByName.Remove(table.Name);
            }
        }
    }
}
