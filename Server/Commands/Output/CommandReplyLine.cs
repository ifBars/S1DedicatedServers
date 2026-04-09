namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Represents a structured command reply line before transport-specific rendering.
    /// </summary>
    internal readonly struct CommandReplyLine
    {
        /// <summary>
        /// Initializes a new structured command reply line.
        /// </summary>
        public CommandReplyLine(CommandReplyLevel level, string message)
        {
            Level = level;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Gets the reply severity.
        /// </summary>
        public CommandReplyLevel Level { get; }

        /// <summary>
        /// Gets the raw reply message for this line.
        /// </summary>
        public string Message { get; }
    }
}
