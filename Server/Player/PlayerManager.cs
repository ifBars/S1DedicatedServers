using FishNet;
using FishNet.Connection;
using FishNet.Managing.Server;
using MelonLoader;
using ScheduleOne.PlayerScripts;
using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod;
using DedicatedServerMod.API;
using FishNet.Transporting;
using DedicatedServerMod.Shared.Configuration;

namespace DedicatedServerMod.Server.Player
{
    /// <summary>
    /// Manages player connections, authentication, and player lifecycle.
    /// Handles player joining/leaving, permissions, and player data tracking.
    /// </summary>
    public class PlayerManager
    {
        private readonly MelonLogger.Instance logger;
        private readonly Dictionary<NetworkConnection, ConnectedPlayerInfo> connectedPlayers;
        private readonly PlayerAuthentication authentication;
        private readonly PlayerPermissions permissions;

        public PlayerManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            connectedPlayers = new Dictionary<NetworkConnection, ConnectedPlayerInfo>();
            authentication = new PlayerAuthentication(logger);
            permissions = new PlayerPermissions(logger);
        }

        /// <summary>
        /// Gets the number of connected players
        /// </summary>
        public int ConnectedPlayerCount => connectedPlayers.Count;

        /// <summary>
        /// Gets the player authentication manager
        /// </summary>
        public PlayerAuthentication Authentication => authentication;

        /// <summary>
        /// Gets the player permissions manager
        /// </summary>
        public PlayerPermissions Permissions => permissions;

