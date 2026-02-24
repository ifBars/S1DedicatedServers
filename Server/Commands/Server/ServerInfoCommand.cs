using MelonLoader;
using System;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Server.Core;
using DedicatedServerMod;
using DedicatedServerMod.Shared.Configuration;

namespace DedicatedServerMod.Server.Commands.Server
{
    /// <summary>
    /// Command to display server information and statistics
    /// </summary>
    public class ServerInfoCommand : BaseServerCommand
    {
        public ServerInfoCommand(MelonLogger.Instance logger, PlayerManager playerMgr) 
            : base(logger, playerMgr)
        {
        }

        public override string CommandWord => "serverinfo";
        public override string Description => "Displays server information and statistics";
        public override string Usage => "serverinfo";
        public override PermissionLevel RequiredPermission => PermissionLevel.Player;

        public override void Execute(CommandContext context)
        {
            try
            {
                var status = ServerBootstrap.GetStatus();
                var playerStats = PlayerManager.GetPlayerStats();
                var permissionSummary = PlayerManager.Permissions.GetPermissionSummary();

                context.Reply("=== Server Information ===");
                context.Reply($"Server Name: {ServerConfig.Instance.ServerName}");
                context.Reply($"Status: {(status.IsRunning ? "Running" : "Stopped")}");
                context.Reply($"Port: {ServerConfig.Instance.ServerPort}");
                context.Reply($"Players: {playerStats.ConnectedPlayers}/{ServerConfig.Instance.MaxPlayers}");
                
                if (status.IsRunning)
                {
                    context.Reply($"Uptime: {status.Uptime:hh\\:mm\\:ss}");
                }

                context.Reply($"Authentication Required: {ServerConfig.Instance.RequireAuthentication}");
                context.Reply($"Authentication Provider: {ServerConfig.Instance.AuthProvider}");
                context.Reply($"Auth Timeout: {ServerConfig.Instance.AuthTimeoutSeconds}s");
                context.Reply($"Friends Only: {ServerConfig.Instance.RequireFriends}");
                context.Reply($"Auto-Save: {(ServerConfig.Instance.AutoSaveEnabled ? $"Enabled ({ServerConfig.Instance.AutoSaveIntervalMinutes}m)" : "Disabled")}");
                
                context.Reply("");
                context.Reply("=== Permissions ===");
                context.Reply($"Operators: {permissionSummary.TotalOperators}");
                context.Reply($"Administrators: {permissionSummary.TotalAdministrators}");
                context.Reply($"Banned Players: {ServerConfig.Instance.BannedPlayers.Count}");

                if (ServerBootstrap.Network != null)
                {
                    var networkStats = ServerBootstrap.Network.GetNetworkStats();
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
