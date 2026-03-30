namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Writes command output to caller-supplied delegates.
    /// </summary>
    public sealed class DelegateCommandOutput : ICommandOutput
    {
        private readonly Action<string> _writeInfo;
        private readonly Action<string> _writeWarning;
        private readonly Action<string> _writeError;

        /// <summary>
        /// Initializes a new delegate-backed command output sink.
        /// </summary>
        public DelegateCommandOutput(Action<string> writeInfo, Action<string> writeWarning, Action<string> writeError)
        {
            _writeInfo = writeInfo;
            _writeWarning = writeWarning;
            _writeError = writeError;
        }

        /// <inheritdoc />
        public void WriteInfo(string message)
        {
            _writeInfo?.Invoke(message);
        }

        /// <inheritdoc />
        public void WriteWarning(string message)
        {
            _writeWarning?.Invoke(message);
        }

        /// <inheritdoc />
        public void WriteError(string message)
        {
            _writeError?.Invoke(message);
        }
    }
}
