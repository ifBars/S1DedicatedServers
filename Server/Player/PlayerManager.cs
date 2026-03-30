#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Managing.Server;
using Il2CppScheduleOne.DevUtilities;
using PlayerScript = Il2CppScheduleOne.PlayerScripts.Player;
#else
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Server;
using ScheduleOne.DevUtilities;
using PlayerScript = ScheduleOne.PlayerScripts.Player;
using LoadManagerType = ScheduleOne.Persistence.LoadManager;
using TimeManagerType = ScheduleOne.GameTime.TimeManager;
#endif
using HarmonyLib;
using MelonLoader;
#if IL2CPP
using Il2CppScheduleOne.PlayerScripts;
using LoadManagerType = Il2CppScheduleOne.Persistence.LoadManager;
using TimeManagerType = Il2CppScheduleOne.GameTime.TimeManager;
#else
using ScheduleOne.PlayerScripts;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DedicatedServerMod;
using DedicatedServerMod.API;
using DedicatedServerMod.Server.Core;
#if IL2CPP
using Il2CppFishNet.Transporting;
#else
using FishNet.Transporting;
#endif
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.ModVerification;
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
    public sealed class PlayerManager
    {
        private static readonly TimeSpan PendingConnectionGracePeriod = TimeSpan.FromSeconds(15);
        private const int InitialTimeReplayAttempts = 4;
        private const float InitialTimeReplayFirstDelaySeconds = 0.35f;
        private const float InitialTimeReplayIntervalSeconds = 0.75f;
        private static readonly MethodInfo SetTimeDataClientMethod = AccessTools.Method(
            typeof(TimeManagerType),
            "SetTimeData_Client",
            new[] { typeof(NetworkConnection), typeof(int), typeof(int), typeof(uint) });

        private readonly MelonLogger.Instance logger;
        private readonly Dictionary<NetworkConnection, ConnectedPlayerInfo> connectedPlayers;
        private readonly PlayerAuthentication authentication;
        private readonly ClientModVerificationManager modVerification;
        private readonly PlayerPermissions permissions;

        internal PlayerManager(MelonLogger.Instance loggerInstance)
        {
            logger = loggerInstance;
            connectedPlayers = new Dictionary<NetworkConnection, ConnectedPlayerInfo>();
            authentication = new PlayerAuthentication(logger);
            modVerification = new ClientModVerificationManager(logger);
            permissions = new PlayerPermissions(logger);
        }

        /// <summary>
        /// Gets the number of connected players
        /// </summary>
        public int ConnectedPlayerCount => GetTrackedPlayersSnapshot(includeLoopbackConnections: true).Count;

        /// <summary>
        /// Gets the player authentication manager
        /// </summary>
        public PlayerAuthentication Authentication => authentication;

        /// <summary>
        /// Gets the client mod verification manager.
        /// </summary>
        internal ClientModVerificationManager ModVerification => modVerification;

        /// <summary>
        /// Gets the player permissions manager
        /// </summary>
        public PlayerPermissions Permissions => permissions;

        /// <summary>
        /// Initialize the player manager
        /// </summary>
        internal void Initialize()
        {
            try
            {
                SetupPlayerHooks();
                authentication.Initialize();
                authentication.AuthenticationCompleted += OnAuthenticationCompleted;
                modVerification.Initialize();
                modVerification.VerificationCompleted += OnModVerificationCompleted;
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
                        IsModVerificationComplete = false,
                        IsModVerificationPending = false,
                        IsAuthenticationPending = false,
                        IsDisconnectProcessed = false
                    };

                    connectedPlayers[connection] = playerInfo;
                }
                else
                {
                    playerInfo.Connection = connection;
                    playerInfo.ClientId = connection.ClientId;
                    playerInfo.IsLoopbackConnection = IsLoopbackConnection(connection);
                    playerInfo.IsDisconnectProcessed = false;
                    playerInfo.IsModVerificationComplete = false;
                    playerInfo.IsModVerificationPending = false;
                    playerInfo.ModVerificationNonce = string.Empty;
                    connectedPlayers[connection] = playerInfo;
                }

                DebugLog.PlayerLifecycleDebug($"Player tracked: ClientId {connection.ClientId} ({ConnectedPlayerCount}/{ServerConfig.Instance.MaxPlayers})");

                bool requiresAuthentication = authentication.IsAuthenticationRequiredForPlayer(playerInfo);
                if (!requiresAuthentication)
                {
                    TrySatisfyNoAuthFlow(playerInfo);
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
                if (playerInfo == null)
                {
                    playerInfo = EnsureTrackedConnection(player.Owner);
                }

                if (playerInfo != null)
                {
                    playerInfo.PlayerInstance = player;
                    playerInfo.IsDisconnectProcessed = false;

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

            var pendingAuthDisconnects = new List<ConnectedPlayerInfo>();
            var pendingVerificationDisconnects = new List<ConnectedPlayerInfo>();
            foreach (var playerInfo in connectedPlayers.Values)
            {
                if (playerInfo == null || playerInfo.Connection == null || !playerInfo.Connection.IsActive)
                {
                    continue;
                }

                if (ServerConfig.Instance.AuthenticationEnabled &&
                    !playerInfo.IsAuthenticated &&
                    !playerInfo.IsAuthenticationPending &&
                    !authentication.ShouldBypassAuthentication(playerInfo))
                {
                    TimeSpan authElapsed = DateTime.Now - playerInfo.ConnectTime;
                    if (authElapsed.TotalSeconds > ServerConfig.Instance.AuthTimeoutSeconds)
                    {
                        pendingAuthDisconnects.Add(playerInfo);
                    }
                }

                if (!modVerification.IsVerificationRequiredForPlayer(playerInfo) ||
                    !playerInfo.IsModVerificationPending ||
                    playerInfo.IsModVerificationComplete)
                {
                    continue;
                }

                DateTime startedAtLocal = playerInfo.ModVerificationStartedAtUtc?.ToLocalTime() ?? playerInfo.ConnectTime;
                TimeSpan verificationElapsed = DateTime.Now - startedAtLocal;
                if (verificationElapsed.TotalSeconds > ServerConfig.Instance.ModVerificationTimeoutSeconds)
                {
                    pendingVerificationDisconnects.Add(playerInfo);
                }
            }

            foreach (var playerInfo in pendingAuthDisconnects)
            {
                logger.Warning($"Disconnecting ClientId {playerInfo.ClientId}: authentication handshake timed out");
                playerInfo.Connection.Disconnect(true);
            }

            foreach (var playerInfo in pendingVerificationDisconnects)
            {
                logger.Warning($"Disconnecting ClientId {playerInfo.ClientId}: client mod verification timed out");
                NotifyAndDisconnectPlayer(playerInfo, "Verification Failed", "Client mod verification timed out.");
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
                BeginModVerification(playerInfo);
                return;
            }

            if (result.ShouldDisconnect && playerInfo.Connection != null && playerInfo.Connection.IsActive)
            {
                logger.Warning($"Disconnecting ClientId {playerInfo.ClientId} due to auth failure: {result.Message}");
                playerInfo.Connection.Disconnect(true);
            }
        }

        private void OnModVerificationCompleted(ConnectedPlayerInfo playerInfo, ModVerificationEvaluationResult result)
        {
            if (playerInfo == null || result == null)
            {
                return;
            }

            DebugLog.PlayerLifecycleDebug($"Client mod verification completed for ClientId {playerInfo.ClientId}: success={result.Success} message='{result.Message}'");
            SendModVerificationResultToClient(playerInfo, result);

            if (result.Success)
            {
                TryFinalizePlayerJoin(playerInfo, "client mod verification completed");
                return;
            }

            if (result.ShouldDisconnect && playerInfo.Connection != null && playerInfo.Connection.IsActive)
            {
                logger.Warning($"Disconnecting ClientId {playerInfo.ClientId} due to client mod verification failure: {result.Message}");
                NotifyAndDisconnectPlayer(playerInfo, "Verification Failed", result.Message);
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
            MelonCoroutines.Start(ReplayInitialTimeDataToClient(playerInfo));
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

            bool requiresModVerification = modVerification.IsVerificationRequiredForPlayer(playerInfo);
            if (requiresModVerification && !playerInfo.IsModVerificationComplete)
            {
                DebugLog.PlayerLifecycleDebug($"Join finalization deferred during {reason}: ClientId {playerInfo.ClientId} has not completed client mod verification yet");
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

        private void SendModVerificationResultToClient(ConnectedPlayerInfo playerInfo, ModVerificationEvaluationResult result)
        {
            if (playerInfo?.Connection == null || result == null)
            {
                return;
            }

            try
            {
                var payload = new ModVerificationResultMessage
                {
                    Success = result.Success,
                    Message = result.Message ?? string.Empty
                };

                string json = JsonConvert.SerializeObject(payload);
                CustomMessaging.SendToClientOrDeferUntilReady(playerInfo.Connection, Constants.Messages.ModVerifyResult, json);
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to send mod verification result to ClientId {playerInfo.ClientId}: {ex.Message}");
            }
        }

        private void BeginModVerification(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.IsModVerificationPending || playerInfo.IsModVerificationComplete)
            {
                return;
            }

            if (!modVerification.IsVerificationRequiredForPlayer(playerInfo))
            {
                ModVerificationResultMessage bypassResult = modVerification.BypassVerification(playerInfo);
                SendModVerificationResultToClient(playerInfo, ModVerificationEvaluationResult.SuccessResult(bypassResult.Message));
                TryFinalizePlayerJoin(playerInfo, "client mod verification bypassed");
                return;
            }

            ModVerificationChallengeMessage challenge = modVerification.CreateChallenge(playerInfo);
            if (challenge == null)
            {
                NotifyAndDisconnectPlayer(playerInfo, "Verification Failed", "Failed to start client mod verification.");
                return;
            }

            string json = JsonConvert.SerializeObject(challenge);
            CustomMessaging.SendToClientOrDeferUntilReady(playerInfo.Connection, Constants.Messages.ModVerifyChallenge, json);
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

        // Replays the native time snapshot after join finalization because the reconnecting client
        // can receive the first minute RPC before its initial SetTimeData target RPC has landed.
        private IEnumerator ReplayInitialTimeDataToClient(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.IsLoopbackConnection)
            {
                yield break;
            }

            if (SetTimeDataClientMethod == null)
            {
                logger.Warning("Unable to replay authoritative time because TimeManager.SetTimeData_Client could not be resolved.");
                yield break;
            }

            yield return new WaitForSecondsRealtime(InitialTimeReplayFirstDelaySeconds);

            for (int attempt = 1; attempt <= InitialTimeReplayAttempts; attempt++)
            {
                if (!IsTrackedPlayerActive(playerInfo))
                {
                    yield break;
                }

                LoadManagerType loadManager = Singleton<LoadManagerType>.Instance;
                TimeManagerType timeManager = NetworkSingleton<TimeManagerType>.Instance;
                if (loadManager != null && timeManager != null && !loadManager.IsLoading && loadManager.IsGameLoaded)
                {
                    try
                    {
                        SetTimeDataClientMethod.Invoke(timeManager, new object[]
                        {
                            playerInfo.Connection,
                            timeManager.ElapsedDays,
                            timeManager.CurrentTime,
                            0u
                        });

                        DebugLog.PlayerLifecycleDebug(
                            $"Replayed authoritative time to ClientId {playerInfo.ClientId} " +
                            $"attempt {attempt}/{InitialTimeReplayAttempts}: day={timeManager.ElapsedDays}, time={timeManager.CurrentTime:D4}");
                    }
                    catch (Exception ex)
                    {
                        logger.Warning($"Failed to replay authoritative time to ClientId {playerInfo.ClientId}: {ex.Message}");
                        yield break;
                    }
                }

                if (attempt < InitialTimeReplayAttempts)
                {
                    yield return new WaitForSecondsRealtime(InitialTimeReplayIntervalSeconds);
                }
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
            return GetTrackedPlayersSnapshot(includeLoopbackConnections: true);
        }

        /// <summary>
        /// Gets the number of non-loopback players visible to the server browser.
        /// </summary>
        public int GetVisiblePlayerCount()
        {
            return GetTrackedPlayersSnapshot(includeLoopbackConnections: false).Count;
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
                IsAuthenticationPending = false,
                IsDisconnectProcessed = false
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

                if (ServerBootstrap.Permissions?.AddBan(null, banIdentifier, reason) != true)
                {
                    logger.Warning($"Ban for {banIdentifier} was rejected or already exists.");
                    return false;
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
            return ServerBootstrap.Permissions?.IsBanned(steamId) == true;
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
                playerInfo.IsDisconnectProcessed = false;
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
                    IsModVerificationComplete = false,
                    IsModVerificationPending = false,
                    IsAuthenticationPending = false,
                    IsDisconnectProcessed = false
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
            bool hasProvidedSteamId = !string.IsNullOrEmpty(steamId) && !IsSyntheticLoopbackSteamId(steamId);
            bool hasMeaningfulPlayerName = !string.IsNullOrEmpty(playerName) && !string.Equals(playerName, "Player", StringComparison.Ordinal);
            bool isDefaultIdentity = !hasProvidedSteamId && !hasMeaningfulPlayerName;
            bool hasValidIdentity = HasValidRemoteIdentity(playerInfo);

            if (isDefaultIdentity && hasValidIdentity)
            {
                DebugLog.PlayerLifecycleDebug($"Skipping default identity update for ClientId {connection.ClientId} - already has valid identity: {playerInfo.DisplayName}");
                return;
            }

            if (IsSyntheticLoopbackSteamId(steamId))
            {
                logger.Warning($"Ignoring synthetic loopback SteamID '{steamId}' for non-loopback ClientId {connection.ClientId}.");
                steamId = string.Empty;
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

            if (!string.IsNullOrEmpty(steamId))
            {
                playerInfo.SteamId = steamId;
            }

            if (!string.IsNullOrEmpty(playerName) && (hasMeaningfulPlayerName || string.IsNullOrEmpty(playerInfo.PlayerName)))
            {
                playerInfo.PlayerName = playerName;
            }

            if (playerInfo.PlayerInstance == null)
            {
                playerInfo.PlayerInstance = TryResolveSpawnedPlayer(connection);
            }

            TrySatisfyNoAuthFlow(playerInfo);

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

        private static bool IsSyntheticLoopbackSteamId(string steamId)
        {
            return string.Equals(steamId, Constants.GhostHostSyntheticSteamId, StringComparison.Ordinal);
        }

        private static bool HasValidRemoteIdentity(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null)
            {
                return false;
            }

            bool hasValidSteamId =
                (!string.IsNullOrEmpty(playerInfo.SteamId) && !IsSyntheticLoopbackSteamId(playerInfo.SteamId)) ||
                (!string.IsNullOrEmpty(playerInfo.AuthenticatedSteamId) && !IsSyntheticLoopbackSteamId(playerInfo.AuthenticatedSteamId));

            bool hasMeaningfulName = !string.IsNullOrEmpty(playerInfo.PlayerName) &&
                                     !string.Equals(playerInfo.PlayerName, "Player", StringComparison.Ordinal);

            return hasValidSteamId || hasMeaningfulName;
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
                TotalBannedPlayers = ServerBootstrap.Permissions?.GetBanEntries().Count ?? 0,
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
        internal void Shutdown()
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
                modVerification.VerificationCompleted -= OnModVerificationCompleted;
                modVerification.Shutdown();

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

            if (playerInfo.IsDisconnectProcessed)
            {
                return;
            }

            playerInfo.IsDisconnectProcessed = true;

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

            playerInfo.Connection = null;
            playerInfo.PlayerInstance = null;

            OnPlayerLeft?.Invoke(playerInfo);
            try { ModManager.NotifyPlayerDisconnected(playerInfo.DisplayName ?? $"ClientId {playerInfo.ClientId}"); } catch { }

            logger.Msg($"Current players: {ConnectedPlayerCount}/{ServerConfig.Instance.MaxPlayers}");
        }

        private void TrySatisfyNoAuthFlow(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || authentication.IsAuthenticationRequiredForPlayer(playerInfo) || playerInfo.IsAuthenticated)
            {
                return;
            }

            playerInfo.IsAuthenticated = true;
            playerInfo.IsAuthenticationPending = false;

            AuthenticationResult bypassResult = new AuthenticationResult
            {
                IsSuccessful = true,
                Message = playerInfo.IsLoopbackConnection
                    ? "Loopback connection bypassed authentication"
                    : "Authentication not required",
                ExtractedSteamId = playerInfo.AuthenticatedSteamId ?? playerInfo.SteamId ?? string.Empty
            };

            SendAuthResultToClient(playerInfo, bypassResult);
            BeginModVerification(playerInfo);
        }

        private static PlayerScript TryResolveSpawnedPlayer(NetworkConnection connection)
        {
            if (connection == null)
            {
                return null;
            }

            try
            {
                return PlayerScript.GetPlayer(connection);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsTrackedPlayerActive(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.Connection == null)
            {
                return false;
            }

            if (playerInfo.IsDisconnectProcessed)
            {
                return false;
            }

            if (playerInfo.Connection.IsActive)
            {
                return true;
            }

            if (InstanceFinder.ServerManager?.Clients != null &&
                InstanceFinder.ServerManager.Clients.TryGetValue(playerInfo.ClientId, out NetworkConnection trackedConnection) &&
                trackedConnection != null &&
                trackedConnection.IsActive)
            {
                return true;
            }

            if (!playerInfo.HasCompletedJoinFlow && DateTime.Now - playerInfo.ConnectTime <= PendingConnectionGracePeriod)
            {
                return true;
            }

            return false;
        }

        private List<ConnectedPlayerInfo> GetTrackedPlayersSnapshot(bool includeLoopbackConnections)
        {
            SweepDisconnectedPlayers();

            var players = new List<ConnectedPlayerInfo>();
            var seenPlayers = new HashSet<ConnectedPlayerInfo>();
            var seenClientIds = new HashSet<int>();

            foreach (ConnectedPlayerInfo player in connectedPlayers.Values)
            {
                if (!IsTrackedPlayerActive(player))
                {
                    continue;
                }

                if (!includeLoopbackConnections && player.IsLoopbackConnection)
                {
                    continue;
                }

                if (!seenPlayers.Add(player) || !seenClientIds.Add(player.ClientId))
                {
                    continue;
                }

                players.Add(player);
            }

            return players;
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
