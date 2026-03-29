using MelonLoader;
using System;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod;
using DedicatedServerMod.Shared.Configuration;

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
        public override string RequiredPermissionNode => DedicatedServerMod.Shared.Permissions.PermissionBuiltIns.Nodes.ServerReloadConfig;

        public override void Execute(CommandContext context)
        {
            try
            {
                ServerConfig.ReloadConfig();
                bool permissionsReloaded = context.Permissions?.Reload() ?? false;

                if (!permissionsReloaded)
                {
                    context.ReplyWarning("Server configuration reloaded, but permissions reload failed. Previous live permission state was kept.");
                    Logger.Warning($"Configuration reloaded by {context.Executor?.DisplayName ?? "Console"}, but permissions reload failed.");
                    return;
                }

                context.Reply("Server configuration and permissions reloaded successfully");
                Logger.Msg($"Configuration reloaded by {context.Executor?.DisplayName ?? "Console"}");
            }
            catch (Exception ex)
            {
                context.ReplyError($"Failed to reload configuration: {ex.Message}");
                Logger.Error($"Error reloading configuration: {ex}");
            }
        }
    }
}
