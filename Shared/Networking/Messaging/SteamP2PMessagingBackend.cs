using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DedicatedServerMod.Shared.Networking;
using DedicatedServerMod.Utils;
using FishNet;
using FishNet.Connection;
using MelonLoader;
using Newtonsoft.Json;
using Steamworks;
using DSConstants = DedicatedServerMod.Utils.Constants;

#if SERVER
using DedicatedServerMod.Server.Core;
using DedicatedServerMod.Server.Player;
using DedicatedServerMod.Shared.Permissions;
using ScheduleOne.PlayerScripts;
#endif

namespace DedicatedServerMod.Shared.Networking.Messaging
{
    /// <summary>
    /// Steamworks P2P messaging backend.
    /// Uses Steam P2P for primary transport and FishNet RPC as bootstrap fallback
    /// until peer identity mapping is established.
    /// </summary>
    public sealed class SteamP2PMessagingBackend : IMessagingBackend
    {
        private const int EnvelopeVersion = 1;
        private const string ServerSteamIdArg = "--server-steamid";
        private const string ServerSteamIdAltArg = "--server-steam-id";

        private MelonLogger.Instance _logger;
        private bool _isInitialized;
        private bool _allowRelay;
        private int _channel;
        private int _maxPayloadBytes;
        private ulong _serverSteamId;

        private Callback<P2PSessionRequest_t> _p2pSessionRequestCallback;
        private Callback<P2PSessionConnectFail_t> _p2pSessionConnectFailCallback;

        private FishNetRpcMessagingBackend _bootstrapBackend;

#if SERVER
        private readonly Dictionary<int, NetworkConnection> _clientIdToConnection = new Dictionary<int, NetworkConnection>();
        private readonly Dictionary<ulong, NetworkConnection> _steamIdToConnection = new Dictionary<ulong, NetworkConnection>();
        private readonly HashSet<int> _fallbackWarnedClientIds = new HashSet<int>();
#endif

