using System;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Object;
using Il2CppFishNet.Object.Delegating;
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
#else
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Delegating;
using FishNet.Serializing;
using FishNet.Transporting;
#endif
using MelonLoader;
#if IL2CPP
using Newtonsoft.Json;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
#else
using Newtonsoft.Json;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
#endif

namespace DedicatedServerMod.Shared.Networking.Messaging
{
    /// <summary>
    /// FishNet RPC-based messaging backend.
    /// Uses custom RPCs registered on DailySummary NetworkBehaviour.
    /// </summary>
    public class FishNetRpcMessagingBackend : IMessagingBackend
    {
        private MelonLogger.Instance _logger;
        private bool _isInitialized;
        private uint _messageId;

        /// <inheritdoc />
        public MessagingBackendType BackendType => MessagingBackendType.FishNetRpc;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public bool IsAvailable => true; // Always available on both Mono and IL2CPP

        /// <inheritdoc />
        public event Action<string, string> ClientMessageReceived;

        /// <inheritdoc />
        public event Action<NetworkConnection, string, string> ServerMessageReceived;

        /// <summary>
        /// Represents a custom message structure.
        /// </summary>
        private struct Message
        {
            public string Command;
            public string Data;
        }

        /// <inheritdoc />
        public bool Initialize(MelonLogger.Instance logger)
        {
            if (_isInitialized)
            {
                return true;
            }

            _logger = logger ?? new MelonLogger.Instance("FishNetRpcMessagingBackend");
            _messageId = Constants.CustomMessageID;

            _isInitialized = true;
            _logger.Msg("FishNet RPC messaging backend initialized");
            return true;
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _isInitialized = false;
            _logger?.Msg("FishNet RPC messaging backend shut down");
        }

        /// <inheritdoc />
        public void Tick()
        {
            // FishNet callbacks are pumped automatically; no manual ticking needed
        }

        /// <inheritdoc />
        public void OnDailySummaryAwake(object instance)
        {
            if (!_isInitialized)
            {
                _logger?.Warning("OnDailySummaryAwake called before initialization");
                return;
            }

            try
            {
                var nb = (NetworkBehaviour)instance;

#if IL2CPP
                _logger?.Warning("FishNet RPC registration is not available on IL2CPP in this backend; using transport fallbacks.");
#else
                // Register server→client Target RPC
                nb.RegisterTargetRpc(_messageId, new ClientRpcDelegate(OnClientMessageReceived));

                // Register client→server Server RPC
                nb.RegisterServerRpc(_messageId, new ServerRpcDelegate(OnServerMessageReceived));

                _logger?.Msg("Registered FishNet custom messaging RPCs on DailySummary");
#endif
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to register custom RPCs: {ex}");
            }
        }

        /// <inheritdoc />
        public bool SendToServer(string command, string data)
        {
            if (!_isInitialized)
            {
                _logger?.Warning($"SendToServer skipped: backend not initialized (cmd='{command}')");
                return false;
            }

            try
            {
                var ds = DailySummary.Instance;
                if (ds == null)
                {
                    _logger?.Warning($"SendToServer skipped: DailySummary instance null for cmd='{command}'");
                    return false;
                }

                var nb = (NetworkBehaviour)ds;
                if (!nb.IsSpawned)
                {
                    _logger?.Warning($"SendToServer skipped: DailySummary not spawned yet for cmd='{command}'");
                    return false;
                }

                var msg = new Message { Command = command, Data = data };
                string raw = JsonConvert.SerializeObject(msg);

                PooledWriter writer = WriterPool.Retrieve();
                ((Writer)writer).WriteString(raw);
                nb.SendServerRpc(_messageId, writer, Channel.Reliable, DataOrderType.Default);
                writer.Store();

                _logger?.Msg($"SendToServer cmd='{command}' len={data?.Length ?? 0}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SendToServer error: {ex}");
                return false;
            }
        }

