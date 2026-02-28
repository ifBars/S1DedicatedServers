using System;
using DedicatedServerMod.Utils;
#if IL2CPP
using Il2CppFishNet.Connection;
#else
using FishNet.Connection;
#endif
using MelonLoader;

namespace DedicatedServerMod.Shared.Networking.Messaging
{
    /// <summary>
    /// Central service managing messaging backend selection and lifecycle.
    /// Provides a unified API that delegates to the active backend implementation.
    /// </summary>
    public static class MessagingService
    {
        private static MelonLogger.Instance _logger;
        private static IMessagingBackend _backend;
        private static bool _isInitialized;

        /// <summary>
        /// Gets whether the messaging service is initialized.
        /// </summary>
        public static bool IsInitialized => _isInitialized && _backend?.IsInitialized == true;

        /// <summary>
        /// Gets the currently active backend type.
        /// </summary>
        public static MessagingBackendType? ActiveBackendType => _backend?.BackendType;

        /// <summary>
        /// Raised when a message is received from the server (client-side).
        /// </summary>
        public static event Action<string, string> ClientMessageReceived;

        /// <summary>
        /// Raised when a message is received from a client (server-side).
        /// </summary>
        public static event Action<NetworkConnection, string, string> ServerMessageReceived;

        /// <summary>
        /// Initializes the messaging service with the configured backend.
        /// </summary>
        /// <param name="logger">Logger instance for messaging system.</param>
        /// <returns>True if initialization succeeded.</returns>
        public static bool Initialize(MelonLogger.Instance logger)
        {
            if (_isInitialized)
            {
                logger?.Msg("MessagingService already initialized");
                return true;
            }

            _logger = logger ?? new MelonLogger.Instance("MessagingService");

            try
            {
                MessagingBackendType backendType = DedicatedServerMod.Shared.Configuration.ServerConfig.Instance.MessagingBackend;
                _logger.Msg($"Initializing messaging service with backend: {backendType}");

                _backend = CreateBackend(backendType);
                if (_backend == null)
                {
                    _logger.Error($"Failed to create messaging backend: {backendType}");
                    return false;
                }

                if (!_backend.IsAvailable)
                {
                    _logger.Error($"Messaging backend {backendType} is not available on this platform");
                    return false;
                }

                // Wire up backend events
                _backend.ClientMessageReceived += OnBackendClientMessageReceived;
                _backend.ServerMessageReceived += OnBackendServerMessageReceived;

                bool success = _backend.Initialize(_logger);
                if (!success)
                {
                    _logger.Error($"Failed to initialize messaging backend: {backendType}");
                    return false;
                }

                _isInitialized = true;
                _logger.Msg($"Messaging service initialized with backend: {backendType}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error initializing messaging service: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Shuts down the messaging service and active backend.
        /// </summary>
        public static void Shutdown()
        {
            if (!_isInitialized || _backend == null)
            {
                return;
            }

            try
            {
                _backend.ClientMessageReceived -= OnBackendClientMessageReceived;
                _backend.ServerMessageReceived -= OnBackendServerMessageReceived;
                _backend.Shutdown();
                _logger?.Msg("Messaging service shut down");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error shutting down messaging service: {ex}");
            }
            finally
            {
                _backend = null;
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Pumps backend callbacks and processing.
        /// Call regularly from OnUpdate.
        /// </summary>
        public static void Tick()
        {
            if (!_isInitialized || _backend == null)
            {
                return;
            }

            try
            {
                _backend.Tick();
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in messaging service tick: {ex}");
            }
        }

        /// <summary>
        /// Called when DailySummary.Awake runs.
        /// Delegates to backend for FishNet-specific registration.
        /// </summary>
        /// <param name="instance">The DailySummary NetworkBehaviour instance.</param>
        public static void OnDailySummaryAwake(object instance)
        {
            if (!_isInitialized || _backend == null)
            {
                return;
            }

            try
            {
                _backend.OnDailySummaryAwake(instance);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in DailySummaryAwake: {ex}");
            }
        }

        /// <summary>
        /// Sends a message from client to server.
        /// </summary>
        /// <param name="command">Message command type.</param>
        /// <param name="data">Message payload data.</param>
        /// <returns>True if message was queued/sent successfully.</returns>
        public static bool SendToServer(string command, string data)
        {
            if (!_isInitialized || _backend == null)
            {
                _logger?.Warning($"SendToServer skipped: messaging not initialized (cmd='{command}')");
                return false;
            }

            try
            {
                return _backend.SendToServer(command, data);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in SendToServer: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Sends a message from server to a specific client.
        /// </summary>
        /// <param name="conn">Target client connection.</param>
        /// <param name="command">Message command type.</param>
        /// <param name="data">Message payload data.</param>
        /// <returns>True if message was queued/sent successfully.</returns>
        public static bool SendToClient(NetworkConnection conn, string command, string data)
        {
            if (!_isInitialized || _backend == null)
            {
                _logger?.Warning($"SendToClient skipped: messaging not initialized (cmd='{command}')");
                return false;
            }

            if (conn == null)
            {
                _logger?.Warning($"SendToClient skipped: connection is null (cmd='{command}')");
                return false;
            }

            try
            {
                return _backend.SendToClient(conn, command, data);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in SendToClient: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Broadcasts a message from server to all connected clients.
        /// </summary>
        /// <param name="command">Message command type.</param>
        /// <param name="data">Message payload data.</param>
        /// <returns>Number of clients message was sent to.</returns>
        public static int BroadcastToClients(string command, string data)
        {
            if (!_isInitialized || _backend == null)
            {
                _logger?.Warning($"BroadcastToClients skipped: messaging not initialized (cmd='{command}')");
                return 0;
            }

            try
            {
                return _backend.BroadcastToClients(command, data);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in BroadcastToClients: {ex}");
                return 0;
            }
        }

        /// <summary>
        /// Gets diagnostic information about the messaging system.
        /// </summary>
        /// <returns>Human-readable status string.</returns>
        public static string GetStatusInfo()
        {
            if (!_isInitialized || _backend == null)
            {
                return "Messaging service not initialized";
            }

            try
            {
                return $"Backend: {_backend.BackendType}\n{_backend.GetStatusInfo()}";
            }
            catch (Exception ex)
            {
                return $"Error getting status: {ex.Message}";
            }
        }

        /// <summary>
        /// Supplies a server peer hint to the active backend.
        /// </summary>
        /// <param name="serverSteamId">Server SteamID64 hint.</param>
        public static void SetServerPeerHint(string serverSteamId)
        {
            if (!_isInitialized || _backend == null || string.IsNullOrWhiteSpace(serverSteamId))
            {
                return;
            }

            try
            {
                _backend.SetServerPeerHint(serverSteamId);
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Failed to apply server peer hint '{serverSteamId}': {ex.Message}");
            }
        }

        private static IMessagingBackend CreateBackend(MessagingBackendType backendType)
        {
            switch (backendType)
            {
                case MessagingBackendType.FishNetRpc:
                    return new FishNetRpcMessagingBackend();
                case MessagingBackendType.SteamP2P:
                    return new SteamP2PMessagingBackend();
                case MessagingBackendType.SteamNetworkingSockets:
                    return new SteamNetworkingSocketsMessagingBackend();
                default:
                    _logger?.Warning($"Unknown messaging backend type: {backendType}, defaulting to FishNetRpc");
                    return new FishNetRpcMessagingBackend();
            }
        }

        private static void OnBackendClientMessageReceived(string command, string data)
        {
            try
            {
                ClientMessageReceived?.Invoke(command, data);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in ClientMessageReceived event: {ex}");
            }
        }

        private static void OnBackendServerMessageReceived(NetworkConnection conn, string command, string data)
        {
            try
            {
                ServerMessageReceived?.Invoke(conn, command, data);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error in ServerMessageReceived event: {ex}");
            }
        }
    }
}
