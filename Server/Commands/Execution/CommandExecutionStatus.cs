namespace DedicatedServerMod.Server.Commands.Execution
{
    /// <summary>
    /// Represents the high-level outcome of executing a command line.
    /// </summary>
    public enum CommandExecutionStatus
    {
        Empty,
        Success,
        ParseError,
        UnknownCommand,
        Unauthorized,
        ExecutionFailed
    }
}