        /// <inheritdoc />
        public bool SendToClient(NetworkConnection conn, string command, string data)
        {
            if (!_isInitialized)
            {
                _logger?.Warning($"SendToClient skipped: backend not initialized (cmd='{command}')");
                return false;
            }

            if (conn == null)
            {
                _logger?.Warning($"SendToClient skipped: connection is null for cmd='{command}'");
                return false;
            }

            try
            {
                var ds = DailySummary.Instance;
                if (ds == null)
                {
                    _logger?.Warning($"SendToClient skipped: DailySummary instance null for cmd='{command}'");
                    return false;
                }

                var msg = new Message { Command = command, Data = data };
                string raw = JsonConvert.SerializeObject(msg);

                PooledWriter writer = WriterPool.Retrieve();
                ((Writer)writer).WriteString(raw);
                ((NetworkBehaviour)ds).SendTargetRpc(_messageId, writer, Channel.Reliable, DataOrderType.Default, conn, false, true);
                writer.Store();

                _logger?.Msg($"SendToClient cmd='{command}' len={data?.Length ?? 0} to={conn.ClientId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"SendToClient error: {ex}");
                return false;
            }
        }

        /// <inheritdoc />
        public int BroadcastToClients(string command, string data)
        {
            if (!_isInitialized)
            {
                _logger?.Warning($"BroadcastToClients skipped: backend not initialized (cmd='{command}')");
                return 0;
            }

            if (!InstanceFinder.IsServer)
            {
                return 0;
            }

            int count = 0;
            try
            {
                var ds = DailySummary.Instance;
                if (ds == null)
                {
                    return 0;
                }

                foreach (var kvp in InstanceFinder.ServerManager.Clients)
                {
                    var client = kvp.Value;
                    if (client != null)
                    {
                        if (SendToClient(client, command, data))
                        {
                            count++;
                        }
                    }
                }

                _logger?.Msg($"BroadcastToClients cmd='{command}' len={data?.Length ?? 0} sentTo={count}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"BroadcastToClients error: {ex}");
            }

            return count;
        }

        /// <inheritdoc />
        public string GetStatusInfo()
        {
            if (!_isInitialized)
            {
                return "Not initialized";
            }

            try
            {
                var ds = DailySummary.Instance;
                bool hasDailySummary = ds != null;
                bool isSpawned = hasDailySummary && ((NetworkBehaviour)ds).IsSpawned;

                return $"DailySummary: {(hasDailySummary ? (isSpawned ? "Spawned" : "Exists") : "Missing")}, MessageID: {_messageId}";
            }
            catch (Exception ex)
            {
                return $"Error getting status: {ex.Message}";
            }
        }

        /// <inheritdoc />
        public void SetServerPeerHint(string serverSteamId)
        {
            // FishNet backend does not require server SteamID hints.
        }

        private void OnClientMessageReceived(PooledReader reader, Channel channel)
        {
            try
            {
                string raw = ((Reader)reader).ReadString();
                var msg = JsonConvert.DeserializeObject<Message>(raw);

                if (msg.Command == null)
                {
                    _logger?.Warning("OnClientMessageReceived: Message command is null");
                    return;
                }

                _logger?.Msg($"OnClientMessageReceived cmd='{msg.Command}' len={msg.Data?.Length ?? 0}");

                try
                {
                    ClientMessageReceived?.Invoke(msg.Command, msg.Data);
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Error in ClientMessageReceived event: {ex}");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"OnClientMessageReceived error: {ex}");
            }
        }

        private void OnServerMessageReceived(PooledReader reader, Channel channel, NetworkConnection conn)
        {
            if (!InstanceFinder.IsServer)
            {
                _logger?.Warning("OnServerMessageReceived: Not on server, ignoring message");
                return;
            }

            try
            {
                string raw = ((Reader)reader).ReadString();
                var msg = JsonConvert.DeserializeObject<Message>(raw);

                if (msg.Command == null)
                {
                    _logger?.Warning("OnServerMessageReceived: Message command is null");
                    return;
                }

                _logger?.Msg($"OnServerMessageReceived cmd='{msg.Command}' len={msg.Data?.Length ?? 0} from={conn?.ClientId}");

                try
                {
                    ServerMessageReceived?.Invoke(conn, msg.Command, msg.Data);
                }
                catch (Exception ex)
                {
                    _logger?.Error($"Error in ServerMessageReceived event: {ex}");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"OnServerMessageReceived error: {ex}");
            }
        }
    }
}
