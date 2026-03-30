namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Writes command output to a TCP session.
    /// </summary>
    public sealed class TcpCommandOutput : ICommandOutput
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
            WriteWithPrefix(message, string.Empty);
        }

        /// <inheritdoc />
        public void WriteWarning(string message)
        {
            WriteWithPrefix(message, "[WARN] ");
        }

        /// <inheritdoc />
        public void WriteError(string message)
        {
            WriteWithPrefix(message, "[ERR] ");
        }

        private void WriteWithPrefix(string message, string prefix)
        {
            string normalized = (message ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                _writeLine(prefix + lines[i]);
            }
        }
    }
}
