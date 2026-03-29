using MelonLoader;
using System.Linq;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Command to list all operators
    /// </summary>
    public class ListOpsCommand : BaseServerCommand
    {
        public ListOpsCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "listops";
        public override string Description => "Lists all server operators";
        public override string Usage => "listops";
        public override string RequiredPermissionNode => DedicatedServerMod.Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsGroupList;

        public override void Execute(CommandContext context)
        {
            var operators = PlayerManager.Permissions.GetOperators();
            
            if (operators.Count == 0)
            {
                context.Reply("No operators configured");
                return;
            }

            context.Reply($"Server Operators ({operators.Count}):");
            
            foreach (var steamId in operators)
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
