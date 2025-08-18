using MelonLoader;
using System.Linq;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Command to list all administrators
    /// </summary>
    public class ListAdminsCommand : BaseServerCommand
    {
        public ListAdminsCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "listadmins";
        public override string Description => "Lists all server administrators";
        public override string Usage => "listadmins";
        public override PermissionLevel RequiredPermission => PermissionLevel.Operator;

        public override void Execute(CommandContext context)
        {
            var administrators = playerManager.Permissions.GetAdministrators();
            
            if (administrators.Count == 0)
            {
                context.Reply("No administrators configured");
                return;
            }

            context.Reply($"Server Administrators ({administrators.Count}):");
            
            foreach (var steamId in administrators)
            {
                var connectedPlayer = playerManager.GetPlayerBySteamId(steamId);
                if (connectedPlayer != null)
                {
                    context.Reply($"  {connectedPlayer.DisplayName} ({steamId}) - Online");
                }
                else
                {
                    context.Reply($"  {steamId} - Offline");
                }
            }
        }
    }
}
