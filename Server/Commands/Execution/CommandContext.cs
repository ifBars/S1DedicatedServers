using System.Collections.Generic;
using DedicatedServerMod.Server.Commands.Output;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Commands.Execution
{
    /// <summary>
    /// Context information for command execution.
    /// </summary>
    public class CommandContext
    {
        /// <summary>
        /// The player executing the command (null for console).
        /// </summary>
        public ConnectedPlayerInfo Executor { get; set; }

        /// <summary>
        /// Command arguments.
        /// </summary>
        public List<string> Arguments { get; set; }

        /// <summary>
        /// Player manager.
        /// </summary>
        public PlayerManager PlayerManager { get; set; }

        /// <summary>
        /// Permission service.
        /// </summary>
        public ServerPermissionService Permissions { get; set; }

        /// <summary>
        /// Optional transport-specific output sink.
        /// </summary>
        public ICommandOutput Output { get; set; }

        /// <summary>
        /// Optional structured reply channel used by internal command transports.
        /// </summary>
        internal ICommandReplyChannel ReplyChannel { get; set; }

        /// <summary>
        /// Whether the command is being executed from console.
        /// </summary>
        public bool IsConsoleExecution => Executor == null;

        /// <summary>
        /// Send a message back to the executor.
        /// </summary>
        public void Reply(string message)
        {
            WriteReply(CommandReplyLevel.Info, message);
        }

        /// <summary>
        /// Send a warning message back to the executor.
        /// </summary>
        public void ReplyWarning(string message)
        {
            WriteReply(CommandReplyLevel.Warning, message);
        }

        /// <summary>
        /// Send an error message back to the executor.
        /// </summary>
        public void ReplyError(string message)
        {
            WriteReply(CommandReplyLevel.Error, message);
        }

        private void WriteReply(CommandReplyLevel level, string message)
        {
            if (ReplyChannel != null)
            {
                ReplyChannel.Write(new CommandReplyLine(level, message));
                return;
            }

            if (Output != null)
            {
                switch (level)
                {
                    case CommandReplyLevel.Warning:
                        Output.WriteWarning(message);
                        break;
                    case CommandReplyLevel.Error:
                        Output.WriteError(message);
                        break;
                    default:
                        Output.WriteInfo(message);
                        break;
                }

                return;
            }

            string rendered = BuildReplyPrefix() + (message ?? string.Empty);
            switch (level)
            {
                case CommandReplyLevel.Warning:
                    DebugLog.Warning(rendered);
                    break;
                case CommandReplyLevel.Error:
                    DebugLog.Error(rendered);
                    break;
                default:
                    DebugLog.Info(rendered);
                    break;
            }
        }

        private string BuildReplyPrefix()
        {
            return IsConsoleExecution
                ? "[COMMAND] "
                : $"[COMMAND -> {Executor.DisplayName}] ";
        }
    }
}
