namespace DedicatedServerMod.API.Toml
{
    /// <summary>
    /// Describes a TOML parser or mapper diagnostic.
    /// </summary>
    public sealed class TomlDiagnostic
    {
        /// <summary>
        /// Initializes a new diagnostic record.
        /// </summary>
        /// <param name="lineNumber">The source line number when available.</param>
        /// <param name="tableName">The TOML table name when available.</param>
        /// <param name="key">The TOML key when available.</param>
        /// <param name="message">The diagnostic message.</param>
        public TomlDiagnostic(int lineNumber, string tableName, string key, string message)
        {
            LineNumber = lineNumber;
            TableName = tableName ?? string.Empty;
            Key = key ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Initializes a new empty diagnostic record.
        /// </summary>
        public TomlDiagnostic()
        {
        }

        /// <summary>
        /// Gets or sets the source line number when available.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Gets or sets the TOML table name when available.
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the TOML key when available.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the diagnostic message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
