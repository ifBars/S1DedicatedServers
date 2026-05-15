using System.Collections.Generic;
using DedicatedServerMod.Server.Commands.Output;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Commands.Execution
{
    /// <summary>
    /// Carries invocation state and reply sinks for a server command execution.
    /// </summary>
    /// <remarks>
    /// Commands can be invoked by the TCP console, stdio host console, web panel, or an in-game
    /// player. Use <see cref="IsConsoleExecution"/> and <see cref="Executor"/> before assuming
    /// a real player exists, especially for commands that support a <c>self</c> target.
    /// </remarks>
    public class CommandContext
    {
        /// <summary>
        /// Gets or sets the player executing the command, or <see langword="null"/> for non-player console execution.
        /// </summary>
        public ConnectedPlayerInfo Executor { get; set; }

        /// <summary>
        /// Gets or sets the parsed command arguments after the command word.
        /// </summary>
        public List<string> Arguments { get; set; }

        /// <summary>
        /// Gets or sets the player manager available to command handlers.
        /// </summary>
        public PlayerManager PlayerManager { get; set; }

        /// <summary>
        /// Gets or sets the live permission service used for authorization-sensitive commands.
        /// </summary>
        public ServerPermissionService Permissions { get; set; }

        /// <summary>
        /// Gets or sets the optional transport-specific output sink.
        /// </summary>
        public ICommandOutput Output { get; set; }

        /// <summary>
        /// Optional structured reply channel used by internal command transports.
        /// </summary>
        internal ICommandReplyChannel ReplyChannel { get; set; }

        /// <summary>
        /// Gets a value indicating whether the command is being executed outside a real player context.
        /// </summary>
        public bool IsConsoleExecution => Executor == null;

        /// <summary>
        /// Sends an informational message back to the command executor.
        /// </summary>
        /// <param name="message">The user-facing message to send.</param>
        public void Reply(string message)
        {
            WriteReply(CommandReplyLevel.Info, message);
        }

        /// <summary>
        /// Sends a warning message back to the command executor.
        /// </summary>
        /// <param name="message">The user-facing warning to send.</param>
        public void ReplyWarning(string message)
        {
            WriteReply(CommandReplyLevel.Warning, message);
        }

        /// <summary>
        /// Sends an error message back to the command executor.
        /// </summary>
        /// <param name="message">The user-facing error to send.</param>
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
