namespace DedicatedServerMod.Server.Commands.Execution
{
    /// <summary>
    /// Represents the result of executing a command line.
    /// </summary>
    public sealed class CommandExecutionResult
    {
        /// <summary>
        /// Initializes a new command execution result instance.
        /// </summary>
        public CommandExecutionResult(CommandExecutionStatus status, string commandWord, string message, Exception exception = null)
        {
            Status = status;
            CommandWord = commandWord ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        /// <summary>
        /// Gets the result status.
        /// </summary>
        public CommandExecutionStatus Status { get; }

        /// <summary>
        /// Gets the normalized command word when available.
        /// </summary>
        public string CommandWord { get; }

        /// <summary>
        /// Gets the primary user-facing message associated with the outcome.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the exception associated with a failed command execution.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets whether the command execution succeeded.
        /// </summary>
        public bool Succeeded => Status == CommandExecutionStatus.Success || Status == CommandExecutionStatus.Empty;
    }
}
