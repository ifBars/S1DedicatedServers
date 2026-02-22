using System;
using DedicatedServerMod.Utils;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Delegating;
using FishNet.Serializing;
using FishNet.Transporting;
using MelonLoader;
using Newtonsoft.Json;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;

namespace DedicatedServerMod.Shared.Networking
{
    /// <summary>
    /// Shared custom messaging hub for server-client communication.
    /// Registers custom RPCs on an existing NetworkBehaviour (DailySummary)
    /// and provides helpers to send and receive messages.
    /// </summary>
    /// <remarks>
    /// This class handles the low-level networking aspects of custom messaging:
    /// - RPC registration on DailySummary
    /// - Message sending (server→client, client→server, broadcast)
    /// - Message deserialization
    /// 
    /// Message routing and command execution is delegated to <see cref="MessageRouter"/>
    /// for separation of concerns.
    /// </remarks>
    public static class CustomMessaging
    {
        #region Message Structure

        /// <summary>
        /// Represents a custom message sent between server and client.
        /// </summary>
        public struct Message
        {
            public string Command;
            public string Data;
        }

        #endregion

        #region Private Fields

        /// <summary>
        /// The logger instance for this messaging system.
        /// </summary>
        private static MelonLogger.Instance _logger = new MelonLogger.Instance("CustomMessaging");

        /// <summary>
        /// The message ID used for RPC registration.
        /// </summary>
        public static readonly uint MessageId = Constants.CustomMessageID;

        #endregion

        #region API Events

        /// <summary>
        /// Raised when a custom message is received from the server (client-side).
        /// </summary>
        public static event Action<string, string> ClientMessageReceived;

        /// <summary>
        /// Raised when a custom message is received from a client (server-side).
        /// </summary>
        public static event Action<NetworkConnection, string, string> ServerMessageReceived;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the custom messaging system.
        /// Should be called during server/client startup.
        /// </summary>
        public static void Initialize()
        {
            _logger.Msg("CustomMessaging system initialized");
        }

        #endregion

        #region RPC Registration

        /// <summary>
        /// Harmony postfix for DailySummary.Awake. Registers custom RPC handlers.
        /// </summary>
        /// <param name="__instance">The DailySummary instance being initialized</param>
        public static void DailySummaryAwakePostfix(object instance)
        {
            try
            {
                var nb = (NetworkBehaviour)instance;

                // Register server→client Target RPC
                nb.RegisterTargetRpc(MessageId, new ClientRpcDelegate(OnClientMessageReceived));

                // Register client→server Server RPC
                nb.RegisterServerRpc(MessageId, new ServerRpcDelegate(OnServerMessageReceived));

                _logger.Msg("Registered custom messaging RPCs on DailySummary");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to register custom RPCs: {ex}");
            }
        }

        #endregion

        #region Send Methods

        /// <summary>
        /// Sends a message from client to server.
        /// </summary>
        /// <param name="command">The message command type</param>
        /// <param name="data">The message payload</param>
        public static void SendToServer(string command, string data = "")
        {
            try
            {
                var ds = DailySummary.Instance;
                if (ds == null)
                {
                    _logger.Warning($"SendToServer skipped: DailySummary instance null for cmd='{command}'");
                    return;
                }
                
                var nb = (NetworkBehaviour)ds;
                if (!nb.IsSpawned)
                {
                    _logger.Warning($"SendToServer skipped: DailySummary not spawned yet for cmd='{command}'");
                    return;
                }

                var msg = new Message { Command = command, Data = data };
                string raw = JsonConvert.SerializeObject(msg);

                PooledWriter writer = WriterPool.Retrieve();
                ((Writer)writer).WriteString(raw);
                nb.SendServerRpc(MessageId, writer, Channel.Reliable, DataOrderType.Default);
                writer.Store();

                _logger.Msg($"SendToServer cmd='{command}' len={data?.Length ?? 0}");
            }
            catch (Exception ex)
            {
                _logger.Error($"SendToServer error: {ex}");
            }
        }

