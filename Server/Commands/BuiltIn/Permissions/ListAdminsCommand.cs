using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Permissions
{
    /// <summary>
    /// Command to list all administrators
    /// </summary>
    public class ListAdminsCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "listadmins";
        public override string Description => "Lists all server administrators";
        public override string Usage => "listadmins";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsGroupList;

        public override void Execute(CommandContext context)
        {
            var administrators = PlayerManager.Permissions.GetAdministrators();
            
            if (administrators.Count == 0)
            {
                context.Reply("No administrators configured");
                return;
            }

            context.Reply($"Server Administrators ({administrators.Count}):");
            
            foreach (var steamId in administrators)
            {
                var connectedPlayer = PlayerManager.GetPlayerBySteamId(steamId);
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
