using MelonLoader;
using System.Linq;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod;
using DedicatedServerMod.Shared.Configuration;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Command to unban a player from the server
    /// </summary>
    public class UnbanCommand : BaseServerCommand
    {
        public UnbanCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "unban";
        public override string Description => "Removes a ban from a player";
        public override string Usage => "unban <steamid>";
        public override PermissionLevel RequiredPermission => PermissionLevel.Operator;

        public override void Execute(CommandContext context)
        {
            if (!ValidateArguments(context, 1))
                return;

            string steamId = context.Arguments[0];

            if (!PlayerManager.IsPlayerBanned(steamId))
            {
                context.ReplyWarning($"Steam ID {steamId} is not banned");
                return;
            }

            // Remove from ban list
            if (ServerConfig.Instance.BannedPlayers.Remove(steamId))
            {
                ServerConfig.SaveConfig();
                context.Reply($"Removed ban for Steam ID: {steamId}");
                Logger.Msg($"Ban removed for SteamID {steamId} by {context.Executor?.DisplayName ?? "Console"}");
            }
            else
            {
                context.ReplyError($"Failed to remove ban for Steam ID: {steamId}");
            }
        }
    }
}
