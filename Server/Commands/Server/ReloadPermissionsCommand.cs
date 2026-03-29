using MelonLoader;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.Server
{
    /// <summary>
    /// Reloads the dedicated permissions graph from disk.
    /// </summary>
    public sealed class ReloadPermissionsCommand : BaseServerCommand
    {
        /// <summary>
        /// Initializes a new reload-permissions command.
        /// </summary>
        public ReloadPermissionsCommand(MelonLogger.Instance logger, PlayerManager playerMgr)
            : base(logger, playerMgr)
        {
        }

        /// <inheritdoc />
        public override string CommandWord => "reloadpermissions";

        /// <inheritdoc />
        public override string Description => "Reloads permissions.toml without restarting the server";

        /// <inheritdoc />
        public override string Usage => "reloadpermissions";

        /// <inheritdoc />
        public override string RequiredPermissionNode => DedicatedServerMod.Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsReload;

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
