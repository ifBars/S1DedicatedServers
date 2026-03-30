using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Permissions
{
    /// <summary>
    /// Command to remove operator privileges from a player
    /// </summary>
    public class DeopCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "deop";
        public override string Description => "Removes operator privileges from a player";
        public override string Usage => "deop <player_name_or_steamid>";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsGroupUnassign;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);
            string targetId = targetPlayer?.TrustedUniqueId ?? identifier;

            if (context.Permissions?.UnassignGroup(context.Executor?.TrustedUniqueId, targetId, Shared.Permissions.PermissionBuiltIns.Groups.Operator, "deop command") == true)
            {
                if (targetPlayer != null)
                {
                    context.Reply($"Removed operator privileges from {targetPlayer.DisplayName} ({targetId})");
                }
                else
                {
                    context.Reply($"Removed operator privileges from Steam ID: {targetId}");
                }
            }
            else
            {
                context.ReplyError($"Failed to remove operator privileges from {targetId}. The target may not be an operator or may outrank you.");
            }
        }
    }
}
