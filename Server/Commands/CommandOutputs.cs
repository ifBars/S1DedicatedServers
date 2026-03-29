using System;
#if IL2CPP
using Console = Il2CppScheduleOne.Console;
#else
using Console = ScheduleOne.Console;
#endif

namespace DedicatedServerMod.Server.Commands
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

    /// <summary>
    /// Writes command output to the game console.
    /// </summary>
    public sealed class GameConsoleCommandOutput : ICommandOutput
    {
        /// <inheritdoc />
        public void WriteInfo(string message)
        {
            Console.Log(message);
        }

        /// <inheritdoc />
        public void WriteWarning(string message)
        {
            Console.LogWarning(message);
        }

        /// <inheritdoc />
        public void WriteError(string message)
        {
            Console.LogError(message);
        }
    }

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

        private static void Write(System.IO.TextWriter writer, string message)
        {
            lock (Sync)
            {
                writer.WriteLine(message ?? string.Empty);
                writer.Flush();
            }
        }
    }
}
