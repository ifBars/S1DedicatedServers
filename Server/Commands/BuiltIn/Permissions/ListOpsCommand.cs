using System;
using System.Collections.Generic;
using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Permissions
{
    /// <summary>
    /// Command to list all operators
    /// </summary>
    public class ListOpsCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "listops";
        public override string Description => "Lists all server operators";
        public override string Usage => "listops";
        public override string RequiredPermissionNode => Shared.Permissions.PermissionBuiltIns.Nodes.PermissionsGroupList;

        public override void Execute(CommandContext context)
        {
            IReadOnlyList<string> operators = context.Permissions?.GetDirectUsersInGroup(PermissionBuiltIns.Groups.Operator) ?? Array.Empty<string>();
            
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
