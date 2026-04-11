#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Managing.Server;
using Il2CppFishNet.Transporting;
using PlayerScript = Il2CppScheduleOne.PlayerScripts.Player;
#else
using FishNet;
using FishNet.Connection;
using FishNet.Transporting;
using PlayerScript = ScheduleOne.PlayerScripts.Player;
#endif
using DedicatedServerMod.API;
using DedicatedServerMod.Shared.Configuration;
using DedicatedServerMod.Shared.ModVerification;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using Newtonsoft.Json;

namespace DedicatedServerMod.Server.Player.Runtime
{
    /// <summary>
    /// Coordinates player lifecycle, auth, verification, and join completion.
    /// </summary>
    internal sealed class PlayerLifecycleCoordinator
    {
        private readonly PlayerSessionRegistry _registry;
        private readonly PlayerAuthentication _authentication;
        private readonly ClientModVerificationManager _modVerification;
        private readonly PlayerClientMessagingService _messaging;
        private readonly PlayerJoinBootstrapService _joinBootstrap;
        private readonly PlayerModerationService _moderation;
        private readonly Action<ConnectedPlayerInfo> _publishPlayerJoined;
        private readonly Action<ConnectedPlayerInfo> _publishPlayerLeft;
        private readonly Action<ConnectedPlayerInfo> _publishPlayerSpawned;

