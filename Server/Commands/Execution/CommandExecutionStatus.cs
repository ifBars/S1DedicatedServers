namespace DedicatedServerMod.Server.Commands.Execution
{
    /// <summary>
    /// Represents the high-level outcome of executing a command line.
    /// </summary>
    public enum CommandExecutionStatus
    {
        /// <summary>
        /// No command was supplied.
        /// </summary>
        Empty,

        /// <summary>
        /// The command completed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// The raw command line could not be parsed.
        /// </summary>
        ParseError,

        /// <summary>
        /// The command word did not match a registered command.
        /// </summary>
        UnknownCommand,

        /// <summary>
        /// The executor did not have the required permission node.
        /// </summary>
        Unauthorized,

        /// <summary>
        /// The command handler threw or reported an execution failure.
        /// </summary>
        ExecutionFailed
    }
}
