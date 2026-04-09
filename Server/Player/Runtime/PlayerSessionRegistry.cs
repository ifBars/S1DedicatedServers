#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
#else
using FishNet;
using FishNet.Connection;
#endif

namespace DedicatedServerMod.Server.Player.Runtime
{
    /// <summary>
    /// Owns the authoritative in-memory registry of tracked player sessions.
    /// </summary>
    internal sealed class PlayerSessionRegistry
    {
        private static readonly TimeSpan PendingConnectionGracePeriod = TimeSpan.FromSeconds(15);

        private readonly Dictionary<int, ConnectedPlayerInfo> _playersByClientId = new Dictionary<int, ConnectedPlayerInfo>();

        internal int ConnectedPlayerCount => GetConnectedPlayers(includeLoopbackConnections: true).Count;

        internal ConnectedPlayerInfo GetPlayer(NetworkConnection connection)
        {
            if (connection == null)
            {
                return null;
            }

            return GetPlayerByClientId(connection.ClientId, connection);
        }

        internal ConnectedPlayerInfo GetPlayerByClientId(int clientId)
        {
            return GetPlayerByClientId(clientId, connection: null);
        }

        internal ConnectedPlayerInfo EnsureTrackedConnection(NetworkConnection connection, bool isLoopbackConnection)
        {
            if (connection == null)
            {
                return null;
            }

            ConnectedPlayerInfo playerInfo = GetPlayerByClientId(connection.ClientId, connection);
            if (playerInfo != null)
            {
                playerInfo.IsLoopbackConnection = isLoopbackConnection;
                playerInfo.IsDisconnectProcessed = false;
                return playerInfo;
            }

            playerInfo = new ConnectedPlayerInfo
            {
                Connection = connection,
                ClientId = connection.ClientId,
                ConnectTime = DateTime.UtcNow,
                IsLoopbackConnection = isLoopbackConnection,
                IsAuthenticated = false,
                IsAuthenticationPending = false,
                IsModVerificationComplete = false,
                IsModVerificationPending = false,
                IsDisconnectProcessed = false
            };

            _playersByClientId[connection.ClientId] = playerInfo;
            return playerInfo;
        }

        internal ConnectedPlayerInfo GetPlayerBySteamId(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
            {
                return null;
            }

            return _playersByClientId.Values.FirstOrDefault(player =>
                IsTrackedPlayerActive(player) &&
                (string.Equals(player.AuthenticatedSteamId, steamId, StringComparison.Ordinal) ||
                 string.Equals(player.SteamId, steamId, StringComparison.Ordinal)));
        }

        internal ConnectedPlayerInfo GetPlayerByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return _playersByClientId.Values.FirstOrDefault(player =>
                IsTrackedPlayerActive(player) &&
                player.PlayerName?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);
        }

        internal IReadOnlyList<ConnectedPlayerInfo> GetConnectedPlayers(bool includeLoopbackConnections)
        {
            return CreateSnapshot(includeLoopbackConnections, activeOnly: true);
        }

        internal int GetVisiblePlayerCount()
        {
            return GetConnectedPlayers(includeLoopbackConnections: false).Count;
        }

        internal IReadOnlyList<ConnectedPlayerInfo> GetAllTrackedPlayers()
        {
            return CreateSnapshot(includeLoopbackConnections: true, activeOnly: false);
        }

        internal void SweepDisconnectedPlayers(Action<ConnectedPlayerInfo> onRemoved = null)
        {
            List<ConnectedPlayerInfo> disconnectedPlayers = _playersByClientId.Values
                .Where(player => player != null && !IsTrackedPlayerActive(player))
                .ToList();

            for (int i = 0; i < disconnectedPlayers.Count; i++)
            {
                onRemoved?.Invoke(disconnectedPlayers[i]);
            }
        }

        internal bool TryRemovePlayer(NetworkConnection connection, ConnectedPlayerInfo playerInfo, out ConnectedPlayerInfo removedPlayer)
        {
            removedPlayer = null;

            if (playerInfo == null)
            {
                if (connection == null)
                {
                    return false;
                }

                playerInfo = GetPlayerByClientId(connection.ClientId);
            }

            return TryRemovePlayer(playerInfo, out removedPlayer);
        }

        internal bool IsTrackedPlayerActive(ConnectedPlayerInfo playerInfo)
        {
            return IsTrackedPlayerActiveCore(playerInfo);
        }

        internal void Clear()
        {
            _playersByClientId.Clear();
        }

        private ConnectedPlayerInfo GetPlayerByClientId(int clientId, NetworkConnection connection)
        {
            if (!_playersByClientId.TryGetValue(clientId, out ConnectedPlayerInfo playerInfo))
            {
                return null;
            }

            if (connection != null)
            {
                playerInfo.Connection = connection;
                playerInfo.ClientId = connection.ClientId;
                playerInfo.IsDisconnectProcessed = false;
            }

            return playerInfo;
        }

        private bool TryRemovePlayer(ConnectedPlayerInfo playerInfo, out ConnectedPlayerInfo removedPlayer)
        {
            removedPlayer = null;
            if (playerInfo == null)
            {
                return false;
            }

            if (!_playersByClientId.Remove(playerInfo.ClientId))
            {
                return false;
            }

            removedPlayer = playerInfo;
            if (removedPlayer.IsDisconnectProcessed)
            {
                return false;
            }

            removedPlayer.IsDisconnectProcessed = true;
            return true;
        }

        private IReadOnlyList<ConnectedPlayerInfo> CreateSnapshot(bool includeLoopbackConnections, bool activeOnly)
        {
            List<ConnectedPlayerInfo> players = new List<ConnectedPlayerInfo>();
            foreach (ConnectedPlayerInfo player in _playersByClientId.Values)
            {
                if (player == null)
                {
                    continue;
                }

                if (activeOnly && !IsTrackedPlayerActive(player))
                {
                    continue;
                }

                if (!includeLoopbackConnections && player.IsLoopbackConnection)
                {
                    continue;
                }

                players.Add(player);
            }

            return players;
        }

        private static bool IsTrackedPlayerActiveCore(ConnectedPlayerInfo playerInfo)
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

            if (!playerInfo.HasCompletedJoinFlow && DateTime.UtcNow - playerInfo.ConnectTime <= PendingConnectionGracePeriod)
            {
                return true;
            }

            return false;
        }
    }
}
