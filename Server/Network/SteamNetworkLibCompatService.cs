using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using FishNet.Connection;
using MelonLoader;
using Newtonsoft.Json;
using Steamworks;
using DSConstants = DedicatedServerMod.Utils.Constants;

#nullable enable

namespace DedicatedServerMod.Server.Network
{
    /// <summary>
    /// Dedicated-session compatibility bridge for SteamNetworkLib clients.
    /// Emulates lobby/member data semantics and routes logical P2P payloads through CustomMessaging.
    /// </summary>
    public sealed class SteamNetworkLibCompatService
    {
        private readonly MelonLogger.Instance _logger;
        private readonly PlayerManager _playerManager;
        private readonly object _stateLock = new object();

        private readonly Dictionary<string, MemberState> _members = new Dictionary<string, MemberState>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _lobbyData = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, string>> _memberData =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private string _sessionId = Guid.NewGuid().ToString("N");
        private string _ownerSteamId = string.Empty;
        private bool _initialized;

        /// <summary>
        /// Initializes compatibility service state and event hooks.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            lock (_stateLock)
            {
                _sessionId = Guid.NewGuid().ToString("N");
                _members.Clear();
                _memberData.Clear();
                _ownerSteamId = string.Empty;
                RebuildMembersFromPlayerManager();
            }

            _playerManager.OnPlayerJoined += OnPlayerJoined;
            _playerManager.OnPlayerLeft += OnPlayerLeft;
            _initialized = true;

            _logger.Msg("SteamNetworkLib compatibility service initialized");
        }

        /// <summary>
        /// Shuts down compatibility hooks and clears runtime state.
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _playerManager.OnPlayerJoined -= OnPlayerJoined;
            _playerManager.OnPlayerLeft -= OnPlayerLeft;

            lock (_stateLock)
            {
                _members.Clear();
                _lobbyData.Clear();
                _memberData.Clear();
                _ownerSteamId = string.Empty;
            }

