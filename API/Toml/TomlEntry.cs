namespace DedicatedServerMod.API.Toml
{
    /// <summary>
    /// Represents a TOML key/value entry and its leading comments.
    /// </summary>
    public sealed class TomlEntry
    {
        /// <summary>
        /// Initializes a new TOML entry.
        /// </summary>
        public TomlEntry(string key, TomlValue value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("TOML entry key cannot be null or whitespace.", nameof(key));
            }

            Key = key;
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Comments = new List<string>();
        }

        /// <summary>
        /// Gets or sets the entry key.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the entry value.
        /// </summary>
        public TomlValue Value { get; set; }

        /// <summary>
        /// Gets the leading comments for this entry.
        /// </summary>
        public IList<string> Comments { get; }

        /// <summary>
        /// Gets or sets the original source line number when known.
        /// </summary>
        public int SourceLine { get; set; }
    }
}
