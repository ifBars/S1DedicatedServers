using System;
using DedicatedServerMod.Server.WebPanel;

namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Creates transport-specific command reply channels while preserving legacy output compatibility.
    /// </summary>
    internal static class CommandReplyChannelFactory
    {
        /// <summary>
        /// Adapts an existing legacy output sink into a structured reply channel.
        /// </summary>
        public static ICommandReplyChannel FromLegacy(ICommandOutput output)
        {
            if (output == null)
            {
                return null;
            }

            if (output is ICommandReplyChannel replyChannel)
            {
                return replyChannel;
            }

            return new LegacyCommandReplyChannel(output);
        }

        /// <summary>
        /// Creates the stdio reply channel.
        /// </summary>
        public static ICommandReplyChannel CreateStdio()
        {
            return new StdioCommandOutput();
        }

        /// <summary>
        /// Creates the TCP reply channel.
        /// </summary>
        public static ICommandReplyChannel CreateTcp(Action<string> writeLine)
        {
            return new TcpCommandOutput(writeLine);
        }

        /// <summary>
        /// Creates the in-game console reply channel.
        /// </summary>
        public static ICommandReplyChannel CreateGameConsole()
        {
            return new GameConsoleCommandOutput();
        }

        /// <summary>
        /// Creates the web panel reply channel.
        /// </summary>
        public static WebPanelCommandReplyChannel CreateWebPanel(WebPanelEventStream eventStream, WebPanelLogBuffer logBuffer)
        {
            return new WebPanelCommandReplyChannel(eventStream, logBuffer);
        }

        private sealed class LegacyCommandReplyChannel : ICommandReplyChannel
        {
            private readonly ICommandOutput _output;

            public LegacyCommandReplyChannel(ICommandOutput output)
            {
                _output = output ?? throw new ArgumentNullException(nameof(output));
            }

            public void Write(CommandReplyLine line)
            {
                switch (line.Level)
                {
                    case CommandReplyLevel.Warning:
                        _output.WriteWarning(line.Message);
                        break;
                    case CommandReplyLevel.Error:
                        _output.WriteError(line.Message);
                        break;
                    default:
                        _output.WriteInfo(line.Message);
                        break;
                }
            }
        }
    }
}
