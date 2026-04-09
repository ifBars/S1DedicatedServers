#if IL2CPP
using Console = Il2CppScheduleOne.Console;
#else
using Console = ScheduleOne.Console;
#endif
namespace DedicatedServerMod.Server.Commands.Output
{
    /// <summary>
    /// Writes command output to the game console.
    /// </summary>
    public sealed class GameConsoleCommandOutput : ICommandOutput, ICommandReplyChannel
    {
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
                string renderedLine = CommandReplyRenderer.RenderText(expandedLine);
                switch (expandedLine.Level)
                {
                    case CommandReplyLevel.Warning:
                        Console.LogWarning(renderedLine);
                        break;
                    case CommandReplyLevel.Error:
                        Console.LogError(renderedLine);
                        break;
                    default:
                        Console.Log(renderedLine);
                        break;
                }
            }
        }
    }
}