            _initialized = false;
        }

        /// <summary>
        /// Handles client register requests and returns a full compatibility snapshot.
        /// </summary>
        /// <param name="conn">Requesting client connection.</param>
        public void HandleRegister(NetworkConnection conn)
        {
            if (conn == null)
            {
                return;
            }

            string localSteamId = ResolveConnectionSteamId(conn);
            if (string.IsNullOrEmpty(localSteamId))
            {
                return;
            }

            lock (_stateLock)
            {
                EnsureMemberEntry(conn, localSteamId);
                EnsureOwnerSelected();

                var payload = BuildSnapshotPayload(localSteamId);
                CustomMessaging.SendToClient(conn, DSConstants.Messages.SnlDedicatedSnapshot, JsonConvert.SerializeObject(payload));
            }
        }

        /// <summary>
        /// Handles lobby-data writes from SteamNetworkLib clients.
        /// </summary>
        /// <param name="conn">Requesting client connection.</param>
        /// <param name="data">Serialized set-lobby-data request.</param>
        public void HandleSetLobbyData(NetworkConnection conn, string data)
        {
            if (conn == null)
            {
                return;
            }

            SetLobbyDataRequest? request = JsonConvert.DeserializeObject<SetLobbyDataRequest>(data ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.Key))
            {
                return;
            }

            string senderSteamId = ResolveConnectionSteamId(conn);
            if (string.IsNullOrEmpty(senderSteamId))
            {
                return;
            }

            lock (_stateLock)
            {
                EnsureOwnerSelected();
                if (!string.Equals(senderSteamId, _ownerSteamId, StringComparison.Ordinal))
                {
                    return;
                }

                _lobbyData.TryGetValue(request.Key, out string oldValue);
                _lobbyData[request.Key] = request.Value ?? string.Empty;

                var payload = new LobbyDataChangedPayload
                {
                    Key = request.Key,
                    OldValue = oldValue ?? string.Empty,
                    NewValue = request.Value,
                    ChangedBySteamId = senderSteamId
                };

                BroadcastToMembers(DSConstants.Messages.SnlDedicatedLobbyDataChanged, payload, excludeSteamId: null);
            }
        }

        /// <summary>
        /// Handles member-data writes from SteamNetworkLib clients.
        /// </summary>
        /// <param name="conn">Requesting client connection.</param>
        /// <param name="data">Serialized set-member-data request.</param>
        public void HandleSetMemberData(NetworkConnection conn, string data)
        {
            if (conn == null)
            {
                return;
            }

            SetMemberDataRequest? request = JsonConvert.DeserializeObject<SetMemberDataRequest>(data ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.Key))
            {
                return;
            }

            string memberSteamId = ResolveConnectionSteamId(conn);
            if (string.IsNullOrEmpty(memberSteamId))
            {
                return;
            }

            lock (_stateLock)
            {
                Dictionary<string, string> map = GetOrCreateMemberDataMap(memberSteamId);
                map.TryGetValue(request.Key, out string oldValue);
                map[request.Key] = request.Value ?? string.Empty;

                var payload = new MemberDataChangedPayload
                {
                    MemberSteamId = memberSteamId,
                    Key = request.Key,
                    OldValue = oldValue ?? string.Empty,
                    NewValue = request.Value
                };

                BroadcastToMembers(DSConstants.Messages.SnlDedicatedMemberDataChanged, payload, excludeSteamId: null);
            }
        }

        /// <summary>
        /// Routes logical P2P payloads between SteamNetworkLib clients through server-authoritative messaging.
        /// </summary>
        /// <param name="conn">Sending client connection.</param>
        /// <param name="data">Serialized logical P2P packet envelope.</param>
        public void HandleP2PSend(NetworkConnection conn, string data)
        {
            if (conn == null)
            {
                return;
            }

            P2PSendRequest? request = JsonConvert.DeserializeObject<P2PSendRequest>(data ?? string.Empty);
            if (request == null || string.IsNullOrWhiteSpace(request.DataBase64))
            {
                return;
            }

            string senderSteamId = ResolveConnectionSteamId(conn);
            if (string.IsNullOrEmpty(senderSteamId))
            {
                return;
            }

            var payload = new P2PMessagePayload
            {
                SenderSteamId = senderSteamId,
                DataBase64 = request.DataBase64
            };

            lock (_stateLock)
            {
                if (!string.IsNullOrWhiteSpace(request.TargetSteamId))
                {
                    NetworkConnection targetConn = ResolveConnectionBySteamId(request.TargetSteamId);
                    if (targetConn != null)
                    {
                        CustomMessaging.SendToClient(targetConn, DSConstants.Messages.SnlDedicatedP2PMessage, JsonConvert.SerializeObject(payload));
                    }

                    return;
                }

                BroadcastToMembers(DSConstants.Messages.SnlDedicatedP2PMessage, payload, excludeSteamId: senderSteamId);
            }
        }

        private void OnPlayerJoined(ConnectedPlayerInfo playerInfo)
        {
            if (!TryGetSteamId(playerInfo, out string steamId))
            {
                return;
            }

            lock (_stateLock)
            {
                EnsureMemberEntry(playerInfo.Connection, steamId);
                EnsureOwnerSelected();

                if (!_members.TryGetValue(steamId, out MemberState state))
                {
                    return;
                }

                var payload = new MemberJoinedPayload
                {
                    Member = state.ToSnapshot(steamId, _ownerSteamId),
                    OwnerSteamId = _ownerSteamId
                };

                BroadcastToMembers(DSConstants.Messages.SnlDedicatedMemberJoined, payload, excludeSteamId: null);
            }
        }

        private void OnPlayerLeft(ConnectedPlayerInfo playerInfo)
        {
            if (!TryGetSteamId(playerInfo, out string steamId))
            {
                return;
            }

            lock (_stateLock)
            {
                _members.Remove(steamId);
                _memberData.Remove(steamId);

                if (string.Equals(_ownerSteamId, steamId, StringComparison.Ordinal))
                {
                    _ownerSteamId = string.Empty;
                    EnsureOwnerSelected();
                }

                var payload = new MemberLeftPayload
                {
                    SteamId = steamId,
                    OwnerSteamId = _ownerSteamId
                };

                BroadcastToMembers(DSConstants.Messages.SnlDedicatedMemberLeft, payload, excludeSteamId: null);
            }
        }

        private void RebuildMembersFromPlayerManager()
        {
            List<ConnectedPlayerInfo> players = _playerManager.GetConnectedPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                ConnectedPlayerInfo player = players[i];
                if (!TryGetSteamId(player, out string steamId))
                {
                    continue;
                }

                EnsureMemberEntry(player.Connection, steamId);
            }

            EnsureOwnerSelected();
        }

        private void EnsureMemberEntry(NetworkConnection connection, string steamId)
        {
            if (string.IsNullOrEmpty(steamId))
            {
                return;
            }

            ConnectedPlayerInfo info = _playerManager.GetPlayer(connection) ?? _playerManager.GetPlayerBySteamId(steamId);
            if (info == null)
            {
                return;
            }

            if (!_members.TryGetValue(steamId, out MemberState state))
            {
                state = new MemberState
                {
                    JoinedAtUtc = DateTime.UtcNow
                };
                _members[steamId] = state;
            }

            state.DisplayName = info.DisplayName;
            state.Connection = info.Connection;
            GetOrCreateMemberDataMap(steamId);
        }

        private void EnsureOwnerSelected()
        {
            if (!string.IsNullOrEmpty(_ownerSteamId) && _members.ContainsKey(_ownerSteamId))
            {
                return;
            }

            _ownerSteamId = _members.Keys.OrderBy(key => key, StringComparer.Ordinal).FirstOrDefault() ?? string.Empty;
        }

        private SnapshotPayload BuildSnapshotPayload(string localSteamId)
        {
            var payload = new SnapshotPayload
            {
                SessionId = _sessionId,
                LocalSteamId = localSteamId,
                OwnerSteamId = _ownerSteamId,
                ServerSteamId = GetServerSteamId(),
                Members = new List<MemberSnapshot>(),
                LobbyData = new Dictionary<string, string>(_lobbyData, StringComparer.Ordinal),
                MemberData = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
            };

            foreach (KeyValuePair<string, MemberState> kvp in _members)
            {
                payload.Members.Add(kvp.Value.ToSnapshot(kvp.Key, _ownerSteamId, localSteamId));
            }

            foreach (KeyValuePair<string, Dictionary<string, string>> kvp in _memberData)
            {
                payload.MemberData[kvp.Key] = new Dictionary<string, string>(kvp.Value, StringComparer.Ordinal);
            }

            return payload;
        }

        private void BroadcastToMembers(string command, object payload, string? excludeSteamId)
        {
            string json = payload is string raw ? raw : JsonConvert.SerializeObject(payload);
            foreach (KeyValuePair<string, MemberState> kvp in _members)
            {
                if (!string.IsNullOrEmpty(excludeSteamId) && string.Equals(kvp.Key, excludeSteamId, StringComparison.Ordinal))
                {
                    continue;
                }

                NetworkConnection conn = kvp.Value.Connection;
                if (conn == null || !conn.IsActive)
                {
                    continue;
                }

                CustomMessaging.SendToClient(conn, command, json);
            }
        }

        private string ResolveConnectionSteamId(NetworkConnection conn)
        {
            ConnectedPlayerInfo info = _playerManager.GetPlayer(conn);
            return info?.TrustedUniqueId ?? string.Empty;
        }

        private NetworkConnection? ResolveConnectionBySteamId(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
            {
                return null;
            }

            ConnectedPlayerInfo info = _playerManager.GetPlayerBySteamId(steamId);
            if (info?.Connection != null && info.Connection.IsActive)
            {
                return info.Connection;
            }

            return null;
        }

        private static bool TryGetSteamId(ConnectedPlayerInfo playerInfo, out string steamId)
        {
            steamId = playerInfo?.TrustedUniqueId ?? string.Empty;
            return IsValidSteamId(steamId);
        }

        private static bool IsValidSteamId(string steamId)
        {
            return ulong.TryParse(steamId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) && value > 0;
        }

        private static string GetServerSteamId()
        {
            try
            {
                return SteamGameServer.GetSteamID().m_SteamID.ToString(CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private Dictionary<string, string> GetOrCreateMemberDataMap(string steamId)
        {
            if (!_memberData.TryGetValue(steamId, out Dictionary<string, string> map))
            {
                map = new Dictionary<string, string>(StringComparer.Ordinal);
                _memberData[steamId] = map;
            }

            return map;
        }

        private sealed class MemberState
        {
            public string DisplayName { get; set; } = string.Empty;
            public DateTime JoinedAtUtc { get; set; }
            public NetworkConnection? Connection { get; set; }

            public MemberSnapshot ToSnapshot(string steamId, string ownerSteamId, string localSteamId = "")
            {
                return new MemberSnapshot
                {
                    SteamId = steamId,
                    DisplayName = DisplayName,
                    IsOwner = string.Equals(steamId, ownerSteamId, StringComparison.Ordinal),
                    IsLocalPlayer = !string.IsNullOrEmpty(localSteamId) && string.Equals(steamId, localSteamId, StringComparison.Ordinal),
                    JoinedAtUnixMs = ToUnixMilliseconds(JoinedAtUtc)
                };
            }
        }

        private static long ToUnixMilliseconds(DateTime value)
        {
            DateTime utcValue = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return new DateTimeOffset(utcValue).ToUnixTimeMilliseconds();
        }

        [Serializable]
        private sealed class SnapshotPayload
        {
            public string SessionId { get; set; } = string.Empty;
            public string LocalSteamId { get; set; } = string.Empty;
            public string OwnerSteamId { get; set; } = string.Empty;
            public string ServerSteamId { get; set; } = string.Empty;
            public List<MemberSnapshot> Members { get; set; } = new List<MemberSnapshot>();
            public Dictionary<string, string> LobbyData { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, Dictionary<string, string>> MemberData { get; set; } =
                new Dictionary<string, Dictionary<string, string>>();
        }

        [Serializable]
        private sealed class MemberSnapshot
        {
            public string SteamId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public bool IsOwner { get; set; }
            public bool IsLocalPlayer { get; set; }
            public long JoinedAtUnixMs { get; set; }
        }

        [Serializable]
        private sealed class MemberJoinedPayload
        {
            public MemberSnapshot Member { get; set; } = new MemberSnapshot();
            public string OwnerSteamId { get; set; } = string.Empty;
        }

        [Serializable]
        private sealed class MemberLeftPayload
        {
            public string SteamId { get; set; } = string.Empty;
            public string OwnerSteamId { get; set; } = string.Empty;
        }

        [Serializable]
        private sealed class LobbyDataChangedPayload
        {
            public string Key { get; set; } = string.Empty;
            public string OldValue { get; set; } = string.Empty;
            public string NewValue { get; set; } = string.Empty;
            public string ChangedBySteamId { get; set; } = string.Empty;
        }

        [Serializable]
        private sealed class MemberDataChangedPayload
        {
            public string MemberSteamId { get; set; } = string.Empty;
            public string Key { get; set; } = string.Empty;
            public string OldValue { get; set; } = string.Empty;
            public string NewValue { get; set; } = string.Empty;
        }

        [Serializable]
        private sealed class SetLobbyDataRequest
        {
            public string Key { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }

        [Serializable]
        private sealed class SetMemberDataRequest
        {
            public string Key { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }

        [Serializable]
        private sealed class P2PSendRequest
        {
            public string TargetSteamId { get; set; } = string.Empty;
            public string DataBase64 { get; set; } = string.Empty;
        }

        [Serializable]
        private sealed class P2PMessagePayload
        {
            public string SenderSteamId { get; set; } = string.Empty;
            public string DataBase64 { get; set; } = string.Empty;
        }

        /// <summary>
        /// Initializes a new compatibility service.
        /// </summary>
        /// <param name="logger">Server logger instance.</param>
        /// <param name="playerManager">Player manager used for identity/connection resolution.</param>
        public SteamNetworkLibCompatService(MelonLogger.Instance logger, PlayerManager playerManager)
        {
            _logger = logger ?? new MelonLogger.Instance("SteamNetworkLibCompatService");
            _playerManager = playerManager;
        }
    }
}