        /// <inheritdoc />
        public MessagingBackendType BackendType => MessagingBackendType.SteamP2P;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public bool IsAvailable
        {
            get
            {
                try
                {
                    return SteamManager.Initialized || SteamAPI.IsSteamRunning();
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <inheritdoc />
        public event Action<string, string> ClientMessageReceived;

        /// <inheritdoc />
        public event Action<NetworkConnection, string, string> ServerMessageReceived;

        /// <inheritdoc />
        public bool Initialize(MelonLogger.Instance logger)
        {
            if (_isInitialized)
            {
                return true;
            }

            _logger = logger ?? new MelonLogger.Instance("SteamP2PMessagingBackend");
            if (!IsAvailable)
            {
                _logger.Error("Steam is not available - cannot initialize Steam P2P messaging backend");
                return false;
            }

            try
            {
                var config = DedicatedServerMod.Shared.Configuration.ServerConfig.Instance;
                _allowRelay = config.SteamP2PAllowRelay;
                _channel = Math.Max(0, config.SteamP2PChannel);
                _maxPayloadBytes = Math.Max(256, config.SteamP2PMaxPayloadBytes);

                SteamNetworking.AllowP2PPacketRelay(_allowRelay);
                _p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
                _p2pSessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);

                _bootstrapBackend = new FishNetRpcMessagingBackend();
                if (!_bootstrapBackend.Initialize(_logger))
                {
                    _logger.Warning("Steam P2P bootstrap backend failed to initialize. Early handshake fallback may be unavailable.");
                }
                else
                {
                    _bootstrapBackend.ClientMessageReceived += OnBootstrapClientMessageReceived;
                    _bootstrapBackend.ServerMessageReceived += OnBootstrapServerMessageReceived;
                }

                TryResolveServerSteamIdFromConfigOrArgs();

                _isInitialized = true;
                _logger.Msg($"Steam P2P messaging initialized (channel={_channel}, relay={_allowRelay}, max={_maxPayloadBytes} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to initialize Steam P2P backend: {ex}");
                return false;
            }
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (!_isInitialized)
            {
                return;
            }

            try
            {
                _p2pSessionRequestCallback?.Dispose();
                _p2pSessionConnectFailCallback?.Dispose();

                if (_bootstrapBackend != null)
                {
                    _bootstrapBackend.ClientMessageReceived -= OnBootstrapClientMessageReceived;
                    _bootstrapBackend.ServerMessageReceived -= OnBootstrapServerMessageReceived;
                    _bootstrapBackend.Shutdown();
                }

#if SERVER
                _clientIdToConnection.Clear();
                _steamIdToConnection.Clear();
                _fallbackWarnedClientIds.Clear();
#endif
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error while shutting down Steam P2P backend: {ex}");
            }
            finally
            {
                _bootstrapBackend = null;
                _isInitialized = false;
            }
        }

        /// <inheritdoc />
        public void Tick()
        {
            if (!_isInitialized)
            {
                return;
            }

#if SERVER
            RefreshServerConnectionMappings();
#endif

            ReadIncomingPackets();
        }

        /// <inheritdoc />
        public void OnDailySummaryAwake(object instance)
        {
            _bootstrapBackend?.OnDailySummaryAwake(instance);
        }

        /// <inheritdoc />
        public bool SendToServer(string command, string data)
        {
            if (!_isInitialized || string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            ulong serverSteamId = ResolveServerSteamId();
            if (serverSteamId == 0)
            {
                // Bootstrap over FishNet until we learn the server SteamID.
                return _bootstrapBackend != null && _bootstrapBackend.SendToServer(command, data);
            }

            SteamEnvelope envelope = new SteamEnvelope
            {
                Version = EnvelopeVersion,
                Command = command,
                Data = data ?? string.Empty,
                SenderClientId = GetLocalClientId(),
                SenderSteamId = GetLocalSteamIdString()
            };

            return SendEnvelope(serverSteamId, envelope, command);
        }

        /// <inheritdoc />
        public bool SendToClient(NetworkConnection conn, string command, string data)
        {
            if (!_isInitialized || conn == null || string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

#if !SERVER
            return false;
#else
            if (!TryResolveSteamIdForConnection(conn, out ulong steamId))
            {
                if (_bootstrapBackend != null)
                {
                    bool fallbackSent = _bootstrapBackend.SendToClient(conn, command, data);
                    if (!_fallbackWarnedClientIds.Contains(conn.ClientId))
                    {
                        _fallbackWarnedClientIds.Add(conn.ClientId);
                        _logger?.Msg($"Steam P2P fallback active for ClientId {conn.ClientId} until SteamID mapping is available.");
                    }

                    return fallbackSent;
                }

                return false;
            }

            SteamEnvelope envelope = new SteamEnvelope
            {
                Version = EnvelopeVersion,
                Command = command,
                Data = data ?? string.Empty,
                SenderClientId = -1,
                SenderSteamId = GetLocalSteamIdString()
            };

            return SendEnvelope(steamId, envelope, command);
#endif
        }

        /// <inheritdoc />
        public int BroadcastToClients(string command, string data)
        {
            if (!_isInitialized || string.IsNullOrWhiteSpace(command) || !InstanceFinder.IsServer)
            {
                return 0;
            }

#if !SERVER
            return 0;
#else
            int sent = 0;
            RefreshServerConnectionMappings();
            foreach (KeyValuePair<int, NetworkConnection> kvp in _clientIdToConnection)
            {
                if (SendToClient(kvp.Value, command, data))
                {
                    sent++;
                }
            }

            return sent;
#endif
        }

        /// <inheritdoc />
        public string GetStatusInfo()
        {
            string localSteamId = GetLocalSteamIdString();
#if SERVER
            return $"LocalSteamId={localSteamId}, Channel={_channel}, Relay={_allowRelay}, KnownClients={_clientIdToConnection.Count}";
#else
            return $"LocalSteamId={localSteamId}, Channel={_channel}, Relay={_allowRelay}, ServerSteamId={_serverSteamId}";
#endif
        }

        /// <inheritdoc />
        public void SetServerPeerHint(string serverSteamId)
        {
            if (TryParseSteamId(serverSteamId, out ulong value))
            {
                _serverSteamId = value;
            }
        }

        private void ReadIncomingPackets()
        {
            while (SteamNetworking.IsP2PPacketAvailable(out uint packetSize, _channel))
            {
                try
                {
                    if (packetSize == 0 || packetSize > _maxPayloadBytes)
                    {
                        byte[] skip = new byte[packetSize];
                        SteamNetworking.ReadP2PPacket(skip, packetSize, out _, out _, _channel);
                        _logger?.Warning($"Dropped Steam P2P packet with invalid size: {packetSize}");
                        continue;
                    }

                    byte[] packetBuffer = new byte[packetSize];
                    if (!SteamNetworking.ReadP2PPacket(packetBuffer, packetSize, out uint bytesRead, out CSteamID remoteId, _channel))
                    {
                        continue;
                    }

                    ProcessIncomingPacket(remoteId, packetBuffer, (int)bytesRead);
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Error reading Steam P2P packet: {ex}");
                }
            }
        }

        private void ProcessIncomingPacket(CSteamID remoteId, byte[] packetBuffer, int bytesRead)
        {
            if (bytesRead <= 0)
            {
                return;
            }

            SteamEnvelope envelope;
            try
            {
                string json = Encoding.UTF8.GetString(packetBuffer, 0, bytesRead);
                envelope = JsonConvert.DeserializeObject<SteamEnvelope>(json);
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Failed to deserialize Steam P2P envelope: {ex.Message}");
                return;
            }

            if (envelope == null || string.IsNullOrWhiteSpace(envelope.Command))
            {
                _logger?.Warning("Dropped Steam P2P envelope with missing command");
                return;
            }

            if (InstanceFinder.IsServer)
            {
#if SERVER
                if (!TryResolveConnectionForRemote(remoteId, envelope, out NetworkConnection conn))
                {
                    _logger?.Warning($"Dropped Steam P2P message '{envelope.Command}': no NetworkConnection mapping for SteamID {remoteId.m_SteamID}");
                    return;
                }

                try
                {
                    ServerMessageReceived?.Invoke(conn, envelope.Command, envelope.Data ?? string.Empty);
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Error dispatching server Steam P2P message '{envelope.Command}': {ex}");
                }
#endif
            }
            else
            {
                _serverSteamId = remoteId.m_SteamID;

                try
                {
                    ClientMessageReceived?.Invoke(envelope.Command, envelope.Data ?? string.Empty);
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Error dispatching client Steam P2P message '{envelope.Command}': {ex}");
                }
            }
        }

        private bool SendEnvelope(ulong targetSteamId, SteamEnvelope envelope, string command)
        {
            if (targetSteamId == 0 || envelope == null)
            {
                return false;
            }

            try
            {
                string json = JsonConvert.SerializeObject(envelope);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                if (payload.Length > _maxPayloadBytes)
                {
                    _logger?.Warning($"Steam P2P send skipped for cmd='{command}': payload {payload.Length} exceeds max {_maxPayloadBytes}");
                    return false;
                }

                bool sent = SteamNetworking.SendP2PPacket(
                    new CSteamID(targetSteamId),
                    payload,
                    (uint)payload.Length,
                    EP2PSend.k_EP2PSendReliable,
                    _channel);

                if (!sent)
                {
                    _logger?.Warning($"Steam P2P send failed for cmd='{command}' target={targetSteamId}");
                }

                return sent;
            }
            catch (Exception ex)
            {
                _logger?.Error($"Steam P2P send error for cmd='{command}': {ex}");
                return false;
            }
        }

        private void OnBootstrapClientMessageReceived(string command, string data)
        {
            TryLearnServerSteamIdFromMessage(command, data);
            ClientMessageReceived?.Invoke(command, data);
        }

        private void OnBootstrapServerMessageReceived(NetworkConnection connection, string command, string data)
        {
            ServerMessageReceived?.Invoke(connection, command, data);
        }

        private void TryLearnServerSteamIdFromMessage(string command, string data)
        {
            if (!string.Equals(command, DSConstants.Messages.AuthChallenge, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                AuthChallengeMessage challenge = JsonConvert.DeserializeObject<AuthChallengeMessage>(data ?? string.Empty);
                if (challenge != null && TryParseSteamId(challenge.ServerSteamId, out ulong steamId))
                {
                    _serverSteamId = steamId;
                }
            }
            catch
            {
                // ignore malformed bootstrap auth challenge payloads
            }
        }

        private static string GetLocalSteamIdString()
        {
            try
            {
                return SteamUser.GetSteamID().m_SteamID.ToString(CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int GetLocalClientId()
        {
            try
            {
                return InstanceFinder.ClientManager?.Connection?.ClientId ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        private ulong ResolveServerSteamId()
        {
            if (_serverSteamId != 0)
            {
                return _serverSteamId;
            }

            TryResolveServerSteamIdFromConfigOrArgs();
            return _serverSteamId;
        }

        private void TryResolveServerSteamIdFromConfigOrArgs()
        {
            if (_serverSteamId != 0)
            {
                return;
            }

            string configuredSteamId = DedicatedServerMod.Shared.Configuration.ServerConfig.Instance.SteamP2PServerSteamId;
            if (TryParseSteamId(configuredSteamId, out ulong parsedFromConfig))
            {
                _serverSteamId = parsedFromConfig;
                return;
            }

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (!string.Equals(arg, ServerSteamIdArg, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(arg, ServerSteamIdAltArg, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 < args.Length && TryParseSteamId(args[i + 1], out ulong parsedFromArg))
                {
                    _serverSteamId = parsedFromArg;
                    return;
                }
            }
        }

#if SERVER
        private void RefreshServerConnectionMappings()
        {
            _clientIdToConnection.Clear();
            _steamIdToConnection.Clear();

            if (InstanceFinder.ServerManager?.Clients == null)
            {
                return;
            }

            foreach (KeyValuePair<int, NetworkConnection> kvp in InstanceFinder.ServerManager.Clients)
            {
                NetworkConnection conn = kvp.Value;
                if (conn == null)
                {
                    continue;
                }

                _clientIdToConnection[conn.ClientId] = conn;
                if (TryResolveSteamIdForConnection(conn, out ulong steamId))
                {
                    _steamIdToConnection[steamId] = conn;
                }
            }
        }

        private bool TryResolveConnectionForRemote(CSteamID remoteId, SteamEnvelope envelope, out NetworkConnection conn)
        {
            conn = null;

            if (envelope != null && envelope.SenderClientId >= 0 && _clientIdToConnection.TryGetValue(envelope.SenderClientId, out conn))
            {
                _steamIdToConnection[remoteId.m_SteamID] = conn;
                return true;
            }

            if (_steamIdToConnection.TryGetValue(remoteId.m_SteamID, out conn))
            {
                return true;
            }

            if (envelope != null && TryParseSteamId(envelope.SenderSteamId, out ulong senderSteamId) && _steamIdToConnection.TryGetValue(senderSteamId, out conn))
            {
                _steamIdToConnection[remoteId.m_SteamID] = conn;
                return true;
            }

            RefreshServerConnectionMappings();

            if (envelope != null && envelope.SenderClientId >= 0 && _clientIdToConnection.TryGetValue(envelope.SenderClientId, out conn))
            {
                _steamIdToConnection[remoteId.m_SteamID] = conn;
                return true;
            }

            if (_steamIdToConnection.TryGetValue(remoteId.m_SteamID, out conn))
            {
                return true;
            }

            if (envelope != null && TryParseSteamId(envelope.SenderSteamId, out senderSteamId) && _steamIdToConnection.TryGetValue(senderSteamId, out conn))
            {
                _steamIdToConnection[remoteId.m_SteamID] = conn;
                return true;
            }

            return false;
        }

        private static bool TryResolveSteamIdForConnection(NetworkConnection connection, out ulong steamId)
        {
            steamId = 0;
            if (connection == null)
            {
                return false;
            }

            try
            {
                ConnectedPlayerInfo connected = ServerBootstrap.Players?.GetPlayer(connection);
                string trusted = connected?.AuthenticatedSteamId;
                if (TryParseSteamId(trusted, out steamId))
                {
                    return true;
                }

                string fallback = connected?.SteamId;
                if (TryParseSteamId(fallback, out steamId))
                {
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                Player player = Player.GetPlayer(connection);
                string resolved = PlayerResolver.GetSteamId(player);
                if (TryParseSteamId(resolved, out steamId))
                {
                    return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }
#endif

        private static bool TryParseSteamId(string raw, out ulong value)
        {
            return ulong.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
        }

        private void OnP2PSessionRequest(P2PSessionRequest_t callback)
        {
            try
            {
                SteamNetworking.AcceptP2PSessionWithUser(callback.m_steamIDRemote);
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Failed to accept Steam P2P session request: {ex.Message}");
            }
        }

        private void OnP2PSessionConnectFail(P2PSessionConnectFail_t callback)
        {
            EP2PSessionError error = (EP2PSessionError)callback.m_eP2PSessionError;
            _logger?.Warning($"Steam P2P session connect failure from {callback.m_steamIDRemote.m_SteamID}: {error}");
        }

        private sealed class SteamEnvelope
        {
            public int Version { get; set; } = EnvelopeVersion;
            public string Command { get; set; } = string.Empty;
            public string Data { get; set; } = string.Empty;
            public int SenderClientId { get; set; } = -1;
            public string SenderSteamId { get; set; } = string.Empty;
        }
    }
}
