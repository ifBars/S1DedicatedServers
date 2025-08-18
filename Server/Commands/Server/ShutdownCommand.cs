using MelonLoader;
using System;
using System.Linq;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Server
{
    /// <summary>
    /// Command to shutdown the server gracefully
    /// </summary>
    public class ShutdownCommand : BaseServerCommand
    {
        public ShutdownCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "shutdown";
        public override string Description => "Gracefully shuts down the server";
        public override string Usage => "shutdown [reason]";
        public override PermissionLevel RequiredPermission => PermissionLevel.Administrator;

        public override void Execute(CommandContext context)
        {
            try
            {
                string reason = context.Arguments.Count > 0 
                    ? string.Join(" ", context.Arguments) 
                    : "Server shutdown";

                context.Reply($"Initiating server shutdown: {reason}");
                logger.Msg($"Server shutdown initiated by {context.Executor?.DisplayName ?? "Console"}: {reason}");
                
                // Notify all players
                playerManager.BroadcastMessage($"Server shutting down: {reason}");
                
                // Trigger graceful shutdown
                MelonCoroutines.Start(DelayedShutdown(reason));
            }
            catch (Exception ex)
            {
                context.ReplyError($"Failed to initiate shutdown: {ex.Message}");
                logger.Error($"Error initiating shutdown: {ex}");
            }
        }

        /// <summary>
        /// Delayed shutdown to allow messages to be sent
        /// </summary>
        private System.Collections.IEnumerator DelayedShutdown(string reason)
        {
            // Wait a moment for messages to be delivered
            yield return new UnityEngine.WaitForSeconds(2f);
            
            // Trigger server shutdown
            ServerBootstrap.Shutdown(reason);
        }
    }
}
