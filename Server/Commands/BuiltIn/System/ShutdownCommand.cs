using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Utils;
using MelonLoader;

namespace DedicatedServerMod.Server.Commands.BuiltIn.System
{
    /// <summary>
    /// Gracefully stops the dedicated server after notifying the command executor.
    /// </summary>
    /// <remarks>
    /// This command is guarded by the dedicated <c>server.stop</c> permission node rather than a
    /// generated command-node permission. It queues shutdown through <see cref="ServerBootstrap"/>
    /// so teardown stays on the server runtime path.
    /// </remarks>
    public class ShutdownCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "shutdown";
        public override string Description => "Gracefully shuts down the server";
        public override string Usage => "shutdown [reason]";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.ServerStop;

        public override void Execute(CommandContext context)
        {
            try
            {
                string reason = context.Arguments.Count > 0 
                    ? string.Join(" ", context.Arguments) 
                    : "Server is shutting down";

                context.Reply($"Initiating server shutdown: {reason}");
                DebugLog.Info($"Server shutdown initiated by {context.Executor?.DisplayName ?? "Console"}: {reason}");
                
                // Notify all players via a UI popup
                // playerManager.BroadcastMessage($"Server shutting down: {reason}");
                
                // Trigger graceful shutdown
                MelonCoroutines.Start(DelayedShutdown(reason));
            }
            catch (Exception ex)
            {
                context.ReplyError($"Failed to initiate shutdown: {ex.Message}");
                DebugLog.Error("Error initiating shutdown", ex);
            }
        }

        /// <summary>
        /// Delays shutdown briefly so command replies and disconnect notices have time to flush.
        /// </summary>
        /// <param name="reason">The reason shown in logs and shutdown handling.</param>
        /// <returns>An enumerator executed by MelonLoader's coroutine scheduler.</returns>
        private global::System.Collections.IEnumerator DelayedShutdown(string reason)
        {
            // Wait a moment for messages to be delivered
            yield return new UnityEngine.WaitForSeconds(2f);
            
            // Trigger server shutdown
            ServerBootstrap.Shutdown(reason);
        }
    }
}
