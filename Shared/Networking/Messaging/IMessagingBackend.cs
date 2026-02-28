using System;
#if IL2CPP
using Il2CppFishNet.Connection;
#else
using FishNet.Connection;
#endif
using MelonLoader;

namespace DedicatedServerMod.Shared.Networking.Messaging
{
    /// <summary>
    /// Contract for pluggable messaging backends.
    /// </summary>
    public interface IMessagingBackend
    {
        /// <summary>
        /// Gets the backend type identifier.
        /// </summary>
        MessagingBackendType BackendType { get; }

        /// <summary>
        /// Gets whether the backend is initialized and available.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Gets whether this backend is available on the current platform/build.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Initializes backend resources.
        /// </summary>
        /// <param name="logger">Logger instance for this backend.</param>
        /// <returns>True if initialization succeeded.</returns>
        bool Initialize(MelonLogger.Instance logger);

        /// <summary>
        /// Shuts down backend resources.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Pumps backend callbacks and processing.
        /// Call regularly (e.g., from OnUpdate).
        /// </summary>
        void Tick();

        /// <summary>
        /// Called when DailySummary.Awake runs (FishNet-specific registration point).
        /// No-op for backends that don't use FishNet RPCs.
        /// </summary>
        /// <param name="instance">The DailySummary NetworkBehaviour instance.</param>
        void OnDailySummaryAwake(object instance);

        /// <summary>
        /// Sends a message from client to server.
        /// </summary>
        /// <param name="command">Message command type.</param>
        /// <param name="data">Message payload data.</param>
        /// <returns>True if message was queued/sent successfully.</returns>
        bool SendToServer(string command, string data);

        /// <summary>
        /// Sends a message from server to a specific client.
        /// </summary>
        /// <param name="conn">Target client connection.</param>
        /// <param name="command">Message command type.</param>
        /// <param name="data">Message payload data.</param>
        /// <returns>True if message was queued/sent successfully.</returns>
        bool SendToClient(NetworkConnection conn, string command, string data);

        /// <summary>
        /// Broadcasts a message from server to all connected clients.
        /// </summary>
        /// <param name="command">Message command type.</param>
        /// <param name="data">Message payload data.</param>
        /// <returns>Number of clients message was sent to.</returns>
        int BroadcastToClients(string command, string data);

        /// <summary>
        /// Raised when a message is received from the server (client-side).
        /// </summary>
        event Action<string, string> ClientMessageReceived;

        /// <summary>
        /// Raised when a message is received from a client (server-side).
        /// </summary>
        event Action<NetworkConnection, string, string> ServerMessageReceived;

        /// <summary>
        /// Gets diagnostic information about the backend state.
        /// </summary>
        /// <returns>Human-readable status string.</returns>
        string GetStatusInfo();

        /// <summary>
        /// Supplies an optional server peer hint used by backends that require
        /// explicit remote identity information (for example, Steam P2P peer routing).
        /// </summary>
        /// <param name="serverSteamId">Server SteamID64 string hint.</param>
        void SetServerPeerHint(string serverSteamId);
    }
}
