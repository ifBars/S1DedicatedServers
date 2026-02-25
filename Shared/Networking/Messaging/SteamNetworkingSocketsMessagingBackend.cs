using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
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
#endif

namespace DedicatedServerMod.Shared.Networking.Messaging
{
    /// <summary>
    /// Steam Networking Sockets messaging backend.
    /// Uses game-server sockets on dedicated server builds and client sockets on player builds.
    /// Falls back to FishNet RPC messaging during early handshake/bootstrap.
    /// </summary>
    public sealed class SteamNetworkingSocketsMessagingBackend : IMessagingBackend
    {
        private const int EnvelopeVersion = 1;
        private const int DefaultBatchSize = 32;
        private const int ReliableSendFlag = 8;
        private const string ServerSteamIdArg = "--server-steamid";
        private const string ServerSteamIdAltArg = "--server-steam-id";

        private MelonLogger.Instance _logger;
        private bool _isInitialized;
        private int _virtualPort;
        private int _maxPayloadBytes;
        private ulong _serverSteamId;

        private HSteamListenSocket _listenSocket = HSteamListenSocket.Invalid;
        private HSteamNetPollGroup _pollGroup = HSteamNetPollGroup.Invalid;
        private HSteamNetConnection _serverConnection = HSteamNetConnection.Invalid;

        private Callback<SteamNetConnectionStatusChangedCallback_t> _connectionStatusCallback;
        private FishNetRpcMessagingBackend _bootstrapBackend;

        private readonly Dictionary<ulong, HSteamNetConnection> _steamIdToSocket = new Dictionary<ulong, HSteamNetConnection>();
        private readonly Dictionary<uint, ulong> _socketHandleToSteamId = new Dictionary<uint, ulong>();

#if SERVER
        private readonly Dictionary<int, NetworkConnection> _clientIdToConnection = new Dictionary<int, NetworkConnection>();
        private readonly Dictionary<ulong, NetworkConnection> _steamIdToConnection = new Dictionary<ulong, NetworkConnection>();
        private readonly HashSet<int> _fallbackWarnedClientIds = new HashSet<int>();
#endif

