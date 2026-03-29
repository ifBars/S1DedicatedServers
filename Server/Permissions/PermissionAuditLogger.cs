using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;
using MelonLoader;

namespace DedicatedServerMod.Server.Permissions
{
    /// <summary>
    /// Centralizes audit logging for privileged permission operations.
    /// </summary>
    internal sealed class PermissionAuditLogger
    {
        private readonly MelonLogger.Instance _logger;

        /// <summary>
        /// Initializes a new audit logger instance.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public PermissionAuditLogger(MelonLogger.Instance logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Logs a permission mutation.
        /// </summary>
        /// <param name="actor">The actor performing the change.</param>
        /// <param name="action">The action name.</param>
        /// <param name="target">The target identifier.</param>
        /// <param name="details">Additional details about the mutation.</param>
        public void LogMutation(string actor, string action, string target, string details)
        {
            if (!ServerConfig.Instance.LogAdminCommands)
            {
                return;
            }

            string message = $"Permission Action - Actor: {actor ?? "Console"} | Action: {action} | Target: {target}";
            if (!string.IsNullOrWhiteSpace(details))
            {
                message += $" | Details: {details}";
            }

            _logger.Msg(message);
            DebugLog.WriteToAdminLog(message);
        }

        /// <summary>
        /// Logs a privileged command execution attempt.
        /// </summary>
        /// <param name="actor">The command executor.</param>
        /// <param name="commandWord">The command word.</param>
        /// <param name="succeeded">Whether the command succeeded.</param>
        /// <param name="details">Additional details about the execution.</param>
        public void LogCommand(ConnectedPlayerInfo actor, string commandWord, bool succeeded, string details)
        {
            if (!ServerConfig.Instance.LogAdminCommands)
            {
                return;
            }

            string actorLabel = actor?.DisplayName ?? "Console";
            string actorId = actor?.TrustedUniqueId ?? "console";
            string message = $"Privileged Command - Actor: {actorLabel} ({actorId}) | Command: {commandWord} | Result: {(succeeded ? "Allowed" : "Denied")}";
            if (!string.IsNullOrWhiteSpace(details))
            {
                message += $" | Details: {details}";
            }

            _logger.Msg(message);
            DebugLog.WriteToAdminLog(message);
        }
    }
}
