using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Permissions
{
    /// <summary>
    /// Command to grant operator privileges to a player
    /// </summary>
    public class OpCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "op";
        public override string Description => "Grants operator privileges to a player";
        public override string Usage => "op <player_name_or_steamid>";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsGroupAssign;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);
            string targetId = targetPlayer?.TrustedUniqueId ?? identifier;

            if (context.Permissions?.AssignGroup(context.Executor?.TrustedUniqueId, targetId, Shared.Permissions.PermissionBuiltIns.Groups.Operator, "op command") == true)
            {
                if (targetPlayer != null)
                {
                    context.Reply($"Granted operator privileges to {targetPlayer.DisplayName} ({targetId})");
                }
                else
                {
                    context.Reply($"Granted operator privileges to Steam ID: {targetId}");
                }
            }
            else
            {
                context.ReplyError($"Failed to grant operator privileges to {targetId}. The target may already be an operator or outrank you.");
            }
        }
    }
}
