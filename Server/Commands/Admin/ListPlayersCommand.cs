using MelonLoader;
using System.Linq;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod;

namespace DedicatedServerMod.Server.Commands.Admin
{
    /// <summary>
    /// Command to list all connected players
    /// </summary>
    public class ListPlayersCommand : BaseServerCommand
    {
        public ListPlayersCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "listplayers";
        public override string Description => "Lists all connected players";
        public override string Usage => "listplayers";
        public override PermissionLevel RequiredPermission => PermissionLevel.Administrator;

        public override void Execute(CommandContext context)
        {
            var players = playerManager.GetConnectedPlayers();
            
            if (players.Count == 0)
            {
                context.Reply("No players currently connected");
                return;
            }

            context.Reply($"Connected players ({players.Count}/{ServerConfig.Instance.MaxPlayers}):");
            
            foreach (var player in players.OrderBy(p => p.ConnectTime))
            {
                var permLevel = playerManager.Permissions.GetPermissionLevel(player);
                var permText = permLevel switch
                {
                    PermissionLevel.Administrator => " [ADMIN]",
                    PermissionLevel.Operator => " [OP]",
                    _ => ""
                };

                var statusText = player.IsConnected ? "Online" : "Connecting";
                var duration = player.ConnectionDuration.ToString(@"mm\:ss");
                
                context.Reply($"  {player.DisplayName}{permText} - {statusText} ({duration})");
            }
        }
    }
}
