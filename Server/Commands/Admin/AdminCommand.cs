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
        public override PermissionLevel RequiredPermission => PermissionLevel.Operator;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);

            if (targetPlayer != null && !string.IsNullOrEmpty(targetPlayer.SteamId))
            {
                if (PlayerManager.Permissions.AddAdministrator(targetPlayer.SteamId))
                {
                    context.Reply($"Granted administrator privileges to {targetPlayer.DisplayName} ({targetPlayer.SteamId})");
                    Logger.Msg($"Administrator granted to {targetPlayer.DisplayName} by {context.Executor?.DisplayName ?? "Console"}");
                }
                else
                {
                    context.ReplyWarning($"{targetPlayer.DisplayName} is already an administrator");
                }
            }
            else
            {
                // Try adding by Steam ID directly
                if (PlayerManager.Permissions.AddAdministrator(identifier))
                {
                    context.Reply($"Granted administrator privileges to Steam ID: {identifier}");
                    Logger.Msg($"Administrator granted to SteamID {identifier} by {context.Executor?.DisplayName ?? "Console"}");
                }
                else
                {
                    context.ReplyError($"Player not found or already an administrator: {identifier}");
                }
            }
        }
    }
}
