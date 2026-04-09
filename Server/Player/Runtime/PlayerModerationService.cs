using System.Threading;
using DedicatedServerMod.Server.Permissions;
using DedicatedServerMod.Utils;

namespace DedicatedServerMod.Server.Player.Runtime
{
    /// <summary>
    /// Handles operational moderation actions against tracked players.
    /// </summary>
    internal sealed class PlayerModerationService
    {
        private readonly PlayerSessionRegistry _registry;
        private readonly PlayerClientMessagingService _messaging;
        private readonly ServerPermissionService _permissionService;

        internal PlayerModerationService(
            PlayerSessionRegistry registry,
            PlayerClientMessagingService messaging,
            ServerPermissionService permissionService)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        }

        internal bool KickPlayer(ConnectedPlayerInfo player, string reason = "Kicked by admin")
        {
            try
            {
                DebugLog.Info($"Kicking player {player.DisplayName}: {reason}");
                return NotifyAndDisconnectPlayer(player, "Kicked", reason);
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error kicking player: {ex}");
                return false;
            }
        }

        internal bool BanPlayer(ConnectedPlayerInfo player, string reason = "Banned by admin")
        {
            try
            {
                string banIdentifier = !string.IsNullOrEmpty(player?.AuthenticatedSteamId)
                    ? player.AuthenticatedSteamId
                    : player?.SteamId;

                if (string.IsNullOrEmpty(banIdentifier))
                {
                    DebugLog.Warning("Cannot ban player without SteamID");
                    return false;
                }

                if (_permissionService.AddBan(null, banIdentifier, reason) != true)
                {
                    DebugLog.Warning($"Ban for {banIdentifier} was rejected or already exists.");
                    return false;
                }

                NotifyAndDisconnectPlayer(player, "Banned", $"Banned: {reason}");
                DebugLog.Info($"Player banned: {player.DisplayName} ({banIdentifier}) - {reason}");
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error banning player: {ex}");
                return false;
            }
        }

        internal bool IsPlayerBanned(string steamId)
        {
            return _permissionService.IsBanned(steamId);
        }

        internal bool NotifyAndDisconnectPlayer(
            ConnectedPlayerInfo player,
            string title,
            string reason,
            float disconnectDelaySeconds = 0.25f)
        {
            if (player?.Connection == null || !player.Connection.IsActive)
            {
                return false;
            }

            try
            {
                _messaging.SendDisconnectNotice(player, title, reason);
                _messaging.BeginDisconnectAfterDelay(player, disconnectDelaySeconds);
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Error($"Error notifying/disconnecting player {player.DisplayName}: {ex}");
                return false;
            }
        }

        internal void NotifyShutdownAndDisconnectAll(string reason, int noticeDelayMilliseconds = 500)
        {
            IReadOnlyList<ConnectedPlayerInfo> playersToKick = _registry.GetAllTrackedPlayers();
            if (playersToKick.Count == 0)
            {
                return;
            }

            for (int i = 0; i < playersToKick.Count; i++)
            {
                ConnectedPlayerInfo player = playersToKick[i];
                try
                {
                    if (player.Connection == null || !player.Connection.IsActive)
                    {
                        continue;
                    }

                    _messaging.SendDisconnectNotice(player, "Server Shutdown", reason);
                }
                catch (Exception ex)
                {
                    DebugLog.Warning($"Error sending shutdown notice to {player.DisplayName}: {ex.Message}");
                }
            }

            if (noticeDelayMilliseconds > 0)
            {
                Thread.Sleep(noticeDelayMilliseconds);
            }

            DisconnectAllImmediately(reason);
        }

        internal void DisconnectAllImmediately(string reason)
        {
            IReadOnlyList<ConnectedPlayerInfo> playersToDisconnect = _registry.GetAllTrackedPlayers();
            for (int i = 0; i < playersToDisconnect.Count; i++)
            {
                ConnectedPlayerInfo player = playersToDisconnect[i];
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
                    DebugLog.Warning($"Error disconnecting player {player.DisplayName}: {ex.Message}");
                }
            }
        }
    }
}
