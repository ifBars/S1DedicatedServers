using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.BuiltIn.System
{
    /// <summary>
    /// Reloads the dedicated permissions graph from disk.
    /// </summary>
    public sealed class ReloadPermissionsCommand : BaseServerCommand
    {
        /// <summary>
        /// Initializes a new reload-permissions command.
        /// </summary>
        public ReloadPermissionsCommand(PlayerManager playerMgr)
            : base(playerMgr)
        {
        }

        /// <inheritdoc />
        public override string CommandWord => "reloadpermissions";

        /// <inheritdoc />
        public override string Description => "Reloads permissions.toml without restarting the server";

        /// <inheritdoc />
        public override string Usage => "reloadpermissions";

        /// <inheritdoc />
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsReload;

        /// <inheritdoc />
        public override void Execute(CommandContext context)
        {
            if (context.Permissions?.Reload() == true)
            {
                context.Reply("Permissions reloaded successfully.");
                return;
            }

            context.ReplyError("Permissions reload failed. Check the server log for details.");
        }
    }
}
