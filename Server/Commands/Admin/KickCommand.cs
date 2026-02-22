using MelonLoader;
using System.Linq;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Command to kick a player from the server
    /// </summary>
    public class KickCommand : BaseServerCommand
    {
        public KickCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "kick";
        public override string Description => "Kicks a player from the server";
        public override string Usage => "kick <player_name_or_id> [reason]";
        public override PermissionLevel RequiredPermission => PermissionLevel.Administrator;

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

            if (PlayerManager.KickPlayer(targetPlayer, reason))
            {
                context.Reply($"Kicked {targetPlayer.DisplayName}: {reason}");
                Logger.Msg($"Player {targetPlayer.DisplayName} kicked by {context.Executor?.DisplayName ?? "Console"}: {reason}");
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
            var executorLevel = PlayerManager.Permissions.GetPermissionLevel(executor);
            var targetLevel = PlayerManager.Permissions.GetPermissionLevel(target);

            // Can only kick players with lower or equal permission level
            return executorLevel >= targetLevel;
        }
    }
}
