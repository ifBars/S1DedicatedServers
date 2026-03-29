using MelonLoader;
using System;
using DedicatedServerMod;
using DedicatedServerMod.Server.Network;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Configuration;

namespace DedicatedServerMod.Server.Commands.Server
{
    /// <summary>
    /// Command to display server information and statistics
    /// </summary>
    public class ServerInfoCommand : BaseServerCommand
    {
        private readonly NetworkManager _networkManager;

        public ServerInfoCommand(MelonLogger.Instance logger, PlayerManager playerMgr, NetworkManager networkManager)
            : base(logger, playerMgr)
        {
            _networkManager = networkManager;
        }

        public override string CommandWord => "serverinfo";
        public override string Description => "Displays server information and statistics";
        public override string Usage => "serverinfo";
        public override string RequiredPermissionNode => DedicatedServerMod.Shared.Permissions.PermissionBuiltIns.Nodes.ServerInfo;

        public override void Execute(CommandContext context)
        {
            try
            {
                bool isRunning = _networkManager?.IsServerRunning ?? false;
                TimeSpan uptime = _networkManager?.Uptime ?? TimeSpan.Zero;
                var playerStats = PlayerManager.GetPlayerStats();
                var permissionSummary = context.Permissions?.GetSummary();

                context.Reply("=== Server Information ===");
                context.Reply($"Server Name: {ServerConfig.Instance.ServerName}");
                context.Reply($"Status: {(isRunning ? "Running" : "Stopped")}");
                context.Reply($"Port: {ServerConfig.Instance.ServerPort}");
                context.Reply($"Players: {playerStats.ConnectedPlayers}/{ServerConfig.Instance.MaxPlayers}");
                
                if (isRunning)
                {
                    context.Reply($"Uptime: {uptime:hh\\:mm\\:ss}");
                }

                context.Reply($"Authentication: {(ServerConfig.Instance.AuthenticationEnabled ? ServerConfig.Instance.AuthProvider.ToString() : "Disabled")}");
                context.Reply($"Auth Timeout: {ServerConfig.Instance.AuthTimeoutSeconds}s");
                context.Reply($"Auto-Save: {(ServerConfig.Instance.AutoSaveEnabled ? $"Enabled ({ServerConfig.Instance.AutoSaveIntervalMinutes}m)" : "Disabled")}");
                
                context.Reply("");
                context.Reply("=== Permissions ===");
                context.Reply($"Groups: {permissionSummary?.TotalGroups ?? 0}");
                context.Reply($"Users With Direct Rules: {permissionSummary?.TotalUsers ?? 0}");
                context.Reply($"Banned Players: {permissionSummary?.TotalBans ?? 0}");
                context.Reply($"Operators: {permissionSummary?.TotalOperators ?? 0}");
                context.Reply($"Administrators: {permissionSummary?.TotalAdministrators ?? 0}");

                if (_networkManager != null)
                {
                    var networkStats = _networkManager.GetNetworkStats();
                    context.Reply("");
                    context.Reply("=== Network ===");
                    context.Reply($"Transport: {(networkStats.IsServerRunning ? "Tugboat (Dedicated)" : "Offline")}");
                    context.Reply($"Port: {networkStats.ServerPort}");
                }
            }
            catch (Exception ex)
            {
                context.ReplyError($"Error retrieving server information: {ex.Message}");
                Logger.Error($"Error in serverinfo command: {ex}");
            }
        }
    }
}
