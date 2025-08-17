using FishNet;
using FishNet.Connection;
using FishNet.Managing.Server;
using MelonLoader;
using ScheduleOne.PlayerScripts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;
using System.Collections;

namespace DedicatedServerMod
{
    /// <summary>
    /// Manages dedicated server operations including client connections,
    /// player management, and basic server administration.
    /// </summary>
    public static class ServerManager
    {
        private static MelonLogger.Instance logger = new MelonLogger.Instance("ServerManager");
        
        // Server configuration
        public static int MaxPlayers { get; set; } = 16;
        public static string ServerName { get; set; } = "Schedule One Dedicated Server";
        public static string ServerPassword { get; set; } = "";
        public static bool RequireAuthentication { get; set; } = false;
        public static bool RequireFriends { get; set; } = false;
        
        // Connected players tracking
        private static Dictionary<NetworkConnection, ConnectedPlayer> connectedPlayers = new Dictionary<NetworkConnection, ConnectedPlayer>();
        
        /// <summary>
        /// Information about a connected player
        /// </summary>
        public class ConnectedPlayer
        {
            public NetworkConnection Connection { get; set; }
            public string SteamId { get; set; }
            public string PlayerName { get; set; }
            public DateTime ConnectTime { get; set; }
            public Player PlayerInstance { get; set; }
            public bool IsAuthenticated { get; set; }
        }

        /// <summary>
        /// Initialize server manager - should be called when server starts
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Hook into FishNet server events
                if (InstanceFinder.ServerManager != null)
                {
                    InstanceFinder.ServerManager.OnServerConnectionState += OnServerConnectionState;
                    InstanceFinder.ServerManager.OnRemoteConnectionState += OnClientConnectionState;
                    logger.Msg("ServerManager initialized with FishNet event hooks");
                }
                else
                {
                    logger.Warning("ServerManager could not find FishNet ServerManager");
                }

