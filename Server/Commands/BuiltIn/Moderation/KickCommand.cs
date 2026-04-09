using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Moderation
{
    /// <summary>
    /// Command to kick a player from the server
    /// </summary>
    public class KickCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "kick";
        public override string Description => "Kicks a player from the server";
        public override string Usage => "kick <player_name_or_id> [reason]";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.PlayerKick;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            string reason = context.Arguments.Count > 1 
                ? string.Join(" ", context.Arguments.Skip(1)) 
                : "Kicked by admin";

            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);

            if (targetPlayer == null)
            {
                context.ReplyError($"Player not found: {identifier}");
                return;
            }

            // Check if trying to kick someone with higher privileges
            if (context.Executor != null && !CanKickPlayer(context.Executor, targetPlayer))
            {
                context.ReplyError($"Cannot kick {targetPlayer.DisplayName}: insufficient privileges");
                return;
            }

            if (PlayerManager.NotifyAndDisconnectPlayer(targetPlayer, "Kicked", reason))
            {
                context.Reply($"Kicked {targetPlayer.DisplayName}: {reason}");
            }
            else
            {
                context.ReplyError($"Failed to kick {targetPlayer.DisplayName}");
            }
        }

        /// <summary>
        /// Check if the executor can kick the target player
        /// </summary>
        private bool CanKickPlayer(ConnectedPlayerInfo executor, ConnectedPlayerInfo target)
        {
            return DedicatedServerMod.Server.Core.ServerBootstrap.Permissions?.HasDominanceOver(executor?.TrustedUniqueId, target?.TrustedUniqueId) == true;
        }
    }
}
