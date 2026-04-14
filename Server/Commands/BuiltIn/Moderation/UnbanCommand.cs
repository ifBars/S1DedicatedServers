using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Moderation
{
    /// <summary>
    /// Command to unban a player from the server
    /// </summary>
    public class UnbanCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "unban";
        public override string Description => "Removes a ban from a player";
        public override string Usage => "unban <steamid>";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.PlayerUnban;

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

            if (context.Permissions?.RemoveBan(GetExecutorTrustedUniqueId(context), steamId, "unban command") == true)
            {
                context.Reply($"Removed ban for Steam ID: {steamId}");
            }
            else
            {
                context.ReplyError($"Failed to remove ban for Steam ID: {steamId}");
            }
        }
    }
}