        /// <summary>
        /// Initialize the player manager
        /// </summary>
        public void Initialize()
        {
            try
            {
                SetupPlayerHooks();
                authentication.Initialize();
                permissions.Initialize();
                logger.Msg("Player manager initialized");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to initialize player manager: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Setup player lifecycle event hooks
        /// </summary>
        private void SetupPlayerHooks()
        {
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnRemoteConnectionState += OnClientConnectionState;
                logger.Msg("Player connection hooks established");
            }

            try
            {
                ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(ScheduleOne.PlayerScripts.Player.onPlayerSpawned, HandleOnPlayerSpawned);
                ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Combine(ScheduleOne.PlayerScripts.Player.onPlayerSpawned, HandleOnPlayerSpawned);
                ScheduleOne.PlayerScripts.Player.onPlayerDespawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(ScheduleOne.PlayerScripts.Player.onPlayerDespawned, OnPlayerDespawned);
                ScheduleOne.PlayerScripts.Player.onPlayerDespawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Combine(ScheduleOne.PlayerScripts.Player.onPlayerDespawned, OnPlayerDespawned);
                logger.Msg("Player spawn hooks established");
            }
            catch (Exception ex)
            {
                logger.Warning($"Could not establish player spawn hooks: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle client connection state changes
        /// </summary>
        private void OnClientConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case RemoteConnectionState.Started:
                    HandlePlayerConnected(conn);
                    break;
                case RemoteConnectionState.Stopped:
                    HandlePlayerDisconnected(conn);
                    break;
            }
        }

        /// <summary>
        /// Handle new player connection
        /// </summary>
        private void HandlePlayerConnected(NetworkConnection connection)
        {
            try
            {
                logger.Msg($"Player connecting: ClientId {connection.ClientId}");
                
                // Skip if already exists (can happen with ghost host)
                if (connectedPlayers.ContainsKey(connection))
                {
                    logger.Msg($"Player already tracked: ClientId {connection.ClientId}");
                    return;
                }

                if (connectedPlayers.Count >= ServerConfig.Instance.MaxPlayers)
                {
                    logger.Warning($"Server full, disconnecting ClientId {connection.ClientId}");
                    connection.Disconnect(true);
                    return;
                }
                
                var playerInfo = new ConnectedPlayerInfo
                {
                    Connection = connection,
                    ConnectTime = DateTime.Now,
                    ClientId = connection.ClientId,
                    IsAuthenticated = false // Will be set to true after authentication
                };

                connectedPlayers[connection] = playerInfo;
                logger.Msg($"Player connected: ClientId {connection.ClientId} ({connectedPlayers.Count}/{ServerConfig.Instance.MaxPlayers})");
                OnPlayerJoined?.Invoke(playerInfo);
                try { ModManager.NotifyPlayerConnected(playerInfo.DisplayName ?? $"ClientId {playerInfo.ClientId}"); } catch {}

                // NOTE: Authentication challenge is now sent when client sends "client_ready" message
                // This ensures the client's RPC handlers are registered before we send the challenge

                // If no password required and no Steam auth required, auto-authenticate
                if (!authentication.RequiresPassword() && !ServerConfig.Instance.RequireAuthentication)
                {
                    playerInfo.IsAuthenticated = true;
                    logger.Msg($"Auto-authenticated ClientId {connection.ClientId} (no password or Steam auth required)");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling player connection: {ex}");
            }
        }

        /// <summary>
        /// Handle player disconnection
        /// </summary>
        private void HandlePlayerDisconnected(NetworkConnection connection)
        {
            try
            {
                if (connectedPlayers.TryGetValue(connection, out var playerInfo))
                {
                    logger.Msg($"Player disconnected: {playerInfo.DisplayName} (ClientId {connection.ClientId})");
                    connectedPlayers.Remove(connection);
                    OnPlayerLeft?.Invoke(playerInfo);
                    try { ModManager.NotifyPlayerDisconnected(playerInfo.DisplayName ?? $"ClientId {playerInfo.ClientId}"); } catch {}

                    logger.Msg($"Current players: {connectedPlayers.Count}/{ServerConfig.Instance.MaxPlayers}");
                }
                else
                {
                    logger.Msg($"Unknown player disconnected: ClientId {connection.ClientId}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling player disconnection: {ex}");
            }
        }

        /// <summary>
        /// Handle player spawned
        /// </summary>
        private void HandleOnPlayerSpawned(ScheduleOne.PlayerScripts.Player player)
        {
            try
            {
                if (player == null || player.Owner == null)
                    return;

                if (connectedPlayers.TryGetValue(player.Owner, out var playerInfo))
                {
                    playerInfo.PlayerInstance = player;
                    // Identity binding happens via DedicatedServerPatches.BindPlayerIdentityPostfix
                    // which calls SetPlayerIdentity when player name data is received
                    logger.Msg($"Player spawned: {player.PlayerName} (ClientId {player.Owner.ClientId}) - awaiting identity");
                }
                else
                {
                    // Only log warning if this is not the initial spawn with default values
                    if (!string.IsNullOrEmpty(player.PlayerCode) || player.PlayerName != "Player")
                    {
                        logger.Warning($"Player spawned but not found in connected players: {player.PlayerName}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in OnPlayerSpawned: {ex}");
            }
        }

        /// <summary>
        /// Handle player despawned
        /// </summary>
        private void OnPlayerDespawned(ScheduleOne.PlayerScripts.Player player)
        {
            try
            {
                if (player == null || player.Owner == null)
                    return;

                if (connectedPlayers.TryGetValue(player.Owner, out var playerInfo))
                {
                    playerInfo.PlayerInstance = null;
                    logger.Msg($"Player despawned: {player.PlayerName} (ClientId {player.Owner.ClientId})");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in OnPlayerDespawned: {ex}");
            }
        }

        // NOTE: Identity binding is now handled by DedicatedServerPatches.BindPlayerIdentityPostfix
        // which is called when the game receives player name data and calls SetPlayerIdentity

        /// <summary>
        /// Get all connected players
        /// </summary>
        public List<ConnectedPlayerInfo> GetConnectedPlayers()
        {
            return new List<ConnectedPlayerInfo>(connectedPlayers.Values);
        }

        /// <summary>
        /// Get player by connection
        /// </summary>
        public ConnectedPlayerInfo GetPlayer(NetworkConnection connection)
        {
            connectedPlayers.TryGetValue(connection, out var player);
            return player;
        }

        /// <summary>
        /// Get player by SteamID
        /// </summary>
        public ConnectedPlayerInfo GetPlayerBySteamId(string steamId)
        {
            return connectedPlayers.Values.FirstOrDefault(p => p.SteamId == steamId);
        }

        /// <summary>
        /// Get player by name (partial match)
        /// </summary>
        public ConnectedPlayerInfo GetPlayerByName(string name)
        {
            return connectedPlayers.Values.FirstOrDefault(p => 
                p.PlayerName?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// Kick a player from the server
        /// </summary>
        public bool KickPlayer(ConnectedPlayerInfo player, string reason = "Kicked by admin")
        {
            try
            {
                logger.Msg($"Kicking player {player.DisplayName}: {reason}");
                player.Connection.Disconnect(true);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error kicking player: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Ban a player
        /// </summary>
        public bool BanPlayer(ConnectedPlayerInfo player, string reason = "Banned by admin")
        {
            try
            {
                if (string.IsNullOrEmpty(player.SteamId))
                {
                    logger.Warning("Cannot ban player without SteamID");
                    return false;
                }

                if (!ServerConfig.Instance.BannedPlayers.Contains(player.SteamId))
                {
                    ServerConfig.Instance.BannedPlayers.Add(player.SteamId);
                    ServerConfig.SaveConfig();
                }

                // Kick the player
                KickPlayer(player, $"Banned: {reason}");
                logger.Msg($"Player banned: {player.DisplayName} ({player.SteamId}) - {reason}");
                
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error banning player: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Check if a player is banned
        /// </summary>
        public bool IsPlayerBanned(string steamId)
        {
            return ServerConfig.Instance.BannedPlayers.Contains(steamId);
        }

        /// <summary>
        /// Set player identity directly (for external patches)
        /// </summary>
        /// <remarks>
        /// This can be called before OnRemoteConnectionState fires, so it may need to create
        /// the player entry. This happens because player name data arrives before the connection
        /// event in some cases.
        /// </remarks>
        public void SetPlayerIdentity(NetworkConnection connection, string steamId, string playerName)
        {
            if (connection == null)
            {
                logger.Warning("SetPlayerIdentity called with null connection");
                return;
            }

            bool isNewPlayer = false;
            if (!connectedPlayers.TryGetValue(connection, out var playerInfo))
            {
                // Player name data can arrive before the connection event fires
                // Create the entry now - HandlePlayerConnected will skip if it already exists
                playerInfo = new ConnectedPlayerInfo
                {
                    Connection = connection,
                    ConnectTime = DateTime.Now,
                    ClientId = connection.ClientId,
                    IsAuthenticated = false
                };
                connectedPlayers[connection] = playerInfo;
                isNewPlayer = true;
                logger.Msg($"Created player entry from identity binding: ClientId {connection.ClientId}");
            }
            else
            {
                // Check if we're trying to overwrite a valid identity with default/empty values
                // This happens when clients spawn with temporary ClientId 0 / "Player" before getting real values
                bool isDefaultIdentity = string.IsNullOrEmpty(steamId) && playerName == "Player";
                bool hasValidIdentity = !string.IsNullOrEmpty(playerInfo.SteamId) || 
                                       (!string.IsNullOrEmpty(playerInfo.PlayerName) && playerInfo.PlayerName != "Player");
                
                if (isDefaultIdentity && hasValidIdentity)
                {
                    logger.Msg($"Skipping default identity update for ClientId {connection.ClientId} - already has valid identity: {playerInfo.DisplayName}");
                    return;
                }
            }

            playerInfo.SteamId = steamId;
            playerInfo.PlayerName = playerName;
            logger.Msg($"Player identity set: ClientId {connection.ClientId} -> SteamID {steamId} ({playerName})");
            
            // NOTE: Authentication is now handled via client_ready message, not here
            
            OnPlayerSpawned?.Invoke(playerInfo);
        }

        /// <summary>
        /// Get player statistics
        /// </summary>
        public PlayerStats GetPlayerStats()
        {
            var stats = new PlayerStats
            {
                ConnectedPlayers = connectedPlayers.Count,
                MaxPlayers = ServerConfig.Instance.MaxPlayers,
                TotalBannedPlayers = ServerConfig.Instance.BannedPlayers.Count,
                Players = GetConnectedPlayers()
            };

            return stats;
        }

        /// <summary>
        /// Shutdown the player manager
        /// </summary>
        public void Shutdown()
        {
            try
            {
                logger.Msg($"Shutting down player manager - {connectedPlayers.Count} players to disconnect");
                
                // Create a snapshot to avoid modification during iteration
                var playersToKick = connectedPlayers.Values.ToList();
                
                // Disconnect all players
                foreach (var player in playersToKick)
                {
                    try
                    {
                        // Skip if already disconnected
                        if (player.Connection == null || !player.Connection.IsActive)
                        {
                            logger.Msg($"Skipping already disconnected player: {player.DisplayName}");
                            continue;
                        }
                        
                        KickPlayer(player, "Server shutdown");
                    }
                    catch (Exception ex)
                    {
                        logger.Warning($"Error kicking player {player.DisplayName} during shutdown: {ex.Message}");
                    }
                }

                connectedPlayers.Clear();

                // Remove hooks
                if (InstanceFinder.ServerManager != null)
                {
                    InstanceFinder.ServerManager.OnRemoteConnectionState -= OnClientConnectionState;
                }

                // Note: Player spawn hooks are harder to remove safely, so we leave them

                logger.Msg("Player manager shutdown complete");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during player manager shutdown: {ex}");
            }
        }

        // Events
        public event Action<ConnectedPlayerInfo> OnPlayerJoined;
        public event Action<ConnectedPlayerInfo> OnPlayerLeft;
        public event Action<ConnectedPlayerInfo> OnPlayerSpawned;
    }

    /// <summary>
    /// Player statistics information
    /// </summary>
    public class PlayerStats
    {
        public int ConnectedPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public int TotalBannedPlayers { get; set; }
        public List<ConnectedPlayerInfo> Players { get; set; }

        public override string ToString()
        {
            return $"Players: {ConnectedPlayers}/{MaxPlayers} | Banned: {TotalBannedPlayers}";
        }
    }
}
