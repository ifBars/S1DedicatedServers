namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Internal transport-facing abstraction for structured command replies.
    /// </summary>
    internal interface ICommandReplyChannel
    {
        /// <summary>
        /// Writes one structured command reply line.
        /// </summary>
        void Write(CommandReplyLine line);
    }
}