                // Subscribe to player lifecycle to capture SteamIDs and names as they arrive
                Player.onPlayerSpawned = (Action<Player>)Delegate.Remove(Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned));
                Player.onPlayerSpawned = (Action<Player>)Delegate.Combine(Player.onPlayerSpawned, new Action<Player>(OnPlayerSpawned));
                Player.onPlayerDespawned = (Action<Player>)Delegate.Remove(Player.onPlayerDespawned, new Action<Player>(OnPlayerDespawned));
                Player.onPlayerDespawned = (Action<Player>)Delegate.Combine(Player.onPlayerDespawned, new Action<Player>(OnPlayerDespawned));
            }
            catch (Exception ex)
            {
                logger.Error($"Error initializing ServerManager: {ex}");
            }
        }

        /// <summary>
        /// Called when server connection state changes
        /// </summary>
        private static void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            logger.Msg($"Server connection state changed: {args.ConnectionState}");
            
            switch (args.ConnectionState)
            {
                case FishNet.Transporting.LocalConnectionState.Started:
                    logger.Msg("=== DEDICATED SERVER ONLINE ===");
                    break;
                case FishNet.Transporting.LocalConnectionState.Stopped:
                    logger.Msg("=== DEDICATED SERVER OFFLINE ===");
                    connectedPlayers.Clear();
                    break;
            }
        }

        /// <summary>
        /// Called when a client connection state changes
        /// </summary>
        private static void OnClientConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case FishNet.Transporting.RemoteConnectionState.Started:
                    OnClientConnected(conn);
                    break;
                case FishNet.Transporting.RemoteConnectionState.Stopped:
                    OnClientDisconnected(conn);
                    break;
            }
        }

        /// <summary>
        /// Handle new client connection
        /// </summary>
        private static void OnClientConnected(NetworkConnection connection)
        {
            try
            {
                logger.Msg($"Client connected: {connection.ClientId}");
                
                // Check if server is full
                if (connectedPlayers.Count >= MaxPlayers)
                {
                    logger.Warning($"Server full, disconnecting client {connection.ClientId}");
                    connection.Disconnect(true);
                    return;
                }

                // Create player record
                var connectedPlayer = new ConnectedPlayer
                {
                    Connection = connection,
                    ConnectTime = DateTime.Now,
                    IsAuthenticated = !RequireAuthentication // Auto-auth if not required
                };

                connectedPlayers[connection] = connectedPlayer;
                
                logger.Msg($"Client {connection.ClientId} added to player list ({connectedPlayers.Count}/{MaxPlayers})");
                
                // Broadcast connection message to other players
                BroadcastMessage($"Player {connection.ClientId} joined the server");
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling client connection: {ex}");
            }
        }

        /// <summary>
        /// Handle client disconnection
        /// </summary>
        private static void OnClientDisconnected(NetworkConnection connection)
        {
            try
            {
                if (connectedPlayers.TryGetValue(connection, out var player))
                {
                    logger.Msg($"Client disconnected: {connection.ClientId} ({player.PlayerName ?? "Unknown"})");
                    connectedPlayers.Remove(connection);
                    
                    // Broadcast disconnection message
                    BroadcastMessage($"Player {player.PlayerName ?? connection.ClientId.ToString()} left the server");
                }
                else
                {
                    logger.Msg($"Unknown client disconnected: {connection.ClientId}");
                }
                
                logger.Msg($"Current players: {connectedPlayers.Count}/{MaxPlayers}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error handling client disconnection: {ex}");
            }
        }

        /// <summary>
        /// Player spawned; bind Player instance and resolve SteamID/Name once available.
        /// </summary>
        private static void OnPlayerSpawned(Player player)
        {
            try
            {
                if (player == null || player.Owner == null)
                    return;

                if (!connectedPlayers.TryGetValue(player.Owner, out var cp))
                {
                    cp = new ConnectedPlayer { Connection = player.Owner, ConnectTime = DateTime.Now };
                    connectedPlayers[player.Owner] = cp;
                }
                cp.PlayerInstance = player;

                // Start a short polling coroutine to capture SteamID (PlayerCode) once RPC sets it
                MelonCoroutines.Start(WaitAndBindPlayerIdentity(player));
            }
            catch (Exception ex)
            {
                logger.Error($"Error in OnPlayerSpawned: {ex}");
            }
        }

        /// <summary>
        /// Player despawned; clear instance reference.
        /// </summary>
        private static void OnPlayerDespawned(Player player)
        {
            try
            {
                if (player == null || player.Owner == null)
                    return;
                if (connectedPlayers.TryGetValue(player.Owner, out var cp))
                {
                    cp.PlayerInstance = null;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in OnPlayerDespawned: {ex}");
            }
        }

        private static IEnumerator WaitAndBindPlayerIdentity(Player player)
        {
            // Poll up to ~5 seconds total
            float waited = 0f;
            const float step = 0.1f;
            while (player != null && player.gameObject != null && waited < 5f)
            {
                try
                {
                    string sid = player.PlayerCode; // SteamID as string when available
                    if (!string.IsNullOrEmpty(sid))
                    {
                        if (connectedPlayers.TryGetValue(player.Owner, out var cp))
                        {
                            cp.SteamId = sid;
                            cp.PlayerName = player.PlayerName;
                            logger.Msg($"[ID MAP] Bound ClientId {player.Owner.ClientId} -> SteamID {sid} ({player.PlayerName})");
                        }
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"WaitAndBindPlayerIdentity error: {ex.Message}");
                }
                yield return new WaitForSeconds(step);
                waited += step;
            }
            // If not resolved, keep name at least
            if (player != null && connectedPlayers.TryGetValue(player.Owner, out var cp2))
            {
                cp2.PlayerName = player.PlayerName;
            }
        }

        /// <summary>
        /// Allows external patches (e.g., Harmony on ReceivePlayerNameData) to set identity immediately.
        /// </summary>
        public static void SetPlayerIdentity(NetworkConnection conn, string steamId, string playerName)
        {
            if (conn == null) return;
            if (!connectedPlayers.TryGetValue(conn, out var cp))
            {
                cp = new ConnectedPlayer { Connection = conn, ConnectTime = DateTime.Now };
                connectedPlayers[conn] = cp;
            }
            cp.SteamId = steamId;
            cp.PlayerName = playerName;
            logger.Msg($"[ID MAP] (direct) ClientId {conn.ClientId} -> SteamID {steamId} ({playerName})");
        }

        /// <summary>
        /// Broadcast a message to all connected clients
        /// </summary>
        public static void BroadcastMessage(string message)
        {
            try
            {
                logger.Msg($"[BROADCAST] {message}");
                
                // In a full implementation, this would send the message to all clients
                // For now, just log it as the prototype doesn't include chat system
                
                // TODO: Implement actual message broadcasting when custom RPC system is added
            }
            catch (Exception ex)
            {
                logger.Error($"Error broadcasting message: {ex}");
            }
        }

        /// <summary>
        /// Get information about all connected players
        /// </summary>
        public static List<ConnectedPlayer> GetConnectedPlayers()
        {
            return new List<ConnectedPlayer>(connectedPlayers.Values);
        }

        /// <summary>
        /// Get connected player by connection
        /// </summary>
        public static ConnectedPlayer GetPlayer(NetworkConnection connection)
        {
            connectedPlayers.TryGetValue(connection, out var player);
            return player;
        }

        /// <summary>
        /// Kick a player from the server
        /// </summary>
        public static bool KickPlayer(ConnectedPlayer connectedPlayer, string reason = "Kicked by admin")
        {
            try
            {
                logger.Msg($"Kicking player {connectedPlayer.PlayerInstance.PlayerName ?? connectedPlayer.Connection.ClientId.ToString()}: {reason}");
                connectedPlayer.Connection.Disconnect(true);
                BroadcastMessage($"Player {connectedPlayer.PlayerInstance.PlayerName ?? connectedPlayer.Connection.ClientId.ToString()} was kicked: {reason}");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error kicking player: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Get server statistics
        /// </summary>
        public static string GetServerStats()
        {
            try
            {
                var stats = "=== Server Statistics ===\n";
                stats += $"Server Name: {ServerName}\n";
                stats += $"Connected Players: {connectedPlayers.Count}/{MaxPlayers}\n";
                stats += $"Server Uptime: {DateTime.Now.Subtract(Process.GetCurrentProcess().StartTime):hh\\:mm\\:ss}\n";
                stats += $"Authentication Required: {RequireAuthentication}\n";
                stats += $"Friends Only: {RequireFriends}\n";
                
                if (connectedPlayers.Count > 0)
                {
                    stats += "\n=== Connected Players ===\n";
                    foreach (var player in connectedPlayers.Values)
                    {
                        var uptime = DateTime.Now.Subtract(player.ConnectTime);
                        stats += $"- {player.PlayerName ?? $"Client {player.Connection.ClientId}"} " +
                               $"(Connected: {uptime:mm\\:ss}, Auth: {player.IsAuthenticated})\n";
                    }
                }

                return stats;
            }
            catch (Exception ex)
            {
                logger.Error($"Error getting server stats: {ex}");
                return "Error retrieving server statistics";
            }
        }

        /// <summary>
        /// Handle authentication ticket from client (placeholder for future implementation)
        /// </summary>
        public static bool AuthenticatePlayer(ConnectedPlayer connectedPlayer, string steamTicket)
        {
            try
            {
                if (!RequireAuthentication)
                {
                    return true; // No auth required
                }

                // TODO: Implement Steam ticket validation here
                // This would verify the ticket with Steam's servers
                logger.Msg($"TODO: Authenticate player {connectedPlayer.Connection.ClientId} with ticket: {steamTicket?.Substring(0, 8)}...");
                
                if (connectedPlayers.TryGetValue(connectedPlayer.Connection, out var player))
                {
                    player.IsAuthenticated = true;
                    player.SteamId = "extracted_from_ticket"; // Would extract from validated ticket
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error($"Error authenticating player: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Save server configuration
        /// </summary>
        public static void SaveConfiguration()
        {
            try
            {
                // TODO: Implement configuration saving to file
                logger.Msg("TODO: Save server configuration to file");
            }
            catch (Exception ex)
            {
                logger.Error($"Error saving configuration: {ex}");
            }
        }

        /// <summary>
        /// Load server configuration
        /// </summary>
        public static void LoadConfiguration()
        {
            try
            {
                // TODO: Implement configuration loading from file
                logger.Msg("TODO: Load server configuration from file");
            }
            catch (Exception ex)
            {
                logger.Error($"Error loading configuration: {ex}");
            }
        }

        /// <summary>
        /// Set the friends check requirement
        /// </summary>
        public static void SetFriendsRequired(bool required)
        {
            RequireFriends = required;
            logger.Msg($"Friends check {(required ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Check if a player should be allowed based on friends requirement
        /// </summary>
        public static bool ShouldAllowPlayer(ulong playerId, ulong hostId)
        {
            // If friends not required, allow everyone
            if (!RequireFriends)
            {
                return true;
            }

            try
            {
                // Check if Steam is available
                if (!SteamManager.Initialized)
                {
                    logger.Warning("Steam not initialized, allowing player");
                    return true;
                }

                // Check friendship status
                var playerSteamId = new CSteamID(playerId);
                var friendRelationship = SteamFriends.GetFriendRelationship(playerSteamId);
                
                bool isFriend = friendRelationship == EFriendRelationship.k_EFriendRelationshipFriend;
                logger.Msg($"Player {playerId} friendship check: {friendRelationship} (allowed: {isFriend})");
                
                return isFriend;
            }
            catch (Exception ex)
            {
                logger.Error($"Error checking friendship for player {playerId}: {ex}");
                // Default to allow on error to prevent breaking the game
                return true;
            }
        }

        /// <summary>
        /// Shutdown the server gracefully
        /// </summary>
        public static void Shutdown(string reason = "Server shutdown")
        {
            try
            {
                logger.Msg($"Shutting down server: {reason}");
                
                // Notify all players
                BroadcastMessage($"Server shutting down: {reason}");
                
                // Disconnect all clients
                foreach (var player in connectedPlayers.Values)
                {
                    player.Connection.Disconnect(true);
                }
                
                // Stop server
                if (InstanceFinder.ServerManager != null)
                {
                    InstanceFinder.ServerManager.StopConnection(true);
                }
                
                connectedPlayers.Clear();
                logger.Msg("Server shutdown complete");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during server shutdown: {ex}");
            }
        }
    }
}
