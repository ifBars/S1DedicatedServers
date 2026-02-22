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
        public override PermissionLevel RequiredPermission => PermissionLevel.Operator;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string identifier = context.Arguments[0];
            ConnectedPlayerInfo targetPlayer = FindPlayerByNameOrId(identifier);

            if (targetPlayer != null && !string.IsNullOrEmpty(targetPlayer.SteamId))
            {
                if (PlayerManager.Permissions.AddOperator(targetPlayer.SteamId))
                {
                    context.Reply($"Granted operator privileges to {targetPlayer.DisplayName} ({targetPlayer.SteamId})");
                    Logger.Msg($"Operator granted to {targetPlayer.DisplayName} by {context.Executor?.DisplayName ?? "Console"}");
                }
                else
                {
                    context.ReplyWarning($"{targetPlayer.DisplayName} is already an operator");
                }
            }
            else
            {
                // Try adding by Steam ID directly
                if (PlayerManager.Permissions.AddOperator(identifier))
                {
                    context.Reply($"Granted operator privileges to Steam ID: {identifier}");
                    Logger.Msg($"Operator granted to SteamID {identifier} by {context.Executor?.DisplayName ?? "Console"}");
                }
                else
                {
                    context.ReplyError($"Player not found or already an operator: {identifier}");
                }
            }
        }
    }
}
