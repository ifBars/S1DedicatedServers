namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Abstraction for routing command output to a transport-specific sink.
    /// </summary>
    public interface ICommandOutput
    {
        /// <summary>
        /// Writes an informational message.
        /// </summary>
        void WriteInfo(string message);

        /// <summary>
        /// Writes a warning message.
        /// </summary>
        void WriteWarning(string message);

        /// <summary>
        /// Writes an error message.
        /// </summary>
        void WriteError(string message);
    }
}