        /// <inheritdoc />
        public MessagingBackendType BackendType => MessagingBackendType.SteamNetworkingSockets;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public bool IsAvailable
        {
            get
            {
                try
                {
#if SERVER
                    SteamGameServer.GetSteamID();
                    return true;
#else
                    return SteamManager.Initialized || SteamAPI.IsSteamRunning();
#endif
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

            _logger = logger ?? new MelonLogger.Instance("SteamNetworkingSocketsMessagingBackend");
            if (!IsAvailable)
            {
                _logger.Error("Steam sockets are not available - cannot initialize Steam Networking Sockets backend");
                return false;
            }

            try
            {
                var config = DedicatedServerMod.Shared.Configuration.ServerConfig.Instance;
                _virtualPort = Math.Max(0, config.SteamP2PChannel);
                _maxPayloadBytes = Math.Max(256, config.SteamP2PMaxPayloadBytes);

                _connectionStatusCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);

                _pollGroup = CreatePollGroup();
                if (IsInvalid(_pollGroup))
                {
                    _logger.Error("Failed to create Steam sockets poll group");
                    return false;
                }

#if SERVER
                SteamGameServerNetworkingUtils.InitRelayNetworkAccess();
                _listenSocket = SteamGameServerNetworkingSockets.CreateListenSocketP2P(_virtualPort, 0, null);
                if (IsInvalid(_listenSocket))
                {
                    _logger.Error($"Failed to create Steam sockets listen socket for virtual port {_virtualPort}");
                    return false;
                }
#else
                SteamNetworkingUtils.InitRelayNetworkAccess();
#endif

                _bootstrapBackend = new FishNetRpcMessagingBackend();
                if (!_bootstrapBackend.Initialize(_logger))
                {
                    _logger.Warning("Steam sockets bootstrap backend failed to initialize. Early handshake fallback may be unavailable.");
                }
                else
                {
                    _bootstrapBackend.ClientMessageReceived += OnBootstrapClientMessageReceived;
                    _bootstrapBackend.ServerMessageReceived += OnBootstrapServerMessageReceived;
                }

                TryResolveServerSteamIdFromConfigOrArgs();

                _isInitialized = true;
                _logger.Msg($"Steam sockets messaging initialized (virtualPort={_virtualPort}, max={_maxPayloadBytes} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to initialize Steam sockets backend: {ex}");
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
                foreach (KeyValuePair<ulong, HSteamNetConnection> kvp in _steamIdToSocket)
                {
                    CloseConnection(kvp.Value, 0, "Messaging shutdown", false);
                }

                _steamIdToSocket.Clear();
                _socketHandleToSteamId.Clear();

#if SERVER
                _clientIdToConnection.Clear();
                _steamIdToConnection.Clear();
                _fallbackWarnedClientIds.Clear();
#endif

                if (!IsInvalid(_listenSocket))
                {
                    CloseListenSocket(_listenSocket);
                    _listenSocket = HSteamListenSocket.Invalid;
                }

                if (!IsInvalid(_pollGroup))
                {
                    DestroyPollGroup(_pollGroup);
                    _pollGroup = HSteamNetPollGroup.Invalid;
                }

                _serverConnection = HSteamNetConnection.Invalid;
                _connectionStatusCallback = null;

                if (_bootstrapBackend != null)
                {
                    _bootstrapBackend.ClientMessageReceived -= OnBootstrapClientMessageReceived;
                    _bootstrapBackend.ServerMessageReceived -= OnBootstrapServerMessageReceived;
                    _bootstrapBackend.Shutdown();
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error while shutting down Steam sockets backend: {ex}");
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
            if (!_isInitialized || !IsAvailable)
            {
                return;
            }

            try
            {
                RunCallbacks();
#if SERVER
                RefreshServerConnectionMappings();
#endif
                ReadIncomingMessages();
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Steam sockets tick failed: {ex.Message}");
            }
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

            if (ShouldPreferBootstrapClientToServer(command) && _bootstrapBackend != null)
            {
                if (_bootstrapBackend.SendToServer(command, data))
                {
                    return true;
                }
            }

            ulong serverSteamId = ResolveServerSteamId();
            if (serverSteamId == 0)
            {
                return _bootstrapBackend != null && _bootstrapBackend.SendToServer(command, data);
            }

            HSteamNetConnection conn = EnsureClientConnection(serverSteamId);
            if (IsInvalid(conn))
            {
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

            if (SendEnvelope(conn, envelope, command))
            {
                return true;
            }

            return _bootstrapBackend != null && _bootstrapBackend.SendToServer(command, data);
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
                return TrySendFallbackToClient(conn, command, data);
            }

            if (!_steamIdToSocket.TryGetValue(steamId, out HSteamNetConnection socket) || IsInvalid(socket))
            {
                return TrySendFallbackToClient(conn, command, data);
            }

            SteamEnvelope envelope = new SteamEnvelope
            {
                Version = EnvelopeVersion,
                Command = command,
                Data = data ?? string.Empty,
                SenderClientId = -1,
                SenderSteamId = GetLocalSteamIdString()
            };

            if (SendEnvelope(socket, envelope, command))
            {
                return true;
            }

            return TrySendFallbackToClient(conn, command, data);
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
            RefreshServerConnectionMappings();
            int sent = 0;
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
            return $"LocalSteamId={localSteamId}, VirtualPort={_virtualPort}, ListenSocket={_listenSocket.m_HSteamListenSocket}, Peers={_steamIdToSocket.Count}";
#else
            return $"LocalSteamId={localSteamId}, VirtualPort={_virtualPort}, ServerSteamId={_serverSteamId}, Connected={(!IsInvalid(_serverConnection))}";
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

        private void ReadIncomingMessages()
        {
            if (IsInvalid(_pollGroup))
            {
                return;
            }

            IntPtr[] messagePointers = new IntPtr[DefaultBatchSize];
            while (true)
            {
                int count = ReceiveMessagesOnPollGroup(_pollGroup, messagePointers, messagePointers.Length);
                if (count <= 0)
                {
                    break;
                }

                for (int i = 0; i < count; i++)
                {
                    IntPtr ptr = messagePointers[i];
                    if (ptr == IntPtr.Zero)
                    {
                        continue;
                    }

                    try
                    {
                        SteamNetworkingMessage_t message = SteamNetworkingMessage_t.FromIntPtr(ptr);
                        ProcessIncomingMessage(message);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warning($"Failed to process Steam sockets message: {ex.Message}");
                    }
                    finally
                    {
                        SteamNetworkingMessage_t.Release(ptr);
                        messagePointers[i] = IntPtr.Zero;
                    }
                }
            }
        }

        private void ProcessIncomingMessage(SteamNetworkingMessage_t message)
        {
            if (message.m_cbSize <= 0 || message.m_cbSize > _maxPayloadBytes || message.m_pData == IntPtr.Zero)
            {
                return;
            }

            byte[] packet = new byte[message.m_cbSize];
            Marshal.Copy(message.m_pData, packet, 0, packet.Length);

            SteamEnvelope envelope;
            try
            {
                string json = Encoding.UTF8.GetString(packet, 0, packet.Length);
                envelope = JsonConvert.DeserializeObject<SteamEnvelope>(json);
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Failed to deserialize Steam sockets envelope: {ex.Message}");
                return;
            }

            if (envelope == null || string.IsNullOrWhiteSpace(envelope.Command))
            {
                return;
            }

            ulong remoteSteamId = message.m_identityPeer.GetSteamID64();
            RememberSocket(remoteSteamId, message.m_conn);

            if (InstanceFinder.IsServer)
            {
#if SERVER
                if (!TryResolveConnectionForInbound(message.m_conn, remoteSteamId, envelope, out NetworkConnection conn))
                {
                    _logger?.Warning($"Dropped Steam sockets cmd='{envelope.Command}': no FishNet connection mapping for SteamID {remoteSteamId}");
                    return;
                }

                ServerMessageReceived?.Invoke(conn, envelope.Command, envelope.Data ?? string.Empty);
#endif
            }
            else
            {
                if (remoteSteamId != 0)
                {
                    _serverSteamId = remoteSteamId;
                }

                ClientMessageReceived?.Invoke(envelope.Command, envelope.Data ?? string.Empty);
            }
        }

        private bool SendEnvelope(HSteamNetConnection connection, SteamEnvelope envelope, string command)
        {
            if (IsInvalid(connection) || envelope == null)
            {
                return false;
            }

            try
            {
                string json = JsonConvert.SerializeObject(envelope);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                if (payload.Length > _maxPayloadBytes)
                {
                    _logger?.Warning($"Steam sockets send skipped for cmd='{command}': payload {payload.Length} exceeds max {_maxPayloadBytes}");
                    return false;
                }

                GCHandle pinned = GCHandle.Alloc(payload, GCHandleType.Pinned);
                try
                {
                    long messageNumber;
                    EResult sendResult = SendMessageToConnection(connection, pinned.AddrOfPinnedObject(), (uint)payload.Length, ReliableSendFlag, out messageNumber);
                    if (sendResult != EResult.k_EResultOK)
                    {
                        _logger?.Warning($"Steam sockets send failed for cmd='{command}', result={sendResult}");
                        return false;
                    }

                    return true;
                }
                finally
                {
                    pinned.Free();
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Steam sockets send error for cmd='{command}': {ex}");
                return false;
            }
        }

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t callback)
        {
            if (!_isInitialized)
            {
                return;
            }

            try
            {
                HSteamNetConnection conn = callback.m_hConn;
                ulong remoteSteamId = callback.m_info.m_identityRemote.GetSteamID64();

                switch (callback.m_info.m_eState)
                {
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
#if SERVER
                        if (callback.m_info.m_hListenSocket == _listenSocket)
                        {
                            EResult acceptResult = SteamGameServerNetworkingSockets.AcceptConnection(conn);
                            if (acceptResult != EResult.k_EResultOK)
                            {
                                _logger?.Warning($"Failed to accept Steam sockets connection {conn.m_HSteamNetConnection}: {acceptResult}");
                                CloseConnection(conn, 0, "Accept failed", false);
                                return;
                            }

                            SteamGameServerNetworkingSockets.SetConnectionPollGroup(conn, _pollGroup);
                        }
#endif
                        break;

                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
#if !SERVER
                        SteamNetworkingSockets.SetConnectionPollGroup(conn, _pollGroup);
                        _serverConnection = conn;
#endif
                        RememberSocket(remoteSteamId, conn);
                        break;

                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Dead:
                        ForgetSocket(conn);

#if !SERVER
                        if (conn.Equals(_serverConnection))
                        {
                            _serverConnection = HSteamNetConnection.Invalid;
                        }
#endif
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Error in Steam sockets connection status callback: {ex.Message}");
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
                // Ignore malformed challenge payloads.
            }
        }

        private HSteamNetConnection EnsureClientConnection(ulong serverSteamId)
        {
#if SERVER
            return HSteamNetConnection.Invalid;
#else
            if (_steamIdToSocket.TryGetValue(serverSteamId, out HSteamNetConnection existing) && !IsInvalid(existing))
            {
                return existing;
            }

            SteamNetworkingIdentity identity = default;
            identity.SetSteamID64(serverSteamId);
            HSteamNetConnection conn = SteamNetworkingSockets.ConnectP2P(ref identity, _virtualPort, 0, null);
            if (IsInvalid(conn))
            {
                _logger?.Warning($"Failed to connect Steam sockets to server SteamID {serverSteamId}");
                return HSteamNetConnection.Invalid;
            }

            SteamNetworkingSockets.SetConnectionPollGroup(conn, _pollGroup);
            RememberSocket(serverSteamId, conn);
            _serverConnection = conn;
            return conn;
#endif
        }

        private static bool ShouldPreferBootstrapClientToServer(string command)
        {
            return string.Equals(command, DSConstants.Messages.AuthHello, StringComparison.Ordinal) ||
                   string.Equals(command, DSConstants.Messages.AuthTicket, StringComparison.Ordinal) ||
                   string.Equals(command, DSConstants.Messages.RequestServerData, StringComparison.Ordinal);
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

        private void RememberSocket(ulong steamId, HSteamNetConnection conn)
        {
            if (steamId == 0 || IsInvalid(conn))
            {
                return;
            }

            _steamIdToSocket[steamId] = conn;
            _socketHandleToSteamId[conn.m_HSteamNetConnection] = steamId;
        }

        private void ForgetSocket(HSteamNetConnection conn)
        {
            if (IsInvalid(conn))
            {
                return;
            }

            if (_socketHandleToSteamId.TryGetValue(conn.m_HSteamNetConnection, out ulong steamId))
            {
                _socketHandleToSteamId.Remove(conn.m_HSteamNetConnection);
                _steamIdToSocket.Remove(steamId);
            }
        }

        private static string GetLocalSteamIdString()
        {
#if SERVER
            try
            {
                return SteamGameServer.GetSteamID().m_SteamID.ToString(CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
#else
            try
            {
                return SteamUser.GetSteamID().m_SteamID.ToString(CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
#endif
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

#if SERVER
        private bool TrySendFallbackToClient(NetworkConnection conn, string command, string data)
        {
            if (_bootstrapBackend == null)
            {
                return false;
            }

            bool fallbackSent = _bootstrapBackend.SendToClient(conn, command, data);
            if (!_fallbackWarnedClientIds.Contains(conn.ClientId))
            {
                _fallbackWarnedClientIds.Add(conn.ClientId);
                _logger?.Msg($"Steam sockets fallback active for ClientId {conn.ClientId} until socket mapping is available.");
            }

            return fallbackSent;
        }

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

        private bool TryResolveConnectionForInbound(HSteamNetConnection socketConnection, ulong remoteSteamId, SteamEnvelope envelope, out NetworkConnection conn)
        {
            conn = null;

            if (envelope != null && envelope.SenderClientId >= 0 && _clientIdToConnection.TryGetValue(envelope.SenderClientId, out conn))
            {
                if (remoteSteamId != 0)
                {
                    _steamIdToConnection[remoteSteamId] = conn;
                }

                return true;
            }

            if (remoteSteamId != 0 && _steamIdToConnection.TryGetValue(remoteSteamId, out conn))
            {
                return true;
            }

            if (envelope != null && TryParseSteamId(envelope.SenderSteamId, out ulong senderSteamId) && _steamIdToConnection.TryGetValue(senderSteamId, out conn))
            {
                if (remoteSteamId != 0)
                {
                    _steamIdToConnection[remoteSteamId] = conn;
                }

                return true;
            }

            RefreshServerConnectionMappings();

            if (envelope != null && envelope.SenderClientId >= 0 && _clientIdToConnection.TryGetValue(envelope.SenderClientId, out conn))
            {
                return true;
            }

            if (remoteSteamId != 0 && _steamIdToConnection.TryGetValue(remoteSteamId, out conn))
            {
                return true;
            }

            if (envelope != null && TryParseSteamId(envelope.SenderSteamId, out senderSteamId) && _steamIdToConnection.TryGetValue(senderSteamId, out conn))
            {
                return true;
            }

            if (envelope != null && TryParseSteamId(envelope.SenderSteamId, out senderSteamId))
            {
                string steamIdText = senderSteamId.ToString(CultureInfo.InvariantCulture);
                ConnectedPlayerInfo playerInfo = ServerBootstrap.Players?.GetPlayerBySteamId(steamIdText);
                if (playerInfo?.Connection != null)
                {
                    conn = playerInfo.Connection;
                    _steamIdToConnection[senderSteamId] = conn;
                    return true;
                }
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

            return false;
        }
#endif

        private void RunCallbacks()
        {
#if SERVER
            SteamGameServerNetworkingSockets.RunCallbacks();
#else
            SteamNetworkingSockets.RunCallbacks();
#endif
        }

        private HSteamNetPollGroup CreatePollGroup()
        {
#if SERVER
            return SteamGameServerNetworkingSockets.CreatePollGroup();
#else
            return SteamNetworkingSockets.CreatePollGroup();
#endif
        }

        private bool DestroyPollGroup(HSteamNetPollGroup pollGroup)
        {
#if SERVER
            return SteamGameServerNetworkingSockets.DestroyPollGroup(pollGroup);
#else
            return SteamNetworkingSockets.DestroyPollGroup(pollGroup);
#endif
        }

        private bool CloseListenSocket(HSteamListenSocket listenSocket)
        {
#if SERVER
            return SteamGameServerNetworkingSockets.CloseListenSocket(listenSocket);
#else
            return SteamNetworkingSockets.CloseListenSocket(listenSocket);
#endif
        }

        private bool CloseConnection(HSteamNetConnection connection, int reasonCode, string debug, bool linger)
        {
#if SERVER
            return SteamGameServerNetworkingSockets.CloseConnection(connection, reasonCode, debug, linger);
#else
            return SteamNetworkingSockets.CloseConnection(connection, reasonCode, debug, linger);
#endif
        }

        private int ReceiveMessagesOnPollGroup(HSteamNetPollGroup pollGroup, IntPtr[] messages, int maxMessages)
        {
#if SERVER
            return SteamGameServerNetworkingSockets.ReceiveMessagesOnPollGroup(pollGroup, messages, maxMessages);
#else
            return SteamNetworkingSockets.ReceiveMessagesOnPollGroup(pollGroup, messages, maxMessages);
#endif
        }

        private EResult SendMessageToConnection(HSteamNetConnection connection, IntPtr data, uint size, int flags, out long messageNumber)
        {
#if SERVER
            return SteamGameServerNetworkingSockets.SendMessageToConnection(connection, data, size, flags, out messageNumber);
#else
            return SteamNetworkingSockets.SendMessageToConnection(connection, data, size, flags, out messageNumber);
#endif
        }

        private static bool IsInvalid(HSteamListenSocket socket)
        {
            return socket.Equals(HSteamListenSocket.Invalid) || socket.m_HSteamListenSocket == 0;
        }

        private static bool IsInvalid(HSteamNetPollGroup pollGroup)
        {
            return pollGroup.Equals(HSteamNetPollGroup.Invalid) || pollGroup.m_HSteamNetPollGroup == 0;
        }

        private static bool IsInvalid(HSteamNetConnection connection)
        {
            return connection.Equals(HSteamNetConnection.Invalid) || connection.m_HSteamNetConnection == 0;
        }

        private static bool TryParseSteamId(string raw, out ulong value)
        {
            return ulong.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
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
