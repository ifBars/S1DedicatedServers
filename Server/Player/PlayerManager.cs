#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Managing.Server;
#else
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Server;
#endif
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
#else
using ScheduleOne.PlayerScripts;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DedicatedServerMod;
using DedicatedServerMod.API;
#if IL2CPP
using Il2CppFishNet.Transporting;
#else
using FishNet.Transporting;
#endif
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using Newtonsoft.Json;
using UnityEngine;

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
        public int ConnectedPlayerCount => connectedPlayers.Values.Count(IsTrackedPlayerActive);

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
#if MONO
                InstanceFinder.ServerManager.OnRemoteConnectionState += OnClientConnectionState;
#else
                DebugLog.PlayerLifecycleDebug("Skipping direct remote connection hook on IL2CPP runtime");
#endif
                DebugLog.PlayerLifecycleDebug("Player connection hooks established");
            }

            try
            {
#if MONO
                ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(ScheduleOne.PlayerScripts.Player.onPlayerSpawned, HandleOnPlayerSpawned);
                ScheduleOne.PlayerScripts.Player.onPlayerSpawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Combine(ScheduleOne.PlayerScripts.Player.onPlayerSpawned, HandleOnPlayerSpawned);
                ScheduleOne.PlayerScripts.Player.onPlayerDespawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(ScheduleOne.PlayerScripts.Player.onPlayerDespawned, OnPlayerDespawned);
                ScheduleOne.PlayerScripts.Player.onPlayerDespawned = (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Combine(ScheduleOne.PlayerScripts.Player.onPlayerDespawned, OnPlayerDespawned);
                DebugLog.PlayerLifecycleDebug("Player spawn hooks established");
#else
                DebugLog.PlayerLifecycleDebug("Skipping player spawn hook wiring on IL2CPP runtime");
#endif
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
                DebugLog.PlayerLifecycleDebug($"Player connecting: ClientId {connection.ClientId}");

                bool isExistingPlayer = connectedPlayers.TryGetValue(connection, out var playerInfo);
                if (!isExistingPlayer)
                {
                    playerInfo = connectedPlayers.Values.FirstOrDefault(p => p != null && p.ClientId == connection.ClientId);
                    isExistingPlayer = playerInfo != null;
                }

                if (!isExistingPlayer && ConnectedPlayerCount >= ServerConfig.Instance.MaxPlayers)
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
                    connectedPlayers[connection] = playerInfo;
                }

                DebugLog.PlayerLifecycleDebug($"Player tracked: ClientId {connection.ClientId} ({ConnectedPlayerCount}/{ServerConfig.Instance.MaxPlayers})");

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
                    TryFinalizePlayerJoin(playerInfo, "connection established without auth requirement");
                }
                else
                {
                    DebugLog.AuthenticationDebug($"ClientId {connection.ClientId} awaiting authentication handshake");
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
                ConnectedPlayerInfo playerInfo = GetPlayer(connection);
                if (playerInfo != null)
                {
                    RemoveTrackedPlayer(connection, playerInfo, logDisconnect: true, reason: "Player disconnected");
                }
                else
                {
                    DebugLog.PlayerLifecycleDebug($"Unknown player disconnected: ClientId {connection.ClientId}");
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

                var playerInfo = GetPlayer(player.Owner);
                if (playerInfo != null)
                {
                    playerInfo.PlayerInstance = player;

                    if (playerInfo.IsLoopbackConnection || GhostHostIdentifier.IsGhostHost(player))
                    {
                        ApplyLoopbackIdentity(playerInfo, player.PlayerCode, player.PlayerName);
                        DebugLog.PlayerLifecycleDebug($"Loopback player spawned and correlated: {playerInfo.DisplayName} (ClientId {player.Owner.ClientId})");
                        TryFinalizePlayerJoin(playerInfo, "loopback player spawned");
                        return;
                    }

                    // Identity binding happens via DedicatedServerPatches.BindPlayerIdentityPostfix
                    // which calls SetPlayerIdentity when player name data is received
                    DebugLog.PlayerLifecycleDebug($"Player spawned: {player.PlayerName} (ClientId {player.Owner.ClientId}) - awaiting identity");
                    TryFinalizePlayerJoin(playerInfo, "player spawned");
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

                var playerInfo = GetPlayer(player.Owner);
                if (playerInfo != null)
                {
                    playerInfo.PlayerInstance = null;
                    DebugLog.PlayerLifecycleDebug($"Player despawned: {player.PlayerName} (ClientId {player.Owner.ClientId})");
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
            SweepDisconnectedPlayers();
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

            DebugLog.AuthenticationDebug($"Authentication completed for ClientId {playerInfo.ClientId}: {result}");
            SendAuthResultToClient(playerInfo, result);

            if (result.IsSuccessful)
            {
                TryFinalizePlayerJoin(playerInfo, "authentication completed");
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

        private void TryFinalizePlayerJoin(ConnectedPlayerInfo playerInfo, string reason)
        {
            if (playerInfo == null || playerInfo.HasCompletedJoinFlow)
            {
                return;
            }

            bool requiresAuthentication = authentication.IsAuthenticationRequiredForPlayer(playerInfo);
            if (requiresAuthentication && !playerInfo.IsAuthenticated)
            {
                DebugLog.PlayerLifecycleDebug($"Join finalization deferred during {reason}: ClientId {playerInfo.ClientId} is not authenticated yet");
                return;
            }

            if (!playerInfo.IsLoopbackConnection && playerInfo.PlayerInstance == null)
            {
                DebugLog.PlayerLifecycleDebug($"Join finalization deferred during {reason}: ClientId {playerInfo.ClientId} has no spawned Player instance yet");
                return;
            }

            FinalizePlayerJoin(playerInfo);
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
                CustomMessaging.SendToClientOrDeferUntilReady(playerInfo.Connection, Constants.Messages.AuthResult, json);
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
                    ServerDescription = cfg.ServerDescription,
                    CurrentPlayers = GetVisiblePlayerCount(),
                    MaxPlayers = cfg.MaxPlayers,
                    AllowSleeping = cfg.AllowSleeping
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
        /// Gets the number of non-loopback players visible to the server browser.
        /// </summary>
        public int GetVisiblePlayerCount()
        {
            int count = 0;
            foreach (ConnectedPlayerInfo player in connectedPlayers.Values)
            {
                if (player != null && !player.IsLoopbackConnection)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Get player by connection
        /// </summary>
        public ConnectedPlayerInfo GetPlayer(NetworkConnection connection)
        {
            SweepDisconnectedPlayers();
            if (connection == null)
            {
                return null;
            }

            if (connectedPlayers.TryGetValue(connection, out var player))
            {
                return player;
            }

            player = connectedPlayers.Values.FirstOrDefault(p => p != null && p.ClientId == connection.ClientId);
            if (player != null)
            {
                player.Connection = connection;
                connectedPlayers[connection] = player;
                DebugLog.PlayerLifecycleDebug($"Recovered player tracking via ClientId match: ClientId {connection.ClientId}");
            }

            return player;
        }

        /// <summary>
        /// Ensures a tracked player entry exists for the provided connection.
        /// </summary>
        public ConnectedPlayerInfo EnsureTrackedConnection(NetworkConnection connection)
        {
            SweepDisconnectedPlayers();

            if (connection == null)
            {
                return null;
            }

            ConnectedPlayerInfo player = GetPlayer(connection);
            if (player != null)
            {
                return player;
            }

            player = new ConnectedPlayerInfo
            {
                Connection = connection,
                ConnectTime = DateTime.Now,
                ClientId = connection.ClientId,
                IsLoopbackConnection = IsLoopbackConnection(connection),
                IsAuthenticated = false,
                IsAuthenticationPending = false
            };

            connectedPlayers[connection] = player;
            DebugLog.PlayerLifecycleDebug($"Created player entry from connection fallback: ClientId {connection.ClientId}");
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

            SweepDisconnectedPlayers();
            return connectedPlayers.Values.FirstOrDefault(p =>
                IsTrackedPlayerActive(p) &&
                (string.Equals(p.AuthenticatedSteamId, steamId, StringComparison.Ordinal) ||
                 string.Equals(p.SteamId, steamId, StringComparison.Ordinal)));
        }

        /// <summary>
        /// Get player by name (partial match)
        /// </summary>
        public ConnectedPlayerInfo GetPlayerByName(string name)
        {
            SweepDisconnectedPlayers();
            return connectedPlayers.Values.FirstOrDefault(p => 
                IsTrackedPlayerActive(p) &&
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
                NotifyAndDisconnectPlayer(player, "Kicked", reason);
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
                NotifyAndDisconnectPlayer(player, "Banned", $"Banned: {reason}");
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
                playerInfo = connectedPlayers.Values.FirstOrDefault(p => p != null && p.ClientId == connection.ClientId);
            }

            if (playerInfo != null)
            {
                playerInfo.Connection = connection;
                connectedPlayers[connection] = playerInfo;
            }
            else
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
                DebugLog.PlayerLifecycleDebug($"Created player entry from identity binding: ClientId {connection.ClientId}");
            }

            if (playerInfo.IsLoopbackConnection)
            {
                ApplyLoopbackIdentity(playerInfo, steamId, playerName);
                DebugLog.PlayerLifecycleDebug($"Loopback identity correlated: ClientId {connection.ClientId} -> SteamID {playerInfo.SteamId} ({playerInfo.PlayerName})");
                OnPlayerSpawned?.Invoke(playerInfo);
                return;
            }

            // Check if we're trying to overwrite a valid identity with default/empty values.
            // This happens when clients spawn with temporary placeholder values before getting real identity data.
            bool isDefaultIdentity = string.IsNullOrEmpty(steamId) && playerName == "Player";
            bool hasValidIdentity = !string.IsNullOrEmpty(playerInfo.SteamId) ||
                                   !string.IsNullOrEmpty(playerInfo.AuthenticatedSteamId) ||
                                   (!string.IsNullOrEmpty(playerInfo.PlayerName) && playerInfo.PlayerName != "Player");

            if (isDefaultIdentity && hasValidIdentity)
            {
                DebugLog.PlayerLifecycleDebug($"Skipping default identity update for ClientId {connection.ClientId} - already has valid identity: {playerInfo.DisplayName}");
                return;
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
            DebugLog.PlayerLifecycleDebug($"Player identity set: ClientId {connection.ClientId} -> SteamID {steamId} ({playerName})");
            OnPlayerSpawned?.Invoke(playerInfo);
            TryFinalizePlayerJoin(playerInfo, "identity set");
        }

        private static void ApplyLoopbackIdentity(ConnectedPlayerInfo playerInfo, string steamId, string playerName)
        {
            if (playerInfo == null)
            {
                return;
            }

            playerInfo.SteamId = NormalizeLoopbackSteamId(steamId);
            playerInfo.PlayerName = NormalizeLoopbackPlayerName(playerName);
        }

        private static string NormalizeLoopbackSteamId(string steamId)
        {
            return string.IsNullOrWhiteSpace(steamId)
                ? Constants.GhostHostSyntheticSteamId
                : steamId;
        }

        private static string NormalizeLoopbackPlayerName(string playerName)
        {
            return string.IsNullOrWhiteSpace(playerName) || string.Equals(playerName, "Player", StringComparison.Ordinal)
                ? Constants.GhostHostDisplayName
                : playerName;
        }

        /// <summary>
        /// Get player statistics
        /// </summary>
        public PlayerStats GetPlayerStats()
        {
            var stats = new PlayerStats
            {
                ConnectedPlayers = connectedPlayers.Values.Count(IsTrackedPlayerActive),
                MaxPlayers = ServerConfig.Instance.MaxPlayers,
                TotalBannedPlayers = ServerConfig.Instance.BannedPlayers.Count,
                Players = GetConnectedPlayers()
            };

            return stats;
        }

        public bool NotifyAndDisconnectPlayer(ConnectedPlayerInfo player, string title, string reason, float disconnectDelaySeconds = 0.25f)
        {
            if (player == null || player.Connection == null || !player.Connection.IsActive)
            {
                return false;
            }

            try
            {
                SendDisconnectNotice(player, title, reason);
                MelonCoroutines.Start(DisconnectPlayerAfterDelay(player, disconnectDelaySeconds));
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error notifying/disconnecting player {player.DisplayName}: {ex}");
                return false;
            }
        }

        public void NotifyShutdownAndDisconnectAll(string reason, int noticeDelayMilliseconds = 500)
        {
            var playersToKick = connectedPlayers.Values.ToList();
            if (playersToKick.Count == 0)
            {
                return;
            }

            foreach (var player in playersToKick)
            {
                try
                {
                    if (player.Connection == null || !player.Connection.IsActive)
                    {
                        continue;
                    }

                    SendDisconnectNotice(player, "Server Shutdown", reason);
                }
                catch (Exception ex)
                {
                    logger.Warning($"Error sending shutdown notice to {player.DisplayName}: {ex.Message}");
                }
            }

            if (noticeDelayMilliseconds > 0)
            {
                System.Threading.Thread.Sleep(noticeDelayMilliseconds);
            }

            foreach (var player in playersToKick)
            {
                try
                {
                    if (player.Connection == null || !player.Connection.IsActive)
                    {
                        continue;
                    }

                        DebugLog.PlayerLifecycleDebug($"Disconnecting player {player.DisplayName}: {reason}");
                        player.Connection.Disconnect(true);
                }
                catch (Exception ex)
                {
                    logger.Warning($"Error disconnecting player {player.DisplayName} during shutdown: {ex.Message}");
                }
            }
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
                            DebugLog.PlayerLifecycleDebug($"Skipping already disconnected player: {player.DisplayName}");
                            continue;
                        }
                        
                        DebugLog.PlayerLifecycleDebug($"Final disconnect cleanup for player: {player.DisplayName}");
                        player.Connection.Disconnect(true);
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
#if MONO
                    InstanceFinder.ServerManager.OnRemoteConnectionState -= OnClientConnectionState;
#endif
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

        private void SweepDisconnectedPlayers()
        {
            List<KeyValuePair<NetworkConnection, ConnectedPlayerInfo>> disconnectedPlayers = null;
            HashSet<ConnectedPlayerInfo> seenPlayers = null;
            HashSet<int> seenClientIds = null;

            foreach (KeyValuePair<NetworkConnection, ConnectedPlayerInfo> entry in connectedPlayers)
            {
                if (IsTrackedPlayerActive(entry.Value))
                {
                    continue;
                }

                seenPlayers ??= new HashSet<ConnectedPlayerInfo>();
                if (!seenPlayers.Add(entry.Value))
                {
                    continue;
                }

                seenClientIds ??= new HashSet<int>();
                if (entry.Value != null && !seenClientIds.Add(entry.Value.ClientId))
                {
                    continue;
                }

                disconnectedPlayers ??= new List<KeyValuePair<NetworkConnection, ConnectedPlayerInfo>>();
                disconnectedPlayers.Add(entry);
            }

            if (disconnectedPlayers == null)
            {
                return;
            }

            foreach (KeyValuePair<NetworkConnection, ConnectedPlayerInfo> entry in disconnectedPlayers)
            {
                RemoveTrackedPlayer(entry.Key, entry.Value, logDisconnect: false, reason: "Pruned stale disconnected player");
            }
        }

        private void RemoveTrackedPlayer(NetworkConnection connection, ConnectedPlayerInfo playerInfo, bool logDisconnect, string reason)
        {
            if (playerInfo == null)
            {
                return;
            }

            if (logDisconnect)
            {
                logger.Msg($"{reason}: {playerInfo.DisplayName} (ClientId {playerInfo.ClientId})");
            }
            else
            {
                DebugLog.PlayerLifecycleDebug($"{reason}: {playerInfo.DisplayName} (ClientId {playerInfo.ClientId})");
            }

            authentication.HandlePlayerDisconnected(playerInfo);

            if (connection != null)
            {
                connectedPlayers.Remove(connection);
            }

            List<NetworkConnection> duplicateConnections = connectedPlayers
                .Where(entry => ReferenceEquals(entry.Value, playerInfo)
                    || (entry.Value != null && entry.Value.ClientId == playerInfo.ClientId))
                .Select(entry => entry.Key)
                .ToList();

            for (int i = 0; i < duplicateConnections.Count; i++)
            {
                connectedPlayers.Remove(duplicateConnections[i]);
            }

            OnPlayerLeft?.Invoke(playerInfo);
            try { ModManager.NotifyPlayerDisconnected(playerInfo.DisplayName ?? $"ClientId {playerInfo.ClientId}"); } catch { }

            logger.Msg($"Current players: {ConnectedPlayerCount}/{ServerConfig.Instance.MaxPlayers}");
        }

        private static bool IsTrackedPlayerActive(ConnectedPlayerInfo playerInfo)
        {
            return playerInfo != null && playerInfo.Connection != null && playerInfo.Connection.IsActive;
        }

        private void SendDisconnectNotice(ConnectedPlayerInfo player, string title, string reason)
        {
            var payload = new DisconnectNoticeMessage
            {
                Title = title ?? string.Empty,
                Message = reason ?? string.Empty
            };

            CustomMessaging.SendToClientOrDeferUntilReady(player.Connection, Constants.Messages.DisconnectNotice, JsonConvert.SerializeObject(payload));
        }

        private IEnumerator DisconnectPlayerAfterDelay(ConnectedPlayerInfo player, float delaySeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delaySeconds));

            if (player?.Connection == null || !player.Connection.IsActive)
            {
                yield break;
            }

            player.Connection.Disconnect(true);
        }
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
