using MelonLoader;
using System;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Server
{
    /// <summary>
    /// Command to reload server configuration
    /// </summary>
    public class ReloadConfigCommand : BaseServerCommand
    {
        public ReloadConfigCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "reloadconfig";
        public override string Description => "Reloads the server configuration from file";
        public override string Usage => "reloadconfig";
        public override PermissionLevel RequiredPermission => PermissionLevel.Administrator;

        public override void Execute(CommandContext context)
        {
            try
            {
                // Reload configuration from file
                ServerConfig.ReloadConfig();
                
                context.Reply("Server configuration reloaded successfully");
                logger.Msg($"Configuration reloaded by {context.Executor?.DisplayName ?? "Console"}");
                
                // Broadcast to all players
                playerManager.BroadcastMessage("Server configuration has been reloaded");
            }
            catch (Exception ex)
            {
                context.ReplyError($"Failed to reload configuration: {ex.Message}");
                logger.Error($"Error reloading configuration: {ex}");
            }
        }
    }
}
