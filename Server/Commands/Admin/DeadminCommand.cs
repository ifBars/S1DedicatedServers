using MelonLoader;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Command to remove administrator privileges from a player
    /// </summary>
    public class DeadminCommand : BaseServerCommand
    {
        public DeadminCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "deadmin";
        public override string Description => "Removes administrator privileges from a player";
        public override string Usage => "deadmin <player_name_or_steamid>";
        public override PermissionLevel RequiredPermission => PermissionLevel.Administrator;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);

            if (targetPlayer != null && !string.IsNullOrEmpty(targetPlayer.SteamId))
            {
                if (playerManager.Permissions.RemoveAdministrator(targetPlayer.SteamId))
                {
                    context.Reply($"Removed administrator privileges from {targetPlayer.DisplayName} ({targetPlayer.SteamId})");
                    playerManager.BroadcastMessage($"{targetPlayer.DisplayName} is no longer an administrator");
                    logger.Msg($"Administrator removed from {targetPlayer.DisplayName} by {context.Executor?.DisplayName ?? "Console"}");
                }
                else
                {
                    context.ReplyWarning($"{targetPlayer.DisplayName} is not an administrator");
                }
            }
            else
            {
                // Try removing by Steam ID directly
                if (playerManager.Permissions.RemoveAdministrator(identifier))
                {
                    context.Reply($"Removed administrator privileges from Steam ID: {identifier}");
                    logger.Msg($"Administrator removed from SteamID {identifier} by {context.Executor?.DisplayName ?? "Console"}");
                }
                else
                {
                    context.ReplyError($"Player not found or not an administrator: {identifier}");
                }
            }
        }
    }
}
