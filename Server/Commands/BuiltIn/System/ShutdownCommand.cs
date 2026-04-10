using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Utils;
using MelonLoader;

namespace DedicatedServerMod.Server.Commands.BuiltIn.System
{
    /// <summary>
    /// Command to shutdown the server gracefully
    /// </summary>
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
        /// Delayed shutdown to allow messages to be sent
        /// </summary>
        private global::System.Collections.IEnumerator DelayedShutdown(string reason)
        {
            // Wait a moment for messages to be delivered
            yield return new UnityEngine.WaitForSeconds(2f);
            
            // Trigger server shutdown
            ServerBootstrap.Shutdown(reason);
        }
    }
}
