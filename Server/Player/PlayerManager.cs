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
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using Newtonsoft.Json;

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
                authentication.AuthenticationCompleted += OnAuthenticationCompleted;
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

                bool isExistingPlayer = connectedPlayers.TryGetValue(connection, out var playerInfo);
                if (!isExistingPlayer && connectedPlayers.Count >= ServerConfig.Instance.MaxPlayers)
                {
                    logger.Warning($"Server full, disconnecting ClientId {connection.ClientId}");
                    connection.Disconnect(true);
                    return;
                }

                if (!isExistingPlayer)
                {
                    playerInfo = new ConnectedPlayerInfo
                    {
                        Connection = connection,
                        ConnectTime = DateTime.Now,
                        ClientId = connection.ClientId,
                        IsLoopbackConnection = IsLoopbackConnection(connection),
                        IsAuthenticated = false,
                        IsAuthenticationPending = false
                    };

                    connectedPlayers[connection] = playerInfo;
                }
                else
                {
                    playerInfo.Connection = connection;
                    playerInfo.ClientId = connection.ClientId;
                    playerInfo.IsLoopbackConnection = IsLoopbackConnection(connection);
                }

                logger.Msg($"Player tracked: ClientId {connection.ClientId} ({connectedPlayers.Count}/{ServerConfig.Instance.MaxPlayers})");

                bool requiresAuthentication = authentication.IsAuthenticationRequiredForPlayer(playerInfo);
                if (!requiresAuthentication)
                {
                    AuthenticationResult bypassResult = new AuthenticationResult
                    {
                        IsSuccessful = true,
                        Message = playerInfo.IsLoopbackConnection
                            ? "Loopback connection bypassed authentication"
                            : "Authentication not required"
                    };

                    playerInfo.IsAuthenticated = true;
                    playerInfo.IsAuthenticationPending = false;

                    SendAuthResultToClient(playerInfo, bypassResult);
                    FinalizePlayerJoin(playerInfo);
                }
                else
                {
                    logger.Msg($"ClientId {connection.ClientId} awaiting authentication handshake");
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

                    authentication.HandlePlayerDisconnected(playerInfo);
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

        /// <summary>
        /// Ticks authentication processing for pending players.
        /// </summary>
        public void Update()
        {
            authentication.Tick();

            if (!ServerConfig.Instance.RequireAuthentication)
            {
                return;
            }

            var pendingDisconnects = new List<ConnectedPlayerInfo>();
            foreach (var playerInfo in connectedPlayers.Values)
            {
                if (playerInfo == null || playerInfo.Connection == null || !playerInfo.Connection.IsActive)
                {
                    continue;
                }

                if (playerInfo.IsAuthenticated || playerInfo.IsAuthenticationPending)
                {
                    continue;
                }

                if (authentication.ShouldBypassAuthentication(playerInfo))
                {
                    continue;
                }

                TimeSpan elapsed = DateTime.Now - playerInfo.ConnectTime;
                if (elapsed.TotalSeconds > ServerConfig.Instance.AuthTimeoutSeconds)
                {
                    pendingDisconnects.Add(playerInfo);
                }
            }

            foreach (var playerInfo in pendingDisconnects)
            {
                logger.Warning($"Disconnecting ClientId {playerInfo.ClientId}: authentication handshake timed out");
                playerInfo.Connection.Disconnect(true);
            }
        }

        private void OnAuthenticationCompleted(ConnectedPlayerInfo playerInfo, AuthenticationResult result)
        {
            if (playerInfo == null || result == null)
            {
                return;
            }

            logger.Msg($"Authentication completed for ClientId {playerInfo.ClientId}: {result}");
            SendAuthResultToClient(playerInfo, result);

            if (result.IsSuccessful)
            {
                FinalizePlayerJoin(playerInfo);
                return;
            }

            if (result.ShouldDisconnect && playerInfo.Connection != null && playerInfo.Connection.IsActive)
            {
                logger.Warning($"Disconnecting ClientId {playerInfo.ClientId} due to auth failure: {result.Message}");
                playerInfo.Connection.Disconnect(true);
            }
        }

        private void FinalizePlayerJoin(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.HasCompletedJoinFlow)
            {
                return;
            }

            playerInfo.HasCompletedJoinFlow = true;

            logger.Msg($"Player joined: {playerInfo.DisplayName} (ClientId {playerInfo.ClientId})");
            OnPlayerJoined?.Invoke(playerInfo);
            try { ModManager.NotifyPlayerConnected(playerInfo.DisplayName ?? $"ClientId {playerInfo.ClientId}"); } catch { }

            SendInitialServerDataToClient(playerInfo.Connection);
        }

        private void SendAuthResultToClient(ConnectedPlayerInfo playerInfo, AuthenticationResult result)
        {
            if (playerInfo?.Connection == null)
            {
                return;
            }

            try
            {
                var payload = new AuthResultMessage
                {
                    Success = result.IsSuccessful,
                    Message = result.Message ?? string.Empty,
                    SteamId = result.ExtractedSteamId ?? string.Empty
                };

                string json = JsonConvert.SerializeObject(payload);
                CustomMessaging.SendToClient(playerInfo.Connection, Constants.Messages.AuthResult, json);
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to send auth result to ClientId {playerInfo.ClientId}: {ex.Message}");
            }
        }

        private void SendInitialServerDataToClient(NetworkConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            try
            {
                var cfg = ServerConfig.Instance;
                var serverData = new DedicatedServerMod.Shared.ServerData
                {
                    ServerName = cfg.ServerName,
                    AllowSleeping = cfg.AllowSleeping,
                    TimeNeverStops = cfg.TimeNeverStops,
                    PublicServer = cfg.PublicServer
                };

                string json = JsonConvert.SerializeObject(serverData);
                CustomMessaging.SendToClient(connection, Constants.Messages.ServerData, json);
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to send server data to ClientId {connection.ClientId}: {ex.Message}");
            }
        }

        private static bool IsLoopbackConnection(NetworkConnection connection)
        {
            if (connection == null)
            {
                return false;
            }

            return connection.IsLocalClient || connection.ClientId == 0;
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
            if (string.IsNullOrWhiteSpace(steamId))
            {
                return null;
            }

            return connectedPlayers.Values.FirstOrDefault(p =>
                string.Equals(p.AuthenticatedSteamId, steamId, StringComparison.Ordinal) ||
                string.Equals(p.SteamId, steamId, StringComparison.Ordinal));
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
                string banIdentifier = !string.IsNullOrEmpty(player.AuthenticatedSteamId)
                    ? player.AuthenticatedSteamId
                    : player.SteamId;

                if (string.IsNullOrEmpty(banIdentifier))
                {
                    logger.Warning("Cannot ban player without SteamID");
                    return false;
                }

                if (!ServerConfig.Instance.BannedPlayers.Contains(banIdentifier))
                {
                    ServerConfig.Instance.BannedPlayers.Add(banIdentifier);
                    ServerConfig.SaveConfig();
                }

                // Kick the player
                KickPlayer(player, $"Banned: {reason}");
                logger.Msg($"Player banned: {player.DisplayName} ({banIdentifier}) - {reason}");
                
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

            if (!connectedPlayers.TryGetValue(connection, out var playerInfo))
            {
                // Player name data can arrive before the connection event fires
                // Create the entry now - HandlePlayerConnected will skip if it already exists
                playerInfo = new ConnectedPlayerInfo
                {
                    Connection = connection,
                    ConnectTime = DateTime.Now,
                    ClientId = connection.ClientId,
                    IsLoopbackConnection = IsLoopbackConnection(connection),
                    IsAuthenticated = false,
                    IsAuthenticationPending = false
                };
                connectedPlayers[connection] = playerInfo;
                logger.Msg($"Created player entry from identity binding: ClientId {connection.ClientId}");
            }
            else
            {
                // Check if we're trying to overwrite a valid identity with default/empty values
                // This happens when clients spawn with temporary ClientId 0 / "Player" before getting real values
                bool isDefaultIdentity = string.IsNullOrEmpty(steamId) && playerName == "Player";
                bool hasValidIdentity = !string.IsNullOrEmpty(playerInfo.SteamId) ||
                                       !string.IsNullOrEmpty(playerInfo.AuthenticatedSteamId) ||
                                       (!string.IsNullOrEmpty(playerInfo.PlayerName) && playerInfo.PlayerName != "Player");
                
                if (isDefaultIdentity && hasValidIdentity)
                {
                    logger.Msg($"Skipping default identity update for ClientId {connection.ClientId} - already has valid identity: {playerInfo.DisplayName}");
                    return;
                }
            }

            if (playerInfo.IsAuthenticated && !string.IsNullOrEmpty(playerInfo.AuthenticatedSteamId))
            {
                if (!string.IsNullOrEmpty(steamId) &&
                    !string.Equals(steamId, playerInfo.AuthenticatedSteamId, StringComparison.Ordinal))
                {
                    logger.Warning($"Ignoring identity steamId overwrite for authenticated ClientId {connection.ClientId}: provided {steamId}, expected {playerInfo.AuthenticatedSteamId}");
                }

                steamId = playerInfo.AuthenticatedSteamId;
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

                authentication.AuthenticationCompleted -= OnAuthenticationCompleted;
                authentication.Shutdown();

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
