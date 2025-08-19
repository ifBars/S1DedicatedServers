using FishNet;
using FishNet.Connection;
using FishNet.Managing.Server;
using MelonLoader;
using ScheduleOne.PlayerScripts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DedicatedServerMod;
using DedicatedServerMod.API;
using FishNet.Transporting;

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
            // Hook into FishNet connection events
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnRemoteConnectionState += OnClientConnectionState;
                logger.Msg("Player connection hooks established");
            }

            // Hook into player spawn/despawn events
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

                // Check if server is full
                if (connectedPlayers.Count >= ServerConfig.Instance.MaxPlayers)
                {
                    logger.Warning($"Server full, disconnecting ClientId {connection.ClientId}");
                    connection.Disconnect(true);
                    return;
                }

                // Create player info
                var playerInfo = new ConnectedPlayerInfo
                {
                    Connection = connection,
                    ConnectTime = DateTime.Now,
                    ClientId = connection.ClientId,
                    IsAuthenticated = !ServerConfig.Instance.RequireAuthentication // Auto-auth if not required
                };

                connectedPlayers[connection] = playerInfo;

                logger.Msg($"Player connected: ClientId {connection.ClientId} ({connectedPlayers.Count}/{ServerConfig.Instance.MaxPlayers})");

                // Trigger connection event
                OnPlayerJoined?.Invoke(playerInfo);
                try { ModManager.NotifyPlayerConnected(playerInfo.DisplayName ?? $"ClientId {playerInfo.ClientId}"); } catch {}
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

                    // Trigger disconnection event
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
                    logger.Msg($"Player spawned: {player.PlayerName} (ClientId {player.Owner.ClientId})");

                    // Start identity binding coroutine
                    MelonCoroutines.Start(BindPlayerIdentity(player, playerInfo));
                }
                else
                {
                    logger.Warning($"Player spawned but not found in connected players: {player.PlayerName}");
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

        /// <summary>
        /// Bind player identity (SteamID and name) once available
        /// </summary>
        private IEnumerator BindPlayerIdentity(ScheduleOne.PlayerScripts.Player player, ConnectedPlayerInfo playerInfo)
        {
            float waited = 0f;
            const float step = 0.1f;
            const float maxWait = 5f;

            while (player != null && player.gameObject != null && waited < maxWait)
            {
                try
                {
                    string steamId = player.PlayerCode;
                    if (!string.IsNullOrEmpty(steamId))
                    {
                        playerInfo.SteamId = steamId;
                        playerInfo.PlayerName = player.PlayerName;
                        logger.Msg($"Player identity bound: ClientId {playerInfo.ClientId} -> SteamID {steamId} ({player.PlayerName})");
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"Error binding player identity: {ex.Message}");
                }

                yield return new WaitForSeconds(step);
                waited += step;
            }

            // Fallback: at least set the name
            if (player != null && !string.IsNullOrEmpty(player.PlayerName))
            {
                playerInfo.PlayerName = player.PlayerName;
                logger.Msg($"Player name bound (no SteamID): ClientId {playerInfo.ClientId} -> {player.PlayerName}");
            }
        }

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
                BroadcastMessage($"Player {player.DisplayName} was kicked: {reason}");
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

                            // Add to ban list using ServerConfig
            if (!ServerConfig.Instance.BannedPlayers.Contains(player.SteamId))
            {
                ServerConfig.Instance.BannedPlayers.Add(player.SteamId);
                ServerConfig.SaveConfig(); // Save the updated config
            }

                // Kick the player
                KickPlayer(player, $"Banned: {reason}");
                
                logger.Msg($"Player banned: {player.DisplayName} ({player.SteamId}) - {reason}");
                BroadcastMessage($"Player {player.DisplayName} was banned: {reason}");
                
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
        /// Broadcast a message to all players
        /// </summary>
        public void BroadcastMessage(string message)
        {
            try
            {
                logger.Msg($"[BROADCAST] {message}");
                // TODO: Implement actual message broadcasting when RPC system is available
            }
            catch (Exception ex)
            {
                logger.Error($"Error broadcasting message: {ex}");
            }
        }

        /// <summary>
        /// Set player identity directly (for external patches)
        /// </summary>
        public void SetPlayerIdentity(NetworkConnection connection, string steamId, string playerName)
        {
            if (connection == null) return;

            if (!connectedPlayers.TryGetValue(connection, out var playerInfo))
            {
                playerInfo = new ConnectedPlayerInfo
                {
                    Connection = connection,
                    ConnectTime = DateTime.Now,
                    ClientId = connection.ClientId
                };
                connectedPlayers[connection] = playerInfo;
            }

            playerInfo.SteamId = steamId;
            playerInfo.PlayerName = playerName;
            logger.Msg($"Player identity set: ClientId {connection.ClientId} -> SteamID {steamId} ({playerName})");
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
                // Disconnect all players
                foreach (var player in connectedPlayers.Values.ToList())
                {
                    KickPlayer(player, "Server shutdown");
                }

                connectedPlayers.Clear();

                // Remove hooks
                if (InstanceFinder.ServerManager != null)
                {
                    InstanceFinder.ServerManager.OnRemoteConnectionState -= OnClientConnectionState;
                }

                // Note: Player spawn hooks are harder to remove safely, so we leave them

                logger.Msg("Player manager shutdown");
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