        internal PlayerLifecycleCoordinator(
            PlayerSessionRegistry registry,
            PlayerAuthentication authentication,
            ClientModVerificationManager modVerification,
            PlayerClientMessagingService messaging,
            PlayerJoinBootstrapService joinBootstrap,
            PlayerModerationService moderation,
            Action<ConnectedPlayerInfo> publishPlayerJoined,
            Action<ConnectedPlayerInfo> publishPlayerLeft,
            Action<ConnectedPlayerInfo> publishPlayerSpawned)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            _modVerification = modVerification ?? throw new ArgumentNullException(nameof(modVerification));
            _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));
            _joinBootstrap = joinBootstrap ?? throw new ArgumentNullException(nameof(joinBootstrap));
            _moderation = moderation ?? throw new ArgumentNullException(nameof(moderation));
            _publishPlayerJoined = publishPlayerJoined ?? throw new ArgumentNullException(nameof(publishPlayerJoined));
            _publishPlayerLeft = publishPlayerLeft ?? throw new ArgumentNullException(nameof(publishPlayerLeft));
            _publishPlayerSpawned = publishPlayerSpawned ?? throw new ArgumentNullException(nameof(publishPlayerSpawned));
        }

        internal void Initialize()
        {
            SetupPlayerHooks();
            _authentication.Initialize();
            _authentication.AuthenticationCompleted += OnAuthenticationCompleted;
            _modVerification.Initialize();
            _modVerification.VerificationCompleted += OnModVerificationCompleted;
        }

        internal void Shutdown()
        {
            _authentication.AuthenticationCompleted -= OnAuthenticationCompleted;
            _authentication.Shutdown();
            _modVerification.VerificationCompleted -= OnModVerificationCompleted;
            _modVerification.Shutdown();

            if (InstanceFinder.ServerManager != null)
            {
#if MONO
                InstanceFinder.ServerManager.OnRemoteConnectionState -= OnClientConnectionState;
#else
                InstanceFinder.ServerManager.OnRemoteConnectionState -= new Action<NetworkConnection, RemoteConnectionStateArgs>(OnClientConnectionState);
#endif
            }

#if MONO
            ScheduleOne.PlayerScripts.Player.onPlayerSpawned =
                (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(
                    ScheduleOne.PlayerScripts.Player.onPlayerSpawned,
                    HandleOnPlayerSpawned);
            ScheduleOne.PlayerScripts.Player.onPlayerDespawned =
                (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(
                    ScheduleOne.PlayerScripts.Player.onPlayerDespawned,
                    OnPlayerDespawned);
#else
            PlayerScript.onPlayerSpawned -= new Action<PlayerScript>(HandleOnPlayerSpawned);
            PlayerScript.onPlayerDespawned -= new Action<PlayerScript>(OnPlayerDespawned);
#endif
        }

        internal void Update()
        {
            _registry.SweepDisconnectedPlayers(playerInfo =>
                HandleTrackedPlayerRemoval(connection: null, playerInfo, logDisconnect: false, reason: "Pruned stale disconnected player"));

            _authentication.Tick();

            DateTime nowUtc = DateTime.UtcNow;
            List<ConnectedPlayerInfo> pendingAuthDisconnects = new List<ConnectedPlayerInfo>();
            List<ConnectedPlayerInfo> pendingVerificationDisconnects = new List<ConnectedPlayerInfo>();

            IReadOnlyList<ConnectedPlayerInfo> players = _registry.GetAllTrackedPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                ConnectedPlayerInfo playerInfo = players[i];
                if (playerInfo?.Connection == null || !playerInfo.Connection.IsActive)
                {
                    continue;
                }

                if (ServerConfig.Instance.AuthenticationEnabled &&
                    !playerInfo.IsAuthenticated &&
                    !playerInfo.IsAuthenticationPending &&
                    !_authentication.ShouldBypassAuthentication(playerInfo) &&
                    (nowUtc - playerInfo.ConnectTime).TotalSeconds > ServerConfig.Instance.AuthTimeoutSeconds)
                {
                    pendingAuthDisconnects.Add(playerInfo);
                }

                if (!_modVerification.IsVerificationRequiredForPlayer(playerInfo) ||
                    !playerInfo.IsModVerificationPending ||
                    playerInfo.IsModVerificationComplete)
                {
                    continue;
                }

                DateTime startedAtUtc = playerInfo.ModVerificationStartedAtUtc ?? playerInfo.ConnectTime;
                if ((nowUtc - startedAtUtc).TotalSeconds > ServerConfig.Instance.ModVerificationTimeoutSeconds)
                {
                    pendingVerificationDisconnects.Add(playerInfo);
                }
            }

            for (int i = 0; i < pendingAuthDisconnects.Count; i++)
            {
                ConnectedPlayerInfo playerInfo = pendingAuthDisconnects[i];
                DebugLog.Warning($"Disconnecting ClientId {playerInfo.ClientId}: authentication handshake timed out");
                _moderation.NotifyAndDisconnectPlayer(
                    playerInfo,
                    "Authentication Failed",
                    $"Authentication timed out after {ServerConfig.Instance.AuthTimeoutSeconds}s.");
            }

            for (int i = 0; i < pendingVerificationDisconnects.Count; i++)
            {
                ConnectedPlayerInfo playerInfo = pendingVerificationDisconnects[i];
                DebugLog.Warning($"Disconnecting ClientId {playerInfo.ClientId}: client mod verification timed out");
                _moderation.NotifyAndDisconnectPlayer(playerInfo, "Verification Failed", "Client mod verification timed out.");
            }
        }

        internal void HandleAuthHello(NetworkConnection conn)
        {
            ConnectedPlayerInfo playerInfo = _registry.GetPlayer(conn);
            if (playerInfo == null)
            {
                DebugLog.Warning($"HandleAuthHello: no player tracked for ClientId {conn.ClientId}");
                return;
            }

            AuthChallengeMessage challenge = _authentication.CreateChallenge(playerInfo);
            if (challenge != null)
            {
                _messaging.SendAuthChallenge(conn, challenge);
                return;
            }

            if (playerInfo.IsAuthenticated)
            {
                _messaging.SendAuthResult(
                    playerInfo,
                    new AuthenticationResult
                    {
                        IsSuccessful = true,
                        Message = "Authentication already satisfied",
                        ExtractedSteamId = playerInfo.AuthenticatedSteamId ?? playerInfo.SteamId ?? string.Empty
                    });
            }
        }

        internal void HandleAuthTicket(NetworkConnection conn, string data)
        {
            ConnectedPlayerInfo playerInfo = _registry.GetPlayer(conn);
            if (playerInfo == null)
            {
                DebugLog.Warning($"HandleAuthTicket: no player tracked for ClientId {conn.ClientId}");
                return;
            }

            AuthTicketMessage ticketMessage;
            try
            {
                ticketMessage = JsonConvert.DeserializeObject<AuthTicketMessage>(data ?? string.Empty);
            }
            catch (JsonException ex)
            {
                DebugLog.Error($"HandleAuthTicket: invalid payload from ClientId {conn.ClientId}", ex);
                _moderation.NotifyAndDisconnectPlayer(playerInfo, "Authentication Failed", "Authentication payload was invalid.");
                return;
            }

            AuthenticationResult beginResult = _authentication.SubmitTicket(playerInfo, ticketMessage);
            if (beginResult.IsPending)
            {
                DebugLog.MessageRoutingDebug($"HandleAuthTicket: pending auth validation for ClientId {conn.ClientId}");
            }
        }

        internal void HandleModVerificationReport(NetworkConnection conn, string data)
        {
            ConnectedPlayerInfo playerInfo = _registry.GetPlayer(conn);
            if (playerInfo == null)
            {
                DebugLog.Warning($"HandleModVerificationReport: no player tracked for ClientId {conn.ClientId}");
                return;
            }

            ModVerificationReportMessage reportMessage;
            try
            {
                reportMessage = JsonConvert.DeserializeObject<ModVerificationReportMessage>(data ?? string.Empty);
            }
            catch (JsonException ex)
            {
                DebugLog.Error($"HandleModVerificationReport: invalid payload from ClientId {conn.ClientId}", ex);
                _moderation.NotifyAndDisconnectPlayer(playerInfo, "Verification Failed", "Client mod verification payload was invalid.");
                return;
            }

            _modVerification.SubmitReport(playerInfo, reportMessage);
        }

        internal bool IsCommandAllowedForConnection(NetworkConnection conn, string command)
        {
            if (string.Equals(command, Constants.Messages.AuthHello, StringComparison.Ordinal) ||
                string.Equals(command, Constants.Messages.AuthTicket, StringComparison.Ordinal))
            {
                return true;
            }

            ConnectedPlayerInfo playerInfo = _registry.GetPlayer(conn);
            if (playerInfo == null)
            {
                return false;
            }

            bool authenticationRequired = ServerConfig.Instance.AuthenticationEnabled &&
                                          !_authentication.ShouldBypassAuthentication(playerInfo);
            if (authenticationRequired && !playerInfo.IsAuthenticated)
            {
                return false;
            }

            if (string.Equals(command, Constants.Messages.ModVerifyReport, StringComparison.Ordinal))
            {
                return !playerInfo.IsModVerificationComplete;
            }

            bool verificationRequired = _modVerification.IsVerificationRequiredForPlayer(playerInfo);
            return !verificationRequired || playerInfo.IsModVerificationComplete;
        }

        internal void SetPlayerIdentity(NetworkConnection connection, string steamId, string playerName)
        {
            if (connection == null)
            {
                DebugLog.Warning("SetPlayerIdentity called with null connection");
                return;
            }

            ConnectedPlayerInfo playerInfo = _registry.EnsureTrackedConnection(connection, IsLoopbackConnection(connection));
            if (playerInfo == null)
            {
                return;
            }

            if (playerInfo.IsLoopbackConnection)
            {
                ApplyLoopbackIdentity(playerInfo, steamId, playerName);
                DebugLog.PlayerLifecycleDebug(
                    $"Loopback identity correlated: ClientId {connection.ClientId} -> SteamID {playerInfo.SteamId} ({playerInfo.PlayerName})");
                _publishPlayerSpawned(playerInfo);
                return;
            }

            bool hasProvidedSteamId = !string.IsNullOrEmpty(steamId) && !IsSyntheticLoopbackSteamId(steamId);
            bool hasMeaningfulPlayerName = !string.IsNullOrEmpty(playerName) && !string.Equals(playerName, "Player", StringComparison.Ordinal);
            bool isDefaultIdentity = !hasProvidedSteamId && !hasMeaningfulPlayerName;
            bool hasValidIdentity = HasValidRemoteIdentity(playerInfo);

            if (isDefaultIdentity && hasValidIdentity)
            {
                DebugLog.PlayerLifecycleDebug(
                    $"Skipping default identity update for ClientId {connection.ClientId} - already has valid identity: {playerInfo.DisplayName}");
                return;
            }

            if (IsSyntheticLoopbackSteamId(steamId))
            {
                DebugLog.Warning($"Ignoring synthetic loopback SteamID '{steamId}' for non-loopback ClientId {connection.ClientId}.");
                steamId = string.Empty;
            }

            if (playerInfo.IsAuthenticated && !string.IsNullOrEmpty(playerInfo.AuthenticatedSteamId))
            {
                if (!string.IsNullOrEmpty(steamId) &&
                    !string.Equals(steamId, playerInfo.AuthenticatedSteamId, StringComparison.Ordinal))
                {
                    DebugLog.Warning(
                        $"Ignoring identity steamId overwrite for authenticated ClientId {connection.ClientId}: provided {steamId}, expected {playerInfo.AuthenticatedSteamId}");
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
            _publishPlayerSpawned(playerInfo);
            TryFinalizePlayerJoin(playerInfo, "identity set");
        }

        private void SetupPlayerHooks()
        {
            if (InstanceFinder.ServerManager != null)
            {
#if MONO
                InstanceFinder.ServerManager.OnRemoteConnectionState -= OnClientConnectionState;
                InstanceFinder.ServerManager.OnRemoteConnectionState += OnClientConnectionState;
#else
                InstanceFinder.ServerManager.OnRemoteConnectionState -= new Action<NetworkConnection, RemoteConnectionStateArgs>(OnClientConnectionState);
                InstanceFinder.ServerManager.OnRemoteConnectionState += new Action<NetworkConnection, RemoteConnectionStateArgs>(OnClientConnectionState);
#endif
                DebugLog.PlayerLifecycleDebug("Player connection hooks established");
            }

            try
            {
#if MONO
                ScheduleOne.PlayerScripts.Player.onPlayerSpawned =
                    (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(
                        ScheduleOne.PlayerScripts.Player.onPlayerSpawned,
                        HandleOnPlayerSpawned);
                ScheduleOne.PlayerScripts.Player.onPlayerSpawned =
                    (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Combine(
                        ScheduleOne.PlayerScripts.Player.onPlayerSpawned,
                        HandleOnPlayerSpawned);
                ScheduleOne.PlayerScripts.Player.onPlayerDespawned =
                    (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Remove(
                        ScheduleOne.PlayerScripts.Player.onPlayerDespawned,
                        OnPlayerDespawned);
                ScheduleOne.PlayerScripts.Player.onPlayerDespawned =
                    (Action<ScheduleOne.PlayerScripts.Player>)Delegate.Combine(
                        ScheduleOne.PlayerScripts.Player.onPlayerDespawned,
                        OnPlayerDespawned);
                DebugLog.PlayerLifecycleDebug("Player spawn hooks established");
#else
                PlayerScript.onPlayerSpawned -= new Action<PlayerScript>(HandleOnPlayerSpawned);
                PlayerScript.onPlayerSpawned += new Action<PlayerScript>(HandleOnPlayerSpawned);
                PlayerScript.onPlayerDespawned -= new Action<PlayerScript>(OnPlayerDespawned);
                PlayerScript.onPlayerDespawned += new Action<PlayerScript>(OnPlayerDespawned);
                DebugLog.PlayerLifecycleDebug("Player spawn hooks established");
#endif
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Could not establish player spawn hooks: {ex.Message}");
            }
        }

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

        private void HandlePlayerConnected(NetworkConnection connection)
        {
            try
            {
                if (connection == null)
                {
                    return;
                }

                _registry.SweepDisconnectedPlayers(playerInfo =>
                    HandleTrackedPlayerRemoval(connection: null, playerInfo, logDisconnect: false, reason: "Pruned stale disconnected player"));

                DebugLog.PlayerLifecycleDebug($"Player connecting: ClientId {connection.ClientId}");

                bool isExistingPlayer = _registry.GetPlayer(connection) != null;
                if (!isExistingPlayer && _registry.GetVisiblePlayerCount() >= ServerConfig.Instance.MaxPlayers)
                {
                    DebugLog.Warning($"Server full, disconnecting ClientId {connection.ClientId}");
                    ConnectedPlayerInfo rejectedPlayer = new ConnectedPlayerInfo
                    {
                        Connection = connection,
                        ConnectTime = DateTime.UtcNow,
                        ClientId = connection.ClientId,
                        IsLoopbackConnection = IsLoopbackConnection(connection),
                        IsDisconnectProcessed = false
                    };

                    _moderation.NotifyAndDisconnectPlayer(rejectedPlayer, "Server Full", "The server is full.");
                    return;
                }

                ConnectedPlayerInfo playerInfo = _registry.EnsureTrackedConnection(connection, IsLoopbackConnection(connection));
                if (playerInfo == null)
                {
                    return;
                }

                playerInfo.IsDisconnectProcessed = false;
                if (isExistingPlayer)
                {
                    playerInfo.IsModVerificationComplete = false;
                    playerInfo.IsModVerificationPending = false;
                    playerInfo.ModVerificationNonce = string.Empty;
                }

                DebugLog.PlayerLifecycleDebug(
                    $"Player tracked: ClientId {connection.ClientId} ({_registry.GetVisiblePlayerCount()}/{ServerConfig.Instance.MaxPlayers} visible)");

                bool requiresAuthentication = _authentication.IsAuthenticationRequiredForPlayer(playerInfo);
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
                DebugLog.Error($"Error handling player connection: {ex}");
            }
        }

        private void HandlePlayerDisconnected(NetworkConnection connection)
        {
            try
            {
                ConnectedPlayerInfo playerInfo = _registry.GetPlayer(connection);
                if (playerInfo != null)
                {
                    HandleTrackedPlayerRemoval(connection, playerInfo, logDisconnect: true, reason: "Player disconnected");
                }
                else
                {
                    DebugLog.PlayerLifecycleDebug($"Unknown player disconnected: ClientId {connection.ClientId}");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error handling player disconnection: {ex}");
            }
        }

        private void HandleOnPlayerSpawned(PlayerScript player)
        {
            try
            {
                if (player == null || player.Owner == null)
                {
                    return;
                }

                ConnectedPlayerInfo playerInfo = _registry.GetPlayer(player.Owner) ??
                    _registry.EnsureTrackedConnection(player.Owner, IsLoopbackConnection(player.Owner));

                if (playerInfo == null)
                {
                    if (!string.IsNullOrEmpty(player.PlayerCode) || player.PlayerName != "Player")
                    {
                        DebugLog.Warning($"Player spawned but not found in connected players: {player.PlayerName}");
                    }

                    return;
                }

                playerInfo.PlayerInstance = player;
                playerInfo.IsDisconnectProcessed = false;

                if (playerInfo.IsLoopbackConnection || player.IsGhostHost())
                {
                    ApplyLoopbackIdentity(playerInfo, player.PlayerCode, player.PlayerName);
                    DebugLog.PlayerLifecycleDebug(
                        $"Loopback player spawned and correlated: {playerInfo.DisplayName} (ClientId {player.Owner.ClientId})");
                    TryFinalizePlayerJoin(playerInfo, "loopback player spawned");
                    return;
                }

                DebugLog.PlayerLifecycleDebug(
                    $"Player spawned: {player.PlayerName} (ClientId {player.Owner.ClientId}) - awaiting identity");
                TryFinalizePlayerJoin(playerInfo, "player spawned");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error in OnPlayerSpawned: {ex}");
            }
        }

        private void OnPlayerDespawned(PlayerScript player)
        {
            try
            {
                if (player == null || player.Owner == null)
                {
                    return;
                }

                ConnectedPlayerInfo playerInfo = _registry.GetPlayer(player.Owner);
                if (playerInfo != null)
                {
                    playerInfo.PlayerInstance = null;
                    DebugLog.PlayerLifecycleDebug($"Player despawned: {player.PlayerName} (ClientId {player.Owner.ClientId})");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error in OnPlayerDespawned: {ex}");
            }
        }

        private void OnAuthenticationCompleted(ConnectedPlayerInfo playerInfo, AuthenticationResult result)
        {
            if (playerInfo == null || result == null)
            {
                return;
            }

            DebugLog.AuthenticationDebug($"Authentication completed for ClientId {playerInfo.ClientId}: {result}");
            _messaging.SendAuthResult(playerInfo, result);

            if (result.IsSuccessful)
            {
                BeginModVerification(playerInfo);
                return;
            }

            if (result.ShouldDisconnect && playerInfo.Connection != null && playerInfo.Connection.IsActive)
            {
                DebugLog.Warning($"Disconnecting ClientId {playerInfo.ClientId} due to auth failure: {result.Message}");
                _moderation.NotifyAndDisconnectPlayer(playerInfo, "Authentication Failed", result.Message);
            }
        }

        private void OnModVerificationCompleted(ConnectedPlayerInfo playerInfo, ModVerificationEvaluationResult result)
        {
            if (playerInfo == null || result == null)
            {
                return;
            }

            DebugLog.PlayerLifecycleDebug(
                $"Client mod verification completed for ClientId {playerInfo.ClientId}: success={result.Success} message='{result.Message}'");
            _messaging.SendModVerificationResult(playerInfo, result);

            if (result.Success)
            {
                TryFinalizePlayerJoin(playerInfo, "client mod verification completed");
                return;
            }

            if (result.ShouldDisconnect && playerInfo.Connection != null && playerInfo.Connection.IsActive)
            {
                DebugLog.Warning($"Disconnecting ClientId {playerInfo.ClientId} due to client mod verification failure: {result.Message}");
                _moderation.NotifyAndDisconnectPlayer(playerInfo, "Verification Failed", result.Message);
            }
        }

        private void FinalizePlayerJoin(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.HasCompletedJoinFlow)
            {
                return;
            }

            playerInfo.HasCompletedJoinFlow = true;
            DebugLog.Info($"Player joined: {playerInfo.DisplayName} (ClientId {playerInfo.ClientId})");
            _publishPlayerJoined(playerInfo);

            try
            {
                ModManager.NotifyPlayerConnected(playerInfo);
            }
            catch
            {
            }

            _joinBootstrap.StartInitialJoinBootstrap(playerInfo);
        }

        private void TryFinalizePlayerJoin(ConnectedPlayerInfo playerInfo, string reason)
        {
            if (playerInfo == null || playerInfo.HasCompletedJoinFlow)
            {
                return;
            }

            bool requiresAuthentication = _authentication.IsAuthenticationRequiredForPlayer(playerInfo);
            if (requiresAuthentication && !playerInfo.IsAuthenticated)
            {
                DebugLog.PlayerLifecycleDebug(
                    $"Join finalization deferred during {reason}: ClientId {playerInfo.ClientId} is not authenticated yet");
                return;
            }

            bool requiresModVerification = _modVerification.IsVerificationRequiredForPlayer(playerInfo);
            if (requiresModVerification && !playerInfo.IsModVerificationComplete)
            {
                DebugLog.PlayerLifecycleDebug(
                    $"Join finalization deferred during {reason}: ClientId {playerInfo.ClientId} has not completed client mod verification yet");
                return;
            }

            if (!playerInfo.IsLoopbackConnection && playerInfo.PlayerInstance == null)
            {
                DebugLog.PlayerLifecycleDebug(
                    $"Join finalization deferred during {reason}: ClientId {playerInfo.ClientId} has no spawned Player instance yet");
                return;
            }

            FinalizePlayerJoin(playerInfo);
        }

        private void BeginModVerification(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || playerInfo.IsModVerificationPending || playerInfo.IsModVerificationComplete)
            {
                return;
            }

            if (!_modVerification.IsVerificationRequiredForPlayer(playerInfo))
            {
                ModVerificationResultMessage bypassResult = _modVerification.BypassVerification(playerInfo);
                _messaging.SendModVerificationResult(playerInfo, ModVerificationEvaluationResult.SuccessResult(bypassResult.Message));
                TryFinalizePlayerJoin(playerInfo, "client mod verification bypassed");
                return;
            }

            ModVerificationChallengeMessage challenge = _modVerification.CreateChallenge(playerInfo);
            if (challenge == null)
            {
                _moderation.NotifyAndDisconnectPlayer(playerInfo, "Verification Failed", "Failed to start client mod verification.");
                return;
            }

            _messaging.SendModVerificationChallenge(playerInfo, challenge);
        }

        private void HandleTrackedPlayerRemoval(NetworkConnection connection, ConnectedPlayerInfo playerInfo, bool logDisconnect, string reason)
        {
            if (!_registry.TryRemovePlayer(connection, playerInfo, out ConnectedPlayerInfo removedPlayer))
            {
                return;
            }

            if (logDisconnect)
            {
                DebugLog.Info($"{reason}: {removedPlayer.DisplayName} (ClientId {removedPlayer.ClientId})");
            }
            else
            {
                DebugLog.PlayerLifecycleDebug($"{reason}: {removedPlayer.DisplayName} (ClientId {removedPlayer.ClientId})");
            }

            _authentication.HandlePlayerDisconnected(removedPlayer);
            _publishPlayerLeft(removedPlayer);

            try
            {
                ModManager.NotifyPlayerDisconnected(removedPlayer);
            }
            catch
            {
            }

            removedPlayer.Connection = null;
            removedPlayer.PlayerInstance = null;

            DebugLog.Info($"Current players: {_registry.GetVisiblePlayerCount()}/{ServerConfig.Instance.MaxPlayers}");
        }

        private void TrySatisfyNoAuthFlow(ConnectedPlayerInfo playerInfo)
        {
            if (playerInfo == null || _authentication.IsAuthenticationRequiredForPlayer(playerInfo) || playerInfo.IsAuthenticated)
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

            _messaging.SendAuthResult(playerInfo, bypassResult);
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

        private static bool IsLoopbackConnection(NetworkConnection connection)
        {
            return connection != null && (connection.IsLocalClient || connection.ClientId == 0);
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
    }
}
