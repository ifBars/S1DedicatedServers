using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Server.Player.Runtime;
using DedicatedServerMod.Utils;
using UnityEngine;
#if IL2CPP
using Il2CppFishNet.Connection;
#else
using FishNet.Connection;
#endif

namespace DedicatedServerMod.Server.Player
{
    /// <summary>
    /// Public facade for player session queries, lifecycle events, and moderation actions.
    /// </summary>
    public sealed class PlayerManager
    {
        private readonly PlayerSessionRegistry _registry;
        private readonly PlayerLifecycleCoordinator _lifecycle;
        private readonly PlayerJoinBootstrapService _joinBootstrap;
        private readonly PlayerModerationService _moderation;
        private readonly PlayerTeleportationService _teleportation;
        private readonly PlayerVisibilityService _visibility;
        private readonly ServerPermissionService _permissionService;

        internal PlayerManager(
            PlayerAuthentication authentication,
            ClientModVerificationManager modVerification,
            ServerPermissionService permissionService)
        {
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _registry = new PlayerSessionRegistry();

            PlayerClientMessagingService messaging = new PlayerClientMessagingService();
            _joinBootstrap = new PlayerJoinBootstrapService(_registry);
            _moderation = new PlayerModerationService(_registry, messaging, _permissionService);
            _teleportation = new PlayerTeleportationService();
            _visibility = new PlayerVisibilityService();
            _lifecycle = new PlayerLifecycleCoordinator(
                _registry,
                authentication ?? throw new ArgumentNullException(nameof(authentication)),
                modVerification ?? throw new ArgumentNullException(nameof(modVerification)),
                messaging,
                _joinBootstrap,
                _moderation,
                PublishPlayerJoined,
                PublishPlayerLeft,
                PublishPlayerSpawned);
        }

        /// <summary>
        /// Gets the number of connected non-loopback players.
        /// </summary>
        public int ConnectedPlayerCount => _registry.GetVisiblePlayerCount();

        /// <summary>
        /// Ticks runtime player lifecycle work.
        /// </summary>
        public void Update()
        {
            _lifecycle.Update();
        }

        /// <summary>
        /// Gets all connected non-loopback players visible to gameplay and administration surfaces.
        /// </summary>
        public IReadOnlyList<ConnectedPlayerInfo> GetConnectedPlayers()
        {
            return _registry.GetConnectedPlayers(includeLoopbackConnections: false);
        }

        /// <summary>
        /// Gets the number of non-loopback players visible to the server browser.
        /// </summary>
        public int GetVisiblePlayerCount()
        {
            return _registry.GetVisiblePlayerCount();
        }

        /// <summary>
        /// Gets the tracked player for a connection.
        /// </summary>
        /// <param name="connection">The connection to resolve.</param>
        /// <returns>The tracked player info, or <see langword="null"/> if not tracked.</returns>
        public ConnectedPlayerInfo GetPlayer(NetworkConnection connection)
        {
            return _registry.GetPlayer(connection);
        }

        /// <summary>
        /// Gets the tracked player by SteamID.
        /// </summary>
        /// <param name="steamId">The SteamID to resolve.</param>
        /// <returns>The tracked player info, or <see langword="null"/> if not found.</returns>
        public ConnectedPlayerInfo GetPlayerBySteamId(string steamId)
        {
            return _registry.GetPlayerBySteamId(steamId);
        }

        /// <summary>
        /// Gets the tracked player by partial or exact player name.
        /// </summary>
        /// <param name="name">The player name to resolve.</param>
        /// <returns>The tracked player info, or <see langword="null"/> if not found.</returns>
        public ConnectedPlayerInfo GetPlayerByName(string name)
        {
            return _registry.GetPlayerByName(name);
        }

        /// <summary>
        /// Kicks a player from the server.
        /// </summary>
        /// <param name="player">The player to disconnect.</param>
        /// <param name="reason">The kick reason shown to the player.</param>
        /// <returns><see langword="true"/> when the disconnect was queued successfully.</returns>
        public bool KickPlayer(ConnectedPlayerInfo player, string reason = "Kicked by admin")
        {
            return _moderation.KickPlayer(player, reason);
        }

