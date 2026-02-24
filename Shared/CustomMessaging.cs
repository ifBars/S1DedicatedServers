using System;
using DedicatedServerMod.Shared.Networking.Messaging;
using FishNet.Connection;
using MelonLoader;

namespace DedicatedServerMod.Shared.Networking
{
    /// <summary>
    /// Shared custom messaging hub for server-client communication.
    /// 
    /// This class provides a stable public API that delegates to the pluggable
    /// messaging backend system. The actual transport implementation is handled
    /// by <see cref="MessagingService"/> and its configured backend.
    /// </summary>
    /// <remarks>
    /// This class maintains backward compatibility with existing code while
    /// allowing runtime selection of messaging backend (FishNet RPC or Steam P2P).
    /// </remarks>
    public static class CustomMessaging
    {
        private static MelonLogger.Instance _logger = new MelonLogger.Instance("CustomMessaging");
        private static bool _eventsWired;

        #region API Events

        /// <summary>
        /// Raised when a custom message is received from the server (client-side).
        /// </summary>
        public static event Action<string, string> ClientMessageReceived
        {
            add
            {
                EnsureEventsWired();
                MessagingService.ClientMessageReceived += value;
            }
            remove
            {
                MessagingService.ClientMessageReceived -= value;
            }
        }

        /// <summary>
        /// Raised when a custom message is received from a client (server-side).
        /// </summary>
        public static event Action<NetworkConnection, string, string> ServerMessageReceived
        {
            add
            {
                EnsureEventsWired();
                MessagingService.ServerMessageReceived += value;
            }
            remove
            {
                MessagingService.ServerMessageReceived -= value;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the custom messaging system.
        /// Should be called during server/client startup.
        /// </summary>
        public static void Initialize()
        {
            _logger.Msg("CustomMessaging initializing via MessagingService");
            MessagingService.Initialize(_logger);
            EnsureEventsWired();
        }

        /// <summary>
        /// Shuts down the custom messaging system.
        /// </summary>
        public static void Shutdown()
        {
            _logger.Msg("CustomMessaging shutting down");
            MessagingService.Shutdown();
        }

        /// <summary>
        /// Pumps backend callbacks. Call regularly from OnUpdate.
        /// </summary>
        public static void Tick()
        {
            MessagingService.Tick();
        }

        #endregion

        #region RPC Registration

        /// <summary>
        /// Harmony postfix for DailySummary.Awake. Delegates to backend for registration.
        /// </summary>
        /// <param name="instance">The DailySummary instance being initialized</param>
        public static void DailySummaryAwakePostfix(object instance)
        {
            MessagingService.OnDailySummaryAwake(instance);
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
            MessagingService.SendToServer(command, data);
        }

        /// <summary>
        /// Sends a message from server to a specific client.
        /// </summary>
        /// <param name="conn">The target connection</param>
        /// <param name="command">The message command type</param>
        /// <param name="data">The message payload</param>
        public static void SendToClient(NetworkConnection conn, string command, string data = "")
        {
            MessagingService.SendToClient(conn, command, data);
        }

        /// <summary>
        /// Broadcasts a message from server to all connected clients.
        /// </summary>
        /// <param name="command">The message command type</param>
        /// <param name="data">The message payload</param>
        public static void BroadcastToClients(string command, string data = "")
        {
            MessagingService.BroadcastToClients(command, data);
        }

        /// <summary>
        /// Supplies a server peer hint used by peer-addressed messaging backends.
        /// </summary>
        /// <param name="serverSteamId">Server SteamID64 hint.</param>
        public static void SetServerPeerHint(string serverSteamId)
        {
            MessagingService.SetServerPeerHint(serverSteamId);
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

        #region Private Helpers

        private static void EnsureEventsWired()
        {
            if (_eventsWired)
            {
                return;
            }

            // Wire up backend events to route through this class for backward compatibility
            MessagingService.ClientMessageReceived += (cmd, data) =>
            {
                try
                {
                    MessageRouter.RouteClientMessage(cmd, data);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error routing client message: {ex}");
                }
            };

            MessagingService.ServerMessageReceived += (conn, cmd, data) =>
            {
                try
                {
                    MessageRouter.RouteServerMessage(conn, cmd, data);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error routing server message: {ex}");
                }
            };

            _eventsWired = true;
        }

        #endregion
    }
}
