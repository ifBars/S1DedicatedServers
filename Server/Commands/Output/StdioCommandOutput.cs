namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Writes command output to redirected process stdout and stderr.
    /// </summary>
    public sealed class StdioCommandOutput : ICommandOutput
    {
        private static readonly object Sync = new object();

        /// <inheritdoc />
        public void WriteInfo(string message)
        {
            Write(System.Console.Out, message);
        }

        /// <inheritdoc />
        public void WriteWarning(string message)
        {
            Write(System.Console.Error, message);
        }

        /// <inheritdoc />
        public void WriteError(string message)
        {
            Write(System.Console.Error, message);
        }

        private static void Write(TextWriter writer, string message)
        {
            lock (Sync)
            {
                writer.WriteLine(message ?? string.Empty);
                writer.Flush();
            }
        }
    }
}
