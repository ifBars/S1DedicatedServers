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
        /// Whether the command is being executed from console.
        /// </summary>
        public bool IsConsoleExecution => Executor == null;

        /// <summary>
        /// Send a message back to the executor.
        /// </summary>
        public void Reply(string message)
        {
            if (Output != null)
            {
                Output.WriteInfo(message);
                return;
            }

            DebugLog.Info(BuildReplyPrefix() + (message ?? string.Empty));
        }

        /// <summary>
        /// Send a warning message back to the executor.
        /// </summary>
        public void ReplyWarning(string message)
        {
            if (Output != null)
            {
                Output.WriteWarning(message);
                return;
            }

            DebugLog.Warning(BuildReplyPrefix() + (message ?? string.Empty));
        }

        /// <summary>
        /// Send an error message back to the executor.
        /// </summary>
        public void ReplyError(string message)
        {
            if (Output != null)
            {
                Output.WriteError(message);
                return;
            }

            DebugLog.Error(BuildReplyPrefix() + (message ?? string.Empty));
        }

        private string BuildReplyPrefix()
        {
            return IsConsoleExecution
                ? "[COMMAND] "
                : $"[COMMAND -> {Executor.DisplayName}] ";
        }
    }
}
