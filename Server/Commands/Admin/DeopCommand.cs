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
        public override PermissionLevel RequiredPermission => PermissionLevel.Operator;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);

            if (targetPlayer != null && !string.IsNullOrEmpty(targetPlayer.SteamId))
            {
                if (playerManager.Permissions.RemoveOperator(targetPlayer.SteamId))
                {
                    context.Reply($"Removed operator privileges from {targetPlayer.DisplayName} ({targetPlayer.SteamId})");
                    playerManager.BroadcastMessage($"{targetPlayer.DisplayName} is no longer an operator");
                    logger.Msg($"Operator removed from {targetPlayer.DisplayName} by {context.Executor?.DisplayName ?? "Console"}");
                }
                else
                {
                    context.ReplyWarning($"{targetPlayer.DisplayName} is not an operator");
                }
            }
            else
            {
                // Try removing by Steam ID directly
                if (playerManager.Permissions.RemoveOperator(identifier))
                {
                    context.Reply($"Removed operator privileges from Steam ID: {identifier}");
                    logger.Msg($"Operator removed from SteamID {identifier} by {context.Executor?.DisplayName ?? "Console"}");
                }
                else
                {
                    context.ReplyError($"Player not found or not an operator: {identifier}");
                }
            }
        }
    }
}
