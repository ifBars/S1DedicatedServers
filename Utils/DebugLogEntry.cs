using System;

namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Severity levels emitted through <see cref="DebugLog"/>.
    /// </summary>
    public enum DebugLogLevel
    {
        Info,
        Warning,
        Error,
        Debug,
        Verbose
    }

    /// <summary>
    /// Represents a structured debug log entry that can be consumed by internal tooling.
    /// </summary>
    public sealed class DebugLogEntry
    {
        /// <summary>
        /// Gets or sets when the entry was recorded.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the log severity.
        /// </summary>
        public DebugLogLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the rendered log message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