        /// <summary>
        /// Bans a player from the server.
        /// </summary>
        /// <param name="player">The player to ban.</param>
        /// <param name="reason">The ban reason shown to the player.</param>
        /// <returns><see langword="true"/> when the ban and disconnect were applied.</returns>
        public bool BanPlayer(ConnectedPlayerInfo player, string reason = "Banned by admin")
        {
            return _moderation.BanPlayer(player, reason);
        }

        /// <summary>
        /// Checks whether a SteamID is currently banned.
        /// </summary>
        /// <param name="steamId">The SteamID to check.</param>
        /// <returns><see langword="true"/> when the subject is banned.</returns>
        public bool IsPlayerBanned(string steamId)
        {
            return _moderation.IsPlayerBanned(steamId);
        }

        /// <summary>
        /// Sets tracked player identity information.
        /// </summary>
        /// <param name="connection">The owning connection.</param>
        /// <param name="steamId">The reported SteamID.</param>
        /// <param name="playerName">The reported player name.</param>
        public void SetPlayerIdentity(NetworkConnection connection, string steamId, string playerName)
        {
            _lifecycle.SetPlayerIdentity(connection, steamId, playerName);
        }

        /// <summary>
        /// Gets aggregate player statistics.
        /// </summary>
        /// <returns>Current connected-player statistics.</returns>
        public PlayerStats GetPlayerStats()
        {
            IReadOnlyList<ConnectedPlayerInfo> players = GetConnectedPlayers();
            return new PlayerStats
            {
                ConnectedPlayers = players.Count,
                MaxPlayers = Shared.Configuration.ServerConfig.Instance.MaxPlayers,
                TotalBannedPlayers = _permissionService.GetBanEntries().Count,
                Players = players
            };
        }

        /// <summary>
        /// Sends a disconnect notice and then disconnects the player.
        /// </summary>
        /// <param name="player">The player to disconnect.</param>
        /// <param name="title">The disconnect title.</param>
        /// <param name="reason">The disconnect reason.</param>
        /// <param name="disconnectDelaySeconds">How long to wait before disconnecting.</param>
        /// <returns><see langword="true"/> when the disconnect was queued.</returns>
        public bool NotifyAndDisconnectPlayer(
            ConnectedPlayerInfo player,
            string title,
            string reason,
            float disconnectDelaySeconds = 0.25f)
        {
            return _moderation.NotifyAndDisconnectPlayer(player, title, reason, disconnectDelaySeconds);
        }

        /// <summary>
        /// Sends a shutdown notice to all tracked players and disconnects them.
        /// </summary>
        /// <param name="reason">The shutdown reason.</param>
        /// <param name="noticeDelayMilliseconds">How long to wait before disconnecting.</param>
        public void NotifyShutdownAndDisconnectAll(string reason, int noticeDelayMilliseconds = 500)
        {
            _moderation.NotifyShutdownAndDisconnectAll(reason, noticeDelayMilliseconds);
        }

        internal IReadOnlyList<ConnectedPlayerInfo> GetTrackedConnectedPlayers()
        {
            return _registry.GetConnectedPlayers(includeLoopbackConnections: true);
        }

        internal bool BringPlayer(ConnectedPlayerInfo targetPlayer, ConnectedPlayerInfo destinationPlayer, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!TryGetArrivalTransform(destinationPlayer, out Vector3 arrivalPosition, out Quaternion arrivalRotation, out errorMessage))
            {
                return false;
            }

            return _teleportation.Teleport(targetPlayer, arrivalPosition, arrivalRotation, alignFeetToPosition: true, out errorMessage);
        }

        internal bool ReturnPlayerToPreviousPosition(ConnectedPlayerInfo targetPlayer, out string errorMessage)
        {
            return _teleportation.ReturnToPreviousPosition(targetPlayer, out errorMessage);
        }

