using MelonLoader;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Command to grant administrator privileges to a player
    /// </summary>
    public class AdminCommand : BaseServerCommand
    {
        public AdminCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "admin";
        public override string Description => "Grants administrator privileges to a player";
        public override string Usage => "admin <player_name_or_steamid>";
        public override string RequiredPermissionNode => DedicatedServerMod.Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsGroupAssign;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);
            string targetId = targetPlayer?.TrustedUniqueId ?? identifier;

            if (context.Permissions?.AssignGroup(context.Executor?.TrustedUniqueId, targetId, DedicatedServerMod.Shared.Permissions.PermissionBuiltIns.Groups.Administrator, "admin command") == true)
            {
                if (targetPlayer != null)
                {
                    context.Reply($"Granted administrator privileges to {targetPlayer.DisplayName} ({targetId})");
                }
                else
                {
                    context.Reply($"Granted administrator privileges to Steam ID: {targetId}");
                }

                Logger.Msg($"Administrator granted to {targetId} by {context.Executor?.DisplayName ?? "Console"}");
            }
            else
            {
                context.ReplyError($"Failed to grant administrator privileges to {targetId}. The target may already be an administrator or outrank you.");
            }
        }
    }
}
