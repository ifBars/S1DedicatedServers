using MelonLoader;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Command to remove operator privileges from a player
    /// </summary>
    public class DeopCommand : BaseServerCommand
    {
        public DeopCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "deop";
        public override string Description => "Removes operator privileges from a player";
        public override string Usage => "deop <player_name_or_steamid>";
        public override string RequiredPermissionNode => DedicatedServerMod.Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsGroupUnassign;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);
            string targetId = targetPlayer?.TrustedUniqueId ?? identifier;

            if (context.Permissions?.UnassignGroup(context.Executor?.TrustedUniqueId, targetId, DedicatedServerMod.Shared.Permissions.PermissionBuiltIns.Groups.Operator, "deop command") == true)
            {
                if (targetPlayer != null)
                {
                    context.Reply($"Removed operator privileges from {targetPlayer.DisplayName} ({targetId})");
                }
                else
                {
                    context.Reply($"Removed operator privileges from Steam ID: {targetId}");
                }

                Logger.Msg($"Operator removed from {targetId} by {context.Executor?.DisplayName ?? "Console"}");
            }
            else
            {
                context.ReplyError($"Failed to remove operator privileges from {targetId}. The target may not be an operator or may outrank you.");
            }
        }
    }
}