        internal bool HasReturnPosition(ConnectedPlayerInfo targetPlayer)
        {
            return _teleportation.HasReturnPosition(targetPlayer);
        }

        internal bool IsPlayerVanished(ConnectedPlayerInfo targetPlayer)
        {
            return _visibility.IsVanished(targetPlayer);
        }

        internal bool SetPlayerVanished(ConnectedPlayerInfo targetPlayer, bool isVanished, out string errorMessage)
        {
            return _visibility.SetVanished(targetPlayer, isVanished, out errorMessage);
        }

        internal void Initialize()
        {
            try
            {
                _lifecycle.Initialize();
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Failed to initialize player manager: {ex}");
                throw;
            }
        }

        internal void HandleAuthHello(NetworkConnection connection)
        {
            _lifecycle.HandleAuthHello(connection);
        }

        internal void HandleAuthTicket(NetworkConnection connection, string data)
        {
            _lifecycle.HandleAuthTicket(connection, data);
        }

        internal void HandleModVerificationReport(NetworkConnection connection, string data)
        {
            _lifecycle.HandleModVerificationReport(connection, data);
        }

        internal bool IsCommandAllowedForConnection(NetworkConnection connection, string command)
        {
            return _lifecycle.IsCommandAllowedForConnection(connection, command);
        }

        internal void SendInitialServerDataToClient(NetworkConnection connection)
        {
            _joinBootstrap.SendInitialServerDataToClient(connection);
        }

        internal void Shutdown()
        {
            try
            {
                DebugLog.Info($"Shutting down player manager - {_registry.GetAllTrackedPlayers().Count} players to disconnect");
                _moderation.DisconnectAllImmediately("Player manager shutdown");
                _lifecycle.Shutdown();
                _registry.Clear();
                DebugLog.Info("Player manager shutdown complete");
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error during player manager shutdown: {ex}");
            }
        }

        /// <summary>
        /// Raised after a tracked player completes the join flow.
        /// </summary>
        public event Action<ConnectedPlayerInfo> OnPlayerJoined;

        /// <summary>
        /// Raised after a tracked player disconnects and cleanup completes.
        /// </summary>
        public event Action<ConnectedPlayerInfo> OnPlayerLeft;

        /// <summary>
        /// Raised when tracked identity information becomes available for a player.
        /// </summary>
        public event Action<ConnectedPlayerInfo> OnPlayerSpawned;

        private void PublishPlayerJoined(ConnectedPlayerInfo playerInfo)
        {
            OnPlayerJoined?.Invoke(playerInfo);
        }

        private void PublishPlayerLeft(ConnectedPlayerInfo playerInfo)
        {
            _teleportation.ClearPlayerState(playerInfo);
            _visibility.HandlePlayerLeft(playerInfo);
            OnPlayerLeft?.Invoke(playerInfo);
        }

        private void PublishPlayerSpawned(ConnectedPlayerInfo playerInfo)
        {
            _visibility.HandlePlayerSpawned(playerInfo);
            OnPlayerSpawned?.Invoke(playerInfo);
        }

        private static bool TryGetArrivalTransform(
            ConnectedPlayerInfo destinationPlayer,
            out Vector3 arrivalPosition,
            out Quaternion arrivalRotation,
            out string errorMessage)
        {
            arrivalPosition = default;
            arrivalRotation = Quaternion.identity;
            errorMessage = string.Empty;

            if (destinationPlayer?.PlayerInstance == null)
            {
                errorMessage = destinationPlayer == null
                    ? "Destination player is required."
                    : $"{destinationPlayer.DisplayName} is not spawned.";
                return false;
            }

            Transform transform = destinationPlayer.PlayerInstance.transform;
            Vector3 forward = transform.forward;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            arrivalPosition = transform.position + forward.normalized * 2.25f;
            arrivalRotation = transform.rotation;
            return true;
        }
    }
}
