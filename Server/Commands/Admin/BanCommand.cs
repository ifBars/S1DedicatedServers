using MelonLoader;
using System.Linq;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Command to ban a player from the server
    /// </summary>
    public class BanCommand : BaseServerCommand
    {
        public BanCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "ban";
        public override string Description => "Bans a player from the server";
        public override string Usage => "ban <player_name_or_id> [reason]";
        public override PermissionLevel RequiredPermission => PermissionLevel.Operator;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            string reason = context.Arguments.Count > 1 
                ? string.Join(" ", context.Arguments.Skip(1)) 
                : "Banned by admin";

            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);

            if (targetPlayer == null)
            {
                context.ReplyError($"Player not found: {identifier}");
                return;
            }

            if (string.IsNullOrEmpty(targetPlayer.SteamId))
            {
                context.ReplyError($"Cannot ban {targetPlayer.DisplayName}: no Steam ID available");
                return;
            }

            // Check if trying to ban someone with equal or higher privileges
            if (context.Executor != null && !CanBanPlayer(context.Executor, targetPlayer))
            {
                context.ReplyError($"Cannot ban {targetPlayer.DisplayName}: insufficient privileges");
                return;
            }

            if (PlayerManager.BanPlayer(targetPlayer, reason))
            {
                context.Reply($"Banned {targetPlayer.DisplayName} ({targetPlayer.SteamId}): {reason}");
                Logger.Msg($"Player {targetPlayer.DisplayName} banned by {context.Executor?.DisplayName ?? "Console"}: {reason}");
            }
            else
            {
                context.ReplyError($"Failed to ban {targetPlayer.DisplayName}");
            }
        }

        /// <summary>
        /// Check if the executor can ban the target player
        /// </summary>
        private bool CanBanPlayer(ConnectedPlayerInfo executor, ConnectedPlayerInfo target)
        {
            var executorLevel = PlayerManager.Permissions.GetPermissionLevel(executor);
            var targetLevel = PlayerManager.Permissions.GetPermissionLevel(target);

            // Can only ban players with lower permission level
            return executorLevel > targetLevel;
        }
    }
}
