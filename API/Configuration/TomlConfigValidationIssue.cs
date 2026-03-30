namespace DedicatedServerMod.API.Configuration
{
    /// <summary>
    /// Represents a typed configuration validation issue.
    /// </summary>
    public sealed class TomlConfigValidationIssue
    {
        /// <summary>
        /// Initializes a new validation issue.
        /// </summary>
        /// <param name="section">The related section name.</param>
        /// <param name="key">The related key name.</param>
        /// <param name="message">The validation message.</param>
        public TomlConfigValidationIssue(string section, string key, string message)
        {
            Section = section ?? string.Empty;
            Key = key ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Gets the section name associated with the issue.
        /// </summary>
        public string Section { get; }

        /// <summary>
        /// Gets the key name associated with the issue.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the validation message.
        /// </summary>
        public string Message { get; }
    }
}
