using MelonLoader;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Command to grant operator privileges to a player
    /// </summary>
    public class OpCommand : BaseServerCommand
    {
        public OpCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "op";
        public override string Description => "Grants operator privileges to a player";
        public override string Usage => "op <player_name_or_steamid>";
        public override string RequiredPermissionNode => DedicatedServerMod.Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsGroupAssign;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);
            string targetId = targetPlayer?.TrustedUniqueId ?? identifier;

            if (context.Permissions?.AssignGroup(context.Executor?.TrustedUniqueId, targetId, DedicatedServerMod.Shared.Permissions.PermissionBuiltIns.Groups.Operator, "op command") == true)
            {
                if (targetPlayer != null)
                {
                    context.Reply($"Granted operator privileges to {targetPlayer.DisplayName} ({targetId})");
                }
                else
                {
                    context.Reply($"Granted operator privileges to Steam ID: {targetId}");
                }

                Logger.Msg($"Operator granted to {targetId} by {context.Executor?.DisplayName ?? "Console"}");
            }
            else
            {
                context.ReplyError($"Failed to grant operator privileges to {targetId}. The target may already be an operator or outrank you.");
            }
        }
    }
}
