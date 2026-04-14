using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Permissions
{
    /// <summary>
    /// Command to grant administrator privileges to a player
    /// </summary>
    public class AdminCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "admin";
        public override string Description => "Grants administrator privileges to a player";
        public override string Usage => "admin <player_name_or_steamid>";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsGroupAssign;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);
            string targetId = targetPlayer?.TrustedUniqueId ?? identifier;

            if (context.Permissions?.AssignGroup(GetExecutorTrustedUniqueId(context), targetId, Shared.Permissions.PermissionBuiltIns.Groups.Administrator, "admin command") == true)
            {
                context.Reply(targetPlayer != null
                    ? $"Granted administrator privileges to {targetPlayer.DisplayName} ({targetId})"
                    : $"Granted administrator privileges to Steam ID: {targetId}");
            }
            else
            {
                context.ReplyError($"Failed to grant administrator privileges to {targetId}. The target may already be an administrator or outrank you.");
            }
        }
    }
}
