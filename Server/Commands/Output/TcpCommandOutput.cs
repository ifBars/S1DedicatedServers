namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Writes command output to a TCP session.
    /// </summary>
    public sealed class TcpCommandOutput : ICommandOutput, ICommandReplyChannel
    {
        private readonly Action<string> _writeLine;

        /// <summary>
        /// Initializes a new TCP command output sink.
        /// </summary>
        public TcpCommandOutput(Action<string> writeLine)
        {
            _writeLine = writeLine ?? throw new ArgumentNullException(nameof(writeLine));
        }

        /// <inheritdoc />
        public void WriteInfo(string message)
        {
            WriteReply(new CommandReplyLine(CommandReplyLevel.Info, message));
        }

        /// <inheritdoc />
        public void WriteWarning(string message)
        {
            WriteReply(new CommandReplyLine(CommandReplyLevel.Warning, message));
        }

        /// <inheritdoc />
        public void WriteError(string message)
        {
            WriteReply(new CommandReplyLine(CommandReplyLevel.Error, message));
        }

        /// <inheritdoc />
        void ICommandReplyChannel.Write(CommandReplyLine line)
        {
            WriteReply(line);
        }

        private void WriteReply(CommandReplyLine line)
        {
            foreach (CommandReplyLine expandedLine in CommandReplyRenderer.Expand(line))
            {
                _writeLine(CommandReplyRenderer.RenderText(expandedLine));
            }
        }
    }
}
