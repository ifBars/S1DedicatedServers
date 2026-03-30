using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Commands.BuiltIn.System
{
    /// <summary>
    /// Command to reload server configuration
    /// </summary>
    public class ReloadConfigCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "reloadconfig";
        public override string Description => "Reloads the server configuration from file";
        public override string Usage => "reloadconfig";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.ServerReloadConfig;

        public override void Execute(CommandContext context)
        {
            try
            {
                ServerConfig.ReloadConfig();
                bool permissionsReloaded = context.Permissions?.Reload() ?? false;

                if (!permissionsReloaded)
                {
                    context.ReplyWarning("Server configuration reloaded, but permissions reload failed. Previous live permission state was kept.");
                    DebugLog.Warning($"Configuration reloaded by {context.Executor?.DisplayName ?? "Console"}, but permissions reload failed.");
                    return;
                }

                context.Reply("Server configuration and permissions reloaded successfully");
                DebugLog.Info($"Configuration reloaded by {context.Executor?.DisplayName ?? "Console"}");
            }
            catch (Exception ex)
            {
                context.ReplyError($"Failed to reload configuration: {ex.Message}");
                DebugLog.Error("Error reloading configuration", ex);
            }
        }
    }
}
