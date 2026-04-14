using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Permissions
{
    /// <summary>
    /// Command to remove administrator privileges from a player
    /// </summary>
    public class DeadminCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "deadmin";
        public override string Description => "Removes administrator privileges from a player";
        public override string Usage => "deadmin <player_name_or_steamid>";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsGroupUnassign;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);
            string targetId = targetPlayer?.TrustedUniqueId ?? identifier;

            if (context.Permissions?.UnassignGroup(GetExecutorTrustedUniqueId(context), targetId, Shared.Permissions.PermissionBuiltIns.Groups.Administrator, "deadmin command") == true)
            {
                if (targetPlayer != null)
                {
                    context.Reply($"Removed administrator privileges from {targetPlayer.DisplayName} ({targetId})");
                }
                else
                {
                    context.Reply($"Removed administrator privileges from Steam ID: {targetId}");
                }
            }
            else
            {
                context.ReplyError($"Failed to remove administrator privileges from {targetId}. The target may not be an administrator or may outrank you.");
            }
        }
    }
}