        /// <summary>
        /// Sends a message from server to a specific client.
        /// </summary>
        /// <param name="conn">The target connection</param>
        /// <param name="command">The message command type</param>
        /// <param name="data">The message payload</param>
        public static void SendToClient(NetworkConnection conn, string command, string data = "")
        {
            try
            {
                var ds = DailySummary.Instance;
                if (ds == null || conn == null)
                {
                    _logger.Warning($"SendToClient skipped: ds null? {ds==null}, conn null? {conn==null} for cmd='{command}'");
                    return;
                }

                var msg = new Message { Command = command, Data = data };
                string raw = JsonConvert.SerializeObject(msg);

                PooledWriter writer = WriterPool.Retrieve();
                ((Writer)writer).WriteString(raw);
                ((NetworkBehaviour)ds).SendTargetRpc(MessageId, writer, Channel.Reliable, DataOrderType.Default, conn, false, true);
                writer.Store();

                _logger.Msg($"SendToClient cmd='{command}' len={data?.Length ?? 0} to={conn.ClientId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"SendToClient error: {ex}");
            }
        }

        /// <summary>
        /// Broadcasts a message from server to all connected clients.
        /// </summary>
        /// <param name="command">The message command type</param>
        /// <param name="data">The message payload</param>
        public static void BroadcastToClients(string command, string data = "")
        {
            try
            {
                var ds = DailySummary.Instance;
                if (ds == null || !InstanceFinder.IsServer)
                    return;

                foreach (var kvp in InstanceFinder.ServerManager.Clients)
                {
                    var client = kvp.Value;
                    if (client != null)
                    {
                        SendToClient(client, command, data);
                    }
                }

                _logger.Msg($"BroadcastToClients cmd='{command}' len={data?.Length ?? 0}");
            }
            catch (Exception ex)
            {
                _logger.Error($"BroadcastToClients error: {ex}");
            }
        }

        #endregion

        #region Receive Handlers

        /// <summary>
        /// Handles messages received from the server (client-side).
        /// </summary>
        private static void OnClientMessageReceived(PooledReader reader, Channel channel)
        {
            try
            {
                string raw = ((Reader)reader).ReadString();
                var msg = JsonConvert.DeserializeObject<Message>(raw);

                if (msg.Command == null)
                {
                    _logger.Warning("OnClientMessageReceived: Message command is null");
                    return;
                }

                _logger.Msg($"OnClientMessageReceived cmd='{msg.Command}' len={msg.Data?.Length ?? 0}");

                // Raise API event for mods
                try
                {
                    ClientMessageReceived?.Invoke(msg.Command, msg.Data);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error in ClientMessageReceived event: {ex}");
                }

                // Delegate to message router for built-in handling
                MessageRouter.RouteClientMessage(msg.Command, msg.Data);
            }
            catch (Exception ex)
            {
                _logger.Error($"OnClientMessageReceived error: {ex}");
            }
        }

        /// <summary>
        /// Handles messages received from clients (server-side).
        /// </summary>
        private static void OnServerMessageReceived(PooledReader reader, Channel channel, NetworkConnection conn)
        {
            if (!InstanceFinder.IsServer)
            {
                _logger.Warning("OnServerMessageReceived: Not on server, ignoring message");
                return;
            }

            try
            {
                string raw = ((Reader)reader).ReadString();
                var msg = JsonConvert.DeserializeObject<Message>(raw);

                if (msg.Command == null)
                {
                    _logger.Warning("OnServerMessageReceived: Message command is null");
                    return;
                }

                // Log verbose messages with Msg (MelonLogger doesn't have Verbose)
                _logger.Msg($"OnServerMessageReceived cmd='{msg.Command}' len={msg.Data?.Length ?? 0} from={conn?.ClientId}");

                // Raise API event for server mods
                try
                {
                    ServerMessageReceived?.Invoke(conn, msg.Command, msg.Data);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error in ServerMessageReceived event: {ex}");
                }

                // Delegate to message router for built-in handling
                MessageRouter.RouteServerMessage(conn, msg.Command, msg.Data);
            }
            catch (Exception ex)
            {
                _logger.Error($"OnServerMessageReceived error: {ex}");
            }
        }

        #endregion

        #region Console Command Initialization

        /// <summary>
        /// Initializes console commands if they're not already initialized.
        /// Critical for dedicated servers where Console.Awake may not run.
        /// </summary>
        /// <param name="commands">The commands dictionary to populate</param>
        public static void InitializeConsoleCommands(System.Collections.Generic.Dictionary<string, ScheduleOne.Console.ConsoleCommand> commands)
        {
            MessageRouter.Initialize(_logger);
        }

        #endregion
    }
}
