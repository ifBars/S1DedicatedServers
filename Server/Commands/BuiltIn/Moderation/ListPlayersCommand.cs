using DedicatedServerMod.Server.Commands.Contracts;
using DedicatedServerMod.Server.Commands.Execution;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.Permissions;

namespace DedicatedServerMod.Server.Commands.BuiltIn.Moderation
{
    /// <summary>
    /// Command to list all connected players
    /// </summary>
    public class ListPlayersCommand(PlayerManager playerMgr) : BaseServerCommand(playerMgr)
    {
        public override string CommandWord => "listplayers";
        public override string Description => "Lists all connected players";
        public override string Usage => "listplayers";
        public override string RequiredPermissionNode => PermissionBuiltIns.Nodes.PlayerList;

        public override void Execute(CommandContext context)
        {
            var players = PlayerManager.GetConnectedPlayers();
            
            if (players.Count == 0)
            {
                context.Reply("No players currently connected");
                return;
            }

            context.Reply($"Connected players ({players.Count}/{ServerConfig.Instance.MaxPlayers}):");
            
            foreach (var player in players.OrderBy(p => p.ConnectTime))
            {
                var statusText = player.IsConnected ? "Online" : "Connecting";
                var duration = player.ConnectionDuration.ToString(@"mm\:ss");
                var highestGroup = context.Permissions?
                    .GetEffectiveGroups(player.TrustedUniqueId)
                    .FirstOrDefault(groupName => !string.Equals(groupName, PermissionBuiltIns.Groups.Default, StringComparison.OrdinalIgnoreCase));
                var roleText = string.IsNullOrWhiteSpace(highestGroup)
                    ? string.Empty
                    : $" [{highestGroup.Replace('-', ' ').ToUpperInvariant()}]";

                context.Reply($"  {player.DisplayName}{roleText} - {statusText} ({duration})");
            }
        }
    }
}
