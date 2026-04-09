using System.IO;

namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Writes command output to a single hosted-console reply stream.
    /// </summary>
    public sealed class StdioCommandOutput : ICommandOutput, ICommandReplyChannel
    {
        private static readonly object Sync = new object();
        private readonly TextWriter _writer;

        /// <summary>
        /// Initializes a new stdio command output sink backed by <see cref="Console.Error"/>.
        /// </summary>
        public StdioCommandOutput()
            : this(System.Console.Error)
        {
        }

        internal StdioCommandOutput(TextWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
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
            lock (Sync)
            {
                foreach (CommandReplyLine expandedLine in CommandReplyRenderer.Expand(line))
                {
                    _writer.WriteLine(CommandReplyRenderer.RenderText(expandedLine));
                }

                _writer.Flush();
            }
        }
    }
}
